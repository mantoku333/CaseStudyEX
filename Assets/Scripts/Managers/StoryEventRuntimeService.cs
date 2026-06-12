using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using Metroidvania.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class StoryEventRuntimeService : MonoBehaviour
{
    private const string RuntimeObjectName = "[StoryEventRuntimeService]";
    private const string DefaultCatalogResourcePath = "Story/StoryEventCatalog";
    private const float DefaultLoadWaitTimeoutSeconds = 10f;

    private static StoryEventRuntimeService instance;
    private static bool sceneHookRegistered;
    private static bool uniTaskExceptionHookRegistered;
    private static string pendingDebugEventId = string.Empty;
    private static string pendingDebugSceneName = string.Empty;
    private static bool pendingDebugIgnoreFlags;

    [SerializeField] private string catalogResourcePath = DefaultCatalogResourcePath;
    [SerializeField] private float maxWaitForLoadSeconds = DefaultLoadWaitTimeoutSeconds;

    private StoryEventCatalog loadedCatalog;
    private StoryEventRunner eventRunner;

    public static bool HasPendingEvents
    {
        get
        {
            EnsureInstance();
            if (instance == null)
            {
                return false;
            }

            instance.EnsureEventRunner();
            return (instance.eventRunner != null && instance.eventRunner.HasPendingEvents) ||
                   FindPlayingSceneStoryEventController() != null;
        }
    }

    public static bool TryPlayEvent(string eventId)
    {
        EnsureInstance();
        if (instance == null)
        {
            return false;
        }

        return instance.TryQueueEventById(eventId, ignoreFlags: false, allowSceneLoad: false) ||
               instance.TryPlaySceneStoryEventController(eventId);
    }

    public static bool TryPlayEventFromDebugger(string eventId, bool ignoreFlags)
    {
        EnsureInstance();
        if (instance == null)
        {
            return false;
        }

        return instance.TryQueueEventById(eventId, ignoreFlags, allowSceneLoad: true);
    }

    public static bool TryCompleteActiveEventFromDebugger()
    {
        EnsureInstance();
        if (instance == null)
        {
            return false;
        }

        instance.EnsureEventRunner();
        return instance.eventRunner != null && instance.eventRunner.CompleteActiveEventImmediately();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        RegisterUniTaskExceptionFilter();

        if (instance != null)
        {
            return;
        }

        var gameObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(gameObject);
        instance = gameObject.AddComponent<StoryEventRuntimeService>();
    }

    private static void RegisterUniTaskExceptionFilter()
    {
        if (uniTaskExceptionHookRegistered)
        {
            return;
        }

        UniTaskScheduler.UnobservedTaskException += OnUnobservedUniTaskException;
        uniTaskExceptionHookRegistered = true;
    }

    private static void OnUnobservedUniTaskException(Exception exception)
    {
        if (IsIgnorableYarnContinueAfterStopException(exception))
        {
            return;
        }

        Debug.LogException(exception);
    }

    private static bool IsIgnorableYarnContinueAfterStopException(Exception exception)
    {
        if (exception == null)
        {
            return false;
        }

        string message = exception.Message ?? string.Empty;
        if (!message.Contains("Cannot continue running dialogue. No node has been selected.", StringComparison.Ordinal))
        {
            return false;
        }

        string stack = exception.StackTrace ?? string.Empty;
        return stack.Contains("Yarn.VirtualMachine.CheckCanContinue", StringComparison.Ordinal) ||
               stack.Contains("Yarn.Dialogue.Continue", StringComparison.Ordinal);
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
        EnsureEventRunner();
        LoadCatalogIfNeeded();
    }

    private void OnEnable()
    {
        RegisterSceneHook();
    }

    private void OnDisable()
    {
        if (!sceneHookRegistered)
        {
            return;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        sceneHookRegistered = false;
    }

    private void RegisterSceneHook()
    {
        if (sceneHookRegistered)
        {
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        sceneHookRegistered = true;
    }

    private void EnsureEventRunner()
    {
        if (eventRunner != null)
        {
            return;
        }

        eventRunner = GetComponent<StoryEventRunner>();
        if (eventRunner == null)
        {
            eventRunner = gameObject.AddComponent<StoryEventRunner>();
        }
    }

    private void LoadCatalogIfNeeded()
    {
        if (loadedCatalog != null || string.IsNullOrWhiteSpace(catalogResourcePath))
        {
            return;
        }

        loadedCatalog = Resources.Load<StoryEventCatalog>(catalogResourcePath.Trim());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (TryConsumePendingDebugRequest(scene.name, out string debugEventId, out bool ignoreFlags))
        {
            StartCoroutine(QueueDebugEventAfterLoad(scene.name, debugEventId, ignoreFlags));
            return;
        }

        StartCoroutine(QueueSceneStartEvents(scene.name));
    }

    private IEnumerator QueueSceneStartEvents(string sceneName)
    {
        EnsureEventRunner();
        LoadCatalogIfNeeded();

        float elapsed = 0f;
        float timeout = Mathf.Max(0.1f, maxWaitForLoadSeconds);
        while (SaveManager.IsLoadInProgress && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (FindFirstObjectByType<DialogueManager>() == null && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return null;

        if (EnqueueSceneComponentEvents(sceneName))
        {
            yield break;
        }

        bool hasCatalogEvents = loadedCatalog != null &&
                                loadedCatalog.sceneStartEvents != null &&
                                loadedCatalog.sceneStartEvents.Count > 0;

        if (hasCatalogEvents)
        {
            bool enqueuedCatalogEvent = false;
            for (int i = 0; i < loadedCatalog.sceneStartEvents.Count; i++)
            {
                StoryEventDefinition definition = loadedCatalog.sceneStartEvents[i];
                if (definition == null || !definition.MatchesScene(sceneName))
                {
                    continue;
                }

                eventRunner.Enqueue(definition);
                enqueuedCatalogEvent = true;
            }

            if (enqueuedCatalogEvent)
            {
                yield break;
            }
        }

        Debug.LogWarning($"[StoryEventRuntimeService] Scene start event not found. scene='{sceneName}'");
    }

    private bool EnqueueSceneComponentEvents(string sceneName)
    {
        SceneStartStoryEventSource[] sources = FindObjectsByType<SceneStartStoryEventSource>(FindObjectsSortMode.None);
        if (sources == null || sources.Length == 0)
        {
            return false;
        }

        bool enqueued = false;
        for (int i = 0; i < sources.Length; i++)
        {
            SceneStartStoryEventSource source = sources[i];
            if (source == null || !source.isActiveAndEnabled)
            {
                continue;
            }

            Scene sourceScene = source.gameObject.scene;
            if (!sourceScene.IsValid() || !string.Equals(sourceScene.name, sceneName, StringComparison.Ordinal))
            {
                continue;
            }

            StoryEventDefinition definition = source.CreateDefinition(sceneName);
            if (definition == null)
            {
                continue;
            }

            if (!definition.CanRunByFlags())
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(definition.storyEventControllerId))
            {
                if (!TryFindSceneStoryEventController(definition.storyEventControllerId, out StoryEventController controller) ||
                    controller == null)
                {
                    Debug.LogWarning(
                        $"[StoryEventRuntimeService] StoryEventController not found or could not start. " +
                        $"eventId='{definition.eventId}', controllerId='{definition.storyEventControllerId}'");
                    enqueued = true;
                    continue;
                }

                StartCoroutine(PlaySceneStoryEventController(definition, controller));
                enqueued = true;
                continue;
            }

            eventRunner.Enqueue(definition);
            enqueued = true;
        }

        return enqueued;
    }

    private bool TryQueueEventById(string eventId, bool ignoreFlags, bool allowSceneLoad)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return false;
        }

        EnsureEventRunner();
        LoadCatalogIfNeeded();

        string trimmedEventId = eventId.Trim();
        string activeSceneName = SceneManager.GetActiveScene().name;
        StoryEventDefinition matchedEvent = FindSceneEventById(trimmedEventId, activeSceneName);
        if (matchedEvent == null)
        {
            StoryEventDefinition anySceneEvent = FindAnySceneEventById(trimmedEventId);
            if (anySceneEvent != null)
            {
                if (string.Equals(trimmedEventId, "prologue", StringComparison.OrdinalIgnoreCase))
                {
                    StoryEventDefinition prologueDefinition = CreatePlayableDefinition(anySceneEvent, ignoreFlags);
                    if (prologueDefinition != null)
                    {
                        prologueDefinition.sceneName = activeSceneName;
                        eventRunner.Enqueue(prologueDefinition);
                        return true;
                    }
                }

                string targetSceneName = string.IsNullOrWhiteSpace(anySceneEvent.sceneName)
                    ? activeSceneName
                    : anySceneEvent.sceneName.Trim();

                if (!string.Equals(activeSceneName, targetSceneName, StringComparison.Ordinal))
                {
                    if (!allowSceneLoad)
                    {
                        return false;
                    }

                    if (Application.CanStreamedLevelBeLoaded(targetSceneName))
                    {
                        pendingDebugEventId = trimmedEventId;
                        pendingDebugSceneName = targetSceneName;
                        pendingDebugIgnoreFlags = ignoreFlags;
                        SceneManager.LoadScene(targetSceneName);
                        return true;
                    }
                }

                eventRunner.Enqueue(CreatePlayableDefinition(anySceneEvent, ignoreFlags));
                return true;
            }

            return false;
        }

        eventRunner.Enqueue(CreatePlayableDefinition(matchedEvent, ignoreFlags));
        return true;
    }

    private bool TryPlaySceneStoryEventController(string eventId)
    {
        return TryFindSceneStoryEventController(eventId, out StoryEventController controller) &&
               controller != null &&
               controller.PlayEvent();
    }

    private bool TryFindSceneStoryEventController(string eventId, out StoryEventController matchedController)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            matchedController = null;
            return false;
        }

        string trimmedEventId = eventId.Trim();
        string activeSceneName = SceneManager.GetActiveScene().name;
        StoryEventController fallbackController = null;

        StoryEventController[] controllers =
            FindObjectsByType<StoryEventController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            StoryEventController controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            bool matchesId =
                string.Equals(controller.EventId, trimmedEventId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(controller.name, trimmedEventId, StringComparison.OrdinalIgnoreCase);
            if (!matchesId)
            {
                continue;
            }

            if (controller.gameObject.scene.IsValid() &&
                string.Equals(controller.gameObject.scene.name, activeSceneName, StringComparison.Ordinal))
            {
                matchedController = controller;
                return true;
            }

            fallbackController ??= controller;
        }

        matchedController = fallbackController;
        return matchedController != null;
    }

    private IEnumerator PlaySceneStoryEventController(StoryEventDefinition definition, StoryEventController controller)
    {
        if (definition == null || controller == null)
        {
            yield break;
        }

        if (!controller.PlayEvent())
        {
            Debug.LogWarning(
                $"[StoryEventRuntimeService] StoryEventController could not start. " +
                $"eventId='{definition.eventId}', controllerId='{definition.storyEventControllerId}'",
                controller);
            yield break;
        }

        definition.onStartMutations?.Apply();

        while (controller != null && controller.IsPlaying)
        {
            yield return null;
        }

        ApplySceneStoryEventCompletionState(definition);
    }

    private static void ApplySceneStoryEventCompletionState(StoryEventDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        definition.onCompleteMutations?.Apply();

        if (!string.IsNullOrWhiteSpace(definition.runOnceFlagKey))
        {
            GameProgressFlags.Set(definition.runOnceFlagKey.Trim(), true);
        }

        if (definition.autoSaveOnComplete)
        {
            SaveManager.TrySaveCurrentGame();
        }
    }

    private static StoryEventController FindPlayingSceneStoryEventController()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;
        StoryEventController fallbackController = null;

        StoryEventController[] controllers =
            FindObjectsByType<StoryEventController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            StoryEventController controller = controllers[i];
            if (controller == null || !controller.IsPlaying)
            {
                continue;
            }

            if (controller.gameObject.scene.IsValid() &&
                string.Equals(controller.gameObject.scene.name, activeSceneName, StringComparison.Ordinal))
            {
                return controller;
            }

            fallbackController ??= controller;
        }

        return fallbackController;
    }

    private StoryEventDefinition FindSceneEventById(string eventId, string sceneName)
    {
        if (loadedCatalog != null &&
            loadedCatalog.sceneStartEvents != null &&
            loadedCatalog.sceneStartEvents.Count > 0)
        {
            for (int i = 0; i < loadedCatalog.sceneStartEvents.Count; i++)
            {
                StoryEventDefinition definition = loadedCatalog.sceneStartEvents[i];
                if (definition == null || !definition.MatchesScene(sceneName))
                {
                    continue;
                }

                if (string.Equals(definition.eventId, eventId, StringComparison.OrdinalIgnoreCase))
                {
                    return definition;
                }
            }
        }

        return null;
    }

    private StoryEventDefinition FindAnySceneEventById(string eventId)
    {
        if (loadedCatalog == null ||
            loadedCatalog.sceneStartEvents == null ||
            loadedCatalog.sceneStartEvents.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < loadedCatalog.sceneStartEvents.Count; i++)
        {
            StoryEventDefinition definition = loadedCatalog.sceneStartEvents[i];
            if (definition == null)
            {
                continue;
            }

            if (string.Equals(definition.eventId, eventId, StringComparison.OrdinalIgnoreCase))
            {
                return definition;
            }
        }

        return null;
    }

    private static bool TryConsumePendingDebugRequest(string loadedSceneName, out string eventId, out bool ignoreFlags)
    {
        eventId = string.Empty;
        ignoreFlags = false;

        if (string.IsNullOrWhiteSpace(pendingDebugEventId) || string.IsNullOrWhiteSpace(pendingDebugSceneName))
        {
            return false;
        }

        if (!string.Equals(loadedSceneName, pendingDebugSceneName, StringComparison.Ordinal))
        {
            return false;
        }

        eventId = pendingDebugEventId;
        ignoreFlags = pendingDebugIgnoreFlags;
        pendingDebugEventId = string.Empty;
        pendingDebugSceneName = string.Empty;
        pendingDebugIgnoreFlags = false;
        return true;
    }

    private IEnumerator QueueDebugEventAfterLoad(string sceneName, string eventId, bool ignoreFlags)
    {
        EnsureEventRunner();
        LoadCatalogIfNeeded();

        float elapsed = 0f;
        float timeout = Mathf.Max(0.1f, maxWaitForLoadSeconds);
        while (SaveManager.IsLoadInProgress && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = 0f;
        while (FindFirstObjectByType<DialogueManager>() == null && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        StoryEventDefinition definition = FindSceneEventById(eventId, sceneName);

        if (definition == null)
        {
            Debug.LogWarning($"[StoryEventRuntimeService] Debug event not found. eventId='{eventId}', scene='{sceneName}'");
            yield break;
        }

        eventRunner.Enqueue(CreatePlayableDefinition(definition, ignoreFlags));
    }

    private static StoryEventDefinition CreatePlayableDefinition(StoryEventDefinition source, bool ignoreFlags)
    {
        if (source == null)
        {
            return null;
        }

        if (!ignoreFlags)
        {
            return source;
        }

        return new StoryEventDefinition
        {
            eventId = source.eventId,
            sceneName = source.sceneName,
            storyEventControllerId = source.storyEventControllerId,
            dialogueNodeName = source.dialogueNodeName,
            dialogueStyle = source.dialogueStyle,
            runOnceFlagKey = string.Empty,
            conditions = new StoryFlagConditionSet(),
            onStartMutations = source.onStartMutations,
            onCompleteMutations = source.onCompleteMutations,
            preActions = source.preActions,
            postActions = source.postActions,
            pausePolicy = source.pausePolicy,
            autoSaveOnComplete = source.autoSaveOnComplete,
            skipWhenDialogueRunning = source.skipWhenDialogueRunning
        };
    }

}
