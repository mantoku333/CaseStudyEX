using Metroidvania.Managers;
using UnityEngine;
using Yarn.Unity;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class TutorialTriggerZone : MonoBehaviour
{
    [Header("Tutorial")]
    [SerializeField] private TutorialOverlayController tutorialOverlay;
    [SerializeField] private string completedFlagKey = GameProgressKeys.TutorialAttackShown;
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
