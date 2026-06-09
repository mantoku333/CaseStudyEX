using System.ComponentModel;
using UnityEngine;
using UnityEngine.Timeline;

[TrackColor(0.05f, 0.05f, 0.05f)]
[TrackClipType(typeof(StoryFadeClip))]
[DisplayName("Fade Track")]
public sealed class StoryFadeTrack : TrackAsset
{
}
