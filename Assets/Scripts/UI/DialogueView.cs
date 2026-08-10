using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

#nullable enable

namespace Metroidvania.UI
{
    public enum DialoguePortraitSlot
    {
        Left,
        Right
    }

    [Serializable]
    public struct PortraitExpression
    {
        [Tooltip("Yarn line tag name used as #face:<name>.")]
        public string expressionName;

        public Sprite portraitSprite;
    }

    [Serializable]
    public struct CharacterPortrait
    {
        [Tooltip("Must match the speaker name written in Yarn.")]
        public string characterName;

        [Tooltip("Used when the line has no #face tag or uses #face:default.")]
        public Sprite portraitSprite;

        public DialoguePortraitSlot slot;

        [Tooltip("Optional expression sprites selected with Yarn tags such as #face:smile.")]
        public PortraitExpression[] expressionPortraits;
    }

    /// <summary>
    /// Fixed-screen ADV dialogue presenter.
    /// This is the only dialogue presenter used by the story runtime after the
    /// bubble-to-ADV cutover. It owns presentation state, but not story-event
    /// completion or Timeline control.
    /// </summary>
    public sealed class DialogueView : DialoguePresenterBase
    {
        private enum LinePresentationState
        {
            Idle,
            Typing,
            WaitingForAdvance
        }

        [Header("Presentation Root")]
        [SerializeField] private GameObject presentationRoot = null!;
        [SerializeField] private GameObject dialoguePanel = null!;

        [Header("Dialogue")]
        [SerializeField] private TextMeshProUGUI speakerNameText = null!;
        [SerializeField] private TextMeshProUGUI dialogueText = null!;
        [SerializeField] private GameObject nextIndicator = null!;

        [Header("Portraits")]
        [SerializeField] private Image leftPortraitImage = null!;
        [SerializeField] private Image rightPortraitImage = null!;
        [SerializeField] private Color speakingPortraitColor = Color.white;
        [SerializeField] private Color inactivePortraitColor = new Color(0.32f, 0.32f, 0.32f, 1f);
        [SerializeField] private List<CharacterPortrait> characterPortraits = new();

        [Header("Log")]
        [SerializeField] private Button? logButton;
        [SerializeField] private GameObject? logPanel;
        [SerializeField] private TextMeshProUGUI? logText;
        [SerializeField] private Button? logCloseButton;

        [Header("Skip")]
        [SerializeField] private Button? skipButton;
        [SerializeField] private GameObject? skipConfirmPanel;
        [SerializeField] private Button? skipConfirmYesButton;
        [SerializeField] private Button? skipConfirmNoButton;

        [Header("Settings")]
        [SerializeField, Min(1f)] private float textSpeed = 30f;

        private readonly List<string> _logEntries = new();
        private readonly StringBuilder _logBuilder = new();
        private readonly HashSet<string> _missingExpressionWarnings = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _currentLineCts;
        private LinePresentationState _lineState;
        private bool _revealAllRequested;
        private bool _presentationEnabled = true;
        private bool _modalOpen;
        private int _lastAdvanceFrame = -1;
        private string? _leftCharacterName;
        private string? _rightCharacterName;
        private string _conversationNodeName = string.Empty;

        public event Action? SkipRequested;

        public bool IsPresentationEnabled => _presentationEnabled;

        private void Awake()
        {
            RegisterButtonSounds();
            HideView();
        }

        private void OnDisable()
        {
            _currentLineCts?.Cancel();
        }

        public void PrepareConversation(string nodeName)
        {
            _conversationNodeName = string.IsNullOrWhiteSpace(nodeName) ? string.Empty : nodeName.Trim();
        }

        public void SetPresentationEnabled(bool enabled)
        {
            _presentationEnabled = enabled;

            if (!enabled)
            {
                _currentLineCts?.Cancel();
                HideView();
            }
        }

        public override YarnTask OnDialogueStartedAsync()
        {
            if (!_presentationEnabled)
            {
                HideView();
                return YarnTask.CompletedTask;
            }

            gameObject.SetActive(true);
            SetActive(presentationRoot, true);
            SetActive(dialoguePanel, true);
            SetActive(logPanel, false);
            SetActive(skipConfirmPanel, false);
            SetActive(nextIndicator, false);
            SetActive(leftPortraitImage, false);
            SetActive(rightPortraitImage, false);

            _leftCharacterName = null;
            _rightCharacterName = null;
            _lineState = LinePresentationState.Idle;
            _modalOpen = false;
            _revealAllRequested = false;
            _logEntries.Clear();
            _missingExpressionWarnings.Clear();
            RefreshLogText();

            if (speakerNameText != null)
            {
                speakerNameText.text = string.Empty;
            }

            if (dialogueText != null)
            {
                dialogueText.text = string.Empty;
                dialogueText.maxVisibleCharacters = int.MaxValue;
            }

            return YarnTask.CompletedTask;
        }

        public override YarnTask OnDialogueCompleteAsync()
        {
            HideView();
            return YarnTask.CompletedTask;
        }

        public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            if (!_presentationEnabled)
            {
                return YarnTask.CompletedTask;
            }

