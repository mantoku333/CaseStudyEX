using System.ComponentModel;
using UnityEngine;
using UnityEngine.Timeline;

[TrackColor(0.85f, 0.55f, 0.15f)]
[TrackClipType(typeof(StoryAudioClip))]
[DisplayName("Audio Track")]
public sealed class StoryAudioTrack : TrackAsset
{
}
