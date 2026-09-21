using System;
using Metroidvania.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DecorationItemSlot : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerMoveHandler
{
    private const string DefaultPriceFormat = "{0} Pt";

    [SerializeField] private TMP_FontAsset shopFont;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject unknownOverlay;
    [SerializeField] private GameObject equippedBadge;
    [SerializeField] private GameObject selectedIndicator;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI unlockedDescriptionText;
    [SerializeField] private GameObject equippedStateRoot;
    [SerializeField] private GameObject lockedStateRoot;
    [SerializeField] private Image lockIconImage;
    [SerializeField] private TextMeshProUGUI lockedCostText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private string priceFormat = DefaultPriceFormat;
    [SerializeField] private Color lockedTint = new Color(0.45f, 0.45f, 0.45f, 1f);

    private ItemData itemData;
    private Action<DecorationItemSlot> onSelected;
    private Action<DecorationItemSlot, bool, Vector2> onLockedHoverChanged;
    private Button button;
    private bool isOwned;
    private bool shopEnabled;
    private Graphic[] tintGraphics;
    private Color[] originalGraphicColors;

    public ItemData ItemData => itemData;
    public bool IsOwned => isOwned;
    public Button Button => button != null ? button : button = GetComponent<Button>();

    public void Initialize(
        ItemData data,
        bool owned,
        bool isEquipped,
        bool enableShop,
        Action<DecorationItemSlot> onSelected,
        Action<DecorationItemSlot, bool, Vector2> onLockedHoverChanged = null)
    {
        ResolveAdjustableStateReferences();
        MigrateLegacyEnglishPriceFormat();
        ApplyShopFont(itemNameText);
        ApplyShopFont(unlockedDescriptionText);
        CaptureOriginalGraphicColors();

        itemData = data;
        isOwned = owned;
        shopEnabled = enableShop;
        this.onSelected = onSelected;
        this.onLockedHoverChanged = onLockedHoverChanged;

        bool showIcon = data != null && data.icon != null;
        if (iconImage != null)
        {
            iconImage.sprite = showIcon ? data.icon : null;
            iconImage.gameObject.SetActive(showIcon);
        }

        if (unknownOverlay != null)
            unknownOverlay.SetActive(data == null || !showIcon);

        if (itemNameText != null)
        {
            ConfigureItemNameText(itemNameText);
            itemNameText.text = data != null ? data.itemName : string.Empty;
        }

        if (unlockedDescriptionText != null)
            unlockedDescriptionText.text = data != null ? data.description : string.Empty;

        if (shopEnabled)
            EnsurePriceText();
        if (priceText != null)
        {
            priceText.gameObject.SetActive(false);
            priceText.text = string.Empty;
        }

        if (equippedBadge != null)
            equippedBadge.SetActive(false);

        SetSelected(isEquipped);
        RefreshStateDecorations(data, owned, isEquipped);

        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
            button.interactable = data != null && (owned || shopEnabled);
            UIButtonSfxPlayer.Register(button);
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectedIndicator != null)
            selectedIndicator.SetActive(selected);
        RefreshStateDecorations(itemData, isOwned, selected);
    }

    public void SetInteractionEnabled(bool enabled)
    {
        if (Button != null)
            Button.interactable = enabled && itemData != null && (isOwned || shopEnabled);
    }

    private void OnClick()
    {
        onSelected?.Invoke(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        NotifyLockedHover(true, eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        NotifyLockedHover(false, eventData);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (!isOwned && itemData != null)
            NotifyLockedHover(true, eventData);
    }

    private void NotifyLockedHover(bool visible, PointerEventData eventData)
    {
        if (isOwned || itemData == null)
            visible = false;

        Vector2 position = eventData != null ? eventData.position : Vector2.zero;
        onLockedHoverChanged?.Invoke(this, visible, position);
    }

    private void CaptureOriginalGraphicColors()
    {
        tintGraphics = GetComponentsInChildren<Graphic>(true);
        originalGraphicColors = new Color[tintGraphics.Length];
        for (int i = 0; i < tintGraphics.Length; i++)
            originalGraphicColors[i] = tintGraphics[i].color;
    }

    private void ApplyLockedTint(bool locked)
    {
        locked = false;
        if (tintGraphics == null || originalGraphicColors == null)
            return;

        for (int i = 0; i < tintGraphics.Length; i++)
        {
            Graphic graphic = tintGraphics[i];
            if (graphic == null)
                continue;

            Color original = originalGraphicColors[i];
            if (!locked)
            {
                graphic.color = original;
                continue;
            }

            Color tinted = new Color(
                original.r * lockedTint.r,
                original.g * lockedTint.g,
                original.b * lockedTint.b,
                original.a);
            graphic.color = tinted;
        }
    }

    private void RefreshStateDecorations(ItemData data, bool owned, bool isEquipped)
    {
        bool hasItem = data != null;
        bool locked = hasItem && !owned;
        bool equipped = hasItem && owned && isEquipped;
        bool unlockedNotEquipped = hasItem && owned && !isEquipped;

        if (equippedStateRoot != null)
            equippedStateRoot.SetActive(equipped);
        if (lockedStateRoot != null)
            lockedStateRoot.SetActive(locked);
        if (lockIconImage != null)
            lockIconImage.gameObject.SetActive(locked);
        if (unlockedDescriptionText != null)
            unlockedDescriptionText.gameObject.SetActive(unlockedNotEquipped);
        if (lockedCostText != null)
        {
            lockedCostText.gameObject.SetActive(locked);
            lockedCostText.text = hasItem ? data.elegantPointCost.ToString() : string.Empty;
            if (locked)
                RestoreLockedCostTextVisibility();
        }
    }

    private void ResolveAdjustableStateReferences()
    {
        if (unlockedDescriptionText == null)
            unlockedDescriptionText = FindChildText("UnlockedDescriptionText");
        if (equippedStateRoot == null)
            equippedStateRoot = transform.Find("EquippedState")?.gameObject;
        if (lockedStateRoot == null)
            lockedStateRoot = transform.Find("LockedState")?.gameObject;
        if (lockIconImage == null)
            lockIconImage = FindChildImage("LockIcon");
        if (lockedCostText == null)
            lockedCostText = FindChildText("LockedState/LockedCostText");
        if (lockedCostText == null)
            lockedCostText = FindDescendantText("LockedCostText");
    }

    private TextMeshProUGUI FindChildText(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
    }

    private TextMeshProUGUI FindDescendantText(string childName)
    {
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null && texts[i].name == childName)
                return texts[i];
        }

        return null;
    }

    private Image FindChildImage(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private void RestoreLockedCostTextVisibility()
    {
        lockedCostText.enabled = true;
        lockedCostText.raycastTarget = false;
        lockedCostText.alpha = 1f;
        lockedCostText.color = new Color(1f, 1f, 1f, 1f);
        lockedCostText.enableWordWrapping = false;
        lockedCostText.overflowMode = TextOverflowModes.Overflow;
        lockedCostText.transform.SetAsLastSibling();
        lockedCostText.ForceMeshUpdate();
    }

    private void EnsurePriceText()
    {
        if (priceText != null || itemNameText == null)
            return;

        // Existing prefabs predate shop prices. Clone their configured TMP label
        // as a runtime fallback until a dedicated PriceText is wired.
        priceText = Instantiate(itemNameText, transform);
        priceText.name = "PriceText (Runtime)";
        priceText.raycastTarget = false;
        ApplyShopFont(priceText);

        RectTransform priceRect = priceText.rectTransform;
        priceRect.localScale = itemNameText.rectTransform.localScale;
        priceRect.anchorMin = new Vector2(0.5f, 0.5f);
        priceRect.anchorMax = new Vector2(0.5f, 0.5f);
        priceRect.pivot = new Vector2(0.5f, 0.5f);
        priceRect.anchoredPosition = new Vector2(0f, -105f);
        priceRect.sizeDelta = new Vector2(200f, 42f);
    }

    private void ApplyShopFont(TMP_Text text)
    {
        if (text != null && shopFont != null)
            text.font = shopFont;
    }

    private static void ConfigureItemNameText(TextMeshProUGUI text)
    {
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Min(text.fontSize, 18f);
        text.fontSizeMax = text.fontSize;
    }

    private void MigrateLegacyEnglishPriceFormat()
    {
        if (string.IsNullOrWhiteSpace(priceFormat) ||
            priceFormat.IndexOf("EP", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            priceFormat = DefaultPriceFormat;
        }
    }
}
