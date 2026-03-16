using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;
using Cysharp.Threading.Tasks;

#nullable enable

namespace Metroidvania.UI
{
    [Serializable]
    public struct CharacterPortrait
    {
        public string characterName;
        public Sprite portraitSprite;
    }

    /// <summary>
    /// Yarn Spinner用のカスタムダイアログView（ADV形式）。
    /// 話者名、テキストタイプライター表示、立ち絵表示、選択肢に対応。
    /// </summary>
    public class DialogueView : DialoguePresenterBase
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject dialoguePanel = null!;
        [SerializeField] private TextMeshProUGUI speakerNameText = null!;
        [SerializeField] private TextMeshProUGUI dialogueText = null!;
        [SerializeField] private Image portraitImage = null!;
        [SerializeField] private GameObject nextIndicator = null!;

        [Header("Options UI Elements")]
        [SerializeField] private GameObject optionsPanel = null!;
        [SerializeField] private GameObject optionButtonPrefab = null!;
        [SerializeField] private Transform optionsContainer = null!;

        [Header("Character Portraits")]
        [Tooltip("話者名と立ち絵の対応リスト")]
        [SerializeField] private System.Collections.Generic.List<CharacterPortrait> characterPortraits = new();

        [Header("Settings")]
        [SerializeField] private float textSpeed = 30f; // character per second
        [SerializeField] private float indicatorBlinkSpeed = 0.5f;

        private Action<int>? _onOptionSelected;

        // 進行管理用のCancellationTokenSource
        private CancellationTokenSource? _currentLineCts;

