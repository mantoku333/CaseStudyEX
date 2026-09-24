using System;
using UnityEngine;

namespace Metroidvania.UI
{
    [CreateAssetMenu(menuName = "Dialogue/Portrait Animation", fileName = "PortraitAnimation")]
    public sealed class PortraitAnimationClip : ScriptableObject
    {
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [SerializeField, Min(1f)] private float framesPerSecond = 30f;
        [SerializeField] private bool loop = true;

        public int FrameCount => frames?.Length ?? 0;
        public float FramesPerSecond => framesPerSecond;
        public Sprite FirstFrame => GetFrame(0d);

        public Sprite GetFrame(double elapsedSeconds)
        {
            if (FrameCount == 0) return null;
            double frame = Math.Floor(Math.Max(0d, elapsedSeconds) * Math.Max(1f, framesPerSecond));
            if (double.IsNaN(frame) || double.IsInfinity(frame)) frame = 0d;
            int index = loop ? (int)(frame % FrameCount) : (int)Math.Min(frame, FrameCount - 1);
            // An incomplete frame list should not make the portrait disappear.
            if (frames[index] != null) return frames[index];
            for (int i = 0; i < FrameCount; i++)
                if (frames[i] != null) return frames[i];
            return null;
        }
    }
}
