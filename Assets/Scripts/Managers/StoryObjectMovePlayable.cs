using UnityEngine;
using UnityEngine.Playables;

public sealed class StoryObjectMovePlayable : PlayableBehaviour
{
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

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        initialized = false;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Transform boundTarget = ResolveBoundTarget(playerData);
        if (boundTarget == null)
        {
            return;
        }

        if (!initialized || target != boundTarget)
        {
            target = boundTarget;
            startPosition = target.position;
            targetPosition = ResolveTargetPosition(playable, startPosition);
            initialized = true;
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
        PlayableDirector director = playable.GetGraph().GetResolver() as PlayableDirector;
        return director != null ? director.GetComponent<StoryEventController>() : null;
    }
}
