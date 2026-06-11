using System;
using Metroidvania.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DecorationItemSlot : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject unknownOverlay;
    [SerializeField] private GameObject equippedBadge;
    [SerializeField] private GameObject selectedIndicator;
    [SerializeField] private TextMeshProUGUI itemNameText;

    private ItemData itemData;
    private Action<ItemData> onSelected;

    public ItemData ItemData => itemData;

    public void Initialize(ItemData data, bool isOwned, bool isEquipped, Action<ItemData> onSelected)
    {
        itemData = data;
        this.onSelected = onSelected;

        bool showIcon = isOwned && data != null && data.icon != null;
        if (iconImage != null)
        {
            iconImage.sprite = showIcon ? data.icon : null;
            iconImage.gameObject.SetActive(showIcon);
        }

        if (unknownOverlay != null)
            unknownOverlay.SetActive(!showIcon);

        if (itemNameText != null)
            itemNameText.text = isOwned && data != null ? data.itemName : string.Empty;

        // バッジはアイテムの説明画像。equipmentBadgeが設定されているアイテムのみ表示
        if (equippedBadge != null)
        {
            bool hasBadge = data != null && data.equipmentBadge != null;
            equippedBadge.SetActive(hasBadge);
            if (hasBadge)
            {
                Image badgeImage = equippedBadge.GetComponent<Image>();
                if (badgeImage != null)
                    badgeImage.sprite = data.equipmentBadge;
            }
        }

        SetSelected(isEquipped);

        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectedIndicator != null)
            selectedIndicator.SetActive(selected);
    }

    private void OnClick()
    {
        onSelected?.Invoke(itemData);
    }
}
