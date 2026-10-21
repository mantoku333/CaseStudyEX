using System;
using System.IO;
using System.Linq;
using Metroidvania.UI;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class NoxExpressionSetup
{
    private const string Clips = "Assets/Data/Dialogue/PortraitAnimations/";

    static NoxExpressionSetup() { EditorApplication.update += PollRequest; }

    private static void PollRequest()
    {
        const string request = "Temp/NoxExpressions.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try
        {
            Run();
            NoxExpressionVerification.Run();
            File.WriteAllText("Temp/NoxExpressions.result", "PASS: setup, portrait regression tests, Nox expression tests and previews.");
        }
        catch (Exception e)
        {
            File.WriteAllText("Temp/NoxExpressions.result", e.ToString());
            Debug.LogException(e);
        }
    }

    [MenuItem("Tools/CaseStudy/Story/Setup Nox Expressions")]
    public static void Run()
    {
        var normal = AssetDatabase.LoadAssetAtPath<PortraitAnimationClip>(Clips + "Nox_normal.asset");
        if (normal == null) throw new InvalidOperationException("Nox_normal is missing.");
        var angryMark = SaveClip("Nox_angry_mark", LoadFrames("Nox_angry_mark"));
        var happyMark = SaveClip("Nox_happy_mark", LoadFrames("Nox_happy_mark"));
        var angry = SaveClip("Nox_angry", LoadFrames("Nox_angry"), angryMark);
        var frames = new SerializedObject(normal).FindProperty("frames");
        var normalFrames = Enumerable.Range(0, frames.arraySize)
            .Select(i => (Sprite)frames.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        var happy = SaveClip("Nox_happy", normalFrames, happyMark);

        const string prefabPath = "Assets/Prefabs/UI/StoryEventCanvas.prefab";
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var serialized = new SerializedObject(root.GetComponentInChildren<DialogueView>(true));
            var portraits = serialized.FindProperty("characterPortraits");
            var nox = Enumerable.Range(0, portraits.arraySize).Select(portraits.GetArrayElementAtIndex)
                .Single(p => p.FindPropertyRelative("characterName").stringValue == "ノクス");
            var expressions = nox.FindPropertyRelative("expressionPortraits");
            SetExpression(expressions, "angry", angry);
            SetExpression(expressions, "happy", happy);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("Nox expressions installed: #face:angry / #face:happy");
    }

    private static Sprite[] LoadFrames(string name)
    {
        var paths = Directory.GetFiles("Assets/Art/temp/" + name, "*.png").OrderBy(p => p, StringComparer.Ordinal).ToArray();
        if (paths.Length != 120) throw new InvalidOperationException(name + " must have 120 frames.");
        return paths.Select(path => AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\', '/')).OfType<Sprite>().Single()).ToArray();
    }

    private static PortraitAnimationClip SaveClip(string name, Sprite[] frames, PortraitAnimationClip overlay = null)
    {
        string path = Clips + name + ".asset";
        var clip = AssetDatabase.LoadAssetAtPath<PortraitAnimationClip>(path);
        if (clip == null)
        {
            clip = ScriptableObject.CreateInstance<PortraitAnimationClip>();
            AssetDatabase.CreateAsset(clip, path);
        }
        var serialized = new SerializedObject(clip);
        var list = serialized.FindProperty("frames");
        list.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        serialized.FindProperty("framesPerSecond").floatValue = 30;
        serialized.FindProperty("loop").boolValue = true;
        serialized.FindProperty("overlayAnimation").objectReferenceValue = overlay;
        serialized.FindProperty("overlayPosition").vector2Value = new Vector2(.3f, .3f);
        serialized.FindProperty("overlayWidth").floatValue = .3f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return clip;
    }

    private static void SetExpression(SerializedProperty expressions, string name, PortraitAnimationClip clip)
    {
        var entry = Enumerable.Range(0, expressions.arraySize).Select(expressions.GetArrayElementAtIndex)
            .FirstOrDefault(p => p.FindPropertyRelative("expressionName").stringValue == name);
        if (entry == null)
        {
            expressions.InsertArrayElementAtIndex(expressions.arraySize);
            entry = expressions.GetArrayElementAtIndex(expressions.arraySize - 1);
        }
        entry.FindPropertyRelative("expressionName").stringValue = name;
        entry.FindPropertyRelative("portraitSprite").objectReferenceValue = clip.FirstFrame;
        entry.FindPropertyRelative("portraitAnimation").objectReferenceValue = clip;
    }
}
