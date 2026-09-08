using System;
using Metroidvania.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;

namespace Metroidvania.Managers
{
    public enum DialogueStyle
    {
        ADV,
        Bubble
    }

    /// <summary>
    /// Owns the single story dialogue runner and routes progression input to
    /// the fixed-screen ADV presenter. The legacy style arguments remain
    /// temporarily so existing Timeline assets can migrate without data loss.
    /// </summary>
    public sealed class DialogueManager : MonoBehaviour
    {
        [SerializeField] private DialogueRunner dialogueRunner = null!;
        [SerializeField] private InputActionReference nextAction = null!;

        [Header("Presentation")]
        [SerializeField] private DialogueView advView = null!;

        [Header("Legacy (migration only)")]
        [SerializeField] private BubbleDialogueView bubbleView = null!;

        private DialogueView subscribedAdvView;

        public DialogueRunner Runner => dialogueRunner;

        private void Awake()
        {
            // Screen-space canvases authored by migration can be serialized
            // with a zero transform scale. Restore it before any overlay UI is used.
            EnsureCanvasHasVisibleScale(transform);
        }

        private void Start()
        {
            ResolveReferences();

            if (nextAction != null)
            {
                nextAction.action.performed -= OnNextPerformed;
                nextAction.action.performed += OnNextPerformed;
            }
            else
            {
                Debug.LogWarning("[DialogueManager] DialogueNext action is not assigned. Pointer input remains available.", this);
            }
        }

        private void OnEnable()
        {
            if (nextAction != null)
            {
                nextAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (nextAction != null)
            {
                nextAction.action.Disable();
            }
        }

        private void OnDestroy()
        {
            if (nextAction != null)
            {
                nextAction.action.performed -= OnNextPerformed;
            }

            SubscribeToAdvView(null);
        }

        private void OnNextPerformed(InputAction.CallbackContext context)
        {
            AdvanceActiveDialogue();
        }

        private void AdvanceActiveDialogue()
        {
            if (dialogueRunner == null || !dialogueRunner.IsDialogueRunning)
            {
                return;
            }

            ResolveReferences();
            advView?.OnContinueClicked();
        }

        /// <summary>
        /// Starts a conversation through the fixed ADV presenter. Style and
        /// bubble-target arguments are intentionally ignored during the
        /// compatibility window and will be removed after asset migration.
        /// </summary>
        public void StartConversation(
            string nodeName,
            DialogueStyle style,
            Transform target = null,
            BubbleDialogueView.SpeakerTargetResolver speakerTargetResolver = null,
            bool speakerTargetResolverOnly = false)
        {
            ResolveReferences();
            if (dialogueRunner == null)
            {
                Debug.LogError("[DialogueManager] DialogueRunner was not found.", this);
                return;
            }

            if (dialogueRunner.IsDialogueRunning)
            {
                dialogueRunner.Stop();
            }

            if (advView == null)
            {
                Debug.LogError(
                    $"[DialogueManager] ADV DialogueView was not found. node='{nodeName}'",
                    this);
                return;
            }

            EnsureCanvasHasVisibleScale(advView.transform);
            advView.gameObject.SetActive(true);
            advView.SetPresentationEnabled(true);
            advView.PrepareConversation(nodeName);
            dialogueRunner.DialoguePresenters = new DialoguePresenterBase[] { advView };

            DisableLegacyBubbleViews();

            Debug.Log(
                $"[DialogueManager] StartConversation node='{nodeName}', requestedStyle={style}, presentation=ADV.",
                this);
            dialogueRunner.StartDialogue(nodeName);
        }

        private void ResolveReferences()
        {
            if (dialogueRunner == null)
            {
                dialogueRunner = FindFirstObjectByType<DialogueRunner>(FindObjectsInactive.Include);
            }

            if (advView == null)
            {
                advView = FindFirstObjectByType<DialogueView>(FindObjectsInactive.Include);
            }

            SubscribeToAdvView(advView);
        }

        private void SubscribeToAdvView(DialogueView view)
        {
            if (subscribedAdvView == view)
            {
                return;
            }

            if (subscribedAdvView != null)
            {
                subscribedAdvView.SkipRequested -= OnSkipRequested;
            }

            subscribedAdvView = view;
            if (subscribedAdvView != null)
            {
                subscribedAdvView.SkipRequested += OnSkipRequested;
            }
        }

        private void OnSkipRequested()
        {
            StoryEventController[] controllers = FindObjectsByType<StoryEventController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < controllers.Length; i++)
            {
                StoryEventController controller = controllers[i];
                if (controller != null && controller.IsPlaying && controller.SkipEventAndComplete())
                {
                    return;
                }
            }

            if (dialogueRunner != null && dialogueRunner.IsDialogueRunning)
            {
                dialogueRunner.Stop();
            }
        }

        private static void DisableLegacyBubbleViews()
        {
            BubbleDialogueView[] views = FindObjectsByType<BubbleDialogueView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < views.Length; i++)
            {
                BubbleDialogueView view = views[i];
                if (view == null)
                {
                    continue;
                }

                view.SetSpeakerTargetResolver(null, false);
                view.SetPresentationEnabled(false);
            }
        }

        private static void EnsureCanvasHasVisibleScale(Transform viewTransform)
        {
            if (viewTransform == null)
            {
                return;
            }

            Canvas parentCanvas = viewTransform.GetComponentInParent<Canvas>(true);
            Transform canvasTransform = parentCanvas != null ? parentCanvas.transform : null;
            if (canvasTransform == null || canvasTransform.localScale.sqrMagnitude > 0.000001f)
            {
                return;
            }

            canvasTransform.localScale = Vector3.one;
        }
    }
}
