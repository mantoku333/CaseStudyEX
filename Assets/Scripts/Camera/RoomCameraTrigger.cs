using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Room camera endpoint. Portals and gates call ActivateCamera at transitions;
/// this component also validates the active room against the player's position.
/// </summary>
// The room is resolved in LateUpdate, and so is CinemachineBrain's blend. CinemachineBrain
// declares no execution order, so without one here the two ran in an undefined order: when the
// brain went first, it rendered a frame with the previous room's camera even though the player
// had already changed rooms - a one frame hitch on every transition. A negative order puts the
// room decision reliably before the brain reads priorities.
[DefaultExecutionOrder(-50)]
public class RoomCameraTrigger : MonoBehaviour
{
    private const string DefaultPlayerTag = "Player";

    private static RoomCameraTrigger _activeTrigger;
    private static readonly List<RoomCameraTrigger> _registeredTriggers = new();
    private static int lastActiveRoomValidationFrame = -1;
    private static float lastActiveRoomChangeTime = float.NegativeInfinity;
    private static RoomCameraTrigger lastLeftRoom;
    private static MonoBehaviour portalCameraOwner;
    private static RoomCameraTrigger portalCameraDestination;
    private static BossAreaController encounterRoomOwner;
    private static RoomCameraTrigger encounterRoom;
    private static bool encounterPublishedRoom;

    public static event Action<RoomCameraTrigger> ActiveRoomChanged;

