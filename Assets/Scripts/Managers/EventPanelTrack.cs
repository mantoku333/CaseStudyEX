using System.ComponentModel;
using UnityEngine;
using UnityEngine.Timeline;

[TrackColor(0.95f, 0.78f, 0.32f)]
[TrackBindingType(typeof(StoryEventController))]
[TrackClipType(typeof(EventPanelClip))]
[DisplayName("Event/Panel Track")]
public sealed class EventPanelTrack : TrackAsset
{
}
