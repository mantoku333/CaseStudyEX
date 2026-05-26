using System.ComponentModel;
using UnityEngine;
using UnityEngine.Timeline;

[TrackColor(0.75f, 0.45f, 1f)]
[TrackBindingType(typeof(StoryEventController))]
[TrackClipType(typeof(StoryYarnDialogueClip))]
[DisplayName("Story/Yarn Dialogue Track")]
public sealed class StoryYarnDialogueTrack : TrackAsset
{
}
