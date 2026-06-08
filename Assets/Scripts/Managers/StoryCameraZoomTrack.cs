using System.ComponentModel;
using UnityEngine;
using UnityEngine.Timeline;

[TrackColor(0.35f, 0.65f, 1f)]
[TrackClipType(typeof(StoryCameraZoomClip))]
[DisplayName("Camera Zoom Track")]
public sealed class StoryCameraZoomTrack : TrackAsset
{
}
