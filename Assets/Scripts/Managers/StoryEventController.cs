using System;
using System.Collections;
using System.Collections.Generic;
using Metroidvania.Managers;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Yarn.Unity;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayableDirector))]
[AddComponentMenu("CaseStudy/Story/Story Event Controller")]
public sealed class StoryEventController : MonoBehaviour, INotificationReceiver
{
    private static readonly string[] PlayerControlBehaviourNames =
    {
        "PlayerController",
        "PlayerController_ozono",
        "PlayerPlatformerMockController",
        "DodgeController",
        "PlayerShooter",
        "GunController",
        "UmbrellaController",
        "UmbrellaAttackController",
        "UmbrellaParryController"
    };

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
    [SerializeField] private bool disableDirectorPlayOnAwake = true;

    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private DialogueStyle defaultDialogueStyle = DialogueStyle.Bubble;
    [SerializeField] private Transform defaultBubbleTarget;
    [SerializeField] private bool skipWhenDialogueRunning = true;

    [Header("Panels")]
    [SerializeField] private EventPanelPresenter panelPresenter;
    [SerializeField] private string panelPresenterName = "EventPanelPresenter";

    [Header("Flags")]
    [SerializeField] private string runOnceFlagKey = string.Empty;
    [SerializeField] private StoryFlagConditionSet conditions = new StoryFlagConditionSet();
    [SerializeField] private StoryFlagMutationSet onStartMutations = new StoryFlagMutationSet();
    [SerializeField] private StoryFlagMutationSet onCompleteMutations = new StoryFlagMutationSet();

    [Header("Completion")]
    [SerializeField] private StoryPausePolicy pausePolicy = StoryPausePolicy.GameplayOnly;
    [SerializeField] private bool autoSaveOnComplete = true;
    [SerializeField] private bool markRunOnceFlagOnComplete = true;

    [Header("Cinematic State")]
    [SerializeField] private bool lockPlayerControlDuringEvent = true;
    [SerializeField] private bool lockPlayerFacingDuringEvent = true;
    [SerializeField] private bool restoreActorTransformsOnExit = true;
    [SerializeField] private bool restoreSpriteFacingOnExit = true;
    [SerializeField] private bool restoreRigidbodyVelocityOnExit = false;

    [Header("Letter Box")]
    [SerializeField] private bool showLetterBoxDuringEvent = true;
    [SerializeField] private GameObject letterBoxView;
    [SerializeField] private string letterBoxViewName = "LetterBoxView";
    [SerializeField] private float letterBoxFadeSeconds = 0.6f;
    [SerializeField] private float letterBoxSlidePixels = 120f;

    [Header("Event Camera")]
    [SerializeField] private bool useEventCameraDuringEvent = true;
    [SerializeField] private CinemachineCamera eventCamera;
    [SerializeField] private string eventCameraName = "EventCam";
    [SerializeField] private int eventCameraPriorityFloor = 100;

    [Header("Bindings")]
    [SerializeField] private List<ActorBinding> actorBindings = new List<ActorBinding>();

    private readonly Dictionary<int, StoryEventMarker> markerByNo = new Dictionary<int, StoryEventMarker>();
    private readonly Dictionary<string, StoryEventActor> actorByKey =
        new Dictionary<string, StoryEventActor>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> firedDialogueClipKeys = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> firedPanelClipKeys = new HashSet<string>(StringComparer.Ordinal);

    private Coroutine playRoutine;
    private Coroutine dialogueRoutine;
    private DialogueRunner activeDialogueRunner;
    private bool waitingDialogueCompletion;
    private Coroutine panelRoutine;
    private EventPanelPresenter activePanelPresenter;
    private PlayableDirector panelPausedDirector;
    private bool shouldResumePanelPausedDirector;
    private bool directorStopped;
    private bool startMutationsApplied;
    private bool completeMutationsApplied;
    private CinematicStateSnapshot cinematicSnapshot;
    private bool cachedLetterBoxViewActiveSelf;
    private float cachedLetterBoxAlpha = 1f;
    private RectTransform cachedLetterBoxTop;
    private RectTransform cachedLetterBoxBottom;
    private Vector2 cachedLetterBoxTopAnchoredPosition;
    private Vector2 cachedLetterBoxBottomAnchoredPosition;
    private Coroutine letterBoxFadeRoutine;
    private bool hasCachedLetterBoxViewState;
    private CinemachineCamera activeEventCamera;
    private CinemachineCamera runtimeEventCamera;
    private int cachedEventCameraPriorityValue;
    private bool cachedEventCameraPriorityEnabled;
    private bool hasCachedEventCameraPriority;

    public string EventId => string.IsNullOrWhiteSpace(eventId) ? name : eventId.Trim();
    public bool IsPlaying => playRoutine != null;
    public PlayableDirector Director => ResolveDirector();

    private void Reset()
    {
        eventId = name;
        director = GetComponent<PlayableDirector>();
        if (director != null)
        {
            director.playOnAwake = false;
        }

        defaultBubbleTarget = transform;
    }

