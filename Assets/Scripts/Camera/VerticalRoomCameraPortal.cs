using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Camera transition volume for vertical movement between two stacked room areas.
/// Place it on the boundary between the upper and lower room colliders.
/// </summary>
[DisallowMultipleComponent]
public sealed class VerticalRoomCameraPortal : MonoBehaviour
{
    private enum PortalDirection
    {
        Bidirectional,
        UpperToLowerOnly,
        LowerToUpperOnly
    }

    private struct CameraPose
    {
        public Vector3 Position;
        public float OrthographicSize;
    }

    private static CinemachineCamera runtimeTransitionCamera;
    private const float MainCameraRefreshInterval = 0.5f;

    [Header("Rooms")]
    [SerializeField] private RoomCameraTrigger upperRoom;
    [SerializeField] private RoomCameraTrigger lowerRoom;

    [Header("Transition Camera")]
    [SerializeField] private CinemachineCamera transitionCamera;
    [SerializeField] private int transitionPriority = 30;
    [SerializeField] private PortalDirection direction = PortalDirection.Bidirectional;
    [SerializeField, Range(0f, 1f)] private float startTargetRoomWeight = 0.35f;
    [SerializeField, Range(0f, 1f)] private float targetTargetRoomWeight = 1f;
    [SerializeField, Min(0f)] private float previewDuration = 0.75f;
    [SerializeField, Min(0f)] private float smoothTime = 0.12f;
    [SerializeField, Min(0f)] private float playerPadding = 2.5f;
    [SerializeField, Min(0f)] private float minimumOrthographicSize;
    [SerializeField] private bool commitWhenPlayerFullyInsideTargetRoom;
    [SerializeField] private bool commitByPortalExitSide = true;

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";

    private readonly HashSet<Collider2D> overlappingPlayerColliders = new();
    private readonly List<Collider2D> playerColliderBuffer = new();
    private Transform playerTransform;
    private RoomCameraTrigger pendingFromRoom;
    private RoomCameraTrigger pendingToRoom;
    private CameraPose fromPoseAtEntry;
    private CameraPose toPoseAtEntry;
    private Vector3 followOffsetAtEntry;
    private Vector3 transitionVelocity;
    private float transitionZoomVelocity;
    private float transitionElapsed;
    private Camera cachedMainCamera;
    private CinemachineCamera cachedFollowCamera;
    private float nextMainCameraRefreshTime;
    private bool transitionActive;
    private bool transitionCommitted;

    private void OnDisable()
    {
        overlappingPlayerColliders.Clear();
        playerTransform = null;
        pendingFromRoom = null;
        pendingToRoom = null;
        followOffsetAtEntry = Vector3.zero;
        transitionVelocity = Vector3.zero;
        transitionZoomVelocity = 0f;
        transitionElapsed = 0f;
        transitionCommitted = false;
        StopTransitionCamera();
    }

