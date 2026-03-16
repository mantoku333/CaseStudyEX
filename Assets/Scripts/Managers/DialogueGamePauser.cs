using UnityEngine;
using Yarn.Unity;

namespace Metroidvania.Managers
{
    /// <summary>
    /// 会話開始時にゲームの時間を止め、会話終了時に復帰させるコンポーネント。
    /// DialogueRunnerからイベントを受け取る。
    /// </summary>
    public class DialogueGamePauser : MonoBehaviour
    {
        [SerializeField] private DialogueRunner dialogueRunner = null!;

        // もとのTimeScaleを保存しておく
        private float _previousTimeScale = 1f;
        private bool _isPaused = false;

        private void Start()
        {
            if (dialogueRunner == null)
            {
                dialogueRunner = FindFirstObjectByType<DialogueRunner>();
            }

            if (dialogueRunner != null)
            {
                // Yarn Spinnerの開始/終了イベントを購読
                dialogueRunner.onDialogueStart?.AddListener(PauseGame);
                dialogueRunner.onDialogueComplete?.AddListener(ResumeGame);
            }
            else
            {
                Debug.LogWarning("[DialogueGamePauser] DialogueRunnerが見つかりません。");
            }
        }

        private void PauseGame()
        {
            if (_isPaused) return;

            _isPaused = true;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            // TODO: 入力制御（プレイヤー動かなくする等）が必要な場合はここに追加
            // 例: PlayerInputModule.Disable() など
            Debug.Log("[DialogueGamePauser] 会話開始: タイムスケールを0にしました。");
        }

        private void ResumeGame()
        {
            if (!_isPaused) return;

            _isPaused = false;
            Time.timeScale = _previousTimeScale;

            Debug.Log("[DialogueGamePauser] 会話終了: タイムスケールを戻しました。");
        }

        private void OnDestroy()
        {
            if (dialogueRunner != null)
            {
                dialogueRunner.onDialogueStart?.RemoveListener(PauseGame);
                dialogueRunner.onDialogueComplete?.RemoveListener(ResumeGame);
            }

            // 万が一停止中に破棄された場合は時間を戻す
            if (_isPaused)
            {
                Time.timeScale = _previousTimeScale;
            }
        }
    }
}
