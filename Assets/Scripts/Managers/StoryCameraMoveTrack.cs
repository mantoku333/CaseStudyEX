using System.ComponentModel;
using UnityEngine;
using UnityEngine.Timeline;

[TrackColor(0.2f, 0.45f, 1f)]
[TrackClipType(typeof(StoryCameraMoveClip))]
[DisplayName("Story/Camera Move Track")]
public sealed class StoryCameraMoveTrack : TrackAsset
{
}
