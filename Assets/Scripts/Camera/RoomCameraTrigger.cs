using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Room camera endpoint. This component does not switch cameras by itself;
/// portals and gates call ActivateCamera when the player reaches a real transition.
/// </summary>
public class RoomCameraTrigger : MonoBehaviour
{
    private static RoomCameraTrigger _activeTrigger;
    private static readonly List<RoomCameraTrigger> _registeredTriggers = new();
    private const float MainCameraRefreshInterval = 0.5f;
    private static Camera _cachedMainCamera;
    private static float _nextMainCameraRefreshTime;
    private static CinemachineCamera _cachedFollowCamera;
    private static CinemachineCamera _cachedDirectFollowCamera;

    public static event Action<RoomCameraTrigger> ActiveRoomChanged;

    [Header("Camera Settings")]
    [SerializeField]
    private CinemachineCamera _roomCamera;

    [SerializeField]
    private bool _useDefaultCameraWhenEntered;

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

    private readonly List<Collider2D> _runtimeColliders2D = new();
    private readonly List<Collider> _runtimeColliders = new();

    private bool IsDefaultTrigger => _useDefaultCameraWhenEntered;
    private bool HasRoomCamera => _roomCamera != null;

    public static RoomCameraTrigger ActiveRoom => _activeTrigger;
    public bool UsesDefaultCameraWhenEntered => IsDefaultTrigger;
    public bool HasAssignedRoomCamera => HasRoomCamera;
    public bool IsBossRoom => HasBossAreaController();
    public bool PreviewDefaultCameraByAreaBounds => _previewDefaultCameraByAreaBounds;
    public float DefaultCameraPreviewWeight => _defaultCameraPreviewWeight;
    public float DefaultCameraPreviewZoomWeight => _defaultCameraPreviewZoomWeight;
    public float MaxDefaultCameraPreviewSize => _maxDefaultCameraPreviewSize;
    public static IReadOnlyList<RoomCameraTrigger> RegisteredTriggers => _registeredTriggers;

    public bool TryGetCameraPose(out Vector3 position, out float orthographicSize)
    {
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
            float aspect = ResolveMainCameraAspect();
            position = bounds.center;
            position.z = TryGetMainCamera(out Camera mainCamera) ? mainCamera.transform.position.z : transform.position.z;
            orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect);
            return true;
        }

        position = transform.position;
        orthographicSize = TryGetMainCamera(out Camera fallbackCamera) && fallbackCamera.orthographic
            ? fallbackCamera.orthographicSize
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

        EnsureRuntimeColliders();
        AddColliderBounds(_runtimeColliders2D, ref bounds, ref hasBounds);
        AddColliderBounds(_runtimeColliders, ref bounds, ref hasBounds);
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
        RefreshRuntimeColliders();

        if (!_registeredTriggers.Contains(this))
        {
            _registeredTriggers.Add(this);
        }

        if (!IsDefaultTrigger && !HasRoomCamera)
        {
            Debug.LogWarning($"[{gameObject.name}] RoomCameraTrigger has no CinemachineCamera assigned.", this);
            return;
        }

        DeactivateOwnCamera();
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

    public void ActivateCamera()
    {
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
        EnsureRuntimeColliders();
        for (int i = 0; i < _runtimeColliders2D.Count; i++)
        {
            Collider2D roomCollider = _runtimeColliders2D[i];
            if (roomCollider != null &&
                roomCollider.enabled &&
                roomCollider.OverlapPoint(point2D))
            {
                return true;
            }
        }

        for (int i = 0; i < _runtimeColliders.Count; i++)
        {
            Collider roomCollider = _runtimeColliders[i];
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
        if (!HasRoomCamera)
        {
            return;
        }

        _roomCamera.Priority.Value = _inactivePriority;
        _roomCamera.Priority.Enabled = true;
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

    private static void ActivateDefaultFollowCamera(RoomCameraTrigger defaultTrigger)
    {
        if (defaultTrigger._roomCamera != null)
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

        if (!TryGetDefaultFollowCameras(out CinemachineCamera followCamera, out CinemachineCamera directFollowCamera))
        {
            Debug.LogWarning(
                $"[{defaultTrigger.name}] Use Default Camera is enabled, but CN_FollowCam was not found.",
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

        EnsureRuntimeColliders();
        for (int i = 0; i < _runtimeColliders2D.Count; i++)
        {
            Collider2D collider2D = _runtimeColliders2D[i];
            if (collider2D == null || !collider2D.enabled)
            {
                continue;
            }

            Bounds bounds = collider2D.bounds;
            bestArea = Mathf.Min(bestArea, bounds.size.x * bounds.size.y);
        }

        for (int i = 0; i < _runtimeColliders.Count; i++)
        {
            Collider collider = _runtimeColliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            bestArea = Mathf.Min(bestArea, bounds.size.x * bounds.size.y);
        }

        return !float.IsPositiveInfinity(bestArea) ? bestArea : float.MaxValue;
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

        if (TryGetDefaultFollowCameras(out CinemachineCamera followCamera, out _))
        {
            position = followCamera.transform.position;
            orthographicSize = followCamera.Lens.OrthographicSize;
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

    private static void AddColliderBounds(List<Collider2D> colliders, ref Bounds bounds, ref bool hasBounds)
    {
        if (colliders == null)
        {
            return;
        }

        for (int i = 0; i < colliders.Count; i++)
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

    private static void AddColliderBounds(List<Collider> colliders, ref Bounds bounds, ref bool hasBounds)
    {
        if (colliders == null)
        {
            return;
        }

        for (int i = 0; i < colliders.Count; i++)
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

    private void RefreshRuntimeColliders()
    {
        _runtimeColliders2D.Clear();
        _runtimeColliders.Clear();
        GetComponents(_runtimeColliders2D);
        GetComponents(_runtimeColliders);
    }

    private void EnsureRuntimeColliders()
    {
        if (_runtimeColliders2D.Count == 0 && _runtimeColliders.Count == 0)
        {
            RefreshRuntimeColliders();
        }
    }

    private static float ResolveMainCameraAspect()
    {
        if (TryGetMainCamera(out Camera mainCamera))
        {
            return mainCamera.aspect;
        }

        return Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
    }

    private static bool TryGetMainCamera(out Camera mainCamera)
    {
        if (_cachedMainCamera != null && Time.unscaledTime < _nextMainCameraRefreshTime)
        {
            mainCamera = _cachedMainCamera;
            return true;
        }

        _cachedMainCamera = MainCameraCache.Get();
        _nextMainCameraRefreshTime = Time.unscaledTime + MainCameraRefreshInterval;
        mainCamera = _cachedMainCamera;
        return mainCamera != null;
    }

    private static bool TryGetDefaultFollowCameras(
        out CinemachineCamera followCamera,
        out CinemachineCamera directFollowCamera)
    {
        if (_cachedFollowCamera != null)
        {
            followCamera = _cachedFollowCamera;
            directFollowCamera = _cachedDirectFollowCamera;
            return true;
        }

        followCamera = null;
        directFollowCamera = null;
        IReadOnlyList<CinemachineCamera> cameras = CinemachineCameraCache.Get(includeInactive: true);

        for (int i = 0; i < cameras.Count; i++)
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

        _cachedFollowCamera = followCamera;
        _cachedDirectFollowCamera = directFollowCamera;
        return followCamera != null;
    }
}
