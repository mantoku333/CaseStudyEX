using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(StoryEventController))]
public sealed class StoryEventControllerEditor : Editor
{
    private SerializedProperty eventIdProperty;
    private SerializedProperty runOnceFlagKeyProperty;
    private SerializedProperty memoNameProperty;

    private static readonly List<CopiedActorBinding> copiedActorBindings = new List<CopiedActorBinding>();
    private static string copiedActorBindingsSourceName;

    private void OnEnable()
    {
        eventIdProperty = serializedObject.FindProperty("eventId");
        runOnceFlagKeyProperty = serializedObject.FindProperty("runOnceFlagKey");
        memoNameProperty = serializedObject.FindProperty("memoName");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawHeader();
        EditorGUILayout.Space(4f);

        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(8f);
        DrawValidation();
        EditorGUILayout.Space(8f);
        DrawTools();
    }

    private new void DrawHeader()
    {
        EditorGUILayout.LabelField("Story Event", EditorStyles.boldLabel);

        string eventId = eventIdProperty != null ? eventIdProperty.stringValue : string.Empty;
        string runOnceFlag = runOnceFlagKeyProperty != null ? runOnceFlagKeyProperty.stringValue : string.Empty;
        string memoName = memoNameProperty != null ? memoNameProperty.stringValue : string.Empty;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Event ID", string.IsNullOrWhiteSpace(eventId) ? "(empty)" : eventId);
            EditorGUILayout.LabelField("メモ", string.IsNullOrWhiteSpace(memoName) ? "(none)" : memoName);
            EditorGUILayout.LabelField("Run Once Flag", string.IsNullOrWhiteSpace(runOnceFlag) ? "(none)" : runOnceFlag);
        }
    }

    private void DrawValidation()
    {
        StoryEventController controller = (StoryEventController)target;
        List<string> warnings = BuildWarnings(controller);

        if (warnings.Count == 0)
        {
            EditorGUILayout.HelpBox("Ready. Timeline, markers, and event identity look valid.", MessageType.Info);
            return;
        }

        for (int i = 0; i < warnings.Count; i++)
        {
            EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
        }
    }

    private void DrawTools()
    {
        StoryEventController controller = (StoryEventController)target;

        EditorGUILayout.LabelField("Authoring Tools", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create Next Marker"))
            {
                CreateNextMarker(controller);
            }

            if (GUILayout.Button("Select Markers"))
            {
                Selection.objects = controller.GetComponentsInChildren<StoryEventMarker>(includeInactive: true);
            }
        }

        if (GUILayout.Button("Fit Move Clips To Walk Speed"))
        {
            StoryMoveClipDurationFitter.Fit(controller, null, true, 0.0f, 0.05f, true);
        }

        if (GUILayout.Button("Refresh Track Names"))
        {
            StoryTimelineTrackNameUtility.RefreshTrackNames(controller, true);
        }

        EditorGUILayout.Space(4f);
        DrawActorBindingTools(controller);
        EditorGUILayout.Space(4f);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = EditorApplication.isPlaying;
            if (GUILayout.Button("Play Event"))
            {
                controller.PlayEvent();
            }

            if (GUILayout.Button("Stop Event"))
            {
                controller.StopEvent();
            }

            GUI.enabled = true;
        }

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Play Event is available in Play Mode.", MessageType.None);
        }
    }

    private static void DrawActorBindingTools(StoryEventController controller)
    {
        EditorGUILayout.LabelField("Actor Binding Tools", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Copy Actor Bindings"))
            {
                CopyActorBindings(controller);
            }

            using (new EditorGUI.DisabledScope(copiedActorBindings.Count == 0))
            {
                if (GUILayout.Button("Paste"))
                {
                    PasteActorBindings(controller, "Paste Actor Bindings");
                }
            }
        }

        using (new EditorGUI.DisabledScope(copiedActorBindings.Count == 0))
        {
            if (GUILayout.Button("Paste To All Story Events In This Scene"))
            {
                PasteActorBindingsToScene(controller);
            }
        }

        if (copiedActorBindings.Count > 0)
        {
            string source = string.IsNullOrWhiteSpace(copiedActorBindingsSourceName)
                ? "unknown"
                : copiedActorBindingsSourceName;
            EditorGUILayout.HelpBox(
                $"Copied {copiedActorBindings.Count} actor binding(s) from '{source}'.",
                MessageType.None);
        }
    }

    private static void CopyActorBindings(StoryEventController controller)
    {
        if (controller == null)
        {
            return;
        }

        var controllerObject = new SerializedObject(controller);
        SerializedProperty bindingsProperty = controllerObject.FindProperty("actorBindings");
        if (bindingsProperty == null || !bindingsProperty.isArray)
        {
            Debug.LogWarning("[StoryEventControllerEditor] actorBindings property was not found.", controller);
            return;
        }

        copiedActorBindings.Clear();
        for (int i = 0; i < bindingsProperty.arraySize; i++)
        {
            copiedActorBindings.Add(CopiedActorBinding.From(bindingsProperty.GetArrayElementAtIndex(i), controller));
        }

        copiedActorBindingsSourceName = controller.name;
        Debug.Log(
            $"[StoryEventControllerEditor] Copied {copiedActorBindings.Count} actor binding(s) from '{controller.name}'.",
            controller);
    }

    private static void PasteActorBindingsToScene(StoryEventController sourceController)
    {
        if (sourceController == null || copiedActorBindings.Count == 0)
        {
            return;
        }

        Scene targetScene = sourceController.gameObject.scene;
        StoryEventController[] controllers =
            FindObjectsByType<StoryEventController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        var sceneControllers = new List<StoryEventController>();
        for (int i = 0; i < controllers.Length; i++)
        {
            StoryEventController controller = controllers[i];
            if (controller != null && controller.gameObject.scene == targetScene)
            {
                sceneControllers.Add(controller);
            }
        }

        if (sceneControllers.Count == 0)
        {
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Paste Actor Bindings",
            $"Paste {copiedActorBindings.Count} actor binding(s) to {sceneControllers.Count} StoryEventController(s) in '{targetScene.name}'?",
            "Paste",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        for (int i = 0; i < sceneControllers.Count; i++)
        {
            PasteActorBindings(sceneControllers[i], "Paste Actor Bindings To Scene Events");
        }

        Debug.Log(
            $"[StoryEventControllerEditor] Pasted actor bindings to {sceneControllers.Count} StoryEventController(s) in scene '{targetScene.name}'.",
            sourceController);
    }

    private static void PasteActorBindings(StoryEventController targetController, string undoName)
    {
        if (targetController == null || copiedActorBindings.Count == 0)
        {
            return;
        }

        Undo.RecordObject(targetController, undoName);

        var controllerObject = new SerializedObject(targetController);
        SerializedProperty bindingsProperty = controllerObject.FindProperty("actorBindings");
        if (bindingsProperty == null || !bindingsProperty.isArray)
        {
            Debug.LogWarning("[StoryEventControllerEditor] actorBindings property was not found.", targetController);
            return;
        }

        bindingsProperty.arraySize = copiedActorBindings.Count;
        for (int i = 0; i < copiedActorBindings.Count; i++)
        {
            CopiedActorBinding copied = copiedActorBindings[i];
            SerializedProperty bindingProperty = bindingsProperty.GetArrayElementAtIndex(i);

            bindingProperty.FindPropertyRelative("actorKey").stringValue = copied.ActorKey;
            bindingProperty.FindPropertyRelative("displayName").stringValue = copied.DisplayName;
            bindingProperty.FindPropertyRelative("actorRoot").objectReferenceValue =
                ResolveCopiedTransform(copied.ActorRoot, targetController);
            bindingProperty.FindPropertyRelative("bubbleTarget").objectReferenceValue =
                ResolveCopiedTransform(copied.BubbleTarget, targetController);
            bindingProperty.FindPropertyRelative("bubbleOffset").vector3Value = copied.BubbleOffset;
        }

        controllerObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetController);
        EditorSceneManager.MarkSceneDirty(targetController.gameObject.scene);
    }

    private static Transform ResolveCopiedTransform(CopiedTransform copiedTransform, StoryEventController targetController)
    {
        if (targetController == null || copiedTransform == null || copiedTransform.IsEmpty)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(copiedTransform.ControllerRelativePath))
        {
            Transform relativeTransform = targetController.transform.Find(copiedTransform.ControllerRelativePath);
            if (relativeTransform != null)
            {
                return relativeTransform;
            }
        }
        else if (copiedTransform.WasControllerRoot)
        {
            return targetController.transform;
        }

        Transform pathTransform = FindTransformByScenePath(targetController.gameObject.scene, copiedTransform.ScenePath);
        if (pathTransform != null)
        {
            return pathTransform;
        }

        return FindTransformByName(targetController.gameObject.scene, copiedTransform.Name);
    }

    private static Transform FindTransformByScenePath(Scene scene, string scenePath)
    {
        if (!scene.IsValid() || string.IsNullOrWhiteSpace(scenePath))
        {
            return null;
        }

        string[] names = scenePath.Split('/');
        if (names.Length == 0)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || root.name != names[0])
            {
                continue;
            }

            Transform current = root.transform;
            for (int nameIndex = 1; nameIndex < names.Length && current != null; nameIndex++)
            {
                current = current.Find(names[nameIndex]);
            }

            if (current != null)
            {
                return current;
            }
        }

        return null;
    }

    private static Transform FindTransformByName(Scene scene, string transformName)
    {
        if (!scene.IsValid() || string.IsNullOrWhiteSpace(transformName))
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform result = FindTransformByNameRecursive(roots[i].transform, transformName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static Transform FindTransformByNameRecursive(Transform current, string transformName)
    {
        if (current == null)
        {
            return null;
        }

        if (current.name == transformName)
        {
            return current;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform result = FindTransformByNameRecursive(current.GetChild(i), transformName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static List<string> BuildWarnings(StoryEventController controller)
    {
        var warnings = new List<string>();

        if (controller == null)
        {
            return warnings;
        }

        if (string.IsNullOrWhiteSpace(controller.EventId))
        {
            warnings.Add("Event ID is empty.");
        }

        if (controller.Director == null)
        {
            warnings.Add("PlayableDirector is missing.");
        }
        else if (controller.Director.playableAsset == null)
        {
            warnings.Add("PlayableDirector has no Timeline asset.");
        }

        StoryEventMarker[] markers = controller.GetComponentsInChildren<StoryEventMarker>(includeInactive: true);
        if (markers.Length == 0)
        {
            warnings.Add("No local markers found. Create Marker_01 or add StoryEventMarker children.");
        }

        var used = new HashSet<int>();
        for (int i = 0; i < markers.Length; i++)
        {
            StoryEventMarker marker = markers[i];
            if (marker == null)
            {
                continue;
            }

            if (!used.Add(marker.MarkerNo))
            {
                warnings.Add($"Marker number {marker.MarkerNo:00} is duplicated.");
            }
        }

        return warnings;
    }

    private static void CreateNextMarker(StoryEventController controller)
    {
        if (controller == null)
        {
            return;
        }

        StoryEventMarker[] markers = controller.GetComponentsInChildren<StoryEventMarker>(includeInactive: true);
        int nextNo = 1;
        Transform previousMarkerTransform = null;
        for (int i = 0; i < markers.Length; i++)
        {
            StoryEventMarker candidate = markers[i];
            if (candidate == null)
            {
                continue;
            }

            if (candidate.MarkerNo >= nextNo)
            {
                nextNo = candidate.MarkerNo + 1;
                previousMarkerTransform = candidate.Target;
            }
        }

        Transform markerRoot = controller.transform.Find("Markers");
        if (markerRoot == null)
        {
            var rootObject = new GameObject("Markers");
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Story Marker Root");
            markerRoot = rootObject.transform;
            markerRoot.SetParent(controller.transform, false);
        }

        var markerObject = new GameObject($"Marker_{nextNo:00}");
        Undo.RegisterCreatedObjectUndo(markerObject, "Create Story Marker");
        markerObject.transform.SetParent(markerRoot, false);
        markerObject.transform.position = previousMarkerTransform != null
            ? previousMarkerTransform.position
            : controller.transform.position;

        StoryEventMarker marker = markerObject.AddComponent<StoryEventMarker>();
        SerializedObject markerSerializedObject = new SerializedObject(marker);
        SerializedProperty markerNoProperty = markerSerializedObject.FindProperty("markerNo");
        if (markerNoProperty != null)
        {
            markerNoProperty.intValue = nextNo;
            markerSerializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        Selection.activeObject = markerObject;
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
    }

    private sealed class CopiedActorBinding
    {
        public string ActorKey { get; private set; }
        public string DisplayName { get; private set; }
        public CopiedTransform ActorRoot { get; private set; }
        public CopiedTransform BubbleTarget { get; private set; }
        public Vector3 BubbleOffset { get; private set; }

        public static CopiedActorBinding From(SerializedProperty bindingProperty, StoryEventController sourceController)
        {
            return new CopiedActorBinding
            {
                ActorKey = bindingProperty.FindPropertyRelative("actorKey").stringValue,
                DisplayName = bindingProperty.FindPropertyRelative("displayName").stringValue,
                ActorRoot = CopiedTransform.From(
                    bindingProperty.FindPropertyRelative("actorRoot").objectReferenceValue as Transform,
                    sourceController),
                BubbleTarget = CopiedTransform.From(
                    bindingProperty.FindPropertyRelative("bubbleTarget").objectReferenceValue as Transform,
                    sourceController),
                BubbleOffset = bindingProperty.FindPropertyRelative("bubbleOffset").vector3Value
            };
        }
    }

    private sealed class CopiedTransform
    {
        public string Name { get; private set; }
        public string ScenePath { get; private set; }
        public string ControllerRelativePath { get; private set; }
        public bool WasControllerRoot { get; private set; }
        public bool IsEmpty => string.IsNullOrWhiteSpace(Name) &&
                               string.IsNullOrWhiteSpace(ScenePath) &&
                               string.IsNullOrWhiteSpace(ControllerRelativePath) &&
                               !WasControllerRoot;

        public static CopiedTransform From(Transform transform, StoryEventController sourceController)
        {
            if (transform == null)
            {
                return new CopiedTransform();
            }

            return new CopiedTransform
            {
                Name = transform.name,
                ScenePath = GetScenePath(transform),
                ControllerRelativePath = GetControllerRelativePath(transform, sourceController),
                WasControllerRoot = sourceController != null && transform == sourceController.transform
            };
        }

        private static string GetScenePath(Transform transform)
        {
            var names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static string GetControllerRelativePath(Transform transform, StoryEventController sourceController)
        {
            if (sourceController == null || transform == null || !transform.IsChildOf(sourceController.transform))
            {
                return string.Empty;
            }

            if (transform == sourceController.transform)
            {
                return string.Empty;
            }

            var names = new List<string>();
            Transform current = transform;
            while (current != null && current != sourceController.transform)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }
    }
}

public static class StoryEventCreateMenu
{
    [MenuItem("GameObject/CaseStudy/Story Event", false, 10)]
    public static void CreateStoryEvent(MenuCommand menuCommand)
    {
        var root = new GameObject("StoryEvent_New");
        Undo.RegisterCreatedObjectUndo(root, "Create Story Event");

        if (menuCommand.context is GameObject parent)
        {
            root.transform.SetParent(parent.transform, false);
        }

        root.AddComponent<PlayableDirector>();
        root.GetComponent<PlayableDirector>().playOnAwake = false;
        root.AddComponent<StoryEventController>();

        var markers = new GameObject("Markers");
        Undo.RegisterCreatedObjectUndo(markers, "Create Story Event Markers");
        markers.transform.SetParent(root.transform, false);

        var marker01 = new GameObject("Marker_01");
        Undo.RegisterCreatedObjectUndo(marker01, "Create Story Event Marker");
        marker01.transform.SetParent(markers.transform, false);
        marker01.AddComponent<StoryEventMarker>();

        Selection.activeObject = root;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
