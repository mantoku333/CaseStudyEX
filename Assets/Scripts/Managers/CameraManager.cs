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
        if (camera == null || camera.GetComponent<FollowCameraFacingBias>() != null)
        {
            return;
        }

        camera.gameObject.AddComponent<FollowCameraFacingBias>();
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
