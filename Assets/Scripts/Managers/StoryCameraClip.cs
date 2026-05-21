using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum StoryCameraTargetMode
{
    Marker = 0,
    WorldPosition = 1,
}

[DisplayName("Story/Camera Move Zoom")]
public sealed class StoryCameraClip : PlayableAsset, ITimelineClipAsset
{
    public StoryCameraTargetMode targetMode = StoryCameraTargetMode.Marker;
    [Min(1)] public int markerNo = 1;
    public Vector3 worldPosition;
    public bool keepCurrentZ = true;
    public bool moveCamera = true;
    public bool zoomCamera = true;
    [Min(0.01f)] public float orthographicSize = 5f;
    public bool smoothStep = true;

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.ClipIn;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<StoryCameraPlayable> playable =
            ScriptPlayable<StoryCameraPlayable>.Create(graph);
        StoryCameraPlayable behaviour = playable.GetBehaviour();
        behaviour.targetMode = targetMode;
        behaviour.markerNo = markerNo;
        behaviour.worldPosition = worldPosition;
        behaviour.keepCurrentZ = keepCurrentZ;
        behaviour.moveCamera = moveCamera;
        behaviour.zoomCamera = zoomCamera;
        behaviour.orthographicSize = orthographicSize;
        behaviour.smoothStep = smoothStep;
        return playable;
    }
}
