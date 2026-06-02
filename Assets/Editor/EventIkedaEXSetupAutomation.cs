using System.Linq;
using Metroidvania.Managers;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace CaseStudy.EditorTools
{
    public static class EventIkedaEXSetupAutomation
    {
        private const string ScenePath = "Assets/Scenes/Event_IkedaEX.unity";
        private const string TimelinePath = "Assets/Data/TL_Event_IkedaEX.playable";
        private const string EventObjectName = "StoryEvent_IkedaEX";
        private const string EventId = "story_event_ikeda_ex";
        private const string FirstDialogueNode = "Prologue_Timeline_01aa";

        [MenuItem("Tools/CaseStudy/Setup Event_IkedaEX Story Event")]
        public static void Setup()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            TimelineAsset timeline = EnsureTimelineAsset();
            GameObject letterBoxView = EnsureLetterBoxView();
            StoryEventController controller = EnsureStoryEventController(timeline, letterBoxView);

            EnsureMarker(controller.transform, 1);
            BindTimeline(controller, timeline);

            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(controller.Director);
            EditorUtility.SetDirty(timeline);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EventIkedaEXSetupAutomation] Ready: {EventObjectName} uses {TimelinePath}");
        }

        private static TimelineAsset EnsureTimelineAsset()
        {
            TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (timeline == null)
            {
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timeline, TimelinePath);
            }

            StoryYarnDialogueTrack dialogueTrack = timeline.GetOutputTracks()
                .OfType<StoryYarnDialogueTrack>()
                .FirstOrDefault();
            if (dialogueTrack == null)
            {
                dialogueTrack = timeline.CreateTrack<StoryYarnDialogueTrack>(null, "Story Yarn Dialogue Track");
            }

            TimelineClip dialogueClip = dialogueTrack.GetClips()
                .FirstOrDefault(clip => ClipUsesNode(clip, FirstDialogueNode));
            if (dialogueClip == null)
            {
                dialogueClip = dialogueTrack.CreateClip<StoryYarnDialogueClip>();
                dialogueClip.displayName = FirstDialogueNode;
                dialogueClip.start = 0d;
                dialogueClip.duration = 3d;
                ConfigureDialogueClip(dialogueClip, FirstDialogueNode);
            }

            return timeline;
        }

        private static bool ClipUsesNode(TimelineClip clip, string nodeName)
        {
            if (clip == null || clip.asset == null)
            {
                return false;
            }

            var serializedClip = new SerializedObject(clip.asset);
            SerializedProperty nodeProperty = serializedClip.FindProperty("nodeName");
            return nodeProperty != null && nodeProperty.stringValue == nodeName;
        }

        private static void ConfigureDialogueClip(TimelineClip clip, string nodeName)
        {
            var serializedClip = new SerializedObject(clip.asset);
            SetString(serializedClip, "nodeName", nodeName);
            SetBool(serializedClip, "useControllerDefaultStyle", true);
            SetInt(serializedClip, "dialogueStyle", (int)DialogueStyle.Bubble);
            SetBool(serializedClip, "pauseTimelineUntilComplete", true);
            SetString(serializedClip, "bubbleActorKey", string.Empty);
            serializedClip.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(clip.asset);
        }

        private static StoryEventController EnsureStoryEventController(TimelineAsset timeline, GameObject letterBoxView)
        {
            GameObject eventObject = GameObject.Find(EventObjectName);
            if (eventObject == null)
            {
                eventObject = new GameObject(EventObjectName);

                GameObject storyRoot = GameObject.Find("Story");
                if (storyRoot != null)
                {
                    eventObject.transform.SetParent(storyRoot.transform, false);
                }
            }

            eventObject.transform.localPosition = Vector3.zero;
            eventObject.transform.localRotation = Quaternion.identity;
            eventObject.transform.localScale = Vector3.one;

            PlayableDirector director = eventObject.GetComponent<PlayableDirector>();
            if (director == null)
            {
                director = eventObject.AddComponent<PlayableDirector>();
            }

            StoryEventController controller = eventObject.GetComponent<StoryEventController>();
            if (controller == null)
            {
                controller = eventObject.AddComponent<StoryEventController>();
            }

            director.playableAsset = timeline;
            director.playOnAwake = false;

            var serializedController = new SerializedObject(controller);
            SetString(serializedController, "eventId", EventId);
            SetString(serializedController, "sceneName", "Event_IkedaEX");
            SetBool(serializedController, "playOnStart", false);
            SetObject(serializedController, "director", director);
            SetObject(
                serializedController,
                "dialogueManager",
                UnityEngine.Object.FindFirstObjectByType<DialogueManager>(FindObjectsInactive.Include));
            SetInt(serializedController, "defaultDialogueStyle", (int)DialogueStyle.Bubble);
            SetObject(serializedController, "defaultBubbleTarget", eventObject.transform);
            SetBool(serializedController, "skipWhenDialogueRunning", true);
            SetObject(
                serializedController,
                "panelPresenter",
                UnityEngine.Object.FindFirstObjectByType<EventPanelPresenter>(FindObjectsInactive.Include));
            SetString(serializedController, "panelPresenterName", "EventPanelPresenter");
            SetBool(serializedController, "showLetterBoxDuringEvent", true);
            SetObject(serializedController, "letterBoxView", letterBoxView);
            SetString(serializedController, "letterBoxViewName", "LetterBoxView");
            SetBool(serializedController, "useEventCameraDuringEvent", true);
            SetObject(serializedController, "eventCamera", FindEventCamera());
            SetString(serializedController, "eventCameraName", "EventCam");
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        private static GameObject EnsureLetterBoxView()
        {
            GameObject existing = FindGameObjectByName("LetterBoxView");
            if (existing != null)
            {
                return existing;
            }

            GameObject eventCanvas = GameObject.Find("EventCanvas");
            if (eventCanvas == null)
            {
                return null;
            }

            RectTransform canvasTransform = eventCanvas.transform as RectTransform;
            if (canvasTransform == null)
            {
                return null;
            }

            GameObject letterBoxView = new GameObject("LetterBoxView", typeof(RectTransform));
            RectTransform letterBoxRect = letterBoxView.GetComponent<RectTransform>();
            letterBoxRect.SetParent(canvasTransform, false);
            letterBoxRect.anchorMin = Vector2.zero;
            letterBoxRect.anchorMax = Vector2.one;
            letterBoxRect.anchoredPosition = Vector2.zero;
            letterBoxRect.sizeDelta = Vector2.zero;
            letterBoxRect.pivot = new Vector2(0.5f, 0.5f);
            letterBoxView.SetActive(false);

            CreateLetterBoxBar(letterBoxRect, "Top", true);
            CreateLetterBoxBar(letterBoxRect, "Bottom", false);
            return letterBoxView;
        }

        private static void CreateLetterBoxBar(RectTransform parent, string name, bool top)
        {
            GameObject bar = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = bar.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = top ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
            rect.anchorMax = top ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
            rect.anchoredPosition = top ? new Vector2(0f, -50f) : new Vector2(0f, 50f);
            rect.sizeDelta = new Vector2(0f, 100f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            Image image = bar.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
        }

        private static void EnsureMarker(Transform eventRoot, int markerNo)
        {
            Transform markersRoot = eventRoot.Find("Markers");
            if (markersRoot == null)
            {
                markersRoot = new GameObject("Markers").transform;
                markersRoot.SetParent(eventRoot, false);
            }

            string markerName = $"Marker_{markerNo:00}";
            Transform markerTransform = markersRoot.Find(markerName);
            if (markerTransform == null)
            {
                markerTransform = new GameObject(markerName).transform;
                markerTransform.SetParent(markersRoot, false);
            }

            markerTransform.localPosition = Vector3.zero;
            markerTransform.localRotation = Quaternion.identity;
            markerTransform.localScale = Vector3.one;

            StoryEventMarker marker = markerTransform.GetComponent<StoryEventMarker>();
            if (marker == null)
            {
                marker = markerTransform.gameObject.AddComponent<StoryEventMarker>();
            }

            var serializedMarker = new SerializedObject(marker);
            SetInt(serializedMarker, "markerNo", markerNo);
            serializedMarker.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(marker);
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

        private static GameObject FindGameObjectByName(string objectName)
        {
            Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            return transforms.FirstOrDefault(transform => transform.name == objectName)?.gameObject;
        }

        private static CinemachineCamera FindEventCamera()
        {
            CinemachineCamera[] cameras = UnityEngine.Object.FindObjectsByType<CinemachineCamera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            return cameras.FirstOrDefault(camera => camera.name.Contains("EventCam"));
        }

        private static void SetString(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = value;
            }
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
            }
        }

        private static void SetInt(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
            }
        }

        private static void SetObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }
    }
}
