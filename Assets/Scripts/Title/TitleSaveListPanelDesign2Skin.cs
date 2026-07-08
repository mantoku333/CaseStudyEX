using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class TitleSaveListPanelDesign2Skin : MonoBehaviour
{
    private const string ChromeName = "Design2Chrome";
    private const float ReferenceWidth = 1998f;
    private const float ReferenceHeight = 1080f;

    private static readonly Vector2 SlotSize = new Vector2(1018f, 142f);
    private static readonly Vector2 ThumbnailSize = new Vector2(192f, 107f);
    private static readonly Color PanelBlack = new Color32(35, 35, 37, 255);
    private static readonly Color SlotHighlight = new Color32(96, 78, 146, 245);

    [SerializeField] private TitleSceneController controller;
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite slotNormalSprite;
    [SerializeField] private Sprite slotSelectedSprite;
    [SerializeField] private Sprite backButtonSprite;
    [SerializeField] private Sprite rightPanelSprite;
    [SerializeField] private Sprite previewFrameSprite;
    [SerializeField] private Sprite titleTextSprite;

    private RectTransform panelRect;
    private TMP_FontAsset fontAsset;
    private TMP_Text previewTitle;
    private TMP_Text previewBody;
    private Image previewThumbnail;
    private bool built;
    private bool forceRebuild;

    public void Initialize(TitleSceneController sceneController)
    {
        controller = sceneController;
        Build();
        ApplySlotLayout();
        RefreshPreviewFromLatestSave();
    }

    private void Awake()
    {
        ResolveController();
        Build();
        ApplySlotLayout();
    }

    private void OnEnable()
    {
        ResolveController();
        Build();
        ApplySlotLayout();
        RefreshPreviewFromLatestSave();
    }

    private void OnValidate()
    {
        ResolveController();
    }

    private void ResolveController()
    {
        if (controller == null)
        {
            controller = FindFirstObjectByType<TitleSceneController>(FindObjectsInactive.Include);
        }
    }

    [ContextMenu("Rebuild Design2 Save List")]
    private void RebuildDesign2SaveList()
    {
        built = false;
        forceRebuild = true;
        Build();
        forceRebuild = false;
        ApplySlotLayout();
        RefreshPreviewFromLatestSave();
    }

    private void Build()
    {
        if (built)
        {
            return;
        }

        panelRect = transform as RectTransform;
        if (panelRect == null)
        {
            return;
        }

        fontAsset = GetComponentInChildren<TMP_Text>(true)?.font;
        Image panelImage = GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = PanelBlack;
            panelImage.raycastTarget = true;
        }

        RectTransform chrome = transform.Find(ChromeName) as RectTransform;
        if (chrome == null)
        {
            chrome = CreateRect(ChromeName, transform);
            Stretch(chrome);
            chrome.SetAsFirstSibling();
        }

        bool needsImportedChrome = forceRebuild || chrome.Find("ImportedBackground") == null || chrome.Find("ImportedTitleText") == null;
        if (!needsImportedChrome)
        {
            CacheChromeReferences(chrome);
            StyleBackButton();
            built = true;
            return;
        }

        ClearChildren(chrome);

        Image background = CreateImage("ImportedBackground", chrome, backgroundSprite, PanelBlack);
        SetRect(background.rectTransform, 0f, 0f, 1920f, 1080f, new Vector2(0f, 1f));

        Image title = CreateImage("ImportedTitleText", chrome, titleTextSprite, Color.white);
        SetRect(title.rectTransform, 58f, 31f, 339f, 53f, new Vector2(0f, 1f));

        Image rightPanel = CreateImage("ImportedRightPanel", chrome, rightPanelSprite, new Color32(57, 53, 75, 255));
        SetRect(rightPanel.rectTransform, 1178f, 157f, 678f, 759f, new Vector2(0f, 1f));

        TMP_Text footer = CreateText("SaveDataFooter", chrome, "ロードするデータを選択してください", 30f, new Color32(183, 185, 143, 255), TextAlignmentOptions.Left);
        SetRect(footer.rectTransform, 88f, 952f, 760f, 48f, new Vector2(0f, 1f));

        Image previewFrame = CreateImage("ImportedPreviewFrame", chrome, previewFrameSprite, new Color32(108, 105, 118, 255));
        SetRect(previewFrame.rectTransform, 1235f, 232f, 564f, 317f, new Vector2(0f, 1f));

        previewThumbnail = CreateImage("PreviewThumbnail", chrome, new Color32(108, 105, 118, 255));
        SetRect(previewThumbnail.rectTransform, 1235f, 232f, 564f, 317f, new Vector2(0f, 1f));
        previewThumbnail.preserveAspect = true;

        previewTitle = CreateText("PreviewTitle", chrome, "セーブデータなし", 56f, Color.white, TextAlignmentOptions.Left);
        SetRect(previewTitle.rectTransform, 1215f, 606f, 560f, 76f, new Vector2(0f, 1f));

        Image titleRule = CreateImage("PreviewTitleRule", chrome, new Color32(210, 207, 220, 255));
        SetRect(titleRule.rectTransform, 1215f, 679f, 400f, 2f, new Vector2(0f, 1f));

        previewBody = CreateText("PreviewBody", chrome, "ロードするデータを選択してください", 31f, Color.white, TextAlignmentOptions.Left);
        SetRect(previewBody.rectTransform, 1215f, 720f, 560f, 130f, new Vector2(0f, 1f));

        StyleBackButton();
        built = true;
    }

    private void CacheChromeReferences(Transform chrome)
    {
        previewThumbnail = FindChild(chrome, "PreviewThumbnail")?.GetComponent<Image>();
        previewTitle = FindChild(chrome, "PreviewTitle")?.GetComponent<TMP_Text>();
        previewBody = FindChild(chrome, "PreviewBody")?.GetComponent<TMP_Text>();
    }

    private void ApplySlotLayout()
    {
        RectTransform scrollView = FindChild(transform, "Scroll View") as RectTransform;
        RectTransform viewport = FindChild(transform, "Viewport") as RectTransform;
        RectTransform content = FindChild(transform, "Content") as RectTransform;

        if (scrollView != null)
        {
            SetRect(scrollView, 85f, 178f, 1030f, 777f, new Vector2(0f, 1f));
            scrollView.localScale = Vector3.one;
            SetTransparent(scrollView.GetComponent<Image>());
            ScrollRect scrollRect = scrollView.GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.scrollSensitivity = 35f;
                scrollRect.verticalScrollbar = null;
                scrollRect.horizontalScrollbar = null;
                if (viewport != null)
                {
                    scrollRect.viewport = viewport;
                }

                if (content != null)
                {
                    scrollRect.content = content;
                }
            }

            HideScrollbars(scrollView);
        }

        if (viewport != null)
        {
            Stretch(viewport);
            ConfigureViewportMask(viewport);
        }

        if (content != null)
        {
            content.localScale = Vector3.one;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, content.sizeDelta.y);

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.padding = new RectOffset(0, 12, 0, 0);
                layout.spacing = 17f;
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        TitleSaveSlotView[] slots = GetComponentsInChildren<TitleSaveSlotView>(true);
        Array.Sort(slots, (a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
        for (int i = 0; i < slots.Length; i++)
        {
            StyleSlot(slots[i], i);
        }

        if (content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
    }

    private void StyleSlot(TitleSaveSlotView slot, int siblingIndex)
    {
        if (slot == null)
        {
            return;
        }

        RectTransform slotRect = slot.transform as RectTransform;
        if (slotRect != null)
        {
            slotRect.SetSiblingIndex(siblingIndex);
            slotRect.localScale = Vector3.one;
            slotRect.sizeDelta = SlotSize;
        }

        LayoutElement layoutElement = slot.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = slot.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.minHeight = SlotSize.y;
        layoutElement.preferredHeight = SlotSize.y;
        layoutElement.minWidth = SlotSize.x;
        layoutElement.preferredWidth = SlotSize.x;
        layoutElement.flexibleHeight = 0f;
        layoutElement.flexibleWidth = 0f;

        Image image = slot.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = slotNormalSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
            image.raycastTarget = true;
        }

        Outline outline = slot.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = false;
        }

        HorizontalLayoutGroup horizontal = slot.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null)
        {
            horizontal.padding = new RectOffset(20, 24, 18, 17);
            horizontal.spacing = 20f;
            horizontal.childAlignment = TextAnchor.MiddleLeft;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;
        }

        slot.ApplyDesign2Layout(ThumbnailSize);
        StyleText(slot.SavedAtLabel, 29f, new Color32(225, 222, 236, 255), TextAlignmentOptions.Left);
        StyleText(slot.StageNameLabel, 47f, Color.white, TextAlignmentOptions.Left);

        Button button = slot.Button;
        if (button != null)
        {
            button.transition = Selectable.Transition.ColorTint;
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = SlotHighlight;
            colors.selectedColor = SlotHighlight;
            colors.pressedColor = new Color32(121, 99, 166, 255);
            colors.disabledColor = new Color32(255, 255, 255, 120);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            SpriteState spriteState = button.spriteState;
            spriteState.highlightedSprite = slotSelectedSprite;
            spriteState.selectedSprite = slotSelectedSprite;
            spriteState.pressedSprite = slotSelectedSprite;
            button.spriteState = spriteState;
            OptionsCanvasButtonUtility.ConfigurePressOnlyFeedback(button);

            if (Application.isPlaying)
            {
                TitleSaveSlotPreviewRelay relay = slot.GetComponent<TitleSaveSlotPreviewRelay>();
                if (relay == null)
                {
                    relay = slot.gameObject.AddComponent<TitleSaveSlotPreviewRelay>();
                }

                relay.Configure(this, button, slot.SlotIndex);
            }
        }
    }

    private void RefreshPreviewFromLatestSave()
    {
        if (SaveManager.TryGetLatestSaveSlot(out int slotIndex, out SaveSlotMeta slotMeta) && slotMeta.HasSave && !slotMeta.IsCorrupted)
        {
            RefreshPreview(slotIndex);
            return;
        }

        RefreshPreview(SaveManager.DefaultSlotIndex);
    }

    internal void RefreshPreview(int slotIndex)
    {
        SaveSlotMeta slotMeta = SaveManager.GetSlotMeta(slotIndex);
        if (!slotMeta.HasSave || slotMeta.IsCorrupted)
        {
            SetPreviewText(slotMeta.IsCorrupted ? "読み込み不可" : "セーブデータなし", "ロードするデータを選択してください");
            SetPreviewThumbnail(controller != null ? controller.GetEmptySlotThumbnail() : null);
            return;
        }

        string stageName = controller != null ? controller.GetStageDisplayName(slotMeta.SceneName, slotMeta.LocationId) : slotMeta.SceneName;
        string savedAt = FormatSavedAt(slotMeta.SavedAtUtc);
        SetPreviewText(stageName, $"保存日時  {savedAt}\nロードするデータを選択してください");
        SetPreviewThumbnail(controller != null ? controller.GetStageThumbnail(slotMeta.SceneName, slotMeta.LocationId) : null);
    }

    private void SetPreviewText(string title, string body)
    {
        if (previewTitle != null)
        {
            previewTitle.text = title;
        }

        if (previewBody != null)
        {
            previewBody.text = body;
        }
    }

    private void SetPreviewThumbnail(Sprite sprite)
    {
        if (previewThumbnail == null)
        {
            return;
        }

        previewThumbnail.sprite = sprite;
        previewThumbnail.color = sprite != null ? Color.white : new Color32(108, 105, 118, 255);
        previewThumbnail.preserveAspect = true;
    }

    private void StyleBackButton()
    {
        RectTransform back = FindChild(transform, "Back") as RectTransform;
        if (back == null)
        {
            return;
        }

        SetRect(back, 1785f, 5f, 113f, 113f, new Vector2(0f, 1f));
        back.localScale = Vector3.one;
        Image image = back.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = backButtonSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
            image.raycastTarget = true;
        }

        Outline outline = back.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = false;
        }

        Button button = back.GetComponent<Button>();
        if (button != null)
        {
            OptionsCanvasButtonUtility.ConfigureSingleIllustrationButton(button);
        }

        TMP_Text label = back.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.gameObject.SetActive(false);
        }
    }

    private static string FormatSavedAt(string savedAtUtc)
    {
        if (DateTime.TryParse(
                savedAtUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime savedAt))
        {
            return savedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
        }

        return "--/-- --:--";
    }

    private TMP_Text CreateText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.layer = gameObject.layer;
        obj.transform.SetParent(parent, false);
        TMP_Text label = obj.GetComponent<TMP_Text>();
        label.text = text;
        if (fontAsset != null)
        {
            label.font = fontAsset;
        }

        StyleText(label, size, color, alignment);
        return label;
    }

    private static void StyleText(TMP_Text label, float size, Color color, TextAlignmentOptions alignment)
    {
        if (label == null)
        {
            return;
        }

        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis;
    }

    private Image CreateImage(string name, Transform parent, Color color)
    {
        return CreateImage(name, parent, null, color);
    }

    private Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.layer = gameObject.layer;
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.layer = parent.gameObject.layer;
        obj.transform.SetParent(parent, false);
        return (RectTransform)obj.transform;
    }

    private static void SetTransparent(Image image)
    {
        if (image == null)
        {
            return;
        }

        image.color = new Color(1f, 1f, 1f, 0f);
        image.raycastTarget = false;
    }

    private static void ConfigureViewportMask(RectTransform viewport)
    {
        Image image = viewport.GetComponent<Image>();
        if (image != null)
        {
            image.color = Color.white;
            image.raycastTarget = false;
        }

        Mask mask = viewport.GetComponent<Mask>();
        if (mask != null)
        {
            mask.showMaskGraphic = false;
        }
    }

    private static void HideScrollbars(Transform root)
    {
        Scrollbar[] scrollbars = root.GetComponentsInChildren<Scrollbar>(true);
        for (int i = 0; i < scrollbars.Length; i++)
        {
            scrollbars[i].gameObject.SetActive(false);
        }
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
    }

    private static void SetRect(RectTransform rect, float left, float top, float width, float height, Vector2 pivot)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = pivot;
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(left - (ReferenceWidth * 0.5f) + (width * pivot.x), (ReferenceHeight * 0.5f) - top - (height * (1f - pivot.y)));
        rect.localScale = Vector3.one;
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindChild(root.GetChild(i), name);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }
}

internal sealed class TitleSaveSlotPreviewRelay : MonoBehaviour
{
    private TitleSaveListPanelDesign2Skin skin;
    private Button button;
    private int slotIndex;

    public void Configure(TitleSaveListPanelDesign2Skin owner, Button targetButton, int targetSlotIndex)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
        }

        skin = owner;
        button = targetButton;
        slotIndex = targetSlotIndex;

        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
            button.onClick.AddListener(OnClick);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
        }
    }

    private void OnClick()
    {
        if (skin != null)
        {
            skin.RefreshPreview(slotIndex);
        }
    }
}