            var completionSource = new YarnTaskCompletionSource();
            RunLineInternalAsync(line, token, completionSource).Forget();
            return completionSource.Task;
        }

        private async UniTaskVoid RunLineInternalAsync(
            LocalizedLine line,
            LineCancellationToken token,
            YarnTaskCompletionSource completionSource)
        {
            _currentLineCts?.Cancel();
            _currentLineCts?.Dispose();
            _currentLineCts = new CancellationTokenSource();

            using var completionTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                token.NextContentToken,
                _currentLineCts.Token);
            CancellationToken completionToken = completionTokenSource.Token;

            string speakerName = line.CharacterName?.Trim() ?? string.Empty;
            string text = line.TextWithoutCharacterName.Text;
            string expressionName = GetExpressionName(line.Metadata);

            ApplySpeaker(speakerName, expressionName);
            AddLogEntry(speakerName, text);

            if (speakerNameText != null)
            {
                speakerNameText.text = speakerName;
                speakerNameText.gameObject.SetActive(!string.IsNullOrEmpty(speakerName));
            }

            _revealAllRequested = false;
            _lineState = LinePresentationState.Typing;
            SetActive(nextIndicator, false);

            try
            {
                int characterCount = PrepareTypewriterText(text);
                float secondsPerCharacter = 1f / Mathf.Max(1f, textSpeed);

                for (int visibleCharacters = 1; visibleCharacters <= characterCount; visibleCharacters++)
                {
                    while (_modalOpen && !completionToken.IsCancellationRequested)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, completionToken);
                    }

                    if (_revealAllRequested || token.HurryUpToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (dialogueText != null)
                    {
                        dialogueText.maxVisibleCharacters = visibleCharacters;
                    }

                    await UniTask.WaitForSeconds(
                        secondsPerCharacter,
                        ignoreTimeScale: true,
                        cancellationToken: completionToken);
                }

                ShowFullLine();
                _lineState = LinePresentationState.WaitingForAdvance;
                SetActive(nextIndicator, true);

