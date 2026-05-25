using System.ComponentModel;
using UnityEngine;
using UnityEngine.Timeline;

[TrackColor(0.95f, 0.55f, 0.25f)]
[TrackBindingType(typeof(Animator))]
[TrackClipType(typeof(StoryAnimatorClip))]
[DisplayName("Story/Animator Track")]
public sealed class StoryAnimatorTrack : TrackAsset
{
}