    private void Update()
    {
        UpdateTransitionCamera();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (TryResolvePlayerTransform(collision.transform, out Transform player))
        {
            HandlePlayerEntered(collision, player);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (TryResolvePlayerTransform(collision.transform, out Transform player))
        {
            overlappingPlayerColliders.Remove(collision);
            if (overlappingPlayerColliders.Count == 0)
            {
                CommitExit(ResolvePlayerCommitPoint(player, player.position));
                playerTransform = null;
                pendingFromRoom = null;
                pendingToRoom = null;
                followOffsetAtEntry = Vector3.zero;
                transitionCommitted = false;
                StopTransitionCamera();
            }
        }
    }

    private void HandlePlayerEntered(Collider2D playerCollider, Transform player)
    {
        bool wasOutsidePortal = overlappingPlayerColliders.Count == 0;
        overlappingPlayerColliders.Add(playerCollider);
        playerTransform = player;

        if (!wasOutsidePortal)
        {
            return;
        }

        Vector3 playerCommitPoint = ResolvePlayerCommitPoint(player, player.position);
        BeginTransition(playerCommitPoint);
        StartTransitionCamera(playerCommitPoint);
    }

    private void BeginTransition(Vector3 playerPosition)
    {
        pendingFromRoom = ResolveCurrentRoom(playerPosition);
        pendingToRoom = ResolveDestinationRoom(pendingFromRoom);
        fromPoseAtEntry = ResolveRoomPose(pendingFromRoom, playerPosition, true);
        toPoseAtEntry = ResolveRoomPose(pendingToRoom, playerPosition, false);
        followOffsetAtEntry = ResolveFollowOffsetAtEntry(playerPosition);
    }

    private void CommitExit(Vector3 playerPosition)
    {
        RoomCameraTrigger finalRoom = ResolveExitRoom(playerPosition);
        CommitToRoom(finalRoom);
    }

    private RoomCameraTrigger ResolveExitRoom(Vector3 playerPosition)
    {
        if (pendingToRoom == null)
        {
            return pendingFromRoom;
        }

        if (commitByPortalExitSide)
        {
            RoomCameraTrigger sideRoom = ResolveRoomOnPortalSide(playerPosition);
            if (sideRoom != null)
            {
                return sideRoom;
            }
        }

        RoomCameraTrigger resolvedLowerRoom = ResolveLowerRoom();
        RoomCameraTrigger resolvedUpperRoom = ResolveUpperRoom();

        if (resolvedLowerRoom != null && resolvedLowerRoom.ContainsPoint(playerPosition))
        {
            return resolvedLowerRoom;
        }

        if (resolvedUpperRoom != null && resolvedUpperRoom.ContainsPoint(playerPosition))
        {
            return resolvedUpperRoom;
        }

        return pendingFromRoom;
    }

    private void StartTransitionCamera(Vector3 playerPosition)
    {
        if (pendingFromRoom == null || pendingToRoom == null)
        {
            return;
        }

        CinemachineCamera camera = GetTransitionCamera();
        if (camera == null)
        {
            return;
        }

        CameraPose startPose = ResolveCurrentViewPose(playerPosition);
        camera.transform.position = startPose.Position;

        LensSettings lens = camera.Lens;
        lens.OrthographicSize = Mathf.Max(startPose.OrthographicSize, minimumOrthographicSize);
        camera.Lens = lens;

        camera.Priority.Enabled = true;
        camera.Priority.Value = transitionPriority;
        transitionVelocity = Vector3.zero;
        transitionZoomVelocity = 0f;
        transitionElapsed = 0f;
        transitionActive = true;
    }

    private void UpdateTransitionCamera()
    {
        if (transitionCommitted || !transitionActive || playerTransform == null)
        {
            return;
        }

        if (TryCommitFullyEnteredTargetRoom())
        {
            return;
        }

        CinemachineCamera camera = GetTransitionCamera();
        if (camera == null)
        {
            return;
        }

        transitionElapsed += Time.deltaTime;
        Vector3 playerPosition = ResolvePlayerCommitPoint(playerTransform, playerTransform.position);
        CameraPose targetPose = BuildTransitionPose(playerPosition);
        if (smoothTime <= 0f)
        {
            camera.transform.position = targetPose.Position;
            SetTransitionCameraSize(camera, targetPose.OrthographicSize);
            return;
        }

        camera.transform.position = Vector3.SmoothDamp(
            camera.transform.position,
            targetPose.Position,
            ref transitionVelocity,
            smoothTime);

        float nextSize = Mathf.SmoothDamp(
            camera.Lens.OrthographicSize,
            targetPose.OrthographicSize,
            ref transitionZoomVelocity,
            smoothTime);
        SetTransitionCameraSize(camera, nextSize);
    }

    private CameraPose BuildTransitionPose(Vector3 playerPosition)
    {
        CameraPose fromPose = ResolveTransitionPose(
            pendingFromRoom,
            fromPoseAtEntry,
            playerPosition,
            false,
            true);
        CameraPose toPose = ResolveTransitionPose(
            pendingToRoom,
            toPoseAtEntry,
            playerPosition,
            false,
            false);

        CameraPose pose;
        float targetWeight = ResolveTargetRoomWeight();
        pose.Position = Vector3.Lerp(fromPose.Position, toPose.Position, targetWeight);
        pose.OrthographicSize = Mathf.Max(
            minimumOrthographicSize,
            Mathf.Lerp(fromPose.OrthographicSize, toPose.OrthographicSize, targetWeight));

        KeepPlayerInsideView(ref pose, playerPosition);
        return pose;
    }

    private float ResolveTargetRoomWeight()
    {
        if (previewDuration <= 0f)
        {
            return targetTargetRoomWeight;
        }

        float t = Mathf.Clamp01(transitionElapsed / previewDuration);
        return Mathf.SmoothStep(startTargetRoomWeight, targetTargetRoomWeight, t);
    }

    private CameraPose ResolveRoomPose(
        RoomCameraTrigger room,
        Vector3 fallbackPosition,
        bool preferCurrentViewForDefaultRoom)
    {
        CameraPose pose;
        if (room != null && room.UsesDefaultCameraWhenEntered)
        {
            return ResolveDefaultRoomBasePose(room, fallbackPosition, preferCurrentViewForDefaultRoom);
        }

        if (room != null && room.TryGetCameraPose(out pose.Position, out pose.OrthographicSize))
        {
            return pose;
        }

        if (TryGetMainCamera(out Camera mainCamera))
        {
            pose.Position = mainCamera.transform.position;
            pose.OrthographicSize = mainCamera.orthographic ? mainCamera.orthographicSize : 10f;
            return pose;
        }

        pose.Position = fallbackPosition;
        pose.Position.z = -10f;
        pose.OrthographicSize = 10f;
        return pose;
    }

    private CameraPose ResolveDefaultRoomBasePose(
        RoomCameraTrigger room,
        Vector3 fallbackPosition,
        bool preferCurrentView)
    {
        if (preferCurrentView && TryGetCurrentCameraPose(out CameraPose pose))
        {
            return pose;
        }

        if (TryResolveDefaultFollowPose(fallbackPosition, out pose))
        {
            return pose;
        }

        if (TryGetCurrentCameraPose(out pose))
        {
            return pose;
        }

        if (room != null && room.TryGetAreaBounds(out Bounds bounds))
        {
            float aspect = ResolveAspect();
            pose.Position = bounds.center;
            pose.Position.z = TryGetMainCamera(out Camera mainCamera) ? mainCamera.transform.position.z : transform.position.z;
            pose.OrthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect);
            return pose;
        }

        pose.Position = fallbackPosition;
        pose.Position.z = -10f;
        pose.OrthographicSize = 10f;
        return pose;
    }

