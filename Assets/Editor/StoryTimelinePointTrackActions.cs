using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEditor.Timeline.Actions;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

internal abstract class StoryTimelinePointTrackAction<TTrack, TMarker> : TrackAction
    where TTrack : TrackAsset
    where TMarker : ScriptableObject, IMarker
{
    public override ActionValidity Validate(IEnumerable<TrackAsset> tracks)
    {
        TrackAsset[] targets = GetValidTargets(tracks);
        if (targets.Length == 0)
        {
            return ActionValidity.NotApplicable;
        }

        return targets.Any(track => track.lockedInHierarchy)
            ? ActionValidity.Invalid
            : ActionValidity.Valid;
    }

    public override bool Execute(IEnumerable<TrackAsset> tracks)
    {
        TrackAsset[] targets = GetValidTargets(tracks);
        if (targets.Length == 0)
        {
            return false;
        }

        double time = ResolveTimelineTime();
        Object lastCreated = null;
        for (int i = 0; i < targets.Length; i++)
        {
            TMarker marker = targets[i].CreateMarker<TMarker>(time);
            lastCreated = marker;
            EditorUtility.SetDirty(targets[i]);
        }

        if (TimelineEditor.inspectedAsset != null)
        {
            EditorUtility.SetDirty(TimelineEditor.inspectedAsset);
        }

        Selection.activeObject = lastCreated;
        TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
        return true;
    }

    private static TrackAsset[] GetValidTargets(IEnumerable<TrackAsset> tracks)
    {
        if (tracks == null)
        {
            return System.Array.Empty<TrackAsset>();
        }

        TrackAsset[] targets = tracks.Where(track => track != null).ToArray();
        if (targets.Length == 0 ||
            targets.Any(track => !typeof(TTrack).IsInstanceOfType(track)))
        {
            return System.Array.Empty<TrackAsset>();
        }

        return targets;
    }

    private static double ResolveTimelineTime()
    {
        PlayableDirector director = TimelineEditor.inspectedDirector;
        if (director != null)
        {
            return director.time;
        }

        return 0d;
    }
}

[MenuEntry("Add Point/Yarn Dialogue Point", 3310)]
internal sealed class AddStoryYarnDialoguePointAction
    : StoryTimelinePointTrackAction<StoryYarnDialogueTrack, StoryYarnDialogueMarker>
{
}

[MenuEntry("Add Point/Panel Point", 3311)]
internal sealed class AddEventPanelPointAction
    : StoryTimelinePointTrackAction<EventPanelTrack, EventPanelMarker>
{
}

[MenuEntry("Add Point/Audio Point", 3312)]
internal sealed class AddStoryAudioPointAction
    : StoryTimelinePointTrackAction<StoryAudioTrack, StoryAudioMarker>
{
}

[MenuEntry("Add Point/Camera Shake Point", 3313)]
internal sealed class AddStoryCameraShakePointAction
    : StoryTimelinePointTrackAction<StoryCameraShakeTrack, StoryCameraShakeMarker>
{
}

[MenuEntry("Add Point/Auto Save Point", 3314)]
internal sealed class AddStoryAutoSavePointAction
    : StoryTimelinePointTrackAction<StoryEventTrack, StoryAutoSaveMarker>
{
}

[MenuEntry("Add Point/Object Move Point", 3315)]
internal sealed class AddStoryObjectMovePointAction
    : StoryTimelinePointTrackAction<StoryObjectMoveTrack, StoryObjectMoveMarker>
{
}
