using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Metroidvania.Managers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Yarn.Unity;

[InitializeOnLoad]
public static class Event99ExpressionTestBuilder
{
    private const string ScenePath = "Assets/Scenes/Mantoku_Dialog.unity";
    private const string YarnProjectPath = "Assets/TestProject.yarnproject";
    private const string YarnSourcePath = "Assets/Data/Dialogue/Event99ExpressionTest.yarn";
    private const string TimelinePath = "Assets/Data/Timeline/TL_Event99.playable";
    private const string EventObjectName = "Event_99";
    private const string EventId = "Event_99";
    private const string DialogueNodeName = "Event99_ExpressionTest";
    private const string AutomationRequestPath = "Temp/Event99ExpressionTestBuilder.request";
    private const string AutomationResultPath = "Temp/Event99ExpressionTestBuilder.result";

    static Event99ExpressionTestBuilder()
    {
        if (File.Exists(AutomationRequestPath))
        {
            EditorApplication.delayCall += BuildIfRequested;
        }
    }

    [MenuItem("Tools/CaseStudy/Story/Create Event99 Expression Test")]
    public static void BuildFromMenu()
    {
        try
        {
            string backupPath = Build();
            EditorUtility.DisplayDialog(
                "Event99 Expression Test",
                $"Event_99を作成しました。\n\nBackup: {backupPath}",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Event99 Expression Test", exception.Message, "OK");
        }
    }

    private static void BuildIfRequested()
    {
        if (!File.Exists(AutomationRequestPath))
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += BuildIfRequested;
            return;
        }

        File.Delete(AutomationRequestPath);
        try
        {
            string backupPath = Build();
            File.WriteAllText(AutomationResultPath, $"SUCCESS\n{backupPath}");
        }
        catch (Exception exception)
        {
            File.WriteAllText(AutomationResultPath, $"ERROR\n{exception}");
            Debug.LogException(exception);
        }
    }

    private static string Build()
    {
        Scene scene = OpenTargetSceneSafely();
        string backupPath = BackupScene();

        ImportAndValidateDialogueNode();
        TimelineAsset timeline = EnsureTimeline();
        StoryEventController controller = EnsureSceneEvent(scene, timeline);
        BindTimeline(controller, timeline);

        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(controller.Director);
        EditorUtility.SetDirty(timeline);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new IOException($"シーンを保存できませんでした: {ScenePath}");
        }

