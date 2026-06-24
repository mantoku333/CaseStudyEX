using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[Category("Boss")]
[Category("SceneValidation")]
public sealed class BossAreaControllerSceneValidationTests
{
    private const string ScenePath = "Assets/Scenes/Fix_Alpha4_Fuyuno.unity";
    private static readonly FieldInfo BossDefeatedFlagKeyField =
        typeof(BossAreaController).GetField("bossDefeatedFlagKey", BindingFlags.Instance | BindingFlags.NonPublic);

    [Test]
    public void FixAlpha4Fuyuno_BossAreasUseUniqueDefeatedFlags()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Assert.IsTrue(scene.IsValid(), $"Scene could not be opened: {ScenePath}");

        BossAreaController[] bossAreas =
            Object.FindObjectsByType<BossAreaController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Assert.That(bossAreas, Is.Not.Empty);
        Assert.That(BossDefeatedFlagKeyField, Is.Not.Null);

        HashSet<string> uniqueFlags = new HashSet<string>();
        List<string> flags = new List<string>();

        for (int i = 0; i < bossAreas.Length; i++)
        {
            string flagKey = (string)BossDefeatedFlagKeyField.GetValue(bossAreas[i]);
            Assert.That(flagKey, Is.Not.Null.And.Not.Empty, $"{bossAreas[i].name} has no defeated flag key.");
            Assert.IsTrue(uniqueFlags.Add(flagKey), $"{bossAreas[i].name} reuses defeated flag key '{flagKey}'.");
            flags.Add(flagKey);
        }

        Assert.Contains(GameProgressKeys.Boss01Defeated, flags);
        Assert.Contains(GameProgressKeys.Boss02Defeated, flags);
        Assert.Contains(GameProgressKeys.LastBossDefeated, flags);
    }
}