    private CameraPose ResolveCurrentViewPose(Vector3 fallbackPosition)
    {
        if (TryGetCurrentCameraPose(out CameraPose pose))
        {
            return pose;
        }

        return ResolveTransitionPose(pendingFromRoom, fromPoseAtEntry, fallbackPosition, false, true);
    }

    private bool TryGetCurrentCameraPose(out CameraPose pose)
    {
        if (TryGetMainCamera(out Camera mainCamera))
        {
            pose.Position = mainCamera.transform.position;
            pose.OrthographicSize = mainCamera.orthographic
                ? mainCamera.orthographicSize
                : Mathf.Max(fromPoseAtEntry.OrthographicSize, minimumOrthographicSize);
            return true;
        }

        CinemachineCamera camera = GetTransitionCamera(false);
        if (camera != null)
        {
            pose.Position = camera.transform.position;
            pose.OrthographicSize = camera.Lens.OrthographicSize;
            return true;
        }

        pose = default;
        return false;
    }

    private CameraPose ResolveTransitionPose(
        RoomCameraTrigger room,
        CameraPose poseAtEntry,
        Vector3 playerPosition,
        bool allowDefaultAreaPreview,
        bool preserveDefaultOffset)
    {
        if (room == null || !room.UsesDefaultCameraWhenEntered)
        {
            return poseAtEntry;
        }

        if (preserveDefaultOffset)
        {
            Vector3 followPosition = playerPosition + followOffsetAtEntry;
            poseAtEntry.Position.x = followPosition.x;
            poseAtEntry.Position.y = followPosition.y;
        }
        else if (TryResolveDefaultFollowPose(playerPosition, out CameraPose followPose))
        {
            poseAtEntry = followPose;
        }

        if (!allowDefaultAreaPreview ||
            !room.PreviewDefaultCameraByAreaBounds ||
            !room.TryGetAreaBounds(out Bounds bounds))
        {
            return poseAtEntry;
        }

        Vector3 previewCenter = Vector3.Lerp(
            poseAtEntry.Position,
            bounds.center,
            room.DefaultCameraPreviewWeight);
        previewCenter.z = poseAtEntry.Position.z;
        poseAtEntry.Position = previewCenter;

        float aspect = ResolveAspect();
        float boundsSize = Mathf.Max(bounds.extents.y, bounds.extents.x / aspect);
        float previewSize = Mathf.Lerp(
            poseAtEntry.OrthographicSize,
            boundsSize,
            room.DefaultCameraPreviewZoomWeight);

        if (room.MaxDefaultCameraPreviewSize > 0f)
        {
            previewSize = Mathf.Min(previewSize, room.MaxDefaultCameraPreviewSize);
        }

        poseAtEntry.OrthographicSize = Mathf.Max(
            minimumOrthographicSize,
            poseAtEntry.OrthographicSize,
            previewSize);
        return poseAtEntry;
    }

