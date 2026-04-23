using GameName.UI;
using UnityEngine;
using UnityEngine.UI;

public static class MantokuStoryHudBootstrap
{
    private const string HudCanvasName = "PlayerHUDCanvas";
    private const string IconName = "Icon";
    private const string IconBackgroundName = "Icon (1)";
    private const string HealthBarBackName = "HPBar_Back";
    private const string HealthBarName = "HPBar";

    private const string IconSpritePath = "Assets/Art/Texture/iris_stamp.png";
    private const string IconBackgroundSpritePath = "Assets/Art/Texture/\u2014Pngtree\u2014hazy white glow_6016180.png";

    private static Sprite fallbackWhiteSprite;

    public static Canvas EnsureHudCanvasExists()
    {
        GameObject existing = GameObject.Find(HudCanvasName);
        if (existing != null && existing.TryGetComponent(out Canvas existingCanvas))
        {
            EnsureVisibilityGate(existing);
            EnsureHudElements((RectTransform)existingCanvas.transform);
            return existingCanvas;
        }

        GameObject canvasObject = new GameObject(
            HudCanvasName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(FlagCanvasGroupVisibility));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = (RectTransform)canvasObject.transform;
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        EnsureHudElements(canvasRect);
        return canvas;
    }

    private static void EnsureVisibilityGate(GameObject canvasObject)
    {
        if (!canvasObject.TryGetComponent(out CanvasGroup canvasGroup))
        {
            canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        }

        if (!canvasObject.TryGetComponent(out FlagCanvasGroupVisibility _))
        {
            canvasObject.AddComponent<FlagCanvasGroupVisibility>();
        }
    }

    private static void EnsureHudElements(RectTransform parent)
    {
        EnsureIconBackground(parent);
        EnsureIcon(parent);
        EnsureHealthBar(parent);
    }

    private static void EnsureIconBackground(RectTransform parent)
    {
        RectTransform iconBackgroundRect = FindOrCreateUiRect(IconBackgroundName, parent);
        iconBackgroundRect.anchorMin = new Vector2(0f, 1f);
        iconBackgroundRect.anchorMax = new Vector2(0f, 1f);
        iconBackgroundRect.pivot = new Vector2(0.5f, 0.5f);
        iconBackgroundRect.anchoredPosition = new Vector2(96.9248f, -87.42444f);
        iconBackgroundRect.sizeDelta = new Vector2(169f, 169f);

        Image iconBackgroundImage = GetOrAddImage(iconBackgroundRect);
        iconBackgroundImage.sprite = LoadHudSprite(IconBackgroundSpritePath);
        iconBackgroundImage.color = new Color(1f, 1f, 1f, 0.11764706f);
        iconBackgroundImage.preserveAspect = true;
        iconBackgroundImage.raycastTarget = false;
    }

    private static void EnsureIcon(RectTransform parent)
    {
        RectTransform iconRect = FindOrCreateUiRect(IconName, parent);
        iconRect.anchorMin = new Vector2(0f, 1f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(96.4f, -86.9f);
        iconRect.sizeDelta = new Vector2(100f, 100f);

        Image iconImage = GetOrAddImage(iconRect);
        iconImage.sprite = LoadHudSprite(IconSpritePath);
        iconImage.color = Color.white;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
    }

    // Mantoku only needs a small subset of the HUD, so we rebuild the exact pieces
    // we need here and keep the layout aligned with the existing prefab.
    private static void EnsureHealthBar(RectTransform parent)
    {
        RectTransform backRect = FindOrCreateUiRect(HealthBarBackName, parent);
        backRect.anchorMin = new Vector2(0.5f, 1f);
        backRect.anchorMax = new Vector2(0.5f, 1f);
        backRect.pivot = new Vector2(0.5f, 1f);
        backRect.anchoredPosition = new Vector2(-224.99f, -68f);
        backRect.sizeDelta = new Vector2(1177.2f, 23f);

        Image backImage = GetOrAddImage(backRect);
        backImage.color = new Color(0.57f, 0.57f, 0.57f, 0.61f);
        backImage.raycastTarget = false;

        RectTransform hpRect = FindOrCreateUiRect(HealthBarName, parent);
        hpRect.anchorMin = new Vector2(0.5f, 1f);
        hpRect.anchorMax = new Vector2(0.5f, 1f);
        hpRect.pivot = new Vector2(0.5f, 1f);
        hpRect.anchoredPosition = new Vector2(-288.26f, -68f);
        hpRect.sizeDelta = new Vector2(1035.1162f, 16.476f);

        Image hpImage = GetOrAddImage(hpRect);
        hpImage.color = new Color(1f, 1f, 1f, 0.396f);
        hpImage.raycastTarget = false;

        if (!hpRect.TryGetComponent(out HealthBarScaleView _))
        {
            hpRect.gameObject.AddComponent<HealthBarScaleView>();
        }
    }

    private static RectTransform FindOrCreateUiRect(string objectName, Transform parent)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
        {
            return (RectTransform)existing;
        }

        GameObject uiObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        uiObject.transform.SetParent(parent, false);
        return (RectTransform)uiObject.transform;
    }

    private static Image GetOrAddImage(RectTransform target)
    {
        if (!target.TryGetComponent(out Image image))
        {
            image = target.gameObject.AddComponent<Image>();
        }

        image.sprite = image.sprite != null ? image.sprite : GetFallbackWhiteSprite();
        return image;
    }

    private static Sprite LoadHudSprite(string assetPath)
    {
#if UNITY_EDITOR
        Sprite editorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (editorSprite != null)
        {
            return editorSprite;
        }
#endif
        return GetFallbackWhiteSprite();
    }

    private static Sprite GetFallbackWhiteSprite()
    {
        if (fallbackWhiteSprite != null)
        {
            return fallbackWhiteSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;

        fallbackWhiteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            1f);
        fallbackWhiteSprite.hideFlags = HideFlags.HideAndDontSave;
        return fallbackWhiteSprite;
    }
}
