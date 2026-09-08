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
        MigrateLegacyEnglishPriceFormat();
        ApplyShopFont(itemNameText);
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
            itemNameText.text = data != null ? data.itemName : string.Empty;

        if (shopEnabled)
            EnsurePriceText();
        if (priceText != null)
        {
            priceText.gameObject.SetActive(false);
            priceText.text = string.Empty;
        }

        if (equippedBadge != null)
        {
            bool hasBadge = owned && isEquipped && data != null && data.equipmentBadge != null;
            equippedBadge.SetActive(hasBadge);
            if (hasBadge)
            {
                Image badgeImage = equippedBadge.GetComponent<Image>();
                if (badgeImage != null)
                    badgeImage.sprite = data.equipmentBadge;
            }
        }

        SetSelected(isEquipped);
        ApplyLockedTint(!owned && data != null);

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

    private void MigrateLegacyEnglishPriceFormat()
    {
        if (string.IsNullOrWhiteSpace(priceFormat) ||
            priceFormat.IndexOf("EP", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            priceFormat = DefaultPriceFormat;
        }
    }
}