    private bool TryResolveDefaultFollowPose(Vector3 playerPosition, out CameraPose pose)
    {
        if (!TryFindFollowCamera(out CinemachineCamera followCamera))
        {
            pose = default;
            return false;
        }

        pose.Position = playerPosition;
        pose.Position.z = followCamera.transform.position.z;
        pose.OrthographicSize = Mathf.Max(minimumOrthographicSize, followCamera.Lens.OrthographicSize);

        if (TryApplyPositionComposerPose(followCamera, playerPosition, ref pose))
        {
            return true;
        }

        if (TryApplyDirectFollowPose(followCamera, playerPosition, ref pose))
        {
            return true;
        }

        if (TryGetMainCamera(out Camera mainCamera))
        {
            pose.Position.z = mainCamera.transform.position.z;
        }

        return true;
    }

    private bool TryFindFollowCamera(out CinemachineCamera followCamera)
    {
        if (cachedFollowCamera != null)
        {
            followCamera = cachedFollowCamera;
            return true;
        }

        CinemachineCamera[] cameras = FindObjectsByType<CinemachineCamera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < cameras.Length; i++)
        {
            CinemachineCamera camera = cameras[i];
            if (camera != null && camera.gameObject.name == "CN_FollowCam")
            {
                cachedFollowCamera = camera;
                followCamera = camera;
                return true;
            }
        }

