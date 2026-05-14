using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CaseStudy.Editor
{
    [InitializeOnLoad]
    public static class FinishPageInstallAutomation
    {
        private const string RequestPath = "Temp/FinishPageInstallRequest.json";
        private const string FinishPromptName = "FinishPrompt";
        private const string HudPrefabPath = "Assets/Prefabs/UI/PlayerHUDCanvas.prefab";
        private const string ScenesRoot = "Assets/Scenes";

        static FinishPageInstallAutomation()
        {
            EditorApplication.delayCall += RunIfRequested;
        }

        [Serializable]
        private sealed class Request
        {
            public string finishPrefabPath;
            public string[] targetPrefabPaths;
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
                if (request == null ||
                    string.IsNullOrWhiteSpace(request.finishPrefabPath) ||
                    request.targetPrefabPaths == null ||
                    request.targetPrefabPaths.Length == 0)
                {
                    Debug.LogWarning("[FinishPageInstallAutomation] Request file is invalid.");
                    return;
                }

                for (int i = 0; i < request.targetPrefabPaths.Length; i++)
                {
                    InstallPromptOnOptionPrefab(request.targetPrefabPaths[i], request.finishPrefabPath);
                }

                CleanupOldFinishImplementation();
                Debug.Log("[FinishPageInstallAutomation] Installed Finish prompt on option prefabs.");
            }
            finally
            {
                DeleteRequest();
                AssetDatabase.Refresh();
            }
        }

        private static void InstallPromptOnOptionPrefab(string targetPrefabPath, string finishPrefabPath)
        {
            GameObject finishPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(finishPrefabPath);
            if (finishPrefab == null)
            {
                Debug.LogWarning($"[FinishPageInstallAutomation] Finish prefab not found: {finishPrefabPath}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(targetPrefabPath);
            try
            {
                Transform menuRoot = root.transform.Find("MenuRoot");
                if (menuRoot == null)
                {
                    Debug.LogWarning($"[FinishPageInstallAutomation] MenuRoot not found in {targetPrefabPath}");
                    return;
                }

                Transform existingPrompt = menuRoot.Find(FinishPromptName);
                if (existingPrompt != null)
                {
                    UnityEngine.Object.DestroyImmediate(existingPrompt.gameObject);
                }

                GameObject promptInstance = (GameObject)PrefabUtility.InstantiatePrefab(finishPrefab);
                promptInstance.name = FinishPromptName;
                promptInstance.transform.SetParent(menuRoot, false);
                promptInstance.SetActive(false);

                if (promptInstance.transform is RectTransform promptRect)
                {
                    promptRect.anchorMin = new Vector2(0f, 1f);
                    promptRect.anchorMax = new Vector2(0f, 1f);
                    promptRect.pivot = new Vector2(0f, 1f);
                    promptRect.localScale = Vector3.one;
                }

                if (promptInstance.GetComponent<MantokuStoryOptionsFinishPromptSkin>() == null)
                {
                    promptInstance.AddComponent<MantokuStoryOptionsFinishPromptSkin>();
                }

                PrefabUtility.SaveAsPrefabAsset(root, targetPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void DeleteRequest()
        {
            if (File.Exists(RequestPath))
            {
                File.Delete(RequestPath);
            }
        }

        private static void CleanupOldFinishImplementation()
        {
            if (File.Exists(HudPrefabPath))
            {
                CleanupPrefab(HudPrefabPath);
            }

            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { ScenesRoot });
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
                bool changed = false;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    changed |= RemoveMissingScriptsRecursive(roots[rootIndex]);
                }

                if (changed)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                }
            }
        }

        private static void CleanupPrefab(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                if (RemoveMissingScriptsRecursive(root))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool RemoveMissingScriptsRecursive(GameObject gameObject)
        {
            bool changed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject) > 0;
            Transform transform = gameObject.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                changed |= RemoveMissingScriptsRecursive(transform.GetChild(i).gameObject);
            }

            return changed;
        }
    }
}
