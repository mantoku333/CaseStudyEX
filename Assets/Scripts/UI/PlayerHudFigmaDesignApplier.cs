using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public sealed class PlayerHudFigmaDesignApplier : MonoBehaviour
{
    private const float CanvasLeft = -960f;
    private const float CanvasTop = 540f;

    [Header("Apply")]
    [SerializeField] private bool repairMissingPartsOnAwake = true;
    [SerializeField] private bool applyOnAwake;

    [Header("HP Bar")]
    [SerializeField] private Sprite hpFrameSprite;
    [SerializeField] private Sprite hpBackSprite;
    [SerializeField] private Sprite hpFillSprite;
    [SerializeField] private Sprite hpIconSprite;
    [SerializeField] private Vector2 playerHpPosition = new Vector2(-926f, 526f);
    [SerializeField] private Vector2 playerHpSize = new Vector2(591f, 202f);
    [SerializeField] private Vector2 hpBarPosition = new Vector2(116f, -61f);
    [SerializeField] private Vector2 hpBarSize = new Vector2(475f, 42f);
    [SerializeField] private Vector2 hpFramePosition = Vector2.zero;
    [SerializeField] private Vector2 hpFrameSize = new Vector2(475f, 42f);
    [SerializeField] private Vector2 hpBackPosition = new Vector2(76f, -10f);
    [SerializeField] private Vector2 hpBackSize = new Vector2(383f, 22f);
    [SerializeField] private Vector2 hpFillPosition = new Vector2(82f, -15f);
    [SerializeField] private Vector2 hpFillSize = new Vector2(370f, 13f);
    [SerializeField] private Vector2 hpIconPosition = new Vector2(45f, -10f);
    [SerializeField] private Vector2 hpIconSize = new Vector2(24f, 23f);
    [SerializeField] private int hpSortingOrder = 40;
    [SerializeField] private int hpBackSortingOrder = 41;
    [SerializeField] private int hpFillSortingOrder = 42;
    [SerializeField] private int hpIconSortingOrder = 43;

    [Header("Elegant Bar")]
    [SerializeField] private Sprite elegantFrameSprite;
    [SerializeField] private Sprite elegantBackSprite;
    [SerializeField] private Sprite elegantFillSprite;
    [SerializeField] private Sprite elegantIconSprite;
    [SerializeField] private Vector2 elegantBarPosition = new Vector2(117f, -106f);
    [SerializeField] private Vector2 elegantBarSize = new Vector2(441f, 38f);
    [SerializeField] private Vector2 elegantBackPosition = new Vector2(75f, -9f);
    [SerializeField] private Vector2 elegantBackSize = new Vector2(282f, 19f);
    [SerializeField] private Vector2 elegantFillPosition = new Vector2(81f, -12f);
    [SerializeField] private Vector2 elegantFillSize = new Vector2(271f, 13f);
    [SerializeField] private Vector2 elegantIconPosition = new Vector2(45f, -7f);
    [SerializeField] private Vector2 elegantIconSize = new Vector2(23f, 23f);
    [SerializeField] private Vector2 elegantTextPosition = new Vector2(370f, 0f);
    [SerializeField] private Vector2 elegantTextSize = new Vector2(55f, 35f);
    [SerializeField] private float elegantTextFontSize = 24f;
    [SerializeField] private Color elegantTextColor = new Color(0.8196079f, 0.5137255f, 0.9490197f, 1f);
    [SerializeField] private int elegantSortingOrder = 35;

    [Header("Portrait Icon Sorting")]
    [SerializeField] private int portraitPathSortingOrder = 50;
    [SerializeField] private int portraitBackgroundSortingOrder = 60;
    [SerializeField] private int portraitCharacterSortingOrder = 75;
    [SerializeField] private int portraitFrameSortingOrder = 90;
    [SerializeField] private int portraitRibbonSortingOrder = 100;

    private void Awake()
    {
        if (repairMissingPartsOnAwake)
        {
            RepairMissingParts();
        }

        if (applyOnAwake)
        {
            Apply();
        }
    }

    [ContextMenu("Apply Figma HUD Design")]
    public void Apply()
    {
        RectTransform playerHp = FindRect(transform, "Player HP");
        if (playerHp == null)
        {
            return;
        }

        ConfigureRootRect(playerHp, playerHpPosition, playerHpSize);
        ApplyHpBar(playerHp);
        ApplyElegantBar(playerHp);
    }

    [ContextMenu("Repair Missing HUD Parts")]
    public void RepairMissingParts()
    {
        RectTransform playerHp = FindRect(transform, "Player HP");
        if (playerHp == null)
        {
            return;
        }

        RectTransform hpRoot = FindRect(playerHp, "HP");
        if (hpRoot != null)
        {
            EnsureSortingCanvas(hpRoot, hpSortingOrder);
            RepairImage(hpRoot, "HP frame 1", hpFrameSprite, hpFramePosition, hpFrameSize);
            Image back = RepairImage(hpRoot, "HP Back 3", hpBackSprite, hpBackPosition, hpBackSize);
            Image fill = RepairImage(hpRoot, "HP full 1", hpFillSprite, hpFillPosition, hpFillSize);
            Image icon = RepairImage(hpRoot, "HP icon 1", hpIconSprite, hpIconPosition, hpIconSize);
            EnsureSortingCanvas(back.rectTransform, hpBackSortingOrder);
            EnsureSortingCanvas(fill.rectTransform, hpFillSortingOrder);
            EnsureSortingCanvas(icon.rectTransform, hpIconSortingOrder);
        }

        RectTransform elegantRoot = FindRect(playerHp, "Elegant Point Gauge");
        if (elegantRoot != null)
        {
            EnsureSortingCanvas(elegantRoot, elegantSortingOrder);
            Image frame = elegantRoot.GetComponent<Image>();
            if (frame != null)
            {
                RepairImage(frame, elegantFrameSprite);
            }

            RepairImage(elegantRoot, "HP Back 3", elegantBackSprite, elegantBackPosition, elegantBackSize);
            RepairImage(elegantRoot, "Fill", elegantFillSprite, elegantFillPosition, elegantFillSize);
            RepairImage(elegantRoot, "優雅 icon 1", elegantIconSprite, elegantIconPosition, elegantIconSize);
        }

        RepairPortraitIconSorting(playerHp);
    }

    private void RepairPortraitIconSorting(RectTransform playerHp)
    {
        EnsureSortingCanvasIfFound(playerHp, "(Path)1 1", portraitPathSortingOrder);
        EnsureSortingCanvasIfFound(playerHp, "Icon back ground1", portraitBackgroundSortingOrder);
        EnsureSortingCanvasIfFound(playerHp, "Iris_tatie01 1", portraitCharacterSortingOrder);
        EnsureSortingCanvasIfFound(playerHp, "Icon Frame 1", portraitFrameSortingOrder);
        EnsureSortingCanvasIfFound(playerHp, "ribbon 1", portraitRibbonSortingOrder);
    }

    private void ApplyHpBar(RectTransform playerHp)
    {
        RectTransform hpRoot = FindRect(playerHp, "HP");
        if (hpRoot == null)
        {
            hpRoot = CreateImageObject("HP", playerHp).rectTransform;
        }

        ConfigureRect(hpRoot, hpBarPosition, hpBarSize);
        EnsureSortingCanvas(hpRoot, hpSortingOrder);

        Image frame = EnsureImage(hpRoot, "HP frame 1");
        ConfigureRect(frame.rectTransform, hpFramePosition, hpFrameSize);
        ConfigureImage(frame, hpFrameSprite);
        frame.transform.SetSiblingIndex(0);

        DisableLegacyObject(hpRoot, "HP Back 1");

        Image back = EnsureImage(hpRoot, "HP Back 3");
        ConfigureRect(back.rectTransform, hpBackPosition, hpBackSize);
        ConfigureImage(back, hpBackSprite);
        EnsureSortingCanvas(back.rectTransform, hpBackSortingOrder);
        back.transform.SetSiblingIndex(1);

        Image fill = EnsureImage(hpRoot, "HP full 1");
        ConfigureRect(fill.rectTransform, hpFillPosition, hpFillSize);
        ConfigureImage(fill, hpFillSprite);
        EnsureSortingCanvas(fill.rectTransform, hpFillSortingOrder);
        fill.transform.SetSiblingIndex(2);

        Image icon = EnsureImage(hpRoot, "HP icon 1");
        ConfigureRect(icon.rectTransform, hpIconPosition, hpIconSize);
        ConfigureImage(icon, hpIconSprite);
        EnsureSortingCanvas(icon.rectTransform, hpIconSortingOrder);
        icon.transform.SetSiblingIndex(3);
    }

    private void ApplyElegantBar(RectTransform playerHp)
    {
        RectTransform elegantRoot = FindRect(playerHp, "Elegant Point Gauge");
        if (elegantRoot == null)
        {
            elegantRoot = CreateImageObject("Elegant Point Gauge", playerHp).rectTransform;
        }

        ConfigureRect(elegantRoot, elegantBarPosition, elegantBarSize);
        EnsureSortingCanvas(elegantRoot, elegantSortingOrder);

        Image frame = elegantRoot.GetComponent<Image>();
        if (frame == null)
        {
            frame = elegantRoot.gameObject.AddComponent<Image>();
        }
        ConfigureImage(frame, elegantFrameSprite);
        elegantRoot.SetSiblingIndex(3);

        Image back = EnsureImage(elegantRoot, "HP Back 3");
        ConfigureRect(back.rectTransform, elegantBackPosition, elegantBackSize);
        ConfigureImage(back, elegantBackSprite);
        back.transform.SetSiblingIndex(0);

        Image fill = EnsureImage(elegantRoot, "Fill");
        ConfigureRect(fill.rectTransform, elegantFillPosition, elegantFillSize);
        ConfigureImage(fill, elegantFillSprite);
        fill.transform.SetSiblingIndex(1);

        Image icon = EnsureImage(elegantRoot, "優雅 icon 1");
        ConfigureRect(icon.rectTransform, elegantIconPosition, elegantIconSize);
        ConfigureImage(icon, elegantIconSprite);
        icon.transform.SetSiblingIndex(2);

        RectTransform balance = FindRect(elegantRoot, "Balance");
        if (balance != null)
        {
            balance.SetSiblingIndex(3);
            ConfigureRect(balance, elegantTextPosition, elegantTextSize);
            TMP_Text text = balance.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.alignment = TextAlignmentOptions.Left;
                text.color = elegantTextColor;
                text.fontSize = elegantTextFontSize;
            }
        }
    }

    private static RectTransform FindRect(Transform root, string objectName)
    {
        Transform found = FindChild(root, objectName);
        return found != null ? found as RectTransform : null;
    }

    private static Transform FindChild(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Image EnsureImage(RectTransform parent, string objectName)
    {
        RectTransform rect = FindRect(parent, objectName);
        Image image = rect != null ? rect.GetComponent<Image>() : null;
        if (image != null)
        {
            return image;
        }

        return CreateImageObject(objectName, parent);
    }

    private static Image CreateImageObject(string objectName, Transform parent)
    {
        var gameObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.layer = parent.gameObject.layer;
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static Canvas EnsureSortingCanvas(RectTransform rect, int sortingOrder)
    {
        Canvas canvas = rect.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = rect.gameObject.AddComponent<Canvas>();
        }

        canvas.enabled = true;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;
        return canvas;
    }

    private static void EnsureSortingCanvasIfFound(Transform root, string objectName, int sortingOrder)
    {
        RectTransform rect = FindRect(root, objectName);
        if (rect != null)
        {
            EnsureSortingCanvas(rect, sortingOrder);
        }
    }

    private static Image RepairImage(RectTransform parent, string objectName, Sprite sprite, Vector2 fallbackPosition, Vector2 fallbackSize)
    {
        RectTransform rect = FindRect(parent, objectName);
        bool created = rect == null;
        Image image = created ? CreateImageObject(objectName, parent) : rect.GetComponent<Image>();
        if (image == null)
        {
            image = rect.gameObject.AddComponent<Image>();
        }

        if (created)
        {
            ConfigureRect(image.rectTransform, fallbackPosition, fallbackSize);
        }

        RepairImage(image, sprite);
        return image;
    }

    private static void RepairImage(Image image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.enabled = true;
        image.raycastTarget = false;
        if (image.sprite == null && sprite != null)
        {
            image.sprite = sprite;
        }

        Color color = image.color;
        if (color.a <= 0.001f)
        {
            color.a = 1f;
            image.color = color;
        }
    }

    private static void ConfigureRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void ConfigureRootRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void DisableLegacyObject(RectTransform root, string objectName)
    {
        Transform found = FindChild(root, objectName);
        if (found != null)
        {
            found.gameObject.SetActive(false);
        }
    }

    private static void ConfigureImage(Image image, Sprite sprite)
    {
        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
    }

    public static Vector2 ToCanvasPosition(float figmaX, float figmaY, float pageX = -265f, float pageY = -50f)
    {
        return new Vector2(
            CanvasLeft + figmaX - pageX,
            CanvasTop - (figmaY - pageY));
    }
}
