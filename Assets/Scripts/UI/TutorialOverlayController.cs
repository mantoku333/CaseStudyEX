using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TutorialOverlayController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject viewRoot;
    [SerializeField] private bool hideViewRootWhenClosed = true;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;
    [FormerlySerializedAs("keyLabel")]
    [SerializeField] private TMP_Text promptLabel;
    [SerializeField] private string promptText = "攻撃する";
    [SerializeField] private Image gifImage;
    [SerializeField] private Sprite[] gifFrames = Array.Empty<Sprite>();
    [SerializeField] private float gifFramesPerSecond = 12f;
    [SerializeField] private float gifLoopIntervalSeconds = 0.5f;
    [SerializeField] private Animator loopAnimator;
    [SerializeField] private bool forceAnimatorUnscaledTime = true;

    [Header("Pause")]
    [SerializeField] private StoryPausePolicy pausePolicy = StoryPausePolicy.TimeScaleZero;
    [SerializeField] private string playerTag = "Player";

    //中江5/22
    [SerializeField] private Image backgroundImage;

    private Sprite backgroundSprite;
    private Image.Type backgroundImageType = Image.Type.Simple;
    private bool backgroundPreserveAspect;
    private bool overrideBackgroundImageSize;
    private Vector2 backgroundImageSize;
    private bool overrideBackgroundImagePosition;
    private Vector2 backgroundImagePosition;
    private bool overrideAnimationImageSize;
    private Vector2 animationImageSize;
    private bool overrideAnimationImagePosition;
    private Vector2 animationImagePosition;
    private bool overrideCloseButtonSize;
    private Vector2 closeButtonSize;
    private bool overrideCloseButtonPosition;
    private Vector2 closeButtonPosition;

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

    private readonly List<Behaviour> pausedBehaviours = new List<Behaviour>();
    private readonly List<MonoBehaviour> playerBehaviourBuffer = new List<MonoBehaviour>();
    private PlayerInput pausedPlayerInput;
    private bool previousPlayerInputEnabled;
    private float previousTimeScale = 1f;
    private bool gameplayPaused;
    private bool timeScalePaused;
    private Action onClosed;
    private bool capturedPrePlayPanelState;
    private bool prePlayPanelActive;
    private bool hidingInternal;
    private bool destroying;
    private int currentGifFrameIndex;
    private float gifFrameTimer;
    private float gifLoopIntervalTimer;
    private Image resolvedGifImage;
    private Image resolvedBackgroundImage;
    private RectTransformSnapshot backgroundDefaultRect;
    private RectTransformSnapshot animationDefaultRect;
    private RectTransformSnapshot closeButtonDefaultRect;

    private struct RectTransformSnapshot
    {
        public bool Captured;
        public Vector2 SizeDelta;
        public Vector2 AnchoredPosition;
        public Vector3 LocalScale;
    }

    public void ConfigureContent(
    string text,
    Sprite[] frames,
    float framesPerSecond,
    float loopIntervalSeconds,
    Sprite background,
    Image.Type backgroundType,
    bool preserveBackgroundAspect,
    bool overrideBackgroundSize,
    Vector2 backgroundSize,
    bool overrideBackgroundPosition,
    Vector2 backgroundPosition,
    bool overrideAnimationSize,
    Vector2 animationSize,
    bool overrideAnimationPosition,
    Vector2 animationPosition,
    bool overrideButtonSize,
    Vector2 buttonSize,
    bool overrideButtonPosition,
    Vector2 buttonPosition
)
    {
        promptText = text ?? string.Empty;
        gifFrames = frames ?? Array.Empty<Sprite>();

        if (framesPerSecond > 0f)
        {
            gifFramesPerSecond = framesPerSecond;
        }

        if (loopIntervalSeconds >= 0f)
        {
            gifLoopIntervalSeconds = loopIntervalSeconds;
        }

        backgroundSprite = background;
        backgroundImageType = backgroundType;
        backgroundPreserveAspect = preserveBackgroundAspect;
        overrideBackgroundImageSize = overrideBackgroundSize;
        backgroundImageSize = backgroundSize;
        overrideBackgroundImagePosition = overrideBackgroundPosition;
        backgroundImagePosition = backgroundPosition;
        overrideAnimationImageSize = overrideAnimationSize;
        animationImageSize = animationSize;
        overrideAnimationImagePosition = overrideAnimationPosition;
        animationImagePosition = animationPosition;
        overrideCloseButtonSize = overrideButtonSize;
        closeButtonSize = buttonSize;
        overrideCloseButtonPosition = overrideButtonPosition;
        closeButtonPosition = buttonPosition;
    }

    private void Awake()
    {
        if (Application.isPlaying && panelRoot != null)
        {
            capturedPrePlayPanelState = true;
            prePlayPanelActive = panelRoot.activeSelf;
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HandleCloseButtonClicked);
        }
    }

    private void OnDisable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(HandleCloseButtonClicked);
        }

        if (!Application.isPlaying)
        {
            RestorePanelStateForEditor();
            return;
        }

        HideInternal(invokeCallback: false);
    }

    private void OnDestroy()
    {
        destroying = true;

        if (!Application.isPlaying)
        {
            return;
        }

        HideInternal(invokeCallback: false);
    }

    private void RestorePanelStateForEditor()
    {
        if (!capturedPrePlayPanelState || panelRoot == null)
        {
            return;
        }

        panelRoot.SetActive(prePlayPanelActive);
        capturedPrePlayPanelState = false;
    }

    public void Show(Action closeCallback = null)
    {
        if (destroying || this == null)
        {
            return;
        }

        GameObject root = ResolveViewRoot();
        if (root != null && !root.activeSelf)
        {
            root.SetActive(true);
        }

        onClosed = closeCallback;

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        RefreshPromptText();
        RefreshBackgroundImage();
        RefreshTutorialLayout();
        RestartGifAnimation();
        RestartLoopAnimation();
        PauseGame();
    }

    private void Update()
    {
        UpdateGifAnimation();
    }

    public void HideWithoutCallback()
    {
        HideInternal(invokeCallback: false);
    }

    private void HandleCloseButtonClicked()
    {
        HideInternal(invokeCallback: true);
    }

    private void HideInternal(bool invokeCallback)
    {
        if (hidingInternal)
        {
            return;
        }

        hidingInternal = true;

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        ResumeGame();

        Action callback = onClosed;
        onClosed = null;

        if (invokeCallback)
        {
            callback?.Invoke();
        }

        GameObject root = ResolveViewRoot();
        if (hideViewRootWhenClosed && root != null && root.activeSelf)
        {
            root.SetActive(false);
        }

        hidingInternal = false;
    }

    private GameObject ResolveViewRoot()
    {
        if (destroying || this == null)
        {
            return null;
        }

        return viewRoot != null ? viewRoot : gameObject;
    }

    private void RestartLoopAnimation()
    {
        if (loopAnimator == null)
        {
            return;
        }

        if (!loopAnimator.isActiveAndEnabled)
        {
            return;
        }

        if (forceAnimatorUnscaledTime)
        {
            loopAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        loopAnimator.Rebind();
        loopAnimator.Update(0f);
    }

    private void RefreshPromptText()
    {
        if (promptLabel == null)
        {
            return;
        }

        promptLabel.text = promptText;
    }

    private void RestartGifAnimation()
    {
        currentGifFrameIndex = 0;
        gifFrameTimer = 0f;
        gifLoopIntervalTimer = 0f;

        Image targetImage = ResolveGifImage();
        if (targetImage == null)
        {
            if (gifFrames != null && gifFrames.Length > 0)
            {
                Debug.LogWarning("[TutorialOverlayController] Gif Image is missing.", this);
            }

            return;
        }

        if (gifFrames == null || gifFrames.Length == 0)
        {
            targetImage.enabled = false;
            targetImage.sprite = null;
            return;
        }

        if (gifFrames.Length == 1)
        {
            Debug.LogWarning("[TutorialOverlayController] Gif Frames has only one sprite, so it will not animate.", this);
        }

        targetImage.enabled = true;
        targetImage.sprite = gifFrames[0];
    }

    private void UpdateGifAnimation()
    {
        Image targetImage = ResolveGifImage();
        if (targetImage == null || !targetImage.enabled || gifFrames == null || gifFrames.Length <= 1)
        {
            return;
        }

        float frameDuration = 1f / Mathf.Max(1f, gifFramesPerSecond);
        if (gifLoopIntervalTimer > 0f)
        {
            gifLoopIntervalTimer = Mathf.Max(0f, gifLoopIntervalTimer - Time.unscaledDeltaTime);
            return;
        }

        gifFrameTimer += Time.unscaledDeltaTime;
        while (gifFrameTimer >= frameDuration)
        {
            gifFrameTimer -= frameDuration;
            currentGifFrameIndex++;
            if (currentGifFrameIndex >= gifFrames.Length)
            {
                currentGifFrameIndex = 0;
                gifLoopIntervalTimer = gifLoopIntervalSeconds;
            }

            targetImage.sprite = gifFrames[currentGifFrameIndex];

            if (gifLoopIntervalTimer > 0f)
            {
                gifFrameTimer = 0f;
                break;
            }
        }
    }

    private Image ResolveGifImage()
    {
        if (gifImage != null)
        {
            resolvedGifImage = gifImage;
            return resolvedGifImage;
        }

        if (resolvedGifImage != null)
        {
            return resolvedGifImage;
        }

        Image[] images = GetComponentsInChildren<Image>(includeInactive: true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && string.Equals(image.name, "Image", StringComparison.OrdinalIgnoreCase))
            {
                resolvedGifImage = image;
                return resolvedGifImage;
            }
        }

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            if (closeButton != null && closeButton.targetGraphic == image)
            {
                continue;
            }

            if (string.Equals(image.name, "Bck", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            resolvedGifImage = image;
            return resolvedGifImage;
        }

        return null;
    }

    private void PauseGame()
    {
        StoryPausePolicy resolvedPolicy = ResolvePausePolicy();

        switch (resolvedPolicy)
        {
            case StoryPausePolicy.None:
                return;

            case StoryPausePolicy.GameplayOnly:
                PauseGameplay();
                return;

            case StoryPausePolicy.TimeScaleZero:
                PauseGameplay();
                if (!timeScalePaused)
                {
                    timeScalePaused = true;
                    HitStopController.BeginExternalPause();
                    previousTimeScale = Time.timeScale;
                    Time.timeScale = 0f;
                }
                return;

            default:
                return;
        }
    }

    private StoryPausePolicy ResolvePausePolicy()
    {
        StoryPausePolicy policy = pausePolicy;
        if (policy == StoryPausePolicy.UseDialogueDefault)
        {
            policy = StoryPauseRuntime.DialogueDefaultPolicy;
        }

        return policy == StoryPausePolicy.UseDialogueDefault ? StoryPausePolicy.TimeScaleZero : policy;
    }

    private void ResumeGame()
    {
        if (timeScalePaused)
        {
            timeScalePaused = false;
            Time.timeScale = previousTimeScale;
            HitStopController.EndExternalPause();
        }

        ResumeGameplay();
    }

    private void PauseGameplay()
    {
        if (gameplayPaused)
        {
            return;
        }

        gameplayPaused = true;
        pausedBehaviours.Clear();

        pausedPlayerInput = ResolvePlayerInput();
        if (pausedPlayerInput != null)
        {
            previousPlayerInputEnabled = pausedPlayerInput.enabled;
            pausedPlayerInput.enabled = false;
        }

        GameObject player = ResolvePlayerObject();
        if (player == null)
        {
            return;
        }

        playerBehaviourBuffer.Clear();
        player.GetComponentsInChildren(true, playerBehaviourBuffer);
        for (int i = 0; i < playerBehaviourBuffer.Count; i++)
        {
            MonoBehaviour behaviour = playerBehaviourBuffer[i];
            if (behaviour == null || !behaviour.enabled)
            {
                continue;
            }

            if (!ShouldPauseBehaviour(behaviour.GetType().Name))
            {
                continue;
            }

            behaviour.enabled = false;
            pausedBehaviours.Add(behaviour);
        }

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void ResumeGameplay()
    {
        if (!gameplayPaused)
        {
            return;
        }

        gameplayPaused = false;

        if (pausedPlayerInput != null)
        {
            pausedPlayerInput.enabled = previousPlayerInputEnabled;
        }

        for (int i = 0; i < pausedBehaviours.Count; i++)
        {
            if (pausedBehaviours[i] != null)
            {
                pausedBehaviours[i].enabled = true;
            }
        }

        pausedBehaviours.Clear();
    }

    private static bool ShouldPauseBehaviour(string typeName)
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

    private PlayerInput ResolvePlayerInput()
    {
        GameObject player = ResolvePlayerObject();
        if (player != null)
        {
            PlayerInput playerInputOnPlayer = player.GetComponentInChildren<PlayerInput>(includeInactive: true);
            if (playerInputOnPlayer != null)
            {
                return playerInputOnPlayer;
            }
        }

        return null;
    }

    private GameObject ResolvePlayerObject()
    {
        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            GameObject taggedPlayer = global::PlayerReferenceCache.GetGameObject(playerTag);
            if (taggedPlayer != null)
            {
                return taggedPlayer;
            }
        }

        global::PlayerController playerController = global::PlayerReferenceCache.GetController();
        if (playerController != null)
        {
            return playerController.gameObject;
        }

        return pausedPlayerInput != null ? pausedPlayerInput.gameObject : null;
    }




    //中江5/22
    private void RefreshBackgroundImage()
    {
        Image targetImage = ResolveBackgroundImage();
        if (targetImage == null)
        {
            return;
        }

        CaptureRectTransform(targetImage.rectTransform, ref backgroundDefaultRect);

        bool hasBackground = backgroundSprite != null;
        targetImage.gameObject.SetActive(hasBackground);
        targetImage.sprite = backgroundSprite;
        targetImage.enabled = hasBackground;
        targetImage.color = Color.white;
        targetImage.type = backgroundImageType;
        targetImage.preserveAspect = backgroundPreserveAspect;

        if (!hasBackground)
        {
            return;
        }

        if (overrideBackgroundImageSize)
        {
            ApplyRectTransformSize(targetImage.rectTransform, backgroundImageSize, resetScale: true);
        }
        else
        {
            RestoreRectTransform(targetImage.rectTransform, ref backgroundDefaultRect, restoreSize: true, restorePosition: false);
        }

        if (overrideBackgroundImagePosition)
        {
            targetImage.rectTransform.anchoredPosition = backgroundImagePosition;
        }
        else
        {
            RestoreRectTransform(targetImage.rectTransform, ref backgroundDefaultRect, restoreSize: false, restorePosition: true);
        }
    }

    private Image ResolveBackgroundImage()
    {
        if (backgroundImage != null)
        {
            resolvedBackgroundImage = backgroundImage;
            return resolvedBackgroundImage;
        }

        if (resolvedBackgroundImage != null)
        {
            return resolvedBackgroundImage;
        }

        Image[] images = GetComponentsInChildren<Image>(includeInactive: true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            if (string.Equals(image.name, "EXbackground", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(image.name, "TutorialBackground", StringComparison.OrdinalIgnoreCase))
            {
                resolvedBackgroundImage = image;
                return resolvedBackgroundImage;
            }
        }

        return null;
    }

    private void RefreshTutorialLayout()
    {
        Image targetImage = ResolveGifImage();
        if (targetImage != null)
        {
            RectTransform rectTransform = targetImage.rectTransform;
            CaptureRectTransform(rectTransform, ref animationDefaultRect);
            if (overrideAnimationImageSize)
            {
                ApplyRectTransformSize(rectTransform, animationImageSize, resetScale: true);
            }
            else
            {
                RestoreRectTransform(rectTransform, ref animationDefaultRect, restoreSize: true, restorePosition: false);
            }

            if (overrideAnimationImagePosition)
            {
                rectTransform.anchoredPosition = animationImagePosition;
            }
            else
            {
                RestoreRectTransform(rectTransform, ref animationDefaultRect, restoreSize: false, restorePosition: true);
            }
        }

        if (closeButton == null)
        {
            return;
        }

        RectTransform closeButtonTransform = closeButton.GetComponent<RectTransform>();
        if (closeButtonTransform == null)
        {
            return;
        }

        CaptureRectTransform(closeButtonTransform, ref closeButtonDefaultRect);

        if (overrideCloseButtonSize)
        {
            ApplyRectTransformSize(closeButtonTransform, closeButtonSize, resetScale: true);
        }
        else
        {
            RestoreRectTransform(closeButtonTransform, ref closeButtonDefaultRect, restoreSize: true, restorePosition: false);
        }

        if (overrideCloseButtonPosition)
        {
            closeButtonTransform.anchoredPosition = closeButtonPosition;
        }
        else
        {
            RestoreRectTransform(closeButtonTransform, ref closeButtonDefaultRect, restoreSize: false, restorePosition: true);
        }
    }

    private static void CaptureRectTransform(RectTransform rectTransform, ref RectTransformSnapshot snapshot)
    {
        if (rectTransform == null || snapshot.Captured)
        {
            return;
        }

        snapshot.Captured = true;
        snapshot.SizeDelta = rectTransform.sizeDelta;
        snapshot.AnchoredPosition = rectTransform.anchoredPosition;
        snapshot.LocalScale = rectTransform.localScale;
    }

    private static void ApplyRectTransformSize(RectTransform rectTransform, Vector2 size, bool resetScale)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.sizeDelta = size;
        if (resetScale)
        {
            rectTransform.localScale = Vector3.one;
        }
    }

    private static void RestoreRectTransform(
        RectTransform rectTransform,
        ref RectTransformSnapshot snapshot,
        bool restoreSize,
        bool restorePosition)
    {
        if (rectTransform == null)
        {
            return;
        }

        if (!snapshot.Captured)
        {
            snapshot.Captured = true;
            snapshot.SizeDelta = rectTransform.sizeDelta;
            snapshot.AnchoredPosition = rectTransform.anchoredPosition;
            snapshot.LocalScale = rectTransform.localScale;
        }

        if (restoreSize)
        {
            rectTransform.sizeDelta = snapshot.SizeDelta;
            rectTransform.localScale = snapshot.LocalScale;
        }

        if (restorePosition)
        {
            rectTransform.anchoredPosition = snapshot.AnchoredPosition;
        }
    }
}
