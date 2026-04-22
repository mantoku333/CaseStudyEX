using UnityEngine;
using Yarn.Unity;

namespace Metroidvania.UI
{
    /// <summary>
    /// オブジェクトに近づき、インタラクトしたときに会話を開始する。
    /// （Player側に別途Input SystemのInteractキーなどの検知がある想定だが、
    /// ここでは簡易の判定を持つ）
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DialogueTrigger : MonoBehaviour
    {
        [Header("Yarn Spinner Settings")]
        [Tooltip("実行するYarnのNode名")]
        [SerializeField] private string targetNode = "Start";
        
        [Tooltip("会話の表示形式（画面下部パネル か キャラ頭上吹き出し）")]
        [SerializeField] private Metroidvania.Managers.DialogueStyle dialogueStyle = Metroidvania.Managers.DialogueStyle.Bubble;
        
        [Tooltip("Bubble形式の場合に吹き出しを追従させる対象（未設定ならこのオブジェクトの頭上に出ます）")]
        [SerializeField] private Transform bubbleTarget;
        
        [Tooltip("DialogueManagerの参照（未設定なら自動取得）")]
        [SerializeField] private Metroidvania.Managers.DialogueManager dialogueManager = null!;

        [Header("Trigger Behavior")]
        [Tooltip("True: トリガー範囲に入ったら自動で開始 / False: 手動で開始処理を呼ぶ")]
        [SerializeField] private bool autoStartOnEnter = true;
        [SerializeField] private bool destroyAfterDialogue = false;

        private bool _isPlayerInRange = false;

        private void Start()
        {
            if (dialogueManager == null)
            {
                dialogueManager = FindFirstObjectByType<Metroidvania.Managers.DialogueManager>();
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Player"))
            {
                _isPlayerInRange = true;
                
                if (autoStartOnEnter)
                {
                    StartDialogue();
                }
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.CompareTag("Player"))
            {
                _isPlayerInRange = false;
            }
        }

        /// <summary>
        /// 外部（Inputなど）から手動で呼び出して会話を開始する用
        /// </summary>
        public void Interact()
        {
            if (_isPlayerInRange && dialogueManager != null && dialogueManager.Runner != null && !dialogueManager.Runner.IsDialogueRunning)
            {
                StartDialogue();
            }
        }

        private void StartDialogue()
        {
            if (dialogueManager == null) return;
            
            if (dialogueManager.Runner != null && !dialogueManager.Runner.IsDialogueRunning)
            {
                Transform target = bubbleTarget != null ? bubbleTarget : transform;
                dialogueManager.StartConversation(targetNode, dialogueStyle, target);
                
                if (destroyAfterDialogue)
                {
                    dialogueManager.Runner.onDialogueComplete?.AddListener(OnDialogueCompleteAndDestroy);
                }
            }
        }

        private void OnDialogueCompleteAndDestroy()
        {
            if (dialogueManager != null && dialogueManager.Runner != null)
            {
                dialogueManager.Runner.onDialogueComplete?.RemoveListener(OnDialogueCompleteAndDestroy);
            }
            Destroy(gameObject);
        }
    }
}
