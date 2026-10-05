using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One persistent, non-interactive notice for successful automatic saves.</summary>
public sealed class AutoSaveNotification : MonoBehaviour
{
    private static AutoSaveNotification instance;
    private Canvas notificationCanvas;
    private CanvasGroup canvasGroup;
    private RectTransform labelRect;
    private float visibleUntil;
    private float displaySeconds = 2.5f;
    private float fadeSeconds = 0.5f;

    public static void Show()
    {
        if (!Application.isPlaying) return;
        if (instance == null)
        {
            var root = new GameObject(nameof(AutoSaveNotification), typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            DontDestroyOnLoad(root);
            instance = root.AddComponent<AutoSaveNotification>();
            instance.CreateView();
        }

        // Consecutive saves extend the same notice instead of stacking messages.
        instance.visibleUntil = Time.unscaledTime + instance.displaySeconds;
        instance.canvasGroup.alpha = 1f;
        instance.notificationCanvas.enabled = true;
        instance.UpdateSafeArea();
    }

    private void CreateView()
    {
        var settings = Resources.Load<AutoSaveNotificationSettings>("UI/AutoSaveNotificationSettings");
        if (settings != null)
        {
            displaySeconds = Mathf.Max(0f, settings.displaySeconds);
            fadeSeconds = Mathf.Max(0f, settings.fadeSeconds);
        }

        notificationCanvas = GetComponent<Canvas>();
        notificationCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        notificationCanvas.sortingOrder = 2000;
        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        var labelObject = new GameObject("SaveMessage", typeof(RectTransform));
        labelObject.transform.SetParent(transform, false);
        labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.pivot = Vector2.zero;
        labelRect.anchoredPosition = new Vector2(40f, 36f);
        labelRect.sizeDelta = new Vector2(360f, 48f);
        var label = labelObject.AddComponent<TextMeshProUGUI>();
        if (settings != null && settings.font != null) label.font = settings.font;
        label.text = "セーブしました";
        label.fontSize = 28f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.BottomLeft;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        label.outlineWidth = 0.15f;
        label.outlineColor = new Color32(0, 0, 0, 220);
    }

    private void Update()
    {
        if (notificationCanvas == null || !notificationCanvas.enabled) return;
        UpdateSafeArea();
        float fadeElapsed = Time.unscaledTime - visibleUntil;
        if (fadeElapsed < 0f) return;
        canvasGroup.alpha = fadeSeconds > 0f ? Mathf.Clamp01(1f - fadeElapsed / fadeSeconds) : 0f;
        if (canvasGroup.alpha <= 0f) notificationCanvas.enabled = false;
    }

    private void UpdateSafeArea()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        Vector2 bottomLeft = new Vector2(Screen.safeArea.xMin / Screen.width, Screen.safeArea.yMin / Screen.height);
        labelRect.anchorMin = labelRect.anchorMax = bottomLeft;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