        AssetDatabase.Refresh();
        Selection.activeGameObject = controller.gameObject;
        Debug.Log(
            $"[Event99ExpressionTestBuilder] Created '{EventObjectName}' with " +
            $"timeline='{TimelinePath}', node='{DialogueNodeName}'.");
        return backupPath;
    }

    private static Scene OpenTargetSceneSafely()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == ScenePath)
        {
            return activeScene;
        }

        if (activeScene.IsValid() && activeScene.isDirty)
        {
            throw new InvalidOperationException(
                $"未保存のシーン '{activeScene.path}' があるため、{ScenePath} を開けません。");
        }

        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    private static string BackupScene()
    {
        string backupDirectory = Path.Combine("Temp", "Event99ExpressionTestBackups");
        Directory.CreateDirectory(backupDirectory);
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string backupPath = Path.Combine(backupDirectory, $"Mantoku_Dialog-{timestamp}.unity");
        File.Copy(ScenePath, backupPath, overwrite: false);
        return backupPath.Replace('\\', '/');
    }

    private static void ImportAndValidateDialogueNode()
    {
        AssetDatabase.ImportAsset(YarnSourcePath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(
            YarnProjectPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        YarnProject yarnProject = AssetDatabase.LoadAssetAtPath<YarnProject>(YarnProjectPath);
        if (yarnProject == null || !yarnProject.NodeNames.Contains(DialogueNodeName))
        {
            throw new InvalidOperationException(
                $"Yarnノード '{DialogueNodeName}' を {YarnProjectPath} から読み込めませんでした。");
        }
    }

    private static TimelineAsset EnsureTimeline()
    {
        TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
        if (timeline == null)
        {
            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = "TL_Event99";
            AssetDatabase.CreateAsset(timeline, TimelinePath);
        }

        StoryYarnDialogueTrack dialogueTrack = timeline.GetOutputTracks()
            .OfType<StoryYarnDialogueTrack>()
            .FirstOrDefault();
        if (dialogueTrack == null)
        {
            dialogueTrack = timeline.CreateTrack<StoryYarnDialogueTrack>(null, "Story Yarn Dialogue Track");
        }

        TimelineClip dialogueClip = dialogueTrack.GetClips().FirstOrDefault();
        if (dialogueClip == null)
        {
            dialogueClip = dialogueTrack.CreateClip<StoryYarnDialogueClip>();
        }

        dialogueClip.displayName = "Iris Expression Test";
        dialogueClip.start = 0d;
        dialogueClip.duration = 1d;
        ConfigureDialogueClip(dialogueClip);
        return timeline;
    }

    private static void ConfigureDialogueClip(TimelineClip clip)
    {
        var serialized = new SerializedObject(clip.asset);
        SetString(serialized, "nodeName", DialogueNodeName);
        SetBool(serialized, "useControllerDefaultStyle", true);
        SetEnum(serialized, "dialogueStyle", (int)DialogueStyle.ADV);
        SetBool(serialized, "pauseTimelineUntilComplete", true);
        SetString(serialized, "bubbleActorKey", string.Empty);

        SerializedProperty clipId = RequireProperty(serialized, "clipId");
        if (string.IsNullOrWhiteSpace(clipId.stringValue))
        {
            clipId.stringValue = Guid.NewGuid().ToString("N");
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(clip.asset);
    }

    private static StoryEventController EnsureSceneEvent(Scene scene, TimelineAsset timeline)
    {
        GameObject eventObject = FindUniqueOptional(scene, EventObjectName);
        if (eventObject == null)
        {
            GameObject template = FindUniqueOptional(scene, "Event_39");
            if (template == null)
            {
                throw new InvalidOperationException("配置先の基準となる Event_39 が見つかりません。");
            }

            eventObject = new GameObject(EventObjectName);
            Undo.RegisterCreatedObjectUndo(eventObject, "Create Event_99");
            SceneManager.MoveGameObjectToScene(eventObject, scene);
            eventObject.transform.SetParent(template.transform.parent, false);
        }

        eventObject.name = EventObjectName;
        eventObject.transform.localPosition = Vector3.zero;
        eventObject.transform.localRotation = Quaternion.identity;
        eventObject.transform.localScale = Vector3.one;

        PlayableDirector director = eventObject.GetComponent<PlayableDirector>();
        if (director == null)
        {
            director = Undo.AddComponent<PlayableDirector>(eventObject);
        }

        StoryEventController controller = eventObject.GetComponent<StoryEventController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<StoryEventController>(eventObject);
        }

        DialogueManager dialogueManager = UnityEngine.Object.FindObjectsByType<DialogueManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(manager => manager != null && manager.gameObject.scene == scene);
        if (dialogueManager == null)
        {
            throw new InvalidOperationException("Mantoku_Dialog内にDialogueManagerが見つかりません。");
        }

        director.playOnAwake = false;
        director.playableAsset = timeline;
        director.extrapolationMode = DirectorWrapMode.None;
        director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;

        var serialized = new SerializedObject(controller);
        SetString(serialized, "eventId", EventId);
        SetString(serialized, "memoName", "ADV表情差分テスト");
        SetBool(serialized, "playOnStart", false);
        SetObject(serialized, "director", director);
        SetBool(serialized, "forceUnscaledTime", true);
        SetBool(serialized, "disableDirectorPlayOnAwake", true);
        SetObject(serialized, "dialogueManager", dialogueManager);
        SetEnum(serialized, "defaultDialogueStyle", (int)DialogueStyle.ADV);
        SetObject(serialized, "defaultBubbleTarget", eventObject.transform);
        SetBool(serialized, "skipWhenDialogueRunning", true);
        SetString(serialized, "runOnceFlagKey", string.Empty);
        SetEnum(serialized, "pausePolicy", (int)StoryPausePolicy.GameplayOnly);
        SetBool(serialized, "autoSaveOnComplete", false);
        SetBool(serialized, "markRunOnceFlagOnComplete", false);
        SetString(serialized, "nextEventIdOnComplete", string.Empty);
        SetBool(serialized, "lockPlayerControlDuringEvent", true);
        SetBool(serialized, "lockPlayerFacingDuringEvent", true);
        SetBool(serialized, "restoreActorTransformsOnExit", true);
        SetBool(serialized, "restorePlayerTransformOnExit", false);
        SetBool(serialized, "restoreSpriteFacingOnExit", true);
        SetBool(serialized, "ensurePlayerControlOnExit", true);
        SetBool(serialized, "showLetterBoxDuringEvent", false);
        SetBool(serialized, "useEventCameraDuringEvent", false);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EnsureMarker(eventObject.transform);
        return controller;
    }

    private static void EnsureMarker(Transform eventRoot)
    {
        Transform markers = eventRoot.Find("Markers");
        if (markers == null)
        {
            markers = new GameObject("Markers").transform;
            Undo.RegisterCreatedObjectUndo(markers.gameObject, "Create Event_99 Markers");
            markers.SetParent(eventRoot, false);
        }

        Transform marker = markers.Find("Marker_01");
        if (marker == null)
        {
            marker = new GameObject("Marker_01").transform;
            Undo.RegisterCreatedObjectUndo(marker.gameObject, "Create Event_99 Marker");
            marker.SetParent(markers, false);
        }

        marker.localPosition = Vector3.zero;
        marker.localRotation = Quaternion.identity;
        marker.localScale = Vector3.one;
        if (marker.GetComponent<StoryEventMarker>() == null)
        {
            Undo.AddComponent<StoryEventMarker>(marker.gameObject);
        }
    }

    private static void BindTimeline(StoryEventController controller, TimelineAsset timeline)
    {
        PlayableDirector director = controller.Director;
        director.playableAsset = timeline;
        foreach (PlayableBinding output in timeline.outputs)
        {
            if (output.sourceObject is StoryYarnDialogueTrack)
            {
                director.SetGenericBinding(output.sourceObject, controller);
            }
        }
    }

    private static GameObject FindUniqueOptional(Scene scene, string objectName)
    {
        List<GameObject> matches = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Where(transform => string.Equals(transform.name, objectName, StringComparison.Ordinal))
            .Select(transform => transform.gameObject)
            .ToList();

        if (matches.Count > 1)
        {
            throw new InvalidOperationException($"'{objectName}' が{matches.Count}個見つかりました。");
        }

        return matches.Count == 1 ? matches[0] : null;
    }

    private static SerializedProperty RequireProperty(SerializedObject target, string propertyName)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property == null)
        {
            throw new MissingFieldException(target.targetObject.GetType().Name, propertyName);
        }

        return property;
    }

    private static void SetString(SerializedObject target, string propertyName, string value)
    {
        RequireProperty(target, propertyName).stringValue = value;
    }

    private static void SetBool(SerializedObject target, string propertyName, bool value)
    {
        RequireProperty(target, propertyName).boolValue = value;
    }

    private static void SetEnum(SerializedObject target, string propertyName, int value)
    {
        RequireProperty(target, propertyName).enumValueIndex = value;
    }

    private static void SetObject(
        SerializedObject target,
        string propertyName,
        UnityEngine.Object value)
    {
        RequireProperty(target, propertyName).objectReferenceValue = value;
    }
}
