using System;
using System.Collections;
using System.Collections.Generic;
using Metroidvania.Managers;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Yarn.Unity;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayableDirector))]
[AddComponentMenu("CaseStudy/Story/Story Event Controller")]
public sealed class StoryEventController : MonoBehaviour, INotificationReceiver
{
    [Serializable]
    private sealed class ActorBinding
    {
        public string actorKey = "actor";
        public Transform actorRoot;
        public Transform bubbleTarget;
    }

    [Header("Identity")]
    [SerializeField] private string eventId = "story_event";
    [SerializeField] private string sceneName = string.Empty;
    [SerializeField] private bool playOnStart;

    [Header("Timeline")]
    [SerializeField] private PlayableDirector director;
    [SerializeField] private bool forceUnscaledTime = true;

    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueStyle defaultDialogueStyle = DialogueStyle.Bubble;
    [SerializeField] private Transform defaultBubbleTarget;
    [SerializeField] private bool skipWhenDialogueRunning = true;

    [Header("Flags")]
    [SerializeField] private string runOnceFlagKey = string.Empty;
    [SerializeField] private StoryFlagConditionSet conditions = new StoryFlagConditionSet();
    [SerializeField] private StoryFlagMutationSet onStartMutations = new StoryFlagMutationSet();
    [SerializeField] private StoryFlagMutationSet onCompleteMutations = new StoryFlagMutationSet();

    [Header("Completion")]
    [SerializeField] private StoryPausePolicy pausePolicy = StoryPausePolicy.GameplayOnly;
    [SerializeField] private bool autoSaveOnComplete = true;
    [SerializeField] private bool markRunOnceFlagOnComplete = true;

    [Header("Bindings")]
    [SerializeField] private List<ActorBinding> actorBindings = new List<ActorBinding>();

    private readonly Dictionary<int, StoryEventMarker> markerByNo = new Dictionary<int, StoryEventMarker>();
    private readonly Dictionary<string, StoryEventActor> actorByKey =
        new Dictionary<string, StoryEventActor>(StringComparer.OrdinalIgnoreCase);

    private Coroutine playRoutine;
    private Coroutine dialogueRoutine;
    private DialogueRunner activeDialogueRunner;
    private bool waitingDialogueCompletion;
    private bool directorStopped;
    private bool startMutationsApplied;
    private bool completeMutationsApplied;

    public string EventId => string.IsNullOrWhiteSpace(eventId) ? name : eventId.Trim();
    public bool IsPlaying => playRoutine != null;
    public PlayableDirector Director => ResolveDirector();

    private void Reset()
    {
        eventId = name;
        director = GetComponent<PlayableDirector>();
        defaultBubbleTarget = transform;
    }

    private void Awake()
    {
        ResolveDirector();
        RebuildLookupCache();
    }

    private void Start()
    {
        if (playOnStart)
        {
            PlayEvent();
        }
    }

    private void OnDisable()
    {
        StopEvent();
    }

    public bool PlayEvent()
    {
        if (playRoutine != null)
        {
            return false;
        }

        if (!CanRun())
        {
            return false;
        }

        PlayableDirector resolvedDirector = ResolveDirector();
        if (resolvedDirector == null || resolvedDirector.playableAsset == null)
        {
            Debug.LogWarning($"[StoryEventController] Timeline is missing. eventId='{EventId}'", this);
            return false;
        }

        playRoutine = StartCoroutine(PlayEventRoutine(resolvedDirector));
        return true;
    }

    public void StopEvent()
    {
        if (dialogueRoutine != null)
        {
            StopCoroutine(dialogueRoutine);
            dialogueRoutine = null;
        }

        UnsubscribeDialogueComplete();
        waitingDialogueCompletion = false;

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        PlayableDirector resolvedDirector = ResolveDirector();
        if (resolvedDirector != null)
        {
            resolvedDirector.stopped -= OnDirectorStopped;
            if (resolvedDirector.state == PlayState.Playing || resolvedDirector.state == PlayState.Paused)
            {
                resolvedDirector.Stop();
            }
        }

        StoryPauseRuntime.ClearOverride();
        directorStopped = false;
        startMutationsApplied = false;
    }

    public Transform GetMarkerTransform(int markerNo)
    {
        RebuildLookupCache();
        return markerByNo.TryGetValue(Mathf.Max(1, markerNo), out StoryEventMarker marker) && marker != null
            ? marker.Target
            : null;
    }

