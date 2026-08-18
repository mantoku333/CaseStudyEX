using System.Collections;
using System.Collections.Generic;
using GameName.Enemy;
using GameName.UI;
using Metroidvania.Data;
using Metroidvania.Managers;
using Player;
using Spine;
using Spine.Unity;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameName.Ending
{
    [DisallowMultipleComponent]
    public sealed class LastBossEndingDirector : MonoBehaviour
    {
        private const string RuntimeCameraName = "EndingCamera_Runtime";

        [Header("Activation")]
        [Tooltip("If empty, this director can run in any scene. Set this to limit it to one scene.")]
        [SerializeField] private string endingSceneName;
        [Tooltip("If not assigned, the director searches by Last Boss Object Name, then any LastBossController.")]
        [SerializeField] private LastBossController targetLastBoss;
        [SerializeField] private string lastBossObjectName = "LastBoss";
        [SerializeField] private string titleSceneName = "Title";
        [SerializeField] private bool waitForBossAreaCompletionBeforeEnding = true;
        [SerializeField] private bool waitForStoryEventsBeforeEnding = true;

        [Header("Camera")]
        [SerializeField] private CinemachineCamera endingCamera;
        [SerializeField] private string endingCameraName = "EndingCamera";
        [SerializeField] private int endingCameraPriority = 200;
        [SerializeField] private Transform cameraStartPoint;
        [SerializeField] private Transform cameraSkyPoint;
        [SerializeField] private Vector3 fallbackSkyOffset = new Vector3(0f, 18f, -10f);

        [Header("Timing")]
        [SerializeField, Min(0f)] private float fadeToBlackDuration = 1.2f;
        [SerializeField, Min(0f)] private float fadeFromBlackDuration = 1.2f;
        [SerializeField, Min(0f)] private float blackHoldSeconds = 1.35f;
        [SerializeField, Min(0f)] private float skyRiseDuration = 5f;
        [SerializeField, Min(0f)] private float endingBgmStartBeforeFadeEndSeconds = 0.5f;

        [Header("Arcanciel Reward")]
        [SerializeField] private ItemData arcancielRewardItemData;
        [SerializeField] private string arcancielRewardProgressFlagKey = GameProgressKeys.EquipmentArcancielUnlocked;
        [SerializeField] private Sprite arcancielRewardNotificationSprite;
        [SerializeField] private Vector2 arcancielRewardNotificationSize = new Vector2(512f, 130f);
        [SerializeField] private Vector2 arcancielRewardNotificationBottomLeftOffset = new Vector2(32f, 32f);
        [SerializeField, Min(0f)] private float arcancielRewardSlideInDuration = 0.45f;
        [SerializeField, Min(0f)] private float arcancielRewardHoldSeconds = 1.2f;
        [SerializeField, Min(0f)] private float arcancielRewardSlideOutDuration = 0.35f;

        [Header("Sky Image")]
        [SerializeField] private Sprite skyImageSprite;
        [SerializeField] private SpriteRenderer skyImageRenderer;
        [SerializeField] private CanvasGroup skyImageCanvasGroup;
        [SerializeField] private string skyImageObjectName = "EndingSkyImage";
        [SerializeField] private Vector2 skyImageWorldSize = new Vector2(18f, 10f);
        [SerializeField] private int skyImageSortingOrder = 50;

        [Header("BGM")]
        [SerializeField] private global::StageBgmController stageBgm;
        [SerializeField] private AudioClip endingBgm;
        [SerializeField, Range(0f, 1f)] private float endingBgmVolume = 0.35f;
        [SerializeField] private bool stopCurrentBgmOnBlackout = true;
        [SerializeField, Min(0f)] private float stopCurrentBgmFadeSeconds = 0f;
        [SerializeField] private bool stopTimelineBgmOnReturn = true;

        [Header("Credits")]
        [SerializeField] private EndingCreditsCanvasController creditsCanvasPrefab;
        [SerializeField] private EndingCreditsCanvasController creditsCanvasPanel;
        [SerializeField] private bool returnToTitleWhenCreditsEnd;

        [Header("Player Ending Pose")]
        [SerializeField] private bool resetPlayerToWalkPoseAfterBlackout = true;
        [SerializeField] private string[] animatorWalkStateNames = { "walk", "Walk", "run", "Run" };
        [SerializeField] private string[] spineWalkAnimationNames = { "walk", "run" };

        [Header("UI")]
        [SerializeField] private CanvasGroup[] hideCanvasGroups;
        [SerializeField] private GameObject[] hideObjects;
        [SerializeField] private bool autoHideKnownGameplayUi = true;

        private readonly List<BehaviourState> disabledGameplayBehaviours = new List<BehaviourState>();
        private readonly List<GameObject> autoHiddenObjects = new List<GameObject>();
        private readonly List<CanvasGroupState> autoHiddenCanvasGroups = new List<CanvasGroupState>();
        private readonly List<AnimatorState> pausedAnimators = new List<AnimatorState>();
        private readonly List<SpineAnimationState> pausedSpineAnimations = new List<SpineAnimationState>();

        private Coroutine endingRoutine;
        private bool subscribedToBoss;
        private bool endingStarted;
        private bool canSkipCredits;
        private bool returningToTitle;
        private bool runtimeSkyImageCreated;
        private bool runtimeCreditsCanvasCreated;
        private bool runtimeRewardNotificationCanvasCreated;
        private float skipInputIgnoreUntil;
        private int originalCameraPriority;
        private bool originalCameraPriorityEnabled;
        private bool hasOriginalCameraPriority;
        private global::PlayerController endingPlayerController;
        private CanvasGroup arcancielRewardNotificationCanvasGroup;
        private RectTransform arcancielRewardNotificationRect;
        private Image arcancielRewardNotificationImage;

        private void Awake()
        {
            ResolveSceneReferences();
            PrepareSkyImage(0f);
            PrepareCreditsCanvas();
            creditsCanvasPanel?.SetImmediateHidden();
        }

        private void OnEnable()
        {
            TrySubscribeToBoss();
        }

        private void OnDisable()
        {
            UnsubscribeFromBoss();
        }

        private void OnDestroy()
        {
            UnsubscribeFromBoss();
            RestoreGameplayControls();
            RestoreAutoHiddenUi();
            RestoreEndingCameraPriority();
            creditsCanvasPanel?.StopPlayback();

            if (runtimeCreditsCanvasCreated && creditsCanvasPanel != null)
            {
                Destroy(creditsCanvasPanel.gameObject);
            }

            if (runtimeRewardNotificationCanvasCreated && arcancielRewardNotificationCanvasGroup != null)
            {
                Destroy(arcancielRewardNotificationCanvasGroup.gameObject);
            }
        }

        private void Update()
        {
            if (!subscribedToBoss && !endingStarted)
            {
                TrySubscribeToBoss();
            }

            if (endingStarted &&
                canSkipCredits &&
                !returningToTitle &&
                Time.unscaledTime >= skipInputIgnoreUntil &&
                WasSpacePressedThisFrame())
            {
                ReturnToTitle();
            }
        }

        private void TrySubscribeToBoss()
        {
            if (subscribedToBoss || endingStarted || !IsInConfiguredEndingScene())
            {
                return;
            }

            ResolveTargetBoss();
            if (targetLastBoss == null)
            {
                return;
            }

            targetLastBoss.Died -= HandleLastBossDied;
            targetLastBoss.Died += HandleLastBossDied;
            subscribedToBoss = true;
        }

        private void UnsubscribeFromBoss()
        {
            if (targetLastBoss != null)
            {
                targetLastBoss.Died -= HandleLastBossDied;
            }

            subscribedToBoss = false;
        }

        private void HandleLastBossDied()
        {
            if (endingStarted || !IsInConfiguredEndingScene())
            {
                return;
            }

            if (endingRoutine != null)
            {
                StopCoroutine(endingRoutine);
            }

            global::BossAreaController targetBossArea = ResolveTargetBossArea();
            endingRoutine = StartCoroutine(PlayArcancielRewardThenEndingRoutine(targetBossArea));
        }

        private IEnumerator PlayArcancielRewardThenEndingRoutine(global::BossAreaController targetBossArea)
        {
            endingStarted = true;
            canSkipCredits = false;
            skipInputIgnoreUntil = float.PositiveInfinity;

            yield return WaitForBossAreaAndStoryEventsBeforeEnding(targetBossArea);

            ResolveSceneReferences();
            DisableGameplayControls();

            bool rewardGranted = !DecorationShopFeature.Enabled &&
                TryGrantArcancielReward(arcancielRewardItemData, arcancielRewardProgressFlagKey);

            if (rewardGranted)
            {
                yield return PlayArcancielRewardNotification();
            }

            yield return PlayEndingRoutine(true);
        }

        private IEnumerator WaitForBossAreaAndStoryEventsBeforeEnding(global::BossAreaController targetBossArea)
        {
            if (waitForBossAreaCompletionBeforeEnding && targetBossArea != null)
            {
                while (!returningToTitle && targetBossArea != null && !targetBossArea.IsEncounterCompleted)
                {
                    yield return null;
                }
            }

            if (!waitForStoryEventsBeforeEnding)
            {
                yield break;
            }

            // BossAreaController starts the post-defeat event after it publishes
            // EncounterCompleted, so give that same frame a chance to enqueue it.
            yield return null;

            while (!returningToTitle && HasStoryEventActivity())
            {
                yield return null;
            }
        }

        private global::BossAreaController ResolveTargetBossArea()
        {
            ResolveTargetBoss();
            if (targetLastBoss == null)
            {
                return null;
            }

            global::BossAreaController[] bossAreas =
                FindObjectsByType<global::BossAreaController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < bossAreas.Length; i++)
            {
                global::BossAreaController bossArea = bossAreas[i];
                if (IsBossAreaForTargetLastBoss(bossArea, targetLastBoss))
                {
                    return bossArea;
                }
            }

            return null;
        }

        private static bool IsBossAreaForTargetLastBoss(
            global::BossAreaController bossArea,
            LastBossController lastBoss)
        {
            if (bossArea == null || lastBoss == null)
            {
                return false;
            }

            if (bossArea.LastBossController == lastBoss)
            {
                return true;
            }

            Transform bossRoot = bossArea.BossRoot;
            Transform lastBossTransform = lastBoss.transform;
            return bossRoot != null && lastBossTransform != null && bossRoot == lastBossTransform;
        }

        private static bool HasStoryEventActivity()
        {
            return StoryEventRuntimeService.HasPendingEvents || IsActiveDialogueRunning();
        }

        private static bool IsActiveDialogueRunning()
        {
            DialogueManager dialogueManager = FindFirstObjectByType<DialogueManager>();
            return dialogueManager != null &&
                   dialogueManager.Runner != null &&
                   dialogueManager.Runner.IsDialogueRunning;
        }

        private IEnumerator PlayEndingRoutine(bool gameplayControlsAlreadyDisabled = false)
        {
            endingStarted = true;
            canSkipCredits = false;
            skipInputIgnoreUntil = float.PositiveInfinity;

            ResolveSceneReferences();
            if (!gameplayControlsAlreadyDisabled)
            {
                DisableGameplayControls();
            }

            // Other death subscribers may start coroutines, so wait one frame before hiding their GameObjects.
            yield return null;

            HideGameplayUi();
            PrepareSkyImage(0f);
            PrepareCreditsCanvas();
            creditsCanvasPanel?.SetImmediateHidden();

            global::StoryOverlayFader.Instance.SetImmediate(0f, Color.black);
            yield return global::StoryOverlayFader.Instance.FadeTo(1f, fadeToBlackDuration, Color.black);
            StopCurrentBgmOnBlackout();

            EnsureEndingCamera();
            ActivateEndingCamera();
            PositionEndingCameraAtStart();
            ResetPlayerToWalkPoseAfterBlackout();

            if (blackHoldSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(blackHoldSeconds);
            }

            yield return FadeFromBlackAndStartEndingBgm();

            yield return AnimateCamera(
                endingCamera.transform.position,
                ResolveSkyPosition(),
                skyRiseDuration);

            yield return PlayCreditsRoutine();

            if (returnToTitleWhenCreditsEnd && !returningToTitle)
            {
                ReturnToTitle();
            }
        }

        public static bool TryGrantArcancielReward(ItemData rewardItemData, string fallbackProgressFlagKey)
        {
            string itemId = ResolveArcancielRewardItemId(rewardItemData, fallbackProgressFlagKey);
            if (string.IsNullOrWhiteSpace(itemId) ||
                GameProgressFlags.Get(itemId) ||
                GameItems.GetCount(itemId) > 0)
            {
                return false;
            }

            GameProgressFlags.Set(itemId, true);
            GameItems.SetCount(itemId, 1);
            return true;
        }

        public static string ResolveArcancielRewardItemId(ItemData rewardItemData, string fallbackProgressFlagKey)
        {
            if (rewardItemData != null && !string.IsNullOrWhiteSpace(rewardItemData.itemId))
            {
                return rewardItemData.itemId;
            }

            return fallbackProgressFlagKey;
        }

        private IEnumerator PlayArcancielRewardNotification()
        {
            if (!EnsureArcancielRewardNotification())
            {
                yield break;
            }

            Vector2 shownPosition = arcancielRewardNotificationBottomLeftOffset;
            Vector2 hiddenPosition = new Vector2(
                -Mathf.Max(1f, arcancielRewardNotificationSize.x) - 32f,
                shownPosition.y);

            arcancielRewardNotificationCanvasGroup.alpha = 1f;
            arcancielRewardNotificationRect.anchoredPosition = hiddenPosition;
            arcancielRewardNotificationImage.enabled = true;

            yield return MoveArcancielRewardNotification(hiddenPosition, shownPosition, arcancielRewardSlideInDuration);

            if (arcancielRewardHoldSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(arcancielRewardHoldSeconds);
            }

            yield return MoveArcancielRewardNotification(shownPosition, hiddenPosition, arcancielRewardSlideOutDuration);
            arcancielRewardNotificationCanvasGroup.alpha = 0f;
        }

        private IEnumerator MoveArcancielRewardNotification(Vector2 from, Vector2 to, float duration)
        {
            if (arcancielRewardNotificationRect == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                arcancielRewardNotificationRect.anchoredPosition = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration && !returningToTitle)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                arcancielRewardNotificationRect.anchoredPosition = Vector2.LerpUnclamped(from, to, SmootherStep(t));
                yield return null;
            }

            if (!returningToTitle)
            {
                arcancielRewardNotificationRect.anchoredPosition = to;
            }
        }

        private bool EnsureArcancielRewardNotification()
        {
            if (arcancielRewardNotificationSprite == null)
            {
                Debug.LogWarning($"{nameof(LastBossEndingDirector)} has no Arcanciel reward notification sprite.", this);
                return false;
            }

            if (arcancielRewardNotificationCanvasGroup == null)
            {
                GameObject canvasObject = new GameObject("ArcancielRewardNotificationCanvas");
                Canvas canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10000;

                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                arcancielRewardNotificationCanvasGroup = canvasObject.AddComponent<CanvasGroup>();
                arcancielRewardNotificationCanvasGroup.alpha = 0f;
                arcancielRewardNotificationCanvasGroup.interactable = false;
                arcancielRewardNotificationCanvasGroup.blocksRaycasts = false;
                runtimeRewardNotificationCanvasCreated = true;
            }

            if (arcancielRewardNotificationImage == null)
            {
                GameObject imageObject = new GameObject("ArcancielRewardNotificationImage");
                imageObject.transform.SetParent(arcancielRewardNotificationCanvasGroup.transform, false);

                arcancielRewardNotificationRect = imageObject.AddComponent<RectTransform>();
                arcancielRewardNotificationRect.anchorMin = Vector2.zero;
                arcancielRewardNotificationRect.anchorMax = Vector2.zero;
                arcancielRewardNotificationRect.pivot = Vector2.zero;

                arcancielRewardNotificationImage = imageObject.AddComponent<Image>();
                arcancielRewardNotificationImage.raycastTarget = false;
                arcancielRewardNotificationImage.preserveAspect = true;
            }

            if (arcancielRewardNotificationRect == null)
            {
                arcancielRewardNotificationRect = arcancielRewardNotificationImage.GetComponent<RectTransform>();
            }

            arcancielRewardNotificationRect.sizeDelta = arcancielRewardNotificationSize;
            arcancielRewardNotificationImage.sprite = arcancielRewardNotificationSprite;
            return true;
        }

        private IEnumerator AnimateCamera(Vector3 fromPosition, Vector3 toPosition, float duration)
        {
            if (endingCamera == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                endingCamera.transform.position = toPosition;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration && !returningToTitle)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = SmootherStep(t);
                endingCamera.transform.position = Vector3.LerpUnclamped(fromPosition, toPosition, eased);
                yield return null;
            }

            if (!returningToTitle)
            {
                endingCamera.transform.position = toPosition;
            }
        }

        private IEnumerator PlayCreditsRoutine()
        {
            PrepareCreditsCanvas();
            if (creditsCanvasPanel == null)
            {
                yield break;
            }

            yield return creditsCanvasPanel.Play(() => returningToTitle, EnableCreditsSkip);
        }

        private void ReturnToTitle()
        {
            if (returningToTitle)
            {
                return;
            }

            returningToTitle = true;
            canSkipCredits = false;
            Time.timeScale = 1f;

            if (stopTimelineBgmOnReturn)
            {
                global::StoryTimelineRuntime.Instance.StopBgm(0.25f);
            }

            creditsCanvasPanel?.StopPlayback();
            RestoreGameplayControls();
            RestoreAutoHiddenUi();
            SceneManager.LoadScene(titleSceneName);
        }

        private void DisableGameplayControls()
        {
            disabledGameplayBehaviours.Clear();

            global::PlayerController playerController = FindFirstObjectByType<global::PlayerController>(FindObjectsInactive.Exclude);
            if (playerController == null)
            {
                return;
            }

            endingPlayerController = playerController;

            DisableBehaviour(playerController);
            DisableBehaviour(playerController.GetComponent<PlayerInput>());
            DisableBehaviour(playerController.GetComponent<global::DodgeController>());
            DisableBehaviour(playerController.GetComponent<global::FallThroughController>());
            DisableBehaviour(playerController.GetComponent<global::UmbrellaController>());
            DisableBehaviour(playerController.GetComponent<global::UmbrellaAttackController>());
            DisableBehaviour(playerController.GetComponent<global::UmbrellaParryController>());
            DisableBehaviour(playerController.GetComponent<global::GunController>());
            DisableBehaviour(playerController.GetComponent<PlayerAbilityController>());
            DisableBehaviour(playerController.GetComponentInChildren<PlayerSpriteAnimator>(true));
            DisableBehaviour(playerController.GetComponentInChildren<Metroidvania.Player.PlayerSpineAnimator>(true));
            PausePlayerVisualAnimations(playerController);

            Rigidbody2D playerRigidbody = playerController.GetComponent<Rigidbody2D>();
            if (playerRigidbody != null)
            {
                playerRigidbody.linearVelocity = Vector2.zero;
                playerRigidbody.angularVelocity = 0f;
                playerRigidbody.Sleep();
            }
        }

        private void DisableBehaviour(Behaviour behaviour)
        {
            if (behaviour == null || !behaviour.enabled)
            {
                return;
            }

            disabledGameplayBehaviours.Add(new BehaviourState(behaviour, true));
            behaviour.enabled = false;
        }

        private void RestoreGameplayControls()
        {
            RestorePlayerVisualAnimations();

            for (int i = 0; i < disabledGameplayBehaviours.Count; i++)
            {
                BehaviourState state = disabledGameplayBehaviours[i];
                if (state.Behaviour != null)
                {
                    state.Behaviour.enabled = state.WasEnabled;
                }
            }

            disabledGameplayBehaviours.Clear();
        }

        private void PausePlayerVisualAnimations(global::PlayerController playerController)
        {
            pausedAnimators.Clear();
            pausedSpineAnimations.Clear();

            if (playerController == null)
            {
                return;
            }

            Animator[] animators = playerController.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                {
                    continue;
                }

                pausedAnimators.Add(new AnimatorState(animator, animator.speed));
                animator.speed = 0f;
                animator.Update(0f);
            }

            SkeletonAnimation[] skeletonAnimations = playerController.GetComponentsInChildren<SkeletonAnimation>(true);
            for (int i = 0; i < skeletonAnimations.Length; i++)
            {
                SkeletonAnimation skeletonAnimation = skeletonAnimations[i];
                if (skeletonAnimation == null)
                {
                    continue;
                }

                pausedSpineAnimations.Add(new SpineAnimationState(skeletonAnimation, skeletonAnimation.timeScale));
                skeletonAnimation.timeScale = 0f;
            }
        }

        private void RestorePlayerVisualAnimations()
        {
            for (int i = 0; i < pausedAnimators.Count; i++)
            {
                AnimatorState state = pausedAnimators[i];
                if (state.Animator != null)
                {
                    state.Animator.speed = state.Speed;
                }
            }

            pausedAnimators.Clear();

            for (int i = 0; i < pausedSpineAnimations.Count; i++)
            {
                SpineAnimationState state = pausedSpineAnimations[i];
                if (state.SkeletonAnimation != null)
                {
                    state.SkeletonAnimation.timeScale = state.TimeScale;
                }
            }

            pausedSpineAnimations.Clear();
        }

        private void ResetPlayerToWalkPoseAfterBlackout()
        {
            if (!resetPlayerToWalkPoseAfterBlackout)
            {
                return;
            }

            global::PlayerController playerController = endingPlayerController != null
                ? endingPlayerController
                : FindFirstObjectByType<global::PlayerController>(FindObjectsInactive.Include);

            if (playerController == null)
            {
                return;
            }

            ResetAnimatorWalkPose(playerController);
            ResetSpineWalkPose(playerController);
        }

        private void ResetAnimatorWalkPose(global::PlayerController playerController)
        {
            Animator[] animators = playerController.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null || animator.runtimeAnimatorController == null)
                {
                    continue;
                }

                string stateName = ResolveAnimatorWalkStateName(animator);
                if (string.IsNullOrEmpty(stateName))
                {
                    continue;
                }

                animator.Play(stateName, 0, 0f);
                animator.Update(0f);
                animator.speed = 0f;
            }
        }

        private string ResolveAnimatorWalkStateName(Animator animator)
        {
            if (animatorWalkStateNames == null)
            {
                return null;
            }

            for (int i = 0; i < animatorWalkStateNames.Length; i++)
            {
                string stateName = animatorWalkStateNames[i];
                if (string.IsNullOrEmpty(stateName))
                {
                    continue;
                }

                if (animator.HasState(0, Animator.StringToHash(stateName)))
                {
                    return stateName;
                }
            }

            return null;
        }

        private void ResetSpineWalkPose(global::PlayerController playerController)
        {
            SkeletonAnimation[] skeletonAnimations = playerController.GetComponentsInChildren<SkeletonAnimation>(true);
            for (int i = 0; i < skeletonAnimations.Length; i++)
            {
                SkeletonAnimation skeletonAnimation = skeletonAnimations[i];
                if (skeletonAnimation == null || skeletonAnimation.AnimationState == null || skeletonAnimation.Skeleton == null)
                {
                    continue;
                }

                string animationName = ResolveSpineWalkAnimationName(skeletonAnimation);
                if (string.IsNullOrEmpty(animationName))
                {
                    continue;
                }

                skeletonAnimation.AnimationState.SetAnimation(0, animationName, true);
                skeletonAnimation.AnimationState.Update(0f);
                skeletonAnimation.AnimationState.Apply(skeletonAnimation.Skeleton);
                skeletonAnimation.Skeleton.UpdateWorldTransform(Skeleton.Physics.Update);
                skeletonAnimation.timeScale = 0f;
            }
        }

        private string ResolveSpineWalkAnimationName(SkeletonAnimation skeletonAnimation)
        {
            if (spineWalkAnimationNames == null || skeletonAnimation.Skeleton?.Data == null)
            {
                return null;
            }

            for (int i = 0; i < spineWalkAnimationNames.Length; i++)
            {
                string animationName = spineWalkAnimationNames[i];
                if (string.IsNullOrEmpty(animationName))
                {
                    continue;
                }

                if (skeletonAnimation.Skeleton.Data.FindAnimation(animationName) != null)
                {
                    return animationName;
                }
            }

            return null;
        }

        private void HideGameplayUi()
        {
            if (hideCanvasGroups != null)
            {
                for (int i = 0; i < hideCanvasGroups.Length; i++)
                {
                    if (hideCanvasGroups[i] != null)
                    {
                        hideCanvasGroups[i].alpha = 0f;
                    }
                }
            }

            if (hideObjects != null)
            {
                for (int i = 0; i < hideObjects.Length; i++)
                {
                    if (hideObjects[i] != null)
                    {
                        HideObjectForEnding(hideObjects[i]);
                    }
                }
            }

            if (!autoHideKnownGameplayUi)
            {
                return;
            }

            AutoHideObjectByName("PlayerHUDCanvas");
            AutoHideObjectByName("BossHPCanvas");
            AutoHideObjectByName("Boss HP");
            AutoHideObjectByName("MinimapSystem");
            AutoHideObjectByName("AreaIllustrationCanvas");
        }

        private void AutoHideObjectByName(string objectName)
        {
            GameObject target = GameObject.Find(objectName);
            if (target == null || target == gameObject || IsCreditsPanelObject(target))
            {
                return;
            }

            HideObjectForEnding(target);
        }

        private void HideObjectForEnding(GameObject target)
        {
            if (target == null || target == gameObject || IsCreditsPanelObject(target))
            {
                return;
            }

            if (ContainsBossHpController(target))
            {
                HideWithCanvasGroup(target);
                return;
            }

            if (target.activeSelf)
            {
                autoHiddenObjects.Add(target);
                target.SetActive(false);
            }
        }

        private void HideWithCanvasGroup(GameObject target)
        {
            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = target.AddComponent<CanvasGroup>();
            }

            autoHiddenCanvasGroups.Add(new CanvasGroupState(
                canvasGroup,
                canvasGroup.alpha,
                canvasGroup.interactable,
                canvasGroup.blocksRaycasts));

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private static bool ContainsBossHpController(GameObject target)
        {
            return target != null &&
                   (target.name == "Boss HP" ||
                    target.name == "BossHPCanvas" ||
                    target.GetComponentInChildren<BossHpCanvasController>(true) != null);
        }

        private bool IsCreditsPanelObject(GameObject target)
        {
            return target != null &&
                   creditsCanvasPanel != null &&
                   (target == creditsCanvasPanel.gameObject || target.transform.IsChildOf(creditsCanvasPanel.transform));
        }

        private void RestoreAutoHiddenUi()
        {
            for (int i = 0; i < autoHiddenObjects.Count; i++)
            {
                if (autoHiddenObjects[i] != null)
                {
                    autoHiddenObjects[i].SetActive(true);
                }
            }

            autoHiddenObjects.Clear();

            for (int i = 0; i < autoHiddenCanvasGroups.Count; i++)
            {
                CanvasGroupState state = autoHiddenCanvasGroups[i];
                if (state.CanvasGroup != null)
                {
                    state.CanvasGroup.alpha = state.Alpha;
                    state.CanvasGroup.interactable = state.Interactable;
                    state.CanvasGroup.blocksRaycasts = state.BlocksRaycasts;
                }
            }

            autoHiddenCanvasGroups.Clear();
        }

        private void ResolveSceneReferences()
        {
            ResolveTargetBoss();

            if (stageBgm == null)
            {
                stageBgm = FindFirstObjectByType<global::StageBgmController>(FindObjectsInactive.Include);
            }

            if (endingCamera == null && !string.IsNullOrWhiteSpace(endingCameraName))
            {
                GameObject cameraObject = GameObject.Find(endingCameraName);
                if (cameraObject != null)
                {
                    endingCamera = cameraObject.GetComponent<CinemachineCamera>();
                }
            }

            if (skyImageRenderer == null || skyImageCanvasGroup == null)
            {
                GameObject imageObject = GameObject.Find(skyImageObjectName);
                if (imageObject != null)
                {
                    if (skyImageRenderer == null)
                    {
                        skyImageRenderer = imageObject.GetComponent<SpriteRenderer>();
                    }

                    if (skyImageCanvasGroup == null)
                    {
                        skyImageCanvasGroup = imageObject.GetComponent<CanvasGroup>();
                    }
                }
            }

            if (skyImageRenderer != null && skyImageRenderer.sprite == null && skyImageSprite != null)
            {
                skyImageRenderer.sprite = skyImageSprite;
                FitSkyImageToConfiguredSize();
            }
        }

        private void ResolveTargetBoss()
        {
            if (targetLastBoss != null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(lastBossObjectName))
            {
                GameObject bossObject = GameObject.Find(lastBossObjectName);
                if (bossObject != null)
                {
                    targetLastBoss = bossObject.GetComponent<LastBossController>();
                    if (targetLastBoss != null)
                    {
                        return;
                    }
                }
            }

            targetLastBoss = FindFirstObjectByType<LastBossController>(FindObjectsInactive.Include);
        }

        private void EnsureEndingCamera()
        {
            if (endingCamera != null)
            {
                return;
            }

            var cameraObject = new GameObject(RuntimeCameraName);
            endingCamera = cameraObject.AddComponent<CinemachineCamera>();
        }

        private void ActivateEndingCamera()
        {
            if (endingCamera == null)
            {
                return;
            }

            if (!hasOriginalCameraPriority)
            {
                originalCameraPriority = endingCamera.Priority.Value;
                originalCameraPriorityEnabled = endingCamera.Priority.Enabled;
                hasOriginalCameraPriority = true;
            }

            endingCamera.Priority.Enabled = true;
            endingCamera.Priority.Value = endingCameraPriority;
        }

        private void RestoreEndingCameraPriority()
        {
            if (!hasOriginalCameraPriority || endingCamera == null)
            {
                return;
            }

            endingCamera.Priority.Value = originalCameraPriority;
            endingCamera.Priority.Enabled = originalCameraPriorityEnabled;
            hasOriginalCameraPriority = false;
        }

        private void PositionEndingCameraAtStart()
        {
            if (endingCamera == null)
            {
                return;
            }

            Vector3 position = cameraStartPoint != null ? cameraStartPoint.position : ResolveCurrentCameraPosition();
            endingCamera.transform.position = position;
            SetCameraOrthographicSize(ResolveCurrentOrthographicSize());
        }

        private Vector3 ResolveCurrentCameraPosition()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                return mainCamera.transform.position;
            }

            return targetLastBoss != null
                ? targetLastBoss.transform.position + new Vector3(0f, 0f, -10f)
                : new Vector3(0f, 0f, -10f);
        }

        private Vector3 ResolveSkyPosition()
        {
            if (cameraSkyPoint != null)
            {
                return cameraSkyPoint.position;
            }

            Vector3 start = endingCamera != null ? endingCamera.transform.position : ResolveCurrentCameraPosition();
            return new Vector3(start.x + fallbackSkyOffset.x, start.y + fallbackSkyOffset.y, fallbackSkyOffset.z);
        }

        private void SetCameraOrthographicSize(float orthographicSize)
        {
            if (endingCamera == null)
            {
                return;
            }

            LensSettings lens = endingCamera.Lens;
            lens.OrthographicSize = Mathf.Max(0.01f, orthographicSize);
            endingCamera.Lens = lens;
        }

        private float ResolveCurrentOrthographicSize()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.orthographic)
            {
                return mainCamera.orthographicSize;
            }

            return endingCamera != null ? endingCamera.Lens.OrthographicSize : 6f;
        }

        private void PrepareSkyImage(float alpha)
        {
            EnsureSkyImageRenderer();
            SetSkyImageAlpha(alpha);
        }

        private void EnsureSkyImageRenderer()
        {
            if (skyImageRenderer == null && skyImageSprite != null)
            {
                var imageObject = new GameObject(skyImageObjectName);
                skyImageRenderer = imageObject.AddComponent<SpriteRenderer>();
                skyImageRenderer.sprite = skyImageSprite;
                skyImageRenderer.sortingOrder = skyImageSortingOrder;
                runtimeSkyImageCreated = true;

                Vector3 skyPosition = ResolveSkyPosition();
                imageObject.transform.position = new Vector3(skyPosition.x, skyPosition.y, 0f);
                FitSkyImageToConfiguredSize();
            }

            if (skyImageRenderer != null)
            {
                skyImageRenderer.sortingOrder = skyImageSortingOrder;

                if (runtimeSkyImageCreated)
                {
                    Vector3 skyPosition = ResolveSkyPosition();
                    skyImageRenderer.transform.position = new Vector3(skyPosition.x, skyPosition.y, 0f);
                }
            }
        }

        private void FitSkyImageToConfiguredSize()
        {
            if (skyImageRenderer == null || skyImageRenderer.sprite == null)
            {
                return;
            }

            Vector2 spriteSize = skyImageRenderer.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                return;
            }

            skyImageRenderer.transform.localScale = new Vector3(
                skyImageWorldSize.x / spriteSize.x,
                skyImageWorldSize.y / spriteSize.y,
                1f);
        }

        private void SetSkyImageAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);

            if (skyImageCanvasGroup != null)
            {
                skyImageCanvasGroup.alpha = alpha;
            }

            if (skyImageRenderer != null)
            {
                Color color = skyImageRenderer.color;
                color.a = alpha;
                skyImageRenderer.color = color;
            }
        }

        private void PlayEndingBgm()
        {
            if (endingBgm == null)
            {
                return;
            }

            if (stageBgm != null)
            {
                stageBgm.PlayTimelineBgm(endingBgm, endingBgmVolume);
                return;
            }

            global::StoryTimelineRuntime.Instance.PlayBgm(endingBgm, endingBgmVolume, true, 1f);
        }

        private IEnumerator FadeFromBlackAndStartEndingBgm()
        {
            Coroutine fadeRoutine = StartCoroutine(global::StoryOverlayFader.Instance.FadeTo(0f, fadeFromBlackDuration, Color.black));
            float waitSeconds = Mathf.Max(0f, fadeFromBlackDuration - endingBgmStartBeforeFadeEndSeconds);

            if (waitSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(waitSeconds);
            }

            if (!returningToTitle)
            {
                PlayEndingBgm();
            }

            yield return fadeRoutine;
        }

        private void StopCurrentBgmOnBlackout()
        {
            if (!stopCurrentBgmOnBlackout || IsEndingBgmAlreadyPlaying())
            {
                return;
            }

            if (stageBgm != null)
            {
                stageBgm.StopTimelineBgm(stopCurrentBgmFadeSeconds);
                return;
            }

            global::StoryTimelineRuntime.Instance.StopBgm(stopCurrentBgmFadeSeconds);
        }

        private bool IsEndingBgmAlreadyPlaying()
        {
            if (endingBgm == null)
            {
                return false;
            }

            global::StageBgmController resolvedStageBgm = stageBgm;
            if (resolvedStageBgm == null)
            {
                resolvedStageBgm = FindFirstObjectByType<global::StageBgmController>(FindObjectsInactive.Include);
            }

            return resolvedStageBgm != null && resolvedStageBgm.RequestedBgmClip == endingBgm;
        }

        private void PrepareCreditsCanvas()
        {
            if (creditsCanvasPanel != null)
            {
                return;
            }

            if (creditsCanvasPrefab != null)
            {
                creditsCanvasPanel = Instantiate(creditsCanvasPrefab);
                creditsCanvasPanel.name = creditsCanvasPrefab.name;
                runtimeCreditsCanvasCreated = true;
                return;
            }

            creditsCanvasPanel = FindFirstObjectByType<EndingCreditsCanvasController>(FindObjectsInactive.Include);
        }

        private static bool WasSpacePressedThisFrame()
        {
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        }

        private void EnableCreditsSkip()
        {
            canSkipCredits = true;
            skipInputIgnoreUntil = Time.unscaledTime + 0.25f;
        }

        private bool IsInConfiguredEndingScene()
        {
            return string.IsNullOrWhiteSpace(endingSceneName) ||
                   string.Equals(SceneManager.GetActiveScene().name, endingSceneName, System.StringComparison.Ordinal);
        }

        private static float SmootherStep(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private readonly struct BehaviourState
        {
            public BehaviourState(Behaviour behaviour, bool wasEnabled)
            {
                Behaviour = behaviour;
                WasEnabled = wasEnabled;
            }

            public Behaviour Behaviour { get; }
            public bool WasEnabled { get; }
        }

        private readonly struct CanvasGroupState
        {
            public CanvasGroupState(CanvasGroup canvasGroup, float alpha, bool interactable, bool blocksRaycasts)
            {
                CanvasGroup = canvasGroup;
                Alpha = alpha;
                Interactable = interactable;
                BlocksRaycasts = blocksRaycasts;
            }

            public CanvasGroup CanvasGroup { get; }
            public float Alpha { get; }
            public bool Interactable { get; }
            public bool BlocksRaycasts { get; }
        }

        private readonly struct AnimatorState
        {
            public AnimatorState(Animator animator, float speed)
            {
                Animator = animator;
                Speed = speed;
            }

            public Animator Animator { get; }
            public float Speed { get; }
        }

        private readonly struct SpineAnimationState
        {
            public SpineAnimationState(SkeletonAnimation skeletonAnimation, float timeScale)
            {
                SkeletonAnimation = skeletonAnimation;
                TimeScale = timeScale;
            }

            public SkeletonAnimation SkeletonAnimation { get; }
            public float TimeScale { get; }
        }
    }
}
