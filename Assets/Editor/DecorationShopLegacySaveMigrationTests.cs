using System.Collections.Generic;
using NUnit.Framework;

public sealed class DecorationShopLegacySaveMigrationTests
{
    private readonly List<ItemSnapshot> originalItems = new List<ItemSnapshot>();
    private List<GameProgressFlags.GameProgressFlagSnapshot> originalFlags;
    private int originalBalance;
    private string originalEquippedId;

    [SetUp]
    public void SetUp()
    {
        originalBalance = ElegantPointWallet.Balance;
        originalFlags = GameProgressFlags.GetSnapshot();
        originalEquippedId = PlayerEquipmentState.EquippedItemId;

        IReadOnlyList<string> insertionOrder = GameItems.InsertionOrder;
        for (int i = 0; i < insertionOrder.Count; i++)
        {
            string itemId = insertionOrder[i];
            originalItems.Add(new ItemSnapshot(itemId, GameItems.GetCount(itemId)));
        }

        ElegantPointWallet.Clear();
        GameProgressFlags.ClearAll();
        GameItems.ClearAll();
        PlayerEquipmentState.ClearAll();
    }

    [TearDown]
    public void TearDown()
    {
        ElegantPointWallet.Clear();
        GameProgressFlags.ClearAll();
        GameItems.ClearAll();
        PlayerEquipmentState.ClearAll();

        ElegantPointWallet.Add(originalBalance);
        for (int i = 0; i < originalFlags.Count; i++)
            GameProgressFlags.Set(originalFlags[i].Key, originalFlags[i].Value);
        for (int i = 0; i < originalItems.Count; i++)
            GameItems.SetCount(originalItems[i].ItemId, originalItems[i].Count);
        if (!string.IsNullOrWhiteSpace(originalEquippedId))
            PlayerEquipmentState.EquipById(originalEquippedId);

        originalItems.Clear();
    }

    [Test]
    public void Restore_LegacySaveClearsDecorationsEquipmentAndPointsButPreservesUnrelatedState()
    {
        const string unrelatedItem = "unrelated_item";
        const string unrelatedFlag = "unrelated_flag";
        string[] decorationIds = GetDecorationIds();

        for (int i = 0; i < decorationIds.Length; i++)
        {
            GameItems.SetCount(decorationIds[i], 1);
            GameProgressFlags.Set(decorationIds[i], true);
        }

        GameItems.SetCount(unrelatedItem, 3);
        GameProgressFlags.Set(unrelatedFlag, true);
        PlayerEquipmentState.EquipById(GameProgressKeys.EquipmentArcancielUnlocked);
        ElegantPointWallet.Add(400);
        var legacySave = new SaveGameData();

        bool migrated = DecorationShopLegacySaveMigration.RestoreFromSaveData(legacySave);

        Assert.That(migrated, Is.True);
        for (int i = 0; i < decorationIds.Length; i++)
        {
            Assert.That(GameItems.GetCount(decorationIds[i]), Is.Zero);
            Assert.That(GameProgressFlags.Get(decorationIds[i]), Is.False);
        }

        Assert.That(PlayerEquipmentState.EquippedItemId, Is.Empty);
        Assert.That(ElegantPointWallet.Balance, Is.Zero);
        Assert.That(GameItems.GetCount(unrelatedItem), Is.EqualTo(3));
        Assert.That(GameProgressFlags.Get(unrelatedFlag), Is.True);
        Assert.That(
            legacySave.GetCustomSectionJson(DecorationShopLegacySaveMigration.SaveSectionKey),
            Is.Not.Empty);
    }

    [Test]
    public void Restore_MigratedSavePreservesNewRewardsAndIsIdempotent()
    {
        var migratedSave = new SaveGameData();
        DecorationShopLegacySaveMigration.CaptureToSaveData(migratedSave);
        GameItems.SetCount(GameProgressKeys.EquipmentArcancielUnlocked, 1);
        GameProgressFlags.Set(GameProgressKeys.EquipmentArcancielUnlocked, true);
        PlayerEquipmentState.EquipById(GameProgressKeys.EquipmentArcancielUnlocked);
        ElegantPointWallet.Add(125);

        bool migrated = DecorationShopLegacySaveMigration.RestoreFromSaveData(migratedSave);

        Assert.That(migrated, Is.False);
        Assert.That(GameItems.GetCount(GameProgressKeys.EquipmentArcancielUnlocked), Is.EqualTo(1));
        Assert.That(GameProgressFlags.Get(GameProgressKeys.EquipmentArcancielUnlocked), Is.True);
        Assert.That(PlayerEquipmentState.EquippedItemId, Is.EqualTo(GameProgressKeys.EquipmentArcancielUnlocked));
        Assert.That(ElegantPointWallet.Balance, Is.EqualTo(125));
    }

    [Test]
    public void Restore_LegacySaveMarksLoadDataBeforeASecondRestorePass()
    {
        var legacySave = new SaveGameData();
        GameItems.SetCount(GameProgressKeys.EquipmentBlueAuraUnlocked, 1);

        Assert.That(
            DecorationShopLegacySaveMigration.RestoreFromSaveData(legacySave),
            Is.True);

        GameItems.SetCount(GameProgressKeys.EquipmentBlueAuraUnlocked, 1);
        ElegantPointWallet.Add(25);

        Assert.That(
            DecorationShopLegacySaveMigration.RestoreFromSaveData(legacySave),
            Is.False);
        Assert.That(GameItems.GetCount(GameProgressKeys.EquipmentBlueAuraUnlocked), Is.EqualTo(1));
        Assert.That(ElegantPointWallet.Balance, Is.EqualTo(25));
    }

    private static string[] GetDecorationIds()
    {
        return new[]
        {
            GameProgressKeys.EquipmentBlueAuraUnlocked,
            GameProgressKeys.EquipmentRedAuraUnlocked,
            GameProgressKeys.EquipmentArcancielUnlocked
        };
    }

    private readonly struct ItemSnapshot
    {
        public readonly string ItemId;
        public readonly int Count;

        public ItemSnapshot(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
}
