using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[DisplayName("Story/Camera Shake")]
public sealed class StoryCameraShakeClip : PlayableAsset, ITimelineClipAsset
{
    [Min(0f)] public float force = 1f;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<StoryCameraShakePlayable> playable =
            ScriptPlayable<StoryCameraShakePlayable>.Create(graph);
        playable.GetBehaviour().force = force;
        return playable;
    }
}
