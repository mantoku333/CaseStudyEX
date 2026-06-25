using GameName.Ending;
using GameName.Enemy;
using Metroidvania.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[Category("Boss")]
[Category("Story/Event")]
public sealed class LastBossArcancielRewardTests
{
    private ItemData rewardItemData;

    [TearDown]
    public void TearDown()
    {
        GameProgressFlags.ClearAll();
        GameItems.ClearAll();

        if (rewardItemData != null)
        {
            Object.DestroyImmediate(rewardItemData);
            rewardItemData = null;
        }
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

    [Test]
    public void TryGrantArcancielReward_AlreadyAcquired_SkipsDuplicateGrant()
    {
        rewardItemData = CreateRewardItemData(GameProgressKeys.EquipmentArcancielUnlocked);
        GameProgressFlags.Set(GameProgressKeys.EquipmentArcancielUnlocked, true);
        GameItems.SetCount(GameProgressKeys.EquipmentArcancielUnlocked, 1);

        bool granted = LastBossEndingDirector.TryGrantArcancielReward(
            rewardItemData,
            "fallback_arcanciel");

        Assert.That(granted, Is.False);
        Assert.That(GameItems.GetCount(GameProgressKeys.EquipmentArcancielUnlocked), Is.EqualTo(1));
    }

    [Test]
    public void EnemyUniqueDropTable_DoesNotContainArcanciel()
    {
        EnemyUniqueDropTable dropTable = AssetDatabase.LoadAssetAtPath<EnemyUniqueDropTable>(
            "Assets/Data/Items/EnemyUniqueDropTable.asset");

        Assert.That(dropTable, Is.Not.Null);

        for (int i = 0; i < dropTable.Entries.Count; i++)
        {
            EnemyUniqueDropTable.DropEntry entry = dropTable.Entries[i];
            Assert.That(entry.ProgressFlagKey, Is.Not.EqualTo(GameProgressKeys.EquipmentArcancielUnlocked));
        }
    }

    private static ItemData CreateRewardItemData(string itemId)
    {
        ItemData itemData = ScriptableObject.CreateInstance<ItemData>();
        itemData.itemName = "Arcanciel";
        itemData.itemId = itemId;
        itemData.itemType = ItemType.Equipment;
        return itemData;
    }
}
