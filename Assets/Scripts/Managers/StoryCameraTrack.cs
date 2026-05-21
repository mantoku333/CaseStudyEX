using System.ComponentModel;
using UnityEngine;
using UnityEngine.Timeline;

[TrackColor(0.25f, 0.45f, 1f)]
[TrackBindingType(typeof(StoryEventController))]
[TrackClipType(typeof(StoryCameraClip))]
[DisplayName("Story/Camera Move Zoom Track")]
public sealed class StoryCameraTrack : TrackAsset
{
}
