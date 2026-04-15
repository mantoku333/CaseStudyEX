using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SaveManager : MonoBehaviour
{
    private const int SaveVersion = 2;
    public const int DefaultSlotIndex = 1;
    public const int MinSlotIndex = 1;
    public const int MaxSlotIndex = 3;
    private static readonly bool EnableLoadTrace = false;
    private const int TraceFrameCount = 120;
    private const float TraceThreshold = 0.001f;

    private static SaveManager instance;
    private static SaveGameData pendingLoadData;
    private static int pendingLoadRequestId;
    private static int pendingLoadSlotIndex;
    private static bool sceneHookRegistered;
    private static int loadRequestSequence;
    private static global::PlayerController tracedPlayerController;
    private static Rigidbody2D tracedPlayerRigidbody;
    private static Vector3 lastTracedTransformPosition;
    private static Vector2 lastTracedRigidbodyPosition;
    private static int traceFramesRemaining;
    private static int tracedLoadRequestId;
    private static readonly Dictionary<string, int> runtimeItems = new Dictionary<string, int>(StringComparer.Ordinal);
    private static readonly List<ISaveDataModule> registeredModules = new List<ISaveDataModule>();
    private static readonly List<ISaveDataModule> moduleExecutionBuffer = new List<ISaveDataModule>();

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

    public static void RegisterModule(ISaveDataModule module)
    {
        if (module == null)
        {
            return;
        }

        if (!registeredModules.Contains(module))
        {
            registeredModules.Add(module);
        }
    }

    public static void UnregisterModule(ISaveDataModule module)
    {
        if (module == null)
        {
            return;
        }

        registeredModules.Remove(module);
    }

    public static void SetFlag(string flagKey, bool value)
    {
        GameProgressFlags.Set(flagKey, value);
    }

    public static bool GetFlag(string flagKey, bool defaultValue = false)
    {
        return GameProgressFlags.Get(flagKey, defaultValue);
    }

    public static void RemoveFlag(string flagKey)
    {
        GameProgressFlags.Remove(flagKey);
    }

    public static void ClearAllFlags()
    {
        GameProgressFlags.ClearAll();
    }

    public static void SetItemCount(string itemId, int count)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        int normalized = Mathf.Max(0, count);
        if (normalized <= 0)
        {
            runtimeItems.Remove(itemId);
            return;
        }

        runtimeItems[itemId] = normalized;
    }

    public static int GetItemCount(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        return runtimeItems.TryGetValue(itemId, out var count) ? Mathf.Max(0, count) : 0;
    }

    public static void AddItemCount(string itemId, int delta)
    {
        if (string.IsNullOrWhiteSpace(itemId) || delta == 0)
        {
            return;
        }

        int current = GetItemCount(itemId);
        SetItemCount(itemId, current + delta);
    }

    public static void ClearAllItems()
    {
        runtimeItems.Clear();
    }

    public static bool HasSave()
    {
        return HasSave(DefaultSlotIndex);
    }

    public static bool HasSave(int slotIndex)
    {
        if (!TryValidateSlotIndex(slotIndex))
        {
            return false;
        }

        return SaveRepository.HasSave(slotIndex);
    }

    public static SaveSlotMeta GetSlotMeta(int slotIndex)
    {
        if (!TryValidateSlotIndex(slotIndex))
        {
            return new SaveSlotMeta(
                slotIndex: slotIndex,
                hasSave: false,
                isCorrupted: false,
                sceneName: string.Empty,
                savedAtUtc: string.Empty);
        }

        return SaveRepository.GetSlotMeta(slotIndex);
    }

    private static bool TryValidateSlotIndex(int slotIndex)
    {
        if (slotIndex >= MinSlotIndex && slotIndex <= MaxSlotIndex)
        {
            return true;
        }

        Debug.LogWarning(
            $"[SaveManager] Invalid slot index: {slotIndex}. Valid range is {MinSlotIndex}-{MaxSlotIndex}.");
        return false;
    }

    public static bool TrySaveCurrentGame()
    {
        return TrySaveCurrentGame(DefaultSlotIndex);
    }

    public static bool TrySaveCurrentGame(int slotIndex)
    {
        if (!TryValidateSlotIndex(slotIndex))
        {
            return false;
        }

        EnsureInstance();

        var playerController = UnityEngine.Object.FindFirstObjectByType<global::PlayerController>();
        if (playerController == null)
        {
            Debug.LogWarning("[SaveManager] PlayerController not found. Save skipped.");
            return false;
        }

        var saveData = new SaveGameData
        {
            version = SaveVersion,
            sceneName = SceneManager.GetActiveScene().name,
            playerPosition = SerializableVector3.FromVector3(playerController.transform.position),
            savedAtUtc = DateTime.UtcNow.ToString("o")
        };

        WriteRuntimeCollectionsToSave(saveData);
        CaptureModules(saveData);

        return SaveRepository.TryWrite(slotIndex, saveData);
    }

    public static bool TryLoadGame(string fallbackSceneName = null)
    {
        return TryLoadGame(DefaultSlotIndex, fallbackSceneName);
    }

    public static bool TryLoadGame(int slotIndex, string fallbackSceneName = null)
    {
        if (!TryValidateSlotIndex(slotIndex))
        {
            return false;
        }

        EnsureInstance();
        int loadRequestId = ++loadRequestSequence;

        if (!SaveRepository.TryRead(slotIndex, out var saveData))
        {
            if (EnableLoadTrace)
            {
                Debug.Log(
                    $"[SaveManager][Trace#{loadRequestId}] TryLoadGame failed: slot={slotIndex}, save file not found or unreadable. fallback='{fallbackSceneName}'");
            }

            if (!string.IsNullOrEmpty(fallbackSceneName))
            {
                SceneManager.LoadScene(fallbackSceneName);
            }

            return false;
        }

        pendingLoadData = saveData;
        pendingLoadRequestId = loadRequestId;
        pendingLoadSlotIndex = slotIndex;
        if (EnableLoadTrace)
        {
            Debug.Log(
                $"[SaveManager][Trace#{loadRequestId}] TryLoadGame slot={slotIndex}, saveScene='{saveData.sceneName}', savePos={saveData.playerPosition.ToVector3()}, fallback='{fallbackSceneName}', activeScene='{SceneManager.GetActiveScene().name}', frame={Time.frameCount}");
            LogPlayerSnapshot($"TryLoadGame#{loadRequestId}/BeforeLoad");
        }

        string sceneToLoad = ResolveSceneToLoad(saveData.sceneName, fallbackSceneName);

        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogError(
                $"[SaveManager] No loadable scene found. requestId={loadRequestId}, slot={slotIndex}, saveScene='{saveData.sceneName}', fallbackScene='{fallbackSceneName}'.");
            pendingLoadData = null;
            pendingLoadRequestId = 0;
            pendingLoadSlotIndex = 0;
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
        return DeleteSave(DefaultSlotIndex);
    }

    public static bool DeleteSave(int slotIndex)
    {
        if (!TryValidateSlotIndex(slotIndex))
        {
            return false;
        }

        pendingLoadData = null;
        pendingLoadRequestId = 0;
        pendingLoadSlotIndex = 0;

        if (!HasSave(slotIndex))
        {
            return true;
        }

        return SaveRepository.TryDelete(slotIndex);
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

        SaveGameData loadedData = pendingLoadData;

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

        ReadRuntimeCollectionsFromSave(loadedData);
        RestoreModules(loadedData);

        Debug.Log($"[SaveManager] Save loaded. slot={pendingLoadSlotIndex}, scene={loadedData.sceneName}");
        if (EnableLoadTrace)
        {
            Debug.Log(
                $"[SaveManager][Trace#{pendingLoadRequestId}] Applied position: transform={playerController.transform.position}, rb={(rigidbody2d != null ? rigidbody2d.position.ToString() : "none")}, frame={Time.frameCount}, playerName='{playerController.gameObject.name}', instanceId={playerController.GetInstanceID()}");
            LogPlayerSnapshot($"TryApplyPendingLoad#{pendingLoadRequestId}/AfterApply");
        }
        BeginPostLoadTrace(playerController, pendingLoadRequestId);
        pendingLoadData = null;
        pendingLoadRequestId = 0;
        pendingLoadSlotIndex = 0;
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

    private static void WriteRuntimeCollectionsToSave(SaveGameData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        if (saveData.items == null)
        {
            saveData.items = new List<SaveItemStackEntry>();
        }
        else
        {
            saveData.items.Clear();
        }

        foreach (var pair in runtimeItems)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            int count = Mathf.Max(0, pair.Value);
            if (count <= 0)
            {
                continue;
            }

            saveData.items.Add(new SaveItemStackEntry
            {
                itemId = pair.Key,
                count = count
            });
        }
    }

    private static void ReadRuntimeCollectionsFromSave(SaveGameData saveData)
    {
        runtimeItems.Clear();

        if (saveData == null)
        {
            return;
        }

        if (saveData.items != null)
        {
            for (int i = 0; i < saveData.items.Count; i++)
            {
                SaveItemStackEntry entry = saveData.items[i];
                if (string.IsNullOrWhiteSpace(entry.itemId))
                {
                    continue;
                }

                int count = Mathf.Max(0, entry.count);
                if (count <= 0)
                {
                    continue;
                }

                runtimeItems[entry.itemId] = count;
            }
        }
    }

    private static void CaptureModules(SaveGameData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        List<ISaveDataModule> modules = CollectModules();
        for (int i = 0; i < modules.Count; i++)
        {
            ISaveDataModule module = modules[i];
            if (module == null)
            {
                continue;
            }

            try
            {
                module.Capture(saveData);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveManager] Save module capture failed: {module.GetType().Name}. {exception}");
            }
        }
    }

    private static void RestoreModules(SaveGameData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        List<ISaveDataModule> modules = CollectModules();
        for (int i = 0; i < modules.Count; i++)
        {
            ISaveDataModule module = modules[i];
            if (module == null)
            {
                continue;
            }

            try
            {
                module.Restore(saveData);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveManager] Save module restore failed: {module.GetType().Name}. {exception}");
            }
        }
    }

    private static List<ISaveDataModule> CollectModules()
    {
        CleanupRegisteredModules();

        var sceneBehaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneBehaviours.Length; i++)
        {
            if (sceneBehaviours[i] is ISaveDataModule sceneModule && !registeredModules.Contains(sceneModule))
            {
                registeredModules.Add(sceneModule);
            }
        }

        moduleExecutionBuffer.Clear();
        for (int i = 0; i < registeredModules.Count; i++)
        {
            ISaveDataModule module = registeredModules[i];
            if (module != null)
            {
                moduleExecutionBuffer.Add(module);
            }
        }

        moduleExecutionBuffer.Sort((left, right) => left.Priority.CompareTo(right.Priority));
        return moduleExecutionBuffer;
    }

    private static void CleanupRegisteredModules()
    {
        for (int i = registeredModules.Count - 1; i >= 0; i--)
        {
            if (registeredModules[i] == null)
            {
                registeredModules.RemoveAt(i);
            }
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

}
