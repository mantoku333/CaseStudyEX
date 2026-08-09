using System;
using Metroidvania.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DecorationItemSlot : MonoBehaviour
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

    private ItemData itemData;
    private Action<DecorationItemSlot> onSelected;
    private Button button;
    private bool isOwned;

    public ItemData ItemData => itemData;
    public bool IsOwned => isOwned;
    public Button Button => button != null ? button : button = GetComponent<Button>();

    public void Initialize(
        ItemData data,
        bool owned,
        bool isEquipped,
        Action<DecorationItemSlot> onSelected)
    {
        MigrateLegacyEnglishPriceFormat();
        ApplyShopFont(itemNameText);

        itemData = data;
        isOwned = owned;
        this.onSelected = onSelected;

        bool showIcon = owned && data != null && data.icon != null;
        if (iconImage != null)
        {
            iconImage.sprite = showIcon ? data.icon : null;
            iconImage.gameObject.SetActive(showIcon);
        }

        if (unknownOverlay != null)
            unknownOverlay.SetActive(!showIcon);

        if (itemNameText != null)
            itemNameText.text = owned && data != null ? data.itemName : string.Empty;

        EnsurePriceText();
        if (priceText != null)
        {
            bool showPrice = !owned && data != null && data.elegantPointCost > 0;
            priceText.gameObject.SetActive(showPrice);
            priceText.text = showPrice
                ? string.Format(priceFormat, data.elegantPointCost)
                : string.Empty;
        }

        if (equippedBadge != null)
        {
            bool hasBadge = owned && data != null && data.equipmentBadge != null;
            equippedBadge.SetActive(hasBadge);
            if (hasBadge)
            {
                Image badgeImage = equippedBadge.GetComponent<Image>();
                if (badgeImage != null)
                    badgeImage.sprite = data.equipmentBadge;
            }
        }

        SetSelected(isEquipped);

        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
            button.interactable = data != null;
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
            Button.interactable = enabled && itemData != null;
    }

    private void OnClick()
    {
        onSelected?.Invoke(this);
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
