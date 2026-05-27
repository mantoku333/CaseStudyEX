using System.ComponentModel;
using UnityEngine;
using UnityEngine.Timeline;

[TrackColor(0.3f, 0.75f, 1f)]
[TrackBindingType(typeof(StoryEventController))]
[DisplayName("Story/Story Event Track")]
public sealed class StoryEventTrack : TrackAsset
{
}
