using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;

[CustomEditor(typeof(StoryEventController))]
public sealed class StoryEventControllerEditor : Editor
{
    private SerializedProperty eventIdProperty;
    private SerializedProperty runOnceFlagKeyProperty;
    private SerializedProperty sceneNameProperty;

    private void OnEnable()
    {
        eventIdProperty = serializedObject.FindProperty("eventId");
        runOnceFlagKeyProperty = serializedObject.FindProperty("runOnceFlagKey");
        sceneNameProperty = serializedObject.FindProperty("sceneName");
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

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Story Event", EditorStyles.boldLabel);

        string eventId = eventIdProperty != null ? eventIdProperty.stringValue : string.Empty;
        string runOnceFlag = runOnceFlagKeyProperty != null ? runOnceFlagKeyProperty.stringValue : string.Empty;
        string sceneName = sceneNameProperty != null ? sceneNameProperty.stringValue : string.Empty;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Event ID", string.IsNullOrWhiteSpace(eventId) ? "(empty)" : eventId);
            EditorGUILayout.LabelField("Scene", string.IsNullOrWhiteSpace(sceneName) ? "(any scene)" : sceneName);
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
        for (int i = 0; i < markers.Length; i++)
        {
            if (markers[i] != null)
            {
                nextNo = Mathf.Max(nextNo, markers[i].MarkerNo + 1);
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
        markerObject.transform.position = controller.transform.position;

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
