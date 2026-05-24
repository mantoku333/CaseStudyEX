using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[DisplayName("Story/Animator")]
public sealed class StoryAnimatorClip : PlayableAsset, ITimelineClipAsset
{
    public StoryAnimatorActionType actionType = StoryAnimatorActionType.PlayState;
    public string parameterOrStateName = "idle";
    public int layer = 0;
    [Range(0f, 1f)] public float normalizedTime;
    public bool boolValue = true;
    public bool restoreBoolOnClipEnd;
    public float floatValue;
    public int integerValue;

    public ClipCaps clipCaps => ClipCaps.ClipIn;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<StoryAnimatorPlayable> playable =
            ScriptPlayable<StoryAnimatorPlayable>.Create(graph);
        StoryAnimatorPlayable behaviour = playable.GetBehaviour();
        behaviour.actionType = actionType;
        behaviour.parameterOrStateName = parameterOrStateName;
        behaviour.layer = layer;
        behaviour.normalizedTime = normalizedTime;
        behaviour.boolValue = boolValue;
        behaviour.restoreBoolOnClipEnd = restoreBoolOnClipEnd;
        behaviour.floatValue = floatValue;
        behaviour.integerValue = integerValue;
        return playable;
    }
}
