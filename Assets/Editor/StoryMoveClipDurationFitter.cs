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

            List<TimelineClip> clips = CollectMoveClips(track);
            clips.Sort(CompareClipStart);
            var virtualPositions = new Dictionary<Transform, Vector3>();

            for (int i = 0; i < clips.Count; i++)
            {
                TimelineClip clip = clips[i];
                StoryObjectMoveClip moveClip = clip.asset as StoryObjectMoveClip;
                if (moveClip == null)
                {
                    continue;
                }

                scannedClips++;

                Transform moveTarget = ResolveMoveTarget(
                    controller,
                    director,
                    moveClip,
                    out bool usesImplicitPlayerFallback);
                if (moveTarget == null)
                {
                    skippedClips++;
                    if (logDetails)
                    {
                        Debug.LogWarning(
                            $"[StoryMoveClipDurationFitter] Move target was not found. Track='{track.name}', clip='{clip.displayName}'",
                            timeline);
                    }

                    continue;
                }

                PlayerController playerController = ResolvePlayerController(moveTarget);
                if (onlyPlayerTracks && playerController == null)
                {
                    skippedClips++;
                    continue;
                }

                PlayerStatsData statsData = playerController != null
                    ? playerController.GetPlayerStatsData()
                    : fallbackPlayerStatsData;
                float moveSpeed = statsData != null ? statsData.MoveSpeed : 0.0f;
                if (moveSpeed <= 0.0f)
                {
                    skippedClips++;
                    Debug.LogWarning(
                        $"[StoryMoveClipDurationFitter] Move speed is missing or zero. Clip '{clip.displayName}' was skipped.",
                        moveTarget);
                    continue;
                }

                bool hasVirtualPosition = virtualPositions.TryGetValue(moveTarget, out Vector3 virtualPosition);
                if (!hasVirtualPosition && usesImplicitPlayerFallback)
                {
                    if (!TryResolveEventAreaStartPosition(controller, out virtualPosition))
                    {
                        skippedClips++;
                        if (logDetails)
                        {
                            Debug.LogWarning(
                                $"[StoryMoveClipDurationFitter] Move target for actorKey='{moveClip.actorKey}' falls back to Player, " +
                                $"but no EventArea trigger was found for eventId='{controller.EventId}'. " +
                                $"Track='{track.name}', clip='{clip.displayName}'",
                                timeline);
                        }

                        continue;
                    }
                }
                else if (!hasVirtualPosition)
                {
                    virtualPosition = moveTarget.position;
                }

                Vector3 targetPosition = ResolveTargetPosition(controller, moveClip, virtualPosition);
                if (playerController != null)
                {
                    targetPosition.y = virtualPosition.y;
                    targetPosition.z = virtualPosition.z;
                }

                if (!hasVirtualPosition && usesImplicitPlayerFallback &&
                    TryResolveEventAreaStartPosition(controller, targetPosition, out Vector3 eventAreaEdgePosition))
                {
                    virtualPosition = eventAreaEdgePosition;
                    targetPosition = ResolveTargetPosition(controller, moveClip, virtualPosition);
                    if (playerController != null)
                    {
                        targetPosition.y = virtualPosition.y;
                        targetPosition.z = virtualPosition.z;
                    }
                }

                float distance = playerController != null
                    ? Mathf.Abs(targetPosition.x - virtualPosition.x)
                    : Vector3.Distance(targetPosition, virtualPosition);
                virtualPositions[moveTarget] = targetPosition;

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

    private static bool TryResolveEventAreaStartPosition(
        StoryEventController controller,
        out Vector3 startPosition)
    {
        startPosition = Vector3.zero;
        StoryEventTrigger2D trigger = FindEventTrigger(controller);
        if (trigger == null)
        {
            return false;
        }

        startPosition = trigger.transform.position;
        return true;
    }

    private static bool TryResolveEventAreaStartPosition(
        StoryEventController controller,
        Vector3 targetPosition,
        out Vector3 startPosition)
    {
        startPosition = Vector3.zero;
        StoryEventTrigger2D trigger = FindEventTrigger(controller);
        if (trigger == null)
        {
            return false;
        }

        Collider2D triggerCollider = trigger.GetComponent<Collider2D>();
        if (triggerCollider == null)
        {
            startPosition = trigger.transform.position;
            return true;
        }

        Bounds bounds = triggerCollider.bounds;
        Vector3 closest = bounds.ClosestPoint(targetPosition);
        closest.z = trigger.transform.position.z;
        startPosition = closest;
        return true;
    }

    private static StoryEventTrigger2D FindEventTrigger(StoryEventController controller)
    {
        if (controller == null || string.IsNullOrWhiteSpace(controller.EventId))
        {
            return null;
        }

        string eventId = controller.EventId.Trim();
        StoryEventTrigger2D[] triggers = Object.FindObjectsByType<StoryEventTrigger2D>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        StoryEventTrigger2D fallback = null;
        for (int i = 0; i < triggers.Length; i++)
        {
            StoryEventTrigger2D trigger = triggers[i];
            if (trigger == null ||
                !string.Equals(trigger.EventId, eventId, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (trigger.gameObject.scene.IsValid() &&
                controller.gameObject.scene.IsValid() &&
                trigger.gameObject.scene == controller.gameObject.scene)
            {
                return trigger;
            }

            fallback ??= trigger;
        }

        return fallback;
    }

    private static Transform ResolveMoveTarget(
        StoryEventController controller,
        IExposedPropertyTable resolver,
        StoryObjectMoveClip moveClip,
        out bool usesImplicitPlayerFallback)
    {
        usesImplicitPlayerFallback = false;
        if (moveClip == null)
        {
            return null;
        }

        Transform directTarget = moveClip.target.Resolve(resolver);
        if (directTarget != null)
        {
            return directTarget;
        }

        if (controller == null)
        {
            return null;
        }

        string key = string.IsNullOrWhiteSpace(moveClip.actorKey) ? "iris" : moveClip.actorKey.Trim();
        if (controller.TryGetAuthoredActorTransform(key, out Transform authoredTarget) && authoredTarget != null)
        {
            return authoredTarget;
        }

        usesImplicitPlayerFallback = true;
        return controller.GetActorTransform(key);
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
