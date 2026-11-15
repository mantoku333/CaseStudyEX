using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [SerializeField] private CinemachineImpulseSource impulseSource;

    private bool isFollowCamActive = true;
    private CinemachineCamera followCam;
    private CinemachineCamera directFollowCam;
    private CinemachineCamera cachedDefaultSettingsFollowCam;
    private bool hasDefaultFollowOrthographicSize;
    private float defaultFollowOrthographicSize;
    private bool hasDefaultFollowComposerTargetOffsetY;
    private float defaultFollowComposerTargetOffsetY;
    private bool hasDefaultFollowComposerScreenPositionY;
    private float defaultFollowComposerScreenPositionY;
    private bool hasDefaultDirectFollowOffsetY;
    private float defaultDirectFollowOffsetY;
    private Coroutine restoreFollowCenterOnActivateRoutine;
    private CinemachinePositionComposer cachedFollowPositionComposer;
    private bool cachedFollowCenterOnActivate;
    private bool hasCachedFollowCenterOnActivate;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            // Keep only the manager object persistent.
            // Persisting transform.root can also carry an AudioListener and cause duplicates after scene loads.
            if (transform.parent != null)
            {
                transform.SetParent(null, true);
            }

            DontDestroyOnLoad(gameObject);
            Initialize();
            return;
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Initialize()
    {
        impulseSource = GetComponent<CinemachineImpulseSource>();
        if (impulseSource == null)
        {
            impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
        }

        EnsureImpulseSourceConfigured();
        FindCameras();
        EnsureImpulseListeners(ResolveImpulseChannel());
    }

    private void FindCameras()
    {
        var cameras = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        foreach (CinemachineCamera cam in cameras)
        {
            if (cam.gameObject.name == "CN_FollowCam")
            {
                followCam = cam;
            }
            else if (cam.gameObject.name == "CN_DirectFollowCam")
            {
                directFollowCam = cam;
            }
        }

        EnsurePlayerFollowBiasComponents();
        CacheDefaultFollowCameraSettings();
    }

    private void EnsurePlayerFollowBiasComponents()
    {
        EnsurePlayerFollowBiasComponent(followCam);
    }

    private static void EnsurePlayerFollowBiasComponent(CinemachineCamera camera)
    {
        if (camera == null)
        {
            return;
        }

        if (camera.GetComponent<FollowCameraFacingBias>() == null)
        {
            camera.gameObject.AddComponent<FollowCameraFacingBias>();
        }

        // The follow camera is used for every room that is too large to show at once. Without
        // bounds it wanders past the room edges, which is where the darkness outside large rooms
        // comes from and why the player's place in frame is not stable there.
        if (camera.GetComponent<FollowCameraRoomClamp>() == null)
        {
            camera.gameObject.AddComponent<FollowCameraRoomClamp>();
        }
    }

    public void ToggleCamera()
    {
        if (!EnsureCameraTargets())
        {
            return;
        }

        if (isFollowCamActive)
        {
            SwitchToDirectFollowCamera();
            return;
        }

        SwitchToFollowCamera();
    }

    public bool SwitchToFollowCamera()
    {
        if (!EnsureCameraTargets())
        {
            return false;
        }

        isFollowCamActive = true;
        followCam.Priority.Value = 10;
        directFollowCam.Priority.Value = 0;
        followCam.Priority.Enabled = true;
        directFollowCam.Priority.Enabled = true;
        Debug.Log("[CameraManager] Camera switched to CN_FollowCam");
        return true;
    }

    /// <summary>Forces Cinemachine to re-read the follow cameras' priorities this frame.</summary>
    public void PrioritizeFollowCameras()
    {
        if (followCam != null)
        {
            followCam.Prioritize();
        }

        if (directFollowCam != null)
        {
            directFollowCam.Prioritize();
        }
    }

    /// <summary>
    /// Recomputes the follow camera's pose now instead of at its next scheduled update.
    ///
    /// The player's Rigidbody2D is not interpolated, so under the brain's Smart Update the follow
    /// camera is only evaluated on physics steps (50Hz). A room change happens in LateUpdate.
    /// On that frame the camera still holds the pose from the last physics step, which was
    /// computed with the previous room's limits - coming from a static room that is the static
    /// room's own picture - while the fog has already moved to the new room. The result is a
    /// frame of black at every cut into a follow area, and it lasts for as long as physics is
    /// paused: a hit-stop sets timeScale to 0, so a cut that lands on one stayed black until
    /// time resumed.
    ///
    /// A camera whose previous state was invalidated snaps to its framing; one that is already
    /// following keeps its place (deltaTime 0 applies no damping) and only takes the new limits.
    /// </summary>
    public void RefreshFollowCameraStateNow()
    {
        if (followCam == null || !followCam.isActiveAndEnabled)
        {
            return;
        }

        followCam.InternalUpdateCameraState(
            Vector3.up,
            followCam.PreviousStateIsValid ? 0f : -1f);
    }

    public bool SwitchToDirectFollowCamera()
    {
        if (!EnsureCameraTargets())
        {
            return false;
        }

        isFollowCamActive = false;
        followCam.Priority.Value = 0;
        directFollowCam.Priority.Value = 10;
        followCam.Priority.Enabled = true;
        directFollowCam.Priority.Enabled = true;
        Debug.Log("[CameraManager] Camera switched to CN_DirectFollowCam");
        return true;
    }

    public bool TryGetFollowCameraPose(out Vector3 position, out float orthographicSize)
    {
        if (followCam == null)
        {
            FindCameras();
        }

        if (followCam == null)
        {
            position = Vector3.zero;
            orthographicSize = 0f;
            return false;
        }

        position = followCam.transform.position;
        orthographicSize = followCam.Lens.OrthographicSize;
        return true;
    }

    public bool TrySetFollowCameraPose(Vector3 position, float orthographicSize)
    {
        if (followCam == null)
        {
            FindCameras();
        }

        if (followCam == null)
        {
            return false;
        }

        followCam.transform.position = position;
        LensSettings lens = followCam.Lens;
        lens.OrthographicSize = orthographicSize;
        followCam.Lens = lens;
        return true;
    }

    public bool TrySetFollowCameraOrthographicSize(float orthographicSize)
    {
        if (followCam == null)
        {
            FindCameras();
        }

        if (followCam == null || orthographicSize <= 0f)
        {
            return false;
        }

        LensSettings lens = followCam.Lens;
        lens.OrthographicSize = orthographicSize;
        followCam.Lens = lens;
        return true;
    }

    public bool TryApplyFollowCameraAreaSettings(
        bool overrideOrthographicSize,
        float orthographicSize,
        bool overrideTargetOffsetY,
        float targetOffsetY,
        bool overrideScreenPositionY,
        float screenPositionY)
    {
        if (followCam == null)
        {
            FindCameras();
        }

        if (followCam == null)
        {
            return false;
        }

        CacheDefaultFollowCameraSettings();

        // Remember the framing so a change can be landed in one frame (see the end of this method).
        float previousOrthographicSize = followCam.Lens.OrthographicSize;
        CinemachinePositionComposer previousComposer = followCam.GetComponent<CinemachinePositionComposer>();
        float previousOffsetY = previousComposer != null ? previousComposer.TargetOffset.y : 0f;
        float previousScreenY = previousComposer != null ? previousComposer.Composition.ScreenPosition.y : 0f;

        if (overrideOrthographicSize && orthographicSize > 0f)
        {
            SetFollowCameraOrthographicSize(orthographicSize);
        }
        else if (hasDefaultFollowOrthographicSize)
        {
            SetFollowCameraOrthographicSize(defaultFollowOrthographicSize);
        }

        CinemachinePositionComposer positionComposer = followCam.GetComponent<CinemachinePositionComposer>();
        if (positionComposer != null)
        {
            if (overrideTargetOffsetY)
            {
                SetFollowComposerTargetOffsetY(positionComposer, targetOffsetY);
            }
            else if (hasDefaultFollowComposerTargetOffsetY)
            {
                SetFollowComposerTargetOffsetY(positionComposer, defaultFollowComposerTargetOffsetY);
            }

            if (overrideScreenPositionY)
            {
                SetFollowComposerScreenPositionY(positionComposer, screenPositionY);
            }
            else if (hasDefaultFollowComposerScreenPositionY)
            {
                SetFollowComposerScreenPositionY(positionComposer, defaultFollowComposerScreenPositionY);
            }
        }

        CinemachineFollow directFollow = followCam.GetComponent<CinemachineFollow>();
        if (directFollow != null)
        {
            if (overrideTargetOffsetY)
            {
                SetDirectFollowOffsetY(directFollow, targetOffsetY);
            }
            else if (hasDefaultDirectFollowOffsetY)
            {
                SetDirectFollowOffsetY(directFollow, defaultDirectFollowOffsetY);
            }
        }

        // Moving between two follow-camera areas whose zoom or offsets differ is an area change
        // like any other, so it has to land finished. Without this the lens changed at once while
        // the body eased to the new offsets over its 1 second damping: a jolt followed by a drift.
        // Areas with identical framing are left alone so a plain crossing stays continuous.
        bool framingChanged =
            !Mathf.Approximately(previousOrthographicSize, followCam.Lens.OrthographicSize) ||
            (previousComposer != null &&
             (!Mathf.Approximately(previousOffsetY, previousComposer.TargetOffset.y) ||
              !Mathf.Approximately(previousScreenY, previousComposer.Composition.ScreenPosition.y)));
        if (framingChanged)
        {
            followCam.PreviousStateIsValid = false;
        }

        return true;
    }

    /// <summary>
    /// Where the follow camera will sit, and at what zoom, once it is live in the given room.
    /// The pose read from the camera's transform is not that: it ignores the room clamp, and
    /// while another camera is live it is simply stale.
    /// </summary>
    public bool TryPredictFollowCameraPose(RoomCameraTrigger room, out Vector3 position, out float orthographicSize)
    {
        position = default;
        orthographicSize = 0f;

        if (followCam == null)
        {
            FindCameras();
        }

        if (followCam == null)
        {
            return false;
        }

        Transform target = followCam.Target.TrackingTarget;
        CinemachinePositionComposer composer = followCam.GetComponent<CinemachinePositionComposer>();
        if (target == null || composer == null)
        {
            return false;
        }

        CacheDefaultFollowCameraSettings();
        float baseOrtho = hasDefaultFollowOrthographicSize ? defaultFollowOrthographicSize : followCam.Lens.OrthographicSize;
        float baseOffsetY = hasDefaultFollowComposerTargetOffsetY ? defaultFollowComposerTargetOffsetY : composer.TargetOffset.y;
        float baseScreenY = hasDefaultFollowComposerScreenPositionY ? defaultFollowComposerScreenPositionY : composer.Composition.ScreenPosition.y;

        orthographicSize = room != null ? room.ResolveDefaultFollowCameraOrthographicSize(baseOrtho) : baseOrtho;
        float offsetY = room != null ? room.ResolveDefaultFollowCameraTargetOffsetY(baseOffsetY) : baseOffsetY;
        float screenY = room != null ? room.ResolveDefaultFollowCameraScreenPositionY(baseScreenY) : baseScreenY;

        Camera main = Camera.main;
        float aspect = main != null && main.aspect > 0.01f ? main.aspect : 16f / 9f;

        // Screen position: +x is right of centre, +y is BELOW centre (Cinemachine flips Y).
        Vector3 tracked = target.position + new Vector3(composer.TargetOffset.x, offsetY, 0f);
        float screenX = composer.Composition.ScreenPosition.x;
        position = new Vector3(
            tracked.x - screenX * orthographicSize * aspect * 2f,
            tracked.y + screenY * orthographicSize * 2f,
            followCam.transform.position.z);

        if (room != null && room.TryGetAreaBounds(out Bounds bounds))
        {
            position = FollowCameraRoomClamp.ClampToRoom(position, bounds, orthographicSize, aspect, target);
        }

        return true;
    }

    private void CacheDefaultFollowCameraSettings()
    {
        if (followCam == null || cachedDefaultSettingsFollowCam == followCam)
        {
            return;
        }

        cachedDefaultSettingsFollowCam = followCam;
        hasDefaultFollowOrthographicSize = true;
        defaultFollowOrthographicSize = followCam.Lens.OrthographicSize;

        CinemachinePositionComposer positionComposer = followCam.GetComponent<CinemachinePositionComposer>();
        hasDefaultFollowComposerTargetOffsetY = positionComposer != null;
        defaultFollowComposerTargetOffsetY = positionComposer != null
            ? positionComposer.TargetOffset.y
            : 0f;
        hasDefaultFollowComposerScreenPositionY = positionComposer != null;
        defaultFollowComposerScreenPositionY = positionComposer != null
            ? positionComposer.Composition.ScreenPosition.y
            : 0f;

        CinemachineFollow directFollow = followCam.GetComponent<CinemachineFollow>();
        hasDefaultDirectFollowOffsetY = directFollow != null;
        defaultDirectFollowOffsetY = directFollow != null
            ? directFollow.FollowOffset.y
            : 0f;
    }

    private void SetFollowCameraOrthographicSize(float orthographicSize)
    {
        LensSettings lens = followCam.Lens;
        lens.OrthographicSize = orthographicSize;
        followCam.Lens = lens;
    }

    private void SetFollowComposerTargetOffsetY(
        CinemachinePositionComposer positionComposer,
        float targetOffsetY)
    {
        FollowCameraFacingBias facingBias = followCam.GetComponent<FollowCameraFacingBias>();
        if (facingBias != null)
        {
            facingBias.SetBaseComposerOffsetY(targetOffsetY);
            return;
        }

        Vector3 targetOffset = positionComposer.TargetOffset;
        targetOffset.y = targetOffsetY;
        positionComposer.TargetOffset = targetOffset;
    }

    private static void SetFollowComposerScreenPositionY(
        CinemachinePositionComposer positionComposer,
        float screenPositionY)
    {
        var composition = positionComposer.Composition;
        Vector2 screenPosition = composition.ScreenPosition;
        screenPosition.y = screenPositionY;
        composition.ScreenPosition = screenPosition;
        positionComposer.Composition = composition;
    }

    private void SetDirectFollowOffsetY(CinemachineFollow directFollow, float offsetY)
    {
        FollowCameraFacingBias facingBias = followCam.GetComponent<FollowCameraFacingBias>();
        if (facingBias != null)
        {
            facingBias.SetBaseDirectFollowOffsetY(offsetY);
            return;
        }

        Vector3 followOffset = directFollow.FollowOffset;
        followOffset.y = offsetY;
        directFollow.FollowOffset = followOffset;
    }

    /// <summary>
    /// Makes the follow camera land fully settled on the frame it becomes active, instead of
    /// cutting to a stale pose and then sliding into place.
    ///
    /// The follow camera keeps its last pose while inactive, and its body damps at 1.0s. With a
    /// cut transition the brain shows that stale pose immediately and the damping then drags the
    /// view into position, which reads as a lurch right after an area change. Clearing
    /// PreviousStateIsValid makes the body skip damping for one evaluation so it resolves exactly
    /// onto its target, and the facing bias is reset so a look-ahead offset built up in the
    /// previous room does not unwind on screen afterwards.
    /// </summary>
    public bool SnapFollowCameraToTarget()
    {
        if (followCam == null)
        {
            FindCameras();
        }

        if (followCam == null)
        {
            return false;
        }

        // Nothing to snap if this camera is already the one being rendered. Snapping anyway
        // is visible as a jolt: moving between two adjoining free-camera rooms re-runs this
        // path, and forcing PreviousStateIsValid there threw the view sideways mid-follow
        // instead of leaving a continuous follow alone.
        if (followCam.IsLive)
        {
            return false;
        }

        FollowCameraFacingBias facingBias = followCam.GetComponent<FollowCameraFacingBias>();
        if (facingBias != null)
        {
            facingBias.ResetSmoothing();
        }

        followCam.PreviousStateIsValid = false;
        return true;
    }

    /// <summary>
    /// Prepares the follow camera to be cut to: look-ahead reset and body snapped onto its target,
    /// so the first frame after the cut is already the settled framing.
    ///
    /// Called before the priority is raised. Does nothing when the follow camera is already
    /// live, because then there is no change of view to absorb.
    /// </summary>
    public bool PrepareFollowCameraEntry()
    {
        if (followCam == null)
        {
            FindCameras();
        }

        if (followCam == null || followCam.IsLive)
        {
            return false;
        }

        FollowCameraFacingBias facingBias = followCam.GetComponent<FollowCameraFacingBias>();
        if (facingBias != null)
        {
            facingBias.ResetSmoothing();
        }

        // Cut, and land settled. An earlier version eased the camera in from the outgoing view
        // here. That made entering a free-camera area a half-second slide while leaving it was
        // an instant cut - the same boundary behaving differently in each direction - and it
        // kept the old room on screen while the fog was already closing over it. Every area
        // change is a cut now, in both directions.
        followCam.PreviousStateIsValid = false;
        return true;
    }

    public void SuppressFollowCameraCenterOnActivateForFrames(int frameCount = 3)
    {
        if (followCam == null)
        {
            FindCameras();
        }

        if (followCam == null)
        {
            return;
        }

        CinemachinePositionComposer positionComposer = followCam.GetComponent<CinemachinePositionComposer>();
        if (positionComposer == null)
        {
            return;
        }

        if (restoreFollowCenterOnActivateRoutine != null)
        {
            StopCoroutine(restoreFollowCenterOnActivateRoutine);
            restoreFollowCenterOnActivateRoutine = null;
        }

        if (!hasCachedFollowCenterOnActivate || cachedFollowPositionComposer != positionComposer)
        {
            cachedFollowPositionComposer = positionComposer;
            cachedFollowCenterOnActivate = positionComposer.CenterOnActivate;
            hasCachedFollowCenterOnActivate = true;
        }

        positionComposer.CenterOnActivate = false;
        restoreFollowCenterOnActivateRoutine =
            StartCoroutine(RestoreFollowCenterOnActivateAfterFrames(Mathf.Max(1, frameCount)));
    }

    private IEnumerator RestoreFollowCenterOnActivateAfterFrames(int frameCount)
    {
        for (int i = 0; i < frameCount; i++)
        {
            yield return null;
        }

        if (cachedFollowPositionComposer != null && hasCachedFollowCenterOnActivate)
        {
            cachedFollowPositionComposer.CenterOnActivate = cachedFollowCenterOnActivate;
        }

        cachedFollowPositionComposer = null;
        cachedFollowCenterOnActivate = false;
        hasCachedFollowCenterOnActivate = false;
        restoreFollowCenterOnActivateRoutine = null;
    }

    private bool EnsureCameraTargets()
    {
        if (followCam == null || directFollowCam == null)
        {
            FindCameras();
        }

        if (followCam != null && directFollowCam != null)
        {
            return true;
        }

        Debug.LogWarning("[CameraManager] Camera targets were not found. Check CN_FollowCam / CN_DirectFollowCam names.");
        return false;
    }

    public string GetActiveCameraName()
    {
        return isFollowCamActive ? "CN_FollowCam" : "CN_DirectFollowCam";
    }

    public void PlayShake(float force)
    {
        PlayShake(force, impulseSource != null ? impulseSource.DefaultVelocity : Vector3.down);
    }

    public void PlayShake(float force, Vector3 direction)
    {
        if (impulseSource == null)
        {
            impulseSource = GetComponent<CinemachineImpulseSource>();
        }

        if (impulseSource == null)
        {
            impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
        }

        EnsureImpulseSourceConfigured();
        EnsureImpulseListeners(ResolveImpulseChannel());

        Vector3 impulsePosition = ResolveImpulsePosition();
        Vector3 velocity = direction.sqrMagnitude > Mathf.Epsilon
            ? direction.normalized * force
            : impulseSource.DefaultVelocity * force;
        impulseSource.GenerateImpulseAtPositionWithVelocity(
            impulsePosition,
            velocity);
        Debug.Log($"[CameraManager] PlayShake: {force}, direction={velocity.normalized}");
    }

    public void PlayShakePulses(float force, Vector3 direction, int pulseCount)
    {
        if (pulseCount <= 1)
        {
            PlayShake(force, direction);
            return;
        }

        if (impulseSource == null)
        {
            impulseSource = GetComponent<CinemachineImpulseSource>();
        }

        if (impulseSource == null)
        {
            impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
        }

        EnsureImpulseSourceConfigured();
        int impulseChannel = ResolveImpulseChannel();
        EnsureImpulseListeners(impulseChannel);

        Vector3 impulsePosition = ResolveImpulsePosition();
        Vector3 velocity = direction.sqrMagnitude > Mathf.Epsilon
            ? direction.normalized * force
            : impulseSource.DefaultVelocity * force;

        CinemachineImpulseDefinition pulseDefinition = CreatePulseImpulseDefinition(
            impulseSource.ImpulseDefinition,
            impulseChannel,
            pulseCount);
        pulseDefinition.CreateEvent(impulsePosition, velocity);
        Debug.Log($"[CameraManager] PlayShakePulses: {force}, direction={velocity.normalized}, count={pulseCount}");
    }

    private static CinemachineImpulseDefinition CreatePulseImpulseDefinition(
        CinemachineImpulseDefinition sourceDefinition,
        int impulseChannel,
        int pulseCount)
    {
        float impulseDuration = sourceDefinition != null
            ? Mathf.Max(0.01f, sourceDefinition.ImpulseDuration)
            : 0.2f;

        return new CinemachineImpulseDefinition
        {
            ImpulseChannel = impulseChannel != 0 ? impulseChannel : 1,
            ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Custom,
            CustomImpulseShape = CreateAlternatingPulseCurve(pulseCount),
            ImpulseDuration = impulseDuration,
            ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform,
            DissipationDistance = sourceDefinition != null ? sourceDefinition.DissipationDistance : 100f,
            DissipationRate = sourceDefinition != null ? sourceDefinition.DissipationRate : 0.25f,
            PropagationSpeed = sourceDefinition != null ? sourceDefinition.PropagationSpeed : 343f,
            ImpactRadius = sourceDefinition != null ? sourceDefinition.ImpactRadius : 100f,
            DirectionMode = sourceDefinition != null
                ? sourceDefinition.DirectionMode
                : CinemachineImpulseManager.ImpulseEvent.DirectionModes.Fixed,
            DissipationMode = sourceDefinition != null
                ? sourceDefinition.DissipationMode
                : CinemachineImpulseManager.ImpulseEvent.DissipationModes.ExponentialDecay
        };
    }

    private static AnimationCurve CreateAlternatingPulseCurve(int pulseCount)
    {
        pulseCount = Mathf.Max(1, pulseCount);
        Keyframe[] keys = new Keyframe[(pulseCount * 2) + 1];
        keys[0] = new Keyframe(0f, 0f);

        for (int i = 0; i < pulseCount; i++)
        {
            float sign = i % 2 == 0 ? 1f : -1f;
            keys[(i * 2) + 1] = new Keyframe((i + 0.5f) / pulseCount, sign);
            keys[(i * 2) + 2] = new Keyframe((i + 1f) / pulseCount, 0f);
        }

        return new AnimationCurve(keys);
    }

    private void EnsureImpulseSourceConfigured()
    {
        if (impulseSource == null)
        {
            return;
        }

        CinemachineImpulseDefinition definition = impulseSource.ImpulseDefinition;
        if (definition == null)
        {
            definition = new CinemachineImpulseDefinition();
            impulseSource.ImpulseDefinition = definition;
        }

        if (definition.ImpulseChannel == 0)
        {
            definition.ImpulseChannel = 1;
        }

        if (definition.ImpulseType == CinemachineImpulseDefinition.ImpulseTypes.Legacy &&
            definition.RawSignal == null)
        {
            definition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            definition.ImpulseDuration = 0.2f;
            definition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            definition.DissipationDistance = 100f;
            definition.DissipationRate = 0.25f;
            definition.PropagationSpeed = 343f;
        }
    }

    private int ResolveImpulseChannel()
    {
        int channel = impulseSource != null && impulseSource.ImpulseDefinition != null
            ? impulseSource.ImpulseDefinition.ImpulseChannel
            : 1;
        return channel != 0 ? channel : 1;
    }

    private static Vector3 ResolveImpulsePosition()
    {
        CinemachineCamera[] cameras = Object.FindObjectsByType<CinemachineCamera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        CinemachineCamera activeCamera = null;
        int activePriority = int.MinValue;
        foreach (CinemachineCamera camera in cameras)
        {
            if (camera == null || !camera.isActiveAndEnabled)
            {
                continue;
            }

            int priority = camera.Priority.Value;
            if (activeCamera == null || priority > activePriority)
            {
                activeCamera = camera;
                activePriority = priority;
            }
        }

        if (activeCamera != null)
        {
            return activeCamera.transform.position;
        }

        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform.position : Vector3.zero;
    }

    private static void EnsureImpulseListeners(int channelMask)
    {
        var cameras = Object.FindObjectsByType<CinemachineCamera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (CinemachineCamera camera in cameras)
        {
            if (camera == null)
            {
                continue;
            }

            CinemachineImpulseListener listener = camera.GetComponent<CinemachineImpulseListener>();
            if (listener == null)
            {
                listener = camera.gameObject.AddComponent<CinemachineImpulseListener>();
            }

            ConfigureImpulseListener(listener, channelMask);
        }
    }

    private static void ConfigureImpulseListener(CinemachineImpulseListener listener, int channelMask)
    {
        if (listener == null)
        {
            return;
        }

        listener.ChannelMask |= channelMask != 0 ? channelMask : 1;

        if (listener.Gain <= 0f)
        {
            listener.Gain = 1f;
        }

        listener.UseCameraSpace = true;
    }
}
