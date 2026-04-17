using System;
using System.Collections;
using Metroidvania.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class StoryEventRuntimeService : MonoBehaviour
{
    private const string RuntimeObjectName = "[StoryEventRuntimeService]";
    private const string DefaultCatalogResourcePath = "Story/StoryEventCatalog";
    private const string DefaultEntrySceneName = "Fix_Player_Mantoku";
    private const float DefaultLoadWaitTimeoutSeconds = 10f;

    private static StoryEventRuntimeService instance;
    private static bool sceneHookRegistered;

    [SerializeField] private string catalogResourcePath = DefaultCatalogResourcePath;
    [SerializeField] private float maxWaitForLoadSeconds = DefaultLoadWaitTimeoutSeconds;

    private StoryEventCatalog loadedCatalog;
    private StoryEventRunner eventRunner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        var gameObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(gameObject);
        instance = gameObject.AddComponent<StoryEventRuntimeService>();
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

        bool hasCatalogEvents = loadedCatalog != null &&
                                loadedCatalog.sceneStartEvents != null &&
                                loadedCatalog.sceneStartEvents.Count > 0;

        if (hasCatalogEvents)
        {
            for (int i = 0; i < loadedCatalog.sceneStartEvents.Count; i++)
            {
                StoryEventDefinition definition = loadedCatalog.sceneStartEvents[i];
                if (definition == null || !definition.MatchesScene(sceneName))
                {
                    continue;
                }

                eventRunner.Enqueue(definition);
            }

            yield break;
        }

        StoryEventDefinition fallbackDefinition = CreateFallbackPrologueDefinition(sceneName);
        if (fallbackDefinition != null)
        {
            eventRunner.Enqueue(fallbackDefinition);
        }
    }

    private static StoryEventDefinition CreateFallbackPrologueDefinition(string sceneName)
    {
        if (!string.Equals(sceneName, DefaultEntrySceneName, StringComparison.Ordinal))
        {
            return null;
        }

        return new StoryEventDefinition
        {
            eventId = "prologue",
            sceneName = DefaultEntrySceneName,
            dialogueNodeName = "Prologue",
            dialogueStyle = DialogueStyle.ADV,
            pausePolicy = StoryPausePolicy.GameplayOnly,
            runOnceFlagKey = GameProgressKeys.PrologueCompleted,
            conditions = new StoryFlagConditionSet(),
            onStartMutations = new StoryFlagMutationSet
            {
                setTrueFlags = new[] { GameProgressKeys.PrologueStarted },
                setFalseFlags = new[] { GameProgressKeys.PrologueCompleted }
            },
            onCompleteMutations = new StoryFlagMutationSet
            {
                setTrueFlags = new[] { GameProgressKeys.PrologueCompleted }
            },
            autoSaveOnComplete = true,
            skipWhenDialogueRunning = true
        };
    }
}
