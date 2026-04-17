using UnityEngine;
using UnityEngine.InputSystem;
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

        private void OnDestroy()
        {
            if (nextAction != null)
            {
                nextAction.action.performed -= OnNextPerformed;
            }
        }

        private void OnNextPerformed(InputAction.CallbackContext context)
        {
            if (dialogueRunner == null || !dialogueRunner.IsDialogueRunning) return;

            // アクティブなViewのみ進行指示を出す
            foreach (var view in dialogueRunner.DialoguePresenters)
            {
                if (view.gameObject.activeInHierarchy)
                {
                    if (view is DialogueView dv) dv.OnContinueClicked();
                    if (view is BubbleDialogueView bv) bv.OnContinueClicked();
                }
            }
        }

        /// <summary>
        /// スタイルを指定して会話を開始する（対象はBubble専用オプション）
        /// </summary>
        public void StartConversation(string nodeName, DialogueStyle style, Transform target = null)
        {
            if (dialogueRunner == null) return;
            if (dialogueRunner.IsDialogueRunning) dialogueRunner.Stop();

            if (advView == null) advView = FindFirstObjectByType<DialogueView>(FindObjectsInactive.Include);
            if (bubbleView == null) bubbleView = FindFirstObjectByType<BubbleDialogueView>(FindObjectsInactive.Include);

            Debug.LogWarning(
                $"[DialogueManager] StartConversation node='{nodeName}', style={style}, " +
                $"target='{(target != null ? target.name : "null")}', " +
                $"advViewFound={advView != null}, bubbleViewFound={bubbleView != null}, frame={Time.frameCount}");

            if (style == DialogueStyle.ADV)
            {
                if (advView != null) advView.gameObject.SetActive(true);
                if (bubbleView != null) bubbleView.gameObject.SetActive(false);
            }
            else if (style == DialogueStyle.Bubble)
            {
                if (advView != null) advView.gameObject.SetActive(false);
                if (bubbleView != null)
                {
                    bubbleView.gameObject.SetActive(true);
                    bubbleView.SetTarget(target);
                }
            }

            dialogueRunner.StartDialogue(nodeName);
        }
    }
}
