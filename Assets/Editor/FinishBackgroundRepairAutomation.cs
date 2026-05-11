using UnityEditor;
using UnityEngine;

namespace CaseStudy.Editor
{
    [InitializeOnLoad]
    public static class FinishBackgroundRepairAutomation
    {
        private const string FinishPrefabPath = "Assets/Figma/Pages/Finish.prefab";
        private const string BackgroundImagePath = "Assets/Figma/ImageFills/ef9e7c08e8367589e085294df6f14b3ea9090962.png";
        private const string BackgroundNodeName = "Background";

        static FinishBackgroundRepairAutomation()
        {
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Run;
                return;
            }

            if (!System.IO.File.Exists(FinishPrefabPath) || !System.IO.File.Exists(BackgroundImagePath))
            {
                return;
            }

            if (EnsureBackgroundImporterSettings())
            {
                EditorApplication.delayCall += Run;
                return;
            }

            Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundImagePath);
            if (backgroundSprite == null)
            {
                EditorApplication.delayCall += Run;
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(FinishPrefabPath);
            bool changed = false;

            try
            {
                Transform background = root.transform.Find(BackgroundNodeName);
                if (background == null)
                {
                    return;
                }

                if (!background.gameObject.activeSelf)
                {
                    background.gameObject.SetActive(true);
                    changed = true;
                }

                Component[] components = background.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];
                    if (component == null)
                    {
                        continue;
                    }

                    SerializedObject serializedObject = new SerializedObject(component);
                    SerializedProperty spriteProperty = serializedObject.FindProperty("m_Sprite");
                    if (spriteProperty == null || spriteProperty.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    if (spriteProperty.objectReferenceValue != backgroundSprite)
                    {
                        spriteProperty.objectReferenceValue = backgroundSprite;
                        serializedObject.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, FinishPrefabPath);
                    AssetDatabase.Refresh();
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool EnsureBackgroundImporterSettings()
        {
            TextureImporter importer = AssetImporter.GetAtPath(BackgroundImagePath) as TextureImporter;
            if (importer == null)
            {
                return false;
            }

            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (!importer.isReadable)
            {
                importer.isReadable = true;
                changed = true;
            }

            if (!changed)
            {
                return false;
            }

            importer.SaveAndReimport();
            return true;
        }
    }
}
