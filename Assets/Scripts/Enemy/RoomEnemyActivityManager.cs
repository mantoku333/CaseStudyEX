using System.Collections.Generic;
using GameName.Enemy;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RoomEnemyActivityManager : MonoBehaviour
{
    private const string RuntimeObjectName = "[RoomEnemyActivityManager]";
    private const float PlayerRoomRefreshInterval = 0.1f;

    private static RoomEnemyActivityManager instance;

    private readonly List<ManagedEnemy> managedEnemies = new List<ManagedEnemy>();
    private RoomCameraTrigger[] roomTriggers = new RoomCameraTrigger[0];
    private Scene managedScene;
    private bool sceneHasPrologueSource;
    private bool gatingActive;
    private Transform playerTransform;
    private RoomCameraTrigger inferredActiveRoom;
    private float nextPlayerRoomRefreshTime;

    private sealed class ManagedEnemy
    {
        public EnemyController Enemy;
        public GameObject Root;
        public RoomCameraTrigger Room;
        public bool InitialActiveSelf;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RuntimeInitialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        EnsureInstance();
        instance.RefreshForCurrentScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureInstance();
        instance.RefreshForCurrentScene();
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject managerObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(managerObject);
        instance = managerObject.AddComponent<RoomEnemyActivityManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        RoomCameraTrigger.ActiveRoomChanged += HandleActiveRoomChanged;
    }

    private void OnDisable()
    {
        RoomCameraTrigger.ActiveRoomChanged -= HandleActiveRoomChanged;
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        instance = null;
    }

    private void Update()
    {
        bool shouldGate = ShouldGateCurrentScene();
        bool shouldApply = shouldGate != gatingActive;
        gatingActive = shouldGate;

        if (gatingActive)
        {
            RefreshInferredActiveRoomIfNeeded(ref shouldApply);
        }

        if (shouldApply)
        {
            ApplyEnemyActivity();
        }
    }

    private void RefreshForCurrentScene()
    {
        managedScene = SceneManager.GetActiveScene();
        sceneHasPrologueSource = HasScenePrologueSource(managedScene);
        roomTriggers = FindRoomTriggers(managedScene);
        playerTransform = null;
        inferredActiveRoom = ResolveActiveRoomFromPlayer();
        nextPlayerRoomRefreshTime = Time.unscaledTime + PlayerRoomRefreshInterval;

        managedEnemies.Clear();
        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyController enemy = enemies[i];
            if (!CanManageEnemy(enemy, managedScene))
            {
                continue;
            }

            managedEnemies.Add(new ManagedEnemy
            {
                Enemy = enemy,
                Root = enemy.gameObject,
                Room = ResolveRoomForPosition(enemy.transform.position),
                InitialActiveSelf = enemy.gameObject.activeSelf
            });
        }

        gatingActive = ShouldGateCurrentScene();
        ApplyEnemyActivity();
    }

    private void HandleActiveRoomChanged(RoomCameraTrigger activeRoom)
    {
        inferredActiveRoom = activeRoom == null ? ResolveActiveRoomFromPlayer() : null;
        ApplyEnemyActivity();
    }

    private bool ShouldGateCurrentScene()
    {
        if (!managedScene.IsValid() || !managedScene.isLoaded)
        {
            return false;
        }

        if (!sceneHasPrologueSource)
        {
            return true;
        }

        return GameProgressFlags.Get(GameProgressKeys.PrologueCompleted);
    }

    private void ApplyEnemyActivity()
    {
        if (!gatingActive)
        {
            RestoreInitialEnemyActivity();
            return;
        }

        RoomCameraTrigger activeRoom = ResolveActiveRoom();
        if (activeRoom == null)
        {
            RestoreInitialEnemyActivity();
            return;
        }

        for (int i = managedEnemies.Count - 1; i >= 0; i--)
        {
            ManagedEnemy managedEnemy = managedEnemies[i];
            if (!IsManagedEnemyValid(managedEnemy))
            {
                managedEnemies.RemoveAt(i);
                continue;
            }

            bool shouldBeActive = managedEnemy.Room == null ||
                                  managedEnemy.Room == activeRoom;
            managedEnemy.Root.SetActive(managedEnemy.InitialActiveSelf && shouldBeActive);
        }
    }

    private void RefreshInferredActiveRoomIfNeeded(ref bool shouldApply)
    {
        if (RoomCameraTrigger.ActiveRoom != null)
        {
            if (inferredActiveRoom != null)
            {
                inferredActiveRoom = null;
            }

            return;
        }

        if (Time.unscaledTime < nextPlayerRoomRefreshTime)
        {
            return;
        }

        nextPlayerRoomRefreshTime = Time.unscaledTime + PlayerRoomRefreshInterval;
        RoomCameraTrigger activeRoom = ResolveActiveRoomFromPlayer();
        if (activeRoom == inferredActiveRoom)
        {
            return;
        }

        inferredActiveRoom = activeRoom;
        shouldApply = true;
    }

    private RoomCameraTrigger ResolveActiveRoom()
    {
        if (RoomCameraTrigger.ActiveRoom != null)
        {
            return RoomCameraTrigger.ActiveRoom;
        }

        return inferredActiveRoom != null && inferredActiveRoom.isActiveAndEnabled ? inferredActiveRoom : null;
    }

    private RoomCameraTrigger ResolveActiveRoomFromPlayer()
    {
        return TryGetPlayerPosition(out Vector3 playerPosition)
            ? ResolveRoomForPosition(playerPosition)
            : null;
    }

    private bool TryGetPlayerPosition(out Vector3 playerPosition)
    {
        if (playerTransform != null &&
            playerTransform.gameObject.scene == managedScene &&
            playerTransform.gameObject.activeInHierarchy)
        {
            playerPosition = playerTransform.position;
            return true;
        }

        playerTransform = null;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null && taggedPlayer.scene == managedScene)
        {
            playerTransform = taggedPlayer.transform;
            playerPosition = playerTransform.position;
            return true;
        }

        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerHealth player = players[i];
            if (player == null || player.gameObject.scene != managedScene)
            {
                continue;
            }

            playerTransform = player.transform;
            playerPosition = playerTransform.position;
            return true;
        }

        playerPosition = default;
        return false;
    }

    private void RestoreInitialEnemyActivity()
    {
        for (int i = managedEnemies.Count - 1; i >= 0; i--)
        {
            ManagedEnemy managedEnemy = managedEnemies[i];
            if (!IsManagedEnemyValid(managedEnemy))
            {
                managedEnemies.RemoveAt(i);
                continue;
            }

            managedEnemy.Root.SetActive(managedEnemy.InitialActiveSelf);
        }
    }

    private RoomCameraTrigger ResolveRoomForPosition(Vector3 position)
    {
        for (int i = 0; i < roomTriggers.Length; i++)
        {
            RoomCameraTrigger roomTrigger = roomTriggers[i];
            if (roomTrigger != null && roomTrigger.ContainsPoint(position))
            {
                return roomTrigger;
            }
        }

        return null;
    }

    private static bool HasScenePrologueSource(Scene scene)
    {
        SceneStartStoryEventSource[] sources = FindObjectsByType<SceneStartStoryEventSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
        {
            SceneStartStoryEventSource source = sources[i];
            if (source != null && source.gameObject.scene == scene)
            {
                return true;
            }
        }

        return false;
    }

    private static RoomCameraTrigger[] FindRoomTriggers(Scene scene)
    {
        RoomCameraTrigger[] allTriggers = FindObjectsByType<RoomCameraTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<RoomCameraTrigger> validTriggers = new List<RoomCameraTrigger>(allTriggers.Length);
        for (int i = 0; i < allTriggers.Length; i++)
        {
            RoomCameraTrigger trigger = allTriggers[i];
            if (trigger == null ||
                trigger.gameObject.scene != scene ||
                !trigger.isActiveAndEnabled ||
                trigger.UsesDefaultCameraWhenEntered ||
                !trigger.HasAssignedRoomCamera)
            {
                continue;
            }

            validTriggers.Add(trigger);
        }

        return validTriggers.ToArray();
    }

    private static bool CanManageEnemy(EnemyController enemy, Scene scene)
    {
        return enemy != null &&
               enemy.gameObject.scene == scene &&
               enemy.GetComponent<StageBossAttack>() == null &&
               enemy.GetComponent<LastBossController>() == null;
    }

    private static bool IsManagedEnemyValid(ManagedEnemy managedEnemy)
    {
        return managedEnemy != null &&
               managedEnemy.Enemy != null &&
               managedEnemy.Root != null;
    }
}
