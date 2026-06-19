using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Metroidvania.Managers;
using Metroidvania.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Yarn.Unity;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayableDirector))]
[AddComponentMenu("CaseStudy/Story/Story Event Controller")]
public sealed class StoryEventController : MonoBehaviour
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
        public string displayName = string.Empty;
        public Transform actorRoot;
        public Transform bubbleTarget;
        public Vector3 bubbleOffset;

        public bool MatchesActorKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) &&
                   !string.IsNullOrWhiteSpace(actorKey) &&
                   string.Equals(actorKey.Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public bool MatchesSpeakerName(string speakerName)
        {
            if (string.IsNullOrWhiteSpace(speakerName))
            {
                return false;
            }

            string speaker = speakerName.Trim();
            return MatchesActorKey(speaker) ||
                   (!string.IsNullOrWhiteSpace(displayName) &&
                    string.Equals(displayName.Trim(), speaker, StringComparison.OrdinalIgnoreCase));
        }

        public Transform ResolveBubbleTarget()
        {
            return bubbleTarget != null ? bubbleTarget : actorRoot;
        }
    }

    [Header("Identity")]
    [SerializeField] private string eventId = "story_event";
    [SerializeField, FormerlySerializedAs("sceneName"), InspectorName("メモ")]
    private string memoName = string.Empty;
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
    [SerializeField] private List<GameObject> deactivateObjectsOnComplete = new List<GameObject>();

    [Header("Cinematic State")]
    [SerializeField] private bool lockPlayerControlDuringEvent = true;
    [SerializeField] private bool lockPlayerFacingDuringEvent = true;
    [SerializeField] private bool restoreActorTransformsOnExit = true;
    [SerializeField] private bool restorePlayerTransformOnExit;
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
    [SerializeField] private bool restoreRoomCameraOnExit = true;
    [SerializeField, Min(0f)] private float eventCameraExitEaseSeconds = 0.35f;
    [SerializeField] private bool alignEventCameraToStartMarker = false;
    [SerializeField, Min(1)] private int eventCameraStartMarkerNo = 4;

    [Header("Bindings")]
    [SerializeField] private List<ActorBinding> actorBindings = new List<ActorBinding>();

    private readonly Dictionary<int, StoryEventMarker> markerByNo = new Dictionary<int, StoryEventMarker>();
    private readonly Dictionary<string, StoryEventActor> actorByKey =
        new Dictionary<string, StoryEventActor>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> firedDialogueClipKeys = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> firedPanelClipKeys = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> firedTimelinePointKeys = new HashSet<string>(StringComparer.Ordinal);

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
    private RoomCameraTrigger cachedRoomCameraBeforeEvent;
    private Canvas cachedHudCanvas;
    private GraphicRaycaster cachedHudRaycaster;
    private FlagCanvasGroupVisibility cachedHudVisibilityGate;
    private CanvasGroup cachedHudCanvasGroup;
    private bool cachedHudCanvasEnabled;
    private bool cachedHudRaycasterEnabled;
    private bool cachedHudVisibilityGateEnabled;
    private float cachedHudCanvasGroupAlpha = 1f;
    private bool cachedHudCanvasGroupInteractable;
    private bool cachedHudCanvasGroupBlocksRaycasts;
    private MinimapManager cachedMinimapManager;
    private MinimapView cachedMinimapView;
    private bool cachedMinimapManagerEnabled;
    private bool cachedMiniMapVisible;
    private bool cachedFullMapVisible;
    private bool hasCachedGameplayUiState;

    public string EventId => string.IsNullOrWhiteSpace(eventId) ? name : eventId.Trim();
    public string MemoName => string.IsNullOrWhiteSpace(memoName) ? string.Empty : memoName.Trim();
    public bool IsPlaying => playRoutine != null;
    public bool HasCompleted => completeMutationsApplied;
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
        RestoreGameplayUiVisibility();
        RestoreLetterBoxViewVisibility();
        RestoreEventCameraPriority();
        RestoreRoomCameraOnEventExit();
        RestoreCinematicState();
        directorStopped = false;
        startMutationsApplied = false;
        firedDialogueClipKeys.Clear();
        firedPanelClipKeys.Clear();
        firedTimelinePointKeys.Clear();
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

            if (binding.MatchesActorKey(key))
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

    private void ApplyObjectMoveMarker(PlayableDirector resolvedDirector, StoryObjectMoveMarker marker)
    {
        if (marker == null || !Application.isPlaying)
        {
            return;
        }

        Transform moveTarget = marker.ResolveTarget(resolvedDirector);
        if (moveTarget == null)
        {
            moveTarget = GetActorTransform(marker.ActorKey);
        }

        if (moveTarget == null)
        {
            Debug.LogWarning(
                $"[StoryEventController] Object Move Point target was not found. actorKey='{marker.ActorKey}'",
                this);
            return;
        }

        Vector3 destination = ResolveObjectMovePointDestination(marker, moveTarget.position);
        global::PlayerController playerController = ResolvePlayerController(moveTarget);
        ApplyInstantObjectMove(moveTarget, destination, playerController);
        ApplyObjectMoveUmbrellaWalkState(moveTarget, marker.UseUmbrellaWalk);
    }

    private Vector3 ResolveObjectMovePointDestination(StoryObjectMoveMarker marker, Vector3 fallback)
    {
        Vector3 resolved = marker.WorldPosition;
        if (marker.TargetMode == StoryObjectMoveTargetMode.Marker)
        {
            Transform destinationMarker = GetMarkerTransform(marker.MarkerNo);
            resolved = destinationMarker != null ? destinationMarker.position : fallback;
        }

        if (!marker.MoveX)
        {
            resolved.x = fallback.x;
        }

        if (!marker.MoveY)
        {
            resolved.y = fallback.y;
        }

        if (marker.KeepCurrentZ)
        {
            resolved.z = fallback.z;
        }

        return resolved;
    }

    private static void ApplyInstantObjectMove(
        Transform moveTarget,
        Vector3 destination,
        global::PlayerController playerController)
    {
        moveTarget.position = destination;

        Rigidbody2D rigidbody2D = playerController != null
            ? playerController.GetComponent<Rigidbody2D>()
            : moveTarget.GetComponent<Rigidbody2D>();
        if (rigidbody2D != null)
        {
            rigidbody2D.position = new Vector2(destination.x, destination.y);
            rigidbody2D.linearVelocity = Vector2.zero;
            if (playerController != null)
            {
                PlayerRigidbodyGroundState.SnapDownToGround(rigidbody2D);
            }
        }

        if (playerController != null)
        {
            playerController.ClearExternalMovementDirection();
        }
    }

    private static void ApplyObjectMoveUmbrellaWalkState(Transform moveTarget, bool useUmbrellaWalk)
    {
        if (!useUmbrellaWalk)
        {
            return;
        }

        UmbrellaController umbrellaController = ResolveUmbrellaController(moveTarget);
        if (umbrellaController == null)
        {
            return;
        }

        umbrellaController.SetUmbrellaState(UmbrellaController.UmbrellaState.Open, false);
    }

    private static global::PlayerController ResolvePlayerController(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        global::PlayerController controller = target.GetComponent<global::PlayerController>();
        return controller != null ? controller : target.GetComponentInParent<global::PlayerController>();
    }

    private static UmbrellaController ResolveUmbrellaController(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        UmbrellaController controller = target.GetComponentInChildren<UmbrellaController>(true);
        return controller != null ? controller : target.GetComponentInParent<UmbrellaController>(true);
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
        firedTimelinePointKeys.Clear();

        StoryPauseRuntime.SetOverride(pausePolicy);
        HideGameplayUiForEvent();
        CaptureCinematicState();
        ApplyCinematicState();
        CaptureRoomCameraBeforeEvent();
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
        resolvedDirector.RebuildGraph();
        resolvedDirector.Evaluate();
        resolvedDirector.Play();

        double lastPointProcessTime = -0.000001d;
        ProcessTimelinePoints(resolvedDirector, lastPointProcessTime, resolvedDirector.time);
        lastPointProcessTime = resolvedDirector.time;

        while (!directorStopped)
        {
            double currentTime = resolvedDirector.time;
            ProcessTimelinePoints(resolvedDirector, lastPointProcessTime, currentTime);
            lastPointProcessTime = currentTime;

            if (!waitingDialogueCompletion && HasDirectorReachedTimelineEnd(resolvedDirector))
            {
                directorStopped = true;
                continue;
            }

            yield return null;
        }

        ProcessTimelinePoints(resolvedDirector, lastPointProcessTime, resolvedDirector.time);

        resolvedDirector.stopped -= OnDirectorStopped;
        ApplyCompletionState();
        StoryPauseRuntime.ClearOverride();
        yield return RestorePresentationOnEventExitRoutine();
        RestoreGameplayUiVisibility();
        RestoreCinematicState();

        playRoutine = null;
    }

    private void ProcessTimelinePoints(PlayableDirector resolvedDirector, double previousTime, double currentTime)
    {
        if (resolvedDirector == null || !(resolvedDirector.playableAsset is TimelineAsset timelineAsset))
        {
            return;
        }

        double minTime = Math.Min(previousTime, currentTime);
        double maxTime = Math.Max(previousTime, currentTime);
        const double epsilon = 0.000001d;

        foreach (TrackAsset track in EnumerateTracks(timelineAsset))
        {
            if (track == null || track.mutedInHierarchy)
            {
                continue;
            }

            foreach (IMarker marker in track.GetMarkers())
            {
                if (marker == null)
                {
                    continue;
                }

                double markerTime = marker.time;
                if (markerTime < minTime - epsilon || markerTime > maxTime + epsilon)
                {
                    continue;
                }

                string pointKey = BuildTimelinePointKey(track, marker);
                if (!firedTimelinePointKeys.Add(pointKey))
                {
                    continue;
                }

                PlayTimelinePoint(resolvedDirector, track, marker);
            }
        }
    }

    private static IEnumerable<TrackAsset> EnumerateTracks(TimelineAsset timelineAsset)
    {
        if (timelineAsset == null)
        {
            yield break;
        }

        foreach (TrackAsset rootTrack in timelineAsset.GetRootTracks())
        {
            foreach (TrackAsset track in EnumerateTrackAndChildren(rootTrack))
            {
                yield return track;
            }
        }
    }

    private static IEnumerable<TrackAsset> EnumerateTrackAndChildren(TrackAsset track)
    {
        if (track == null)
        {
            yield break;
        }

        yield return track;

        foreach (TrackAsset childTrack in track.GetChildTracks())
        {
            foreach (TrackAsset descendant in EnumerateTrackAndChildren(childTrack))
            {
                yield return descendant;
            }
        }
    }

    private static string BuildTimelinePointKey(TrackAsset track, IMarker marker)
    {
        string markerObjectId = marker is UnityEngine.Object markerObject
            ? markerObject.GetInstanceID().ToString(CultureInfo.InvariantCulture)
            : marker.GetHashCode().ToString(CultureInfo.InvariantCulture);

        return string.Join(
            "|",
            track != null ? track.GetInstanceID().ToString(CultureInfo.InvariantCulture) : "track",
            marker.GetType().FullName,
            markerObjectId,
            marker.time.ToString("0.######", CultureInfo.InvariantCulture));
    }

    private void PlayTimelinePoint(PlayableDirector resolvedDirector, TrackAsset track, IMarker marker)
    {
        if (marker is StoryYarnDialogueMarker dialogueMarker)
        {
            if (!(track is StoryYarnDialogueTrack))
            {
                return;
            }

            TryStartDialogueFromTimeline(
                dialogueMarker.TriggerKey,
                dialogueMarker.NodeName,
                dialogueMarker.UseControllerDefaultStyle,
                dialogueMarker.DialogueStyle,
                dialogueMarker.PauseTimelineUntilComplete,
                dialogueMarker.BubbleActorKey);
            return;
        }

        if (marker is StoryAudioMarker audioMarker)
        {
            if (!(track is StoryAudioTrack))
            {
                return;
            }

            PlayAudioMarker(audioMarker);
            return;
        }

        if (marker is StoryCameraShakeMarker shakeMarker)
        {
            if (!(track is StoryCameraShakeTrack))
            {
                return;
            }

            PlayCameraShakeMarker(shakeMarker);
            return;
        }

        if (marker is EventPanelMarker panelMarker)
        {
            if (!(track is EventPanelTrack))
            {
                return;
            }

            EventPanelPresenter panelPresenterOverride = panelMarker.ResolvePanelPresenter(resolvedDirector);
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

        if (marker is StoryObjectMoveMarker objectMoveMarker)
        {
            if (!(track is StoryObjectMoveTrack))
            {
                return;
            }

            ApplyObjectMoveMarker(resolvedDirector, objectMoveMarker);
            return;
        }

        if (marker is StoryAutoSaveMarker autoSaveMarker)
        {
            if (!(track is StoryEventTrack))
            {
                return;
            }

            if (autoSaveMarker.ApplyCompleteMutationsBeforeSave)
            {
                ApplyCompleteMutations();
            }

            SaveManager.TrySaveCurrentGame();
        }
    }

    private bool CanRun()
    {
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
            PauseDirectorAfterCurrentEvaluation(resolvedDirector);
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
                ApplyDiaryCollectionOnPanelClose(content);
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

    private static void ApplyDiaryCollectionOnPanelClose(EventPanelContent content)
    {
        if (content == null || !content.collectDiaryOnClose ||
            string.IsNullOrWhiteSpace(content.diaryProgressFlagKey))
        {
            return;
        }

        GameProgressFlags.Set(content.diaryProgressFlagKey.Trim(), true);
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
            PauseDirectorAfterCurrentEvaluation(resolvedDirector);
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

    private static void PauseDirectorAfterCurrentEvaluation(PlayableDirector resolvedDirector)
    {
        if (resolvedDirector == null)
        {
            return;
        }

        resolvedDirector.Evaluate();
        resolvedDirector.Pause();
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
        BubbleDialogueView.SpeakerTargetResolver speakerTargetResolver =
            style == DialogueStyle.Bubble ? TryResolveBubbleTargetForSpeaker : null;
        dialogueManager.StartConversation(
            nodeName,
            style,
            bubbleTarget,
            speakerTargetResolver,
            speakerTargetResolver != null);

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

                if (binding.MatchesActorKey(key))
                {
                    Transform bindingTarget = binding.ResolveBubbleTarget();
                    if (bindingTarget != null)
                    {
                        return bindingTarget;
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

    private bool TryResolveBubbleTargetForSpeaker(string characterName, out Transform target, out Vector3 targetOffset)
    {
        target = null;
        targetOffset = Vector3.zero;

        if (string.IsNullOrWhiteSpace(characterName))
        {
            return false;
        }

        string speakerName = characterName.Trim();
        if (TryResolveBubbleTargetBySpeakerName(speakerName, out target, out targetOffset))
        {
            return true;
        }

        string alias = ResolveSpeakerActorAlias(speakerName);
        return !string.Equals(alias, speakerName, StringComparison.OrdinalIgnoreCase) &&
               TryResolveBubbleTargetBySpeakerName(alias, out target, out targetOffset);
    }

    private bool TryResolveBubbleTargetBySpeakerName(string speakerName, out Transform target, out Vector3 targetOffset)
    {
        target = null;
        targetOffset = Vector3.zero;

        if (string.IsNullOrWhiteSpace(speakerName))
        {
            return false;
        }

        string speaker = speakerName.Trim();
        for (int i = 0; i < actorBindings.Count; i++)
        {
            ActorBinding binding = actorBindings[i];
            if (binding == null || !binding.MatchesSpeakerName(speaker))
            {
                continue;
            }

            target = binding.ResolveBubbleTarget();
            if (target == null)
            {
                continue;
            }

            targetOffset = binding.bubbleOffset;
            return true;
        }

        RebuildLookupCache();
        foreach (StoryEventActor actor in actorByKey.Values)
        {
            if (actor == null || !actor.MatchesSpeakerName(speaker))
            {
                continue;
            }

            target = actor.BubbleTarget;
            if (target == null)
            {
                continue;
            }

            targetOffset = actor.BubbleOffset;
            return true;
        }

        Transform actorTransform = GetActorTransform(speaker);
        if (actorTransform == null)
        {
            return false;
        }

        target = actorTransform;
        return true;
    }

    private static string ResolveSpeakerActorAlias(string speakerName)
    {
        if (string.IsNullOrWhiteSpace(speakerName))
        {
            return string.Empty;
        }

        string speaker = speakerName.Trim();
        if (string.Equals(speaker, "\u30A4\u30EA\u30B9", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(speaker, "player", StringComparison.OrdinalIgnoreCase))
        {
            return "iris";
        }

        if (string.Equals(speaker, "\u30CE\u30AF\u30B9", StringComparison.OrdinalIgnoreCase))
        {
            return "nox";
        }

        return speaker;
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
        DeactivateObjectsOnComplete();

        if (markRunOnceFlagOnComplete && !string.IsNullOrWhiteSpace(runOnceFlagKey))
        {
            GameProgressFlags.Set(runOnceFlagKey.Trim(), true);
        }

        if (autoSaveOnComplete)
        {
            SaveManager.TrySaveCurrentGame();
        }
    }

    private void DeactivateObjectsOnComplete()
    {
        if (deactivateObjectsOnComplete == null)
        {
            return;
        }

        for (int i = 0; i < deactivateObjectsOnComplete.Count; i++)
        {
            GameObject target = deactivateObjectsOnComplete[i];
            if (target != null)
            {
                target.SetActive(false);
            }
        }
    }

    private static bool HasDirectorReachedTimelineEnd(PlayableDirector targetDirector)
    {
        if (targetDirector == null || targetDirector.extrapolationMode == DirectorWrapMode.Loop)
        {
            return false;
        }

        double duration = targetDirector.duration;
        if (double.IsInfinity(duration) || double.IsNaN(duration) || duration <= 0.000001d)
        {
            return false;
        }

        return targetDirector.time >= duration - 0.0001d;
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
            restorePlayerTransformOnExit,
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
        LetterBoxExitState exitState = BeginLetterBoxExit();
        if (exitState == null)
        {
            return;
        }

        StartLetterBoxTransition(exitState.CanvasGroup, 1f, 1f, 1f, 0f, () =>
        {
            CompleteLetterBoxExit(exitState);
        });
    }

    private LetterBoxExitState BeginLetterBoxExit()
    {
        if (!hasCachedLetterBoxViewState)
        {
            return null;
        }

        GameObject view = ResolveLetterBoxView();
        bool restoreActiveSelf = cachedLetterBoxViewActiveSelf;
        float restoreAlpha = cachedLetterBoxAlpha;

        if (letterBoxFadeRoutine != null)
        {
            StopCoroutine(letterBoxFadeRoutine);
            letterBoxFadeRoutine = null;
        }

        if (view == null)
        {
            ClearLetterBoxCache();
            return null;
        }

        CanvasGroup canvasGroup = EnsureLetterBoxCanvasGroup(view);
        if (canvasGroup == null)
        {
            view.SetActive(restoreActiveSelf);
            ClearLetterBoxCache();
            return null;
        }

        canvasGroup.alpha = 1f;
        SetLetterBoxSlidePosition(1f);
        return new LetterBoxExitState(view, canvasGroup, restoreActiveSelf, restoreAlpha);
    }

    private void CompleteLetterBoxExit(LetterBoxExitState exitState)
    {
        if (exitState == null)
        {
            return;
        }

        if (exitState.View != null)
        {
            RestoreLetterBoxBarPositions();
            exitState.View.SetActive(exitState.RestoreActiveSelf);
            if (exitState.RestoreActiveSelf && exitState.CanvasGroup != null)
            {
                exitState.CanvasGroup.alpha = exitState.RestoreAlpha;
            }
        }

        ClearLetterBoxCache();
    }

    private void ClearLetterBoxCache()
    {
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

        AlignEventCameraToCurrentView(activeEventCamera);
        AlignEventCameraToStartMarker(activeEventCamera);

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

    private static void AlignEventCameraToCurrentView(CinemachineCamera targetCamera)
    {
        if (targetCamera == null)
        {
            return;
        }

        CinemachineCamera currentCamera = FindHighestPriorityCameraExcept(targetCamera);
        if (currentCamera == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            float orthographicSize = mainCamera.orthographic
                ? mainCamera.orthographicSize
                : targetCamera.Lens.OrthographicSize;
            ApplyCameraPose(targetCamera, mainCamera.transform.position, orthographicSize);
            return;
        }

        ApplyCameraPose(targetCamera, currentCamera.transform.position, currentCamera.Lens.OrthographicSize);
    }

    private void AlignEventCameraToStartMarker(CinemachineCamera targetCamera)
    {
        if (!alignEventCameraToStartMarker || targetCamera == null)
        {
            return;
        }

        Transform marker = GetMarkerTransform(eventCameraStartMarkerNo);
        if (marker == null)
        {
            Debug.LogWarning(
                $"[StoryEventController] Event camera start marker was not found. eventId='{EventId}', markerNo={eventCameraStartMarkerNo}",
                this);
            return;
        }

        Vector3 targetPosition = marker.position;
        targetPosition.z = targetCamera.transform.position.z;
        ApplyCameraPose(targetCamera, targetPosition, targetCamera.Lens.OrthographicSize);
    }

    private static CinemachineCamera FindHighestPriorityCameraExcept(CinemachineCamera excludedCamera)
    {
        CinemachineCamera bestCamera = null;
        int bestPriority = int.MinValue;
        CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < cameras.Length; i++)
        {
            CinemachineCamera camera = cameras[i];
            if (camera == null || camera == excludedCamera || IsRuntimeEventCamera(camera))
            {
                continue;
            }

            int priority = camera.Priority.Value;
            if (bestCamera == null || priority > bestPriority)
            {
                bestCamera = camera;
                bestPriority = priority;
            }
        }

        return bestCamera;
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

    private void CaptureRoomCameraBeforeEvent()
    {
        cachedRoomCameraBeforeEvent = restoreRoomCameraOnExit ? RoomCameraTrigger.ActiveRoom : null;
    }

    private void RestoreRoomCameraOnEventExit()
    {
        if (!restoreRoomCameraOnExit)
        {
            cachedRoomCameraBeforeEvent = null;
            return;
        }

        if (cachedRoomCameraBeforeEvent != null && cachedRoomCameraBeforeEvent.isActiveAndEnabled)
        {
            cachedRoomCameraBeforeEvent.ActivateCamera();
            cachedRoomCameraBeforeEvent = null;
            return;
        }

        cachedRoomCameraBeforeEvent = null;

        GameObject playerObject = ResolvePlayerObjectForCameraRestore();
        if (playerObject == null)
        {
            return;
        }

        RoomCameraTrigger.TryActivateRoomAtPosition(playerObject.transform.position, out _);
    }

    private IEnumerator RestorePresentationOnEventExitRoutine()
    {
        LetterBoxExitState letterBoxExit = BeginLetterBoxExit();
        CinemachineCamera handoffCamera = activeEventCamera;
        bool shouldRestoreRoomCamera = restoreRoomCameraOnExit;
        bool hasHandoffCamera = handoffCamera != null;
        bool hasCameraTarget = false;
        bool targetUsesDefaultCamera = false;
        Vector3 cameraStartPosition = Vector3.zero;
        Vector3 cameraTargetPosition = Vector3.zero;
        float cameraStartOrthographicSize = 0f;
        float cameraTargetOrthographicSize = 0f;

        if (!shouldRestoreRoomCamera)
        {
            yield return AnimatePresentationExit(letterBoxExit, false, handoffCamera, cameraStartPosition, cameraTargetPosition, cameraStartOrthographicSize, cameraTargetOrthographicSize);
            RestoreEventCameraPriority();
            yield break;
        }

        if (hasHandoffCamera &&
            TryResolveEventExitCameraTarget(
                handoffCamera,
                out cameraTargetPosition,
                out cameraTargetOrthographicSize,
                out targetUsesDefaultCamera))
        {
            hasCameraTarget = true;
            cameraStartPosition = handoffCamera.transform.position;
            cameraStartOrthographicSize = handoffCamera.Lens.OrthographicSize;
        }

        bool shouldEaseCamera = hasCameraTarget && !targetUsesDefaultCamera && hasHandoffCamera;
        if (hasCameraTarget && targetUsesDefaultCamera)
        {
            CameraManager.Instance?.TrySetFollowCameraPose(cameraTargetPosition, cameraTargetOrthographicSize);
        }

        yield return AnimatePresentationExit(
            letterBoxExit,
            shouldEaseCamera,
            handoffCamera,
            cameraStartPosition,
            cameraTargetPosition,
            cameraStartOrthographicSize,
            cameraTargetOrthographicSize);

        RestoreEventCameraPriority();
        RestoreRoomCameraOnEventExit();
    }

    private IEnumerator AnimatePresentationExit(
        LetterBoxExitState letterBoxExit,
        bool easeCamera,
        CinemachineCamera camera,
        Vector3 cameraStartPosition,
        Vector3 cameraTargetPosition,
        float cameraStartOrthographicSize,
        float cameraTargetOrthographicSize)
    {
        bool animateLetterBox = letterBoxExit != null;
        float duration = animateLetterBox
            ? Mathf.Max(0f, letterBoxFadeSeconds)
            : Mathf.Max(0f, eventCameraExitEaseSeconds);

        cameraTargetOrthographicSize = Mathf.Max(0.01f, cameraTargetOrthographicSize);
        if (easeCamera && camera != null)
        {
            bool alreadyAtTarget =
                (cameraStartPosition - cameraTargetPosition).sqrMagnitude <= 0.0001f &&
                Mathf.Abs(cameraStartOrthographicSize - cameraTargetOrthographicSize) <= 0.0001f;
            easeCamera = !alreadyAtTarget;
        }

        if (duration <= 0f || (!animateLetterBox && !easeCamera))
        {
            if (easeCamera && camera != null)
            {
                ApplyCameraPose(camera, cameraTargetPosition, cameraTargetOrthographicSize);
            }

            if (animateLetterBox)
            {
                SetLetterBoxSlidePosition(0f);
                CompleteLetterBoxExit(letterBoxExit);
            }

            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = t * t * (3f - 2f * t);

            if (animateLetterBox)
            {
                SetLetterBoxSlidePosition(Mathf.Lerp(1f, 0f, easedT));
            }

            if (easeCamera && camera != null)
            {
                ApplyCameraPose(
                    camera,
                    Vector3.Lerp(cameraStartPosition, cameraTargetPosition, easedT),
                    Mathf.Lerp(cameraStartOrthographicSize, cameraTargetOrthographicSize, easedT));
            }

            yield return null;
        }

        if (animateLetterBox)
        {
            SetLetterBoxSlidePosition(0f);
            CompleteLetterBoxExit(letterBoxExit);
        }

        if (easeCamera && camera != null)
        {
            ApplyCameraPose(camera, cameraTargetPosition, cameraTargetOrthographicSize);
        }
    }

    private bool TryResolveEventExitCameraTarget(
        CinemachineCamera handoffCamera,
        out Vector3 position,
        out float orthographicSize,
        out bool targetUsesDefaultCamera)
    {
        position = handoffCamera != null ? handoffCamera.transform.position : Vector3.zero;
        orthographicSize = handoffCamera != null ? handoffCamera.Lens.OrthographicSize : 0f;
        targetUsesDefaultCamera = false;

        RoomCameraTrigger targetRoom = ResolveEventExitRoom();
        if (targetRoom == null || !targetRoom.TryGetCameraPose(out Vector3 roomPosition, out float roomOrthographicSize))
        {
            return handoffCamera != null;
        }

        targetUsesDefaultCamera = targetRoom.UsesDefaultCameraWhenEntered;
        if (targetUsesDefaultCamera)
        {
            return handoffCamera != null;
        }

        position = roomPosition;
        orthographicSize = roomOrthographicSize;
        return true;
    }

    private RoomCameraTrigger ResolveEventExitRoom()
    {
        if (cachedRoomCameraBeforeEvent != null && cachedRoomCameraBeforeEvent.isActiveAndEnabled)
        {
            return cachedRoomCameraBeforeEvent;
        }

        GameObject playerObject = ResolvePlayerObjectForCameraRestore();
        if (playerObject == null)
        {
            return null;
        }

        return RoomCameraTrigger.TryGetRoomAtPosition(playerObject.transform.position, out RoomCameraTrigger room)
            ? room
            : null;
    }

    private static void ApplyCameraPose(CinemachineCamera camera, Vector3 position, float orthographicSize)
    {
        if (camera == null)
        {
            return;
        }

        camera.transform.position = position;
        LensSettings lens = camera.Lens;
        lens.OrthographicSize = Mathf.Max(0.01f, orthographicSize);
        camera.Lens = lens;
    }

    private static GameObject ResolvePlayerObjectForCameraRestore()
    {
        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            return taggedPlayer;
        }

        global::PlayerController playerController =
            FindFirstObjectByType<global::PlayerController>(FindObjectsInactive.Include);
        return playerController != null ? playerController.gameObject : null;
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
        runtimeCameraObject.SetActive(true);
        runtimeEventCamera = runtimeCameraObject.GetComponent<CinemachineCamera>();
        if (runtimeEventCamera == null)
        {
            Destroy(runtimeCameraObject);
            return null;
        }

        runtimeEventCamera.enabled = true;
        CameraTarget runtimeTarget = runtimeEventCamera.Target;
        runtimeTarget.TrackingTarget = null;
        runtimeTarget.LookAtTarget = null;
        runtimeEventCamera.Target = runtimeTarget;

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
        CinemachineCamera fallbackCamera = null;
        for (int i = 0; i < cameras.Length; i++)
        {
            CinemachineCamera camera = cameras[i];
            if (camera == null || IsRuntimeEventCamera(camera))
            {
                continue;
            }

            if (string.Equals(camera.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                eventCamera = camera;
                return eventCamera;
            }

            if (fallbackCamera == null &&
                camera.name.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                fallbackCamera = camera;
            }
        }

        if (fallbackCamera != null)
        {
            eventCamera = fallbackCamera;
            return eventCamera;
        }

        return null;
    }

    private static bool IsRuntimeEventCamera(CinemachineCamera camera)
    {
        return camera != null &&
               (camera.name.EndsWith("_Runtime", StringComparison.OrdinalIgnoreCase) ||
                (camera.gameObject.hideFlags & (HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild)) != 0);
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

    private void HideGameplayUiForEvent()
    {
        RestoreGameplayUiVisibility();

        GameObject hudObject = GameObject.Find("PlayerHUDCanvas");
        if (hudObject != null)
        {
            cachedHudCanvas = hudObject.GetComponent<Canvas>();
            cachedHudRaycaster = hudObject.GetComponent<GraphicRaycaster>();
            cachedHudVisibilityGate = hudObject.GetComponent<FlagCanvasGroupVisibility>();
            cachedHudCanvasGroup = EnsureCanvasGroup(hudObject);

            if (cachedHudVisibilityGate != null)
            {
                cachedHudVisibilityGateEnabled = cachedHudVisibilityGate.enabled;
                cachedHudVisibilityGate.enabled = false;
                hasCachedGameplayUiState = true;
            }

            if (cachedHudCanvas != null)
            {
                cachedHudCanvasEnabled = cachedHudCanvas.enabled;
                cachedHudCanvas.enabled = false;
                hasCachedGameplayUiState = true;
            }

            if (cachedHudRaycaster != null)
            {
                cachedHudRaycasterEnabled = cachedHudRaycaster.enabled;
                cachedHudRaycaster.enabled = false;
                hasCachedGameplayUiState = true;
            }

            if (cachedHudCanvasGroup != null)
            {
                cachedHudCanvasGroupAlpha = cachedHudCanvasGroup.alpha;
                cachedHudCanvasGroupInteractable = cachedHudCanvasGroup.interactable;
                cachedHudCanvasGroupBlocksRaycasts = cachedHudCanvasGroup.blocksRaycasts;
                cachedHudCanvasGroup.alpha = 0f;
                cachedHudCanvasGroup.interactable = false;
                cachedHudCanvasGroup.blocksRaycasts = false;
                hasCachedGameplayUiState = true;
            }
        }

        cachedMinimapManager = MinimapManager.Instance;
        if (cachedMinimapManager == null)
        {
            cachedMinimapManager = FindFirstObjectByType<MinimapManager>(FindObjectsInactive.Include);
        }

        if (cachedMinimapManager == null)
        {
            return;
        }

        cachedMinimapManagerEnabled = cachedMinimapManager.enabled;
        cachedMinimapView = cachedMinimapManager.GetComponent<MinimapView>();
        if (cachedMinimapView == null)
        {
            cachedMinimapView = FindFirstObjectByType<MinimapView>(FindObjectsInactive.Include);
        }

        if (cachedMinimapView != null)
        {
            cachedMiniMapVisible = cachedMinimapView.IsMiniMapVisible;
            cachedFullMapVisible = cachedMinimapView.IsFullMapVisible;
            cachedMinimapView.SetPanelVisibility(false, false);
        }

        cachedMinimapManager.enabled = false;
        hasCachedGameplayUiState = true;
    }

    private void RestoreGameplayUiVisibility()
    {
        if (!hasCachedGameplayUiState)
        {
            return;
        }

        if (cachedHudCanvas != null)
        {
            cachedHudCanvas.enabled = cachedHudCanvasEnabled;
        }

        if (cachedHudRaycaster != null)
        {
            cachedHudRaycaster.enabled = cachedHudRaycasterEnabled;
        }

        if (cachedHudVisibilityGate != null)
        {
            cachedHudVisibilityGate.enabled = cachedHudVisibilityGateEnabled;
            if (cachedHudVisibilityGate.enabled)
            {
                cachedHudVisibilityGate.EvaluateAndApply();
            }
        }

        if (cachedHudCanvasGroup != null)
        {
            cachedHudCanvasGroup.alpha = cachedHudCanvasGroupAlpha;
            cachedHudCanvasGroup.interactable = cachedHudCanvasGroupInteractable;
            cachedHudCanvasGroup.blocksRaycasts = cachedHudCanvasGroupBlocksRaycasts;
        }

        if (cachedMinimapView != null)
        {
            cachedMinimapView.SetPanelVisibility(cachedMiniMapVisible, cachedFullMapVisible);
        }

        if (cachedMinimapManager != null)
        {
            cachedMinimapManager.enabled = cachedMinimapManagerEnabled;
        }

        cachedHudCanvas = null;
        cachedHudRaycaster = null;
        cachedHudVisibilityGate = null;
        cachedHudCanvasGroup = null;
        cachedMinimapManager = null;
        cachedMinimapView = null;
        cachedHudCanvasEnabled = false;
        cachedHudRaycasterEnabled = false;
        cachedHudVisibilityGateEnabled = false;
        cachedHudCanvasGroupAlpha = 1f;
        cachedHudCanvasGroupInteractable = false;
        cachedHudCanvasGroupBlocksRaycasts = false;
        cachedMinimapManagerEnabled = false;
        cachedMiniMapVisible = false;
        cachedFullMapVisible = false;
        hasCachedGameplayUiState = false;
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject target)
    {
        if (target == null)
        {
            return null;
        }

        if (!target.TryGetComponent(out CanvasGroup canvasGroup))
        {
            canvasGroup = target.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
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
            bool capturePlayerTransform,
            bool captureSpriteFacing,
            bool captureRigidbodyVelocity)
        {
            GameObject playerObject = ResolvePlayerObject();
            Transform playerTransform = playerObject != null ? playerObject.transform : null;
            PlayerController playerController = playerObject != null
                ? playerObject.GetComponent<PlayerController>()
                : FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
            PlayerInput playerInput = playerObject != null
                ? playerObject.GetComponent<PlayerInput>()
                : FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Include);

            var snapshot = new CinematicStateSnapshot(
                PlayerControllerState.Capture(playerController),
                PlayerInputState.Capture(playerInput),
                PlayerControlBehaviourState.Capture(playerObject, captureRigidbodyVelocity));

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

                bool isPlayerTransform = playerTransform != null && target == playerTransform;
                if (captureTransforms && (!isPlayerTransform || capturePlayerTransform))
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
        private readonly PlayerRigidbodyGroundState rigidbodyGroundState;

        private PlayerControlBehaviourState(GameObject playerObject, bool restoreRigidbodyVelocityOnExit)
        {
            if (playerObject == null)
            {
                return;
            }

            MonoBehaviour[] behaviours = playerObject.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null ||
                    behaviour is PlayerController ||
                    !IsPlayerControlBehaviourName(behaviour.GetType().Name))
                {
                    continue;
                }

                behaviourStates.Add(new BehaviourState(behaviour));
            }

            rigidbodyGroundState = PlayerRigidbodyGroundState.Capture(
                playerObject.GetComponent<Rigidbody2D>(),
                restoreRigidbodyVelocityOnExit);
        }

        public static PlayerControlBehaviourState Capture(GameObject playerObject, bool restoreRigidbodyVelocityOnExit)
        {
            return playerObject != null
                ? new PlayerControlBehaviourState(playerObject, restoreRigidbodyVelocityOnExit)
                : null;
        }

        public void ApplyLock()
        {
            for (int i = 0; i < behaviourStates.Count; i++)
            {
                behaviourStates[i].ApplyLock();
            }

            rigidbodyGroundState?.ApplyLock();
        }

        public void Restore()
        {
            rigidbodyGroundState?.Restore();

            for (int i = 0; i < behaviourStates.Count; i++)
            {
                behaviourStates[i].Restore();
            }
        }
    }

    private sealed class PlayerRigidbodyGroundState
    {
        private const string GroundLayerName = "Ground";
        private const string FallThroughFloorLayerName = "FallThroughFloor";
        private const float GroundSnapMaxDistance = 12f;

        private readonly Rigidbody2D target;
        private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
        private readonly Vector2 linearVelocity;
        private readonly float angularVelocity;
        private readonly bool preserveVelocityOnRestore;

        private PlayerRigidbodyGroundState(Rigidbody2D target, bool preserveVelocityOnRestore)
        {
            this.target = target;
            this.preserveVelocityOnRestore = preserveVelocityOnRestore;
            linearVelocity = target.linearVelocity;
            angularVelocity = target.angularVelocity;
        }

        public static PlayerRigidbodyGroundState Capture(Rigidbody2D target, bool preserveVelocityOnRestore)
        {
            return target != null ? new PlayerRigidbodyGroundState(target, preserveVelocityOnRestore) : null;
        }

        public void ApplyLock()
        {
            if (target == null)
            {
                return;
            }

            target.linearVelocity = Vector2.zero;
            target.angularVelocity = 0f;
            SnapDownToGround();
        }

        public void Restore()
        {
            if (target == null)
            {
                return;
            }

            if (preserveVelocityOnRestore)
            {
                target.linearVelocity = linearVelocity;
                target.angularVelocity = angularVelocity;
            }
            else
            {
                target.linearVelocity = Vector2.zero;
                target.angularVelocity = 0f;
            }

            SnapDownToGround();
            target.WakeUp();
        }

        public static void SnapDownToGround(Rigidbody2D target)
        {
            if (target == null)
            {
                return;
            }

            new PlayerRigidbodyGroundState(target, preserveVelocityOnRestore: false).SnapDownToGround();
        }

        private void SnapDownToGround()
        {
            Collider2D bodyCollider = ResolveBodyCollider();
            int groundMask = BuildGroundMask();
            if (bodyCollider == null || groundMask == 0)
            {
                return;
            }

            Physics2D.SyncTransforms();

            ContactFilter2D filter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = false
            };
            filter.SetLayerMask(groundMask);

            int hitCount = bodyCollider.Cast(Vector2.down, filter, castHits, GroundSnapMaxDistance);
            if (hitCount <= 0)
            {
                return;
            }

            float closestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D hit = castHits[i];
                castHits[i] = default;
                if (hit.collider == null)
                {
                    continue;
                }

                closestDistance = Mathf.Min(closestDistance, Mathf.Max(0f, hit.distance));
            }

            ClearCastBuffer(hitCount);

            if (float.IsInfinity(closestDistance) || closestDistance <= Mathf.Epsilon)
            {
                return;
            }

            Vector2 nextPosition = target.position + Vector2.down * closestDistance;
            target.position = nextPosition;
            Vector3 transformPosition = target.transform.position;
            target.transform.position = new Vector3(nextPosition.x, nextPosition.y, transformPosition.z);
            Physics2D.SyncTransforms();
        }

        private Collider2D ResolveBodyCollider()
        {
            Collider2D[] colliders = target.GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider != null && collider.enabled && !collider.isTrigger)
                {
                    return collider;
                }
            }

            return null;
        }

        private static int BuildGroundMask()
        {
            int mask = 0;
            int groundLayer = LayerMask.NameToLayer(GroundLayerName);
            if (groundLayer >= 0)
            {
                mask |= 1 << groundLayer;
            }

            int fallThroughLayer = LayerMask.NameToLayer(FallThroughFloorLayerName);
            if (fallThroughLayer >= 0)
            {
                mask |= 1 << fallThroughLayer;
            }

            return mask;
        }

        private void ClearCastBuffer(int usedCount)
        {
            for (int i = usedCount; i < castHits.Length; i++)
            {
                castHits[i] = default;
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

    private sealed class LetterBoxExitState
    {
        public LetterBoxExitState(
            GameObject view,
            CanvasGroup canvasGroup,
            bool restoreActiveSelf,
            float restoreAlpha)
        {
            View = view;
            CanvasGroup = canvasGroup;
            RestoreActiveSelf = restoreActiveSelf;
            RestoreAlpha = restoreAlpha;
        }

        public GameObject View { get; }
        public CanvasGroup CanvasGroup { get; }
        public bool RestoreActiveSelf { get; }
        public float RestoreAlpha { get; }
    }
}