    private void Awake()
    {
        ResolveDirector();
        DisableDirectorPlayOnAwakeIfNeeded();
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
        StopActiveDialogue();
        StopPanelFromTimeline(resumeDirector: false);

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
        RestoreLetterBoxViewVisibility();
        RestoreEventCameraPriority();
        RestoreCinematicState();
        directorStopped = false;
        startMutationsApplied = false;
        firedDialogueClipKeys.Clear();
        firedPanelClipKeys.Clear();
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

    public CinemachineCamera GetEventCameraForTimeline()
    {
        return activeEventCamera != null ? activeEventCamera : ResolveEventCamera();
    }

    public void OnNotify(Playable origin, INotification notification, object context)
    {
        if (notification is StoryYarnDialogueMarker dialogueMarker)
        {
            TryStartDialogueFromTimeline(
                dialogueMarker.TriggerKey,
                dialogueMarker.NodeName,
                dialogueMarker.UseControllerDefaultStyle,
                dialogueMarker.DialogueStyle,
                dialogueMarker.PauseTimelineUntilComplete,
                dialogueMarker.BubbleActorKey);
            return;
        }

        if (notification is StoryAudioMarker audioMarker)
        {
            PlayAudioMarker(audioMarker);
            return;
        }

        if (notification is StoryCameraShakeMarker shakeMarker)
        {
            PlayCameraShakeMarker(shakeMarker);
            return;
        }

        if (notification is EventPanelMarker panelMarker)
        {
            EventPanelPresenter panelPresenterOverride = panelMarker.ResolvePanelPresenter(origin);
            if (panelMarker.UsesExistingPanel)
            {
                TryShowExistingPanelFromTimeline(
                    panelMarker.TriggerKey,
                    panelMarker.PanelPresenterName,
                    panelPresenterOverride,
                    panelMarker.PauseTimelineUntilClosed,
                    panelMarker.AutoCloseSecondsWhenNoButton);
            }
            else
            {
                TryShowPanelFromTimeline(
                    panelMarker.TriggerKey,
                    panelMarker.BuildContent(),
                    panelMarker.PauseTimelineUntilClosed,
                    panelMarker.AutoCloseSecondsWhenNoButton,
                    panelMarker.PanelPresenterName,
                    panelPresenterOverride);
            }

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

    private static void PlayAudioMarker(StoryAudioMarker marker)
    {
        if (marker == null || !Application.isPlaying)
        {
            return;
        }

        if (marker.AudioKind == StoryTimelineAudioKind.Bgm)
        {
            if (marker.Action == StoryTimelineAudioAction.Stop)
            {
                StoryTimelineRuntime.Instance.StopBgm(marker.FadeSeconds);
                return;
            }

            StoryTimelineRuntime.Instance.PlayBgm(marker.AudioClip, marker.Volume, marker.Loop, marker.FadeSeconds);
            return;
        }

        if (marker.Action == StoryTimelineAudioAction.Play)
        {
            StoryTimelineRuntime.Instance.PlaySe(marker.AudioClip, marker.Volume);
        }
    }

    private static void PlayCameraShakeMarker(StoryCameraShakeMarker marker)
    {
        if (marker == null || !Application.isPlaying)
        {
            return;
        }

        CameraManager cameraManager = CameraManager.Instance;
        if (cameraManager == null)
        {
            cameraManager = FindFirstObjectByType<CameraManager>(FindObjectsInactive.Include);
        }

        if (cameraManager != null)
        {
            cameraManager.PlayShake(marker.Force, ResolveShakeDirection(marker.Direction, marker.CustomDirection));
            return;
        }

        Debug.LogWarning("[StoryEventController] CameraManager was not found. Shake marker was skipped.");
    }

    private static Vector3 ResolveShakeDirection(StoryCameraShakeDirection direction, Vector2 customDirection)
    {
        Vector2 resolved = direction switch
        {
            StoryCameraShakeDirection.Vertical => Vector2.up,
            StoryCameraShakeDirection.Diagonal => new Vector2(1f, 1f),
            StoryCameraShakeDirection.Custom => customDirection,
            _ => Vector2.right,
        };

        if (resolved.sqrMagnitude <= Mathf.Epsilon)
        {
            resolved = Vector2.right;
        }

        resolved.Normalize();
        return new Vector3(resolved.x, resolved.y, 0f);
    }

    private IEnumerator PlayEventRoutine(PlayableDirector resolvedDirector)
    {
        directorStopped = false;
        completeMutationsApplied = false;
        firedDialogueClipKeys.Clear();
        firedPanelClipKeys.Clear();

        StoryPauseRuntime.SetOverride(pausePolicy);
        CaptureCinematicState();
        ApplyCinematicState();
        ElevateEventCameraPriority();
        ShowLetterBoxView();
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
        RestoreLetterBoxViewVisibility();
        RestoreEventCameraPriority();
        RestoreCinematicState();

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

    public bool TryStartDialogueFromTimeline(
        string clipKey,
        string nodeName,
        bool useControllerDefaultStyle,
        DialogueStyle dialogueStyle,
        bool pauseTimelineUntilComplete,
        string bubbleActorKey)
    {
        string resolvedNodeName = string.IsNullOrWhiteSpace(nodeName) ? "Start" : nodeName.Trim();
        string resolvedBubbleActorKey = string.IsNullOrWhiteSpace(bubbleActorKey) ? string.Empty : bubbleActorKey.Trim();
        string resolvedClipKey = BuildDialogueClipKey(
            clipKey,
            resolvedNodeName,
            useControllerDefaultStyle,
            dialogueStyle,
            pauseTimelineUntilComplete,
            resolvedBubbleActorKey);

        if (!firedDialogueClipKeys.Add(resolvedClipKey))
        {
            return false;
        }

        if (dialogueRoutine != null)
        {
            StopCoroutine(dialogueRoutine);
            UnsubscribeDialogueComplete();
        }

        dialogueRoutine = StartCoroutine(PlayDialogueRoutine(
            resolvedNodeName,
            useControllerDefaultStyle,
            dialogueStyle,
            pauseTimelineUntilComplete,
            resolvedBubbleActorKey));
        return true;
    }

    public void StartDialogueFromTimeline(
        string nodeName,
        bool useControllerDefaultStyle,
        DialogueStyle dialogueStyle,
        bool pauseTimelineUntilComplete,
        string bubbleActorKey)
    {
        TryStartDialogueFromTimeline(
            string.Empty,
            nodeName,
            useControllerDefaultStyle,
            dialogueStyle,
            pauseTimelineUntilComplete,
            bubbleActorKey);
    }

    public bool TryShowPanelFromTimeline(
        string clipKey,
        EventPanelContent content,
        bool pauseTimelineUntilClosed,
        float autoCloseSecondsWhenNoButton,
        string panelPresenterNameOverride = null,
        EventPanelPresenter panelPresenterOverride = null)
    {
        string resolvedClipKey =
            BuildPanelClipKey(clipKey, content, pauseTimelineUntilClosed, autoCloseSecondsWhenNoButton);
        if (!firedPanelClipKeys.Add(resolvedClipKey))
        {
            return false;
        }

        StopPanelFromTimeline(resumeDirector: true);
        panelRoutine = StartCoroutine(
            ShowPanelRoutine(
                content,
                pauseTimelineUntilClosed,
                autoCloseSecondsWhenNoButton,
                panelPresenterNameOverride,
                panelPresenterOverride));
        return true;
    }

    public bool TryShowExistingPanelFromTimeline(
        string clipKey,
        string panelPresenterNameOverride,
        EventPanelPresenter panelPresenterOverride,
        bool pauseTimelineUntilClosed,
        float autoCloseSecondsWhenNoButton)
    {
        string resolvedClipKey =
            BuildPanelClipKey(clipKey, null, pauseTimelineUntilClosed, autoCloseSecondsWhenNoButton);
        if (!firedPanelClipKeys.Add(resolvedClipKey))
        {
            return false;
        }

        StopPanelFromTimeline(resumeDirector: true);
        panelRoutine = StartCoroutine(
            ShowExistingPanelRoutine(
                pauseTimelineUntilClosed,
                autoCloseSecondsWhenNoButton,
                panelPresenterNameOverride,
                panelPresenterOverride));
        return true;
    }

    private static string BuildDialogueClipKey(
        string clipKey,
        string nodeName,
        bool useControllerDefaultStyle,
        DialogueStyle dialogueStyle,
        bool pauseTimelineUntilComplete,
        string bubbleActorKey)
    {
        if (!string.IsNullOrWhiteSpace(clipKey))
        {
            return clipKey.Trim();
        }

        return string.Join(
            "|",
            nodeName,
            useControllerDefaultStyle ? "default" : dialogueStyle.ToString(),
            pauseTimelineUntilComplete ? "pause" : "continue",
            bubbleActorKey);
    }

    private static string BuildPanelClipKey(
        string clipKey,
        EventPanelContent content,
        bool pauseTimelineUntilClosed,
        float autoCloseSecondsWhenNoButton)
    {
        if (!string.IsNullOrWhiteSpace(clipKey))
        {
            return clipKey.Trim();
        }

        string title = content != null && !string.IsNullOrWhiteSpace(content.title)
            ? content.title.Trim()
            : string.Empty;
        string body = content != null && !string.IsNullOrWhiteSpace(content.body)
            ? content.body.Trim()
            : string.Empty;
        EventPanelKind kind = content != null ? content.kind : EventPanelKind.Custom;

        return string.Join(
            "|",
            kind.ToString(),
            title,
            body,
            pauseTimelineUntilClosed ? "pause" : "continue",
            autoCloseSecondsWhenNoButton.ToString("0.###"));
    }

    private IEnumerator ShowPanelRoutine(
        EventPanelContent content,
        bool pauseTimelineUntilClosed,
        float autoCloseSecondsWhenNoButton,
        string panelPresenterNameOverride,
        EventPanelPresenter panelPresenterOverride)
    {
        PlayableDirector resolvedDirector = ResolveDirector();
        bool shouldPauseTimeline = pauseTimelineUntilClosed && resolvedDirector != null;
        if (shouldPauseTimeline)
        {
            resolvedDirector.Pause();
            panelPausedDirector = resolvedDirector;
            shouldResumePanelPausedDirector = true;
        }

        EventPanelPresenter presenter = ResolvePanelPresenter(panelPresenterNameOverride, panelPresenterOverride);
        if (presenter == null)
        {
            ResumePanelPausedDirectorIfNeeded();
            panelRoutine = null;
            yield break;
        }

        activePanelPresenter = presenter;
        bool panelClosed = false;
        bool panelShown = presenter.Show(
            content,
            () =>
            {
                panelClosed = true;
                if (activePanelPresenter == presenter)
                {
                    activePanelPresenter = null;
                }
            },
            autoCloseSecondsWhenNoButton);

        if (!panelShown)
        {
            activePanelPresenter = null;
            ResumePanelPausedDirectorIfNeeded();
            panelRoutine = null;
            yield break;
        }

        if (shouldPauseTimeline)
        {
            while (!panelClosed)
            {
                yield return null;
            }

            ResumePanelPausedDirectorIfNeeded();
        }

        panelRoutine = null;
    }

    private IEnumerator ShowExistingPanelRoutine(
        bool pauseTimelineUntilClosed,
        float autoCloseSecondsWhenNoButton,
        string panelPresenterNameOverride,
        EventPanelPresenter panelPresenterOverride)
    {
        PlayableDirector resolvedDirector = ResolveDirector();
        bool shouldPauseTimeline = pauseTimelineUntilClosed && resolvedDirector != null;
        if (shouldPauseTimeline)
        {
            resolvedDirector.Pause();
            panelPausedDirector = resolvedDirector;
            shouldResumePanelPausedDirector = true;
        }

        EventPanelPresenter presenter = ResolvePanelPresenter(panelPresenterNameOverride, panelPresenterOverride);
        if (presenter == null)
        {
            ResumePanelPausedDirectorIfNeeded();
            panelRoutine = null;
            yield break;
        }

        activePanelPresenter = presenter;
        bool panelClosed = false;
        bool panelShown = presenter.ShowExisting(
            () =>
            {
                panelClosed = true;
                if (activePanelPresenter == presenter)
                {
                    activePanelPresenter = null;
                }
            },
            autoCloseSecondsWhenNoButton);

        if (!panelShown)
        {
            activePanelPresenter = null;
            ResumePanelPausedDirectorIfNeeded();
            panelRoutine = null;
            yield break;
        }

        if (shouldPauseTimeline)
        {
            while (!panelClosed)
            {
                yield return null;
            }

            ResumePanelPausedDirectorIfNeeded();
        }

        panelRoutine = null;
    }

    private void StopPanelFromTimeline(bool resumeDirector)
    {
        if (panelRoutine != null)
        {
            StopCoroutine(panelRoutine);
            panelRoutine = null;
        }

        if (activePanelPresenter != null)
        {
            activePanelPresenter.HideWithoutCallback();
            activePanelPresenter = null;
        }

        if (resumeDirector)
        {
            ResumePanelPausedDirectorIfNeeded();
            return;
        }

        panelPausedDirector = null;
        shouldResumePanelPausedDirector = false;
    }

    private void ResumePanelPausedDirectorIfNeeded()
    {
        PlayableDirector resolvedDirector = panelPausedDirector;
        bool shouldResume = shouldResumePanelPausedDirector;
        panelPausedDirector = null;
        shouldResumePanelPausedDirector = false;

        ResumeDirectorIfNeeded(resolvedDirector, shouldResume);
    }

    private IEnumerator PlayDialogueRoutine(
        string nodeName,
        bool useControllerDefaultStyle,
        DialogueStyle dialogueStyle,
        bool pauseTimelineUntilComplete,
        string bubbleActorKey)
    {
        PlayableDirector resolvedDirector = ResolveDirector();
        bool shouldPauseTimeline = pauseTimelineUntilComplete && resolvedDirector != null;

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
        if (runner.Dialogue == null || !runner.Dialogue.NodeExists(nodeName))
        {
            Debug.LogWarning(
                $"[StoryEventController] Dialogue node not found. eventId='{EventId}', node='{nodeName}'",
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
                    $"[StoryEventController] Dialogue already running. Skip node='{nodeName}', eventId='{EventId}'",
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

        DialogueStyle style = useControllerDefaultStyle ? defaultDialogueStyle : dialogueStyle;
        Transform bubbleTarget = ResolveBubbleTarget(bubbleActorKey);
        dialogueManager.StartConversation(nodeName, style, bubbleTarget);

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

    private void StopActiveDialogue()
    {
        DialogueRunner runner = activeDialogueRunner;
        if (runner == null && dialogueManager != null)
        {
            runner = dialogueManager.Runner;
        }

        if (runner != null && runner.IsDialogueRunning)
        {
            runner.Stop();
        }
    }

    private void CaptureCinematicState()
    {
        cinematicSnapshot = CinematicStateSnapshot.Capture(
            this,
            restoreActorTransformsOnExit,
            restoreSpriteFacingOnExit,
            restoreRigidbodyVelocityOnExit);
    }

    private void ApplyCinematicState()
    {
        if (cinematicSnapshot == null)
        {
            return;
        }

        cinematicSnapshot.ApplyCinematicLocks(lockPlayerControlDuringEvent, lockPlayerFacingDuringEvent);
    }

    private void RestoreCinematicState()
    {
        if (cinematicSnapshot == null)
        {
            return;
        }

        cinematicSnapshot.Restore();
        cinematicSnapshot = null;
    }

    private void ShowLetterBoxView()
    {
        if (!showLetterBoxDuringEvent)
        {
            return;
        }

        GameObject view = ResolveLetterBoxView();
        if (view == null)
        {
            return;
        }

        if (!hasCachedLetterBoxViewState)
        {
            cachedLetterBoxViewActiveSelf = view.activeSelf;
            CanvasGroup canvasGroup = EnsureLetterBoxCanvasGroup(view);
            cachedLetterBoxAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
            CacheLetterBoxBars(view);
            hasCachedLetterBoxViewState = true;
        }

        view.SetActive(true);
        CanvasGroup activeCanvasGroup = EnsureLetterBoxCanvasGroup(view);
        if (activeCanvasGroup == null)
        {
            return;
        }

        activeCanvasGroup.alpha = 0f;
        SetLetterBoxSlidePosition(0f);
        StartLetterBoxTransition(activeCanvasGroup, 0f, 1f, 0f, 1f, null);
    }

    private void RestoreLetterBoxViewVisibility()
    {
        if (!hasCachedLetterBoxViewState)
        {
            return;
        }

        GameObject view = ResolveLetterBoxView();
        bool restoreActiveSelf = cachedLetterBoxViewActiveSelf;
        float restoreAlpha = cachedLetterBoxAlpha;

        if (view != null)
        {
            CanvasGroup canvasGroup = EnsureLetterBoxCanvasGroup(view);
            if (canvasGroup != null)
            {
                StartLetterBoxTransition(canvasGroup, canvasGroup.alpha, 0f, 1f, 0f, () =>
                {
                    if (view == null)
                    {
                        return;
                    }

                    RestoreLetterBoxBarPositions();
                    view.SetActive(restoreActiveSelf);
                    if (restoreActiveSelf && canvasGroup != null)
                    {
                        canvasGroup.alpha = restoreAlpha;
                    }
                });
            }
            else
            {
                view.SetActive(restoreActiveSelf);
            }
        }

        cachedLetterBoxViewActiveSelf = false;
        cachedLetterBoxAlpha = 1f;
        cachedLetterBoxTop = null;
        cachedLetterBoxBottom = null;
        cachedLetterBoxTopAnchoredPosition = Vector2.zero;
        cachedLetterBoxBottomAnchoredPosition = Vector2.zero;
        hasCachedLetterBoxViewState = false;
    }

    private CanvasGroup EnsureLetterBoxCanvasGroup(GameObject view)
    {
        if (view == null)
        {
            return null;
        }

        CanvasGroup canvasGroup = view.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = view.AddComponent<CanvasGroup>();
        }

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        return canvasGroup;
    }

    private void CacheLetterBoxBars(GameObject view)
    {
        cachedLetterBoxTop = FindLetterBoxBar(view.transform, "Top");
        cachedLetterBoxBottom = FindLetterBoxBar(view.transform, "Bottom");

        if (cachedLetterBoxTop != null)
        {
            cachedLetterBoxTopAnchoredPosition = cachedLetterBoxTop.anchoredPosition;
        }

        if (cachedLetterBoxBottom != null)
        {
            cachedLetterBoxBottomAnchoredPosition = cachedLetterBoxBottom.anchoredPosition;
        }
    }

    private static RectTransform FindLetterBoxBar(Transform root, string barName)
    {
        if (root == null)
        {
            return null;
        }

        Transform child = root.Find(barName);
        if (child != null)
        {
            return child as RectTransform;
        }

        RectTransform[] rectTransforms = root.GetComponentsInChildren<RectTransform>(includeInactive: true);
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform candidate = rectTransforms[i];
            if (candidate != null && candidate.name == barName)
            {
                return candidate;
            }
        }

        return null;
    }

    private void SetLetterBoxSlidePosition(float normalizedVisible)
    {
        float hiddenOffset = Mathf.Max(0f, letterBoxSlidePixels);

        if (cachedLetterBoxTop != null)
        {
            float topOffset = Mathf.Max(hiddenOffset, cachedLetterBoxTop.rect.height);
            cachedLetterBoxTop.anchoredPosition =
                Vector2.Lerp(
                    cachedLetterBoxTopAnchoredPosition + new Vector2(0f, topOffset),
                    cachedLetterBoxTopAnchoredPosition,
                    normalizedVisible);
        }

        if (cachedLetterBoxBottom != null)
        {
            float bottomOffset = Mathf.Max(hiddenOffset, cachedLetterBoxBottom.rect.height);
            cachedLetterBoxBottom.anchoredPosition =
                Vector2.Lerp(
                    cachedLetterBoxBottomAnchoredPosition - new Vector2(0f, bottomOffset),
                    cachedLetterBoxBottomAnchoredPosition,
                    normalizedVisible);
        }
    }

    private void RestoreLetterBoxBarPositions()
    {
        if (cachedLetterBoxTop != null)
        {
            cachedLetterBoxTop.anchoredPosition = cachedLetterBoxTopAnchoredPosition;
        }

        if (cachedLetterBoxBottom != null)
        {
            cachedLetterBoxBottom.anchoredPosition = cachedLetterBoxBottomAnchoredPosition;
        }
    }

    private void StartLetterBoxTransition(
        CanvasGroup canvasGroup,
        float fromAlpha,
        float toAlpha,
        float fromSlide,
        float toSlide,
        Action onComplete)
    {
        if (letterBoxFadeRoutine != null)
        {
            StopCoroutine(letterBoxFadeRoutine);
            letterBoxFadeRoutine = null;
        }

        float duration = Mathf.Max(0f, letterBoxFadeSeconds);
        if (duration <= 0f)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = toAlpha;
            }

            SetLetterBoxSlidePosition(toSlide);
            onComplete?.Invoke();
            return;
        }

        letterBoxFadeRoutine = StartCoroutine(AnimateLetterBox(
            canvasGroup,
            fromAlpha,
            toAlpha,
            fromSlide,
            toSlide,
            duration,
            onComplete));
    }

    private IEnumerator AnimateLetterBox(
        CanvasGroup canvasGroup,
        float fromAlpha,
        float toAlpha,
        float fromSlide,
        float toSlide,
        float duration,
        Action onComplete)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = t * t * (3f - 2f * t);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, easedT);
            }

            SetLetterBoxSlidePosition(Mathf.Lerp(fromSlide, toSlide, easedT));
            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = toAlpha;
        }

