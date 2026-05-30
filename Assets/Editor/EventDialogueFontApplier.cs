using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class EventDialogueFontApplier
{
    private const string FontPath = "Assets/Data/HannariMincho-Regular.otf";
    private const string FontAssetPath = "Assets/Data/HannariMincho-Regular SDF.asset";
    private const string EventCanvasPrefabPath = "Assets/Prefabs/UI/EventCanvas.prefab";

    [MenuItem("Tools/Dialogue/Apply Hannari Font To Event Canvas")]
    public static void ApplyHannariFontToEventCanvas()
    {
        TMP_FontAsset fontAsset = GetOrCreateFontAsset();
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(EventCanvasPrefabPath);

        try
        {
            int changedCount = 0;
            TextMeshProUGUI[] texts = prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI text in texts)
            {
                if (!IsEventDialogueText(text.transform))
                {
                    continue;
                }

                text.font = fontAsset;
                text.fontSharedMaterial = fontAsset.material;
                EditorUtility.SetDirty(text);
                changedCount++;
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, EventCanvasPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Applied '{fontAsset.name}' to {changedCount} event dialogue text objects in {EventCanvasPrefabPath}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static TMP_FontAsset GetOrCreateFontAsset()
    {
        TMP_FontAsset existingFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existingFontAsset != null)
        {
            return existingFontAsset;
        }

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (sourceFont == null)
        {
            throw new InvalidOperationException($"Font file was not found: {FontPath}");
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            90,
            9,
            GlyphRenderMode.SDFAA,
            2048,
            2048,
            AtlasPopulationMode.Dynamic,
            true);

        if (fontAsset == null)
        {
            throw new InvalidOperationException($"Could not create TMP font asset from: {FontPath}");
        }

        fontAsset.name = "HannariMincho-Regular SDF";
        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

        if (fontAsset.material != null)
        {
            fontAsset.material.name = "HannariMincho-Regular SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
        {
            if (atlasTexture == null)
            {
                continue;
            }

            atlasTexture.name = "HannariMincho-Regular Atlas";
            AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FontAssetPath);

        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
    }

    private static bool IsEventDialogueText(Transform transform)
    {
        bool hasDialogueRoot = false;
        Transform current = transform;

        while (current != null)
        {
            string objectName = current.name.Trim();

            if (objectName.Equals("TutorialView", StringComparison.OrdinalIgnoreCase) ||
                objectName.Equals("LetterBoxView", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (objectName.Equals("EventView", StringComparison.OrdinalIgnoreCase) ||
                objectName.Equals("DialogView", StringComparison.OrdinalIgnoreCase) ||
                objectName.Equals("BubbleView", StringComparison.OrdinalIgnoreCase) ||
                objectName.Equals("DialogPanel", StringComparison.OrdinalIgnoreCase) ||
                objectName.Equals("BubbleDialogPanel", StringComparison.OrdinalIgnoreCase))
            {
                hasDialogueRoot = true;
            }

            current = current.parent;
        }

        return hasDialogueRoot;
    }
}
