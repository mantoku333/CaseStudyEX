using System.ComponentModel;
using UnityEngine;
using UnityEngine.Timeline;

[System.Serializable]
[HideInMenu]
[DisplayName("Story/Audio Point")]
public sealed class StoryAudioMarker : Marker
{
    [SerializeField] private StoryTimelineAudioKind audioKind = StoryTimelineAudioKind.Se;
    [SerializeField] private StoryTimelineAudioAction action = StoryTimelineAudioAction.Play;
    [SerializeField] private AudioClip audioClip;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;
    [SerializeField] private bool loop = true;
    [SerializeField, Min(0f)] private float fadeSeconds = 0.5f;

    public StoryTimelineAudioKind AudioKind => audioKind;
    public StoryTimelineAudioAction Action => action;
    public AudioClip AudioClip => audioClip;
    public float Volume => Mathf.Clamp01(volume);
    public bool Loop => loop;
    public float FadeSeconds => Mathf.Max(0f, fadeSeconds);
}
