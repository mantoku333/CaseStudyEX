using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public static class StoryTimelineTrackNameUtility
{
    [MenuItem("Tools/CaseStudy/Story/Refresh Track Names")]
    public static void RefreshSelectedControllerTrackNames()
    {
        StoryEventController controller = ResolveSelectedController();
        if (controller == null)
        {
            Debug.LogWarning("[StoryTimelineTrackNameUtility] Select a StoryEventController in the Hierarchy first.");
            return;
        }

        RefreshTrackNames(controller, true);
    }

    public static int RefreshTrackNames(StoryEventController controller, bool logResult)
    {
        if (controller == null)
        {
            return 0;
        }

        PlayableDirector director = controller.Director;
        TimelineAsset timeline = director != null ? director.playableAsset as TimelineAsset : null;
        if (timeline == null)
        {
            Debug.LogWarning("[StoryTimelineTrackNameUtility] PlayableDirector has no TimelineAsset.", controller);
            return 0;
        }

        Undo.RecordObject(timeline, "Refresh Track Names");

        int changedCount = 0;
        foreach (TrackAsset track in timeline.GetOutputTracks())
        {
            string name = ResolveTrackName(track);
            if (string.IsNullOrEmpty(name) || track.name == name)
            {
                continue;
            }

            track.name = name;
            changedCount++;
        }

        if (changedCount > 0)
        {
            EditorUtility.SetDirty(timeline);
            TimelineEditor.Refresh(RefreshReason.ContentsModified);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }

        if (logResult)
        {
            Debug.Log(
                $"[StoryTimelineTrackNameUtility] Refreshed track names. changed={changedCount}",
                timeline);
        }

        return changedCount;
    }

    private static string ResolveTrackName(TrackAsset track)
    {
        return track switch
        {
            StoryYarnDialogueTrack => "Yarn Dialogue Track",
            StoryCameraShakeTrack => "Camera Shake Track",
            StoryFadeTrack => "Fade Track",
            StoryCameraMoveTrack => "Camera Move Track",
            StoryCameraZoomTrack => "Camera Zoom Track",
            StoryObjectMoveTrack => "Object Move Track",
            StoryAnimatorTrack => "Animator Track",
            StoryAudioTrack => "Audio Track",
            EventPanelTrack => "Panel Track",
            StoryEventTrack => "Event Track",
            _ => string.Empty,
        };
    }

    private static StoryEventController ResolveSelectedController()
    {
        if (Selection.activeGameObject == null)
        {
            return null;
        }

        return Selection.activeGameObject.GetComponentInParent<StoryEventController>();
    }
}
