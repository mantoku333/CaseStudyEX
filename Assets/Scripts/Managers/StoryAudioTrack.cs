using System.ComponentModel;
using UnityEngine;
using UnityEngine.Timeline;

[TrackColor(0.85f, 0.55f, 0.15f)]
[TrackBindingType(typeof(StoryEventController))]
[TrackClipType(typeof(StoryAudioClip))]
[DisplayName("Story/Audio Track")]
public sealed class StoryAudioTrack : TrackAsset
{
}
