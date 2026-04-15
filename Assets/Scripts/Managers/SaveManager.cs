using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SaveManager : MonoBehaviour
{
    private const string SaveFileName = "save_slot_01.json";
    private const int SaveVersion = 1;
    private const bool EnableLoadTrace = false;
    private const int TraceFrameCount = 120;
    private const float TraceThreshold = 0.001f;

    private static SaveManager instance;
    private static SaveData pendingLoadData;
    private static int pendingLoadRequestId;
    private static bool sceneHookRegistered;
    private static int loadRequestSequence;
    private static global::PlayerController tracedPlayerController;
    private static Rigidbody2D tracedPlayerRigidbody;
    private static Vector3 lastTracedTransformPosition;
    private static Vector2 lastTracedRigidbodyPosition;
    private static int traceFramesRemaining;
    private static int tracedLoadRequestId;

    private static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
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
        RegisterSceneHook();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        TraceLoadedPlayerPosition("Update");

        if (pendingLoadData != null)
        {
            TryApplyPendingLoad();
        }
    }

    private void FixedUpdate()
    {
        TraceLoadedPlayerPosition("FixedUpdate");
    }

    private void LateUpdate()
    {
        TraceLoadedPlayerPosition("LateUpdate");
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        var gameObject = new GameObject(nameof(SaveManager));
        DontDestroyOnLoad(gameObject);
        instance = gameObject.AddComponent<SaveManager>();
    }

    private static void RegisterSceneHook()
    {
        if (sceneHookRegistered)
        {
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        sceneHookRegistered = true;
    }

    public static bool HasSave()
    {
        return File.Exists(SaveFilePath);
    }

    public static bool TrySaveCurrentGame()
    {
        EnsureInstance();

        var playerController = UnityEngine.Object.FindFirstObjectByType<global::PlayerController>();
        if (playerController == null)
        {
            Debug.LogWarning("[SaveManager] PlayerController not found. Save skipped.");
            return false;
        }

        var saveData = new SaveData
        {
            version = SaveVersion,
            sceneName = SceneManager.GetActiveScene().name,
            playerPosition = SerializableVector3.FromVector3(playerController.transform.position),
            savedAtUtc = DateTime.UtcNow.ToString("o")
        };

        return WriteSaveData(saveData);
    }

    public static bool TryLoadGame(string fallbackSceneName = null)
    {
        EnsureInstance();
        int loadRequestId = ++loadRequestSequence;

        if (!TryReadSaveData(out var saveData))
        {
            if (EnableLoadTrace)
            {
                Debug.Log(
                    $"[SaveManager][Trace#{loadRequestId}] TryLoadGame failed: save file not found or unreadable. fallback='{fallbackSceneName}'");
            }

            if (!string.IsNullOrEmpty(fallbackSceneName))
            {
                SceneManager.LoadScene(fallbackSceneName);
            }

            return false;
        }

        pendingLoadData = saveData;
        pendingLoadRequestId = loadRequestId;
        if (EnableLoadTrace)
        {
            Debug.Log(
                $"[SaveManager][Trace#{loadRequestId}] TryLoadGame saveScene='{saveData.sceneName}', savePos={saveData.playerPosition.ToVector3()}, fallback='{fallbackSceneName}', activeScene='{SceneManager.GetActiveScene().name}', frame={Time.frameCount}");
            LogPlayerSnapshot($"TryLoadGame#{loadRequestId}/BeforeLoad");
        }

        string sceneToLoad = ResolveSceneToLoad(saveData.sceneName, fallbackSceneName);

        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogError(
                $"[SaveManager] No loadable scene found. requestId={loadRequestId}, saveScene='{saveData.sceneName}', fallbackScene='{fallbackSceneName}'.");
            pendingLoadData = null;
            pendingLoadRequestId = 0;
            return false;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        bool isSameLoadedScene =
            activeScene.IsValid() &&
            activeScene.isLoaded &&
            string.Equals(activeScene.name, sceneToLoad, StringComparison.Ordinal);

        if (isSameLoadedScene)
        {
            if (EnableLoadTrace)
            {
                Debug.Log($"[SaveManager][Trace#{loadRequestId}] Same scene load detected. Skip LoadScene and apply in runtime scene: '{sceneToLoad}'.");
            }

            return true;
        }

        SceneManager.LoadScene(sceneToLoad);
        if (EnableLoadTrace)
        {
            Debug.Log($"[SaveManager][Trace#{loadRequestId}] LoadScene requested: '{sceneToLoad}'");
        }
        return true;
    }

    public static bool DeleteSave()
    {
        pendingLoadData = null;

        if (!HasSave())
        {
            return true;
        }

        try
        {
            File.Delete(SaveFilePath);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SaveManager] Failed to delete save: {exception}");
            return false;
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pendingLoadData == null)
        {
            return;
        }

        if (EnableLoadTrace)
        {
            Debug.Log($"[SaveManager][Trace#{pendingLoadRequestId}] sceneLoaded: scene='{scene.name}', mode={mode}, frame={Time.frameCount}");
            LogPlayerSnapshot($"TryApplyPendingLoad#{pendingLoadRequestId}/OnSceneLoaded");
        }

        TryApplyPendingLoad();
    }

    private static void TryApplyPendingLoad()
    {
        if (pendingLoadData == null)
        {
            return;
        }

        var playerController = UnityEngine.Object.FindFirstObjectByType<global::PlayerController>();
        if (playerController == null)
        {
            if (EnableLoadTrace)
            {
                Debug.Log($"[SaveManager][Trace#{pendingLoadRequestId}] TryApplyPendingLoad: player not found at frame={Time.frameCount}");
            }
            return;
        }

        Vector3 loadedPosition = pendingLoadData.playerPosition.ToVector3();
        var rigidbody2d = playerController.GetComponent<Rigidbody2D>();
        if (rigidbody2d != null)
        {
            rigidbody2d.position = new Vector2(loadedPosition.x, loadedPosition.y);
            playerController.transform.position = new Vector3(loadedPosition.x, loadedPosition.y, loadedPosition.z);
            rigidbody2d.linearVelocity = Vector2.zero;
            rigidbody2d.angularVelocity = 0f;
            rigidbody2d.Sleep();
        }
        else
        {
            playerController.transform.position = loadedPosition;
        }

        Debug.Log($"[SaveManager] Save loaded. scene={pendingLoadData.sceneName}");
        if (EnableLoadTrace)
        {
            Debug.Log(
                $"[SaveManager][Trace#{pendingLoadRequestId}] Applied position: transform={playerController.transform.position}, rb={(rigidbody2d != null ? rigidbody2d.position.ToString() : "none")}, frame={Time.frameCount}, playerName='{playerController.gameObject.name}', instanceId={playerController.GetInstanceID()}");
            LogPlayerSnapshot($"TryApplyPendingLoad#{pendingLoadRequestId}/AfterApply");
        }
        BeginPostLoadTrace(playerController, pendingLoadRequestId);
        pendingLoadData = null;
        pendingLoadRequestId = 0;
    }

    private static void BeginPostLoadTrace(global::PlayerController playerController, int loadRequestId)
    {
        if (!EnableLoadTrace || playerController == null)
        {
            return;
        }

        tracedLoadRequestId = loadRequestId;
        tracedPlayerController = playerController;
        tracedPlayerRigidbody = playerController.GetComponent<Rigidbody2D>();
        lastTracedTransformPosition = tracedPlayerController.transform.position;
        lastTracedRigidbodyPosition = tracedPlayerRigidbody != null ? tracedPlayerRigidbody.position : Vector2.zero;
        traceFramesRemaining = TraceFrameCount;

        Debug.Log(
            $"[SaveManager][Trace#{tracedLoadRequestId}] Begin monitor: frame={Time.frameCount}, transform={lastTracedTransformPosition}, rb={(tracedPlayerRigidbody != null ? lastTracedRigidbodyPosition.ToString() : "none")}");
    }

    private static void TraceLoadedPlayerPosition(string phase)
    {
        if (!EnableLoadTrace || traceFramesRemaining <= 0)
        {
            return;
        }

        if (tracedPlayerController == null)
        {
            Debug.Log($"[SaveManager][Trace#{tracedLoadRequestId}] Monitor aborted: traced player destroyed.");
            traceFramesRemaining = 0;
            return;
        }

        traceFramesRemaining--;

        Vector3 currentTransformPosition = tracedPlayerController.transform.position;
        bool transformMoved = (currentTransformPosition - lastTracedTransformPosition).sqrMagnitude > TraceThreshold;

        Vector2 currentRigidbodyPosition = Vector2.zero;
        bool rigidbodyMoved = false;
        Vector2 currentVelocity = Vector2.zero;
        float currentAngularVelocity = 0f;

        if (tracedPlayerRigidbody != null)
        {
            currentRigidbodyPosition = tracedPlayerRigidbody.position;
            rigidbodyMoved = (currentRigidbodyPosition - lastTracedRigidbodyPosition).sqrMagnitude > TraceThreshold;
            currentVelocity = tracedPlayerRigidbody.linearVelocity;
            currentAngularVelocity = tracedPlayerRigidbody.angularVelocity;
        }

        if (transformMoved || rigidbodyMoved)
        {
            var dodgeController = tracedPlayerController.GetComponent<DodgeController>();
            bool isDodging = dodgeController != null && dodgeController.IsDodging();
            Debug.Log(
                $"[SaveManager][Trace#{tracedLoadRequestId}] {phase} frame={Time.frameCount} moved: transform {lastTracedTransformPosition} -> {currentTransformPosition}, rb {lastTracedRigidbodyPosition} -> {currentRigidbodyPosition}, vel={currentVelocity}, angVel={currentAngularVelocity}, dodging={isDodging}, scene={SceneManager.GetActiveScene().name}");
            lastTracedTransformPosition = currentTransformPosition;
            lastTracedRigidbodyPosition = currentRigidbodyPosition;
        }

        if (traceFramesRemaining == 0)
        {
            Debug.Log($"[SaveManager][Trace#{tracedLoadRequestId}] Monitor finished.");
            tracedLoadRequestId = 0;
        }
    }

    private static void LogPlayerSnapshot(string context)
    {
        if (!EnableLoadTrace)
        {
            return;
        }

        var players = UnityEngine.Object.FindObjectsByType<global::PlayerController>(FindObjectsSortMode.None);
        if (players == null || players.Length == 0)
        {
            Debug.Log($"[SaveManager][Trace] {context}: no PlayerController found.");
            return;
        }

        for (int i = 0; i < players.Length; i++)
        {
            var player = players[i];
            if (player == null)
            {
                continue;
            }

            var rb = player.GetComponent<Rigidbody2D>();
            Debug.Log(
                $"[SaveManager][Trace] {context}: player[{i}] name='{player.gameObject.name}', instanceId={player.GetInstanceID()}, active={player.gameObject.activeInHierarchy}, scene='{player.gameObject.scene.name}', transform={player.transform.position}, rb={(rb != null ? rb.position.ToString() : "none")}");
        }
    }

    private static string ResolveSceneToLoad(string saveSceneName, string fallbackSceneName)
    {
        if (!string.IsNullOrEmpty(saveSceneName) && Application.CanStreamedLevelBeLoaded(saveSceneName))
        {
            return saveSceneName;
        }

        if (!string.IsNullOrEmpty(fallbackSceneName) && Application.CanStreamedLevelBeLoaded(fallbackSceneName))
        {
            return fallbackSceneName;
        }

        return string.Empty;
    }

    private static bool TryReadSaveData(out SaveData saveData)
    {
        saveData = null;

        if (!HasSave())
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            saveData = JsonUtility.FromJson<SaveData>(json);
            return saveData != null;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SaveManager] Failed to read save: {exception}");
            return false;
        }
    }

    private static bool WriteSaveData(SaveData saveData)
    {
        try
        {
            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(SaveFilePath, json);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SaveManager] Failed to write save: {exception}");
            return false;
        }
    }

    [Serializable]
    private sealed class SaveData
    {
        public int version;
        public string sceneName;
        public SerializableVector3 playerPosition;
        public string savedAtUtc;
    }

    [Serializable]
    private struct SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3()
        {
            return new Vector3(x, y, z);
        }

        public static SerializableVector3 FromVector3(Vector3 value)
        {
            return new SerializableVector3
            {
                x = value.x,
                y = value.y,
                z = value.z
            };
        }
    }
}
