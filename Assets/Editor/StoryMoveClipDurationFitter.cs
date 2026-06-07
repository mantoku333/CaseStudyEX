using System.Collections.Generic;
using Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public static class StoryMoveClipDurationFitter
{
    private const float DefaultMinDuration = 0.05f;
    private const float DefaultPaddingSeconds = 0.0f;
    private const double ChangeEpsilon = 0.0001d;

    public readonly struct Result
    {
        public Result(int scannedClips, int changedClips, int skippedClips)
        {
            ScannedClips = scannedClips;
            ChangedClips = changedClips;
            SkippedClips = skippedClips;
        }

        public int ScannedClips { get; }
        public int ChangedClips { get; }
        public int SkippedClips { get; }
    }

    [MenuItem("Tools/CaseStudy/Story/Fit Move Clips To Walk Speed")]
    public static void FitSelectedController()
    {
        StoryEventController controller = ResolveSelectedController();
        if (controller == null)
        {
            Debug.LogWarning("[StoryMoveClipDurationFitter] Select a StoryEventController in the Hierarchy first.");
            return;
        }

        Fit(controller, null, true, DefaultPaddingSeconds, DefaultMinDuration, true);
    }

    public static Result Fit(
        StoryEventController controller,
        PlayerStatsData fallbackPlayerStatsData,
        bool onlyPlayerTracks,
        float paddingSeconds,
        float minDuration,
        bool logDetails)
    {
        if (controller == null)
        {
            Debug.LogWarning("[StoryMoveClipDurationFitter] StoryEventController is missing.");
            return new Result(0, 0, 0);
        }

        PlayableDirector director = controller.Director;
        if (director == null)
        {
            Debug.LogWarning("[StoryMoveClipDurationFitter] PlayableDirector is missing.", controller);
            return new Result(0, 0, 0);
        }

        TimelineAsset timeline = director.playableAsset as TimelineAsset;
        if (timeline == null)
        {
            Debug.LogWarning("[StoryMoveClipDurationFitter] PlayableDirector has no TimelineAsset.", director);
            return new Result(0, 0, 0);
        }

        Undo.RecordObject(timeline, "Fit Move Clips To Walk Speed");

        int scannedClips = 0;
        int changedClips = 0;
        int skippedClips = 0;

        foreach (TrackAsset track in timeline.GetOutputTracks())
        {
            if (!(track is StoryObjectMoveTrack))
            {
                continue;
            }

            Transform boundTarget = ResolveBoundTransform(director.GetGenericBinding(track));
            if (boundTarget == null)
            {
                skippedClips += CountMoveClips(track);
                if (logDetails)
                {
                    Debug.LogWarning(
                        $"[StoryMoveClipDurationFitter] Track '{track.name}' has no Transform binding.",
                        timeline);
                }

                continue;
            }

            PlayerController playerController = ResolvePlayerController(boundTarget);
            if (onlyPlayerTracks && playerController == null)
            {
                skippedClips += CountMoveClips(track);
                continue;
            }

            PlayerStatsData statsData = playerController != null
                ? playerController.GetPlayerStatsData()
                : fallbackPlayerStatsData;
            float moveSpeed = statsData != null ? statsData.MoveSpeed : 0.0f;
            if (moveSpeed <= 0.0f)
            {
                skippedClips += CountMoveClips(track);
                Debug.LogWarning(
                    $"[StoryMoveClipDurationFitter] Move speed is missing or zero. Track '{track.name}' was skipped.",
                    boundTarget);
                continue;
            }

            Vector3 virtualPosition = boundTarget.position;
            List<TimelineClip> clips = CollectMoveClips(track);
            clips.Sort(CompareClipStart);

            for (int i = 0; i < clips.Count; i++)
            {
                TimelineClip clip = clips[i];
                StoryObjectMoveClip moveClip = clip.asset as StoryObjectMoveClip;
                if (moveClip == null)
                {
                    continue;
                }

                scannedClips++;

                Vector3 targetPosition = ResolveTargetPosition(controller, moveClip, virtualPosition);
                if (playerController != null)
                {
                    targetPosition.y = virtualPosition.y;
                    targetPosition.z = virtualPosition.z;
                }

                float distance = playerController != null
                    ? Mathf.Abs(targetPosition.x - virtualPosition.x)
                    : Vector3.Distance(targetPosition, virtualPosition);
                virtualPosition = targetPosition;

                if (distance <= Mathf.Epsilon)
                {
                    skippedClips++;
                    continue;
                }

                double desiredDuration = Mathf.Max(minDuration, (distance / moveSpeed) + paddingSeconds);
                if (System.Math.Abs(clip.duration - desiredDuration) <= ChangeEpsilon)
                {
                    continue;
                }

                clip.duration = desiredDuration;
                changedClips++;

                if (logDetails)
                {
                    Debug.Log(
                        $"[StoryMoveClipDurationFitter] {timeline.name}/{track.name}/{clip.displayName}: " +
                        $"distance={distance:0.###}, speed={moveSpeed:0.###}, duration={desiredDuration:0.###}",
                        timeline);
                }

                WarnIfOverlapsNextClip(timeline, track, clip, clips, i);
            }
        }

        if (changedClips > 0)
        {
            EditorUtility.SetDirty(timeline);
            TimelineEditor.Refresh(RefreshReason.ContentsModified);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }

        Debug.Log(
            $"[StoryMoveClipDurationFitter] Done. scanned={scannedClips}, changed={changedClips}, skipped={skippedClips}",
            timeline);

        return new Result(scannedClips, changedClips, skippedClips);
    }

    private static StoryEventController ResolveSelectedController()
    {
        if (Selection.activeGameObject == null)
        {
            return null;
        }

        return Selection.activeGameObject.GetComponentInParent<StoryEventController>();
    }

    private static Transform ResolveBoundTransform(Object binding)
    {
        if (binding is Transform transform)
        {
            return transform;
        }

        if (binding is GameObject gameObject)
        {
            return gameObject.transform;
        }

        if (binding is Component component)
        {
            return component.transform;
        }

        return null;
    }

    private static PlayerController ResolvePlayerController(Transform target)
    {
        if (target == null)
        {
            return null;
        }

        PlayerController controller = target.GetComponent<PlayerController>();
        return controller != null ? controller : target.GetComponentInParent<PlayerController>();
    }

    private static Vector3 ResolveTargetPosition(
        StoryEventController controller,
        StoryObjectMoveClip moveClip,
        Vector3 fallback)
    {
        Vector3 resolved = moveClip.worldPosition;
        if (moveClip.targetMode == StoryObjectMoveTargetMode.Marker)
        {
            Transform marker = controller.GetMarkerTransform(moveClip.markerNo);
            resolved = marker != null ? marker.position : fallback;
        }

        if (!moveClip.moveX)
        {
            resolved.x = fallback.x;
        }

        if (!moveClip.moveY)
        {
            resolved.y = fallback.y;
        }

        if (moveClip.keepCurrentZ)
        {
            resolved.z = fallback.z;
        }

        return resolved;
    }

    private static List<TimelineClip> CollectMoveClips(TrackAsset track)
    {
        var clips = new List<TimelineClip>();
        foreach (TimelineClip clip in track.GetClips())
        {
            if (clip.asset is StoryObjectMoveClip)
            {
                clips.Add(clip);
            }
        }

        return clips;
    }

    private static int CountMoveClips(TrackAsset track)
    {
        int count = 0;
        foreach (TimelineClip clip in track.GetClips())
        {
            if (clip.asset is StoryObjectMoveClip)
            {
                count++;
            }
        }

        return count;
    }

    private static int CompareClipStart(TimelineClip left, TimelineClip right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left == null)
        {
            return 1;
        }

        if (right == null)
        {
            return -1;
        }

        return left.start.CompareTo(right.start);
    }

    private static void WarnIfOverlapsNextClip(
        TimelineAsset timeline,
        TrackAsset track,
        TimelineClip clip,
        List<TimelineClip> clips,
        int clipIndex)
    {
        int nextIndex = clipIndex + 1;
        if (nextIndex >= clips.Count)
        {
            return;
        }

        TimelineClip nextClip = clips[nextIndex];
        if (nextClip == null || clip.end <= nextClip.start + ChangeEpsilon)
        {
            return;
        }

        Debug.LogWarning(
            $"[StoryMoveClipDurationFitter] '{clip.displayName}' now overlaps the next Move clip on " +
            $"'{timeline.name}/{track.name}'. Move later clips or shorten the distance.",
            timeline);
    }
}