    public Transform GetActorTransform(string actorKey)
    {
        if (string.IsNullOrWhiteSpace(actorKey))
        {
            return null;
        }

        RebuildLookupCache();
        string key = actorKey.Trim();

        for (int i = 0; i < actorBindings.Count; i++)
        {
            ActorBinding binding = actorBindings[i];
            if (binding == null || binding.actorRoot == null || string.IsNullOrWhiteSpace(binding.actorKey))
            {
                continue;
            }

            if (string.Equals(binding.actorKey.Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                return binding.actorRoot;
            }
        }

        if (actorByKey.TryGetValue(key, out StoryEventActor actor) && actor != null)
        {
            return actor.Root;
        }

        if (string.Equals(key, "iris", StringComparison.OrdinalIgnoreCase))
        {
            global::PlayerController player =
                FindFirstObjectByType<global::PlayerController>(FindObjectsInactive.Include);
            return player != null ? player.transform : null;
        }

        return null;
    }

    public void OnNotify(Playable origin, INotification notification, object context)
    {
        if (notification is StoryYarnDialogueMarker dialogueMarker)
        {
            StartDialogueFromTimeline(dialogueMarker);
            return;
        }

        if (notification is StoryAutoSaveMarker autoSaveMarker)
        {
            if (autoSaveMarker.ApplyCompleteMutationsBeforeSave)
            {
                ApplyCompleteMutations();
            }

            SaveManager.TrySaveCurrentGame();
        }
    }

    private IEnumerator PlayEventRoutine(PlayableDirector resolvedDirector)
    {
        directorStopped = false;
        completeMutationsApplied = false;

        StoryPauseRuntime.SetOverride(pausePolicy);
        ApplyStartMutations();

        resolvedDirector.stopped -= OnDirectorStopped;
        resolvedDirector.stopped += OnDirectorStopped;

        if (forceUnscaledTime)
        {
            resolvedDirector.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        }

        resolvedDirector.time = 0d;
        resolvedDirector.Evaluate();
        resolvedDirector.Play();

        while (!directorStopped)
        {
            yield return null;
        }

        resolvedDirector.stopped -= OnDirectorStopped;
        ApplyCompletionState();
        StoryPauseRuntime.ClearOverride();

        playRoutine = null;
    }

    private bool CanRun()
    {
        if (!string.IsNullOrWhiteSpace(sceneName) &&
            !StoryEventDefinition.MatchesConfiguredScene(sceneName, SceneManager.GetActiveScene().name))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(runOnceFlagKey) && GameProgressFlags.Get(runOnceFlagKey.Trim()))
        {
            return false;
        }

        return conditions == null || conditions.IsSatisfied();
    }

    private void StartDialogueFromTimeline(StoryYarnDialogueMarker marker)
    {
        if (marker == null)
        {
            return;
        }

        if (dialogueRoutine != null)
        {
            StopCoroutine(dialogueRoutine);
            UnsubscribeDialogueComplete();
        }

        dialogueRoutine = StartCoroutine(PlayDialogueRoutine(marker));
    }

    private IEnumerator PlayDialogueRoutine(StoryYarnDialogueMarker marker)
    {
        PlayableDirector resolvedDirector = ResolveDirector();
        bool shouldPauseTimeline = marker.PauseTimelineUntilComplete && resolvedDirector != null;

        if (shouldPauseTimeline)
        {
            resolvedDirector.Pause();
        }

        ResolveDialogueManagerIfNeeded();
        if (dialogueManager == null || dialogueManager.Runner == null)
        {
            Debug.LogWarning($"[StoryEventController] DialogueManager not found. eventId='{EventId}'", this);
            ResumeDirectorIfNeeded(resolvedDirector, shouldPauseTimeline);
            dialogueRoutine = null;
            yield break;
        }

        DialogueRunner runner = dialogueManager.Runner;
        if (runner.Dialogue == null || !runner.Dialogue.NodeExists(marker.NodeName))
        {
            Debug.LogWarning(
                $"[StoryEventController] Dialogue node not found. eventId='{EventId}', node='{marker.NodeName}'",
                this);
            ResumeDirectorIfNeeded(resolvedDirector, shouldPauseTimeline);
            dialogueRoutine = null;
            yield break;
        }

        if (runner.IsDialogueRunning)
        {
            if (skipWhenDialogueRunning)
            {
                Debug.LogWarning(
                    $"[StoryEventController] Dialogue already running. Skip node='{marker.NodeName}', eventId='{EventId}'",
                    this);
                ResumeDirectorIfNeeded(resolvedDirector, shouldPauseTimeline);
                dialogueRoutine = null;
                yield break;
            }

            runner.Stop();
        }

        activeDialogueRunner = runner;
        waitingDialogueCompletion = true;
        activeDialogueRunner.onDialogueComplete?.AddListener(OnDialogueComplete);

        DialogueStyle style = marker.UseControllerDefaultStyle ? defaultDialogueStyle : marker.DialogueStyle;
        Transform bubbleTarget = ResolveBubbleTarget(marker.BubbleActorKey);
        dialogueManager.StartConversation(marker.NodeName, style, bubbleTarget);

        while (waitingDialogueCompletion)
        {
            yield return null;
        }

        UnsubscribeDialogueComplete();
        ResumeDirectorIfNeeded(resolvedDirector, shouldPauseTimeline);
        dialogueRoutine = null;
    }

