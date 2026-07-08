using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Room camera endpoint. Portals and gates call ActivateCamera at transitions;
/// this component also validates the active room against the player's position.
/// </summary>
public class RoomCameraTrigger : MonoBehaviour
{
    private const string DefaultPlayerTag = "Player";

    private static RoomCameraTrigger _activeTrigger;
    private static readonly List<RoomCameraTrigger> _registeredTriggers = new();
    private static int lastActiveRoomValidationFrame = -1;

    public static event Action<RoomCameraTrigger> ActiveRoomChanged;

    [Header("Camera Settings")]
    [SerializeField]
    private CinemachineCamera _roomCamera;

    [SerializeField]
    private bool _useDefaultCameraWhenEntered;

    [Header("Horizontal Follow Camera")]
    [SerializeField]
    private bool _useHorizontalFollowCameraWhenEntered;

    [SerializeField]
    private CinemachineCamera _horizontalFollowCamera;

    [SerializeField]
    private bool _useManualYForHorizontalFollow;

    [SerializeField]
    private float _horizontalFollowFixedY;

    [SerializeField]
    private bool _clampHorizontalFollowXToArea = true;

    [SerializeField, Min(0f)]
    private float _horizontalFollowSmoothTime = 0.15f;

    [SerializeField]
    private string _playerTag = "Player";

    [SerializeField]
    private int _activePriority = 20;

    [SerializeField]
    private int _inactivePriority = 0;

    [Header("Default Camera Preview")]
    [SerializeField]
    private bool _previewDefaultCameraByAreaBounds = true;

    [SerializeField, Range(0f, 1f)]
    private float _defaultCameraPreviewWeight = 0.65f;

    [SerializeField, Range(0f, 1f)]
    private float _defaultCameraPreviewZoomWeight = 0.55f;

    [SerializeField, Min(0f)]
    private float _maxDefaultCameraPreviewSize = 12f;

    [Header("Area Bounds")]
    [SerializeField]
    private Collider2D[] _areaColliders2D;

    [SerializeField]
    private Collider[] _areaColliders;

    private bool IsDefaultTrigger => _useDefaultCameraWhenEntered;
    private bool HasRoomCamera => _roomCamera != null;
    private bool IsHorizontalFollowTrigger => _useHorizontalFollowCameraWhenEntered;
    private bool HasHorizontalFollowCamera => _horizontalFollowCamera != null;
    private Transform horizontalFollowPlayer;
    private float horizontalFollowVelocityX;

    public static RoomCameraTrigger ActiveRoom => _activeTrigger;
    public bool UsesDefaultCameraWhenEntered => IsDefaultTrigger;
    public bool HasAssignedRoomCamera => HasRoomCamera;
    public bool UsesHorizontalFollowCameraWhenEntered => IsHorizontalFollowTrigger;
    public bool IsBossRoom => HasBossAreaController();
    public bool PreviewDefaultCameraByAreaBounds => _previewDefaultCameraByAreaBounds;
    public float DefaultCameraPreviewWeight => _defaultCameraPreviewWeight;
    public float DefaultCameraPreviewZoomWeight => _defaultCameraPreviewZoomWeight;
    public float MaxDefaultCameraPreviewSize => _maxDefaultCameraPreviewSize;

    public bool TryGetCameraPose(out Vector3 position, out float orthographicSize)
    {
        if (IsHorizontalFollowTrigger && _horizontalFollowCamera != null)
        {
            position = _horizontalFollowCamera.transform.position;
            orthographicSize = _horizontalFollowCamera.Lens.OrthographicSize;
            return true;
        }

        if (_roomCamera != null)
        {
            position = _roomCamera.transform.position;
            orthographicSize = _roomCamera.Lens.OrthographicSize;
            return true;
        }

        if (IsDefaultTrigger && TryGetDefaultFollowCameraPose(out position, out orthographicSize))
        {
            return true;
        }

        if (TryGetAreaBounds(out Bounds bounds))
        {
            float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
            position = bounds.center;
            position.z = Camera.main != null ? Camera.main.transform.position.z : transform.position.z;
            orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect);
            return true;
        }

