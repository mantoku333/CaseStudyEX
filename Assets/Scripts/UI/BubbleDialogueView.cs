using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using Yarn.Unity;

#nullable enable

namespace Metroidvania.UI
{
    [Serializable]
    public struct BubbleSpeakerAnchor
    {
        public string characterName;
        public Transform target;
        public Vector3 offset;
    }

    /// <summary>
    /// Bubble-style dialogue presenter.
    /// - Follows a speaker transform in world space.
    /// - Resizes the bubble based on the full line length.
    /// </summary>
    public class BubbleDialogueView : DialoguePresenterBase
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject bubblePanel = null!;
        [SerializeField] private TextMeshProUGUI dialogueText = null!;

        [Header("Settings")]
        [SerializeField] private float textSpeed = 30f;
        [SerializeField] private Vector3 offset = new Vector3(0f, 2f, 0f);
        [SerializeField] private bool clampBubbleToScreen = true;
        [SerializeField] private Vector2 screenEdgePadding = new Vector2(24f, 24f);

        [Header("Speaker Anchors")]
        [SerializeField] private List<BubbleSpeakerAnchor> speakerAnchors = new();
        [SerializeField] private bool tryAutoResolveSpeakerByObjectName = true;
        [SerializeField] private bool logUnresolvedSpeaker = true;

        [Header("Auto Size")]
        [SerializeField] private bool autoResizeBubble = true;
        [SerializeField] private Vector2 bubblePadding = new Vector2(72f, 44f);
        [SerializeField] private float minBubbleWidth = 140f;
        [SerializeField] private float maxBubbleWidth = 980f;
        [SerializeField] private float minBubbleHeight = 84f;
        [SerializeField] private float maxBubbleHeight = 640f;
        [SerializeField] private float maxTextWidth = 900f;
        [SerializeField] private int widenBubbleWhenLineCountExceeds = 2;
        [SerializeField] private float extraHeightSafetyPadding = 12f;
        [SerializeField] private bool logAutoSizeResult = false;
        [SerializeField] private bool normalizeTextMargin = true;
        [SerializeField] private Vector4 normalizedTextMargin = new Vector4(0f, 0f, 12f, 6f);

        private readonly Dictionary<string, Transform?> _speakerTargetCache =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _warnedUnresolvedSpeakers =
            new(StringComparer.OrdinalIgnoreCase);

        private Transform? _conversationDefaultTarget;
        private Transform? _currentTarget;
        private Vector3 _currentOffset;

        private Camera? _mainCamera;
        private RectTransform? _bubbleRectTransform;
        private RectTransform? _textRectTransform;
        private CancellationTokenSource? _currentLineCts;
        private bool _hasLoggedAutoSizeStart;
        private bool _hasLoggedAutoSizeSkipReason;

        private void Awake()
        {
            EnsureAutoSizeDefaultsIfMissing();

            _mainCamera = Camera.main;
            _bubbleRectTransform = bubblePanel != null ? bubblePanel.GetComponent<RectTransform>() : null;
            _textRectTransform = dialogueText != null ? dialogueText.rectTransform : null;
            _currentOffset = offset;

            if (bubblePanel != null)
            {
                bubblePanel.SetActive(false);
            }

            if (dialogueText != null)
            {
                ApplyTextLayoutDefaults();
                dialogueText.text = string.Empty;
            }
        }

        public void SetTarget(Transform? target)
        {
            _conversationDefaultTarget = target;
            _currentTarget = target;
            _currentOffset = offset;
        }

        private void LateUpdate()
        {
            if (bubblePanel == null)
            {
                return;
            }

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
            }

            if (_currentTarget == null || _mainCamera == null)
            {
                return;
            }

