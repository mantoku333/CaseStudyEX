using Metroidvania.Managers;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Yarn.Unity;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class TutorialTriggerZone : MonoBehaviour
{
    private enum TutorialPreset
    {
        Attack = 0,
        Dodge,
        Parry,
        Gimmick,
        Glide,
        Gun,
        BasicMove,
        Custom
    }

    private enum TutorialCutsceneActionType
    {
        DelayRealtime = 0,
        FadeBlack = 1,
        SetActorActive = 2,
        PlaceActor = 3,
        MoveActor = 4,
        FaceActor = 5
    }

    [Serializable]
    private sealed class TutorialCutsceneAction
    {
        public TutorialCutsceneActionType actionType = TutorialCutsceneActionType.DelayRealtime;
        public float seconds = 0.25f;
        public float targetAlpha = 1f;
        public string actorId = "iris";
        public string markerId = string.Empty;
        public string direction = "right";
        public bool active = true;
    }

    [Header("Tutorial")]
    [SerializeField] private TutorialOverlayController tutorialOverlay;
    [SerializeField] private TutorialPreset preset = TutorialPreset.Attack;
    [SerializeField, HideInInspector] private TutorialPreset previousPreset = TutorialPreset.Attack;
    [SerializeField] private string completedFlagKey = GameProgressKeys.TutorialAttackShown;
    [SerializeField] private string promptText = "攻撃する";
    [SerializeField] private Sprite[] gifFrames = System.Array.Empty<Sprite>();
    [SerializeField] private float gifFramesPerSecond = 12f;
    [SerializeField] private float gifLoopIntervalSeconds = 0.5f;
    [SerializeField] private bool markCompletedOnOpen;
    [SerializeField] private bool disableAfterCompletion = true;

    [Header("Dialogue Before Tutorial")]
    [SerializeField] private bool showAfterDialogue;
    [SerializeField] private DialogueManager dialogueManager;
    [SerializeField] private string dialogueNodeName = string.Empty;
    [SerializeField] private DialogueStyle dialogueStyle = DialogueStyle.Bubble;
    [SerializeField] private Transform bubbleTarget;
    [SerializeField] private bool skipWhenDialogueAlreadyRunning = true;
    [SerializeField] private bool hidePromptTextAfterDialogue;

    [Header("Cutscene Before Tutorial")]
    [SerializeField] private bool playCutsceneBeforeTutorial;
    [SerializeField] private bool disablePlayerControlDuringCutscene = true;
    [SerializeField] private List<TutorialCutsceneAction> cutsceneActions = new List<TutorialCutsceneAction>();

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";


    [Header("Background(中江5/22)")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Image.Type backgroundImageType = Image.Type.Simple;
    [SerializeField] private bool preserveBackgroundAspect;
    [SerializeField] private Vector2 backgroundImageSize = new Vector2(800f, 450f);
    [SerializeField] private bool overrideBackgroundImageSize;
    [SerializeField] private Vector2 backgroundImagePosition;
    [SerializeField] private bool overrideBackgroundImagePosition;

    [Header("Tutorial Animation Layout")]
    [SerializeField] private Vector2 animationImageSize = new Vector2(160f, 180f);
    [SerializeField] private bool overrideAnimationImageSize = true;
    [SerializeField] private Vector2 animationImagePosition = new Vector2(-200f, -20f);
    [SerializeField] private bool overrideAnimationImagePosition = true;

    [Header("Close Button Layout")]
    [SerializeField] private Vector2 closeButtonSize = new Vector2(280f, 60f);
    [SerializeField] private bool overrideCloseButtonSize = true;
    [SerializeField] private Vector2 closeButtonPosition = new Vector2(0f, -280f);
    [SerializeField] private bool overrideCloseButtonPosition = true;

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

    private readonly List<Behaviour> cutscenePausedBehaviours = new List<Behaviour>();
    private readonly List<MonoBehaviour> cutscenePlayerBehaviourBuffer = new List<MonoBehaviour>();
    private bool triggered;
    private DialogueRunner activeDialogueRunner;
    private bool waitingDialogueCompletion;
    private Coroutine cutsceneRoutine;
    private PlayerInput cutscenePausedPlayerInput;
    private bool previousCutscenePlayerInputEnabled;
    private bool cutsceneControlPaused;
    private bool shuttingDown;

    private void Reset()
    {
        EnsureTriggerCollider();
        ApplyPresetValues();
        previousPreset = preset;
    }

    private void OnValidate()
    {
        if (preset == previousPreset)
        {
            return;
        }

        ApplyPresetValues();
        previousPreset = preset;
    }

    private void Awake()
    {
        shuttingDown = false;
        EnsureTriggerCollider();
        ResolveDialogueManagerIfNeeded();

        if (IsAlreadyCompleted())
        {
            triggered = true;
            DisableIfConfigured();
        }
    }

    private void OnEnable()
    {
        shuttingDown = false;
    }

    private void OnDisable()
    {
        shuttingDown = true;
        StopCutsceneIfRunning();
        UnsubscribeDialogueComplete();
    }

    private void OnDestroy()
    {
        shuttingDown = true;
        StopCutsceneIfRunning();
        UnsubscribeDialogueComplete();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(playerTag) && !other.CompareTag(playerTag))
        {
            return;
        }

        if (IsAlreadyCompleted())
        {
            triggered = true;
            DisableIfConfigured();
            return;
        }

        if (tutorialOverlay == null)
        {
            Debug.LogWarning($"[TutorialTriggerZone] TutorialOverlayController is missing on '{name}'.");
            return;
        }

        triggered = true;

        if (TryStartDialogueBeforeTutorial())
        {
            return;
        }

        ShowTutorialAfterCutscene();
    }

    private bool TryStartDialogueBeforeTutorial()
    {
        if (!showAfterDialogue || string.IsNullOrWhiteSpace(dialogueNodeName))
        {
            return false;
        }

        ResolveDialogueManagerIfNeeded();
        if (dialogueManager == null || dialogueManager.Runner == null)
        {
            Debug.LogWarning($"[TutorialTriggerZone] DialogueManager not found. Fallback to tutorial only. object='{name}'");
            return false;
        }

        DialogueRunner runner = dialogueManager.Runner;
        if (runner.Dialogue == null || !runner.Dialogue.NodeExists(dialogueNodeName))
        {
            Debug.LogWarning(
                $"[TutorialTriggerZone] Dialogue node not found. node='{dialogueNodeName}'. Fallback to tutorial only.");
            return false;
        }

        if (runner.IsDialogueRunning)
        {
            if (skipWhenDialogueAlreadyRunning)
            {
                Debug.LogWarning(
                    $"[TutorialTriggerZone] Dialogue already running. Skip pre-dialogue and show tutorial. object='{name}'");
                return false;
            }

            runner.Stop();
        }

        activeDialogueRunner = runner;
        waitingDialogueCompletion = true;
        activeDialogueRunner.onDialogueComplete?.AddListener(OnDialogueCompleteThenShowTutorial);

        Transform target = bubbleTarget != null ? bubbleTarget : transform;
        dialogueManager.StartConversation(dialogueNodeName, dialogueStyle, target);
        return true;
    }

    private void OnDialogueCompleteThenShowTutorial()
    {
        if (!waitingDialogueCompletion || shuttingDown || !isActiveAndEnabled)
        {
            return;
        }

        UnsubscribeDialogueComplete();
        ShowTutorialAfterCutscene();
    }

    private void ShowTutorialAfterCutscene()
    {
        if (shuttingDown || !isActiveAndEnabled)
        {
            return;
        }

        if (!playCutsceneBeforeTutorial || cutsceneActions == null || cutsceneActions.Count == 0)
        {
            ShowTutorialOverlay();
            return;
        }

        StopCutsceneIfRunning();
        cutsceneRoutine = StartCoroutine(PlayCutsceneThenShowTutorial());
    }

    private IEnumerator PlayCutsceneThenShowTutorial()
    {
        if (shuttingDown || !isActiveAndEnabled)
        {
            yield break;
        }

        PausePlayerControlForCutscene();

        try
        {
            for (int i = 0; i < cutsceneActions.Count; i++)
            {
                TutorialCutsceneAction action = cutsceneActions[i];
                if (action == null)
                {
                    continue;
                }

                yield return ExecuteCutsceneAction(action);
            }
        }
        finally
        {
            ResumePlayerControlForCutscene();
            cutsceneRoutine = null;
        }

        if (!shuttingDown && isActiveAndEnabled)
        {
            ShowTutorialOverlay();
        }
    }

    private IEnumerator ExecuteCutsceneAction(TutorialCutsceneAction action)
    {
        switch (action.actionType)
        {
            case TutorialCutsceneActionType.DelayRealtime:
                yield return ProloguePresentationRuntime.Instance.WaitRealtime(action.seconds);
                yield break;

            case TutorialCutsceneActionType.FadeBlack:
                yield return ProloguePresentationRuntime.Instance.FadeBlack(action.targetAlpha, action.seconds);
                yield break;

            case TutorialCutsceneActionType.SetActorActive:
                ProloguePresentationRuntime.Instance.SetActorActive(action.actorId, action.active);
                yield break;

            case TutorialCutsceneActionType.PlaceActor:
                ProloguePresentationRuntime.Instance.PlaceActor(action.actorId, action.markerId);
                yield break;

            case TutorialCutsceneActionType.MoveActor:
                yield return ProloguePresentationRuntime.Instance.MoveActor(action.actorId, action.markerId, action.seconds);
                yield break;

            case TutorialCutsceneActionType.FaceActor:
                ProloguePresentationRuntime.Instance.FaceActor(action.actorId, action.direction);
                yield break;
        }
    }

    private void StopCutsceneIfRunning()
    {
        if (cutsceneRoutine == null)
        {
            return;
        }

        StopCoroutine(cutsceneRoutine);
        cutsceneRoutine = null;
        ResumePlayerControlForCutscene();
    }

    private void PausePlayerControlForCutscene()
    {
        if (!disablePlayerControlDuringCutscene || cutsceneControlPaused)
        {
            return;
        }

        cutsceneControlPaused = true;
        cutscenePausedBehaviours.Clear();

        GameObject player = ResolvePlayerObject();
        if (player == null)
        {
            return;
        }

        cutscenePausedPlayerInput = player.GetComponentInChildren<PlayerInput>(includeInactive: true);
        if (cutscenePausedPlayerInput != null)
        {
            previousCutscenePlayerInputEnabled = cutscenePausedPlayerInput.enabled;
            cutscenePausedPlayerInput.enabled = false;
        }

        cutscenePlayerBehaviourBuffer.Clear();
        player.GetComponentsInChildren(true, cutscenePlayerBehaviourBuffer);
        for (int i = 0; i < cutscenePlayerBehaviourBuffer.Count; i++)
        {
            MonoBehaviour behaviour = cutscenePlayerBehaviourBuffer[i];
            if (behaviour == null || !behaviour.enabled)
            {
                continue;
            }

            if (!ShouldPauseBehaviour(behaviour.GetType().Name))
            {
                continue;
            }

            behaviour.enabled = false;
            cutscenePausedBehaviours.Add(behaviour);
        }

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private void ResumePlayerControlForCutscene()
    {
        if (!cutsceneControlPaused)
        {
            return;
        }

        cutsceneControlPaused = false;

        if (cutscenePausedPlayerInput != null)
        {
            cutscenePausedPlayerInput.enabled = previousCutscenePlayerInputEnabled;
        }

        cutscenePausedPlayerInput = null;

        for (int i = 0; i < cutscenePausedBehaviours.Count; i++)
        {
            if (cutscenePausedBehaviours[i] != null)
            {
                cutscenePausedBehaviours[i].enabled = true;
            }
        }

        cutscenePausedBehaviours.Clear();
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
        return playerController != null ? playerController.gameObject : null;
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

    private void ShowTutorialOverlay()
    {
        if (shuttingDown || !isActiveAndEnabled || tutorialOverlay == null)
        {
            return;
        }

        if (markCompletedOnOpen)
        {
            MarkCompleted();
        }

        string resolvedPromptText =
            hidePromptTextAfterDialogue && showAfterDialogue && !string.IsNullOrWhiteSpace(dialogueNodeName)
                ? string.Empty
                : promptText;

        tutorialOverlay.ConfigureContent(
            resolvedPromptText,
            gifFrames,
            gifFramesPerSecond,
            gifLoopIntervalSeconds,
            backgroundSprite,
            backgroundImageType,
            preserveBackgroundAspect,
            overrideBackgroundImageSize,
            backgroundImageSize,
            overrideBackgroundImagePosition,
            backgroundImagePosition,
            overrideAnimationImageSize,
            animationImageSize,
            overrideAnimationImagePosition,
            animationImagePosition,
            overrideCloseButtonSize,
            closeButtonSize,
            overrideCloseButtonPosition,
            closeButtonPosition);

        tutorialOverlay.Show(OnTutorialClosed);
    }

    private void OnTutorialClosed()
    {
        if (!markCompletedOnOpen)
        {
            MarkCompleted();
        }

        DisableIfConfigured();
    }

    private bool IsAlreadyCompleted()
    {
        if (string.IsNullOrWhiteSpace(completedFlagKey))
        {
            return false;
        }

        return GameProgressFlags.Get(completedFlagKey);
    }

    private void MarkCompleted()
    {
        if (string.IsNullOrWhiteSpace(completedFlagKey))
        {
            return;
        }

        GameProgressFlags.Set(completedFlagKey, true);
    }

    private void DisableIfConfigured()
    {
        if (disableAfterCompletion)
        {
            enabled = false;
        }
    }

    private void EnsureTriggerCollider()
    {
        Collider2D target = GetComponent<Collider2D>();
        if (target != null)
        {
            target.isTrigger = true;
        }
    }

    private void ResolveDialogueManagerIfNeeded()
    {
        if (dialogueManager == null)
        {
            dialogueManager = FindFirstObjectByType<DialogueManager>();
        }
    }

    private void ApplyPresetValues()
    {
        switch (preset)
        {
            case TutorialPreset.Attack:
                completedFlagKey = GameProgressKeys.TutorialAttackShown;
                promptText = "攻撃する";
                dialogueNodeName = "Tutorial_Attack";
                break;

            case TutorialPreset.Dodge:
                completedFlagKey = GameProgressKeys.TutorialDodgeShown;
                promptText = "回避する";
                dialogueNodeName = "Tutorial_Dodge";
                break;

            case TutorialPreset.Parry:
                completedFlagKey = GameProgressKeys.TutorialParryShown;
                promptText = "傘でパリィする";
                dialogueNodeName = "Tutorial_Parry";
                break;

            case TutorialPreset.Gimmick:
                completedFlagKey = GameProgressKeys.TutorialGimmickShown;
                promptText = "ギミックを動かす";
                dialogueNodeName = "Tutorial_Gimmick";
                break;

            case TutorialPreset.Glide:
                completedFlagKey = GameProgressKeys.TutorialGlideShown;
                promptText = "傘を開いて滑空する";
                dialogueNodeName = "Tutorial_Glide";
                break;

            case TutorialPreset.Gun:
                completedFlagKey = GameProgressKeys.TutorialGlideShown;
                promptText = "銃で反動する";
                dialogueNodeName = "Tutorial_Gun";
                break;

            case TutorialPreset.BasicMove:
                completedFlagKey = GameProgressKeys.TutorialGlideShown;
                promptText = "動く";
                dialogueNodeName = "Tutorial_BasicMove";
                break;
        }
    }

    private void UnsubscribeDialogueComplete()
    {
        if (activeDialogueRunner != null)
        {
            activeDialogueRunner.onDialogueComplete?.RemoveListener(OnDialogueCompleteThenShowTutorial);
            activeDialogueRunner = null;
        }

        waitingDialogueCompletion = false;
    }
}
