using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum EventPanelKind
{
    Custom = 0,
    Diary = 1,
    Guide = 2
}

[Serializable]
public sealed class EventPanelContent
{
    public EventPanelKind kind = EventPanelKind.Custom;
    public string title = string.Empty;
    [TextArea(2, 10)] public string body = string.Empty;
    public string closeLabel = string.Empty;
    public Sprite illustration;
    public Sprite[] animationFrames = Array.Empty<Sprite>();
    [Min(0f)] public float animationFramesPerSecond = 12f;
    [Min(0f)] public float animationLoopIntervalSeconds = 0.5f;
}

[DisallowMultipleComponent]
[AddComponentMenu("CaseStudy/Event/Event Panel Presenter")]
public sealed class EventPanelPresenter : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;

    [Header("Content")]
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text bodyLabel;
    [SerializeField] private TMP_Text closeButtonLabel;
    [SerializeField] private Image diaryBackdropImage;
    [SerializeField] private Image illustrationImage;
    [SerializeField] private Image animationImage;

    [Header("Diary Scroll")]
    [SerializeField] private ScrollRect bodyScrollRect;
    [SerializeField] private RectTransform bodyScrollViewport;
    [SerializeField] private RectTransform bodyScrollContent;
    [SerializeField, Min(0f)] private float bodyScrollSensitivity = 35f;

    [Header("Input")]
    [SerializeField] private bool closeOnSpace = true;

    private Action onClosed;
    private Coroutine autoCloseRoutine;
    private bool isVisible;
    private bool hidingInternal;
    private Sprite[] currentAnimationFrames = Array.Empty<Sprite>();
    private int currentAnimationFrameIndex;
    private float animationFrameTimer;
    private float animationLoopIntervalTimer;
    private float currentAnimationFramesPerSecond = 12f;
    private float currentAnimationLoopIntervalSeconds = 0.5f;
    private bool pendingBodyScrollRefresh;

    public bool IsVisible => isVisible;

    private void Reset()
    {
        panelRoot = gameObject;
        ResolveMissingReferences();
    }

    private void Awake()
    {
        ResolveMissingReferences();

        if (!isVisible)
        {
            HideWithoutCallback();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        ResolveMissingReferences();
    }
#endif

    private void OnEnable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
        }
    }

    private void OnDisable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Hide);
        }

        HideWithoutCallback();
    }

    private void Update()
    {
        if (pendingBodyScrollRefresh)
        {
            RefreshBodyScrollLayout();
        }

        if (isVisible && closeOnSpace && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Hide();
            return;
        }

        UpdateAnimation();
    }

    public bool ShowDiary(DiaryEntryData diaryEntryData, Action closeCallback = null, float autoCloseSecondsWhenNoButton = 0f)
    {
        return Show(CreateDiaryContent(diaryEntryData), closeCallback, autoCloseSecondsWhenNoButton);
    }

    public bool Show(EventPanelContent content, Action closeCallback = null, float autoCloseSecondsWhenNoButton = 0f)
    {
        float resolvedAutoCloseSeconds = ResolveAutoCloseSeconds(content, autoCloseSecondsWhenNoButton);
        if (!ValidateRequiredReferences(resolvedAutoCloseSeconds))
        {
            return false;
        }

        StopAutoCloseRoutine();
        onClosed = closeCallback;
        ApplyContent(content);
        SetVisible(true);
        RefreshDiaryBackdropImage();
        RefreshBodyScrollLayout();

        if (closeButton == null && resolvedAutoCloseSeconds > 0f)
        {
            autoCloseRoutine = StartCoroutine(AutoClose(resolvedAutoCloseSeconds));
        }

        return true;
    }

    public bool ShowExisting(Action closeCallback = null, float autoCloseSecondsWhenNoButton = 0f)
    {
        if (!ValidateRequiredReferences(autoCloseSecondsWhenNoButton))
        {
            return false;
        }

        StopAutoCloseRoutine();
        onClosed = closeCallback;
        SetVisible(true);
        RefreshDiaryBackdropImage();
        pendingBodyScrollRefresh = bodyScrollRect != null && bodyScrollContent != null;
        RefreshBodyScrollLayout();

        if (closeButton == null && autoCloseSecondsWhenNoButton > 0f)
        {
            autoCloseRoutine = StartCoroutine(AutoClose(autoCloseSecondsWhenNoButton));
        }

        return true;
    }

    public void Hide()
    {
        HideInternal(invokeCallback: true);
    }

    public void HideWithoutCallback()
    {
        HideInternal(invokeCallback: false);
    }

    private void HideInternal(bool invokeCallback)
    {
        if (hidingInternal)
        {
            return;
        }

        hidingInternal = true;
        StopAutoCloseRoutine();
        SetVisible(false);
        currentAnimationFrames = Array.Empty<Sprite>();
        pendingBodyScrollRefresh = false;

        Action callback = onClosed;
        onClosed = null;

        if (invokeCallback)
        {
            callback?.Invoke();
        }

        hidingInternal = false;
    }

    private IEnumerator AutoClose(float seconds)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds));
        autoCloseRoutine = null;
        Hide();
    }

    private static float ResolveAutoCloseSeconds(EventPanelContent content, float autoCloseSecondsWhenNoButton)
    {
        if (content != null && content.kind == EventPanelKind.Diary)
        {
            return 0f;
        }

        return autoCloseSecondsWhenNoButton;
    }

    private void StopAutoCloseRoutine()
    {
        if (autoCloseRoutine == null)
        {
            return;
        }

        StopCoroutine(autoCloseRoutine);
        autoCloseRoutine = null;
    }

    private void ApplyContent(EventPanelContent content)
    {
        if (content == null)
        {
            content = new EventPanelContent();
        }

        SetText(titleLabel, ResolveTitle(content));
        SetText(bodyLabel, content.body);
        SetText(closeButtonLabel, ResolveCloseLabel(content));
        bool isDiary = content.kind == EventPanelKind.Diary;
        ConfigureBodyLabel(isDiary);
        RefreshDiaryBackdropImage();
        pendingBodyScrollRefresh = isDiary && EnsureBodyScrollLayout();
        ApplyImage(illustrationImage, content.illustration);

        currentAnimationFrames = content.animationFrames ?? Array.Empty<Sprite>();
        currentAnimationFramesPerSecond = content.animationFramesPerSecond > 0f
            ? content.animationFramesPerSecond
            : 12f;
        currentAnimationLoopIntervalSeconds = Mathf.Max(0f, content.animationLoopIntervalSeconds);
        RestartAnimation();
    }

    private static EventPanelContent CreateDiaryContent(DiaryEntryData diaryEntryData)
    {
        return new EventPanelContent
        {
            kind = EventPanelKind.Diary,
            title = diaryEntryData != null ? diaryEntryData.GetTitle() : string.Empty,
            body = diaryEntryData != null ? diaryEntryData.GetContent() : string.Empty,
            closeLabel = "SPACEで閉じる"
        };
    }

    private static string ResolveTitle(EventPanelContent content)
    {
        if (!string.IsNullOrWhiteSpace(content.title))
        {
            return content.title.Trim();
        }

        switch (content.kind)
        {
            case EventPanelKind.Diary:
                return "Diary";
            case EventPanelKind.Guide:
                return "Guide";
            default:
                return string.Empty;
        }
    }

    private static string ResolveCloseLabel(EventPanelContent content)
    {
        if (!string.IsNullOrWhiteSpace(content.closeLabel))
        {
            return content.closeLabel.Trim();
        }

        return content.kind == EventPanelKind.Diary ? "SPACEで閉じる" : string.Empty;
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label == null)
        {
            return;
        }

        label.text = value ?? string.Empty;
    }

    private void ConfigureBodyLabel(bool isDiary)
    {
        if (!isDiary || bodyLabel == null)
        {
            return;
        }

        bodyLabel.alignment = TextAlignmentOptions.TopLeft;
        bodyLabel.enableWordWrapping = true;
        bodyLabel.overflowMode = TextOverflowModes.Overflow;
        bodyLabel.raycastTarget = false;
    }

    private bool EnsureBodyScrollLayout()
    {
        if (bodyLabel == null)
        {
            return false;
        }

        ResolveBodyScrollReferences();
        if (bodyScrollRect == null)
        {
            CreateBodyScrollLayout();
        }

        if (bodyScrollRect == null || bodyScrollViewport == null || bodyScrollContent == null)
        {
            return false;
        }

        ConfigureBodyScrollRect();
        return true;
    }

    private void ResolveBodyScrollReferences()
    {
        if (bodyScrollRect == null)
        {
            bodyScrollRect = bodyLabel.GetComponentInParent<ScrollRect>();
        }

        if (bodyScrollRect == null)
        {
            return;
        }

        if (bodyScrollViewport == null)
        {
            bodyScrollViewport = bodyScrollRect.viewport != null
                ? bodyScrollRect.viewport
                : bodyScrollRect.GetComponent<RectTransform>();
        }

        if (bodyScrollContent == null)
        {
            bodyScrollContent = bodyScrollRect.content;
        }
    }

    private void CreateBodyScrollLayout()
    {
        RectTransform bodyRect = bodyLabel.rectTransform;
        if (bodyRect == null || bodyRect.parent == null)
        {
            return;
        }

        RectTransform parentRect = bodyRect.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        int siblingIndex = bodyRect.GetSiblingIndex();
        var viewportObject = new GameObject("DiaryBodyScrollView", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.layer = bodyLabel.gameObject.layer;

        bodyScrollViewport = viewportObject.GetComponent<RectTransform>();
        bodyScrollViewport.SetParent(parentRect, worldPositionStays: false);
        bodyScrollViewport.SetSiblingIndex(siblingIndex);
        CopyRectTransformLayout(bodyRect, bodyScrollViewport);

        Image raycastImage = viewportObject.GetComponent<Image>();
        raycastImage.color = new Color(1f, 1f, 1f, 0f);
        raycastImage.raycastTarget = true;

        var contentObject = new GameObject("DiaryBodyScrollContent", typeof(RectTransform));
        contentObject.layer = bodyLabel.gameObject.layer;
        bodyScrollContent = contentObject.GetComponent<RectTransform>();
        bodyScrollContent.SetParent(bodyScrollViewport, worldPositionStays: false);
        ConfigureContentRect(bodyScrollContent);

        bodyRect.SetParent(bodyScrollContent, worldPositionStays: false);
        ConfigureBodyRect(bodyRect);

        bodyScrollRect = viewportObject.GetComponent<ScrollRect>();
    }

    private static void CopyRectTransformLayout(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.pivot = source.pivot;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
    }

    private static void ConfigureContentRect(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private static void ConfigureBodyRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
    }

    private void ConfigureBodyScrollRect()
    {
        bodyScrollRect.viewport = bodyScrollViewport;
        bodyScrollRect.content = bodyScrollContent;
        bodyScrollRect.horizontal = false;
        bodyScrollRect.vertical = true;
        bodyScrollRect.movementType = ScrollRect.MovementType.Clamped;
        bodyScrollRect.inertia = true;
        bodyScrollRect.scrollSensitivity = bodyScrollSensitivity;
        bodyScrollRect.horizontalScrollbar = null;
        bodyScrollRect.verticalScrollbar = null;
        bodyScrollRect.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        bodyScrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
    }

    private void RefreshBodyScrollLayout()
    {
        if (!pendingBodyScrollRefresh)
        {
            return;
        }

        if (!EnsureBodyScrollLayout())
        {
            pendingBodyScrollRefresh = false;
            return;
        }

        ConfigureBodyLabel(isDiary: true);
        Canvas.ForceUpdateCanvases();

        float viewportWidth = bodyScrollViewport.rect.width;
        float viewportHeight = bodyScrollViewport.rect.height;
        if (viewportWidth <= 0.01f || viewportHeight <= 0.01f)
        {
            return;
        }

        bodyLabel.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        float preferredHeight = bodyLabel.GetPreferredValues(bodyLabel.text, viewportWidth, Mathf.Infinity).y;
        float contentHeight = Mathf.Max(viewportHeight, preferredHeight);

        Vector2 contentSize = bodyScrollContent.sizeDelta;
        contentSize.x = 0f;
        contentSize.y = contentHeight;
        bodyScrollContent.sizeDelta = contentSize;
        bodyScrollContent.anchoredPosition = Vector2.zero;

        LayoutRebuilder.ForceRebuildLayoutImmediate(bodyScrollContent);
        bodyScrollRect.velocity = Vector2.zero;
        bodyScrollRect.verticalNormalizedPosition = 1f;
        pendingBodyScrollRefresh = false;
    }

    private void RefreshDiaryBackdropImage()
    {
        if (diaryBackdropImage == null)
        {
            return;
        }

        RectTransform backdropRect = diaryBackdropImage.rectTransform;
        RectTransform panelRect = panelRoot != null ? panelRoot.transform as RectTransform : null;

        if (panelRect != null && backdropRect.parent == panelRect.parent)
        {
            if (backdropRect.GetSiblingIndex() > panelRect.GetSiblingIndex())
            {
                backdropRect.SetSiblingIndex(panelRect.GetSiblingIndex());
            }

            backdropRect.position = panelRect.TransformPoint(panelRect.rect.center);
        }
        else
        {
            backdropRect.anchorMin = new Vector2(0.5f, 0.5f);
            backdropRect.anchorMax = new Vector2(0.5f, 0.5f);
            backdropRect.anchoredPosition = Vector2.zero;
        }

        backdropRect.pivot = new Vector2(0.5f, 0.5f);
        diaryBackdropImage.preserveAspect = true;
        diaryBackdropImage.raycastTarget = false;
        diaryBackdropImage.enabled = isVisible && diaryBackdropImage.sprite != null;
    }

    private static void ApplyImage(Image image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private void RestartAnimation()
    {
        currentAnimationFrameIndex = 0;
        animationFrameTimer = 0f;
        animationLoopIntervalTimer = 0f;

        if (animationImage == null)
        {
            return;
        }

        if (currentAnimationFrames == null || currentAnimationFrames.Length == 0)
        {
            animationImage.enabled = false;
            animationImage.sprite = null;
            return;
        }

        animationImage.enabled = true;
        animationImage.sprite = currentAnimationFrames[0];
    }

    private void UpdateAnimation()
    {
        if (!isVisible || animationImage == null || !animationImage.enabled ||
            currentAnimationFrames == null || currentAnimationFrames.Length <= 1)
        {
            return;
        }

        if (animationLoopIntervalTimer > 0f)
        {
            animationLoopIntervalTimer = Mathf.Max(0f, animationLoopIntervalTimer - Time.unscaledDeltaTime);
            return;
        }

        float frameDuration = 1f / Mathf.Max(1f, currentAnimationFramesPerSecond);
        animationFrameTimer += Time.unscaledDeltaTime;
        while (animationFrameTimer >= frameDuration)
        {
            animationFrameTimer -= frameDuration;
            currentAnimationFrameIndex++;
            if (currentAnimationFrameIndex >= currentAnimationFrames.Length)
            {
                currentAnimationFrameIndex = 0;
                animationLoopIntervalTimer = currentAnimationLoopIntervalSeconds;
            }

            animationImage.sprite = currentAnimationFrames[currentAnimationFrameIndex];
            if (animationLoopIntervalTimer > 0f)
            {
                animationFrameTimer = 0f;
                break;
            }
        }
    }

    private void SetVisible(bool visible)
    {
        isVisible = visible;
        if (panelRoot != null && panelRoot.activeSelf != visible)
        {
            panelRoot.SetActive(visible);
        }

        RefreshDiaryBackdropImage();
    }

    private bool ValidateRequiredReferences(float autoCloseSecondsWhenNoButton)
    {
        ResolveMissingReferences();

        if (panelRoot != null && (closeButton != null || closeOnSpace || autoCloseSecondsWhenNoButton > 0f))
        {
            return true;
        }

        Debug.LogError(
            "[EventPanelPresenter] Required references are missing. " +
            $"panelRoot='{panelRoot}', closeButton='{closeButton}', " +
            $"autoCloseSecondsWhenNoButton={autoCloseSecondsWhenNoButton}",
            this);

#if UNITY_EDITOR
        Debug.Break();
#endif
        return false;
    }

    private void ResolveMissingReferences()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (closeButton == null)
        {
            closeButton = GetComponentInChildren<Button>(includeInactive: true);
        }

        TMP_Text[] labels = null;
        if (titleLabel == null)
        {
            titleLabel = FindLabelByName(ref labels, "Title", "TitleLabel", "DiaryTitle");
        }

        if (bodyLabel == null)
        {
            bodyLabel = FindLabelByName(ref labels, "Body", "BodyLabel", "Content", "DiaryBody", "DiaryContent");
        }

        if (bodyLabel == null)
        {
            bodyLabel = FindFirstBodyCandidate(ref labels);
        }

        if (closeButtonLabel == null && closeButton != null)
        {
            closeButtonLabel = closeButton.GetComponentInChildren<TMP_Text>(includeInactive: true);
        }

        Image[] images = null;
        if (illustrationImage == null)
        {
            illustrationImage = FindImageByName(ref images, "Illustration", "IllustrationImage", "DiaryImage");
        }

        if (animationImage == null)
        {
            animationImage = FindImageByName(ref images, "Animation", "AnimationImage", "GifImage");
        }

        if (diaryBackdropImage == null)
        {
            diaryBackdropImage = FindImageByName(
                ref images,
                "DiaryBackdrop",
                "DiaryBackdropImage",
                "Backdrop",
                "BackdropImage",
                "BackgroundImage");
        }

        ScrollRect[] scrollRects = null;
        if (bodyScrollRect == null)
        {
            bodyScrollRect = FindScrollRectByName(
                ref scrollRects,
                "DiaryBodyScroll",
                "DiaryBodyScrollView",
                "BodyScroll",
                "BodyScrollView");
        }

        if (bodyScrollRect != null)
        {
            ResolveBodyScrollReferences();
        }

        RefreshDiaryBackdropImage();
    }

    private TMP_Text FindLabelByName(ref TMP_Text[] labels, params string[] names)
    {
        if (labels == null)
        {
            labels = GetComponentsInChildren<TMP_Text>(includeInactive: true);
        }

        for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
        {
            string targetName = names[nameIndex];
            for (int i = 0; i < labels.Length; i++)
            {
                TMP_Text label = labels[i];
                if (label != null && string.Equals(label.name, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return label;
                }
            }
        }

        return null;
    }

    private TMP_Text FindFirstBodyCandidate(ref TMP_Text[] labels)
    {
        if (labels == null)
        {
            labels = GetComponentsInChildren<TMP_Text>(includeInactive: true);
        }

        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];
            if (label == null || label == titleLabel || label == closeButtonLabel)
            {
                continue;
            }

            if (closeButton != null && label.transform.IsChildOf(closeButton.transform))
            {
                continue;
            }

            return label;
        }

        return null;
    }

    private Image FindImageByName(ref Image[] images, params string[] names)
    {
        if (images == null)
        {
            images = GetComponentsInChildren<Image>(includeInactive: true);
        }

        for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
        {
            string targetName = names[nameIndex];
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image != null && string.Equals(image.name, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return image;
                }
            }
        }

        return null;
    }

    private ScrollRect FindScrollRectByName(ref ScrollRect[] scrollRects, params string[] names)
    {
        if (scrollRects == null)
        {
            scrollRects = GetComponentsInChildren<ScrollRect>(includeInactive: true);
        }

        for (int nameIndex = 0; nameIndex < names.Length; nameIndex++)
        {
            string targetName = names[nameIndex];
            for (int i = 0; i < scrollRects.Length; i++)
            {
                ScrollRect scrollRect = scrollRects[i];
                if (scrollRect != null && string.Equals(scrollRect.name, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return scrollRect;
                }
            }
        }

        return null;
    }
}
