using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[DisplayName("Story/Object Move")]
public sealed class StoryObjectMoveClip : PlayableAsset, ITimelineClipAsset
{
    public StoryObjectMoveTargetMode targetMode = StoryObjectMoveTargetMode.Marker;
    [Min(1)] public int markerNo = 1;
    public Vector3 worldPosition;
    public bool keepCurrentZ = true;
    public bool moveX = true;
    public bool moveY = true;
    public bool smoothStep = true;

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.ClipIn;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<StoryObjectMovePlayable> playable =
            ScriptPlayable<StoryObjectMovePlayable>.Create(graph);
        StoryObjectMovePlayable behaviour = playable.GetBehaviour();
        behaviour.targetMode = targetMode;
        behaviour.markerNo = markerNo;
        behaviour.worldPosition = worldPosition;
        behaviour.keepCurrentZ = keepCurrentZ;
        behaviour.moveX = moveX;
        behaviour.moveY = moveY;
        behaviour.smoothStep = smoothStep;
        return playable;
    }
}
