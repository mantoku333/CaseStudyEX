using System.Collections.Generic;
using System.Linq;
using GameName.Enemy;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class Fix0906SceneValidationTests
{
    private const string ScenePath = "Assets/Scenes/FixScenes/Fix_0906.unity";

    [Test]
    public void Scene_HasIndependentPickupAndShutterSaveKeys()
    {
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var components = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var pickups = components.OfType<ItemPickup>().Where(pickup =>
                new SerializedObject(pickup).FindProperty("itemData").objectReferenceValue ==
                AssetDatabase.LoadAssetAtPath<Object>("Assets/Data/Items/HitPointUpItem.asset"))
                .OrderBy(pickup => pickup.transform.position.x).ToArray();
            Assert.That(pickups.Length, Is.EqualTo(4));
            for (int i = 0; i < pickups.Length; i++)
            {
                Assert.That(new SerializedObject(pickups[i]).FindProperty("pickupSaveId").stringValue,
                    Is.EqualTo($"fix0906_hp_{i + 1:00}"));
            }

            var shutters = components.OfType<ShutterWallBlockRise>().ToArray();
            Assert.That(shutters.Length, Is.EqualTo(7));
            var keys = new HashSet<string>();
            foreach (var shutter in shutters)
            {
                string key = new SerializedObject(shutter).FindProperty("persistentStateKey").stringValue;
                Assert.That(key, Does.StartWith("fix0906_shutter_"));
                Assert.That(keys.Add(key), Is.True, $"Duplicate shutter key on {shutter.name}");
                Assert.That(components.OfType<LeverSwitch2D>().Count(lever =>
                    new SerializedObject(lever).FindProperty("shutterWall").objectReferenceValue == shutter),
                    Is.EqualTo(1), $"{shutter.name} must have exactly one linked lever.");
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void Scene_LastBossHasOneOwnerWithOriginalEventAndCameraSettings()
    {
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var areas = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<BossAreaController>(true))
                .Where(area =>
                {
                    var boss = new SerializedObject(area).FindProperty("bossRoot").objectReferenceValue as Transform;
                    return boss != null && boss.GetComponent<LastBossController>() != null;
                }).ToArray();
            Assert.That(areas.Length, Is.EqualTo(1), "Include disabled owners to prevent accidental reactivation.");
            var owner = areas[0];
            Assert.That(AnimationUtility.CalculateTransformPath(owner.transform, null),
                Is.EqualTo("Stage/Area/324/Col_324"));
            var serialized = new SerializedObject(owner);
            Assert.That(serialized.FindProperty("preEncounterStoryEventId").stringValue, Is.EqualTo("Event_33"));
            Assert.That(serialized.FindProperty("postDefeatStoryEventId").stringValue, Is.EqualTo("Event_35"));
            Assert.That(serialized.FindProperty("waitForPreEncounterStoryEvent").boolValue, Is.True);
            Assert.That(serialized.FindProperty("waitForPostDefeatStoryEvent").boolValue, Is.True);
            Assert.That(serialized.FindProperty("bossDefeatedFlagKey").stringValue, Is.EqualTo("last_boss_defeated"));
            Assert.That(serialized.FindProperty("fixedBossCamera").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("dualTargetCameraTarget").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("bossBgm").objectReferenceValue.name, Is.EqualTo("lastbossBGM"));
            Assert.That(serialized.FindProperty("confineInsideArea").boolValue, Is.True);
            Assert.That(owner.transform.position.x, Is.EqualTo(1071.2923f).Within(0.01f));
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
