using Unity.Cinemachine;
using UnityEngine;

public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [SerializeField] private CinemachineImpulseSource impulseSource;

    private bool isFollowCamActive = true;
    private CinemachineCamera followCam;
    private CinemachineCamera directFollowCam;

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

        FindCameras();
        EnsureImpulseListeners();
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
    }

    private void EnsurePlayerFollowBiasComponents()
    {
        EnsurePlayerFollowBiasComponent(followCam);
        EnsurePlayerFollowBiasComponent(directFollowCam);
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
        if (followCam == null || directFollowCam == null)
        {
            FindCameras();
        }

        if (followCam == null || directFollowCam == null)
        {
            Debug.LogWarning("[CameraManager] Camera targets were not found. Check CN_FollowCam / CN_DirectFollowCam names.");
            return;
        }

        isFollowCamActive = !isFollowCamActive;

        if (isFollowCamActive)
        {
            followCam.Priority.Value = 10;
            directFollowCam.Priority.Value = 0;
            followCam.Priority.Enabled = true;
            directFollowCam.Priority.Enabled = true;
            Debug.Log("[CameraManager] Camera switched to CN_FollowCam");
            return;
        }

        followCam.Priority.Value = 0;
        directFollowCam.Priority.Value = 10;
        followCam.Priority.Enabled = true;
        directFollowCam.Priority.Enabled = true;
        Debug.Log("[CameraManager] Camera switched to CN_DirectFollowCam");
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
        EnsureImpulseListeners();

        if (impulseSource == null)
        {
            impulseSource = GetComponent<CinemachineImpulseSource>();
        }

        if (impulseSource == null)
        {
            impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
        }

        Vector3 impulsePosition = ResolveImpulsePosition();
        Vector3 velocity = direction.sqrMagnitude > Mathf.Epsilon
            ? direction.normalized * force
            : impulseSource.DefaultVelocity * force;
        impulseSource.GenerateImpulseAtPositionWithVelocity(
            impulsePosition,
            velocity);
        Debug.Log($"[CameraManager] PlayShake: {force}, direction={velocity.normalized}");
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

    private static void EnsureImpulseListeners()
    {
        var cameras = Object.FindObjectsByType<CinemachineCamera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (CinemachineCamera camera in cameras)
        {
            if (camera == null || camera.GetComponent<CinemachineImpulseListener>() != null)
            {
                continue;
            }

            camera.gameObject.AddComponent<CinemachineImpulseListener>();
        }
    }
}
