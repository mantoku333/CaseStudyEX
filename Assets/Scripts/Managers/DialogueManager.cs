using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;
using Metroidvania.UI;

namespace Metroidvania.Managers
{
    /// <summary>
    /// 会話システムの全体管理クラス。
    /// Input Systemからの入力（Space / South Button）を受け取り、
    /// DialogueRunnerや各Viewへ進行の合図を送る。
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        [SerializeField] private DialogueRunner dialogueRunner = null!;
        [SerializeField] private InputActionReference nextAction = null!;

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

            // DialogueRunnerに登録されているすべてのViewに対して「進める」指示を出す
            foreach (var view in dialogueRunner.DialoguePresenters)
            {
                if (view is DialogueView dv)
                {
                    // DialogueView 側で独自のメソッドを用意していないため、
                    // Yarn Spinnerの `DialogueRunner` にある `Continue()` は使用できない可能性がある。
                    // 実際には、現在表示中のLineのCancellationToken (HurryUp / NextContent) を
                    // 発火させる仕組みが DialogueRunner に必要だが、Yarn 3.x の Public API では非推奨化されたものが多い。
                    // 代わりに、現在待機中のLineCancellationTokenSourceをキャンセルする処理をDialogueViewに持たせるのが確実。
                    dv.OnContinueClicked();
                }
            }
        }
    }
}
