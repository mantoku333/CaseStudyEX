using System.Collections.Generic;
using System.Reflection;
using GameName.Enemy;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BossAreaControllerSceneValidationTests
{
    private const string ScenePath = "Assets/Scenes/Fix_Alpha4_Fuyuno.unity";
    private const string EventsPrefabPath = "Assets/Prefabs/Events.prefab";
    private static readonly FieldInfo BossDefeatedFlagKeyField =
        typeof(BossAreaController).GetField("bossDefeatedFlagKey", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo BossRootField =
        typeof(BossAreaController).GetField("bossRoot", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo BossBgmField =
        typeof(BossAreaController).GetField("bossBgm", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo AutoSaveOnCompleteField =
        typeof(StoryEventController).GetField("autoSaveOnComplete", BindingFlags.Instance | BindingFlags.NonPublic);

    [Test]
    public void EventsPrefab_LastBossPreEncounterDoesNotOverwriteRespawnSave()
    {
        GameObject eventsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EventsPrefabPath);
        Assert.That(eventsPrefab, Is.Not.Null, $"Prefab could not be loaded: {EventsPrefabPath}");
        Assert.That(AutoSaveOnCompleteField, Is.Not.Null);

        StoryEventController[] storyEvents =
            eventsPrefab.GetComponentsInChildren<StoryEventController>(includeInactive: true);
        StoryEventController lastBossPreEncounter = null;

        for (int i = 0; i < storyEvents.Length; i++)
        {
            if (storyEvents[i] != null && storyEvents[i].EventId == "Event_33")
            {
                lastBossPreEncounter = storyEvents[i];
                break;
            }
        }

        Assert.That(lastBossPreEncounter, Is.Not.Null, "Event_33 must exist in the shared Events prefab.");
        Assert.That(
            (bool)AutoSaveOnCompleteField.GetValue(lastBossPreEncounter),
            Is.False,
            "Event_33 moves the player into the LastBoss room, so saving on completion replaces the safe respawn position.");
    }

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

    [TestCase("Assets/Scenes/FixScenes/Fix_Master.unity")]
    [TestCase("Assets/Scenes/FixScenes/future_fuyuno_master.unity")]
    public void Scene_AllLastBossAreasUseLastBossBgm(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Assert.IsTrue(scene.IsValid(), $"Scene could not be opened: {scenePath}");
        Assert.That(BossRootField, Is.Not.Null);
        Assert.That(BossBgmField, Is.Not.Null);

        BossAreaController[] bossAreas =
            Object.FindObjectsByType<BossAreaController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int lastBossAreaCount = 0;

        for (int i = 0; i < bossAreas.Length; i++)
        {
            Transform bossRoot = (Transform)BossRootField.GetValue(bossAreas[i]);
            if (bossRoot == null || bossRoot.GetComponent<LastBossController>() == null)
            {
                continue;
            }

            lastBossAreaCount++;
            AudioClip bossBgm = (AudioClip)BossBgmField.GetValue(bossAreas[i]);
            Assert.That(bossBgm, Is.Not.Null, $"{bossAreas[i].name} has no LastBoss BGM assigned.");
            Assert.That(bossBgm.name, Is.EqualTo("lastbossBGM"),
                $"{bossAreas[i].name} uses the wrong LastBoss BGM.");
        }

        Assert.That(lastBossAreaCount, Is.EqualTo(1),
            $"{scenePath} must contain exactly one BossAreaController for LastBoss.");
    }
}
