using UnityEngine;
using UnityEngine.Playables;

public sealed class StoryAudioPlayable : PlayableBehaviour
{
    public StoryTimelineAudioKind audioKind;
    public StoryTimelineAudioAction action;
    public AudioClip audioClip;
    public float volume = 1f;
    public bool loop = true;
    public float fadeSeconds = 0.5f;

    private bool fired;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        if (fired || !Application.isPlaying)
        {
            return;
        }

        fired = true;

        StoryTimelineRuntime.Instance.PlayTimelineAudio(
            audioKind,
            action,
            audioClip,
            volume,
            loop,
            fadeSeconds);
    }

    public override void OnGraphStop(Playable playable)
    {
        fired = false;
    }
}
