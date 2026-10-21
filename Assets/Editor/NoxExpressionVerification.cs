using System.IO;
using System.Reflection;
using Metroidvania.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class NoxExpressionVerification
{
    [MenuItem("Tools/CaseStudy/Story/Verify Nox Expressions")]
    public static void Run()
    {
        var nox = new NoxExpressionTests();
        try
        {
            nox.Overlay_FollowsClockAndClearsForStaticFacesAndConversationEnd();
            nox.SharedPrefab_NoxExpressionsHaveAllFramesAndFollowSideChanges();
        }
        finally { nox.Cleanup(); }
        var animation = new DialoguePortraitAnimationTests();
        try
        {
            animation.SharedPrefab_HasCompleteClipsWithStableSpriteBounds();
            animation.LeftAndRightPortraits_KeepIndependentAnimationClocks();
            animation.ExpressionChanges_RestartButRepeatedLinesKeepPlayingAndStaticFacesStop();
        }
        finally { animation.Cleanup(); }
        new DialogueArtworkTests().Nox_MovesBetweenSidesWithoutReplacingIrisAndResetsNextConversation();
        Render();
    }

    private static void Render()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(1920, 1080, 24);
        try
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/StoryEventCanvas.prefab"), scene);
            root.transform.localScale = Vector3.one;
            root.transform.Find("StoryEventOverlay").gameObject.SetActive(false);
            var view = root.GetComponentInChildren<DialogueView>(true);
            var serialized = new SerializedObject(view);
            view.OnDialogueStartedAsync();
            ((GameObject)serialized.FindProperty("presentationRoot").objectReferenceValue).SetActive(true);
            ((GameObject)serialized.FindProperty("dialoguePanel").objectReferenceValue).SetActive(true);
            ((Image)serialized.FindProperty("centerIllustrationImage").objectReferenceValue).gameObject.SetActive(false);
            var cameraObject = new GameObject("NoxPreviewCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .14f, .2f);
            camera.targetTexture = target;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            var text = (TextMeshProUGUI)serialized.FindProperty("dialogueText").objectReferenceValue;
            text.maxVisibleCharacters = int.MaxValue;
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var apply = typeof(DialogueView).GetMethod("ApplySpeaker", flags);
            var advance = typeof(DialogueView).GetMethod("AdvancePortraitAnimations", flags);
            apply.Invoke(view, new object[] { "イリス", "normal" });
            Directory.CreateDirectory("Temp/NoxExpressions");
            foreach (var side in new[] { "left", "right" })
            {
                typeof(DialogueView).GetMethod("ApplyNoxPlacementMetadata", flags)
                    .Invoke(view, new object[] { new[] { "nox:" + side } });
                foreach (var expression in new[] { "angry", "happy", "normal" })
                {
                    text.text = expression == "angry" ? "怒り表情＋怒り漫符" : expression == "happy" ? "上機嫌＋音符" : "通常表情";
                    apply.Invoke(view, new object[] { "ノクス", expression });
                    Canvas.ForceUpdateCanvases();
                    advance.Invoke(view, new object[] { 2f });
                    camera.Render();
                    var previous = RenderTexture.active;
                    var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                    try
                    {
                        RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                        image.Apply();
                        File.WriteAllBytes("Temp/NoxExpressions/" + side + "-" + expression + ".png", image.EncodeToPNG());
                    }
                    finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
                }
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); Object.DestroyImmediate(target); }
    }
}
