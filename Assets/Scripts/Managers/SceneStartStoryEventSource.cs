using System.Collections.Generic;
using Metroidvania.Managers;
using UnityEngine;

[AddComponentMenu("CaseStudy/Story/Scene Start Story Event Source")]
public sealed class SceneStartStoryEventSource : MonoBehaviour
{
    private static readonly List<SceneStartStoryEventSource> RegisteredSourcesInternal =
        new List<SceneStartStoryEventSource>();

    public static IReadOnlyList<SceneStartStoryEventSource> RegisteredSources
    {
        get
        {
            PruneRegisteredSources();
            return RegisteredSourcesInternal;
        }
    }

    [Header("Story Event")]
    [SerializeField] private string eventId = "prologue";
    [SerializeField] private string storyEventControllerId = string.Empty;
    [SerializeField] private string dialogueNodeName = "Prologue";
    [SerializeField] private DialogueStyle dialogueStyle = DialogueStyle.Bubble;
    [SerializeField] private StoryPausePolicy pausePolicy = StoryPausePolicy.GameplayOnly;
    [SerializeField] private string runOnceFlagKey = GameProgressKeys.PrologueCompleted;
    [SerializeField] private bool autoSaveOnComplete = true;
    [SerializeField] private bool skipWhenDialogueRunning = true;

    [Header("Flags")]
    [SerializeField] private StoryFlagConditionSet conditions = new StoryFlagConditionSet();
    [SerializeField] private StoryFlagMutationSet onStartMutations = new StoryFlagMutationSet();
    [SerializeField] private StoryFlagMutationSet onCompleteMutations = new StoryFlagMutationSet();

    [Header("Actions")]
    [SerializeField] private List<StoryEventActionDefinition> preActions = new List<StoryEventActionDefinition>();
    [SerializeField] private List<StoryEventActionDefinition> postActions = new List<StoryEventActionDefinition>();

    private void Reset()
    {
        eventId = "prologue";
        storyEventControllerId = string.Empty;
        dialogueNodeName = "Prologue";
        dialogueStyle = DialogueStyle.Bubble;
        pausePolicy = StoryPausePolicy.GameplayOnly;
        runOnceFlagKey = GameProgressKeys.PrologueCompleted;
        autoSaveOnComplete = true;
        skipWhenDialogueRunning = true;

        conditions = new StoryFlagConditionSet();
        onStartMutations = new StoryFlagMutationSet
        {
            setTrueFlags = new[] { GameProgressKeys.PrologueStarted },
            setFalseFlags = new[] { GameProgressKeys.PrologueCompleted }
        };
        onCompleteMutations = new StoryFlagMutationSet
        {
            setTrueFlags = new[] { GameProgressKeys.PrologueCompleted }
        };

        preActions = new List<StoryEventActionDefinition>();
        postActions = new List<StoryEventActionDefinition>();
    }

    private void Awake()
    {
        RegisterSource(this);
    }

    private void OnDestroy()
    {
        RegisteredSourcesInternal.Remove(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegisteredSources()
    {
        RegisteredSourcesInternal.Clear();
    }

    private static void RegisterSource(SceneStartStoryEventSource source)
    {
        if (source != null && !RegisteredSourcesInternal.Contains(source))
        {
            RegisteredSourcesInternal.Add(source);
        }
    }

    private static void PruneRegisteredSources()
    {
        for (int i = RegisteredSourcesInternal.Count - 1; i >= 0; i--)
        {
            if (RegisteredSourcesInternal[i] == null)
            {
                RegisteredSourcesInternal.RemoveAt(i);
            }
        }
    }

    public StoryEventDefinition CreateDefinition(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(dialogueNodeName) &&
            string.IsNullOrWhiteSpace(storyEventControllerId))
        {
            return null;
        }

        return new StoryEventDefinition
        {
            eventId = string.IsNullOrWhiteSpace(eventId) ? "scene_start_event" : eventId.Trim(),
            sceneName = sceneName,
            storyEventControllerId = string.IsNullOrWhiteSpace(storyEventControllerId)
                ? string.Empty
                : storyEventControllerId.Trim(),
            dialogueNodeName = string.IsNullOrWhiteSpace(dialogueNodeName)
                ? string.Empty
                : dialogueNodeName.Trim(),
            dialogueStyle = dialogueStyle,
            runOnceFlagKey = runOnceFlagKey,
            conditions = conditions ?? new StoryFlagConditionSet(),
            onStartMutations = onStartMutations ?? new StoryFlagMutationSet(),
            onCompleteMutations = onCompleteMutations ?? new StoryFlagMutationSet(),
            preActions = preActions ?? new List<StoryEventActionDefinition>(),
            postActions = postActions ?? new List<StoryEventActionDefinition>(),
            pausePolicy = pausePolicy,
            autoSaveOnComplete = autoSaveOnComplete,
            skipWhenDialogueRunning = skipWhenDialogueRunning
        };
    }
}
