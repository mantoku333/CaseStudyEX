using System.Collections.Generic;
using GameName.Enemy;
using Metroidvania.Enemy;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class RoomEnemyActivityManager : MonoBehaviour
{
    private const string RuntimeObjectName = "[RoomEnemyActivityManager]";
    private const float PlayerRoomRefreshInterval = 0.1f;

    private static RoomEnemyActivityManager instance;

    [SerializeField, Min(1)] private int maxEnemyStateChangesPerFrame = 8;
    [SerializeField] private bool debugLogging;

    private readonly List<ManagedEnemy> managedEnemies = new List<ManagedEnemy>();
    private readonly List<RoomCameraTrigger> roomTriggers = new List<RoomCameraTrigger>();
    private Scene managedScene;
    private bool sceneHasPrologueSource;
    private bool gatingActive;
    private Transform playerTransform;
    private Transform cachedPlayerColliderRoot;
    private readonly List<Collider2D> cachedPlayerColliders = new List<Collider2D>();
    private RoomCameraTrigger inferredActiveRoom;
    private RoomCameraTrigger lastResolvedActiveRoom;
    private float nextPlayerRoomRefreshTime;
    private bool hasPendingEnemyStateChanges;
    private int nextEnemyStateChangeIndex;

    private sealed class ManagedEnemy
    {
        public EnemyController Enemy;
        public GameObject Root;
        public RoomCameraTrigger Room;
        // 所属ルームは変えず、ルーム外へ出たときの帰還先だけを固定する。
        public Vector3 OriginalStartPosition;
        public bool InitialActiveSelf;
        public bool DesiredGameplayActive;
        public bool AppliedGameplayActive;
        // 帰還開始時にタックル状態を止めるため、事前に参照を保持しておく。
        public readonly List<EnemyTackleAttack> TackleAttacks = new List<EnemyTackleAttack>();
        public readonly List<MonoBehaviour> GameplayBehaviours = new List<MonoBehaviour>();
        public readonly List<bool> InitialBehaviourEnabled = new List<bool>();
        public readonly List<Rigidbody2D> Rigidbodies = new List<Rigidbody2D>();
        public readonly List<bool> InitialRigidbodySimulated = new List<bool>();
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

        ProcessPendingEnemyStateChanges(maxEnemyStateChangesPerFrame);
    }

    private void RefreshForCurrentScene()
    {
        managedScene = SceneManager.GetActiveScene();
        sceneHasPrologueSource = HasScenePrologueSource(managedScene);
        FindRoomTriggers(managedScene, roomTriggers);
        playerTransform = null;
        cachedPlayerColliderRoot = null;
        cachedPlayerColliders.Clear();
        inferredActiveRoom = ResolveActiveRoomFromPlayer();
        lastResolvedActiveRoom = inferredActiveRoom;
        nextPlayerRoomRefreshTime = Time.unscaledTime + PlayerRoomRefreshInterval;
        hasPendingEnemyStateChanges = false;
        nextEnemyStateChangeIndex = 0;

        managedEnemies.Clear();
        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyController enemy = enemies[i];
            if (!CanManageEnemy(enemy, managedScene))
            {
                continue;
            }

            RoomCameraTrigger enemyRoom = ResolveRoomForPosition(enemy.transform.position);
            if (enemyRoom == null)
            {
                continue;
            }

            managedEnemies.Add(new ManagedEnemy
            {
                Enemy = enemy,
                Root = enemy.gameObject,
                Room = enemyRoom,
                OriginalStartPosition = enemy.OriginalStartPosition,
                InitialActiveSelf = enemy.gameObject.activeSelf,
                DesiredGameplayActive = enemy.gameObject.activeSelf,
                AppliedGameplayActive = enemy.gameObject.activeSelf,
            });

            ManagedEnemy managedEnemy = managedEnemies[managedEnemies.Count - 1];
            enemy.GetComponentsInChildren(true, managedEnemy.TackleAttacks);
            CollectGameplayBehaviours(enemy.gameObject, managedEnemy.GameplayBehaviours);
            enemy.GetComponentsInChildren(true, managedEnemy.Rigidbodies);
            CaptureBehaviourEnabledStates(managedEnemy.GameplayBehaviours, managedEnemy.InitialBehaviourEnabled);
            CaptureRigidbodySimulatedStates(managedEnemy.Rigidbodies, managedEnemy.InitialRigidbodySimulated);
        }

        gatingActive = ShouldGateCurrentScene();
        ApplyEnemyActivity();
    }

    private void HandleActiveRoomChanged(RoomCameraTrigger activeRoom)
    {
        if (activeRoom != null)
        {
            RememberResolvedActiveRoom(activeRoom);
            inferredActiveRoom = null;
        }
        else
        {
            RoomCameraTrigger roomFromPlayer = ResolveActiveRoomFromPlayer();
            if (roomFromPlayer != null)
            {
                inferredActiveRoom = roomFromPlayer;
                RememberResolvedActiveRoom(roomFromPlayer);
            }
        }

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
            QueueInitialEnemyActivityRestore();
            return;
        }

        bool hasPlayerPosition = TryGetPlayerPosition(out Vector3 playerPosition);
        // カメラ用のアクティブルームは、敵の所属変更ではなく重なり部から戻す判定にだけ使う。
        RoomCameraTrigger activeRoom = ResolveActiveRoom();
        bool shouldQueueStateApplication = false;

        for (int i = managedEnemies.Count - 1; i >= 0; i--)
        {
            ManagedEnemy managedEnemy = managedEnemies[i];
            if (!IsManagedEnemyValid(managedEnemy))
            {
                managedEnemies.RemoveAt(i);
                continue;
            }

            if (!managedEnemy.InitialActiveSelf)
            {
                shouldQueueStateApplication |= SetDesiredGameplayActive(managedEnemy, false);
                shouldQueueStateApplication |= managedEnemy.AppliedGameplayActive != managedEnemy.DesiredGameplayActive;
                continue;
            }

            bool shouldRunGameplay = ShouldRunManagedEnemyGameplay(
                managedEnemy,
                activeRoom,
                hasPlayerPosition,
                playerPosition);

            shouldQueueStateApplication |= SetDesiredGameplayActive(managedEnemy, shouldRunGameplay);
            shouldQueueStateApplication |= managedEnemy.AppliedGameplayActive != managedEnemy.DesiredGameplayActive;
        }

        if (shouldQueueStateApplication)
        {
            QueueEnemyStateApplication();
        }
    }

    private void RefreshInferredActiveRoomIfNeeded(ref bool shouldApply)
    {
        if (Time.unscaledTime < nextPlayerRoomRefreshTime)
        {
            return;
        }

        nextPlayerRoomRefreshTime = Time.unscaledTime + PlayerRoomRefreshInterval;
        shouldApply = true;

        if (RoomCameraTrigger.ActiveRoom != null)
        {
            if (inferredActiveRoom != null)
            {
                inferredActiveRoom = null;
            }

            RememberResolvedActiveRoom(RoomCameraTrigger.ActiveRoom);
            return;
        }

        RoomCameraTrigger activeRoom = ResolveActiveRoomFromPlayer();
        if (activeRoom == null)
        {
            return;
        }

        if (activeRoom == inferredActiveRoom)
        {
            return;
        }

        inferredActiveRoom = activeRoom;
        RememberResolvedActiveRoom(activeRoom);
    }

    private bool SetDesiredGameplayActive(ManagedEnemy managedEnemy, bool active)
    {
        if (managedEnemy.DesiredGameplayActive == active)
        {
            return false;
        }

        managedEnemy.DesiredGameplayActive = active;
        return true;
    }

    private bool ShouldRunManagedEnemyGameplay(
        ManagedEnemy managedEnemy,
        RoomCameraTrigger activeRoom,
        bool hasPlayerPosition,
        Vector3 playerPosition)
    {
        bool enemyInsideHomeRoom = IsEnemyInsideHomeRoom(managedEnemy);
        bool enemyReturningHome = managedEnemy.Enemy.IsReturningHome;
        bool enemyInsideOtherActiveRoom = IsEnemyInsideOtherActiveRoom(managedEnemy, activeRoom);

        // 所属ルーム外、帰還中、または別カメラの重なり領域にいる間は眠らせずに帰還させる。
        if (!enemyInsideHomeRoom || enemyReturningHome || enemyInsideOtherActiveRoom)
        {
            StartEnemyReturnHomeIfNeeded(managedEnemy);
            return true;
        }

        if (hasPlayerPosition)
        {
            // 敵の起床判定はアクティブカメラではなく、プレイヤーが敵の所属ルーム内にいるかで決める。
            return IsPointInRoom(managedEnemy.Room, playerPosition);
        }

        return managedEnemy.DesiredGameplayActive;
    }

    private bool IsEnemyInsideOtherActiveRoom(ManagedEnemy managedEnemy, RoomCameraTrigger activeRoom)
    {
        // ルームが重なっている場所で別カメラがアクティブなら、敵をその場に残さず初期位置へ戻す。
        return activeRoom != null &&
               activeRoom != managedEnemy.Room &&
               IsPointInRoom(activeRoom, managedEnemy.Enemy.transform.position);
    }

    private bool IsEnemyInsideHomeRoom(ManagedEnemy managedEnemy)
    {
        return managedEnemy.Room == null ||
               IsPointInRoom(managedEnemy.Room, managedEnemy.Enemy.transform.position);
    }

    private static bool IsPointInRoom(RoomCameraTrigger room, Vector3 position)
    {
        return room != null && room.ContainsPoint(position);
    }

    private static void StartEnemyReturnHomeIfNeeded(ManagedEnemy managedEnemy)
    {
        if (managedEnemy.Enemy.IsReturningHome)
        {
            return;
        }

        // タックルの速度制御を止めてから帰還へ切り替える。
        CancelTackleAttacks(managedEnemy);
        managedEnemy.Enemy.StartReturnHome();
    }

    private static void CancelTackleAttacks(ManagedEnemy managedEnemy)
    {
        List<EnemyTackleAttack> tackleAttacks = managedEnemy.TackleAttacks;
        if (tackleAttacks == null)
        {
            return;
        }

        for (int i = 0; i < tackleAttacks.Count; i++)
        {
            if (tackleAttacks[i] != null)
            {
                tackleAttacks[i].CancelForLeashReturn();
            }
        }
    }

    private RoomCameraTrigger ResolveActiveRoom()
    {
        if (RoomCameraTrigger.ActiveRoom != null)
        {
            RememberResolvedActiveRoom(RoomCameraTrigger.ActiveRoom);
            return RoomCameraTrigger.ActiveRoom;
        }

        if (inferredActiveRoom != null && inferredActiveRoom.isActiveAndEnabled)
        {
            RememberResolvedActiveRoom(inferredActiveRoom);
            return inferredActiveRoom;
        }

        return lastResolvedActiveRoom != null && lastResolvedActiveRoom.isActiveAndEnabled
            ? lastResolvedActiveRoom
            : null;
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
            playerPosition = ResolvePlayerReferencePoint(playerTransform, playerTransform.position);
            return true;
        }

        playerTransform = null;
        cachedPlayerColliderRoot = null;
        cachedPlayerColliders.Clear();

        GameObject taggedPlayer = global::PlayerReferenceCache.GetGameObject();
        if (taggedPlayer != null && taggedPlayer.scene == managedScene)
        {
            playerTransform = taggedPlayer.transform;
            playerPosition = ResolvePlayerReferencePoint(playerTransform, playerTransform.position);
            return true;
        }

        GameObject playerObject = global::PlayerReferenceCache.GetGameObject(forceRefresh: true);
        PlayerHealth playerHealth = playerObject != null ? playerObject.GetComponentInChildren<PlayerHealth>(true) : null;
        if (playerHealth != null && playerHealth.gameObject.scene == managedScene)
        {
            playerTransform = playerHealth.transform;
            playerPosition = ResolvePlayerReferencePoint(playerTransform, playerTransform.position);
            return true;
        }

        playerPosition = default;
        return false;
    }

    private Vector3 ResolvePlayerReferencePoint(Transform player, Vector3 fallbackPosition)
    {
        if (player == null)
        {
            return fallbackPosition;
        }

        if (cachedPlayerColliderRoot != player)
        {
            cachedPlayerColliderRoot = player;
            cachedPlayerColliders.Clear();
            player.GetComponentsInChildren(false, cachedPlayerColliders);
        }

        if (TryResolveBoundsCenter(cachedPlayerColliders, false, out Vector3 center) ||
            TryResolveBoundsCenter(cachedPlayerColliders, true, out center))
        {
            return center;
        }

        return fallbackPosition;
    }

    private static bool TryResolveBoundsCenter(
        List<Collider2D> colliders,
        bool includeTriggers,
        out Vector3 center)
    {
        bool hasBounds = false;
        Bounds bounds = default;

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Count; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null ||
                    !collider.enabled ||
                    !includeTriggers && collider.isTrigger)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
        }

        center = hasBounds ? bounds.center : Vector3.zero;
        return hasBounds;
    }

    private void RememberResolvedActiveRoom(RoomCameraTrigger room)
    {
        if (room == null || !room.isActiveAndEnabled)
        {
            return;
        }

        lastResolvedActiveRoom = room;
    }

    private void QueueInitialEnemyActivityRestore()
    {
        for (int i = managedEnemies.Count - 1; i >= 0; i--)
        {
            ManagedEnemy managedEnemy = managedEnemies[i];
            if (!IsManagedEnemyValid(managedEnemy))
            {
                managedEnemies.RemoveAt(i);
                continue;
            }

            managedEnemy.DesiredGameplayActive = managedEnemy.InitialActiveSelf;
        }

        QueueEnemyStateApplication();
    }

    private void QueueEnemyStateApplication()
    {
        hasPendingEnemyStateChanges = true;
        nextEnemyStateChangeIndex = Mathf.Clamp(nextEnemyStateChangeIndex, 0, Mathf.Max(0, managedEnemies.Count - 1));
    }

    private void ProcessPendingEnemyStateChanges(int maxStateChanges)
    {
        if (!hasPendingEnemyStateChanges || managedEnemies.Count == 0)
        {
            return;
        }

        int budget = Mathf.Max(1, maxStateChanges);
        int changedCount = ProcessPendingEnemyStateChangesMatching(budget, desiredActive: true);
        if (changedCount < budget)
        {
            ProcessPendingEnemyStateChangesMatching(budget - changedCount, desiredActive: false);
        }

        hasPendingEnemyStateChanges = HasPendingEnemyStateChanges();
    }

    private int ProcessPendingEnemyStateChangesMatching(int maxStateChanges, bool desiredActive)
    {
        int changedCount = 0;
        int checkedCount = 0;

        while (managedEnemies.Count > 0 &&
               checkedCount < managedEnemies.Count &&
               changedCount < maxStateChanges)
        {
            if (nextEnemyStateChangeIndex >= managedEnemies.Count)
            {
                nextEnemyStateChangeIndex = 0;
            }

            ManagedEnemy managedEnemy = managedEnemies[nextEnemyStateChangeIndex];
            if (!IsManagedEnemyValid(managedEnemy))
            {
                managedEnemies.RemoveAt(nextEnemyStateChangeIndex);
                continue;
            }

            if (managedEnemy.AppliedGameplayActive != managedEnemy.DesiredGameplayActive &&
                managedEnemy.DesiredGameplayActive == desiredActive)
            {
                SetManagedEnemyGameplayActive(managedEnemy, managedEnemy.DesiredGameplayActive);
                changedCount++;
            }

            nextEnemyStateChangeIndex++;
            checkedCount++;
        }

        return changedCount;
    }

    private bool HasPendingEnemyStateChanges()
    {
        for (int i = managedEnemies.Count - 1; i >= 0; i--)
        {
            ManagedEnemy managedEnemy = managedEnemies[i];
            if (!IsManagedEnemyValid(managedEnemy))
            {
                managedEnemies.RemoveAt(i);
                continue;
            }

            if (managedEnemy.AppliedGameplayActive != managedEnemy.DesiredGameplayActive)
            {
                return true;
            }
        }

        return false;
    }

    private void SetManagedEnemyGameplayActive(ManagedEnemy managedEnemy, bool active)
    {
        if (!IsManagedEnemyValid(managedEnemy))
        {
            return;
        }

        if (active)
        {
            SetRigidbodiesActive(managedEnemy, true);
            SetBehavioursActive(managedEnemy, true);
        }
        else
        {
            managedEnemy.Enemy.StopHorizontalMotion();
            SetBehavioursActive(managedEnemy, false);
            SetRigidbodiesActive(managedEnemy, false);
        }

        managedEnemy.AppliedGameplayActive = active;
        LogDebug($"{managedEnemy.Root.name} gameplay {(active ? "resumed" : "slept")}.");
    }

    private static void SetBehavioursActive(ManagedEnemy managedEnemy, bool active)
    {
        List<MonoBehaviour> behaviours = managedEnemy.GameplayBehaviours;
        List<bool> initialEnabled = managedEnemy.InitialBehaviourEnabled;
        if (behaviours == null || initialEnabled == null)
        {
            return;
        }

        if (active)
        {
            for (int i = 0; i < behaviours.Count && i < initialEnabled.Count; i++)
            {
                if (behaviours[i] != null)
                {
                    behaviours[i].enabled = initialEnabled[i];
                }
            }

            return;
        }

        for (int i = behaviours.Count - 1; i >= 0; i--)
        {
            if (behaviours[i] != null)
            {
                behaviours[i].enabled = false;
            }
        }
    }

    private static void SetRigidbodiesActive(ManagedEnemy managedEnemy, bool active)
    {
        List<Rigidbody2D> rigidbodies = managedEnemy.Rigidbodies;
        List<bool> initialSimulated = managedEnemy.InitialRigidbodySimulated;
        if (rigidbodies == null || initialSimulated == null)
        {
            return;
        }

        for (int i = 0; i < rigidbodies.Count && i < initialSimulated.Count; i++)
        {
            if (rigidbodies[i] != null)
            {
                rigidbodies[i].simulated = active && initialSimulated[i];
            }
        }
    }

    private RoomCameraTrigger ResolveRoomForPosition(Vector3 position)
    {
        RoomCameraTrigger bestRoom = null;
        float bestArea = float.PositiveInfinity;

        for (int i = 0; i < roomTriggers.Count; i++)
        {
            RoomCameraTrigger roomTrigger = roomTriggers[i];
            if (roomTrigger == null || !roomTrigger.ContainsPoint(position))
            {
                continue;
            }

            float area = ResolveRoomBoundsArea(roomTrigger);
            if (area < bestArea)
            {
                bestArea = area;
                bestRoom = roomTrigger;
            }
        }

        return bestRoom;
    }

    private static float ResolveRoomBoundsArea(RoomCameraTrigger room)
    {
        if (room != null && room.TryGetAreaBounds(out Bounds bounds))
        {
            return bounds.size.x * bounds.size.y;
        }

        return float.MaxValue;
    }

    private static bool HasScenePrologueSource(Scene scene)
    {
        IReadOnlyList<SceneStartStoryEventSource> sources = SceneStartStoryEventSource.RegisteredSources;
        for (int i = 0; i < sources.Count; i++)
        {
            SceneStartStoryEventSource source = sources[i];
            if (source != null && source.gameObject.scene == scene)
            {
                return true;
            }
        }

        return false;
    }

    private static void FindRoomTriggers(Scene scene, List<RoomCameraTrigger> validTriggers)
    {
        validTriggers.Clear();
        IReadOnlyList<RoomCameraTrigger> allTriggers = RoomCameraTrigger.RegisteredTriggers;
        for (int i = 0; i < allTriggers.Count; i++)
        {
            RoomCameraTrigger trigger = allTriggers[i];
            if (trigger == null ||
                trigger.gameObject.scene != scene ||
                !trigger.isActiveAndEnabled)
            {
                continue;
            }

            validTriggers.Add(trigger);
        }
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

    private static void CollectGameplayBehaviours(GameObject root, List<MonoBehaviour> behaviours)
    {
        behaviours.Clear();
        if (root == null)
        {
            return;
        }

        root.GetComponentsInChildren(true, behaviours);
        for (int i = behaviours.Count - 1; i >= 0; i--)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (!IsGameplayBehaviour(behaviour))
            {
                behaviours.RemoveAt(i);
            }
        }
    }

    private static bool IsGameplayBehaviour(MonoBehaviour behaviour)
    {
        return behaviour is EnemyController ||
               behaviour is EnemyTackleAttack ||
               behaviour is EnemyRangedAttack ||
               behaviour is EnemyShooter ||
               behaviour is EnemyContact ||
               behaviour is EnemySpriteAnimator;
    }

    private static void CaptureBehaviourEnabledStates(List<MonoBehaviour> behaviours, List<bool> states)
    {
        states.Clear();
        if (behaviours == null)
        {
            return;
        }

        for (int i = 0; i < behaviours.Count; i++)
        {
            states.Add(behaviours[i] != null && behaviours[i].enabled);
        }
    }

    private static void CaptureRigidbodySimulatedStates(List<Rigidbody2D> rigidbodies, List<bool> states)
    {
        states.Clear();
        if (rigidbodies == null)
        {
            return;
        }

        for (int i = 0; i < rigidbodies.Count; i++)
        {
            states.Add(rigidbodies[i] != null && rigidbodies[i].simulated);
        }
    }

    private void LogDebug(string message)
    {
        if (debugLogging)
        {
            Debug.Log($"[RoomEnemyActivityManager] {message}", this);
        }
    }
}
