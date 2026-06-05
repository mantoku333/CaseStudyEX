using System;
using Metroidvania.Data;
using UnityEngine;


/// <summary>
/// オプション画面側などで使う装備状態の管理クラス
///・装備アイテムを持っているか確認
///・装備する
///・装備を外す
///・現在装備中IDを保存
///・セーブ/ロード対応
///　等を扱うことが出来る
/// </summary>
public static class PlayerEquipmentState
{
    private const string SectionKey = "player_equipment_v1";
    private static readonly PlayerEquipmentSaveModule module = new PlayerEquipmentSaveModule();

    private static string equippedItemId = string.Empty;
    private static ItemData equippedItemData;

    public static event Action EquippedItemChanged;

    public static string EquippedItemId => equippedItemId;
    public static ItemData EquippedItemData => equippedItemData;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SaveManager.RegisterModule(module);
    }

    public static bool HasItem(ItemData itemData)
    {
        if (itemData == null || string.IsNullOrWhiteSpace(itemData.itemId))
        {
            return false;
        }

        return GameItems.GetCount(itemData.itemId) > 0 || GameProgressFlags.Get(itemData.itemId);
    }

    public static bool Equip(ItemData itemData)
    {
        if (itemData == null || itemData.itemType != ItemType.Equipment)
        {
            return false;
        }

        if (!HasItem(itemData))
        {
            return false;
        }

        equippedItemId = itemData.itemId ?? string.Empty;
        equippedItemData = itemData;
        EquippedItemChanged?.Invoke();
        return true;
    }

    public static void EquipById(string itemId)
    {
        equippedItemId = itemId ?? string.Empty;
        equippedItemData = null;
        EquippedItemChanged?.Invoke();
    }

    public static void Unequip()
    {
        equippedItemId = string.Empty;
        equippedItemData = null;
        EquippedItemChanged?.Invoke();
    }

    public static void ClearAll()
    {
        Unequip();
    }

    public static bool IsEquipped(ItemData itemData)
    {
        return itemData != null &&
               !string.IsNullOrWhiteSpace(itemData.itemId) &&
               string.Equals(equippedItemId, itemData.itemId, StringComparison.Ordinal);
    }

    private static void RestoreFromSaveData(SaveGameData saveData)
    {
        equippedItemData = null;

        if (saveData == null)
        {
            equippedItemId = string.Empty;
            EquippedItemChanged?.Invoke();
            return;
        }

        string json = saveData.GetCustomSectionJson(SectionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            equippedItemId = string.Empty;
            EquippedItemChanged?.Invoke();
            return;
        }

        try
        {
            PlayerEquipmentPayload payload = JsonUtility.FromJson<PlayerEquipmentPayload>(json);
            equippedItemId = payload.equippedItemId ?? string.Empty;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[PlayerEquipmentState] Failed to parse saved equipment. {exception}");
            equippedItemId = string.Empty;
        }

        EquippedItemChanged?.Invoke();
    }

    [Serializable]
    private struct PlayerEquipmentPayload
    {
        public string equippedItemId;
    }

    private sealed class PlayerEquipmentSaveModule : ISaveDataModule
    {
        public int Priority => 215;

        public void Capture(SaveGameData saveData)
        {
            if (saveData == null)
            {
                return;
            }

            var payload = new PlayerEquipmentPayload
            {
                equippedItemId = equippedItemId ?? string.Empty
            };

            saveData.SetCustomSectionJson(SectionKey, JsonUtility.ToJson(payload));
        }

        public void Restore(SaveGameData saveData)
        {
            RestoreFromSaveData(saveData);
        }
    }
}
