using System;
using System.Collections;
using TMPro;
using UnityEngine;
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
    [SerializeField] private Image illustrationImage;
    [SerializeField] private Image animationImage;

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

    public bool IsVisible => isVisible;

    private void Awake()
    {
        isVisible = panelRoot != null && panelRoot.activeSelf;
    }

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
        UpdateAnimation();
    }

    public bool Show(EventPanelContent content, Action closeCallback = null, float autoCloseSecondsWhenNoButton = 0f)
    {
        if (!ValidateRequiredReferences(autoCloseSecondsWhenNoButton))
        {
            return false;
        }

        StopAutoCloseRoutine();
        onClosed = closeCallback;
        ApplyContent(content);
        SetVisible(true);

        if (closeButton == null)
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
        ApplyImage(illustrationImage, content.illustration);

        currentAnimationFrames = content.animationFrames ?? Array.Empty<Sprite>();
        currentAnimationFramesPerSecond = content.animationFramesPerSecond > 0f
            ? content.animationFramesPerSecond
            : 12f;
        currentAnimationLoopIntervalSeconds = Mathf.Max(0f, content.animationLoopIntervalSeconds);
        RestartAnimation();
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
        return string.IsNullOrWhiteSpace(content.closeLabel) ? string.Empty : content.closeLabel.Trim();
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label == null)
        {
            return;
        }

        label.text = value ?? string.Empty;
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
    }

    private bool ValidateRequiredReferences(float autoCloseSecondsWhenNoButton)
    {
        if (panelRoot != null && (closeButton != null || autoCloseSecondsWhenNoButton > 0f))
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
}
