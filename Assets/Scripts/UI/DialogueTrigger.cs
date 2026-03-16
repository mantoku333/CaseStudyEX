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
        
        [Tooltip("DialogueRunnerの参照（未設定なら自動取得）")]
        [SerializeField] private DialogueRunner dialogueRunner = null!;

        [Header("Trigger Behavior")]
        [Tooltip("True: トリガー範囲に入ったら自動で開始 / False: 手動で開始処理を呼ぶ")]
        [SerializeField] private bool autoStartOnEnter = true;
        [SerializeField] private bool destroyAfterDialogue = false;

        private bool _isPlayerInRange = false;

        private void Start()
        {
            if (dialogueRunner == null)
            {
                dialogueRunner = FindFirstObjectByType<DialogueRunner>();
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
            if (_isPlayerInRange && dialogueRunner != null && !dialogueRunner.IsDialogueRunning)
            {
                StartDialogue();
            }
        }

        private void StartDialogue()
        {
            if (dialogueRunner == null) return;
            
            if (!dialogueRunner.IsDialogueRunning)
            {
                dialogueRunner.StartDialogue(targetNode);
                
                if (destroyAfterDialogue)
                {
                    // 会話終了時に自身を削除する処理を仕込む
                    dialogueRunner.onDialogueComplete?.AddListener(OnDialogueCompleteAndDestroy);
                }
            }
        }

        private void OnDialogueCompleteAndDestroy()
        {
            dialogueRunner.onDialogueComplete?.RemoveListener(OnDialogueCompleteAndDestroy);
            Destroy(gameObject);
        }
    }
}
