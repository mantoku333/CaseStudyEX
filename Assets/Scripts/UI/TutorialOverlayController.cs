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
    private PlayerInput pausedPlayerInput;
    private bool previousPlayerInputEnabled;
    private float previousTimeScale = 1f;
    private bool gameplayPaused;
    private bool timeScalePaused;
    private Action onClosed;
    private bool capturedPrePlayPanelState;
    private bool prePlayPanelActive;
    private bool hidingInternal;
    private int currentGifFrameIndex;
    private float gifFrameTimer;
    private float gifLoopIntervalTimer;
    private Image resolvedGifImage;

    public void ConfigureContent(string text, Sprite[] frames, float framesPerSecond, float loopIntervalSeconds)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            promptText = text;
        }

        if (frames != null && frames.Length > 0)
        {
            gifFrames = frames;
        }

        if (framesPerSecond > 0f)
        {
            gifFramesPerSecond = framesPerSecond;
        }

        if (loopIntervalSeconds >= 0f)
        {
            gifLoopIntervalSeconds = loopIntervalSeconds;
        }
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

        MonoBehaviour[] behaviours = player.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
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

        PlayerInput playerInput = FindFirstObjectByType<PlayerInput>();
        if (playerInput != null)
        {
            return playerInput;
        }

        return null;
    }

    private GameObject ResolvePlayerObject()
    {
        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
            if (taggedPlayer != null)
            {
                return taggedPlayer;
            }
        }

        global::PlayerController playerController = FindFirstObjectByType<global::PlayerController>();
        if (playerController != null)
        {
            return playerController.gameObject;
        }

        return pausedPlayerInput != null ? pausedPlayerInput.gameObject : null;
    }
}