        SetLetterBoxSlidePosition(toSlide);
        letterBoxFadeRoutine = null;
        onComplete?.Invoke();
    }

    private GameObject ResolveLetterBoxView()
    {
        if (letterBoxView != null)
        {
            return letterBoxView;
        }

        if (string.IsNullOrWhiteSpace(letterBoxViewName))
        {
            return null;
        }

        string targetName = letterBoxViewName.Trim();
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == targetName)
            {
                letterBoxView = candidate.gameObject;
                return letterBoxView;
            }
        }

        return null;
    }

    private void ElevateEventCameraPriority()
    {
        RestoreEventCameraPriority();

        if (!useEventCameraDuringEvent)
        {
            return;
        }

        CinemachineCamera sourceCamera = ResolveEventCamera();
        if (sourceCamera == null)
        {
            return;
        }

        activeEventCamera = CreateRuntimeEventCamera(sourceCamera);
        if (activeEventCamera == null)
        {
            return;
        }

        cachedEventCameraPriorityValue = activeEventCamera.Priority.Value;
        cachedEventCameraPriorityEnabled = activeEventCamera.Priority.Enabled;
        hasCachedEventCameraPriority = true;

        int maxPriority = int.MinValue;
        CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null)
            {
                maxPriority = Mathf.Max(maxPriority, cameras[i].Priority.Value);
            }
        }

        int topPriority = maxPriority == int.MinValue ? eventCameraPriorityFloor : maxPriority + 10;
        int desiredPriority = Mathf.Max(eventCameraPriorityFloor, topPriority);
        activeEventCamera.Priority.Value = desiredPriority;
        activeEventCamera.Priority.Enabled = true;
    }

    private void RestoreEventCameraPriority()
    {
        bool activeCameraIsRuntime = runtimeEventCamera != null && activeEventCamera == runtimeEventCamera;

        if (!hasCachedEventCameraPriority)
        {
            DestroyRuntimeEventCamera();
            activeEventCamera = null;
            return;
        }

        if (activeEventCamera != null && !activeCameraIsRuntime)
        {
            activeEventCamera.Priority.Value = cachedEventCameraPriorityValue;
            activeEventCamera.Priority.Enabled = cachedEventCameraPriorityEnabled;
        }

        DestroyRuntimeEventCamera();
        activeEventCamera = null;
        cachedEventCameraPriorityValue = 0;
        cachedEventCameraPriorityEnabled = false;
        hasCachedEventCameraPriority = false;
    }

    private CinemachineCamera CreateRuntimeEventCamera(CinemachineCamera sourceCamera)
    {
        if (sourceCamera == null)
        {
            return null;
        }

        if (!Application.isPlaying)
        {
            return sourceCamera;
        }

        if (runtimeEventCamera != null)
        {
            return runtimeEventCamera;
        }

        GameObject runtimeCameraObject = Instantiate(sourceCamera.gameObject, sourceCamera.transform.parent);
        runtimeCameraObject.name = $"{sourceCamera.name}_Runtime";
        runtimeCameraObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        runtimeEventCamera = runtimeCameraObject.GetComponent<CinemachineCamera>();
        if (runtimeEventCamera == null)
        {
            Destroy(runtimeCameraObject);
            return null;
        }

        return runtimeEventCamera;
    }

    private void DestroyRuntimeEventCamera()
    {
        if (runtimeEventCamera == null)
        {
            return;
        }

        GameObject runtimeCameraObject = runtimeEventCamera.gameObject;
        runtimeEventCamera = null;

        if (Application.isPlaying)
        {
            Destroy(runtimeCameraObject);
        }
        else
        {
            DestroyImmediate(runtimeCameraObject);
        }
    }

    private CinemachineCamera ResolveEventCamera()
    {
        if (eventCamera != null)
        {
            return eventCamera;
        }

        if (string.IsNullOrWhiteSpace(eventCameraName))
        {
            return null;
        }

        string targetName = eventCameraName.Trim();
        CinemachineCamera[] cameras =
            FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            CinemachineCamera camera = cameras[i];
            if (camera == null)
            {
                continue;
            }

            if (string.Equals(camera.name, targetName, StringComparison.OrdinalIgnoreCase) ||
                camera.name.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                eventCamera = camera;
                return eventCamera;
            }
        }

        return null;
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

    private void DisableDirectorPlayOnAwakeIfNeeded()
    {
        if (!disableDirectorPlayOnAwake || director == null)
        {
            return;
        }

        director.playOnAwake = false;
    }

    private void ResolveDialogueManagerIfNeeded()
    {
        if (dialogueManager == null)
        {
            dialogueManager = FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include);
        }
    }

    private EventPanelPresenter ResolvePanelPresenter(
        string panelPresenterNameOverride = null,
        EventPanelPresenter panelPresenterOverride = null)
    {
        if (panelPresenterOverride != null)
        {
            return panelPresenterOverride;
        }

        if (!string.IsNullOrWhiteSpace(panelPresenterNameOverride))
        {
            EventPanelPresenter namedPresenter = FindPanelPresenterByName(panelPresenterNameOverride.Trim());
            if (namedPresenter != null)
            {
                return namedPresenter;
            }

            Debug.LogError(
                $"[StoryEventController] EventPanelPresenter '{panelPresenterNameOverride.Trim()}' not found. eventId='{EventId}'",
                this);
#if UNITY_EDITOR
            Debug.Break();
#endif
            return null;
        }

        if (panelPresenter != null)
        {
            return panelPresenter;
        }

        if (!string.IsNullOrWhiteSpace(panelPresenterName))
        {
            panelPresenter = FindPanelPresenterByName(panelPresenterName.Trim());
            if (panelPresenter != null)
            {
                return panelPresenter;
            }
        }

        panelPresenter = FindFirstObjectByType<EventPanelPresenter>(FindObjectsInactive.Include);
        if (panelPresenter == null)
        {
            Debug.LogError($"[StoryEventController] EventPanelPresenter not found. eventId='{EventId}'", this);
#if UNITY_EDITOR
            Debug.Break();
#endif
        }

        return panelPresenter;
    }

    private static EventPanelPresenter FindPanelPresenterByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        EventPanelPresenter[] presenters =
            FindObjectsByType<EventPanelPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < presenters.Length; i++)
        {
            EventPanelPresenter candidate = presenters[i];
            if (candidate != null && string.Equals(candidate.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
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

    private sealed class CinematicStateSnapshot
    {
        private readonly List<TransformState> transformStates = new List<TransformState>();
        private readonly List<SpriteRendererState> spriteRendererStates = new List<SpriteRendererState>();
        private readonly List<Rigidbody2DState> rigidbodyStates = new List<Rigidbody2DState>();
        private readonly PlayerControllerState playerControllerState;
        private readonly PlayerInputState playerInputState;
        private readonly PlayerControlBehaviourState playerControlBehaviourState;

        private CinematicStateSnapshot(
            PlayerControllerState playerControllerState,
            PlayerInputState playerInputState,
            PlayerControlBehaviourState playerControlBehaviourState)
        {
            this.playerControllerState = playerControllerState;
            this.playerInputState = playerInputState;
            this.playerControlBehaviourState = playerControlBehaviourState;
        }

        public static CinematicStateSnapshot Capture(
            StoryEventController owner,
            bool captureTransforms,
            bool captureSpriteFacing,
            bool captureRigidbodyVelocity)
        {
            GameObject playerObject = ResolvePlayerObject();
            PlayerController playerController = playerObject != null
                ? playerObject.GetComponent<PlayerController>()
                : FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            PlayerInput playerInput = playerObject != null
                ? playerObject.GetComponent<PlayerInput>()
                : FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Include);

            var snapshot = new CinematicStateSnapshot(
                PlayerControllerState.Capture(playerController),
                PlayerInputState.Capture(playerInput),
                PlayerControlBehaviourState.Capture(playerObject));

            var transforms = new HashSet<Transform>();
            owner.CollectEventActorTransforms(transforms);

            if (playerObject != null)
            {
                transforms.Add(playerObject.transform);
            }

            foreach (Transform target in transforms)
            {
                if (target == null)
                {
                    continue;
                }

                if (captureTransforms)
                {
                    snapshot.transformStates.Add(TransformState.Capture(target));
                }

                if (captureSpriteFacing)
                {
                    SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        if (renderers[i] != null)
                        {
                            snapshot.spriteRendererStates.Add(SpriteRendererState.Capture(renderers[i]));
                        }
                    }
                }

                if (captureRigidbodyVelocity)
                {
                    Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        snapshot.rigidbodyStates.Add(Rigidbody2DState.Capture(rb));
                    }
                }
            }

            return snapshot;
        }

        public void ApplyCinematicLocks(bool lockPlayerControl, bool lockPlayerFacing)
        {
            playerControllerState?.ApplyCinematicLocks(lockPlayerControl, lockPlayerFacing);
            if (lockPlayerControl)
            {
                playerInputState?.ApplyLock();
                playerControlBehaviourState?.ApplyLock();
            }
        }

        public void Restore()
        {
            for (int i = 0; i < transformStates.Count; i++)
            {
                transformStates[i].Restore();
            }

            for (int i = 0; i < spriteRendererStates.Count; i++)
            {
                spriteRendererStates[i].Restore();
            }

            for (int i = 0; i < rigidbodyStates.Count; i++)
            {
                rigidbodyStates[i].Restore();
            }

            playerControllerState?.Restore();
            playerControlBehaviourState?.Restore();
            playerInputState?.Restore();
        }

        private static GameObject ResolvePlayerObject()
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                return taggedPlayer;
            }

            PlayerController playerController =
                FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            if (playerController != null)
            {
                return playerController.gameObject;
            }

            MonoBehaviour[] behaviours =
                FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null && IsPlayerControlBehaviourName(behaviour.GetType().Name))
                {
                    return behaviour.gameObject;
                }
            }

            return null;
        }
    }

    private void CollectEventActorTransforms(HashSet<Transform> results)
    {
        if (results == null)
        {
            return;
        }

        RebuildLookupCache();

        for (int i = 0; i < actorBindings.Count; i++)
        {
            ActorBinding binding = actorBindings[i];
            if (binding != null && binding.actorRoot != null)
            {
                results.Add(binding.actorRoot);
            }
        }

        foreach (StoryEventActor actor in actorByKey.Values)
        {
            if (actor != null && actor.Root != null)
            {
                results.Add(actor.Root);
            }
        }
    }

    private sealed class TransformState
    {
        private readonly Transform target;
        private readonly Vector3 localPosition;
        private readonly Quaternion localRotation;
        private readonly Vector3 localScale;

        private TransformState(Transform target)
        {
            this.target = target;
            localPosition = target.localPosition;
            localRotation = target.localRotation;
            localScale = target.localScale;
        }

        public static TransformState Capture(Transform target)
        {
            return new TransformState(target);
        }

        public void Restore()
        {
            if (target == null)
            {
                return;
            }

            target.localPosition = localPosition;
            target.localRotation = localRotation;
            target.localScale = localScale;
        }
    }

    private sealed class SpriteRendererState
    {
        private readonly SpriteRenderer target;
        private readonly bool flipX;
        private readonly bool flipY;

        private SpriteRendererState(SpriteRenderer target)
        {
            this.target = target;
            flipX = target.flipX;
            flipY = target.flipY;
        }

        public static SpriteRendererState Capture(SpriteRenderer target)
        {
            return new SpriteRendererState(target);
        }

        public void Restore()
        {
            if (target == null)
            {
                return;
            }

            target.flipX = flipX;
            target.flipY = flipY;
        }
    }

    private sealed class Rigidbody2DState
    {
        private readonly Rigidbody2D target;
        private readonly Vector2 linearVelocity;
        private readonly float angularVelocity;

        private Rigidbody2DState(Rigidbody2D target)
        {
            this.target = target;
            linearVelocity = target.linearVelocity;
            angularVelocity = target.angularVelocity;
        }

        public static Rigidbody2DState Capture(Rigidbody2D target)
        {
            return new Rigidbody2DState(target);
        }

        public void Restore()
        {
            if (target == null)
            {
                return;
            }

            target.linearVelocity = linearVelocity;
            target.angularVelocity = angularVelocity;
        }
    }

    private sealed class PlayerControllerState
    {
        private readonly PlayerController target;
        private readonly bool externalControlLocked;
        private readonly bool externalFacingLocked;
        private readonly bool facingRight;

        private PlayerControllerState(PlayerController target)
        {
            this.target = target;
            externalControlLocked = target.IsExternalControlLocked;
            externalFacingLocked = target.IsExternalFacingLocked;
            facingRight = target.IsFacingRight;
        }

        public static PlayerControllerState Capture(PlayerController target)
        {
            return target != null ? new PlayerControllerState(target) : null;
        }

        public void ApplyCinematicLocks(bool lockControl, bool lockFacing)
        {
            if (target == null)
            {
                return;
            }

            if (lockControl)
            {
                target.SetExternalControlLocked(true);
            }

            if (lockFacing)
            {
                target.SetExternalFacingLocked(true, target.IsFacingRight);
            }
        }

        public void Restore()
        {
            if (target == null)
            {
                return;
            }

            target.SetExternalFacingLocked(true, facingRight);
            target.SetExternalControlLocked(externalControlLocked);
            target.SetExternalFacingLocked(externalFacingLocked, facingRight);
        }
    }

    private sealed class PlayerInputState
    {
        private readonly PlayerInput target;
        private readonly bool enabled;

        private PlayerInputState(PlayerInput target)
        {
            this.target = target;
            enabled = target.enabled;
        }

        public static PlayerInputState Capture(PlayerInput target)
        {
            return target != null ? new PlayerInputState(target) : null;
        }

        public void ApplyLock()
        {
            if (target != null)
            {
                target.enabled = false;
            }
        }

        public void Restore()
        {
            if (target != null)
            {
                target.enabled = enabled;
            }
        }
    }

    private sealed class PlayerControlBehaviourState
    {
        private readonly List<BehaviourState> behaviourStates = new List<BehaviourState>();
        private readonly Rigidbody2D rigidbody2D;

        private PlayerControlBehaviourState(GameObject playerObject)
        {
            if (playerObject == null)
            {
                return;
            }

            MonoBehaviour[] behaviours = playerObject.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || !IsPlayerControlBehaviourName(behaviour.GetType().Name))
                {
                    continue;
                }

                behaviourStates.Add(new BehaviourState(behaviour));
            }

            rigidbody2D = playerObject.GetComponent<Rigidbody2D>();
        }

        public static PlayerControlBehaviourState Capture(GameObject playerObject)
        {
            return playerObject != null ? new PlayerControlBehaviourState(playerObject) : null;
        }

        public void ApplyLock()
        {
            for (int i = 0; i < behaviourStates.Count; i++)
            {
                behaviourStates[i].ApplyLock();
            }

            if (rigidbody2D != null)
            {
                rigidbody2D.linearVelocity = Vector2.zero;
                rigidbody2D.angularVelocity = 0f;
            }
        }

        public void Restore()
        {
            for (int i = 0; i < behaviourStates.Count; i++)
            {
                behaviourStates[i].Restore();
            }
        }
    }

    private sealed class BehaviourState
    {
        private readonly Behaviour target;
        private readonly bool enabled;

        public BehaviourState(Behaviour target)
        {
            this.target = target;
            enabled = target.enabled;
        }

        public void ApplyLock()
        {
            if (target != null)
            {
                target.enabled = false;
            }
        }

        public void Restore()
        {
            if (target != null)
            {
                target.enabled = enabled;
            }
        }
    }

    private static bool IsPlayerControlBehaviourName(string typeName)
    {
        for (int i = 0; i < PlayerControlBehaviourNames.Length; i++)
        {
            if (PlayerControlBehaviourNames[i] == typeName)
            {
                return true;
            }
        }

        return false;
    }
}
