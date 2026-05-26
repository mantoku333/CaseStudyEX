using System.Collections;
using System.Collections.Generic;
using Metroidvania.Managers;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yarn.Unity;

public sealed class StoryEventRunner : MonoBehaviour
{
    [SerializeField] private DialogueManager dialogueManager = null!;
    [SerializeField] private string eventCameraName = "EventCam";
    [SerializeField] private int eventCameraPriorityFloor = 100;
    [SerializeField] private GameObject letterBoxView;
    [SerializeField] private string letterBoxViewName = "LetterBoxView";
    [SerializeField] private float letterBoxFadeSeconds = 0.6f;
    [SerializeField] private float letterBoxSlidePixels = 120f;

    private readonly Queue<StoryEventDefinition> queuedEvents = new Queue<StoryEventDefinition>();
    private StoryEventDefinition activeEvent;
    private DialogueRunner activeDialogueRunner;
    private Coroutine activeRoutine;
    private bool waitingDialogueCompletion;
    private CinemachineCamera activeEventCamera;
    private int cachedEventCameraPriorityValue;
    private bool cachedEventCameraPriorityEnabled;
    private bool hasCachedEventCameraPriority;
    private bool activeEventStartMutationsApplied;
    private Canvas cachedHudCanvas;
    private GraphicRaycaster cachedHudRaycaster;
    private bool cachedHudCanvasEnabled;
    private bool cachedHudRaycasterEnabled;
    private MinimapManager cachedMinimapManager;
    private MinimapView cachedMinimapView;
    private bool cachedMinimapManagerEnabled;
    private bool cachedMiniMapVisible;
    private bool cachedFullMapVisible;
    private bool hasCachedPrologueUiState;
    private bool cachedLetterBoxViewActiveSelf;
    private float cachedLetterBoxAlpha = 1f;
    private CanvasGroup cachedLetterBoxCanvasGroup;
    private RectTransform cachedLetterBoxTop;
    private RectTransform cachedLetterBoxBottom;
    private Vector2 cachedLetterBoxTopAnchoredPosition;
    private Vector2 cachedLetterBoxBottomAnchoredPosition;
    private Coroutine letterBoxFadeRoutine;
    private bool hasCachedLetterBoxViewState;

    public bool HasPendingEvents => activeEvent != null || queuedEvents.Count > 0;

    public void Enqueue(StoryEventDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        queuedEvents.Enqueue(definition);
        TryStartNextEvent();
    }

    public void ClearQueue()
    {
        queuedEvents.Clear();
    }

    public bool CompleteActiveEventImmediately()
    {
        if (activeEvent == null)
        {
            return false;
        }

        StoryEventDefinition completedEvent = activeEvent;

        if (!activeEventStartMutationsApplied)
        {
            completedEvent.onStartMutations?.Apply();
        }

        if (activeDialogueRunner != null && activeDialogueRunner.IsDialogueRunning)
        {
            activeDialogueRunner.Stop();
        }

        StopTimelineActions(completedEvent.preActions);
        StopTimelineActions(completedEvent.postActions);

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        waitingDialogueCompletion = false;
        UnsubscribeFromDialogueComplete();
        StoryPauseRuntime.ClearOverride();
        RestoreEventCameraPriority();
        RestoreLetterBoxViewVisibility();

        ApplyCompletionState(completedEvent);

        activeEvent = null;
        activeEventStartMutationsApplied = false;
        TryStartNextEvent();
        return true;
    }

    private void OnDisable()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        waitingDialogueCompletion = false;
        UnsubscribeFromDialogueComplete();
        RestoreEventCameraPriority();
        RestoreLetterBoxViewVisibility();
        StoryPauseRuntime.ClearOverride();

