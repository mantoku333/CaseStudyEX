using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[DisplayName("Story/Fade")]
public sealed class StoryFadeClip : PlayableAsset, ITimelineClipAsset
{
    [Range(0f, 1f)] public float fromAlpha = 0f;
    [Range(0f, 1f)] public float toAlpha = 1f;
    public bool useCurrentAlphaAsStart = true;
    public bool smoothStep = true;
    public Color color = Color.black;

    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.ClipIn;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<StoryFadePlayable> playable = ScriptPlayable<StoryFadePlayable>.Create(graph);
        StoryFadePlayable behaviour = playable.GetBehaviour();
        behaviour.fromAlpha = fromAlpha;
        behaviour.toAlpha = toAlpha;
        behaviour.useCurrentAlphaAsStart = useCurrentAlphaAsStart;
        behaviour.smoothStep = smoothStep;
        behaviour.color = color;
        return playable;
    }
}
