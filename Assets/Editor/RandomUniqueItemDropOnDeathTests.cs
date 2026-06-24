using System.Collections.Generic;
using System.Reflection;
using GameName.Enemy;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

[Category("Gameplay")]
public sealed class RandomUniqueItemDropOnDeathTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<GameObject> objectsToDestroy = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        GameProgressFlags.ClearAll();

        for (int i = objectsToDestroy.Count - 1; i >= 0; i--)
        {
            GameObject gameObject = objectsToDestroy[i];
            if (gameObject == null)
            {
                continue;
            }

            PrepareForImmediateDestroy(gameObject);
            Object.DestroyImmediate(gameObject);
        }

        objectsToDestroy.Clear();
    }

    [Test]
    public void SelectDropEntry_ChanceRollFails_ReturnsNull()
    {
        GameObject itemPrefab = CreateObject("ItemPrefab");
        var entries = new[]
        {
            new EnemyUniqueDropTable.DropEntry(itemPrefab, GameProgressKeys.EquipmentBlueAuraUnlocked, 0.3f)
        };

        EnemyUniqueDropTable.DropEntry selected = RandomUniqueItemDropOnDeath.SelectDropEntry(
            entries,
            _ => false,
            0.31f);

        Assert.That(selected, Is.Null);
    }

    [Test]
    public void SelectDropEntry_AllFlagsAlreadyCollected_ReturnsNull()
    {
        GameProgressFlags.Set(GameProgressKeys.EquipmentBlueAuraUnlocked, true);
        GameProgressFlags.Set(GameProgressKeys.EquipmentRedAuraUnlocked, true);
        GameProgressFlags.Set(GameProgressKeys.EquipmentArcancielUnlocked, true);

        GameObject itemPrefab = CreateObject("ItemPrefab");
        var entries = new[]
        {
            new EnemyUniqueDropTable.DropEntry(itemPrefab, GameProgressKeys.EquipmentBlueAuraUnlocked, 1f),
            new EnemyUniqueDropTable.DropEntry(itemPrefab, GameProgressKeys.EquipmentRedAuraUnlocked, 1f),
            new EnemyUniqueDropTable.DropEntry(itemPrefab, GameProgressKeys.EquipmentArcancielUnlocked, 1f)
        };

        EnemyUniqueDropTable.DropEntry selected = RandomUniqueItemDropOnDeath.SelectDropEntry(
            entries,
            key => GameProgressFlags.Get(key),
            0f);

        Assert.That(selected, Is.Null);
    }

    [Test]
    public void SelectDropEntry_SkipsCollectedFlag()
    {
        GameProgressFlags.Set(GameProgressKeys.EquipmentBlueAuraUnlocked, true);

        GameObject itemPrefab = CreateObject("ItemPrefab");
        var entries = new[]
        {
            new EnemyUniqueDropTable.DropEntry(itemPrefab, GameProgressKeys.EquipmentBlueAuraUnlocked, 1f),
            new EnemyUniqueDropTable.DropEntry(itemPrefab, GameProgressKeys.EquipmentRedAuraUnlocked, 1f)
        };

        EnemyUniqueDropTable.DropEntry selected = RandomUniqueItemDropOnDeath.SelectDropEntry(
            entries,
            key => GameProgressFlags.Get(key),
            0f);

        Assert.That(selected, Is.Not.Null);
        Assert.That(selected.ProgressFlagKey, Is.EqualTo(GameProgressKeys.EquipmentRedAuraUnlocked));
    }

    [Test]
    public void SelectDropEntry_UsesEntryDropChance()
    {
        GameObject itemPrefab = CreateObject("ItemPrefab");
        var entries = new[]
        {
            new EnemyUniqueDropTable.DropEntry(itemPrefab, GameProgressKeys.EquipmentBlueAuraUnlocked, 0.03f),
            new EnemyUniqueDropTable.DropEntry(itemPrefab, GameProgressKeys.EquipmentRedAuraUnlocked, 0.005f)
        };

        EnemyUniqueDropTable.DropEntry blueSelected = RandomUniqueItemDropOnDeath.SelectDropEntry(
            entries,
            _ => false,
            0.029f);

        EnemyUniqueDropTable.DropEntry redSelected = RandomUniqueItemDropOnDeath.SelectDropEntry(
            entries,
            _ => false,
            0.031f);

        EnemyUniqueDropTable.DropEntry noDrop = RandomUniqueItemDropOnDeath.SelectDropEntry(
            entries,
            _ => false,
            0.035f);

        Assert.That(blueSelected, Is.Not.Null);
        Assert.That(blueSelected.ProgressFlagKey, Is.EqualTo(GameProgressKeys.EquipmentBlueAuraUnlocked));
        Assert.That(redSelected, Is.Not.Null);
        Assert.That(redSelected.ProgressFlagKey, Is.EqualTo(GameProgressKeys.EquipmentRedAuraUnlocked));
        Assert.That(noDrop, Is.Null);
    }

    [Test]
    public void IsEligibleEnemy_LastBoss_ReturnsFalse()
    {
        GameObject lastBoss = CreateObject("LastBoss");
        lastBoss.AddComponent<Rigidbody2D>().gravityScale = 0f;
        lastBoss.AddComponent<BoxCollider2D>();
        lastBoss.AddComponent<LastBossController>();

        Assert.That(RandomUniqueItemDropOnDeath.IsEligibleEnemy(lastBoss, true), Is.False);
        Assert.That(RandomUniqueItemDropOnDeath.IsEligibleEnemy(lastBoss, false), Is.False);
    }

    [Test]
    public void IsEligibleEnemy_StageBossFollowsSetting()
    {
        GameObject stageBoss = CreateObject("StageBoss");
        stageBoss.AddComponent<EnemyController>();
        stageBoss.AddComponent<StageBossAttack>();

        Assert.That(RandomUniqueItemDropOnDeath.IsEligibleEnemy(stageBoss, false), Is.False);
        Assert.That(RandomUniqueItemDropOnDeath.IsEligibleEnemy(stageBoss, true), Is.True);
    }

    [Test]
    public void IsEligibleEnemy_NormalEnemy_ReturnsTrue()
    {
        GameObject enemy = CreateObject("Enemy");
        enemy.AddComponent<EnemyController>();

        Assert.That(RandomUniqueItemDropOnDeath.IsEligibleEnemy(enemy, false), Is.True);
    }

    private GameObject CreateObject(string name)
    {
        var gameObject = new GameObject(name);
        objectsToDestroy.Add(gameObject);
        return gameObject;
    }

    private static void PrepareForImmediateDestroy(GameObject gameObject)
    {
        LastBossController boss = gameObject.GetComponent<LastBossController>();
        if (boss == null)
        {
            return;
        }

        FieldInfo telegraphField = typeof(LastBossController).GetField("telegraphObject", InstancePrivate);
        if (telegraphField != null)
        {
            telegraphField.SetValue(boss, null);
        }
    }
}
