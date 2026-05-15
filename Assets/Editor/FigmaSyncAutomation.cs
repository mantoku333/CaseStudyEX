using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityFigmaBridge.Editor.Settings;

namespace CaseStudy.Editor
{
    [InitializeOnLoad]
    public static class FigmaSyncAutomation
    {
        private const string RequestPath = "Temp/FigmaSyncRequest.json";
        private const string RunningKey = "CaseStudy.FigmaSyncAutomation.Running";
        private const string FrameNameKey = "CaseStudy.FigmaSyncAutomation.FrameName";
        private const string ScenePathKey = "CaseStudy.FigmaSyncAutomation.ScenePath";
        private const string StartTicksKey = "CaseStudy.FigmaSyncAutomation.StartTicks";
        private const string TimeoutSecondsKey = "CaseStudy.FigmaSyncAutomation.TimeoutSeconds";
        private const string ImportedRootName = "FigmaImported";

        static FigmaSyncAutomation()
        {
            EditorApplication.delayCall += TryStartFromRequest;
        }

        [Serializable]
        private sealed class SyncRequest
        {
            public string documentUrl;
            public string scenePath;
            public string frameName;
            public int timeoutSeconds = 300;
            public bool placeInScene = true;
        }

        private static void TryStartFromRequest()
        {
            if (!File.Exists(RequestPath))
            {
                return;
            }

            if (SessionState.GetBool(RunningKey, false))
            {
                EnsurePolling();
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryStartFromRequest;
                return;
            }

            var request = LoadRequest();
            if (request == null)
            {
                Fail("Request file could not be parsed.");
                return;
            }

            if (string.IsNullOrWhiteSpace(request.frameName))
            {
                Fail("Frame name is empty.");
                return;
            }

            if (Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
            {
                Fail("TextMesh Pro essentials are missing. Import TMP Essential Resources in Unity and retry.");
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<UnityFigmaBridgeSettings>("Assets/UnityFigmaBridgeSettings.asset");
            if (settings == null)
            {
                Fail("Assets/UnityFigmaBridgeSettings.asset was not found.");
                return;
            }

            settings.DocumentUrl = request.documentUrl;
            settings.BuildPrototypeFlow = false;
            settings.RunTimeAssetsScenePath = request.scenePath;
            settings.OnlyImportSelectedPages = false;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            AssetDatabase.Refresh();

            SessionState.SetBool(RunningKey, true);
            SessionState.SetString(FrameNameKey, request.frameName);
            SessionState.SetString(ScenePathKey, request.scenePath ?? string.Empty);
            SessionState.SetString(StartTicksKey, DateTime.UtcNow.Ticks.ToString());
            SessionState.SetInt(TimeoutSecondsKey, Mathf.Max(30, request.timeoutSeconds));

            Debug.Log($"[FigmaSyncAutomation] Starting Figma sync for '{request.frameName}'.");

            var importerType = typeof(UnityFigmaBridgeSettings).Assembly.GetType("UnityFigmaBridge.Editor.UnityFigmaBridgeImporter");
            var syncMethod = importerType?.GetMethod("Sync", BindingFlags.Static | BindingFlags.NonPublic);
            if (syncMethod == null)
            {
                Fail("Could not find UnityFigmaBridge importer sync method.");
                return;
            }

            syncMethod.Invoke(null, null);
            EnsurePolling();
        }

        private static void EnsurePolling()
        {
            EditorApplication.update -= PollForGeneratedPrefab;
            EditorApplication.update += PollForGeneratedPrefab;
        }

        private static void PollForGeneratedPrefab()
        {
            if (!SessionState.GetBool(RunningKey, false))
            {
                EditorApplication.update -= PollForGeneratedPrefab;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            var startTicksString = SessionState.GetString(StartTicksKey, string.Empty);
            if (long.TryParse(startTicksString, out var startTicks))
            {
                var elapsed = DateTime.UtcNow - new DateTime(startTicks, DateTimeKind.Utc);
                if (elapsed.TotalSeconds > SessionState.GetInt(TimeoutSecondsKey, 300))
                {
                    Fail("Timed out waiting for UnityFigmaBridge to generate the screen prefab.");
                    return;
                }
            }

            var frameName = SessionState.GetString(FrameNameKey, string.Empty);
            var prefabPath = FindGeneratedScreenPrefabPath(frameName);
            if (prefabPath == null)
            {
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                return;
            }

            var scenePath = SessionState.GetString(ScenePathKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(scenePath))
            {
                PlacePrefabInScene(prefab, scenePath, frameName);
            }

            Debug.Log($"[FigmaSyncAutomation] Generated '{prefabPath}' and placed '{frameName}' into '{scenePath}'.");
            Complete();
        }

        private static void PlacePrefabInScene(GameObject prefab, string scenePath, string frameName)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (canvas == null)
            {
                canvas = CreateCanvas();
            }

            var importedRoot = canvas.transform.Find(ImportedRootName);
            if (importedRoot == null)
            {
                var rootObject = new GameObject(ImportedRootName, typeof(RectTransform));
                importedRoot = rootObject.transform;
                importedRoot.SetParent(canvas.transform, false);

                var rootRect = (RectTransform)importedRoot;
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
                rootRect.anchoredPosition = Vector2.zero;
            }

            var existing = importedRoot.Cast<Transform>()
                .Where(child => child.name.Equals(frameName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var child in existing)
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = frameName;
            instance.transform.SetParent(importedRoot, false);

            if (instance.transform is RectTransform rectTransform)
            {
                rectTransform.anchoredPosition = Vector2.zero;
                rectTransform.localScale = Vector3.one;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length == 0)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            return canvas;
        }

        private static string FindGeneratedScreenPrefabPath(string frameName)
        {
            const string screensFolder = "Assets/Figma/Screens";
            var exactPath = $"{screensFolder}/{frameName}.prefab";
            if (File.Exists(exactPath))
            {
                return exactPath;
            }

            if (!Directory.Exists(screensFolder))
            {
                return null;
            }

            return Directory.GetFiles(screensFolder, $"{frameName}*.prefab", SearchOption.TopDirectoryOnly)
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path)
                .FirstOrDefault();
        }

        private static SyncRequest LoadRequest()
        {
            try
            {
                var json = File.ReadAllText(RequestPath);
                return JsonUtility.FromJson<SyncRequest>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[FigmaSyncAutomation] Failed to load request: {exception}");
                return null;
            }
        }

        private static void Complete()
        {
            ClearState();
            DeleteRequestFile();
            EditorApplication.update -= PollForGeneratedPrefab;
        }

        private static void Fail(string message)
        {
            Debug.LogWarning($"[FigmaSyncAutomation] {message}");
            ClearState();
            DeleteRequestFile();
            EditorApplication.update -= PollForGeneratedPrefab;
        }

        private static void ClearState()
        {
            SessionState.EraseBool(RunningKey);
            SessionState.EraseString(FrameNameKey);
            SessionState.EraseString(ScenePathKey);
            SessionState.EraseString(StartTicksKey);
            SessionState.EraseInt(TimeoutSecondsKey);
        }

        private static void DeleteRequestFile()
        {
            if (File.Exists(RequestPath))
            {
                File.Delete(RequestPath);
            }
        }
    }
}
