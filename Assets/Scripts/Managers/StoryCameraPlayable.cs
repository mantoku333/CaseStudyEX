using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

public sealed class StoryCameraPlayable : PlayableBehaviour
{
    public StoryCameraTargetMode targetMode;
    public int markerNo = 1;
    public Vector3 worldPosition;
    public bool keepCurrentZ = true;
    public bool moveCamera = true;
    public bool zoomCamera = true;
    public float orthographicSize = 5f;
    public bool smoothStep = true;

    private bool initialized;
    private CinemachineCamera camera;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float startOrthographicSize;

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

        StoryEventController controller = ResolveStoryEventController(playable, playerData);
        if (controller == null)
        {
            return;
        }

        if (!initialized)
        {
            camera = controller.GetEventCameraForTimeline();
            if (camera == null)
            {
                return;
            }

            startPosition = camera.transform.position;
            targetPosition = ResolveTargetPosition(controller, startPosition);
            startOrthographicSize = camera.Lens.OrthographicSize;
            initialized = true;
        }

        if (camera == null)
        {
            return;
        }

        double duration = playable.GetDuration();
        float t = duration > 0d ? Mathf.Clamp01((float)(playable.GetTime() / duration)) : 1f;
        if (smoothStep)
        {
            t = t * t * (3f - 2f * t);
        }

        if (moveCamera)
        {
            camera.transform.position = Vector3.Lerp(startPosition, targetPosition, t);
        }

        if (zoomCamera)
        {
            LensSettings lens = camera.Lens;
            lens.OrthographicSize = Mathf.Lerp(startOrthographicSize, Mathf.Max(0.01f, orthographicSize), t);
            camera.Lens = lens;
        }
    }

    private Vector3 ResolveTargetPosition(StoryEventController controller, Vector3 fallback)
    {
        Vector3 target = worldPosition;
        if (targetMode == StoryCameraTargetMode.Marker)
        {
            Transform marker = controller.GetMarkerTransform(markerNo);
            target = marker != null ? marker.position : fallback;
        }

        if (keepCurrentZ)
        {
            target.z = fallback.z;
        }

        return target;
    }

    private static StoryEventController ResolveStoryEventController(Playable playable, object playerData)
    {
        if (playerData is StoryEventController boundController)
        {
            return boundController;
        }

        PlayableDirector director = playable.GetGraph().GetResolver() as PlayableDirector;
        return director != null ? director.GetComponent<StoryEventController>() : null;
    }
}
