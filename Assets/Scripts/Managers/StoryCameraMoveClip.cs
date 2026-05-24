using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[DisplayName("Story/Camera Move")]
public sealed class StoryCameraMoveClip : PlayableAsset, ITimelineClipAsset
{
    public StoryCameraTargetMode targetMode = StoryCameraTargetMode.Marker;
    [Min(1)] public int markerNo = 1;
    public Vector3 worldPosition;
    public bool keepCurrentZ = true;
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
        behaviour.moveCamera = true;
        behaviour.zoomCamera = false;
        behaviour.smoothStep = smoothStep;
        return playable;
    }
}