        activeEvent = null;
        queuedEvents.Clear();
    }

    private void TryStartNextEvent()
    {
        if (activeEvent != null || activeRoutine != null)
        {
            return;
        }

        while (queuedEvents.Count > 0)
        {
            StoryEventDefinition nextEvent = queuedEvents.Dequeue();
            if (TryStartEvent(nextEvent))
            {
                return;
            }
        }
    }

    private bool TryStartEvent(StoryEventDefinition definition)
    {
        if (definition == null)
        {
            return false;
        }

        string activeSceneName = SceneManager.GetActiveScene().name;
        if (!definition.MatchesScene(activeSceneName))
        {
            return false;
        }

        if (!definition.CanRunByFlags())
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.dialogueNodeName))
        {
            Debug.LogWarning("[StoryEventRunner] dialogueNodeName is empty.");
            return false;
        }

        if (dialogueManager == null)
        {
            dialogueManager = FindFirstObjectByType<DialogueManager>();
        }

        if (dialogueManager == null || dialogueManager.Runner == null)
        {
            Debug.LogWarning($"[StoryEventRunner] DialogueManager not found. eventId='{definition.eventId}'");
            return false;
        }

        DialogueRunner runner = dialogueManager.Runner;
        if (runner.Dialogue == null || !runner.Dialogue.NodeExists(definition.dialogueNodeName))
        {
            Debug.LogWarning(
                $"[StoryEventRunner] Dialogue node not found. eventId='{definition.eventId}', node='{definition.dialogueNodeName}'");
            return false;
        }

        if (runner.IsDialogueRunning && definition.skipWhenDialogueRunning)
        {
            Debug.LogWarning(
                $"[StoryEventRunner] Dialogue is already running. eventId='{definition.eventId}' was skipped.");
            return false;
        }

        if (runner.IsDialogueRunning && !definition.skipWhenDialogueRunning)
        {
            runner.Stop();
        }

        activeEvent = definition;
        activeEventStartMutationsApplied = false;
        activeRoutine = StartCoroutine(RunEventSequence(definition, runner));
        return true;
    }

    private IEnumerator RunEventSequence(StoryEventDefinition definition, DialogueRunner runner)
    {
        ElevateEventCameraPriority();
        ApplyPrologueUiVisibility(definition);
        ShowLetterBoxView();

        try
        {
            StoryPauseRuntime.SetOverride(definition.pausePolicy);

            if (string.Equals(definition.eventId, "prologue", System.StringComparison.OrdinalIgnoreCase))
            {
                CameraIntroMove introMove =
                    FindFirstObjectByType<CameraIntroMove>(FindObjectsInactive.Include);

                if (introMove != null)
                {
                    yield return introMove.PlayIntroSequence();
                }
            }

            if (definition.preActions != null && definition.preActions.Count > 0)
            {
                yield return RunActions(definition.preActions);
            }

            activeDialogueRunner = runner;
            waitingDialogueCompletion = true;
            activeDialogueRunner.onDialogueComplete?.AddListener(OnDialogueComplete);

            activeEventStartMutationsApplied = true;
            definition.onStartMutations?.Apply();
            Debug.LogWarning(
                $"[StoryEventRunner] Start eventId='{definition.eventId}', node='{definition.dialogueNodeName}', style={definition.dialogueStyle}, frame={Time.frameCount}");
            dialogueManager.StartConversation(definition.dialogueNodeName, definition.dialogueStyle);

            while (waitingDialogueCompletion)
            {
                yield return null;
            }

            UnsubscribeFromDialogueComplete();

            if (definition.postActions != null && definition.postActions.Count > 0)
            {
                yield return RunActions(definition.postActions);
            }

            ApplyCompletionState(definition);
        }
        finally
        {
            waitingDialogueCompletion = false;
            UnsubscribeFromDialogueComplete();
            StoryPauseRuntime.ClearOverride();
            RestorePrologueUiVisibility();
            RestoreEventCameraPriority();
            RestoreLetterBoxViewVisibility();
            activeEvent = null;
            activeEventStartMutationsApplied = false;
            activeRoutine = null;
        }

        TryStartNextEvent();
    }

    private IEnumerator RunActions(List<StoryEventActionDefinition> actions)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            StoryEventActionDefinition action = actions[i];
            if (action == null)
            {
                continue;
            }

            yield return ExecuteAction(action);
        }
    }

    private IEnumerator ExecuteAction(StoryEventActionDefinition action)
    {
        switch (action.actionType)
        {
            case StoryEventActionType.DelayRealtime:
                yield return WaitForRealtime(action.seconds);
                yield break;

            case StoryEventActionType.FadeOverlay:
                yield return StoryOverlayFader.Instance.FadeTo(
                    action.targetAlpha,
                    action.durationSeconds,
                    action.overlayColor);
                yield break;

            case StoryEventActionType.SwitchCameraPriority:
                ApplyCameraPriority(action);
                yield break;

            case StoryEventActionType.PlayTimeline:
                if (action.waitForCompletion)
                {
                    yield return PlayTimelineAndWait(action);
                }
                else
                {
                    PlayTimeline(action);
                }
                yield break;

            case StoryEventActionType.PlayStoryEventTimeline:
                if (action.waitForCompletion)
                {
                    yield return PlayStoryEventTimelineAndWait(action);
                }
                else
                {
                    PlayStoryEventTimeline(action);
                }
                yield break;
        }
    }

    private static void ApplyCameraPriority(StoryEventActionDefinition action)
    {
        if (string.IsNullOrWhiteSpace(action.targetName))
        {
            return;
        }

        CinemachineCamera[] cameras = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] == null)
            {
                continue;
            }

            if (cameras[i].name == action.targetName.Trim())
            {
                cameras[i].Priority = action.priority;
            }

            if (!string.IsNullOrWhiteSpace(action.secondaryTargetName) &&
                cameras[i].name == action.secondaryTargetName.Trim())
            {
                cameras[i].Priority = action.secondaryPriority;
            }
        }
    }

    private static void PlayTimeline(StoryEventActionDefinition action)
    {
        PlayableDirector director = FindDirectorByName(action.targetName);
        if (director == null)
        {
            return;
        }

        if (action.forceUnscaledTime)
        {
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        }

        if (action.stopBeforePlay)
        {
            director.Stop();
        }

        director.Play();
    }

    private static IEnumerator PlayTimelineAndWait(StoryEventActionDefinition action)
    {
        PlayableDirector director = FindDirectorByName(action.targetName);
        if (director == null)
        {
            yield break;
        }

        if (action.forceUnscaledTime)
        {
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        }

        if (action.stopBeforePlay)
        {
            director.Stop();
        }

        director.Play();
        while (director.state == PlayState.Playing)
        {
            yield return null;
        }
    }

    private static void PlayStoryEventTimeline(StoryEventActionDefinition action)
    {
        StoryEventController controller = FindStoryEventController(action.targetName);
        if (controller != null)
        {
            controller.PlayEvent();
        }
    }

    private static IEnumerator PlayStoryEventTimelineAndWait(StoryEventActionDefinition action)
    {
        StoryEventController controller = FindStoryEventController(action.targetName);
        if (controller == null || !controller.PlayEvent())
        {
            yield break;
        }

        while (controller.IsPlaying)
        {
            yield return null;
        }
    }

    private static StoryEventController FindStoryEventController(string eventNameOrId)
    {
        if (string.IsNullOrWhiteSpace(eventNameOrId))
        {
            return null;
        }

        string trimmedName = eventNameOrId.Trim();
        StoryEventController[] controllers =
            Object.FindObjectsByType<StoryEventController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            StoryEventController controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            if (string.Equals(controller.EventId, trimmedName, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(controller.name, trimmedName, System.StringComparison.OrdinalIgnoreCase))
            {
                return controller;
            }
        }

        return null;
    }

    private static PlayableDirector FindDirectorByName(string directorName)
    {
        if (string.IsNullOrWhiteSpace(directorName))
        {
            return null;
        }

        string trimmedName = directorName.Trim();
        PlayableDirector[] directors = Object.FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None);
        for (int i = 0; i < directors.Length; i++)
        {
            if (directors[i] != null && directors[i].name == trimmedName)
            {
                return directors[i];
            }
        }

        return null;
    }

    private static IEnumerator WaitForRealtime(float seconds)
    {
        float duration = Mathf.Max(0f, seconds);
        if (duration <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void OnDialogueComplete()
    {
        waitingDialogueCompletion = false;
    }

    private static void ApplyCompletionState(StoryEventDefinition definition)
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

    private static void StopTimelineActions(List<StoryEventActionDefinition> actions)
    {
        if (actions == null || actions.Count == 0)
        {
            return;
        }

        for (int i = 0; i < actions.Count; i++)
        {
            StoryEventActionDefinition action = actions[i];
            if (action == null || action.actionType != StoryEventActionType.PlayTimeline)
            {
                continue;
            }

            PlayableDirector director = FindDirectorByName(action.targetName);
            if (director != null && director.state == PlayState.Playing)
            {
                director.Stop();
            }
        }
    }

    private void UnsubscribeFromDialogueComplete()
    {
        if (activeDialogueRunner != null)
        {
            activeDialogueRunner.onDialogueComplete?.RemoveListener(OnDialogueComplete);
            activeDialogueRunner = null;
        }
    }

    private void ElevateEventCameraPriority()
    {
        RestoreEventCameraPriority();

        if (string.IsNullOrWhiteSpace(eventCameraName))
        {
            return;
        }

        string targetName = eventCameraName.Trim();
        CinemachineCamera[] cameras = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        int maxPriority = int.MinValue;

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] == null)
            {
                continue;
            }

            maxPriority = Mathf.Max(maxPriority, cameras[i].Priority.Value);

            if (activeEventCamera == null &&
                (string.Equals(cameras[i].name, targetName, System.StringComparison.OrdinalIgnoreCase) ||
                 cameras[i].name.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                activeEventCamera = cameras[i];
            }
        }

        if (activeEventCamera == null)
        {
            return;
        }

        cachedEventCameraPriorityValue = activeEventCamera.Priority.Value;
        cachedEventCameraPriorityEnabled = activeEventCamera.Priority.Enabled;
        hasCachedEventCameraPriority = true;

        int topPriority = maxPriority == int.MinValue ? eventCameraPriorityFloor : maxPriority + 10;
        int desiredPriority = Mathf.Max(eventCameraPriorityFloor, topPriority);
        activeEventCamera.Priority.Value = desiredPriority;
        activeEventCamera.Priority.Enabled = true;
    }

    private void RestoreEventCameraPriority()
    {
        if (!hasCachedEventCameraPriority)
        {
            return;
        }

        if (activeEventCamera != null)
        {
            activeEventCamera.Priority.Value = cachedEventCameraPriorityValue;
            activeEventCamera.Priority.Enabled = cachedEventCameraPriorityEnabled;
        }

        activeEventCamera = null;
        hasCachedEventCameraPriority = false;
        cachedEventCameraPriorityValue = 0;
        cachedEventCameraPriorityEnabled = false;
    }

    private void ShowLetterBoxView()
    {
        GameObject view = ResolveLetterBoxView();
        if (view == null)
        {
            return;
        }

        if (!hasCachedLetterBoxViewState)
        {
            cachedLetterBoxViewActiveSelf = view.activeSelf;
            cachedLetterBoxCanvasGroup = EnsureLetterBoxCanvasGroup(view);
            cachedLetterBoxAlpha = cachedLetterBoxCanvasGroup != null ? cachedLetterBoxCanvasGroup.alpha : 1f;
            CacheLetterBoxBars(view);
            hasCachedLetterBoxViewState = true;
        }

        view.SetActive(true);
        CanvasGroup canvasGroup = EnsureLetterBoxCanvasGroup(view);
        if (canvasGroup == null)
        {
            return;
        }

        float fromAlpha = 0f;
        canvasGroup.alpha = fromAlpha;
        SetLetterBoxSlidePosition(0f);
        StartLetterBoxTransition(canvasGroup, fromAlpha, 1f, 0f, 1f, null);
    }

    private void RestoreLetterBoxViewVisibility()
    {
        if (!hasCachedLetterBoxViewState)
        {
            return;
        }

        bool restoreActiveSelf = cachedLetterBoxViewActiveSelf;
        float restoreAlpha = cachedLetterBoxAlpha;
        GameObject view = ResolveLetterBoxView();
        if (view != null)
        {
            CanvasGroup canvasGroup = EnsureLetterBoxCanvasGroup(view);
            if (canvasGroup != null)
            {
                StartLetterBoxTransition(canvasGroup, canvasGroup.alpha, 0f, 1f, 0f, () =>
                {
                    if (view != null)
                    {
                        RestoreLetterBoxBarPositions();
                        view.SetActive(restoreActiveSelf);
                        if (restoreActiveSelf && canvasGroup != null)
                        {
                            canvasGroup.alpha = restoreAlpha;
                        }
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
        cachedLetterBoxCanvasGroup = null;
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

    private RectTransform FindLetterBoxBar(Transform root, string barName)
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

        RectTransform[] rectTransforms = root.GetComponentsInChildren<RectTransform>(true);
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
            float topHeight = cachedLetterBoxTop.rect.height;
            float topOffset = Mathf.Max(hiddenOffset, topHeight);
            cachedLetterBoxTop.anchoredPosition =
                Vector2.Lerp(
                    cachedLetterBoxTopAnchoredPosition + new Vector2(0f, topOffset),
                    cachedLetterBoxTopAnchoredPosition,
                    normalizedVisible);
        }

        if (cachedLetterBoxBottom != null)
        {
            float bottomHeight = cachedLetterBoxBottom.rect.height;
            float bottomOffset = Mathf.Max(hiddenOffset, bottomHeight);
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
        System.Action onComplete)
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
        System.Action onComplete)
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

    private void ApplyPrologueUiVisibility(StoryEventDefinition definition)
    {
        RestorePrologueUiVisibility();

        if (definition == null ||
            !string.Equals(definition.eventId, "prologue", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        GameObject hudObject = GameObject.Find("PlayerHUDCanvas");
        if (hudObject != null)
        {
            cachedHudCanvas = hudObject.GetComponent<Canvas>();
            cachedHudRaycaster = hudObject.GetComponent<GraphicRaycaster>();

            if (cachedHudCanvas != null)
            {
                cachedHudCanvasEnabled = cachedHudCanvas.enabled;
                cachedHudCanvas.enabled = false;
                hasCachedPrologueUiState = true;
            }

            if (cachedHudRaycaster != null)
            {
                cachedHudRaycasterEnabled = cachedHudRaycaster.enabled;
                cachedHudRaycaster.enabled = false;
                hasCachedPrologueUiState = true;
            }
        }

        cachedMinimapManager = MinimapManager.Instance;
        if (cachedMinimapManager != null)
        {
            cachedMinimapManagerEnabled = cachedMinimapManager.enabled;
            cachedMinimapManager.enabled = false;
            cachedMinimapView = cachedMinimapManager.GetComponent<MinimapView>();

            if (cachedMinimapView != null)
            {
                cachedMiniMapVisible = cachedMinimapView.IsMiniMapVisible;
                cachedFullMapVisible = cachedMinimapView.IsFullMapVisible;
                cachedMinimapView.SetPanelVisibility(false, false);
            }

            hasCachedPrologueUiState = true;
        }
    }

    private void RestorePrologueUiVisibility()
    {
        if (!hasCachedPrologueUiState)
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
        cachedMinimapManager = null;
        cachedMinimapView = null;
        cachedHudCanvasEnabled = false;
        cachedHudRaycasterEnabled = false;
        cachedMinimapManagerEnabled = false;
        cachedMiniMapVisible = false;
        cachedFullMapVisible = false;
        hasCachedPrologueUiState = false;
    }
}
