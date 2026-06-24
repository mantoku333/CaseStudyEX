using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Camera follow target that eases toward the midpoint of two gameplay targets.
/// Optionally adjusts orthographic size so both targets remain visible.
/// </summary>
public sealed class DualTargetCameraTarget : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform primaryTarget;
    [SerializeField] private Transform secondaryTarget;
    [SerializeField, Range(0f, 1f)] private float secondaryWeight = 0.5f;
    [SerializeField] private Vector3 worldOffset;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float smoothTime = 0.35f;
    [SerializeField, Min(0f)] private float maxSpeed = 80f;
    [SerializeField] private bool keepCurrentZ = true;
    [SerializeField] private bool startFromCurrentMainCamera = true;
    [SerializeField] private bool preferZoomOverCenterMovement = true;
    [SerializeField, Range(0.1f, 1f)] private float innerFrame = 0.82f;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera controlledCamera;
    [SerializeField] private bool assignSelfAsTrackingTarget = true;

    [Header("Bounds")]
    [SerializeField] private bool confineCameraToBounds = true;
    [SerializeField] private Collider2D cameraBounds2D;
    [SerializeField] private bool limitZoomToBounds = true;
    [SerializeField] private Vector2 boundsInset;

    [Header("Auto Zoom")]
    [SerializeField] private bool adjustOrthographicSize = true;
    [SerializeField] private bool useControlledCameraSizeAsMinimum = true;
    [SerializeField, Min(0.01f)] private float minOrthographicSize = 8f;
    [SerializeField, Min(0.01f)] private float maxOrthographicSize = 16f;
    [SerializeField, Min(0f)] private float horizontalPadding = 4f;
    [SerializeField, Min(0f)] private float verticalPadding = 3f;
    [SerializeField, Min(0f)] private float zoomSmoothTime = 0.3f;

    private Vector3 positionVelocity;
    private float zoomVelocity;
    private float runtimeMinOrthographicSize;
    private Camera cachedMainCamera;
    private float nextMainCameraRefreshTime;

    private const float MainCameraRefreshInterval = 0.5f;

    public void Configure(
        Transform primary,
        Transform secondary,
        CinemachineCamera camera = null,
        Collider2D bounds2D = null)
    {
        primaryTarget = primary;
        secondaryTarget = secondary;

        if (camera != null)
        {
            controlledCamera = camera;
        }

        if (bounds2D != null)
        {
            cameraBounds2D = bounds2D;
        }

        CacheRuntimeZoomLimits();
        MoveToCurrentMainCameraPosition();
        ApplyCameraTarget();
        positionVelocity = Vector3.zero;
    }

    private void OnEnable()
    {
        CacheRuntimeZoomLimits();
        ApplyCameraTarget();
        SnapToDesiredPosition();
    }

    private void OnValidate()
    {
        if (maxOrthographicSize < minOrthographicSize)
        {
            maxOrthographicSize = minOrthographicSize;
        }

        boundsInset.x = Mathf.Max(0f, boundsInset.x);
        boundsInset.y = Mathf.Max(0f, boundsInset.y);
    }

    private void LateUpdate()
    {
        if (!TryResolveTargetPositions(out Vector3 primaryPosition, out Vector3 secondaryPosition))
        {
            return;
        }

        float cameraSize = UpdateOrthographicSize(primaryPosition, secondaryPosition, transform.position);
        Vector3 desiredPosition = CalculateDesiredPosition(primaryPosition, secondaryPosition, cameraSize);

        if (smoothTime <= 0f)
        {
            transform.position = desiredPosition;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                smoothTime,
                maxSpeed);
        }
    }

    private void ApplyCameraTarget()
    {
        if (!assignSelfAsTrackingTarget || controlledCamera == null)
        {
            return;
        }

        controlledCamera.Follow = transform;
        EnsureFollowComponent();
    }

    private void EnsureFollowComponent()
    {
        CinemachineFollow follow = controlledCamera.GetComponent<CinemachineFollow>();
        if (follow == null)
        {
            follow = controlledCamera.gameObject.AddComponent<CinemachineFollow>();
        }

        Vector3 followOffset = follow.FollowOffset;
        followOffset.z = -10f;
        follow.FollowOffset = followOffset;

        var trackerSettings = follow.TrackerSettings;
        trackerSettings.PositionDamping = Vector3.zero;
        trackerSettings.RotationDamping = Vector3.zero;
        follow.TrackerSettings = trackerSettings;
    }

    private void SnapToDesiredPosition()
    {
        if (TryResolveTargetPositions(out Vector3 primaryPosition, out Vector3 secondaryPosition))
        {
            float cameraSize = ResolveCurrentOrthographicSize();
            Vector3 desiredPosition = CalculateDesiredPosition(primaryPosition, secondaryPosition, cameraSize);
            transform.position = desiredPosition;
            positionVelocity = Vector3.zero;
        }
    }

    private void MoveToCurrentMainCameraPosition()
    {
        if (!startFromCurrentMainCamera)
        {
            return;
        }

        if (!TryGetMainCamera(out Camera mainCamera))
        {
            return;
        }

        Vector3 startPosition = mainCamera.transform.position;
        if (keepCurrentZ)
        {
            startPosition.z = transform.position.z;
        }

        if (confineCameraToBounds)
        {
            startPosition = ClampCameraCenterToBounds(startPosition, ResolveCurrentOrthographicSize());
        }

        transform.position = startPosition;
        positionVelocity = Vector3.zero;
    }

    private bool TryResolveTargetPositions(out Vector3 primaryPosition, out Vector3 secondaryPosition)
    {
        Transform resolvedPrimary = primaryTarget != null ? primaryTarget : secondaryTarget;
        Transform resolvedSecondary = secondaryTarget != null ? secondaryTarget : primaryTarget;

        if (resolvedPrimary == null)
        {
            primaryPosition = transform.position;
            secondaryPosition = transform.position;
            return false;
        }

        primaryPosition = resolvedPrimary.position;
        secondaryPosition = resolvedSecondary != null
            ? resolvedSecondary.position
            : primaryPosition;
        return true;
    }

    private Vector3 CalculateDesiredPosition(Vector3 primaryPosition, Vector3 secondaryPosition, float cameraSize)
    {
        Vector3 desiredPosition = preferZoomOverCenterMovement
            ? CalculateMinimalCenterMovement(primaryPosition, secondaryPosition, cameraSize)
            : CalculateWeightedMidpoint(primaryPosition, secondaryPosition);

        desiredPosition += worldOffset;

        if (confineCameraToBounds)
        {
            desiredPosition = ClampCameraCenterToBounds(desiredPosition, cameraSize);
        }

        if (keepCurrentZ)
        {
            desiredPosition.z = transform.position.z;
        }

        return desiredPosition;
    }

    private Vector3 CalculateWeightedMidpoint(Vector3 primaryPosition, Vector3 secondaryPosition)
    {
        return Vector3.Lerp(primaryPosition, secondaryPosition, secondaryWeight);
    }

    private Vector3 CalculateMinimalCenterMovement(Vector3 primaryPosition, Vector3 secondaryPosition, float cameraSize)
    {
        Vector3 center = transform.position;
        float aspect = ResolveAspect();
        float frameHalfHeight = Mathf.Max(0.01f, cameraSize * innerFrame - verticalPadding);
        float frameHalfWidth = Mathf.Max(0.01f, cameraSize * aspect * innerFrame - horizontalPadding);

        center = KeepPointInsideFrame(center, primaryPosition, frameHalfWidth, frameHalfHeight);
        center = KeepPointInsideFrame(center, secondaryPosition, frameHalfWidth, frameHalfHeight);
        return center;
    }

    private static Vector3 KeepPointInsideFrame(
        Vector3 center,
        Vector3 point,
        float frameHalfWidth,
        float frameHalfHeight)
    {
        if (point.x > center.x + frameHalfWidth)
        {
            center.x = point.x - frameHalfWidth;
        }
        else if (point.x < center.x - frameHalfWidth)
        {
            center.x = point.x + frameHalfWidth;
        }

        if (point.y > center.y + frameHalfHeight)
        {
            center.y = point.y - frameHalfHeight;
        }
        else if (point.y < center.y - frameHalfHeight)
        {
            center.y = point.y + frameHalfHeight;
        }

        return center;
    }

    private float UpdateOrthographicSize(Vector3 primaryPosition, Vector3 secondaryPosition, Vector3 cameraCenter)
    {
        if (!adjustOrthographicSize || controlledCamera == null)
        {
            return ResolveCurrentOrthographicSize();
        }

        float aspect = ResolveAspect();
        float halfHeight = Mathf.Max(
            Mathf.Abs(primaryPosition.y - cameraCenter.y),
            Mathf.Abs(secondaryPosition.y - cameraCenter.y)) + verticalPadding;
        float halfWidth = Mathf.Max(
            Mathf.Abs(primaryPosition.x - cameraCenter.x),
            Mathf.Abs(secondaryPosition.x - cameraCenter.x)) + horizontalPadding;
        float desiredSize = Mathf.Max(halfHeight, halfWidth / Mathf.Max(0.01f, aspect));
        float minSize = ResolveMinOrthographicSize();
        float maxSize = ResolveMaxOrthographicSize(minSize, aspect);
        desiredSize = Mathf.Clamp(desiredSize, minSize, maxSize);

        LensSettings lens = controlledCamera.Lens;
        if (zoomSmoothTime <= 0f)
        {
            lens.OrthographicSize = desiredSize;
        }
        else
        {
            lens.OrthographicSize = Mathf.SmoothDamp(
                lens.OrthographicSize,
                desiredSize,
                ref zoomVelocity,
                zoomSmoothTime);
        }

        controlledCamera.Lens = lens;
        return lens.OrthographicSize;
    }

    private void CacheRuntimeZoomLimits()
    {
        runtimeMinOrthographicSize = minOrthographicSize;
        if (!useControlledCameraSizeAsMinimum || controlledCamera == null)
        {
            return;
        }

        runtimeMinOrthographicSize = Mathf.Max(
            runtimeMinOrthographicSize,
            controlledCamera.Lens.OrthographicSize);
    }

    private float ResolveMinOrthographicSize()
    {
        float minSize = Mathf.Max(0.01f, minOrthographicSize);
        if (useControlledCameraSizeAsMinimum)
        {
            minSize = Mathf.Max(minSize, runtimeMinOrthographicSize);
        }

        return minSize;
    }

    private float ResolveMaxOrthographicSize(float minSize, float aspect)
    {
        float maxSize = Mathf.Max(minSize, maxOrthographicSize);
        if (!limitZoomToBounds || !TryGetCameraBounds(out Bounds bounds))
        {
            return maxSize;
        }

        float boundsHalfHeight = Mathf.Max(0.01f, bounds.extents.y - boundsInset.y);
        float boundsHalfWidthAsSize = Mathf.Max(0.01f, bounds.extents.x - boundsInset.x) /
            Mathf.Max(0.01f, aspect);
        float boundsLimitedSize = Mathf.Min(boundsHalfHeight, boundsHalfWidthAsSize);
        return Mathf.Max(minSize, Mathf.Min(maxSize, boundsLimitedSize));
    }

    private Vector3 ClampCameraCenterToBounds(Vector3 center, float cameraSize)
    {
        if (!TryGetCameraBounds(out Bounds bounds))
        {
            return center;
        }

        float aspect = ResolveAspect();
        float halfHeight = cameraSize + boundsInset.y;
        float halfWidth = cameraSize * aspect + boundsInset.x;

        float minX = bounds.min.x + halfWidth;
        float maxX = bounds.max.x - halfWidth;
        center.x = minX <= maxX ? Mathf.Clamp(center.x, minX, maxX) : bounds.center.x;

        float minY = bounds.min.y + halfHeight;
        float maxY = bounds.max.y - halfHeight;
        center.y = minY <= maxY ? Mathf.Clamp(center.y, minY, maxY) : bounds.center.y;

        return center;
    }

    private bool TryGetCameraBounds(out Bounds bounds)
    {
        if (cameraBounds2D != null && cameraBounds2D.enabled)
        {
            bounds = cameraBounds2D.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    private float ResolveCurrentOrthographicSize()
    {
        return controlledCamera != null
            ? controlledCamera.Lens.OrthographicSize
            : ResolveMinOrthographicSize();
    }

    private float ResolveAspect()
    {
        if (TryGetMainCamera(out Camera mainCamera))
        {
            return mainCamera.aspect;
        }

        return Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
    }

    private bool TryGetMainCamera(out Camera mainCamera)
    {
        if (cachedMainCamera != null && Time.unscaledTime < nextMainCameraRefreshTime)
        {
            mainCamera = cachedMainCamera;
            return true;
        }

        cachedMainCamera = Camera.main;
        nextMainCameraRefreshTime = Time.unscaledTime + MainCameraRefreshInterval;
        mainCamera = cachedMainCamera;
        return mainCamera != null;
    }
}
