using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Yarn.Unity;
using Metroidvania.UI;

namespace Metroidvania.Managers
{
    public enum DialogueStyle
    {
        ADV,
        Bubble
    }

    /// <summary>
    /// 会話システムの全体管理クラス。
    /// Input Systemからの入力（Space / South Button）を受け取り、
    /// DialogueRunnerや各アクティブなViewへ進行の合図を送る。
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        [SerializeField] private DialogueRunner dialogueRunner = null!;
        [SerializeField] private InputActionReference nextAction = null!;
        
        [Header("Views")]
        [SerializeField] private DialogueView advView = null!;
        [SerializeField] private BubbleDialogueView bubbleView = null!;

        public DialogueRunner Runner => dialogueRunner;

        private int lastAdvanceFrame = -1;
        private int ignoreAnyButtonInputFrame = -1;

        private void Start()
        {
            if (dialogueRunner == null)
            {
                dialogueRunner = FindFirstObjectByType<DialogueRunner>();
            }

            if (nextAction != null)
            {
                // 入力イベントの購読
                nextAction.action.performed += OnNextPerformed;
            }
            else
            {
                Debug.LogWarning("[DialogueManager] DialogueNextアクションが設定されていません。");
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

        private void Update()
        {
            if (dialogueRunner == null || !dialogueRunner.IsDialogueRunning) return;
            if (Time.frameCount <= ignoreAnyButtonInputFrame) return;
            if (!WasAnyButtonPressedThisFrame()) return;

            AdvanceActiveDialogueViews();
        }

        private void OnDestroy()
        {
            if (nextAction != null)
            {
                nextAction.action.performed -= OnNextPerformed;
            }
        }

        private void OnNextPerformed(InputAction.CallbackContext context)
        {
            AdvanceActiveDialogueViews();
        }

        private void AdvanceActiveDialogueViews()
        {
            if (dialogueRunner == null || !dialogueRunner.IsDialogueRunning) return;
            if (lastAdvanceFrame == Time.frameCount) return;

            lastAdvanceFrame = Time.frameCount;

            if (dialogueRunner.DialoguePresenters == null)
            {
                return;
            }

            // アクティブなViewのみ進行指示を出す
            foreach (var view in dialogueRunner.DialoguePresenters)
            {
                if (view == null || view.gameObject == null || !view.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (view is DialogueView dv && dv.IsPresentationEnabled)
                {
                    dv.OnContinueClicked();
                }

                if (view is BubbleDialogueView bv && bv.IsPresentationEnabled)
                {
                    bv.OnContinueClicked();
                }
            }
        }

        private static bool WasAnyButtonPressedThisFrame()
        {
            foreach (var device in InputSystem.devices)
            {
                if (device == null || !device.enabled)
                {
                    continue;
                }

                foreach (var control in device.allControls)
                {
                    if (control is ButtonControl button && button.wasPressedThisFrame)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// スタイルを指定して会話を開始する（対象はBubble専用オプション）
        /// </summary>
        public void StartConversation(
            string nodeName,
            DialogueStyle style,
            Transform target = null,
            BubbleDialogueView.SpeakerTargetResolver speakerTargetResolver = null,
            bool speakerTargetResolverOnly = false)
        {
            if (dialogueRunner == null) return;
            if (dialogueRunner.IsDialogueRunning) dialogueRunner.Stop();

            if (advView == null) advView = FindFirstObjectByType<DialogueView>(FindObjectsInactive.Include);
            bubbleView = ResolveBubbleView();

            Debug.LogWarning(
                $"[DialogueManager] StartConversation node='{nodeName}', style={style}, " +
                $"target='{(target != null ? target.name : "null")}', " +
                $"advViewFound={advView != null}, bubbleViewFound={bubbleView != null}, frame={Time.frameCount}");

            if (style == DialogueStyle.ADV)
            {
                if (advView != null)
                {
                    advView.gameObject.SetActive(true);
                    advView.SetPresentationEnabled(true);
                }

                if (bubbleView != null)
                {
                    bubbleView.gameObject.SetActive(true);
                    bubbleView.SetSpeakerTargetResolver(null, false);
                    bubbleView.SetPresentationEnabled(false);
                }
            }
            else if (style == DialogueStyle.Bubble)
            {
                if (advView != null)
                {
                    advView.gameObject.SetActive(true);
                    advView.SetPresentationEnabled(false);
                }

                if (bubbleView != null)
                {
                    bubbleView.gameObject.SetActive(true);
                    bubbleView.SetSpeakerTargetResolver(speakerTargetResolver, speakerTargetResolverOnly);
                    bubbleView.SetPresentationEnabled(true);
                    bubbleView.SetTarget(target);
                }
            }

            ignoreAnyButtonInputFrame = Time.frameCount;
            dialogueRunner.StartDialogue(nodeName);
        }

        private BubbleDialogueView ResolveBubbleView()
        {
            BubbleDialogueView preferredView = FindPreferredBubbleView();
            if (preferredView != null)
            {
                return preferredView;
            }

            if (bubbleView != null)
            {
                return bubbleView;
            }

            return FindFirstObjectByType<BubbleDialogueView>(FindObjectsInactive.Include);
        }

        private static BubbleDialogueView FindPreferredBubbleView()
        {
            BubbleDialogueView[] views = FindObjectsByType<BubbleDialogueView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < views.Length; i++)
            {
                BubbleDialogueView view = views[i];
                if (view != null &&
                    string.Equals(view.name, "BubbleView", StringComparison.OrdinalIgnoreCase) &&
                    HasParentNamed(view.transform, "EventCanvas"))
                {
                    return view;
                }
            }

            for (int i = 0; i < views.Length; i++)
            {
                BubbleDialogueView view = views[i];
                if (view != null &&
                    string.Equals(view.name, "BubbleView", StringComparison.OrdinalIgnoreCase))
                {
                    return view;
                }
            }

            return null;
        }

        private static bool HasParentNamed(Transform transform, string parentName)
        {
            Transform current = transform;
            while (current != null)
            {
                if (string.Equals(current.name, parentName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
