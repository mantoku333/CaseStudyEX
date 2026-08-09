using System;
using UnityEngine;

/// <summary>
/// One-time migration for saves made while decoration purchases were enabled.
/// Legacy saves did not record acquisition provenance, so all three decoration
/// ownership records and the Elegant Point balance are reset together.
/// </summary>
public static class DecorationShopLegacySaveMigration
{
    public const string SaveSectionKey = "decoration_shop_disabled_migration_v1";
    public const int CurrentVersion = 1;

    private static readonly DecorationShopLegacySaveMigrationModule module =
        new DecorationShopLegacySaveMigrationModule();

    private static readonly string[] DecorationItemIds =
    {
        GameProgressKeys.EquipmentBlueAuraUnlocked,
        GameProgressKeys.EquipmentRedAuraUnlocked,
        GameProgressKeys.EquipmentArcancielUnlocked
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SaveManager.RegisterModule(module);
    }

    public static void CaptureToSaveData(SaveGameData saveData)
    {
        if (saveData == null)
        {
            return;
        }

        saveData.SetCustomSectionJson(
            SaveSectionKey,
            JsonUtility.ToJson(new MigrationPayload { version = CurrentVersion }));
    }

    public static bool RestoreFromSaveData(SaveGameData saveData)
    {
        if (saveData == null || GetSavedVersion(saveData) >= CurrentVersion)
        {
            return false;
        }

        ApplyLegacyReset();

        // Mark the in-memory load data immediately so repeated restore passes
        // remain idempotent. The normal save flow persists this marker later.
        CaptureToSaveData(saveData);
        return true;
    }

    private static int GetSavedVersion(SaveGameData saveData)
    {
        string json = saveData.GetCustomSectionJson(SaveSectionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return 0;
        }

        try
        {
            MigrationPayload payload = JsonUtility.FromJson<MigrationPayload>(json);
            return payload != null ? Mathf.Max(0, payload.version) : 0;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[{nameof(DecorationShopLegacySaveMigration)}] Invalid migration marker. " +
                $"The legacy reset will be applied. {exception}");
            return 0;
        }
    }

    private static void ApplyLegacyReset()
    {
        for (int i = 0; i < DecorationItemIds.Length; i++)
        {
            string itemId = DecorationItemIds[i];
            GameItems.Remove(itemId);
            GameProgressFlags.Remove(itemId);
        }

        if (IsManagedDecorationId(PlayerEquipmentState.EquippedItemId))
        {
            PlayerEquipmentState.Unequip();
        }

        ElegantPointWallet.Clear();
    }

    private static bool IsManagedDecorationId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        for (int i = 0; i < DecorationItemIds.Length; i++)
        {
            if (string.Equals(itemId, DecorationItemIds[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    [Serializable]
    private sealed class MigrationPayload
    {
        public int version;
    }

    private sealed class DecorationShopLegacySaveMigrationModule : ISaveDataModule
    {
        // Runs after progress flags (200), inventory (210), equipment (215),
        // and Elegant Points (230) have restored their saved state.
        public int Priority => 240;

        public void Capture(SaveGameData saveData)
        {
            CaptureToSaveData(saveData);
        }

        public void Restore(SaveGameData saveData)
        {
            RestoreFromSaveData(saveData);
        }
    }
}