    private void ResumeDirectorIfNeeded(PlayableDirector resolvedDirector, bool shouldResume)
    {
        if (!shouldResume || resolvedDirector == null || directorStopped)
        {
            return;
        }

        resolvedDirector.time += 0.0001d;
        resolvedDirector.Resume();
    }

    private Transform ResolveBubbleTarget(string actorKey)
    {
        if (!string.IsNullOrWhiteSpace(actorKey))
        {
            string key = actorKey.Trim();
            for (int i = 0; i < actorBindings.Count; i++)
            {
                ActorBinding binding = actorBindings[i];
                if (binding == null || string.IsNullOrWhiteSpace(binding.actorKey))
                {
                    continue;
                }

                if (string.Equals(binding.actorKey.Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    if (binding.bubbleTarget != null)
                    {
                        return binding.bubbleTarget;
                    }

                    if (binding.actorRoot != null)
                    {
                        return binding.actorRoot;
                    }
                }
            }

            RebuildLookupCache();
            if (actorByKey.TryGetValue(key, out StoryEventActor actor) && actor != null)
            {
                return actor.BubbleTarget;
            }

            Transform actorTransform = GetActorTransform(key);
            if (actorTransform != null)
            {
                return actorTransform;
            }
        }

        return defaultBubbleTarget != null ? defaultBubbleTarget : transform;
    }

    private void ApplyStartMutations()
    {
        if (startMutationsApplied)
        {
            return;
        }

        startMutationsApplied = true;
        onStartMutations?.Apply();
    }

    private void ApplyCompletionState()
    {
        ApplyCompleteMutations();

        if (markRunOnceFlagOnComplete && !string.IsNullOrWhiteSpace(runOnceFlagKey))
        {
            GameProgressFlags.Set(runOnceFlagKey.Trim(), true);
        }

        if (autoSaveOnComplete)
        {
            SaveManager.TrySaveCurrentGame();
        }
    }

    private void ApplyCompleteMutations()
    {
        if (completeMutationsApplied)
        {
            return;
        }

        completeMutationsApplied = true;
        onCompleteMutations?.Apply();
    }

    private void OnDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (stoppedDirector == director)
        {
            directorStopped = true;
        }
    }

    private void OnDialogueComplete()
    {
        waitingDialogueCompletion = false;
    }

    private void UnsubscribeDialogueComplete()
    {
        if (activeDialogueRunner != null)
        {
            activeDialogueRunner.onDialogueComplete?.RemoveListener(OnDialogueComplete);
            activeDialogueRunner = null;
        }

        waitingDialogueCompletion = false;
    }

    private PlayableDirector ResolveDirector()
    {
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }

        return director;
    }

    private void ResolveDialogueManagerIfNeeded()
    {
        if (dialogueManager == null)
        {
            dialogueManager = FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
        }
    }

    private void RebuildLookupCache()
    {
        markerByNo.Clear();
        StoryEventMarker[] markers = GetComponentsInChildren<StoryEventMarker>(includeInactive: true);
        for (int i = 0; i < markers.Length; i++)
        {
            StoryEventMarker marker = markers[i];
            if (marker == null)
            {
                continue;
            }

            int markerNo = marker.MarkerNo;
            if (!markerByNo.ContainsKey(markerNo))
            {
                markerByNo.Add(markerNo, marker);
            }
        }

        actorByKey.Clear();
        StoryEventActor[] actors = GetComponentsInChildren<StoryEventActor>(includeInactive: true);
        for (int i = 0; i < actors.Length; i++)
        {
            StoryEventActor actor = actors[i];
            if (actor == null || string.IsNullOrWhiteSpace(actor.ActorKey))
            {
                continue;
            }

            if (!actorByKey.ContainsKey(actor.ActorKey))
            {
                actorByKey.Add(actor.ActorKey, actor);
            }
        }
    }
}
