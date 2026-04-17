using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class StoryOverlayFader : MonoBehaviour
{
    private const string RuntimeObjectName = "[StoryOverlayFader]";

    private static StoryOverlayFader instance;

    private CanvasGroup canvasGroup;
    private Image overlayImage;

    public static StoryOverlayFader Instance
    {
        get
        {
            EnsureInstance();
            return instance;
        }
    }

    public float CurrentAlpha => canvasGroup != null ? canvasGroup.alpha : 0f;

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        var gameObject = new GameObject(RuntimeObjectName);
        DontDestroyOnLoad(gameObject);
        instance = gameObject.AddComponent<StoryOverlayFader>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
    }

    public IEnumerator FadeTo(float targetAlpha, float durationSeconds, Color color)
    {
        BuildOverlay();

        overlayImage.color = color;
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, durationSeconds);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    public void SetImmediate(float alpha, Color color)
    {
        BuildOverlay();
        overlayImage.color = color;
        canvasGroup.alpha = alpha;
    }

    private void BuildOverlay()
    {
        if (canvasGroup != null && overlayImage != null)
        {
            return;
        }

        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
        }

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (overlayImage == null)
        {
            var imageObject = new GameObject("Overlay");
            imageObject.transform.SetParent(transform, false);

            var rect = imageObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            overlayImage = imageObject.AddComponent<Image>();
            overlayImage.color = Color.black;
            overlayImage.raycastTarget = false;
        }
    }
}
