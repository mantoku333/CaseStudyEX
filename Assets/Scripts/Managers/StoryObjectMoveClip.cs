using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[DisplayName("Story/Object Move")]
public sealed class StoryObjectMoveClip : PlayableAsset, ITimelineClipAsset
{
    public string actorKey = "iris";
    public ExposedReference<Transform> target;
    public StoryObjectMoveTargetMode targetMode = StoryObjectMoveTargetMode.Marker;
    [Min(1)] public int markerNo = 1;
    public Vector3 worldPosition;
    public bool keepCurrentZ = true;
    public bool moveX = true;
    public bool moveY = true;
    public bool smoothStep = true;
    [InspectorName("傘あり")]
    [Tooltip("プレイヤーを傘あり状態にして、移動後のアイドルも傘ありの見た目にします。")]
    public bool useUmbrellaWalk;

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.ClipIn;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<StoryObjectMovePlayable> playable =
            ScriptPlayable<StoryObjectMovePlayable>.Create(graph);
        StoryObjectMovePlayable behaviour = playable.GetBehaviour();
        behaviour.actorKey = actorKey;
        behaviour.targetReference = target;
        behaviour.targetMode = targetMode;
        behaviour.markerNo = markerNo;
        behaviour.worldPosition = worldPosition;
        behaviour.keepCurrentZ = keepCurrentZ;
        behaviour.moveX = moveX;
        behaviour.moveY = moveY;
        behaviour.smoothStep = smoothStep;
        behaviour.useUmbrellaWalk = useUmbrellaWalk;
        return playable;
    }
}
