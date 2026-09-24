using System.Linq;
using Metroidvania.Managers;
using Metroidvania.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Yarn.Unity;

public sealed class StoryEventCanvasPrefabTests
{
    [TestCase("Assets/Scenes/Mantoku_Dialog.unity")]
    [TestCase("Assets/Scenes/FixScenes/Fix_0906.unity")]
    public void Scene_UsesSharedCanvasWithOneDialogueSystem(string scenePath)
    {
        var scene = EditorSceneManager.OpenPreviewScene(scenePath);
        try
        {
            var components = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Component>(true)).Where(c => c != null).ToArray();
            var canvas = components.OfType<Canvas>().Single(c => c.name == "EventCanvas");
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(canvas),
                Is.EqualTo("Assets/Prefabs/UI/StoryEventCanvas.prefab"));
            var manager = components.OfType<DialogueManager>().Single();
            var runner = components.OfType<DialogueRunner>().Single();
            var view = components.OfType<DialogueView>().Single();
            Assert.That(manager.transform.IsChildOf(canvas.transform), Is.True);
            Assert.That(manager.gameObject.activeInHierarchy, Is.True);
            var serialized = new SerializedObject(manager);
            Assert.That(serialized.FindProperty("dialogueRunner").objectReferenceValue, Is.EqualTo(runner));
            Assert.That(serialized.FindProperty("advView").objectReferenceValue, Is.EqualTo(view));
            Assert.That(serialized.FindProperty("nextAction").objectReferenceValue, Is.Not.Null);
            Assert.That(runner.DialoguePresenters.ToArray(), Is.EqualTo(new DialoguePresenterBase[] { view }));
            Assert.That(new SerializedObject(runner).FindProperty("yarnProject").objectReferenceValue, Is.Not.Null);

            var tutorial = canvas.GetComponentInChildren<TutorialOverlayController>(true);
            Assert.That(tutorial, Is.Not.Null);
            Assert.That(canvas.transform.Find("StoryEventOverlay/DiaryView").GetComponent<EventPanelPresenter>(), Is.Not.Null);
            Assert.That(canvas.transform.Find("StoryEventOverlay/LetterBoxView"), Is.Not.Null);
            foreach (var trigger in components.OfType<TutorialTriggerZone>())
            {
                var binding = new SerializedObject(trigger).FindProperty("tutorialOverlay").objectReferenceValue;
                if (binding != null) Assert.That(binding, Is.EqualTo(tutorial), trigger.name);
            }
            foreach (var controller in components.OfType<StoryEventController>())
            {
                var binding = new SerializedObject(controller).FindProperty("dialogueManager").objectReferenceValue;
                if (binding != null) Assert.That(binding, Is.EqualTo(manager), controller.name);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void Prefab_PreservesAuthoredPortraitOverrides()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/StoryEventCanvas.prefab");
        try
        {
            var view = new SerializedObject(root.GetComponentInChildren<DialogueView>(true));
            var portraits = view.FindProperty("characterPortraits");
            Assert.That(portraits.arraySize, Is.EqualTo(2));
            var nox = portraits.GetArrayElementAtIndex(1);
            Assert.That(nox.FindPropertyRelative("characterName").stringValue, Is.EqualTo("ノクス"));
            var expressions = nox.FindPropertyRelative("expressionPortraits");
            Assert.That(expressions.arraySize, Is.EqualTo(1));
            Assert.That(expressions.GetArrayElementAtIndex(0).FindPropertyRelative("expressionName").stringValue, Is.EqualTo("assertive"));
            Assert.That(expressions.GetArrayElementAtIndex(0).FindPropertyRelative("portraitSprite").objectReferenceValue, Is.Not.Null);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
