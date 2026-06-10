using System.ComponentModel;
using UnityEngine;
using UnityEngine.Timeline;

[TrackColor(1f, 0.3f, 0.25f)]
[TrackClipType(typeof(StoryCameraShakeClip))]
[DisplayName("Camera Shake Track")]
public sealed class StoryCameraShakeTrack : TrackAsset
{
}
