using System.Collections.Generic;
using Metroidvania.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DecorationPage : MonoBehaviour
{
    [Header("カタログ")]
    [SerializeField] private ItemData[] itemCatalog;
    [SerializeField] private int minimumSlotCount = 9;

    [Header("スロット")]
    [SerializeField] private Transform slotContainer;
    [SerializeField] private DecorationItemSlot slotPrefab;

    [Header("右パネル")]
    [SerializeField] private Image previewImage;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Transform abilityTagContainer;
    [SerializeField] private GameObject abilityTagPrefab;

    [Header("ボタン")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button removeButton;

    private readonly List<DecorationItemSlot> slots = new List<DecorationItemSlot>();
    private ItemData selectedItem;
    private DecorationItemSlot selectedSlot;
    private GameObject itemNameBase;
    private Sprite defaultPreviewSprite;

    private void Awake()
    {
        if (itemNameBase == null)
            itemNameBase = transform.Find("item name base")?.gameObject;

        if (previewImage != null)
        {
            defaultPreviewSprite = previewImage.sprite;
            previewImage.preserveAspect = true;
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
            UIButtonSfxPlayer.Register(confirmButton);
        }
        if (removeButton != null)
        {
            removeButton.onClick.AddListener(OnRemoveClicked);
            UIButtonSfxPlayer.Register(removeButton);
        }
        ClearRightPanel();
    }

    /// <summary>
    /// デコレーションページを開いた時にOptionsMenuから呼ぶ
    /// </summary>
    public void Refresh()
    {
        ClearSlots();
        selectedItem = null;
        selectedSlot = null;

        if (itemCatalog == null)
        {
            ClearRightPanel();
            return;
        }

        // 取得順に並べた所持装備リストを作る
        var orderedItems = new System.Collections.Generic.List<ItemData>();
        IReadOnlyList<string> pickupOrder = GameItems.InsertionOrder;
        for (int i = 0; i < pickupOrder.Count; i++)
        {
            ItemData data = FindItemDataById(pickupOrder[i]);
            if (data != null && data.itemType == ItemType.Equipment)
                orderedItems.Add(data);
        }

        int totalSlots = Mathf.Max(minimumSlotCount, orderedItems.Count);

        for (int i = 0; i < totalSlots; i++)
        {
            ItemData data = i < orderedItems.Count ? orderedItems[i] : null;
            bool isOwned = data != null;
            bool isEquipped = data != null && PlayerEquipmentState.IsEquipped(data);

            DecorationItemSlot slot = Instantiate(slotPrefab, slotContainer);
            slot.Initialize(data, isOwned, isEquipped, OnSlotSelected);
            slots.Add(slot);

            if (isEquipped)
            {
                selectedItem = data;
                selectedSlot = slot;
                slot.SetSelected(true);
            }
        }

        RefreshRightPanel();
    }

    private void ClearSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
                Destroy(slots[i].gameObject);
        }
        slots.Clear();
    }

    private void OnSlotSelected(ItemData data)
    {
        if (selectedSlot != null)
            selectedSlot.SetSelected(false);

        selectedItem = data;
        selectedSlot = FindSlotByData(data);

        if (selectedSlot != null)
            selectedSlot.SetSelected(true);

        RefreshRightPanel();
    }

    private void OnConfirmClicked()
    {
        if (selectedItem == null)
            return;

        bool success = PlayerEquipmentState.Equip(selectedItem);
        if (!success)
            return;

        RefreshAllSlotSelection();
    }

    private void OnRemoveClicked()
    {
        PlayerEquipmentState.Unequip();
        selectedItem = null;
        selectedSlot = null;
        RefreshAllSlotSelection();
        ClearRightPanel();
    }

    private void RefreshAllSlotSelection()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
                continue;
            ItemData data = slots[i].ItemData;
            slots[i].SetSelected(data != null && PlayerEquipmentState.IsEquipped(data));
        }
    }

    private void RefreshRightPanel()
    {
        if (selectedItem == null)
        {
            ClearRightPanel();
            return;
        }

        if (previewImage != null)
        {
            previewImage.sprite = selectedItem.illustration;
            previewImage.gameObject.SetActive(selectedItem.illustration != null);
        }

        if (itemNameBase != null)
            itemNameBase.SetActive(true);

        if (itemNameText != null)
        {
            itemNameText.gameObject.SetActive(true);
            itemNameText.text = selectedItem.itemName;
        }

        if (descriptionText != null)
        {
            descriptionText.gameObject.SetActive(true);
            descriptionText.text = selectedItem.description;
        }

        RefreshAbilityTags();
    }

    private void ClearRightPanel()
    {
        if (previewImage != null)
        {
            previewImage.sprite = defaultPreviewSprite;
            previewImage.gameObject.SetActive(true);
        }

        if (itemNameBase != null)
            itemNameBase.SetActive(false);

        if (itemNameText != null)
            itemNameText.gameObject.SetActive(false);

        if (descriptionText != null)
            descriptionText.gameObject.SetActive(false);

        ClearAbilityTags();
    }

    private void RefreshAbilityTags()
    {
        ClearAbilityTags();

        if (abilityTagContainer == null || abilityTagPrefab == null || selectedItem == null)
            return;

        EquipmentAbilityData[] abilities = selectedItem.equipmentAbility;
        if (abilities == null)
            return;

        for (int i = 0; i < abilities.Length; i++)
        {
            string label = GetAbilityLabel(abilities[i].abilityType);
            if (string.IsNullOrEmpty(label))
                continue;

            GameObject tag = Instantiate(abilityTagPrefab, abilityTagContainer);
            TextMeshProUGUI text = tag.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
                text.text = label;
        }
    }

    private void ClearAbilityTags()
    {
        if (abilityTagContainer == null)
            return;

        for (int i = abilityTagContainer.childCount - 1; i >= 0; i--)
            Destroy(abilityTagContainer.GetChild(i).gameObject);
    }

    private ItemData FindItemDataById(string itemId)
    {
        if (itemCatalog == null || string.IsNullOrEmpty(itemId))
            return null;
        for (int i = 0; i < itemCatalog.Length; i++)
        {
            if (itemCatalog[i] != null && itemCatalog[i].itemId == itemId)
                return itemCatalog[i];
        }
        return null;
    }

    private DecorationItemSlot FindSlotByData(ItemData data)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null && slots[i].ItemData == data)
                return slots[i];
        }
        return null;
    }

    private static string GetAbilityLabel(EquipmentAbilityType type)
    {
        switch (type)
        {
            case EquipmentAbilityType.GunRecoilForceBonus:    return "反動移動の距離 UP";
            case EquipmentAbilityType.AttackPowerMultiplier:  return "攻撃力 UP";
            case EquipmentAbilityType.HealPercentOnEnemyKill: return "撃破時 HP 回復";
            default:                                           return string.Empty;
        }
    }
}
