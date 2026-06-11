using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CaseStudy.Editor
{
    [InitializeOnLoad]
    public static class MantokuOptionsSkinSwapAutomation
    {
        private const string RequestPath = "Temp/MantokuOptionsSkinSwapRequest.json";

        static MantokuOptionsSkinSwapAutomation()
        {
            EditorApplication.delayCall += RunIfRequested;
        }

        [Serializable]
        private sealed class Request
        {
            public string importedPrefabPath;
            public string[] targetPrefabPaths;
            public string cleanupScenePath;
        }

        private static void RunIfRequested()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            try
            {
                Request request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
                if (request == null || string.IsNullOrWhiteSpace(request.importedPrefabPath))
                {
                    Debug.LogWarning("[MantokuOptionsSkinSwapAutomation] Request file is invalid.");
                    DeleteRequest();
                    return;
                }

                GameObject importedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(request.importedPrefabPath);
                if (importedPrefab == null)
                {
                    Debug.LogWarning($"[MantokuOptionsSkinSwapAutomation] Imported prefab not found: {request.importedPrefabPath}");
                    DeleteRequest();
                    return;
                }

                if (request.targetPrefabPaths != null)
                {
                    for (int i = 0; i < request.targetPrefabPaths.Length; i++)
                    {
                        UpdateTargetPrefab(request.targetPrefabPaths[i], importedPrefab);
                    }
                }

                if (!string.IsNullOrWhiteSpace(request.cleanupScenePath))
                {
                    CleanupScene(request.cleanupScenePath);
                }

                Debug.Log("[MantokuOptionsSkinSwapAutomation] Updated option prefabs to use Option_Test.");
            }
            finally
            {
                DeleteRequest();
                AssetDatabase.Refresh();
            }
        }

        private static void UpdateTargetPrefab(string targetPrefabPath, GameObject importedPrefab)
        {
            if (string.IsNullOrWhiteSpace(targetPrefabPath))
            {
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(targetPrefabPath);
            try
            {
                Transform menuRoot = root.transform.Find("MenuRoot");
                if (menuRoot == null)
                {
                    Debug.LogWarning($"[MantokuOptionsSkinSwapAutomation] MenuRoot not found in {targetPrefabPath}");
                    return;
                }

                Transform existingAlternate = menuRoot.Find("AlternateMainMenuPanel");
                if (existingAlternate != null)
                {
                    UnityEngine.Object.DestroyImmediate(existingAlternate.gameObject);
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(importedPrefab);
                instance.name = "AlternateMainMenuPanel";
                instance.transform.SetParent(menuRoot, false);

                if (instance.transform is RectTransform rectTransform)
                {
                    rectTransform.anchorMin = Vector2.zero;
                    rectTransform.anchorMax = Vector2.one;
                    rectTransform.offsetMin = Vector2.zero;
                    rectTransform.offsetMax = Vector2.zero;
                    rectTransform.anchoredPosition = Vector2.zero;
                    rectTransform.localScale = Vector3.one;
                }

                if (instance.GetComponent<OptionsMainMenuSkin>() == null)
                {
                    instance.AddComponent<OptionsMainMenuSkin>();
                }

                Transform skill = instance.transform.Find("Skill");
                if (skill != null)
                {
                    skill.gameObject.SetActive(false);
                }

                PrefabUtility.SaveAsPrefabAsset(root, targetPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CleanupScene(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject importedRoot = GameObject.Find("FigmaImported");
            if (importedRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(importedRoot);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void DeleteRequest()
        {
            if (File.Exists(RequestPath))
            {
                File.Delete(RequestPath);
            }
        }
    }
}
