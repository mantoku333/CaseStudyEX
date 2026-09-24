using System;
using System.Collections.Generic;
using System.Globalization;
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

        [Tooltip("Optional sprite animation. When assigned, it replaces the static expression sprite.")]
        public PortraitAnimationClip? portraitAnimation;
    }

    [Serializable]
    public struct CharacterPortrait
    {
        [Tooltip("Must match the speaker name written in Yarn.")]
        public string characterName;

        [Tooltip("Used when the line has no #face tag or uses #face:default.")]
        public Sprite portraitSprite;

        [Tooltip("Optional default animation used by no tag, #face:default, or #face:normal.")]
        public PortraitAnimationClip? portraitAnimation;

        public DialoguePortraitSlot slot;

        [Tooltip("Optional expression sprites selected with Yarn tags such as #face:smile.")]
        public PortraitExpression[] expressionPortraits;
    }

    [Serializable]
    public struct DialogueIllustration
    {
        [Tooltip("Yarn line tag name used as #illustration:<name>.")]
        public string illustrationName;

        public Sprite illustrationSprite;

        [Tooltip("Optional. Uses the shared panel-open SE when this is not assigned.")]
        public AudioClip displaySfx;
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

        [Header("Authored Dialogue Artwork")]
        [SerializeField] private Image? dialogueBackground;
        [SerializeField] private Image? speakerNameArtwork;
        [SerializeField] private Sprite? leftDialogueSprite;
        [SerializeField] private Sprite? rightDialogueSprite;
        [SerializeField] private Sprite? irisNameSprite;
        [SerializeField] private Sprite? noxLeftNameSprite;
        [SerializeField] private Sprite? noxRightNameSprite;
        [SerializeField] private Sprite? thanatosNameSprite;

        [Header("Portraits")]
        [SerializeField] private Image leftPortraitImage = null!;
        [SerializeField] private Image rightPortraitImage = null!;
        [SerializeField] private Color speakingPortraitColor = Color.white;
        [SerializeField] private Color inactivePortraitColor = new Color(0.32f, 0.32f, 0.32f, 1f);
        [SerializeField] private List<CharacterPortrait> characterPortraits = new();

        [Header("Center Illustrations")]
        [SerializeField] private Image? centerIllustrationImage;
        [SerializeField] private List<DialogueIllustration> centerIllustrations = new();
        [SerializeField, Range(0f, 1f)] private float illustrationSfxVolume = 1f;
        [SerializeField, Min(0f)] private float illustrationEntranceSeconds = 0.2f;
        [SerializeField, Min(0f)] private float illustrationEntranceOffset = 60f;

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

        [Header("Text Effects")]
        [Tooltip("Per-character position amplitude in UI pixels for Yarn lines tagged #shake.")]
        [SerializeField, Min(0f)] private float shakePositionAmplitude = 2.5f;
        [Tooltip("Per-character rotation amplitude in degrees for Yarn lines tagged #shake.")]
        [SerializeField, Min(0f)] private float shakeRotationAmplitude = 3f;
        [Tooltip("Animation speed for Yarn lines tagged #shake.")]
        [SerializeField, Min(0f)] private float shakeSpeed = 12f;

        private readonly List<string> _logEntries = new();
        private readonly StringBuilder _logBuilder = new();
        private readonly HashSet<string> _missingExpressionWarnings = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _missingIllustrationWarnings = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _currentLineCts;
        private LinePresentationState _lineState;
        private bool _revealAllRequested;
        private bool _pageAdvanceRequested;
        private int _currentDialoguePage = 1;
        private int _dialoguePageCount = 1;
        private bool _emphasizedLine;
        private bool _presentationEnabled = true;
        private bool _modalOpen;
        private int _lastAdvanceFrame = -1;
        private string? _leftCharacterName;
        private string? _rightCharacterName;
        private string _conversationNodeName = string.Empty;
        private float _baseDialogueFontSize;
        private bool _shakeTextActive;
        private CancellationTokenSource? _illustrationAnimationCts;
        private Vector2 _illustrationRestPosition;
        private bool _illustrationRestPositionCaptured;
        private readonly PortraitPlayback _leftPortraitPlayback = new();
        private readonly PortraitPlayback _rightPortraitPlayback = new();

        private sealed class PortraitPlayback
        {
            private Image? _image;
            private PortraitAnimationClip? _clip;
            private string? _characterName;
            private double _elapsedSeconds;

            public void Play(Image? image, Sprite sprite, PortraitAnimationClip? clip, string characterName)
            {
                if (image == null) { Stop(); return; }
                bool continuing = _image == image && _clip != null && _clip == clip &&
                    string.Equals(_characterName, characterName, StringComparison.OrdinalIgnoreCase);
                _image = image;
                _clip = clip;
                _characterName = characterName;
                if (!continuing) _elapsedSeconds = 0d;
                SetPortrait(image, clip != null ? clip.GetFrame(_elapsedSeconds) ?? sprite : sprite, true);
            }

            public void Advance(float unscaledDeltaSeconds)
            {
                if (_clip == null || _image == null || !_image.gameObject.activeInHierarchy) return;
                _elapsedSeconds += Math.Max(0f, unscaledDeltaSeconds);
                Sprite frame = _clip.GetFrame(_elapsedSeconds);
                if (frame != null && _image.sprite != frame) _image.sprite = frame;
            }

            public void Stop()
            {
                _image = null;
                _clip = null;
                _characterName = null;
                _elapsedSeconds = 0d;
            }
        }

        public event Action? SkipRequested;

        public bool IsPresentationEnabled => _presentationEnabled;

        private void Awake()
        {
            CaptureBaseDialogueFontSize();
            CaptureIllustrationRestPosition();
            RegisterButtonSounds();
            HideView();
        }

        private void LateUpdate()
        {
            AdvancePortraitAnimations(Time.unscaledDeltaTime);
            if (_shakeTextActive &&
                _presentationEnabled &&
                dialogueText != null &&
                dialogueText.gameObject.activeInHierarchy)
            {
                ApplyPerCharacterShake();
            }
        }

        private void OnDisable()
        {
            _currentLineCts?.Cancel();
            CancelIllustrationAnimation();
            StopPortraitAnimations();
        }

        private void AdvancePortraitAnimations(float unscaledDeltaSeconds)
        {
            if (!_presentationEnabled) return;
            _leftPortraitPlayback.Advance(unscaledDeltaSeconds);
            _rightPortraitPlayback.Advance(unscaledDeltaSeconds);
        }

        private void StopPortraitAnimations()
        {
            _leftPortraitPlayback.Stop();
            _rightPortraitPlayback.Stop();
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
            StopPortraitAnimations();
            SetActive(presentationRoot, true);
            SetActive(dialoguePanel, true);
            SetActive(logPanel, false);
            SetActive(skipConfirmPanel, false);
            SetActive(nextIndicator, false);
            SetActive(leftPortraitImage, false);
            SetActive(rightPortraitImage, false);
            SetActive(speakerNameArtwork, false);
            HideCenterIllustration();

            _leftCharacterName = null;
            _rightCharacterName = null;
            _lineState = LinePresentationState.Idle;
            _modalOpen = false;
            _revealAllRequested = false;
            _logEntries.Clear();
            _missingExpressionWarnings.Clear();
            _missingIllustrationWarnings.Clear();
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
            bool isNarration = IsNarration(speakerName);

            ApplyLineTextEffects(line.Metadata);
            ApplyIllustrationMetadata(line.Metadata);
            ApplySpeaker(speakerName, expressionName);
            AddLogEntry(speakerName, text);

            if (speakerNameText != null)
            {
                speakerNameText.text = speakerName;
                speakerNameText.gameObject.SetActive(!isNarration &&
                    (speakerNameArtwork == null || !speakerNameArtwork.gameObject.activeSelf));
            }

            _revealAllRequested = false;
            _lineState = LinePresentationState.Typing;
            SetActive(nextIndicator, false);

            try
            {
                int characterCount = PrepareTypewriterText(text);
                float secondsPerCharacter = 1f / Mathf.Max(1f, textSpeed);

                for (_currentDialoguePage = 1; _currentDialoguePage <= _dialoguePageCount; _currentDialoguePage++)
                {
                    _pageAdvanceRequested = false;
                    _revealAllRequested = false;
                    _lineState = LinePresentationState.Typing;
                    SetActive(nextIndicator, false);
                    int firstCharacter = 0;
                    int lastCharacter = characterCount;
                    if (dialogueText != null)
                    {
                        dialogueText.pageToDisplay = _currentDialoguePage;
                        TMP_PageInfo page = dialogueText.textInfo.pageInfo[_currentDialoguePage - 1];
                        firstCharacter = page.firstCharacterIndex;
                        lastCharacter = page.lastCharacterIndex + 1;
                        dialogueText.maxVisibleCharacters = firstCharacter;
                    }

                    for (int visibleCharacters = firstCharacter + 1; visibleCharacters <= lastCharacter; visibleCharacters++)
                    {
                        while (_modalOpen && !completionToken.IsCancellationRequested)
                            await UniTask.Yield(PlayerLoopTiming.Update, completionToken);
                        if (_revealAllRequested || token.HurryUpToken.IsCancellationRequested) break;
                        if (dialogueText != null) dialogueText.maxVisibleCharacters = visibleCharacters;
                        await UniTask.WaitForSeconds(secondsPerCharacter, ignoreTimeScale: true,
                            cancellationToken: completionToken);
                    }

                    ShowFullLine();
                    _lineState = LinePresentationState.WaitingForAdvance;
                    SetActive(nextIndicator, true);
                    if (_currentDialoguePage < _dialoguePageCount)
                        await UniTask.WaitUntil(() => _pageAdvanceRequested, cancellationToken: completionToken);
                    else
                        await UniTask.WaitUntilCanceled(completionToken);
                }
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
                _pageAdvanceRequested = false;
                _currentDialoguePage = _dialoguePageCount = 1;

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

            dialogueText.enableAutoSizing = false;
            dialogueText.textWrappingMode = TextWrappingModes.NoWrap;
            dialogueText.overflowMode = TextOverflowModes.Overflow;
            dialogueText.pageToDisplay = 1;
            dialogueText.maxVisibleCharacters = int.MaxValue;
            dialogueText.text = text.Replace("\r\n", "\n").Replace('\r', '\n');
            dialogueText.ForceMeshUpdate(true);

            // TMP's parsed character indices exclude rich-text tags and treat sprite tags
            // and surrogate pairs as one displayed character. Keep the original markup.
            string source = dialogueText.text;
            var formatted = new StringBuilder(source.Length + 32);
            int sourceIndex = 0, column = 0, row = 0;
            int columns = _emphasizedLine ? 20 : 30;
            int rows = _emphasizedLine ? 1 : 2;
            for (int i = 0; i < dialogueText.textInfo.characterCount; i++)
            {
                TMP_CharacterInfo character = dialogueText.textInfo.characterInfo[i];
                if (character.index >= source.Length) break;
                formatted.Append(source, sourceIndex, character.index - sourceIndex);
                if (character.character == '\n')
                {
                    row++;
                    formatted.Append(row >= rows ? "<page>" : "\n");
                    if (row >= rows) row = 0;
                    column = 0;
                }
                else
                {
                    if (column == columns)
                    {
                        row++;
                        formatted.Append(row >= rows ? "<page>" : "\n");
                        if (row >= rows) row = 0;
                        column = 0;
                    }
                    formatted.Append(source, character.index, character.stringLength);
                    column++;
                }
                sourceIndex = character.index + character.stringLength;
            }
            formatted.Append(source, sourceIndex, source.Length - sourceIndex);
            dialogueText.text = formatted.ToString();
            dialogueText.overflowMode = TextOverflowModes.Page;
            dialogueText.ForceMeshUpdate(true);
            _currentDialoguePage = 1;
            _dialoguePageCount = Mathf.Max(1, dialogueText.textInfo.pageCount);
            dialogueText.maxVisibleCharacters = 0;
            return dialogueText.textInfo?.characterCount ?? text.Length;
        }

        private void ShowFullLine()
        {
            if (dialogueText != null)
            {
                dialogueText.maxVisibleCharacters = int.MaxValue;
            }
        }

        private void CaptureBaseDialogueFontSize()
        {
            if (dialogueText != null && _baseDialogueFontSize <= 0f)
            {
                _baseDialogueFontSize = dialogueText.fontSize;
            }
        }

        private void ApplyLineTextEffects(string[]? metadata)
        {
            _shakeTextActive = HasMetadataTag(metadata, "shake");

            if (dialogueText == null)
            {
                return;
            }

            CaptureBaseDialogueFontSize();
            _emphasizedLine = _shakeTextActive || GetLineFontScale(metadata) > 1f;
            float fontScale = _emphasizedLine ? 1.5f : 1f;
            dialogueText.fontSize = _baseDialogueFontSize * fontScale;
        }

        private void ResetLineTextEffects()
        {
            _shakeTextActive = false;
            _emphasizedLine = false;
            if (dialogueText != null && _baseDialogueFontSize > 0f)
            {
                dialogueText.fontSize = _baseDialogueFontSize;
            }
        }

        private void ApplyPerCharacterShake()
        {
            dialogueText.ForceMeshUpdate();
            TMP_TextInfo textInfo = dialogueText.textInfo;
            if (textInfo == null || textInfo.characterCount == 0)
            {
                return;
            }

            int visibleCharacterCount = dialogueText.maxVisibleCharacters == int.MaxValue
                ? textInfo.characterCount
                : Mathf.Min(dialogueText.maxVisibleCharacters, textInfo.characterCount);
            float time = Time.unscaledTime * shakeSpeed;

            for (int characterIndex = 0; characterIndex < visibleCharacterCount; characterIndex++)
            {
                TMP_CharacterInfo characterInfo = textInfo.characterInfo[characterIndex];
                if (!characterInfo.isVisible)
                {
                    continue;
                }

                int materialIndex = characterInfo.materialReferenceIndex;
                int vertexIndex = characterInfo.vertexIndex;
                if (materialIndex < 0 || materialIndex >= textInfo.meshInfo.Length)
                {
                    continue;
                }

                Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
                if (vertexIndex < 0 || vertexIndex + 3 >= vertices.Length)
                {
                    continue;
                }

                Vector3 center = (vertices[vertexIndex] + vertices[vertexIndex + 2]) * 0.5f;
                float phase = characterIndex * 1.618034f;
                var offset = new Vector3(
                    Mathf.Sin(time * 1.13f + phase) * shakePositionAmplitude,
                    Mathf.Sin(time * 1.47f + phase * 2.17f) * shakePositionAmplitude,
                    0f);
                Quaternion rotation = Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Sin(time * 0.91f + phase * 1.31f) * shakeRotationAmplitude);

                for (int corner = 0; corner < 4; corner++)
                {
                    int index = vertexIndex + corner;
                    vertices[index] = center + rotation * (vertices[index] - center) + offset;
                }
            }

            dialogueText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
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
                if (_currentDialoguePage < _dialoguePageCount)
                    _pageAdvanceRequested = true;
                else
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
            ApplySpeakerArtwork(speakerName);
            if (IsNarration(speakerName))
            {
                SetPortraitColor(leftPortraitImage, false);
                SetPortraitColor(rightPortraitImage, false);
                return;
            }

            CharacterPortrait? portrait = FindPortrait(speakerName);
            if (portrait.HasValue)
            {
                CharacterPortrait value = portrait.Value;
                Sprite? sprite = FindExpressionSprite(value, expressionName);
                PortraitAnimationClip? animation = FindExpressionAnimation(value, expressionName);
                if (sprite != null && value.slot == DialoguePortraitSlot.Right)
                {
                    _rightPortraitPlayback.Play(rightPortraitImage, sprite, animation, speakerName);
                    _rightCharacterName = speakerName;
                }
                else if (sprite != null)
                {
                    _leftPortraitPlayback.Play(leftPortraitImage, sprite, animation, speakerName);
                    _leftCharacterName = speakerName;
                }
            }

            SetPortraitColor(
                leftPortraitImage,
                string.Equals(_leftCharacterName, speakerName, StringComparison.OrdinalIgnoreCase));
            SetPortraitColor(
                rightPortraitImage,
                string.Equals(_rightCharacterName, speakerName, StringComparison.OrdinalIgnoreCase));
        }

        private void ApplySpeakerArtwork(string speakerName)
        {
            if (dialogueBackground == null || speakerNameArtwork == null) return;

            // Prefer the authored portrait slot, including scenes that place Nox on the right.
            CharacterPortrait? portrait = FindPortrait(speakerName);
            bool right = portrait.HasValue
                ? portrait.Value.slot == DialoguePortraitSlot.Right
                : string.Equals(speakerName, "イリス", StringComparison.OrdinalIgnoreCase);
            Sprite? name = speakerName switch
            {
                "イリス" => irisNameSprite,
                "ノクス" => right ? noxRightNameSprite : noxLeftNameSprite,
                "タナトス" => thanatosNameSprite,
                _ => null
            };

            // Unnamed/unknown speakers keep a text label without revealing a character's name.
            speakerNameArtwork.sprite = name;
            speakerNameArtwork.gameObject.SetActive(name != null);
            dialogueBackground.sprite = right ? rightDialogueSprite : leftDialogueSprite;
            RectTransform window = dialogueBackground.rectTransform;
            window.anchorMin = window.anchorMax = new Vector2(right ? 1f : 0f, 0f);
            window.pivot = new Vector2(right ? 1f : 0f, 0f);
            window.anchoredPosition = Vector2.zero;
            window.sizeDelta = new Vector2(1600f, 271f);

            if (dialogueText != null)
            {
                RectTransform text = dialogueText.rectTransform;
                text.anchorMin = text.anchorMax = new Vector2(0f, 1f);
                text.pivot = new Vector2(0f, 1f);
                text.anchoredPosition = new Vector2(right ? 80f : 400f, -30f);
                text.sizeDelta = new Vector2(1144f, 180f);
            }
            if (speakerNameText != null)
            {
                RectTransform label = speakerNameText.rectTransform;
                label.anchoredPosition = new Vector2(right ? 897f : 609f, 54f);
                speakerNameText.gameObject.SetActive(name == null && !IsNarration(speakerName));
            }
            if (nextIndicator != null)
            {
                RectTransform marker = (RectTransform)nextIndicator.transform;
                marker.anchorMin = marker.anchorMax = Vector2.zero;
                marker.anchoredPosition = new Vector2(right ? 1220f : 1540f, 70f);
            }
        }

        private static bool IsNarration(string? speakerName)
        {
            return string.IsNullOrWhiteSpace(speakerName);
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

        private static bool HasMetadataTag(string[]? metadata, string expectedTag)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(expectedTag))
            {
                return false;
            }

            for (int i = 0; i < metadata.Length; i++)
            {
                string tag = (metadata[i] ?? string.Empty).Trim().TrimStart('#');
                if (string.Equals(tag, expectedTag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static float GetLineFontScale(string[]? metadata)
        {
            if (metadata == null)
            {
                return 1f;
            }

            const string prefix = "size:";
            for (int i = 0; i < metadata.Length; i++)
            {
                string tag = (metadata[i] ?? string.Empty).Trim().TrimStart('#');
                if (!tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value = tag.Substring(prefix.Length).Trim();
                return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float scale) &&
                       scale > 0f
                    ? scale
                    : 1f;
            }

            return 1f;
        }

        private void ApplyIllustrationMetadata(string[]? metadata)
        {
            string? command = GetIllustrationCommand(metadata);
            if (command == null)
            {
                return;
            }

            if (IsIllustrationHideCommand(command))
            {
                HideCenterIllustration();
                return;
            }

            DialogueIllustration? illustration = FindIllustration(command);
            if (!illustration.HasValue || illustration.Value.illustrationSprite == null)
            {
                if (_missingIllustrationWarnings.Add(command))
                {
                    Debug.LogWarning(
                        $"[DialogueView] Center illustration was not found; hiding the illustration. " +
                        $"node='{_conversationNodeName}', illustration='{command}'",
                        this);
                }

                HideCenterIllustration();
                return;
            }

            ShowCenterIllustration(illustration.Value);
        }

        private void ShowCenterIllustration(DialogueIllustration illustration)
        {
            if (centerIllustrationImage == null)
            {
                return;
            }

            centerIllustrationImage.sprite = illustration.illustrationSprite;
            centerIllustrationImage.preserveAspect = true;
            centerIllustrationImage.color = Color.white;
            centerIllustrationImage.gameObject.SetActive(true);
            StartIllustrationEntrance();

            if (illustration.displaySfx != null)
            {
                StoryTimelineRuntime.Instance.PlaySe(illustration.displaySfx, illustrationSfxVolume);
            }
            else
            {
                UIButtonSfxPlayer.PlayPanelOpen();
            }
        }

        private void HideCenterIllustration()
        {
            CancelIllustrationAnimation();
            if (centerIllustrationImage == null)
            {
                return;
            }

            CaptureIllustrationRestPosition();
            centerIllustrationImage.rectTransform.anchoredPosition = _illustrationRestPosition;
            centerIllustrationImage.gameObject.SetActive(false);
            centerIllustrationImage.sprite = null;
        }

        private void CaptureIllustrationRestPosition()
        {
            if (centerIllustrationImage != null && !_illustrationRestPositionCaptured)
            {
                _illustrationRestPosition = centerIllustrationImage.rectTransform.anchoredPosition;
                _illustrationRestPositionCaptured = true;
            }
        }

        private void StartIllustrationEntrance()
        {
            if (centerIllustrationImage == null)
            {
                return;
            }

            CancelIllustrationAnimation();
            CaptureIllustrationRestPosition();

            RectTransform rectTransform = centerIllustrationImage.rectTransform;
            if (illustrationEntranceSeconds <= 0f || illustrationEntranceOffset <= 0f)
            {
                rectTransform.anchoredPosition = _illustrationRestPosition;
                return;
            }

            rectTransform.anchoredPosition =
                _illustrationRestPosition + Vector2.down * illustrationEntranceOffset;
            _illustrationAnimationCts = new CancellationTokenSource();
            AnimateIllustrationEntranceAsync(rectTransform, _illustrationAnimationCts).Forget();
        }

        private async UniTaskVoid AnimateIllustrationEntranceAsync(
            RectTransform rectTransform,
            CancellationTokenSource animationCts)
        {
            CancellationToken cancellationToken = animationCts.Token;
            Vector2 startPosition = rectTransform.anchoredPosition;
            float elapsed = 0f;

            try
            {
                while (elapsed < illustrationEntranceSeconds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    elapsed += Time.unscaledDeltaTime;
                    float progress = Mathf.Clamp01(elapsed / illustrationEntranceSeconds);
                    rectTransform.anchoredPosition = Vector2.LerpUnclamped(
                        startPosition,
                        _illustrationRestPosition,
                        EaseOutCubic(progress));
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                rectTransform.anchoredPosition = _illustrationRestPosition;
            }
            catch (OperationCanceledException)
            {
                // A replacement illustration, hide command, or dialogue shutdown interrupted the entrance.
            }
            finally
            {
                if (ReferenceEquals(_illustrationAnimationCts, animationCts))
                {
                    _illustrationAnimationCts = null;
                }

                animationCts.Dispose();
            }
        }

        private void CancelIllustrationAnimation()
        {
            CancellationTokenSource? animationCts = _illustrationAnimationCts;
            _illustrationAnimationCts = null;
            if (animationCts == null)
            {
                return;
            }

            animationCts.Cancel();
        }

        private static float EaseOutCubic(float progress)
        {
            float clamped = Mathf.Clamp01(progress);
            return 1f - Mathf.Pow(1f - clamped, 3f);
        }

        private DialogueIllustration? FindIllustration(string illustrationName)
        {
            for (int i = 0; i < centerIllustrations.Count; i++)
            {
                DialogueIllustration illustration = centerIllustrations[i];
                if (string.Equals(
                        illustration.illustrationName?.Trim(),
                        illustrationName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return illustration;
                }
            }

            return null;
        }

        private static string? GetIllustrationCommand(string[]? metadata)
        {
            if (metadata == null)
            {
                return null;
            }

            const string prefix = "illustration:";
            for (int i = 0; i < metadata.Length; i++)
            {
                string tag = (metadata[i] ?? string.Empty).Trim().TrimStart('#');
                if (tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return tag.Substring(prefix.Length).Trim();
                }
            }

            return null;
        }

        private static bool IsIllustrationHideCommand(string command)
        {
            return string.IsNullOrWhiteSpace(command) ||
                   string.Equals(command, "hide", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(command, "none", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(command, "off", StringComparison.OrdinalIgnoreCase);
        }

        private Sprite? FindExpressionSprite(CharacterPortrait portrait, string expressionName)
        {
            if (IsDefaultExpression(expressionName))
            {
                return portrait.portraitAnimation?.FirstFrame ?? portrait.portraitSprite;
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
                        (expression.portraitSprite != null || expression.portraitAnimation?.FirstFrame != null))
                    {
                        return expression.portraitAnimation?.FirstFrame ?? expression.portraitSprite;
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

            return portrait.portraitAnimation?.FirstFrame ?? portrait.portraitSprite;
        }

        private static bool IsDefaultExpression(string expressionName) =>
            string.IsNullOrWhiteSpace(expressionName) ||
            string.Equals(expressionName, "default", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(expressionName, "normal", StringComparison.OrdinalIgnoreCase);

        private static PortraitAnimationClip? FindExpressionAnimation(CharacterPortrait portrait, string expressionName)
        {
            if (!IsDefaultExpression(expressionName) && portrait.expressionPortraits != null)
            {
                foreach (var expression in portrait.expressionPortraits)
                {
                    if (string.Equals(expression.expressionName?.Trim(), expressionName, StringComparison.OrdinalIgnoreCase) &&
                        (expression.portraitSprite != null || expression.portraitAnimation?.FirstFrame != null))
                        return expression.portraitAnimation?.FirstFrame != null ? expression.portraitAnimation : null;
                }
            }
            return portrait.portraitAnimation?.FirstFrame != null ? portrait.portraitAnimation : null;
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
            StopPortraitAnimations();
            _currentLineCts?.Cancel();
            _lineState = LinePresentationState.Idle;
            _modalOpen = false;
            _revealAllRequested = false;
            _leftCharacterName = null;
            _rightCharacterName = null;
            ResetLineTextEffects();

            SetActive(nextIndicator, false);
            SetActive(logPanel, false);
            SetActive(skipConfirmPanel, false);
            SetActive(leftPortraitImage, false);
            SetActive(rightPortraitImage, false);
            HideCenterIllustration();
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
