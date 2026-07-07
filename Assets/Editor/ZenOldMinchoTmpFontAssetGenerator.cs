using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class ZenOldMinchoTmpFontAssetGenerator
{
    private const string FontDirectory = "Assets/Figma/Fonts/ZenOldMincho";
    private const string OutputDirectory = "Assets/Figma/Fonts/ZenOldMincho/TMP";

    private static readonly string[] FontWeights =
    {
        "Regular",
        "Medium",
        "SemiBold",
        "Bold",
        "Black"
    };

    [InitializeOnLoadMethod]
    private static void CreateMissingFontAssetsOnEditorLoad()
    {
        if (!HasMissingFontAssets())
        {
            return;
        }

        EditorApplication.delayCall += CreateFontAssets;
    }

    [MenuItem("Tools/UI/Create Zen Old Mincho TMP Font Assets")]
    public static void CreateFontAssets()
    {
        EnsureOutputDirectory();

        foreach (string weight in FontWeights)
        {
            CreateFontAsset(weight);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Zen Old Mincho TMP font assets are ready.");
    }

    private static bool HasMissingFontAssets()
    {
        foreach (string weight in FontWeights)
        {
            string fontAssetPath = $"{OutputDirectory}/ZenOldMincho-{weight} SDF.asset";
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath) == null)
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureOutputDirectory()
    {
        if (!AssetDatabase.IsValidFolder(OutputDirectory))
        {
            AssetDatabase.CreateFolder(FontDirectory, "TMP");
        }
    }

    private static void CreateFontAsset(string weight)
    {
        string sourceFontPath = $"{FontDirectory}/ZenOldMincho-{weight}.ttf";
        string fontAssetName = $"ZenOldMincho-{weight} SDF";
        string fontAssetPath = $"{OutputDirectory}/{fontAssetName}.asset";

        TMP_FontAsset existingFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
        if (existingFontAsset != null)
        {
            Debug.Log($"Skipped existing TMP font asset: {fontAssetPath}");
            return;
        }

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourceFontPath);
        if (sourceFont == null)
        {
            throw new InvalidOperationException($"Font file was not found: {sourceFontPath}");
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            90,
            9,
            GlyphRenderMode.SDFAA,
            4096,
            4096,
            AtlasPopulationMode.Dynamic,
            true);

        if (fontAsset == null)
        {
            throw new InvalidOperationException($"Could not create TMP font asset from: {sourceFontPath}");
        }

        fontAsset.name = fontAssetName;
        AssetDatabase.CreateAsset(fontAsset, fontAssetPath);

        if (fontAsset.material != null)
        {
            fontAsset.material.name = $"{fontAssetName} Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
        {
            if (atlasTexture == null)
            {
                continue;
            }

            atlasTexture.name = $"{fontAssetName} Atlas";
            AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.ImportAsset(fontAssetPath);
        Debug.Log($"Created TMP font asset: {fontAssetPath}");
    }
}
