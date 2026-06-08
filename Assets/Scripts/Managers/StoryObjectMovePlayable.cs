using UnityEngine;
using UnityEngine.Playables;

public sealed class StoryObjectMovePlayable : PlayableBehaviour
{
    public string actorKey = "iris";
    public ExposedReference<Transform> targetReference;
    public StoryObjectMoveTargetMode targetMode;
    public int markerNo = 1;
    public Vector3 worldPosition;
    public bool keepCurrentZ = true;
    public bool moveX = true;
    public bool moveY = true;
    public bool smoothStep = true;

    private bool initialized;
    private Transform target;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private PlayerController playerController;
    private bool loggedDurationWarning;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        initialized = false;
        playerController = null;
        loggedDurationWarning = false;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Transform boundTarget = ResolveMoveTarget(playable, playerData);
        if (boundTarget == null)
        {
            return;
        }

        if (!initialized || target != boundTarget)
        {
            target = boundTarget;
            startPosition = target.position;
            targetPosition = ResolveTargetPosition(playable, startPosition);
            playerController = ResolvePlayerController(target);
            if (playerController != null)
            {
                targetPosition.y = startPosition.y;
                targetPosition.z = startPosition.z;
                WarnIfPlayerMoveClipIsTooShort(playable);
                playerController.StartExternalMoveToX(targetPosition.x);
            }
            initialized = true;
        }

        if (playerController != null)
        {
            return;
        }

        double duration = playable.GetDuration();
        float t = duration > 0d ? Mathf.Clamp01((float)(playable.GetTime() / duration)) : 1f;
        if (smoothStep)
        {
            t = t * t * (3f - 2f * t);
        }

        Vector3 nextPosition = Vector3.Lerp(startPosition, targetPosition, t);
        target.position = nextPosition;
    }

    private void WarnIfPlayerMoveClipIsTooShort(Playable playable)
    {
        if (loggedDurationWarning || playerController == null)
        {
            return;
        }

        Player.PlayerStatsData statsData = playerController.GetPlayerStatsData();
        float moveSpeed = statsData != null ? statsData.MoveSpeed : 0.0f;
        if (moveSpeed <= 0.0f)
        {
            return;
        }

        float distance = Mathf.Abs(targetPosition.x - startPosition.x);
        double requiredDuration = distance / moveSpeed;
        double clipDuration = playable.GetDuration();
        if (requiredDuration <= clipDuration + 0.05d)
        {
            return;
        }

        loggedDurationWarning = true;
        Debug.LogWarning(
            $"[StoryObjectMovePlayable] Player Move clip is shorter than walk-speed travel time. " +
            $"distance={distance:0.###}, speed={moveSpeed:0.###}, clip={clipDuration:0.###}, required={requiredDuration:0.###}. " +
            "Use Tools/CaseStudy/Story/Fit Move Clips To Walk Speed.",
            playerController);
    }

    private Transform ResolveMoveTarget(Playable playable, object playerData)
    {
        Transform resolvedTarget = targetReference.Resolve(playable.GetGraph().GetResolver());
        if (resolvedTarget != null)
        {
            return resolvedTarget;
        }

        resolvedTarget = ResolveBoundTarget(playerData);
        if (resolvedTarget != null)
        {
            return resolvedTarget;
        }

        StoryEventController controller = ResolveStoryEventController(playable);
        if (controller == null)
        {
            return null;
        }

        string key = string.IsNullOrWhiteSpace(actorKey) ? "iris" : actorKey.Trim();
        return controller.GetActorTransform(key);
    }

    private static Transform ResolveBoundTarget(object playerData)
    {
        if (playerData is Transform transform)
        {
            return transform;
        }

        if (playerData is GameObject gameObject)
        {
            return gameObject.transform;
        }

        if (playerData is Component component)
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
        if (controller != null)
        {
            return controller;
        }

        return target.GetComponentInParent<PlayerController>();
    }

    private Vector3 ResolveTargetPosition(Playable playable, Vector3 fallback)
    {
        Vector3 resolved = worldPosition;
        if (targetMode == StoryObjectMoveTargetMode.Marker)
        {
            StoryEventController controller = ResolveStoryEventController(playable);
            Transform marker = controller != null ? controller.GetMarkerTransform(markerNo) : null;
            resolved = marker != null ? marker.position : fallback;
        }

        if (!moveX)
        {
            resolved.x = fallback.x;
        }

        if (!moveY)
        {
            resolved.y = fallback.y;
        }

        if (keepCurrentZ)
        {
            resolved.z = fallback.z;
        }

        return resolved;
    }

    private static StoryEventController ResolveStoryEventController(Playable playable)
    {
        PlayableDirector director = ResolvePlayableDirector(playable);
        return director != null ? director.GetComponent<StoryEventController>() : null;
    }

    private static PlayableDirector ResolvePlayableDirector(Playable playable)
    {
        return playable.GetGraph().GetResolver() as PlayableDirector;
    }

}