        private void Awake()
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(false);
            if (nextIndicator != null) nextIndicator.SetActive(false);
            if (speakerNameText != null) speakerNameText.text = "";
            if (dialogueText != null) dialogueText.text = "";
            if (portraitImage != null) portraitImage.gameObject.SetActive(false);
        }

        public override YarnTask OnDialogueStartedAsync()
        {
            gameObject.SetActive(true);
            if (dialoguePanel != null) dialoguePanel.SetActive(true);
            if (optionsPanel != null) optionsPanel.SetActive(false);
            return YarnTask.CompletedTask;
        }

        public override YarnTask OnDialogueCompleteAsync()
        {
            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            if (optionsPanel != null) optionsPanel.SetActive(false);
            if (portraitImage != null) portraitImage.gameObject.SetActive(false);
            gameObject.SetActive(false);
            return YarnTask.CompletedTask;
        }

        public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            var taskCompletionSource = new YarnTaskCompletionSource();
            RunLineInternalAsync(line, token, taskCompletionSource).Forget();
            return taskCompletionSource.Task;
        }

        private async UniTaskVoid RunLineInternalAsync(LocalizedLine line, LineCancellationToken token, YarnTaskCompletionSource tcs)
        {
            _currentLineCts?.Cancel();
            _currentLineCts?.Dispose();
            _currentLineCts = new CancellationTokenSource();

            // 元々のYarn提供のTokenと自分の入力用Tokenをリンクさせる
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                token.NextContentToken,
                token.HurryUpToken,
                _currentLineCts.Token
            );
            var mergedToken = linkedTokenSource.Token;

            if (speakerNameText != null) speakerNameText.text = line.CharacterName ?? "";
            if (dialogueText != null) dialogueText.text = "";
            if (nextIndicator != null) nextIndicator.SetActive(false);

            // 立ち絵の切り替え
            if (portraitImage != null)
            {
                var portraitData = characterPortraits.Find(p => p.characterName == line.CharacterName);
                if (portraitData.portraitSprite != null)
                {
                    portraitImage.sprite = portraitData.portraitSprite;
                    portraitImage.gameObject.SetActive(true);
                }
                else
                {
                    // データがない場合は非表示
                    portraitImage.gameObject.SetActive(false);
                }
            }

            var text = line.TextWithoutCharacterName.Text;

            try
            {
                // タイプライター演出
                int textLength = text.Length;
                float delayBetweenChars = 1f / textSpeed;

                for (int i = 0; i < textLength; i++)
                {
                    if (dialogueText != null)
                    {
                        dialogueText.text = text.Substring(0, i + 1);
                    }

                    // HurryUp（スキップ指示）が来たら即全文表示
                    if (token.HurryUpToken.IsCancellationRequested)
                    {
                        if (dialogueText != null) dialogueText.text = text;
                        break;
                    }

                    // NextContent または 行自体のキャンセル（ユーザー入力等）が来たら中断
                    if (mergedToken.IsCancellationRequested)
                    {
                        break;
                    }

                    // unscaledDeltaTimeで待機
                    await UniTask.WaitForSeconds(delayBetweenChars, ignoreTimeScale: true, cancellationToken: mergedToken);
                }
            }
            catch (OperationCanceledException)
            {
                // キャンセル時は全文表示
                if (dialogueText != null) dialogueText.text = text;
            }

            // 全文表示後、次へマーカー点滅
            BlinkIndicatorTask(mergedToken).Forget();

            // NextContentTokenが呼ばれるか、ユーザー入力（_currentLineCts）が発火するまで待機
            try
            {
                await UniTask.WaitUntilCanceled(mergedToken);
            }
            catch (OperationCanceledException)
            {
                // 次に進む合図
            }
            finally
            {
                _currentLineCts?.Dispose();
                _currentLineCts = null;
                tcs.TrySetResult();
            }
        }

        public void OnContinueClicked()
        {
            _currentLineCts?.Cancel();
        }

        private async UniTask BlinkIndicatorTask(CancellationToken token)
        {
            if (nextIndicator == null) return;
            nextIndicator.SetActive(true);
            
            try
            {
                while (!token.IsCancellationRequested)
                {
                    nextIndicator.SetActive(!nextIndicator.activeSelf);
                    await UniTask.WaitForSeconds(indicatorBlinkSpeed, ignoreTimeScale: true, cancellationToken: token);
                }
            }
            catch (OperationCanceledException)
            {
                // token cancelled
            }
            finally
            {
                if (nextIndicator != null)
                {
                    nextIndicator.SetActive(false);
                }
            }
        }

        public override YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
        {
            var taskCompletionSource = new YarnTaskCompletionSource<DialogueOption?>();
            RunOptionsInternalAsync(dialogueOptions, cancellationToken, taskCompletionSource).Forget();
            return taskCompletionSource.Task;
        }

        private async UniTaskVoid RunOptionsInternalAsync(DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken, YarnTaskCompletionSource<DialogueOption?> outTcs)
        {
            optionsPanel.SetActive(true);

            // 既存のボタンをクリア
            foreach (Transform child in optionsContainer)
            {
                Destroy(child.gameObject);
            }

            // ボタン生成
            for (int i = 0; i < dialogueOptions.Length; i++)
            {
                var option = dialogueOptions[i];
                var buttonObj = Instantiate(optionButtonPrefab, optionsContainer);
                buttonObj.SetActive(true);

                var textComp = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
                if (textComp != null)
                {
                    textComp.text = option.Line.Text.Text;
                }

                var buttonComp = buttonObj.GetComponent<Button>();
                if (buttonComp != null)
                {
                    int index = i;
                    buttonComp.onClick.AddListener(() =>
                    {
                        _onOptionSelected?.Invoke(index);
                    });
                }
            }

            // 選択されるまで待機
            var tcs = new UniTaskCompletionSource<DialogueOption?>();
            _onOptionSelected = (index) =>
            {
                _onOptionSelected = null;
                optionsPanel.SetActive(false);
                tcs.TrySetResult(dialogueOptions[index]);
            };

            // キャンセルされたらnullを返す
            using var reg = cancellationToken.NextContentToken.Register(() =>
            {
                _onOptionSelected = null;
                optionsPanel.SetActive(false);
                tcs.TrySetResult(null);
            });

            var result = await tcs.Task;
            outTcs.TrySetResult(result);
        }
    }
}
