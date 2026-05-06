using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TutorialOverlayController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text keyLabel;
    [SerializeField] private string inputActionName = "Attack";
    [SerializeField] private string fallbackLabel = "Attack";
    [SerializeField] private Animator loopAnimator;
    [SerializeField] private bool forceAnimatorUnscaledTime = true;

    [Header("Binding Groups")]
    [SerializeField] private string keyboardMouseGroup = "Keyboard&Mouse";
    [SerializeField] private string gamepadGroup = "Gamepad";
    [SerializeField] private string keyboardPreferredPathPrefix = "<Keyboard>/";
    [SerializeField] private string gamepadPreferredPathPrefix = "<Gamepad>/";

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
        onClosed = closeCallback;

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        RefreshKeyLabel();
        RestartLoopAnimation();
        PauseGame();
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

    private void RefreshKeyLabel()
    {
        if (keyLabel == null)
        {
            return;
        }

        keyLabel.text = ResolveBindingLabel();
    }

    private string ResolveBindingLabel()
    {
        PlayerInput playerInput = ResolvePlayerInput();
        if (playerInput == null || playerInput.actions == null || string.IsNullOrWhiteSpace(inputActionName))
        {
            return fallbackLabel;
        }

        InputActionAsset actions = playerInput.actions;
        PlayerInputBindingOverrides.EnsureOverridesLoaded(actions);

        bool preferGamepad = IsGamepadControlScheme(playerInput.currentControlScheme);
        if (TryResolveBindingLabel(actions, preferGamepad, out string label))
        {
            return label;
        }

        if (TryResolveBindingLabel(actions, !preferGamepad, out label))
        {
            return label;
        }

        return fallbackLabel;
    }

    private bool TryResolveBindingLabel(InputActionAsset actions, bool useGamepadGroup, out string label)
    {
        label = string.Empty;

        string group = useGamepadGroup ? gamepadGroup : keyboardMouseGroup;
        string preferredPrefix = useGamepadGroup ? gamepadPreferredPathPrefix : keyboardPreferredPathPrefix;
        if (string.IsNullOrWhiteSpace(group))
        {
            return false;
        }

        if (!PlayerInputBindingOverrides.TryGetBindingEffectivePath(
                actions,
                inputActionName,
                group,
                out string effectivePath,
                preferredPrefix))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(effectivePath))
        {
            return false;
        }

        string humanReadable = InputControlPath.ToHumanReadableString(
            effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice);

        label = string.IsNullOrWhiteSpace(humanReadable) ? effectivePath : humanReadable;
        return true;
    }

    private static bool IsGamepadControlScheme(string controlScheme)
    {
        if (string.IsNullOrWhiteSpace(controlScheme))
        {
            return false;
        }

        return controlScheme.IndexOf("gamepad", StringComparison.OrdinalIgnoreCase) >= 0;
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
