using UnityEngine;

namespace GameName.Audio
{
    [CreateAssetMenu(fileName = "SurfaceAudioProfile", menuName = "GameName/Audio/Surface Audio Profile")]
    public sealed class SurfaceAudioProfile : ScriptableObject
    {
        [Header("Footsteps")]
        [SerializeField] private AudioClip[] footstepClips = new AudioClip[0];
        [SerializeField] private FootstepPlaybackMode footstepPlaybackMode;
        [SerializeField] private FootstepClipOrder footstepClipOrder;
        [SerializeField, Min(0f)] private float footstepIntervalOverride;
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 1f;

        [Header("Landing")]
        [SerializeField] private AudioClip[] landingClips = new AudioClip[0];
        [SerializeField, Range(0f, 1f)] private float landingVolume = 1f;

        public AudioClip[] FootstepClips => footstepClips;
        public FootstepPlaybackMode FootstepMode => footstepPlaybackMode;
        public FootstepClipOrder FootstepOrder => footstepClipOrder;
        public float FootstepIntervalOverride => footstepIntervalOverride;
        public float FootstepVolume => footstepVolume;
        public AudioClip[] LandingClips => landingClips;
        public float LandingVolume => landingVolume;

        public enum FootstepPlaybackMode
        {
            OneShot,
            LoopFirstClip
        }

        public enum FootstepClipOrder
        {
            Random,
            Sequential
        }
    }
}