        followCamera = null;
        return false;
    }

    private bool TryApplyPositionComposerPose(
        CinemachineCamera followCamera,
        Vector3 playerPosition,
        ref CameraPose pose)
    {
        CinemachinePositionComposer composer = followCamera.GetComponent<CinemachinePositionComposer>();
        if (composer == null)
        {
            return false;
        }

        float aspect = ResolveAspect();
        Vector3 trackedPosition = playerPosition + composer.TargetOffset;
        Vector2 screenPosition = composer.Composition.ScreenPosition;
        pose.Position.x = trackedPosition.x - screenPosition.x * pose.OrthographicSize * aspect * 2f;
        pose.Position.y = trackedPosition.y - screenPosition.y * pose.OrthographicSize * 2f;
        return true;
    }

    private static bool TryApplyDirectFollowPose(
        CinemachineCamera followCamera,
        Vector3 playerPosition,
        ref CameraPose pose)
    {
        CinemachineFollow directFollow = followCamera.GetComponent<CinemachineFollow>();
        if (directFollow == null)
        {
            return false;
        }

        Vector3 followOffset = directFollow.FollowOffset;
        pose.Position.x = playerPosition.x + followOffset.x;
        pose.Position.y = playerPosition.y + followOffset.y;
        return true;
    }

    private Vector3 ResolveFollowOffsetAtEntry(Vector3 playerPosition)
    {
        if (TryGetCurrentCameraPose(out CameraPose currentPose))
        {
            return currentPose.Position - playerPosition;
        }

        if (CameraManager.Instance != null &&
            CameraManager.Instance.TryGetFollowCameraPose(out Vector3 followPosition, out _))
        {
            return followPosition - playerPosition;
        }

        RoomCameraTrigger defaultRoom = pendingToRoom != null && pendingToRoom.UsesDefaultCameraWhenEntered
            ? pendingToRoom
            : pendingFromRoom;

        if (defaultRoom != null &&
            defaultRoom.TryGetCameraPose(out Vector3 defaultPosition, out _))
        {
            return defaultPosition - playerPosition;
        }

        return Vector3.zero;
    }

    private void AlignDefaultFollowCameraBeforeCommit(RoomCameraTrigger finalRoom)
    {
        if (finalRoom == null || !finalRoom.UsesDefaultCameraWhenEntered)
        {
            return;
        }

        CinemachineCamera camera = GetTransitionCamera(false);
        if (camera == null)
        {
            return;
        }

        CameraManager.Instance?.TrySetFollowCameraPose(
            camera.transform.position,
            camera.Lens.OrthographicSize);
    }

    private void KeepPlayerInsideView(ref CameraPose pose, Vector3 playerPosition)
    {
        float aspect = ResolveAspect();
        float halfHeight = Mathf.Max(0.1f, pose.OrthographicSize - playerPadding);
        float halfWidth = Mathf.Max(0.1f, pose.OrthographicSize * aspect - playerPadding);

        if (playerPosition.x < pose.Position.x - halfWidth)
        {
            pose.Position.x = playerPosition.x + halfWidth;
        }
        else if (playerPosition.x > pose.Position.x + halfWidth)
        {
            pose.Position.x = playerPosition.x - halfWidth;
        }

        if (playerPosition.y < pose.Position.y - halfHeight)
        {
            pose.Position.y = playerPosition.y + halfHeight;
        }
        else if (playerPosition.y > pose.Position.y + halfHeight)
        {
            pose.Position.y = playerPosition.y - halfHeight;
        }
    }

    private bool TryCommitFullyEnteredTargetRoom()
    {
        if (!commitWhenPlayerFullyInsideTargetRoom ||
            playerTransform == null ||
            pendingToRoom == null ||
            !IsPlayerFullyInsideRoom(pendingToRoom))
        {
            return false;
        }

        CommitToRoom(pendingToRoom);
        return true;
    }

    private void CommitToRoom(RoomCameraTrigger room)
    {
        if (room == null)
        {
            return;
        }

        AlignDefaultFollowCameraBeforeCommit(room);
        room.ActivateCamera();
        StopTransitionCamera();
        transitionCommitted = true;
    }

    private bool IsPlayerFullyInsideRoom(RoomCameraTrigger room)
    {
        FillPlayerColliderBuffer(playerTransform);
        bool checkedCollider = false;

        for (int i = 0; i < playerColliderBuffer.Count; i++)
        {
            Collider2D collider = playerColliderBuffer[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            checkedCollider = true;
            if (!IsBoundsInsideRoom(room, collider.bounds))
            {
                return false;
            }
        }

        if (checkedCollider)
        {
            return true;
        }

        return room.ContainsPoint(playerTransform.position);
    }

    private static bool IsBoundsInsideRoom(RoomCameraTrigger room, Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3 center = bounds.center;

        return
            room.ContainsPoint(center) &&
            room.ContainsPoint(new Vector3(min.x, min.y, center.z)) &&
            room.ContainsPoint(new Vector3(min.x, max.y, center.z)) &&
            room.ContainsPoint(new Vector3(max.x, min.y, center.z)) &&
            room.ContainsPoint(new Vector3(max.x, max.y, center.z));
    }

    private void StopTransitionCamera()
    {
        CinemachineCamera camera = GetTransitionCamera(false);
        if (camera != null)
        {
            camera.Priority.Enabled = true;
            camera.Priority.Value = 0;
        }

        transitionActive = false;
    }

    private CinemachineCamera GetTransitionCamera(bool createRuntimeCamera = true)
    {
        if (transitionCamera != null)
        {
            return transitionCamera;
        }

        if (!createRuntimeCamera)
        {
            return runtimeTransitionCamera;
        }

        if (runtimeTransitionCamera != null)
        {
            return runtimeTransitionCamera;
        }

        GameObject cameraObject = new GameObject("CN_VerticalPortalTransitionRuntime");
        runtimeTransitionCamera = cameraObject.AddComponent<CinemachineCamera>();
        runtimeTransitionCamera.Priority.Enabled = true;
        runtimeTransitionCamera.Priority.Value = 0;
        return runtimeTransitionCamera;
    }

    private static void SetTransitionCameraSize(CinemachineCamera camera, float orthographicSize)
    {
        LensSettings lens = camera.Lens;
        lens.OrthographicSize = orthographicSize;
        camera.Lens = lens;
    }

    private RoomCameraTrigger ResolveCurrentRoom(Vector3 playerPosition)
    {
        RoomCameraTrigger resolvedUpperRoom = ResolveUpperRoom();
        RoomCameraTrigger resolvedLowerRoom = ResolveLowerRoom();
        bool upperContainsPlayer = resolvedUpperRoom != null && resolvedUpperRoom.ContainsPoint(playerPosition);
        bool lowerContainsPlayer = resolvedLowerRoom != null && resolvedLowerRoom.ContainsPoint(playerPosition);
        RoomCameraTrigger activeRoom = RoomCameraTrigger.ActiveRoom;

        if ((activeRoom == resolvedUpperRoom && upperContainsPlayer) ||
            (activeRoom == resolvedLowerRoom && lowerContainsPlayer))
        {
            return activeRoom;
        }

        if (upperContainsPlayer && !lowerContainsPlayer)
        {
            return resolvedUpperRoom;
        }

        if (lowerContainsPlayer && !upperContainsPlayer)
        {
            return resolvedLowerRoom;
        }

        RoomCameraTrigger sideRoom = ResolveRoomOnPortalSide(playerPosition);
        if (activeRoom == sideRoom)
        {
            return activeRoom;
        }

        if (sideRoom != null)
        {
            return sideRoom;
        }

        return activeRoom == resolvedUpperRoom || activeRoom == resolvedLowerRoom
            ? activeRoom
            : null;
    }

    private RoomCameraTrigger ResolveDestinationRoom(RoomCameraTrigger fromRoom)
    {
        RoomCameraTrigger resolvedUpperRoom = ResolveUpperRoom();
        RoomCameraTrigger resolvedLowerRoom = ResolveLowerRoom();

        if (fromRoom == resolvedUpperRoom && direction != PortalDirection.LowerToUpperOnly)
        {
            return resolvedLowerRoom;
        }

        if (fromRoom == resolvedLowerRoom && direction != PortalDirection.UpperToLowerOnly)
        {
            return resolvedUpperRoom;
        }

        return null;
    }

    private RoomCameraTrigger ResolveRoomOnPortalSide(Vector3 playerPosition)
    {
        if (pendingFromRoom != null && pendingToRoom != null)
        {
            Vector3 fromCenter = ResolveRoomCenter(pendingFromRoom, transform.position);
            Vector3 toCenter = ResolveRoomCenter(pendingToRoom, transform.position);
            Vector3 fromToTarget = toCenter - fromCenter;
            fromToTarget.z = 0f;

            if (fromToTarget.sqrMagnitude > 0.001f)
            {
                Vector3 portalToPlayerTowardTarget = playerPosition - transform.position;
                portalToPlayerTowardTarget.z = 0f;
                return Vector3.Dot(portalToPlayerTowardTarget, fromToTarget) >= 0f
                    ? pendingToRoom
                    : pendingFromRoom;
            }
        }

        RoomCameraTrigger resolvedUpperRoom = ResolveUpperRoom();
        RoomCameraTrigger resolvedLowerRoom = ResolveLowerRoom();
        Vector3 upperCenter = ResolveRoomCenter(resolvedUpperRoom, transform.position + Vector3.up);
        Vector3 lowerCenter = ResolveRoomCenter(resolvedLowerRoom, transform.position + Vector3.down);
        Vector3 lowerToUpper = upperCenter - lowerCenter;
        lowerToUpper.z = 0f;

        if (lowerToUpper.sqrMagnitude <= 0.001f)
        {
            lowerToUpper = Vector3.up;
        }

        Vector3 portalToPlayer = playerPosition - transform.position;
        portalToPlayer.z = 0f;
        return Vector3.Dot(portalToPlayer, lowerToUpper) >= 0f ? resolvedUpperRoom : resolvedLowerRoom;
    }

    private RoomCameraTrigger ResolveUpperRoom()
    {
        if (upperRoom == null || lowerRoom == null)
        {
            return upperRoom;
        }

        Vector3 upperCenter = ResolveRoomCenter(upperRoom, upperRoom.transform.position);
        Vector3 lowerCenter = ResolveRoomCenter(lowerRoom, lowerRoom.transform.position);
        return upperCenter.y >= lowerCenter.y ? upperRoom : lowerRoom;
    }

    private RoomCameraTrigger ResolveLowerRoom()
    {
        if (upperRoom == null || lowerRoom == null)
        {
            return lowerRoom;
        }

        Vector3 upperCenter = ResolveRoomCenter(upperRoom, upperRoom.transform.position);
        Vector3 lowerCenter = ResolveRoomCenter(lowerRoom, lowerRoom.transform.position);
        return upperCenter.y >= lowerCenter.y ? lowerRoom : upperRoom;
    }

    private Vector3 ResolvePlayerCommitPoint(Transform player, Vector3 fallbackPosition)
    {
        if (player == null)
        {
            return fallbackPosition;
        }

        FillPlayerColliderBuffer(player);
        if (TryResolveBoundsCenter(false, out Vector3 center) ||
            TryResolveBoundsCenter(true, out center))
        {
            return center;
        }

        return fallbackPosition;
    }

    private bool TryResolveBoundsCenter(bool includeTriggers, out Vector3 center)
    {
        bool hasBounds = false;
        Bounds bounds = default;

        for (int i = 0; i < playerColliderBuffer.Count; i++)
        {
            Collider2D collider = playerColliderBuffer[i];
            if (collider == null ||
                !collider.enabled ||
                !includeTriggers && collider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        center = hasBounds ? bounds.center : Vector3.zero;
        return hasBounds;
    }

    private void FillPlayerColliderBuffer(Transform player)
    {
        playerColliderBuffer.Clear();
        if (player != null)
        {
            player.GetComponentsInChildren(false, playerColliderBuffer);
        }
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

        cachedMainCamera = MainCameraCache.Get();
        nextMainCameraRefreshTime = Time.unscaledTime + MainCameraRefreshInterval;
        mainCamera = cachedMainCamera;
        return mainCamera != null;
    }

    private static Vector3 ResolveRoomCenter(RoomCameraTrigger room, Vector3 fallback)
    {
        if (room != null && room.TryGetAreaBounds(out Bounds bounds))
        {
            return bounds.center;
        }

        if (room != null)
        {
            return room.transform.position;
        }

        return fallback;
    }

    private bool TryResolvePlayerTransform(Transform source, out Transform resolvedPlayer)
    {
        resolvedPlayer = null;
        if (source == null || string.IsNullOrWhiteSpace(playerTag))
        {
            return false;
        }

        Transform current = source;
        while (current != null)
        {
            if (current.CompareTag(playerTag))
            {
                resolvedPlayer = current;
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
