using System.Collections.Generic;
using GameName.Ending;
using GameName.Enemy;
using Metroidvania.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class LastBossArcancielRewardTests
{
    private readonly List<ItemSnapshot> originalItems = new List<ItemSnapshot>();
    private List<GameProgressFlags.GameProgressFlagSnapshot> originalFlags;
    private int originalBalance;
    private string originalEquippedId;
    private ItemData rewardItemData;

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

        if (rewardItemData != null)
            Object.DestroyImmediate(rewardItemData);

        originalItems.Clear();
        rewardItemData = null;
    }

    [Test]
    public void ShopFeature_IsDisabledByDefault()
    {
        Assert.That(DecorationShopFeature.Enabled, Is.False);
    }

    [Test]
    public void TryGrantArcancielReward_MarksProgressFlagAndInventory()
    {
        rewardItemData = CreateRewardItemData(GameProgressKeys.EquipmentArcancielUnlocked);

        bool granted = LastBossEndingDirector.TryGrantArcancielReward(
            rewardItemData,
            "fallback_arcanciel");

        Assert.That(granted, Is.True);
        Assert.That(GameProgressFlags.Get(GameProgressKeys.EquipmentArcancielUnlocked), Is.True);
        Assert.That(GameItems.GetCount(GameProgressKeys.EquipmentArcancielUnlocked), Is.EqualTo(1));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void TryGrantArcancielReward_AlreadyAcquired_SkipsDuplicateGrant(bool representedByProgressFlag)
    {
        rewardItemData = CreateRewardItemData(GameProgressKeys.EquipmentArcancielUnlocked);
        if (representedByProgressFlag)
            GameProgressFlags.Set(GameProgressKeys.EquipmentArcancielUnlocked, true);
        else
            GameItems.SetCount(GameProgressKeys.EquipmentArcancielUnlocked, 1);

        bool granted = LastBossEndingDirector.TryGrantArcancielReward(
            rewardItemData,
            "fallback_arcanciel");

        Assert.That(granted, Is.False);
        Assert.That(GameItems.GetCount(GameProgressKeys.EquipmentArcancielUnlocked), Is.LessThanOrEqualTo(1));
    }

    [Test]
    public void EndingDirectorPrefab_HasArcancielRewardAndNotificationReferences()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Environment/LastBossEndingDirector.prefab");
        Assert.That(prefab, Is.Not.Null);

        LastBossEndingDirector director = prefab.GetComponent<LastBossEndingDirector>();
        Assert.That(director, Is.Not.Null);

        var serializedDirector = new SerializedObject(director);
        Assert.That(
            serializedDirector.FindProperty("arcancielRewardItemData").objectReferenceValue,
            Is.Not.Null);
        Assert.That(
            serializedDirector.FindProperty("arcancielRewardProgressFlagKey").stringValue,
            Is.EqualTo(GameProgressKeys.EquipmentArcancielUnlocked));
        Assert.That(
            serializedDirector.FindProperty("arcancielRewardNotificationSprite").objectReferenceValue,
            Is.Not.Null);
    }

    [Test]
    public void EnemyUniqueDropTable_KeepsBlueAndRedRatesAndExcludesArcanciel()
    {
        EnemyUniqueDropTable dropTable = AssetDatabase.LoadAssetAtPath<EnemyUniqueDropTable>(
            "Assets/Data/Items/EnemyUniqueDropTable.asset");

        Assert.That(dropTable, Is.Not.Null);
        EnemyUniqueDropTable.DropEntry red = FindEntry(
            dropTable,
            GameProgressKeys.EquipmentRedAuraUnlocked);
        EnemyUniqueDropTable.DropEntry blue = FindEntry(
            dropTable,
            GameProgressKeys.EquipmentBlueAuraUnlocked);

        Assert.That(red, Is.Not.Null);
        Assert.That(red.DropChance, Is.EqualTo(0.03f).Within(0.0001f));
        Assert.That(blue, Is.Not.Null);
        Assert.That(blue.DropChance, Is.EqualTo(0.005f).Within(0.0001f));
        Assert.That(
            FindEntry(dropTable, GameProgressKeys.EquipmentArcancielUnlocked),
            Is.Null);
    }

    private static EnemyUniqueDropTable.DropEntry FindEntry(
        EnemyUniqueDropTable dropTable,
        string progressFlagKey)
    {
        for (int i = 0; i < dropTable.Entries.Count; i++)
        {
            EnemyUniqueDropTable.DropEntry entry = dropTable.Entries[i];
            if (entry != null && entry.ProgressFlagKey == progressFlagKey)
                return entry;
        }

        return null;
    }

    private static ItemData CreateRewardItemData(string itemId)
    {
        ItemData itemData = ScriptableObject.CreateInstance<ItemData>();
        itemData.itemName = "Arcanciel";
        itemData.itemId = itemId;
        itemData.itemType = ItemType.Equipment;
        return itemData;
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