    // Static state here outlives a scene load. It is reset explicitly because the project may
    // disable domain reload on entering play mode, which would otherwise carry a destroyed
    // trigger and a stale portal hold into the next run.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _activeTrigger = null;
        _registeredTriggers.Clear();
        lastActiveRoomValidationFrame = -1;
        lastActiveRoomChangeTime = float.NegativeInfinity;
        lastLeftRoom = null;
        portalCameraOwner = null;
        portalCameraDestination = null;
        encounterRoomOwner = null;
        encounterRoom = null;
        encounterPublishedRoom = false;
        ActiveRoomChanged = null;
    }

    [Header("Camera Settings")]
    [SerializeField]
    private CinemachineCamera _roomCamera;

    [SerializeField]
    private bool _useDefaultCameraWhenEntered;

    [Header("Default Follow Camera Overrides")]
    [SerializeField]
    private bool _overrideDefaultFollowCameraOrthographicSize;

    [SerializeField, Min(0f)]
    private float _defaultFollowCameraOrthographicSize;

    [SerializeField]
    private bool _overrideDefaultFollowCameraTargetOffsetY;

    [SerializeField]
    private float _defaultFollowCameraTargetOffsetY;

    [SerializeField]
    private bool _overrideDefaultFollowCameraScreenPositionY;

    [SerializeField, Range(-1f, 1f)]
    private float _defaultFollowCameraScreenPositionY;

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

    [SerializeField, Tooltip(
        "Optional. Clamp and vertical centring use this shape instead of this room's own area.\n\n" +
        "Give a run of adjoining rooms the same horizontal follow camera and the same shape here " +
        "and the camera stops changing between them: it simply keeps following the player across " +
        "the whole run. That is the only way to remove the jolt at a seam, because any transition " +
        "between two cameras 38 units apart either whips or drifts.")]
    private Collider2D _horizontalFollowClampBounds;

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

    [Header("Room Switching Feel")]
    [SerializeField, Min(0f), Tooltip(
        "How far past this room's edge the player must go before the room is released. " +
        "Stops the view flipping while walking along a shared boundary.")]
    private float _roomExitMargin = 0.25f;

    [SerializeField, Min(0f), Tooltip(
        "How far inside a room the player must be before it takes the camera. " +
        "Scaled down automatically for rooms too thin to reach it.")]
    private float _roomEnterDepth = 0.25f;

    [SerializeField, Min(0f), Tooltip(
        "How far back inside the room just left the player must go before the camera returns " +
        "to it. This is the only place a deep threshold belongs: a static room camera shows just " +
        "about 2 units beyond its room, so a deep threshold on the way IN leaves the player off " +
        "screen until the switch. Going in is prompt; only turning straight back is resisted.")]
    private float _roomReturnDepth = 0.25f;

    [SerializeField, Min(0f), Tooltip(
        "How long a return to the room just left is resisted, so repeated crossings cannot " +
        "strobe the view. Moving on to a different room is never delayed by this.")]
    private float _roomMinHoldSeconds = 0f;

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

    /// <summary>
    /// Set by <see cref="RoomCameraDirector"/> while it owns the camera.
    ///
    /// This component is not only a camera trigger: <c>RoomFogRevealManager</c>,
    /// <c>RoomEnemyActivityManager</c> and <c>StoryEventController</c> all use it as the room
    /// identity and listen to <see cref="ActiveRoomChanged"/>. So it has to stay on every room
    /// even after the director takes over the camera, or fog, enemy sleep/wake and story-event
    /// room restore all break. This flag silences only the camera-driving half of the component.
    /// </summary>
    public static bool ExternalCameraAuthority { get; set; }

    /// <summary>
    /// Lets the director publish the active room without touching camera priorities, so the
    /// systems that key off <see cref="ActiveRoomChanged"/> keep working unchanged.
    /// </summary>
    public static void SetActiveRoomExternally(RoomCameraTrigger room)
    {
        if (_activeTrigger == room)
        {
            return;
        }

        _activeTrigger = room;
        NotifyActiveRoomChanged();
    }
    public bool UsesDefaultCameraWhenEntered => IsDefaultTrigger;
    public bool HasAssignedRoomCamera => HasRoomCamera;
    public bool UsesHorizontalFollowCameraWhenEntered => IsHorizontalFollowTrigger;
    public bool IsBossRoom => HasBossAreaController();
    public bool HasDefaultFollowCameraLensOverride =>
        _overrideDefaultFollowCameraOrthographicSize && _defaultFollowCameraOrthographicSize > 0f;
    public float DefaultFollowCameraOrthographicSize => _defaultFollowCameraOrthographicSize;
    public bool HasDefaultFollowCameraTargetOffsetYOverride => _overrideDefaultFollowCameraTargetOffsetY;
    public float DefaultFollowCameraTargetOffsetY => _defaultFollowCameraTargetOffsetY;
    public bool HasDefaultFollowCameraScreenPositionYOverride => _overrideDefaultFollowCameraScreenPositionY;
    public float DefaultFollowCameraScreenPositionY => _defaultFollowCameraScreenPositionY;
    public bool PreviewDefaultCameraByAreaBounds => _previewDefaultCameraByAreaBounds;
    public float DefaultCameraPreviewWeight => _defaultCameraPreviewWeight;
    public float DefaultCameraPreviewZoomWeight => _defaultCameraPreviewZoomWeight;
    public float MaxDefaultCameraPreviewSize => _maxDefaultCameraPreviewSize;

    public float ResolveDefaultFollowCameraOrthographicSize(float fallbackOrthographicSize)
    {
        return HasDefaultFollowCameraLensOverride
            ? _defaultFollowCameraOrthographicSize
            : fallbackOrthographicSize;
    }

    public float ResolveDefaultFollowCameraTargetOffsetY(float fallbackTargetOffsetY)
    {
        return HasDefaultFollowCameraTargetOffsetYOverride
            ? _defaultFollowCameraTargetOffsetY
            : fallbackTargetOffsetY;
    }

    public float ResolveDefaultFollowCameraScreenPositionY(float fallbackScreenPositionY)
    {
        return HasDefaultFollowCameraScreenPositionYOverride
            ? _defaultFollowCameraScreenPositionY
            : fallbackScreenPositionY;
    }

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
            orthographicSize = ResolveDefaultFollowCameraOrthographicSize(orthographicSize);
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

    /// <summary>
    /// Only an explicit teleport may force a room. The walk-through portals used to as well,
    /// which made two things decide the active room with different thresholds: the portal
    /// committed as soon as the player was inside the next room, and again by "portal side" on
    /// leaving its collider, while the per-frame validation applied its own depth rule. The
    /// recording of this scene shows the result as cuts 0.17s apart (133.33s -> 133.63s,
    /// 134.40s -> 134.57s). Those portals sit on the room boundary anyway - a median of 0.32
    /// units from it horizontally - so the validation switches at the same place.
    /// </summary>
    private static bool MayDriveCamera(MonoBehaviour portalOwner)
    {
        return portalOwner is RoomCameraSwitchPortal;
    }

    public static bool HoldCameraForPortal(
        MonoBehaviour portalOwner,
        RoomCameraTrigger destinationRoom)
    {
        if (!MayDriveCamera(portalOwner))
        {
            return false;
        }

        if (EncounterCameraOwnership.IsActive || portalOwner == null ||
            destinationRoom == null ||
            !destinationRoom.isActiveAndEnabled ||
            !destinationRoom.CanControlCamera())
        {
            return false;
        }

        portalCameraOwner = portalOwner;
        portalCameraDestination = destinationRoom;
        destinationRoom.ActivateCamera();
        return _activeTrigger == destinationRoom;
    }

    public static void ReleaseCameraFromPortal(MonoBehaviour portalOwner)
    {
        if (portalOwner == null || portalCameraOwner != portalOwner)
        {
            return;
        }

        ClearPortalCameraHold();
        lastActiveRoomValidationFrame = -1;
    }

    public static bool CommitCameraFromPortal(
        MonoBehaviour portalOwner,
        RoomCameraTrigger finalRoom)
    {
        if (!MayDriveCamera(portalOwner))
        {
            return false;
        }

        if (EncounterCameraOwnership.IsActive || portalOwner == null ||
            (portalCameraOwner != null && portalCameraOwner != portalOwner))
        {
            return false;
        }

        if (portalCameraOwner == portalOwner)
        {
            ClearPortalCameraHold();
        }

        lastActiveRoomValidationFrame = -1;
        if (finalRoom == null || !finalRoom.isActiveAndEnabled)
        {
            return false;
        }

        finalRoom.ActivateCamera();
        return _activeTrigger == finalRoom;
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

        if (portalCameraDestination == this)
        {
            ClearPortalCameraHold();
        }

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
        TryActivateHorizontalFollowFromPlayer(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        TryActivateHorizontalFollowFromPlayer(collision);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryActivateHorizontalFollowFromPlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryActivateHorizontalFollowFromPlayer(other);
    }

    private void TryActivateHorizontalFollowFromPlayer(Collider2D source)
    {
        if (!IsHorizontalFollowTrigger ||
            !PlayerCameraColliderUtility.TryResolvePlayerTransform(source, _playerTag, out Transform player))
        {
            return;
        }

        ActivateHorizontalFollowFromPlayer(player);
    }

    private void TryActivateHorizontalFollowFromPlayer(Collider source)
    {
        if (!IsHorizontalFollowTrigger ||
            !PlayerCameraColliderUtility.TryResolvePlayerTransform(source, _playerTag, out Transform player))
        {
            return;
        }

        ActivateHorizontalFollowFromPlayer(player);
    }

    private void ActivateHorizontalFollowFromPlayer(Transform player)
    {
        horizontalFollowPlayer = player;
        if (_activeTrigger != this)
        {
            ActivateCamera();
        }
    }

    public void ActivateCamera()
    {
        // Under the director, priorities are not this component's to set. Callers such as
        // WarpArea2D and StoryEventController still invoke this after moving the player; the
        // director resolves the room from the player's position on its next LateUpdate, so the
        // request is satisfied without two things writing priorities.
        if (ExternalCameraAuthority)
        {
            return;
        }

        if (EncounterCameraOwnership.IsActive)
        {
            return;
        }

        if (IsActivationBlockedByPortalHold(this))
        {
            return;
        }

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
        FlushCameraPriorities();
    }

    /// <summary>
    /// Makes CinemachineBrain act on the priorities just written, in this frame.
    ///
    /// Cinemachine only notices a changed priority in each camera's own Update(), so a change
    /// made here in LateUpdate is not seen by the brain until the next frame. The fog, on the
    /// other hand, switches to the new room immediately. For that one frame the screen showed
    /// the old camera with the new room's fog over it - a fully black frame on every cut.
    /// Prioritize() forces the re-sort now, so the camera and the fog change together.
    /// </summary>
    private static void FlushCameraPriorities()
    {
        for (int i = _registeredTriggers.Count - 1; i >= 0; i--)
        {
            RoomCameraTrigger trigger = _registeredTriggers[i];
            if (trigger == null)
            {
                continue;
            }

            if (trigger._roomCamera != null)
            {
                trigger._roomCamera.Prioritize();
            }

            if (trigger._horizontalFollowCamera != null)
            {
                trigger._horizontalFollowCamera.Prioritize();
            }
        }

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.PrioritizeFollowCameras();
        }
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
                IsPointInsideCollider(roomCollider, worldPosition))
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

        // Moving into another room of the same camera run must not restart the follow.
        // The camera object does not change, so Cinemachine sees no transition at all; if the
        // smoothing velocity were zeroed here the motion would stall for a moment at every
        // internal seam, which is the jolt this run setup exists to remove.
        RoomCameraTrigger previous = _activeTrigger;
        bool continuesSameCamera =
            previous != null &&
            previous != this &&
            previous._horizontalFollowCamera == _horizontalFollowCamera;

        DeactivateAllRoomCamerasExcept(this);

        if (_activeTrigger != this)
        {
            _activeTrigger = this;
            NotifyActiveRoomChanged();
        }

        if (continuesSameCamera)
        {
            horizontalFollowVelocityX = previous.horizontalFollowVelocityX;
            if (previous.horizontalFollowPlayer != null)
            {
                horizontalFollowPlayer = previous.horizontalFollowPlayer;
            }
        }
        else
        {
            horizontalFollowVelocityX = 0f;
            _horizontalFollowCamera.Follow = null;
            _horizontalFollowCamera.Target.TrackingTarget = null;
        }

        UpdateHorizontalFollowCamera();
        _horizontalFollowCamera.Priority.Value = _activePriority;
        _horizontalFollowCamera.Priority.Enabled = true;
        FlushCameraPriorities();
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
            targetPosition.x = PlayerCameraColliderUtility.TryGetCameraPoint(horizontalFollowPlayer, out Vector3 cameraPoint)
                ? cameraPoint.x
                : horizontalFollowPlayer.position.x;
        }

        targetPosition.y = ResolveHorizontalFollowY();

        if (_clampHorizontalFollowXToArea && TryGetHorizontalFollowBounds(out Bounds bounds))
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

    /// <summary>
    /// Bounds the horizontal follow clamps against: the run shape when one is assigned,
    /// otherwise this room's own area.
    /// </summary>
    private bool TryGetHorizontalFollowBounds(out Bounds bounds)
    {
        if (_horizontalFollowClampBounds != null)
        {
            // A run shape is a shape, not a physics body, so it is kept disabled - and a
            // disabled Collider2D reports empty bounds. Read the box dimensions directly.
            if (_horizontalFollowClampBounds is BoxCollider2D box)
            {
                Transform host = box.transform;
                Vector3 scale = host.lossyScale;
                Vector2 size = new Vector2(
                    Mathf.Abs(box.size.x * scale.x),
                    Mathf.Abs(box.size.y * scale.y));
                Vector3 center = host.TransformPoint(box.offset);
                bounds = new Bounds(center, new Vector3(size.x, size.y, 0f));
                return true;
            }

            if (_horizontalFollowClampBounds.enabled)
            {
                bounds = _horizontalFollowClampBounds.bounds;
                return true;
            }
        }

        return TryGetAreaBounds(out bounds);
    }

    private float ResolveHorizontalFollowY()
    {
        if (_useManualYForHorizontalFollow)
        {
            return _horizontalFollowFixedY;
        }

        if (TryGetHorizontalFollowBounds(out Bounds bounds))
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

    private static void ReleaseToDefaultCamera(RoomCameraTrigger defaultTrigger)
    {
        if (defaultTrigger == null)
        {
            return;
        }

        DeactivateAllRoomCamerasExcept(defaultTrigger);
        ActivateDefaultFollowCamera(defaultTrigger);
        FlushCameraPriorities();

        if (_activeTrigger == defaultTrigger)
        {
            return;
        }

        _activeTrigger = defaultTrigger;
        NotifyActiveRoomChanged();
        RefreshFollowCameraState();
    }

    private static void ReleaseToGlobalDefaultFollowCamera()
    {
        DeactivateAllRoomCamerasExcept(null);
        ActivateDefaultFollowCamera(null);
        FlushCameraPriorities();

        if (_activeTrigger == null)
        {
            return;
        }

        _activeTrigger = null;
        NotifyActiveRoomChanged();
        RefreshFollowCameraState();
    }

    /// <summary>
    /// Called after the new room has been announced, so the follow camera's limits are already
    /// the new room's when its pose is recomputed. See CameraManager.RefreshFollowCameraStateNow.
    /// </summary>
    private static void RefreshFollowCameraState()
    {
        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.RefreshFollowCameraStateNow();
        }
    }

    private static void ActivateDefaultFollowCamera(RoomCameraTrigger defaultTrigger)
    {
        if (defaultTrigger != null && defaultTrigger._roomCamera != null)
        {
            defaultTrigger._roomCamera.Priority.Value = defaultTrigger._activePriority;
            defaultTrigger._roomCamera.Priority.Enabled = true;
            return;
        }

        if (CameraManager.Instance != null)
        {
            // Order matters. Raising the priority first makes the brain cut to the follow
            // camera's stale pose and old lens, and only then does the room's zoom and offset
            // get written and the body damp into place - the lurch you see entering a
            // free-camera area. Set the framing first, then hand over the priority.
            //
            // PrepareFollowCameraEntry also gives the camera the view it is replacing, so the
            // player keeps its place on screen across the cut instead of jumping to the follow
            // camera's composition. It is a no-op when the follow camera is already live, which
            // is what keeps movement between two adjoining free-camera rooms completely still.
            defaultTrigger?.ApplyDefaultFollowCameraOverrides();
            CameraManager.Instance.PrepareFollowCameraEntry();

            if (CameraManager.Instance.SwitchToFollowCamera())
            {
                return;
            }
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
        defaultTrigger?.ApplyDefaultFollowCameraOverrides(followCamera);

        if (directFollowCamera != null)
        {
            directFollowCamera.Priority.Value = 0;
            directFollowCamera.Priority.Enabled = true;
        }
    }

    private void ApplyDefaultFollowCameraOverrides(CinemachineCamera followCamera = null)
    {
        if (CameraManager.Instance != null &&
            CameraManager.Instance.TryApplyFollowCameraAreaSettings(
                HasDefaultFollowCameraLensOverride,
                _defaultFollowCameraOrthographicSize,
                HasDefaultFollowCameraTargetOffsetYOverride,
                _defaultFollowCameraTargetOffsetY,
                HasDefaultFollowCameraScreenPositionYOverride,
                _defaultFollowCameraScreenPositionY))
        {
            return;
        }

        if (!HasDefaultFollowCameraLensOverride)
        {
            return;
        }

        if (followCamera == null)
        {
            TryFindDefaultFollowCamera(out followCamera);
        }

        if (followCamera == null)
        {
            return;
        }

        LensSettings lens = followCamera.Lens;
        lens.OrthographicSize = _defaultFollowCameraOrthographicSize;
        followCamera.Lens = lens;
    }

    private static void ValidateActiveRoomAgainstPlayer()
    {
        // RoomCameraDirector owns the camera; this component is then only the room identity
        // that fog, enemy activity and story events read.
        if (ExternalCameraAuthority)
        {
            return;
        }

        if (EncounterCameraOwnership.IsActive)
        {
            PublishEncounterRoom();
            return;
        }

        if (encounterPublishedRoom)
        {
            // The encounter only named the room; it never set camera priorities, and it can end
            // without restoring the room camera (player death, load). Re-assert the room once so
            // the camera that takes over belongs to the room the fog is showing.
            encounterPublishedRoom = false;
            RoomCameraTrigger room = _activeTrigger;
            if (room != null && room.isActiveAndEnabled)
            {
                room.ActivateCamera();
            }
        }

        if (lastActiveRoomValidationFrame == Time.frameCount)
        {
            return;
        }

        lastActiveRoomValidationFrame = Time.frameCount;
        if (EnforcePortalCameraHold())
        {
            return;
        }

        if (!TryResolvePlayerCameraPointForValidation(out Vector3 playerPosition))
        {
            return;
        }

        RoomCameraTrigger activeTrigger = _activeTrigger;
        bool hasActive = activeTrigger != null && activeTrigger.isActiveAndEnabled;

        // Hold the current room until the player is clearly out of it.
        //
        // Without the margin, the room changed the instant the camera point crossed a collider
        // edge. Neighbouring rooms here share their edges, so the flip band was effectively zero
        // width: walking back and forth over a seam swapped the view every few frames, and each
        // swap moves it about 38 units. That is the "switches too much" and "switches too early"
        // problem. This only affects the per-frame validation; an explicit
        // TryActivateRoomAtPosition (warp, story event, boss intro) still snaps immediately.
        if (hasActive)
        {
            float exitMargin = Mathf.Max(0f, activeTrigger._roomExitMargin);
            if (activeTrigger.ContainsPoint(playerPosition) ||
                activeTrigger.GetDepthInside(playerPosition) > -exitMargin)
            {
                return;
            }
        }

        RoomCameraTrigger containingRoom = hasActive
            ? FindDeepestRoomContaining(
                playerPosition,
                activeTrigger._roomEnterDepth,
                lastLeftRoom,
                activeTrigger._roomReturnDepth)
            : FindDeepestRoomContaining(playerPosition, 0f, null, 0f);

        if (containingRoom != null)
        {
            if (containingRoom != activeTrigger)
            {
                // Resist only going straight back to the room we just left. A blanket time lock
                // would also delay legitimate fast transits: the player falls under gravity
                // scale 2, so a vertical room crossing reaches about 28 units/second against
                // 7 while running, and holding for 0.3s there would leave the camera roughly
                // 8 units behind - a third of the way into the new room. Oscillation is always
                // a return to the previous room, so that is the only case worth throttling.
                float minHold = Mathf.Max(0f, activeTrigger != null ? activeTrigger._roomMinHoldSeconds : 0f);
                if (hasActive &&
                    containingRoom == lastLeftRoom &&
                    Time.unscaledTime - lastActiveRoomChangeTime < minHold)
                {
                    return;
                }

                lastLeftRoom = activeTrigger;
                containingRoom.ActivateCamera();
                lastActiveRoomChangeTime = Time.unscaledTime;
            }

            return;
        }

        // The player is inside no room at all. Room colliders in this scene do not tile
        // perfectly, and dropping to the follow camera in those seams produced a visible snap
        // out and straight back, so the last room is kept instead.
        if (hasActive)
        {
            return;
        }

        ReleaseToGlobalDefaultFollowCamera();
    }

    /// <summary>
    /// While a boss encounter owns the camera, the room on screen is the boss room, so that is
    /// the active room.
    ///
    /// The encounter takes the camera the moment the player's body touches the boss area, and
    /// room validation is frozen from then on. The room itself only becomes active once the
    /// player is a quarter unit inside, so the freeze always won: the picture cut to the boss
    /// room while the active room stayed the corridor, and the fog kept the whole boss room
    /// black for the entire fight. Only the room identity is published here - the encounter's
    /// brain override already decides the picture, so priorities are left alone.
    /// </summary>
    private static void PublishEncounterRoom()
    {
        BossAreaController encounter = EncounterCameraOwnership.Owner;
        if (encounter == null)
        {
            return;
        }

        if (encounterRoomOwner != encounter)
        {
            encounterRoomOwner = encounter;
            encounterRoom = ResolveEncounterRoom(encounter);
        }

        RoomCameraTrigger room = encounterRoom;
        if (room == null || !room.isActiveAndEnabled || _activeTrigger == room)
        {
            return;
        }

        lastLeftRoom = _activeTrigger;
        _activeTrigger = room;
        lastActiveRoomChangeTime = Time.unscaledTime;
        encounterPublishedRoom = true;
        NotifyActiveRoomChanged();
    }

    /// <summary>
    /// The room a boss encounter takes place in.
    ///
    /// Only some arenas are the room object itself (Col_221, Col_324). The BossArea prefab is a
    /// separate object dropped inside a room - the first boss sits inside Col_109 that way - so
    /// it has no RoomCameraTrigger of its own and the room has to be found from where the arena
    /// is. Looking on the same object only left that fight fogged from start to finish.
    /// </summary>
    private static RoomCameraTrigger ResolveEncounterRoom(BossAreaController encounter)
    {
        RoomCameraTrigger room = encounter.GetComponent<RoomCameraTrigger>();
        if (room != null)
        {
            return room;
        }

        Collider2D area = encounter.GetComponent<Collider2D>();
        Vector3 centre = area != null ? area.bounds.center : encounter.transform.position;
        return FindDeepestRoomContaining(centre, 0f, null, 0f);
    }

    /// <summary>
    /// Distance from the point to the nearest edge of this room. Positive inside, negative out.
    /// </summary>
    public float GetDepthInside(Vector3 worldPosition)
    {
        if (!TryGetAreaBounds(out Bounds bounds))
        {
            return float.NegativeInfinity;
        }

        float dx = Mathf.Min(worldPosition.x - bounds.min.x, bounds.max.x - worldPosition.x);
        float dy = Mathf.Min(worldPosition.y - bounds.min.y, bounds.max.y - worldPosition.y);
        return Mathf.Min(dx, dy);
    }

    /// <summary>
    /// Deepest-inside room containing the point, requiring at least <paramref name="requiredDepth"/>
    /// of penetration before a room qualifies.
    ///
    /// Replaces smallest-area selection for the per-frame validation. Area was a poor rule here:
    /// Col_212 and Col_213 differ by one square unit, so which room won was effectively arbitrary
    /// and unrelated to the direction the player came from.
    /// </summary>
    private static RoomCameraTrigger FindDeepestRoomContaining(
        Vector3 worldPosition,
        float requiredDepth,
        RoomCameraTrigger returnRoom,
        float returnDepth)
    {
        RoomCameraTrigger best = null;
        float bestDepth = float.NegativeInfinity;

        for (int i = _registeredTriggers.Count - 1; i >= 0; i--)
        {
            RoomCameraTrigger trigger = _registeredTriggers[i];
            if (trigger == null)
            {
                _registeredTriggers.RemoveAt(i);
                continue;
            }

            if (!trigger.isActiveAndEnabled ||
                !trigger.CanControlCamera() ||
                !trigger.ContainsPoint(worldPosition))
            {
                continue;
            }

            float depth = trigger.GetDepthInside(worldPosition);

            // A room thinner than twice the requirement can never reach it, so scale the
            // requirement to what that room can actually offer.
            float required = trigger == returnRoom
                ? Mathf.Max(requiredDepth, returnDepth)
                : requiredDepth;
            if (required > 0f && trigger.TryGetAreaBounds(out Bounds bounds))
            {
                required = Mathf.Min(required, Mathf.Min(bounds.extents.x, bounds.extents.y) * 0.5f);
            }

            if (depth < required)
            {
                continue;
            }

            if (depth > bestDepth)
            {
                bestDepth = depth;
                best = trigger;
            }
        }

        return best;
    }

    private bool CanControlCamera()
    {
        if (IsHorizontalFollowTrigger)
        {
            return HasHorizontalFollowCamera;
        }

        return IsDefaultTrigger || HasRoomCamera;
    }

    private static bool IsActivationBlockedByPortalHold(RoomCameraTrigger requestedRoom)
    {
        if (!TryGetPortalCameraDestination(out RoomCameraTrigger destinationRoom))
        {
            return false;
        }

        return requestedRoom != destinationRoom;
    }

    private static bool EnforcePortalCameraHold()
    {
        if (!TryGetPortalCameraDestination(out RoomCameraTrigger destinationRoom))
        {
            return false;
        }

        if (_activeTrigger != destinationRoom)
        {
            destinationRoom.ActivateCamera();
        }

        return true;
    }

    private static bool TryGetPortalCameraDestination(out RoomCameraTrigger destinationRoom)
    {
        bool ownerIsUsable =
            portalCameraOwner != null &&
            portalCameraOwner.isActiveAndEnabled;
        bool destinationIsUsable =
            portalCameraDestination != null &&
            portalCameraDestination.isActiveAndEnabled;

        if (!ownerIsUsable || !destinationIsUsable)
        {
            ClearPortalCameraHold();
            destinationRoom = null;
            return false;
        }

        destinationRoom = portalCameraDestination;
        return true;
    }

    internal static void ClearPortalCameraHold()
    {
        portalCameraOwner = null;
        portalCameraDestination = null;
    }

    private static bool TryResolvePlayerCameraPointForValidation(out Vector3 playerPosition)
    {
        string playerTag = _activeTrigger != null && !string.IsNullOrWhiteSpace(_activeTrigger._playerTag)
            ? _activeTrigger._playerTag
            : DefaultPlayerTag;

        return PlayerCameraColliderUtility.TryGetCameraPointFromPlayerTag(playerTag, out playerPosition);
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
                    IsPointInsideCollider(areaCollider, worldPosition))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPointInsideCollider(Collider collider, Vector3 worldPosition)
    {
        Vector3 closestPoint = collider.ClosestPoint(worldPosition);
        return (closestPoint - worldPosition).sqrMagnitude <= 0.0001f;
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

        if (TryFindDefaultFollowCamera(out CinemachineCamera camera))
        {
            position = camera.transform.position;
            orthographicSize = camera.Lens.OrthographicSize;
            return true;
        }

        position = Vector3.zero;
        orthographicSize = 0f;
        return false;
    }

    private static bool TryFindDefaultFollowCamera(out CinemachineCamera followCamera)
    {
        CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < cameras.Length; i++)
        {
            CinemachineCamera camera = cameras[i];
            if (camera != null && camera.gameObject.name == "CN_FollowCam")
            {
                followCamera = camera;
                return true;
            }
        }

        followCamera = null;
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
