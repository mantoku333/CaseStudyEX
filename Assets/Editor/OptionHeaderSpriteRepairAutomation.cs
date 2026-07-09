using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class OptionHeaderSpriteRepairAutomation
{
    private const string PrefabPath = "Assets/Prefabs/UI/OptionsCanvas.prefab";
    private const string RequestPath = "Temp/RepairOptionHeaderSprites.request";

    private struct HeaderSpriteBinding
    {
        public readonly string ObjectPath;
        public readonly string AssetPath;
        public readonly string SpriteName;

        public HeaderSpriteBinding(string objectPath, string assetPath, string spriteName)
        {
            ObjectPath = objectPath;
            AssetPath = assetPath;
            SpriteName = spriteName;
        }
    }

    private static readonly HeaderSpriteBinding[] Bindings =
    {
        new HeaderSpriteBinding("MenuRoot/OptionPanel/Back Button", "Assets/Art/Texture/UI/Back Botton 1.png", "Back Botton 1_0"),
        new HeaderSpriteBinding("MenuRoot/OptionPanel/Q", "Assets/Art/Texture/UI/Q 1.png", "Q 1_0"),
        new HeaderSpriteBinding("MenuRoot/OptionPanel/E", "Assets/Art/Texture/UI/E 1.png", "E 1_0"),
        new HeaderSpriteBinding("MenuRoot/OptionPanel/back", "Assets/Art/Texture/UI/back.png", "back_0"),
        new HeaderSpriteBinding("MenuRoot/OptionPanel/Back ground 1_0", "Assets/Art/Texture/UI/Back ground 1.png", "Back ground 1_0"),
        new HeaderSpriteBinding("MenuRoot/OptionPanel/Select 2_0", "Assets/Art/Texture/UI/Select 2.png", "Select 2_0"),
        new HeaderSpriteBinding("MenuRoot/OptionPanel/Map No Select 1_0", "Assets/Art/Texture/UI/Map No Select 1.png", "Map No Select 1_0"),
        new HeaderSpriteBinding("MenuRoot/OptionPanel/Map Select 1_0", "Assets/Art/Texture/UI/Map Select 1.png", "Map Select 1_0"),
        new HeaderSpriteBinding("MenuRoot/OptionPanel/Decoration No Select 1_0", "Assets/Art/Texture/UI/Decoration No Select 1.png", "Decoration No Select 1_0"),
        new HeaderSpriteBinding("MenuRoot/OptionPanel/Decoration Select 1_0", "Assets/Art/Texture/UI/Decoration Select 1.png", "Decoration Select 1_0"),
        new HeaderSpriteBinding("MenuRoot/OptionPanel/Note No Select 1_0", "Assets/Art/Texture/UI/Note No Select 1.png", "Note No Select 1_0"),
        new HeaderSpriteBinding("MenuRoot/OptionPanel/Note Select 1_0", "Assets/Art/Texture/UI/Note Select 1.png", "Note Select 1_0"),
        new HeaderSpriteBinding("MenuRoot/OptionPanel/Option text No select 1_0", "Assets/Art/Texture/UI/Option text No select 1.png", "Option text No select 1_0"),
        new HeaderSpriteBinding("MenuRoot/OptionPanel/Option text select 1_0", "Assets/Art/Texture/UI/Option text select 1.png", "Option text select 1_0"),
    };

    [InitializeOnLoadMethod]
    private static void RunIfRequestedOnLoad()
    {
        EditorApplication.delayCall += RunIfRequested;
        EditorApplication.update += RunIfRequested;
    }

    [MenuItem("Tools/UI/Repair Option Header Sprites")]
    public static void Repair()
    {
        for (int i = 0; i < Bindings.Length; i++)
        {
            ConfigureTexture(Bindings[i].AssetPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            for (int i = 0; i < Bindings.Length; i++)
            {
                ApplyBinding(root.transform, Bindings[i]);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[OptionHeaderSpriteRepair] Repaired OptionsCanvas prefab header sprites.");
    }

    private static void RunIfRequested()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        try
        {
            Repair();
            File.Delete(RequestPath);
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void ConfigureTexture(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[OptionHeaderSpriteRepair] Texture importer not found: {assetPath}");
            return;
        }

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Multiple)
        {
            importer.spriteImportMode = SpriteImportMode.Multiple;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
        else
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }
    }

    private static void ApplyBinding(Transform root, HeaderSpriteBinding binding)
    {
        Transform target = root.Find(binding.ObjectPath);
        if (target == null)
        {
            Debug.LogError($"[OptionHeaderSpriteRepair] Prefab object not found: {binding.ObjectPath}");
            return;
        }

        Image image = target.GetComponent<Image>();
        if (image == null)
        {
            Debug.LogError($"[OptionHeaderSpriteRepair] Image component not found: {binding.ObjectPath}");
            return;
        }

        Sprite sprite = LoadSprite(binding.AssetPath, binding.SpriteName);
        if (sprite == null)
        {
            Debug.LogError($"[OptionHeaderSpriteRepair] Sprite not found: {binding.AssetPath} / {binding.SpriteName}");
            return;
        }

        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = false;
    }

    private static Sprite LoadSprite(string assetPath, string spriteName)
    {
        Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite && sprite.name == spriteName)
            {
                return sprite;
            }
        }

        assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite && sprite.name == spriteName)
            {
                return sprite;
            }
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }
}
