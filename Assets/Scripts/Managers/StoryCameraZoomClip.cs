using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[DisplayName("Story/Camera Zoom")]
public sealed class StoryCameraZoomClip : PlayableAsset, ITimelineClipAsset
{
    [Min(0.01f)] public float orthographicSize = 5f;
    public bool smoothStep = true;

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.ClipIn;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<StoryCameraPlayable> playable =
            ScriptPlayable<StoryCameraPlayable>.Create(graph);
        StoryCameraPlayable behaviour = playable.GetBehaviour();
        behaviour.moveCamera = false;
        behaviour.zoomCamera = true;
        behaviour.orthographicSize = orthographicSize;
        behaviour.smoothStep = smoothStep;
        return playable;
    }
}
