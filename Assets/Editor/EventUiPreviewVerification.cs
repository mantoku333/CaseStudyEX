using System;
using System.IO;
using System.Reflection;
using Metroidvania.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class EventUiPreviewVerification
{
    static EventUiPreviewVerification()
    {
        EditorApplication.update += PollRequest;
    }

    private static void PollRequest()
    {
        if (File.Exists("Temp/EventUiPreview.request") && !EditorApplication.isCompiling &&
            !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode) Run();
    }

    [MenuItem("Tools/CaseStudy/Story/Verify and Export Event UI Previews")]
    private static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += Run; return; }
        File.Delete("Temp/EventUiPreview.request");
        try
        {
            new DialogueArtworkTests().SharedPrefab_SwitchesArtworkAndKeepsUnknownNamesHidden();
            new DialogueArtworkTests().DialogueText_UsesThirtyByTwoOrTwentyByOneWithoutShrinking();
            new DialogueArtworkTests().Nox_MovesBetweenSidesWithoutReplacingIrisAndResetsNextConversation();
            var animationTests = new DialoguePortraitAnimationTests();
            try
            {
                animationTests.LeftAndRightPortraits_KeepIndependentAnimationClocks();
                animationTests.ExpressionChanges_RestartButRepeatedLinesKeepPlayingAndStaticFacesStop();
            }
            finally { animationTests.Cleanup(); }
            Render();
            File.WriteAllText("Temp/EventUiPreview.result", "PASS: Nox left/right/return, Iris preserved, animation and conversation reset; artwork routing, button bindings, 30x2 normal / 20x1 emphasis, no shrinking, rich text, manual breaks, shake enlargement and page advance; previews rendered.");
        }
        catch (Exception e) { File.WriteAllText("Temp/EventUiPreview.result", e.ToString()); Debug.LogException(e); }
    }

    private static void Render()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var texture = new RenderTexture(1920, 1080, 24);
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/StoryEventCanvas.prefab");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            root.transform.localScale = Vector3.one;
            root.transform.Find("StoryEventOverlay").gameObject.SetActive(false);
            var view = root.GetComponentInChildren<DialogueView>(true);
            var so = new SerializedObject(view);
            var presentation = (GameObject)so.FindProperty("presentationRoot").objectReferenceValue;
            view.OnDialogueStartedAsync();
            presentation.SetActive(true);
            ((Image)so.FindProperty("centerIllustrationImage").objectReferenceValue).gameObject.SetActive(false);
            ((GameObject)so.FindProperty("dialoguePanel").objectReferenceValue).SetActive(true);
            var cameraObject = new GameObject("PreviewCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .14f, .2f);
            camera.targetTexture = texture;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            var text = (TextMeshProUGUI)so.FindProperty("dialogueText").objectReferenceValue;
            text.text = "<color=#FFE832>ここから先</color>は、まだ知らない景色。\n一緒に行きましょう。";
            text.maxVisibleCharacters = int.MaxValue;
            var apply = typeof(DialogueView).GetMethod("ApplySpeaker", BindingFlags.NonPublic | BindingFlags.Instance);
            apply.Invoke(view, new object[] { "ノクス", "default" });
            apply.Invoke(view, new object[] { "イリス", "default" });
            ((GameObject)so.FindProperty("nextIndicator").objectReferenceValue).SetActive(true);
            Directory.CreateDirectory("outputs/event-ui");
            Capture(camera, texture, "iris");
            apply.Invoke(view, new object[] { "ノクス", "default" });
            Capture(camera, texture, "nox-left");
            apply.Invoke(view, new object[] { "タナトス", "default" });
            Capture(camera, texture, "thanatos");
            apply.Invoke(view, new object[] { "ノクス", "default" });
            Capture(camera, texture, "nox-right");
            apply.Invoke(view, new object[] { "イリス", "default" });
            Capture(camera, texture, "iris-with-nox-right");
            typeof(DialogueView).GetMethod("ApplyNoxPlacementMetadata", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(view, new object[] { new[] { "nox:left" } });
            apply.Invoke(view, new object[] { "ノクス", "default" });
            Capture(camera, texture, "nox-return-left");
            ((GameObject)so.FindProperty("skipConfirmPanel").objectReferenceValue).SetActive(true);
            Capture(camera, texture, "skip-confirm");
            ((GameObject)so.FindProperty("skipConfirmPanel").objectReferenceValue).SetActive(false);
            var prepare = typeof(DialogueView).GetMethod("PrepareTypewriterText", BindingFlags.NonPublic | BindingFlags.Instance);
            var effects = typeof(DialogueView).GetMethod("ApplyLineTextEffects", BindingFlags.NonPublic | BindingFlags.Instance);
            effects.Invoke(view, new object[] { Array.Empty<string>() });
            prepare.Invoke(view, new object[] { string.Concat(System.Linq.Enumerable.Repeat("あいうえおかきくけこ", 6)) });
            text.maxVisibleCharacters = int.MaxValue;
            Capture(camera, texture, "long-text");
            effects.Invoke(view, new object[] { new[] { "size:1.5" } });
            prepare.Invoke(view, new object[] { "あいうえおかきくけこさしすせそたちつてと" });
            text.maxVisibleCharacters = int.MaxValue;
            Capture(camera, texture, "emphasized-text");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static void Capture(Camera camera, RenderTexture target, string name)
    {
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        try
        {
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes("outputs/event-ui/" + name + ".png", image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
    }
}
