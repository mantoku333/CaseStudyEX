using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace CaseStudy.Editor
{
    [InitializeOnLoad]
    public static class OptionTestAlphaHitConfigurator
    {
        private static readonly string[] TargetPrefabPaths =
        {
            "Assets/Figma/Screens/Option_Test.prefab",
            "Assets/Figma/Pages/Finish.prefab"
        };

        private const float AlphaThreshold = 0.1f;
        private static bool isRunning;

        static OptionTestAlphaHitConfigurator()
        {
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (isRunning || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Run;
                return;
            }

            isRunning = true;
            try
            {
                ConfigurePrefab();
            }
            finally
            {
                isRunning = false;
            }
        }

        private static void ConfigurePrefab()
        {
            bool importerChangedAny = false;
            for (int prefabIndex = 0; prefabIndex < TargetPrefabPaths.Length; prefabIndex++)
            {
                string prefabPath = TargetPrefabPaths[prefabIndex];
                if (string.IsNullOrWhiteSpace(prefabPath))
                {
                    continue;
                }

                if (!System.IO.File.Exists(prefabPath))
                {
                    continue;
                }

                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                bool prefabChanged = false;

                try
                {
                    Image[] images = prefabRoot.GetComponentsInChildren<Image>(true);
                    HashSet<string> processedAssetPaths = new HashSet<string>();
                    for (int i = 0; i < images.Length; i++)
                    {
                        Image image = images[i];
                        if (image == null || image.sprite == null)
                        {
                            continue;
                        }

                        string assetPath = AssetDatabase.GetAssetPath(image.sprite.texture);
                        if (string.IsNullOrEmpty(assetPath))
                        {
                            continue;
                        }

                        if (!processedAssetPaths.Add(assetPath))
                        {
                            continue;
                        }

                        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                        if (importer == null)
                        {
                            continue;
                        }

                        bool importerChanged = false;
                        if (!importer.isReadable)
                        {
                            importer.isReadable = true;
                            importerChanged = true;
                        }

                        if (!importer.alphaIsTransparency)
                        {
                            importer.alphaIsTransparency = true;
                            importerChanged = true;
                        }

                        if (importerChanged)
                        {
                            importer.SaveAndReimport();
                            importerChangedAny = true;
                        }
                    }

                    if (!importerChangedAny)
                    {
                        for (int i = 0; i < images.Length; i++)
                        {
                            Image image = images[i];
                            if (image == null || image.sprite == null)
                            {
                                continue;
                            }

                            if (image.alphaHitTestMinimumThreshold < AlphaThreshold)
                            {
                                image.alphaHitTestMinimumThreshold = AlphaThreshold;
                                prefabChanged = true;
                            }
                        }
                    }

                    if (prefabChanged)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            if (importerChangedAny)
            {
                EditorApplication.delayCall += Run;
            }
        }
    }
}