                await UniTask.WaitUntilCanceled(completionToken);
            }
            catch (OperationCanceledException)
            {
                // The next line or story shutdown was requested.
            }
            finally
            {
                SetActive(nextIndicator, false);
                _lineState = LinePresentationState.Idle;
                _revealAllRequested = false;

                _currentLineCts?.Dispose();
                _currentLineCts = null;
                completionSource.TrySetResult();
            }
        }

        private int PrepareTypewriterText(string text)
        {
            if (dialogueText == null)
            {
                return 0;
            }

            dialogueText.text = text;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();
            return dialogueText.textInfo?.characterCount ?? text.Length;
        }

        private void ShowFullLine()
        {
            if (dialogueText != null)
            {
                dialogueText.maxVisibleCharacters = int.MaxValue;
            }
        }

        public void OnContinueClicked()
        {
            if (!_presentationEnabled || _modalOpen || _lastAdvanceFrame == Time.frameCount)
            {
                return;
            }

            _lastAdvanceFrame = Time.frameCount;

            if (_lineState == LinePresentationState.Typing)
            {
                _revealAllRequested = true;
                ShowFullLine();
                return;
            }

            if (_lineState == LinePresentationState.WaitingForAdvance)
            {
                _currentLineCts?.Cancel();
            }
        }

        public void OnLogClicked()
        {
            if (logPanel == null)
            {
                return;
            }

            RefreshLogText();
            _modalOpen = true;
            logPanel.SetActive(true);
        }

        public void OnLogCloseClicked()
        {
            SetActive(logPanel, false);
            _modalOpen = skipConfirmPanel != null && skipConfirmPanel.activeSelf;
        }

        public void OnSkipClicked()
        {
            if (skipConfirmPanel == null)
            {
                return;
            }

            _modalOpen = true;
            skipConfirmPanel.SetActive(true);
        }

        public void OnSkipConfirmNoClicked()
        {
            SetActive(skipConfirmPanel, false);
            _modalOpen = logPanel != null && logPanel.activeSelf;
        }

        public void OnSkipConfirmYesClicked()
        {
            SetActive(skipConfirmPanel, false);
            _modalOpen = false;
            SkipRequested?.Invoke();
        }

        public override YarnTask<DialogueOption?> RunOptionsAsync(
            DialogueOption[] dialogueOptions,
            LineCancellationToken cancellationToken)
        {
            Debug.LogError(
                $"[DialogueView] Yarn options are not supported by the current ADV specification. " +
                $"node='{_conversationNodeName}', optionCount={dialogueOptions.Length}",
                this);
            return YarnTask.FromResult<DialogueOption?>(null);
        }

        private void ApplySpeaker(string speakerName, string expressionName)
        {
            CharacterPortrait? portrait = FindPortrait(speakerName);
            if (portrait.HasValue)
            {
                CharacterPortrait value = portrait.Value;
                Sprite? sprite = FindExpressionSprite(value, expressionName);
                if (sprite != null && value.slot == DialoguePortraitSlot.Right)
                {
                    SetPortrait(rightPortraitImage, sprite, true);
                    _rightCharacterName = speakerName;
                }
                else if (sprite != null)
                {
                    SetPortrait(leftPortraitImage, sprite, true);
                    _leftCharacterName = speakerName;
                }
            }

            bool hasSpeaker = !string.IsNullOrWhiteSpace(speakerName);
            SetPortraitColor(
                leftPortraitImage,
                hasSpeaker && string.Equals(_leftCharacterName, speakerName, StringComparison.OrdinalIgnoreCase));
            SetPortraitColor(
                rightPortraitImage,
                hasSpeaker && string.Equals(_rightCharacterName, speakerName, StringComparison.OrdinalIgnoreCase));
        }

        private CharacterPortrait? FindPortrait(string speakerName)
        {
            if (string.IsNullOrWhiteSpace(speakerName))
            {
                return null;
            }

            for (int i = 0; i < characterPortraits.Count; i++)
            {
                CharacterPortrait portrait = characterPortraits[i];
                if (string.Equals(
                        portrait.characterName?.Trim(),
                        speakerName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return portrait;
                }
            }

            return null;
        }

        private static string GetExpressionName(string[]? metadata)
        {
            if (metadata == null)
            {
                return "default";
            }

            const string prefix = "face:";
            for (int i = 0; i < metadata.Length; i++)
            {
                string tag = (metadata[i] ?? string.Empty).Trim().TrimStart('#');
                if (!tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string expressionName = tag.Substring(prefix.Length).Trim();
                return string.IsNullOrEmpty(expressionName) ? "default" : expressionName;
            }

            return "default";
        }

        private Sprite? FindExpressionSprite(CharacterPortrait portrait, string expressionName)
        {
            if (string.IsNullOrWhiteSpace(expressionName) ||
                string.Equals(expressionName, "default", StringComparison.OrdinalIgnoreCase))
            {
                return portrait.portraitSprite;
            }

            PortraitExpression[]? expressions = portrait.expressionPortraits;
            if (expressions != null)
            {
                for (int i = 0; i < expressions.Length; i++)
                {
                    PortraitExpression expression = expressions[i];
                    if (string.Equals(
                            expression.expressionName?.Trim(),
                            expressionName,
                            StringComparison.OrdinalIgnoreCase) &&
                        expression.portraitSprite != null)
                    {
                        return expression.portraitSprite;
                    }
                }
            }

            string warningKey = $"{portrait.characterName}\n{expressionName}";
            if (_missingExpressionWarnings.Add(warningKey))
            {
                Debug.LogWarning(
                    $"[DialogueView] Portrait expression was not found; using the default portrait. " +
                    $"node='{_conversationNodeName}', character='{portrait.characterName}', face='{expressionName}'",
                    this);
            }

            return portrait.portraitSprite;
        }

        private void SetPortraitColor(Image? image, bool isSpeaking)
        {
            if (image != null && image.gameObject.activeSelf)
            {
                image.color = isSpeaking ? speakingPortraitColor : inactivePortraitColor;
            }
        }

        private static void SetPortrait(Image? image, Sprite sprite, bool visible)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.preserveAspect = true;
            image.gameObject.SetActive(visible);
        }

        private void AddLogEntry(string speakerName, string text)
        {
            string entry = string.IsNullOrWhiteSpace(speakerName)
                ? text
                : $"{speakerName}\n{text}";
            _logEntries.Add(entry);
            RefreshLogText();
        }

        private void RefreshLogText()
        {
            if (logText == null)
            {
                return;
            }

            _logBuilder.Clear();
            for (int i = 0; i < _logEntries.Count; i++)
            {
                if (i > 0)
                {
                    _logBuilder.Append("\n\n");
                }

                _logBuilder.Append(_logEntries[i]);
            }

            logText.text = _logBuilder.ToString();
        }

        private void RegisterButtonSounds()
        {
            RegisterButtonSound(logButton);
            RegisterButtonSound(logCloseButton);
            RegisterButtonSound(skipButton);
            RegisterButtonSound(skipConfirmYesButton);
            RegisterButtonSound(skipConfirmNoButton);
        }

        private static void RegisterButtonSound(Button? button)
        {
            if (button != null)
            {
                UIButtonSfxPlayer.Register(button);
            }
        }

        private void HideView()
        {
            _currentLineCts?.Cancel();
            _lineState = LinePresentationState.Idle;
            _modalOpen = false;
            _revealAllRequested = false;
            _leftCharacterName = null;
            _rightCharacterName = null;

            SetActive(nextIndicator, false);
            SetActive(logPanel, false);
            SetActive(skipConfirmPanel, false);
            SetActive(leftPortraitImage, false);
            SetActive(rightPortraitImage, false);
            SetActive(dialoguePanel, false);
            SetActive(presentationRoot, false);

            if (speakerNameText != null)
            {
                speakerNameText.text = string.Empty;
            }

            if (dialogueText != null)
            {
                dialogueText.text = string.Empty;
                dialogueText.maxVisibleCharacters = int.MaxValue;
            }
        }

        private static void SetActive(GameObject? target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private static void SetActive(Component? target, bool active)
        {
            if (target != null)
            {
                target.gameObject.SetActive(active);
            }
        }
    }
}
