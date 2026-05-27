using System.ComponentModel;
using UnityEngine;
using UnityEngine.Timeline;

[TrackColor(0.3f, 0.75f, 0.45f)]
[TrackBindingType(typeof(Transform))]
[TrackClipType(typeof(StoryObjectMoveClip))]
[DisplayName("Story/Object Move Track")]
public sealed class StoryObjectMoveTrack : TrackAsset
{
}
