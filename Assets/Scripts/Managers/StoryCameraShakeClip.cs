using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[DisplayName("Story/Camera Shake")]
public sealed class StoryCameraShakeClip : PlayableAsset, ITimelineClipAsset
{
    [Min(0f)] public float force = 1f;
    public StoryCameraShakeDirection direction = StoryCameraShakeDirection.Horizontal;
    public Vector2 customDirection = Vector2.right;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<StoryCameraShakePlayable> playable =
            ScriptPlayable<StoryCameraShakePlayable>.Create(graph);
        StoryCameraShakePlayable behaviour = playable.GetBehaviour();
        behaviour.force = force;
        behaviour.direction = direction;
        behaviour.customDirection = customDirection;
        return playable;
    }
}

public enum StoryCameraShakeDirection
{
    Horizontal = 0,
    Vertical = 1,
    Diagonal = 2,
    Custom = 3,
}
