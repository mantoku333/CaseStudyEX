using System;
using System.Reflection;
using GameName.Ending;
using GameName.Enemy;
using NUnit.Framework;
using UnityEditor;

public sealed class LastBossEndingRegressionTests
{
    [Test]
    public void EndingDirector_HasNoArcancielRewardPath()
    {
        const BindingFlags allMembers = BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        foreach (FieldInfo field in typeof(LastBossEndingDirector).GetFields(allMembers))
        {
            Assert.That(field.Name.IndexOf("arcanciel", StringComparison.OrdinalIgnoreCase), Is.LessThan(0));
        }

        foreach (MethodInfo method in typeof(LastBossEndingDirector).GetMethods(allMembers))
        {
            Assert.That(method.Name.IndexOf("arcanciel", StringComparison.OrdinalIgnoreCase), Is.LessThan(0));
        }
    }

    [Test]
    public void EnemyUniqueDropTable_DoesNotContainArcanciel()
    {
        EnemyUniqueDropTable dropTable = AssetDatabase.LoadAssetAtPath<EnemyUniqueDropTable>(
            "Assets/Data/Items/EnemyUniqueDropTable.asset");

        Assert.That(dropTable, Is.Not.Null);
        for (int i = 0; i < dropTable.Entries.Count; i++)
        {
            Assert.That(
                dropTable.Entries[i].ProgressFlagKey,
                Is.Not.EqualTo(GameProgressKeys.EquipmentArcancielUnlocked));
        }
    }
}