            Vector3 worldPos = _currentTarget.position + _currentOffset;
            Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0f)
            {
                bubblePanel.SetActive(false);
                return;
            }

            if (!bubblePanel.activeSelf && gameObject.activeInHierarchy)
            {
                bubblePanel.SetActive(true);
            }

            if (clampBubbleToScreen)
            {
                screenPos = ClampToScreen(screenPos);
            }

            bubblePanel.transform.position = screenPos;
        }

        private Vector3 ClampToScreen(Vector3 screenPos)
        {
            if (_bubbleRectTransform == null)
            {
                return screenPos;
            }

            Vector3 lossyScale = _bubbleRectTransform.lossyScale;
            float halfWidth = Mathf.Max(0f, _bubbleRectTransform.rect.width * Mathf.Abs(lossyScale.x) * 0.5f);
            float halfHeight = Mathf.Max(0f, _bubbleRectTransform.rect.height * Mathf.Abs(lossyScale.y) * 0.5f);

            float padX = Mathf.Max(0f, screenEdgePadding.x);
            float padY = Mathf.Max(0f, screenEdgePadding.y);

            float minX = padX + halfWidth;
            float maxX = Screen.width - padX - halfWidth;
            float minY = padY + halfHeight;
            float maxY = Screen.height - padY - halfHeight;

            screenPos.x = minX > maxX
                ? Screen.width * 0.5f
                : Mathf.Clamp(screenPos.x, minX, maxX);
            screenPos.y = minY > maxY
                ? Screen.height * 0.5f
                : Mathf.Clamp(screenPos.y, minY, maxY);

            return screenPos;
        }

        public override YarnTask OnDialogueStartedAsync()
        {
            if (!gameObject.activeSelf)
            {
                return YarnTask.CompletedTask;
            }

            if (logAutoSizeResult)
            {
                Debug.Log(
                    $"[BubbleDialogueView] OnDialogueStarted scene='{gameObject.scene.name}', object='{name}', frame={Time.frameCount}");
            }

            gameObject.SetActive(true);
            if (bubblePanel != null)
            {
                bubblePanel.SetActive(true);
            }

            if (_currentTarget == null)
            {
                _currentTarget = _conversationDefaultTarget;
                _currentOffset = offset;
            }

            if (logAutoSizeResult && !_hasLoggedAutoSizeStart)
            {
                _hasLoggedAutoSizeStart = true;
                string bubbleScale = _bubbleRectTransform != null ? _bubbleRectTransform.localScale.ToString("F3") : "null";
                string textScale = _textRectTransform != null ? _textRectTransform.localScale.ToString("F3") : "null";
                string bubbleAnchors = _bubbleRectTransform != null
                    ? $"{_bubbleRectTransform.anchorMin}->{_bubbleRectTransform.anchorMax}"
                    : "null";
                string textAnchors = _textRectTransform != null
                    ? $"{_textRectTransform.anchorMin}->{_textRectTransform.anchorMax}"
                    : "null";
                Debug.Log(
                    $"[BubbleDialogueView] AutoSize debug ON. scene='{gameObject.scene.name}', object='{name}', " +
                    $"autoResize={autoResizeBubble}, logAutoSizeResult={logAutoSizeResult}, maxTextWidth={maxTextWidth:0.0}, " +
                    $"minBubble=({minBubbleWidth:0.0},{minBubbleHeight:0.0}), " +
                    $"maxBubble=({maxBubbleWidth:0.0},{maxBubbleHeight:0.0}), padding={bubblePadding}, " +
                    $"bubbleScale={bubbleScale}, textScale={textScale}, bubbleAnchors={bubbleAnchors}, textAnchors={textAnchors}");
            }

            return YarnTask.CompletedTask;
        }

        public override YarnTask OnDialogueCompleteAsync()
        {
            if (bubblePanel != null)
            {
                bubblePanel.SetActive(false);
            }

            _currentTarget = null;
            _currentOffset = offset;
            gameObject.SetActive(false);
            return YarnTask.CompletedTask;
        }

        public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            if (!gameObject.activeSelf)
            {
                return YarnTask.CompletedTask;
            }

            var taskCompletionSource = new YarnTaskCompletionSource();
            RunLineInternalAsync(line, token, taskCompletionSource).Forget();
            return taskCompletionSource.Task;
        }

        private async UniTaskVoid RunLineInternalAsync(LocalizedLine line, LineCancellationToken token, YarnTaskCompletionSource tcs)
        {
            _currentLineCts?.Cancel();
            _currentLineCts?.Dispose();
            _currentLineCts = new CancellationTokenSource();

            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                token.NextContentToken,
                token.HurryUpToken,
                _currentLineCts.Token
            );
            CancellationToken mergedToken = linkedTokenSource.Token;

            ApplySpeakerTarget(line.CharacterName);

            if (dialogueText != null)
            {
                ApplyTextLayoutDefaults();
                dialogueText.text = string.Empty;
            }

            string text = line.TextWithoutCharacterName.Text;
            UpdateBubbleSizeForText(text);

            try
            {
                int textLength = text.Length;
                float delayBetweenChars = 1f / Mathf.Max(1f, textSpeed);

                for (int i = 0; i < textLength; i++)
                {
                    if (dialogueText != null)
                    {
                        dialogueText.text = text.Substring(0, i + 1);
                    }

                    if (token.HurryUpToken.IsCancellationRequested)
                    {
                        if (dialogueText != null)
                        {
                            dialogueText.text = text;
                        }
                        break;
                    }

                    if (mergedToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await UniTask.WaitForSeconds(delayBetweenChars, ignoreTimeScale: true, cancellationToken: mergedToken);
                }
            }
            catch (OperationCanceledException)
            {
                if (dialogueText != null)
                {
                    dialogueText.text = text;
                }
            }

            try
            {
                await UniTask.WaitUntilCanceled(mergedToken);
            }
            catch (OperationCanceledException)
            {
                // continue
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

        public override YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] dialogueOptions, LineCancellationToken cancellationToken)
        {
            return YarnTask.FromResult<DialogueOption?>(dialogueOptions.Length > 0 ? dialogueOptions[0] : null);
        }

        private void ApplySpeakerTarget(string? characterName)
        {
            if (TryResolveSpeakerTarget(characterName, out Transform? target, out Vector3 speakerOffset))
            {
                _currentTarget = target;
                _currentOffset = speakerOffset;
                return;
            }

            string? speakerAlias = ResolveSpeakerAlias(characterName);
            if (!string.IsNullOrEmpty(speakerAlias) &&
                !string.Equals(characterName?.Trim(), speakerAlias, StringComparison.OrdinalIgnoreCase) &&
                TryResolveSpeakerTarget(speakerAlias, out target, out speakerOffset))
            {
                _currentTarget = target;
                _currentOffset = speakerOffset;
                return;
            }

            if (IsNarrationSpeaker(characterName))
            {
                _currentTarget = _conversationDefaultTarget;
                if (_currentTarget == null)
                {
                    _currentTarget = FindPlayerTransform();
                }
                _currentOffset = offset;
                return;
            }

            if (logUnresolvedSpeaker && !string.IsNullOrWhiteSpace(characterName))
            {
                string speaker = characterName.Trim();
                if (_warnedUnresolvedSpeakers.Add(speaker))
                {
                    Debug.LogWarning($"[BubbleDialogueView] Speaker target not resolved for '{speaker}'. Falling back to default target/player.");
                }
            }

            _currentTarget = _conversationDefaultTarget;
            if (_currentTarget == null)
            {
                _currentTarget = FindPlayerTransform();
            }
            _currentOffset = offset;
        }

        private bool TryResolveSpeakerTarget(string? characterName, out Transform? target, out Vector3 speakerOffset)
        {
            target = null;
            speakerOffset = offset;

            if (string.IsNullOrWhiteSpace(characterName))
            {
                return false;
            }

            string speaker = characterName.Trim();

            for (int i = 0; i < speakerAnchors.Count; i++)
            {
                BubbleSpeakerAnchor anchor = speakerAnchors[i];
                if (!string.Equals(anchor.characterName?.Trim(), speaker, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (anchor.target == null)
                {
                    continue;
                }

                target = anchor.target;
                speakerOffset = offset + anchor.offset;
                return true;
            }

            if (_speakerTargetCache.TryGetValue(speaker, out Transform? cached) && cached != null)
            {
                target = cached;
                return true;
            }
            if (_speakerTargetCache.ContainsKey(speaker))
            {
                return false;
            }

            if (!tryAutoResolveSpeakerByObjectName)
            {
                return false;
            }

            Transform? resolved = FindSpeakerTransformByName(speaker);
            _speakerTargetCache[speaker] = resolved;

            if (resolved == null)
            {
                return false;
            }

            target = resolved;
            return true;
        }

        private static Transform? FindSpeakerTransformByName(string speaker)
        {
            if (string.IsNullOrWhiteSpace(speaker))
            {
                return null;
            }

            string trimmedSpeaker = speaker.Trim();
            string alias = ResolveSpeakerAlias(trimmedSpeaker) ?? trimmedSpeaker;

            if (string.Equals(alias, "player", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alias, "iris", StringComparison.OrdinalIgnoreCase))
            {
                Transform? player = FindPlayerTransform();
                if (player != null)
                {
                    return player;
                }
            }

            if (string.Equals(alias, "nox", StringComparison.OrdinalIgnoreCase))
            {
                Transform? noxTransform = FindTransformByExactName("PG_ACTOR_nox", "_PG_ACTOR_nox", "Nox", "nox");
                if (noxTransform != null)
                {
                    return noxTransform;
                }

                // Nox is often represented by the umbrella in the prologue scene.
                Transform? umbrellaMarker = FindTransformByExactName("PG_MARKER_umbrella", "_PG_MARKER_umbrella");
                if (umbrellaMarker != null)
                {
                    return umbrellaMarker;
                }
            }

            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform tf = transforms[i];
                if (tf != null && string.Equals(tf.name, trimmedSpeaker, StringComparison.OrdinalIgnoreCase))
                {
                    return tf;
                }
            }

            string prefixed = "PG_ACTOR_" + alias;
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform tf = transforms[i];
                if (tf != null &&
                    (string.Equals(tf.name, prefixed, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(tf.name, "_" + prefixed, StringComparison.OrdinalIgnoreCase)))
                {
                    return tf;
                }
            }

            return null;
        }

        private static Transform? FindTransformByExactName(params string[] candidateNames)
        {
            if (candidateNames == null || candidateNames.Length == 0)
            {
                return null;
            }

            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < candidateNames.Length; i++)
            {
                string name = candidateNames[i];
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                for (int j = 0; j < transforms.Length; j++)
                {
                    Transform tf = transforms[j];
                    if (tf != null && string.Equals(tf.name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return tf;
                    }
                }
            }

            return null;
        }

        private static bool IsNarrationSpeaker(string? speaker)
        {
            string? alias = ResolveSpeakerAlias(speaker);
            return string.Equals(alias, "narration", StringComparison.OrdinalIgnoreCase);
        }

        private static string? ResolveSpeakerAlias(string? speaker)
        {
            if (string.IsNullOrWhiteSpace(speaker))
            {
                return null;
            }

            string trimmed = speaker.Trim();
            if (string.Equals(trimmed, "\u30A4\u30EA\u30B9", StringComparison.OrdinalIgnoreCase) || // イリス
                string.Equals(trimmed, "iris", StringComparison.OrdinalIgnoreCase))
            {
                return "iris";
            }

            if (string.Equals(trimmed, "\u30CE\u30AF\u30B9", StringComparison.OrdinalIgnoreCase) || // ノクス
                string.Equals(trimmed, "nox", StringComparison.OrdinalIgnoreCase))
            {
                return "nox";
            }

            if (string.Equals(trimmed, "\u30CA\u30EC\u30FC\u30B7\u30E7\u30F3", StringComparison.OrdinalIgnoreCase) || // ナレーション
                string.Equals(trimmed, "narration", StringComparison.OrdinalIgnoreCase))
            {
                return "narration";
            }

            return trimmed;
        }

        private static Transform? FindPlayerTransform()
        {
            global::PlayerController player =
                UnityEngine.Object.FindFirstObjectByType<global::PlayerController>(FindObjectsInactive.Include);
            return player != null ? player.transform : null;
        }

        private void UpdateBubbleSizeForText(string text)
        {
            if (!autoResizeBubble || dialogueText == null || _bubbleRectTransform == null || _textRectTransform == null)
            {
                if (!_hasLoggedAutoSizeSkipReason)
                {
                    _hasLoggedAutoSizeSkipReason = true;
                    Debug.LogWarning(
                        $"[BubbleDialogueView] AutoSize skipped. autoResizeBubble={autoResizeBubble}, " +
                        $"dialogueTextNull={dialogueText == null}, bubbleRectNull={_bubbleRectTransform == null}, textRectNull={_textRectTransform == null}");
                }
                return;
            }

            ApplyTextLayoutDefaults();

            // Bubble and text rects can have different scales per scene/prefab override.
            // Convert measurements between text-local units and bubble-local units so
            // wrapping and padding stay visually consistent.
            float bubbleScaleX = SafePositiveScale(_bubbleRectTransform.lossyScale.x);
            float bubbleScaleY = SafePositiveScale(_bubbleRectTransform.lossyScale.y);
            float textScaleX = SafePositiveScale(_textRectTransform.lossyScale.x);
            float textScaleY = SafePositiveScale(_textRectTransform.lossyScale.y);
            float textToBubbleX = textScaleX / bubbleScaleX;
            float textToBubbleY = textScaleY / bubbleScaleY;
            float bubbleToTextX = bubbleScaleX / textScaleX;
            float bubbleToTextY = bubbleScaleY / textScaleY;

            string measureText = string.IsNullOrEmpty(text) ? " " : text;
            float clampedMaxTextWidthBubble = Mathf.Clamp(maxTextWidth, 32f, Mathf.Max(32f, maxBubbleWidth - bubblePadding.x));
            float clampedMaxTextWidthText = Mathf.Max(8f, clampedMaxTextWidthBubble * bubbleToTextX);

            // 1) Measure text at max width.
            _textRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, clampedMaxTextWidthText);
            dialogueText.text = measureText;
            dialogueText.ForceMeshUpdate();

            float measuredTextWidthText = Mathf.Clamp(dialogueText.preferredWidth, 8f, clampedMaxTextWidthText);
            float measuredTextWidthBubble = measuredTextWidthText * textToBubbleX;
            float bubbleWidth = Mathf.Clamp(measuredTextWidthBubble + bubblePadding.x, minBubbleWidth, maxBubbleWidth);
            float finalTextWidthBubble = Mathf.Max(8f, bubbleWidth - bubblePadding.x);
            float finalTextWidthText = Mathf.Max(8f, finalTextWidthBubble * bubbleToTextX);

            // 2) Prime layout and prefer wider bubble when wrapped lines exceed threshold.
            _textRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, finalTextWidthText);
            dialogueText.ForceMeshUpdate();
            int measuredLineCount = dialogueText.textInfo != null ? dialogueText.textInfo.lineCount : 1;
            if (measuredLineCount > widenBubbleWhenLineCountExceeds &&
                finalTextWidthBubble < clampedMaxTextWidthBubble)
            {
                float widenedTextWidthBubble = clampedMaxTextWidthBubble;
                bubbleWidth = Mathf.Clamp(widenedTextWidthBubble + bubblePadding.x, minBubbleWidth, maxBubbleWidth);
                finalTextWidthBubble = Mathf.Max(8f, bubbleWidth - bubblePadding.x);
                finalTextWidthText = Mathf.Max(8f, finalTextWidthBubble * bubbleToTextX);

                _textRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, finalTextWidthText);
                dialogueText.ForceMeshUpdate();
                measuredLineCount = dialogueText.textInfo != null ? dialogueText.textInfo.lineCount : measuredLineCount;
            }

            bool hitMaxWidth =
                Mathf.Approximately(bubbleWidth, maxBubbleWidth) ||
                Mathf.Approximately(finalTextWidthBubble, clampedMaxTextWidthBubble);

            // 3) Re-measure height with final width and a small safety padding.
            Vector2 preferredAtFinalWidth = dialogueText.GetPreferredValues(measureText, finalTextWidthText, 0f);
            float measuredTextHeightText = Mathf.Max(8f, preferredAtFinalWidth.y);
            float measuredTextHeightBubble = measuredTextHeightText * textToBubbleY + Mathf.Max(0f, extraHeightSafetyPadding);
            float bubbleHeight = Mathf.Clamp(measuredTextHeightBubble + bubblePadding.y, minBubbleHeight, maxBubbleHeight);
            float finalTextHeightBubble = Mathf.Max(8f, bubbleHeight - bubblePadding.y);
            float finalTextHeightText = Mathf.Max(8f, finalTextHeightBubble * bubbleToTextY);
            bool hitMaxHeight = Mathf.Approximately(bubbleHeight, maxBubbleHeight);

            // 4) Apply final sizes.
            _bubbleRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);
            _bubbleRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);
            _textRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, finalTextWidthText);
            _textRectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, finalTextHeightText);
            _textRectTransform.anchorMin = new Vector2(0f, 0.5f);
            _textRectTransform.anchorMax = new Vector2(0f, 0.5f);
            _textRectTransform.pivot = new Vector2(0f, 0.5f);

            // Some scenes keep legacy anchored offsets (for example, large +Y),
            // which makes text render outside the bubble. Re-center the text rect
            // inside the current bubble while preserving left/right and top/bottom padding.
            float textOffsetX = Mathf.Max(0f, (bubbleWidth - finalTextWidthBubble) * 0.5f);
            _textRectTransform.anchoredPosition = new Vector2(textOffsetX, 0f);

            // Keep typewriter behavior intact.
            dialogueText.text = string.Empty;

            if (logAutoSizeResult)
            {
                Debug.LogWarning(
                    $"[BubbleDialogueView] AutoSize textLen={text.Length}, bubble=({bubbleWidth:0.0},{bubbleHeight:0.0}), " +
                    $"text=({finalTextWidthBubble:0.0},{finalTextHeightBubble:0.0}), lines={measuredLineCount}, " +
                    $"hitMaxW={hitMaxWidth}, hitMaxH={hitMaxHeight}");
            }
        }

        private static float SafePositiveScale(float value)
        {
            return Mathf.Max(0.0001f, Mathf.Abs(value));
        }

        private void ApplyTextLayoutDefaults()
        {
            if (dialogueText == null)
            {
                return;
            }

            if (normalizeTextMargin)
            {
                dialogueText.margin = normalizedTextMargin;
            }

            dialogueText.enableWordWrapping = true;
        }

        private void EnsureAutoSizeDefaultsIfMissing()
        {
            // Old scene data can miss newly-added serialized fields and come in as zeros/false.
            bool looksUninitialized =
                maxBubbleWidth <= 0f &&
                maxBubbleHeight <= 0f &&
                maxTextWidth <= 0f &&
                bubblePadding == Vector2.zero &&
                minBubbleWidth <= 0f &&
                minBubbleHeight <= 0f;

            if (!looksUninitialized)
            {
                return;
            }

            autoResizeBubble = true;
            bubblePadding = new Vector2(72f, 44f);
            minBubbleWidth = 140f;
            maxBubbleWidth = 980f;
            minBubbleHeight = 84f;
            maxBubbleHeight = 640f;
            maxTextWidth = 900f;
            widenBubbleWhenLineCountExceeds = 2;
            extraHeightSafetyPadding = 12f;
            logAutoSizeResult = false;

            Debug.LogWarning("[BubbleDialogueView] AutoSize fields looked uninitialized. Runtime defaults were applied.");
        }
    }
}
