using GameName.Audio;
using UnityEngine;

namespace Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerFootstepController : MonoBehaviour
    {
        [Header("Footstep Clips")]
        [SerializeField] private AudioClip[] footstepClips = new AudioClip[0];

        [Header("Landing Clips")]
        [SerializeField] private AudioClip[] landingClips = new AudioClip[0];
        [SerializeField, Min(0f)] private float landingVelocityThreshold = 2f;

        [Header("Surface Detection")]
        [SerializeField] private LayerMask surfaceLayerMask;
        [SerializeField, Min(0.01f)] private float surfaceProbeDistance = 1.2f;
        [SerializeField] private SurfaceAudioProfile fallbackSurfaceProfile;

        [Header("Playback")]
        [SerializeField, Min(0.05f)] private float stepInterval = 0.35f;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float landingVolume = 0.9f;
        [SerializeField, Min(0f)] private float minMoveSpeed = 0.1f;
        [SerializeField, Range(0f, 0.5f)] private float pitchRandomRange = 0.08f;
        [SerializeField] private AudioSource footstepAudioSource;

        [Header("Debug")]
        [SerializeField] private bool logFootstepDebug;
        [SerializeField, Min(0.1f)] private float debugLogInterval = 0.5f;

        [SerializeField] private bool useAnimationEventsForFootsteps = true;

        private Rigidbody2D rigidBody2d;
        private IPlayerViewStateProvider stateProvider;
        private float stepTimer;
        private float defaultPitch;
        private bool hasGroundState;
        private bool wasGrounded;
        private float minAirborneVelocityY;
        private float nextDebugLogTime;
        private int nextFootstepClipIndex;

        private void Awake()
        {
            rigidBody2d = GetComponent<Rigidbody2D>();
            stateProvider = GetComponent<IPlayerViewStateProvider>();
            EnsureFootstepAudioSource();

            if (footstepAudioSource != null)
            {
                defaultPitch = footstepAudioSource.pitch;
            }
        }

        private void Update()
        {
            UpdateLandingSound();

            if (useAnimationEventsForFootsteps)
            {
                StopLoopingFootstep();
                return;
            }

            if (!ShouldPlayFootsteps())
            {
                StopLoopingFootstep();
                stepTimer = 0f;
                LogWaitingForFootstep();
                return;
            }

            SurfaceAudioProfile surfaceProfile = ResolveCurrentSurfaceProfile();
            if (ShouldLoopFootstep(surfaceProfile))
            {
                stepTimer = 0f;
                PlayLoopingFootstep(surfaceProfile);
                return;
            }

            StopLoopingFootstep();
            stepTimer -= Time.deltaTime;
            if (stepTimer > 0f)
            {
                return;
            }

            PlayFootstep(surfaceProfile);
            stepTimer = ResolveFootstepInterval(surfaceProfile);
        }

        private void UpdateLandingSound()
        {
            if (stateProvider == null)
            {
                return;
            }

            bool isGrounded = stateProvider.IsGrounded;

            if (!hasGroundState)
            {
                hasGroundState = true;
                wasGrounded = isGrounded;
                minAirborneVelocityY = 0f;
                return;
            }

            if (!isGrounded && rigidBody2d != null)
            {
                minAirborneVelocityY = Mathf.Min(minAirborneVelocityY, rigidBody2d.linearVelocity.y);
            }

            if (!wasGrounded && isGrounded)
            {
                if (Mathf.Abs(minAirborneVelocityY) >= landingVelocityThreshold)
                {
                    SurfaceAudioProfile surfaceProfile = ResolveCurrentSurfaceProfile();
                    PlayRandomClip(
                        ResolveLandingClips(surfaceProfile),
                        ResolveLandingVolume(surfaceProfile));
                }

                minAirborneVelocityY = 0f;
            }

            if (wasGrounded && !isGrounded)
            {
                minAirborneVelocityY = 0f;
            }

            wasGrounded = isGrounded;
        }

        private bool ShouldPlayFootsteps()
        {
            if (footstepAudioSource == null)
            {
                return false;
            }

            if (stateProvider == null || !stateProvider.IsGrounded || !stateProvider.IsMoving)
            {
                return false;
            }

            if (rigidBody2d == null)
            {
                return true;
            }

            return Mathf.Abs(rigidBody2d.linearVelocity.x) >= minMoveSpeed;
        }

        private void PlayFootstep(SurfaceAudioProfile surfaceProfile)
        {
            PlayClip(
                ResolveFootstepClips(surfaceProfile),
                ResolveFootstepVolume(surfaceProfile),
                ResolveFootstepOrder(surfaceProfile));
        }

        private bool ShouldLoopFootstep(SurfaceAudioProfile surfaceProfile)
        {
            return surfaceProfile != null &&
                surfaceProfile.FootstepMode == SurfaceAudioProfile.FootstepPlaybackMode.LoopFirstClip &&
                GetFirstClip(ResolveFootstepClips(surfaceProfile)) != null;
        }

        private void PlayLoopingFootstep(SurfaceAudioProfile surfaceProfile)
        {
            AudioClip loopClip = GetFirstClip(ResolveFootstepClips(surfaceProfile));
            if (loopClip == null || footstepAudioSource == null)
            {
                LogFootstepDebug("Loop footstep tried to play, but no AudioClip or AudioSource was available.");
                return;
            }

            float clipVolume = ResolveFootstepVolume(surfaceProfile);
            if (footstepAudioSource.isPlaying &&
                footstepAudioSource.loop &&
                footstepAudioSource.clip == loopClip)
            {
                footstepAudioSource.volume = clipVolume;
                return;
            }

            footstepAudioSource.Stop();
            footstepAudioSource.clip = loopClip;
            footstepAudioSource.loop = true;
            footstepAudioSource.volume = clipVolume;
            footstepAudioSource.pitch = defaultPitch;
            footstepAudioSource.Play();
            LogFootstepDebug($"Loop footstep started: {loopClip.name}, volume: {clipVolume:0.00}");
        }

        private void StopLoopingFootstep()
        {
            if (footstepAudioSource == null || !footstepAudioSource.loop)
            {
                return;
            }

            footstepAudioSource.Stop();
            footstepAudioSource.clip = null;
            footstepAudioSource.loop = false;
            footstepAudioSource.volume = 1f;
            footstepAudioSource.pitch = defaultPitch;
            LogFootstepDebug("Loop footstep stopped.");
        }

        private SurfaceAudioProfile ResolveCurrentSurfaceProfile()
        {
            SurfaceAudioProfile profile = FindSurfaceProfileBelow();
            if (profile != null)
            {
                return profile;
            }

            return fallbackSurfaceProfile;
        }

        private SurfaceAudioProfile FindSurfaceProfileBelow()
        {
            int layerMask = surfaceLayerMask.value != 0
                ? surfaceLayerMask.value
                : Physics2D.AllLayers;

            RaycastHit2D hit = Physics2D.Raycast(
                transform.position,
                Vector2.down,
                surfaceProbeDistance,
                layerMask);

            if (hit.collider == null)
            {
                LogFootstepDebug("No ground was found below Player. Check Surface Layer Mask and Surface Probe Distance.");
                return null;
            }

            Vector2 surfacePoint = hit.point + Vector2.down * 0.02f;
            SurfaceAudioProfile zoneProfile = ResolveZoneProfile(surfacePoint);
            if (zoneProfile != null)
            {
                LogFootstepDebug($"Surface zone profile found: {zoneProfile.name}");
                return zoneProfile;
            }

            SurfaceAudioSource surfaceAudioSource = hit.collider.GetComponentInParent<SurfaceAudioSource>();
            SurfaceAudioProfile sourceProfile = surfaceAudioSource != null ? surfaceAudioSource.ResolveProfile(surfacePoint) : null;
            if (sourceProfile != null)
            {
                LogFootstepDebug($"Tilemap surface profile found: {sourceProfile.name}");
            }
            else
            {
                LogFootstepDebug($"Ground found, but no SurfaceAudioZone or SurfaceAudioSource was found. Ground: {hit.collider.name}");
            }

            return sourceProfile;
        }

        private SurfaceAudioProfile ResolveZoneProfile(Vector2 surfacePoint)
        {
            SurfaceAudioZone selectedZone = ResolveZoneAtPoint(surfacePoint, null);
            selectedZone = ResolveZoneAtPoint(transform.position, selectedZone);
            return selectedZone != null ? selectedZone.Profile : null;
        }

        private SurfaceAudioZone ResolveZoneAtPoint(Vector2 point, SurfaceAudioZone currentBestZone)
        {
            Collider2D[] hits = Physics2D.OverlapPointAll(point);
            SurfaceAudioZone selectedZone = null;

            if (currentBestZone != null)
            {
                selectedZone = currentBestZone;
            }

            for (int i = 0; i < hits.Length; i++)
            {
                SurfaceAudioZone zone = hits[i].GetComponentInParent<SurfaceAudioZone>();
                if (zone == null || zone.Profile == null)
                {
                    continue;
                }

                if (selectedZone == null || zone.Priority > selectedZone.Priority)
                {
                    selectedZone = zone;
                }
            }

            return selectedZone;
        }

        private AudioClip[] ResolveFootstepClips(SurfaceAudioProfile surfaceProfile)
        {
            if (surfaceProfile != null &&
                surfaceProfile.FootstepClips != null &&
                surfaceProfile.FootstepClips.Length > 0)
            {
                return surfaceProfile.FootstepClips;
            }

            return footstepClips;
        }

        private float ResolveFootstepVolume(SurfaceAudioProfile surfaceProfile)
        {
            return volume * (surfaceProfile != null ? surfaceProfile.FootstepVolume : 1f);
        }

        private float ResolveFootstepInterval(SurfaceAudioProfile surfaceProfile)
        {
            if (surfaceProfile != null && surfaceProfile.FootstepIntervalOverride > 0f)
            {
                return surfaceProfile.FootstepIntervalOverride;
            }

            return stepInterval;
        }

        private SurfaceAudioProfile.FootstepClipOrder ResolveFootstepOrder(SurfaceAudioProfile surfaceProfile)
        {
            return surfaceProfile != null
                ? surfaceProfile.FootstepOrder
                : SurfaceAudioProfile.FootstepClipOrder.Random;
        }

        private AudioClip[] ResolveLandingClips(SurfaceAudioProfile surfaceProfile)
        {
            if (surfaceProfile != null &&
                surfaceProfile.LandingClips != null &&
                surfaceProfile.LandingClips.Length > 0)
            {
                return surfaceProfile.LandingClips;
            }

            return landingClips;
        }

        private float ResolveLandingVolume(SurfaceAudioProfile surfaceProfile)
        {
            return landingVolume * (surfaceProfile != null ? surfaceProfile.LandingVolume : 1f);
        }

        private void PlayRandomClip(AudioClip[] clips, float clipVolume)
        {
            PlayClip(clips, clipVolume, SurfaceAudioProfile.FootstepClipOrder.Random);
        }

        private void PlayClip(
            AudioClip[] clips,
            float clipVolume,
            SurfaceAudioProfile.FootstepClipOrder clipOrder)
        {
            AudioClip clip = GetClip(clips, clipOrder);
            if (clip == null || footstepAudioSource == null)
            {
                LogFootstepDebug("Footstep tried to play, but no AudioClip or AudioSource was available.");
                return;
            }

            float pitchOffset = Random.Range(-pitchRandomRange, pitchRandomRange);
            footstepAudioSource.volume = 1f;
            footstepAudioSource.loop = false;
            footstepAudioSource.pitch = Mathf.Max(0.01f, defaultPitch + pitchOffset);
            footstepAudioSource.PlayOneShot(clip, clipVolume);
            LogFootstepDebug($"Footstep sound played: {clip.name}, volume: {clipVolume:0.00}");
        }

        private AudioClip GetFirstClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                {
                    return clips[i];
                }
            }

            return null;
        }

        private AudioClip GetClip(AudioClip[] clips, SurfaceAudioProfile.FootstepClipOrder clipOrder)
        {
            if (clipOrder == SurfaceAudioProfile.FootstepClipOrder.Sequential)
            {
                return GetSequentialClip(clips);
            }

            return GetRandomClip(clips);
        }

        private AudioClip GetSequentialClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            for (int attempt = 0; attempt < clips.Length; attempt++)
            {
                int index = nextFootstepClipIndex % clips.Length;
                nextFootstepClipIndex = (nextFootstepClipIndex + 1) % clips.Length;

                if (clips[index] != null)
                {
                    return clips[index];
                }
            }

            return null;
        }

        private AudioClip GetRandomClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            for (int attempt = 0; attempt < clips.Length; attempt++)
            {
                AudioClip clip = clips[Random.Range(0, clips.Length)];
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

        private void EnsureFootstepAudioSource()
        {
            if (footstepAudioSource != null)
            {
                return;
            }

            footstepAudioSource = gameObject.AddComponent<AudioSource>();
            footstepAudioSource.playOnAwake = false;
            footstepAudioSource.loop = false;
            footstepAudioSource.spatialBlend = 0f;
        }

        private void LogWaitingForFootstep()
        {
            if (!logFootstepDebug || Time.time < nextDebugLogTime)
            {
                return;
            }

            bool isGrounded = stateProvider != null && stateProvider.IsGrounded;
            bool isMoving = stateProvider != null && stateProvider.IsMoving;
            float horizontalSpeed = rigidBody2d != null ? Mathf.Abs(rigidBody2d.linearVelocity.x) : 0f;

            LogFootstepDebug(
                $"Waiting for footstep. Grounded: {isGrounded}, Moving: {isMoving}, Speed: {horizontalSpeed:0.00}");
        }

        private void LogFootstepDebug(string message)
        {
            if (!logFootstepDebug || Time.time < nextDebugLogTime)
            {
                return;
            }

            nextDebugLogTime = Time.time + debugLogInterval;
            Debug.Log($"[PlayerFootstepController] {message}", this);
        }
        public void PlayFootstepFromAnimationEvent()
        {
            if (!ShouldPlayFootsteps())
            {
                return;
            }

            SurfaceAudioProfile surfaceProfile = ResolveCurrentSurfaceProfile();
            PlayFootstep(surfaceProfile);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            stepInterval = Mathf.Max(0.05f, stepInterval);
            minMoveSpeed = Mathf.Max(0f, minMoveSpeed);
            landingVelocityThreshold = Mathf.Max(0f, landingVelocityThreshold);
            surfaceProbeDistance = Mathf.Max(0.01f, surfaceProbeDistance);
            debugLogInterval = Mathf.Max(0.1f, debugLogInterval);
        }
#endif
    }

}
