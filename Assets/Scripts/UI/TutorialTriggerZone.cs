using Metroidvania.Managers;
using UnityEngine;
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
        Custom
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

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";

    private bool triggered;
    private DialogueRunner activeDialogueRunner;
    private bool waitingDialogueCompletion;

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
        EnsureTriggerCollider();
        ResolveDialogueManagerIfNeeded();

        if (IsAlreadyCompleted())
        {
            triggered = true;
            DisableIfConfigured();
        }
    }

    private void OnDisable()
    {
        UnsubscribeDialogueComplete();
    }

    private void OnDestroy()
    {
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

        ShowTutorialOverlay();
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
        if (!waitingDialogueCompletion)
        {
            return;
        }

        UnsubscribeDialogueComplete();
        ShowTutorialOverlay();
    }

    private void ShowTutorialOverlay()
    {
        if (markCompletedOnOpen)
        {
            MarkCompleted();
        }

        tutorialOverlay.ConfigureContent(promptText, gifFrames, gifFramesPerSecond, gifLoopIntervalSeconds);
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