        position = transform.position;
        orthographicSize = Camera.main != null && Camera.main.orthographic
            ? Camera.main.orthographicSize
            : 10f;
        return false;
    }

    public bool TryGetAreaBounds(out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = default;

        AddColliderBounds(_areaColliders2D, ref bounds, ref hasBounds);
        AddColliderBounds(_areaColliders, ref bounds, ref hasBounds);

        if (hasBounds)
        {
            return true;
        }

        AddColliderBounds(GetComponents<Collider2D>(), ref bounds, ref hasBounds);
        AddColliderBounds(GetComponents<Collider>(), ref bounds, ref hasBounds);
        return hasBounds;
    }

    public static bool TryActivateRoomAtPosition(Vector3 worldPosition, out RoomCameraTrigger activatedRoom)
    {
        activatedRoom = FindSmallestRoomContaining(worldPosition, out int matchCount);
        if (activatedRoom == null)
        {
            return false;
        }

        if (matchCount > 1)
        {
            Debug.LogWarning(
                $"[RoomCameraTrigger] Multiple rooms contain {worldPosition}. Selected {activatedRoom.name} by smallest bounds.",
                activatedRoom);
        }

        activatedRoom.ActivateCamera();
        return true;
    }

    public static bool TryGetRoomAtPosition(Vector3 worldPosition, out RoomCameraTrigger room)
    {
        room = FindSmallestRoomContaining(worldPosition, out _);
        return room != null;
    }

    private void Awake()
    {
        if (!_registeredTriggers.Contains(this))
        {
            _registeredTriggers.Add(this);
        }

        if (!IsDefaultTrigger && !IsHorizontalFollowTrigger && !HasRoomCamera)
        {
            Debug.LogWarning($"[{gameObject.name}] RoomCameraTrigger has no CinemachineCamera assigned.", this);
            return;
        }

        DeactivateOwnCamera();
    }

    private void LateUpdate()
    {
        ValidateActiveRoomAgainstPlayer();

        if (_activeTrigger != this || !IsHorizontalFollowTrigger)
        {
            return;
        }

        UpdateHorizontalFollowTarget();
    }

    private void OnDisable()
    {
        _registeredTriggers.Remove(this);

        if (_activeTrigger == this)
        {
            _activeTrigger = null;
            NotifyActiveRoomChanged();
        }

        DeactivateOwnCamera();
    }

    private void OnDestroy()
    {
        horizontalFollowPlayer = null;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryActivateHorizontalFollowFromPlayer(collision.transform);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        TryActivateHorizontalFollowFromPlayer(collision.transform);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryActivateHorizontalFollowFromPlayer(other.transform);
    }

    private void OnTriggerStay(Collider other)
    {
        TryActivateHorizontalFollowFromPlayer(other.transform);
    }

    private void TryActivateHorizontalFollowFromPlayer(Transform source)
    {
        if (!IsHorizontalFollowTrigger || !TryResolvePlayerTransform(source, out Transform player))
        {
            return;
        }

        horizontalFollowPlayer = player;
        if (_activeTrigger != this)
        {
            ActivateCamera();
        }
    }

    public void ActivateCamera()
    {
        if (IsHorizontalFollowTrigger)
        {
            ActivateHorizontalFollowCamera();
            return;
        }

        if (IsDefaultTrigger)
        {
            ReleaseToDefaultCamera(this);
            return;
        }

        if (!HasRoomCamera)
        {
            return;
        }

        DeactivateAllRoomCamerasExcept(this);

        if (_activeTrigger != this)
        {
            _activeTrigger = this;
            NotifyActiveRoomChanged();
        }

        _roomCamera.Priority.Value = _activePriority;
        _roomCamera.Priority.Enabled = true;
    }

    public void DeactivateCamera()
    {
        if (IsDefaultTrigger)
        {
            return;
        }

        if (_activeTrigger == this)
        {
            _activeTrigger = null;
            NotifyActiveRoomChanged();
        }

        DeactivateOwnCamera();
    }

    public bool ContainsPoint(Vector3 worldPosition)
    {
        bool hasConfiguredBounds =
            HasConfiguredBounds(_areaColliders2D) ||
            HasConfiguredBounds(_areaColliders);

        if (hasConfiguredBounds)
        {
            return ContainsPointInConfiguredBounds(worldPosition);
        }

        Vector2 point2D = new Vector2(worldPosition.x, worldPosition.y);
        Collider2D[] colliders2D = GetComponents<Collider2D>();
        for (int i = 0; i < colliders2D.Length; i++)
        {
            Collider2D roomCollider = colliders2D[i];
            if (roomCollider != null &&
                roomCollider.enabled &&
                roomCollider.OverlapPoint(point2D))
            {
                return true;
            }
        }

        Collider[] colliders = GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider roomCollider = colliders[i];
            if (roomCollider != null &&
                roomCollider.enabled &&
                roomCollider.bounds.Contains(worldPosition))
            {
                return true;
            }
        }

        return false;
    }

    private void DeactivateOwnCamera()
    {
        if (HasHorizontalFollowCamera)
        {
            _horizontalFollowCamera.Priority.Value = _inactivePriority;
            _horizontalFollowCamera.Priority.Enabled = true;
        }

        if (!HasRoomCamera)
        {
            return;
        }

        _roomCamera.Priority.Value = _inactivePriority;
        _roomCamera.Priority.Enabled = true;
    }

    private void ActivateHorizontalFollowCamera()
    {
        if (!HasHorizontalFollowCamera)
        {
            Debug.LogWarning(
                $"[{gameObject.name}] Use Horizontal Follow Camera is enabled, but no camera is assigned.",
                this);
            return;
        }

        DeactivateAllRoomCamerasExcept(this);

        if (_activeTrigger != this)
        {
            _activeTrigger = this;
            NotifyActiveRoomChanged();
        }

        horizontalFollowVelocityX = 0f;
        _horizontalFollowCamera.Follow = null;
        _horizontalFollowCamera.Target.TrackingTarget = null;
        UpdateHorizontalFollowCamera();
        _horizontalFollowCamera.Priority.Value = _activePriority;
        _horizontalFollowCamera.Priority.Enabled = true;
    }

    private void UpdateHorizontalFollowTarget()
    {
        UpdateHorizontalFollowCamera();
    }

    private void UpdateHorizontalFollowCamera()
    {
        if (_horizontalFollowCamera == null)
        {
            return;
        }

        if (horizontalFollowPlayer == null)
        {
            horizontalFollowPlayer = ResolvePlayerTransform();
        }

        Vector3 targetPosition = _horizontalFollowCamera.transform.position;
        if (horizontalFollowPlayer != null)
        {
            targetPosition.x = horizontalFollowPlayer.position.x;
        }

        targetPosition.y = ResolveHorizontalFollowY();

        if (_clampHorizontalFollowXToArea && TryGetAreaBounds(out Bounds bounds))
        {
            float halfWidth = ResolveCameraHalfWidth(_horizontalFollowCamera);
            float minX = bounds.min.x + halfWidth;
            float maxX = bounds.max.x - halfWidth;
            targetPosition.x = minX <= maxX
                ? Mathf.Clamp(targetPosition.x, minX, maxX)
                : bounds.center.x;
        }

        if (_horizontalFollowSmoothTime <= 0f)
        {
            _horizontalFollowCamera.transform.position = targetPosition;
            return;
        }

        Vector3 currentPosition = _horizontalFollowCamera.transform.position;
        currentPosition.x = Mathf.SmoothDamp(
            currentPosition.x,
            targetPosition.x,
            ref horizontalFollowVelocityX,
            _horizontalFollowSmoothTime);
        currentPosition.y = targetPosition.y;
        _horizontalFollowCamera.transform.position = currentPosition;
    }

    private float ResolveHorizontalFollowY()
    {
        if (_useManualYForHorizontalFollow)
        {
            return _horizontalFollowFixedY;
        }

        if (TryGetAreaBounds(out Bounds bounds))
        {
            return bounds.center.y;
        }

        return _horizontalFollowCamera != null
            ? _horizontalFollowCamera.transform.position.y
            : transform.position.y;
    }

    private static float ResolveCameraHalfWidth(CinemachineCamera camera)
    {
        if (camera == null)
        {
            return 0f;
        }

        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        return camera.Lens.OrthographicSize * aspect;
    }

    private Transform ResolvePlayerTransform()
    {
        if (string.IsNullOrWhiteSpace(_playerTag))
        {
            return null;
        }

        try
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag(_playerTag);
            return playerObject != null ? playerObject.transform : null;
        }
        catch (UnityException)
        {
            return null;
        }
    }

    private bool TryResolvePlayerTransform(Transform source, out Transform resolvedPlayer)
    {
        resolvedPlayer = null;
        if (source == null || string.IsNullOrWhiteSpace(_playerTag))
        {
            return false;
        }

        Transform current = source;
        while (current != null)
        {
            if (current.CompareTag(_playerTag))
            {
                resolvedPlayer = current;
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static void ReleaseToDefaultCamera(RoomCameraTrigger defaultTrigger)
    {
        if (defaultTrigger == null)
        {
            return;
        }

        DeactivateAllRoomCamerasExcept(defaultTrigger);
        ActivateDefaultFollowCamera(defaultTrigger);

        if (_activeTrigger == defaultTrigger)
        {
            return;
        }

        _activeTrigger = defaultTrigger;
        NotifyActiveRoomChanged();
    }

    private static void ReleaseToGlobalDefaultFollowCamera()
    {
        DeactivateAllRoomCamerasExcept(null);
        ActivateDefaultFollowCamera(null);

        if (_activeTrigger == null)
        {
            return;
        }

        _activeTrigger = null;
        NotifyActiveRoomChanged();
    }

    private static void ActivateDefaultFollowCamera(RoomCameraTrigger defaultTrigger)
    {
        if (defaultTrigger != null && defaultTrigger._roomCamera != null)
        {
            defaultTrigger._roomCamera.Priority.Value = defaultTrigger._activePriority;
            defaultTrigger._roomCamera.Priority.Enabled = true;
            return;
        }

        if (CameraManager.Instance != null &&
            CameraManager.Instance.SwitchToFollowCamera())
        {
            return;
        }

        CinemachineCamera followCamera = null;
        CinemachineCamera directFollowCamera = null;
        CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < cameras.Length; i++)
        {
            CinemachineCamera camera = cameras[i];
            if (camera == null)
            {
                continue;
            }

            if (camera.gameObject.name == "CN_FollowCam")
            {
                followCamera = camera;
            }
            else if (camera.gameObject.name == "CN_DirectFollowCam")
            {
                directFollowCamera = camera;
            }
        }

        if (followCamera == null)
        {
            string contextName = defaultTrigger != null ? defaultTrigger.name : nameof(RoomCameraTrigger);
            Debug.LogWarning(
                $"[{contextName}] Default follow camera was requested, but CN_FollowCam was not found.",
                defaultTrigger);
            return;
        }

        followCamera.Priority.Value = 10;
        followCamera.Priority.Enabled = true;

        if (directFollowCamera != null)
        {
            directFollowCamera.Priority.Value = 0;
            directFollowCamera.Priority.Enabled = true;
        }
    }

    private static void ValidateActiveRoomAgainstPlayer()
    {
        if (lastActiveRoomValidationFrame == Time.frameCount)
        {
            return;
        }

        lastActiveRoomValidationFrame = Time.frameCount;

        Transform player = ResolvePlayerTransformForValidation();
        if (player == null)
        {
            return;
        }

        Vector3 playerPosition = player.position;
        RoomCameraTrigger activeTrigger = _activeTrigger;
        if (activeTrigger != null &&
            activeTrigger.isActiveAndEnabled &&
            activeTrigger.ContainsPoint(playerPosition))
        {
            return;
        }

        if (TryGetRoomAtPosition(playerPosition, out RoomCameraTrigger containingRoom))
        {
            if (containingRoom != activeTrigger)
            {
                containingRoom.ActivateCamera();
            }

            return;
        }

        ReleaseToGlobalDefaultFollowCamera();
    }

    private static Transform ResolvePlayerTransformForValidation()
    {
        string playerTag = _activeTrigger != null && !string.IsNullOrWhiteSpace(_activeTrigger._playerTag)
            ? _activeTrigger._playerTag
            : DefaultPlayerTag;

        try
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
            return playerObject != null ? playerObject.transform : null;
        }
        catch (UnityException)
        {
            return null;
        }
    }

    private static void DeactivateAllRoomCamerasExcept(RoomCameraTrigger exception)
    {
        for (int i = _registeredTriggers.Count - 1; i >= 0; i--)
        {
            RoomCameraTrigger trigger = _registeredTriggers[i];
            if (trigger == null)
            {
                _registeredTriggers.RemoveAt(i);
                continue;
            }

            if (trigger == exception)
            {
                continue;
            }

            if (trigger.IsDefaultTrigger && !trigger.HasRoomCamera)
            {
                continue;
            }

            trigger.DeactivateOwnCamera();
        }
    }

    private static void NotifyActiveRoomChanged()
    {
        ActiveRoomChanged?.Invoke(_activeTrigger);
    }

    private static RoomCameraTrigger FindSmallestRoomContaining(Vector3 worldPosition, out int matchCount)
    {
        matchCount = 0;
        RoomCameraTrigger bestRoom = null;
        float bestArea = float.PositiveInfinity;

        for (int i = _registeredTriggers.Count - 1; i >= 0; i--)
        {
            RoomCameraTrigger trigger = _registeredTriggers[i];
            if (trigger == null)
            {
                _registeredTriggers.RemoveAt(i);
                continue;
            }

            if (!trigger.isActiveAndEnabled || !trigger.ContainsPoint(worldPosition))
            {
                continue;
            }

            matchCount++;
            float area = trigger.GetSmallestBoundsArea();
            if (area < bestArea)
            {
                bestArea = area;
                bestRoom = trigger;
            }
        }

        return bestRoom;
    }

    private bool ContainsPointInConfiguredBounds(Vector3 worldPosition)
    {
        Vector2 point2D = new Vector2(worldPosition.x, worldPosition.y);
        if (_areaColliders2D != null)
        {
            for (int i = 0; i < _areaColliders2D.Length; i++)
            {
                Collider2D areaCollider = _areaColliders2D[i];
                if (areaCollider != null &&
                    areaCollider.enabled &&
                    areaCollider.OverlapPoint(point2D))
                {
                    return true;
                }
            }
        }

        if (_areaColliders != null)
        {
            for (int i = 0; i < _areaColliders.Length; i++)
            {
                Collider areaCollider = _areaColliders[i];
                if (areaCollider != null &&
                    areaCollider.enabled &&
                    areaCollider.bounds.Contains(worldPosition))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private float GetSmallestBoundsArea()
    {
        float bestArea = float.PositiveInfinity;

        if (_areaColliders2D != null)
        {
            for (int i = 0; i < _areaColliders2D.Length; i++)
            {
                Collider2D areaCollider = _areaColliders2D[i];
                if (areaCollider == null || !areaCollider.enabled)
                {
                    continue;
                }

                Bounds bounds = areaCollider.bounds;
                bestArea = Mathf.Min(bestArea, bounds.size.x * bounds.size.y);
            }
        }

        if (_areaColliders != null)
        {
            for (int i = 0; i < _areaColliders.Length; i++)
            {
                Collider areaCollider = _areaColliders[i];
                if (areaCollider == null || !areaCollider.enabled)
                {
                    continue;
                }

                Bounds bounds = areaCollider.bounds;
                bestArea = Mathf.Min(bestArea, bounds.size.x * bounds.size.y);
            }
        }

        if (!float.IsPositiveInfinity(bestArea))
        {
            return bestArea;
        }

        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null && collider2D.enabled)
        {
            Bounds bounds = collider2D.bounds;
            return bounds.size.x * bounds.size.y;
        }

        Collider collider = GetComponent<Collider>();
        if (collider != null && collider.enabled)
        {
            Bounds bounds = collider.bounds;
            return bounds.size.x * bounds.size.y;
        }

        return float.MaxValue;
    }

    private static bool HasConfiguredBounds(Collider2D[] colliders)
    {
        if (colliders == null)
        {
            return false;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasConfiguredBounds(Collider[] colliders)
    {
        if (colliders == null)
        {
            return false;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetDefaultFollowCameraPose(out Vector3 position, out float orthographicSize)
    {
        if (CameraManager.Instance != null &&
            CameraManager.Instance.TryGetFollowCameraPose(out position, out orthographicSize))
        {
            return true;
        }

        CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < cameras.Length; i++)
        {
            CinemachineCamera camera = cameras[i];
            if (camera == null || camera.gameObject.name != "CN_FollowCam")
            {
                continue;
            }

            position = camera.transform.position;
            orthographicSize = camera.Lens.OrthographicSize;
            return true;
        }

        position = Vector3.zero;
        orthographicSize = 0f;
        return false;
    }

    private static void AddColliderBounds(Collider2D[] colliders, ref Bounds bounds, ref bool hasBounds)
    {
        if (colliders == null)
        {
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            AddBounds(collider.bounds, ref bounds, ref hasBounds);
        }
    }

    private static void AddColliderBounds(Collider[] colliders, ref Bounds bounds, ref bool hasBounds)
    {
        if (colliders == null)
        {
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            AddBounds(collider.bounds, ref bounds, ref hasBounds);
        }
    }

    private static void AddBounds(Bounds candidate, ref Bounds bounds, ref bool hasBounds)
    {
        if (!hasBounds)
        {
            bounds = candidate;
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(candidate);
    }

    private bool HasBossAreaController()
    {
        if (GetComponent<BossAreaController>() != null ||
            GetComponentInChildren<BossAreaController>(true) != null ||
            GetComponentInParent<BossAreaController>() != null)
        {
            return true;
        }

        if (_roomCamera != null)
        {
            Transform cameraTransform = _roomCamera.transform;
            if (cameraTransform.GetComponent<BossAreaController>() != null ||
                cameraTransform.GetComponentInChildren<BossAreaController>(true) != null ||
                cameraTransform.GetComponentInParent<BossAreaController>() != null)
            {
                return true;
            }

            if (cameraTransform.parent != null &&
                cameraTransform.parent.GetComponentInChildren<BossAreaController>(true) != null)
            {
                return true;
            }
        }

        Transform parent = transform.parent;
        if (parent == null)
        {
            return false;
        }

        bool looksLikeAreaChild =
            gameObject.name.StartsWith("Col_", StringComparison.OrdinalIgnoreCase) ||
            gameObject.name.StartsWith("CN_", StringComparison.OrdinalIgnoreCase);
        return looksLikeAreaChild &&
               parent.GetComponentInChildren<BossAreaController>(true) != null;
    }
}
