using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum StoryTimelineAudioKind
{
    Bgm = 0,
    Se = 1,
    Ambience = 2,
}

public enum StoryTimelineAudioAction
{
    Play = 0,
    Stop = 1,
}

[DisplayName("Story/Audio")]
public sealed class StoryAudioClip : PlayableAsset, ITimelineClipAsset
{
    public StoryTimelineAudioKind audioKind = StoryTimelineAudioKind.Se;
    public StoryTimelineAudioAction action = StoryTimelineAudioAction.Play;
    public AudioClip audioClip;
    [Range(0f, 1f)] public float volume = 1f;
    public bool loop = true;
    [Min(0f)] public float fadeSeconds = 0.5f;

    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<StoryAudioPlayable> playable = ScriptPlayable<StoryAudioPlayable>.Create(graph);
        StoryAudioPlayable behaviour = playable.GetBehaviour();
        behaviour.audioKind = audioKind;
        behaviour.action = action;
        behaviour.audioClip = audioClip;
        behaviour.volume = volume;
        behaviour.loop = loop;
        behaviour.fadeSeconds = fadeSeconds;
        return playable;
    }
}
