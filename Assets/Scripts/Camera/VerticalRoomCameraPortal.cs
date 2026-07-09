using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;

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

    [Header("Rooms")]
    [SerializeField] private RoomCameraTrigger upperRoom;
    [SerializeField] private RoomCameraTrigger lowerRoom;

    [Header("Transition Camera")]
    [SerializeField] private CinemachineCamera transitionCamera;
    [SerializeField] private int transitionPriority = 30;
    [SerializeField] private PortalDirection direction = PortalDirection.Bidirectional;

    [FormerlySerializedAs("startTargetRoomWeight")]
    [SerializeField, Range(0f, 1f)] private float portalRoomWeight = 0.35f;

    [SerializeField, Min(0f)] private float smoothTime = 0.12f;
    [SerializeField, Min(0f)] private float minimumOrthographicSize;
    [SerializeField] private bool commitWhenPlayerFullyInsideTargetRoom;
    [SerializeField] private bool commitByPortalExitSide = true;

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";

    private readonly HashSet<Collider2D> overlappingPlayerColliders = new();
    private Transform playerTransform;
    private RoomCameraTrigger pendingFromRoom;
    private RoomCameraTrigger pendingToRoom;
    private CameraPose fromPoseAtEntry;
    private CameraPose toPoseAtEntry;
    private Vector3 followOffsetAtEntry;
    private Vector3 transitionVelocity;
    private float transitionZoomVelocity;
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
        transitionCommitted = false;
        StopTransitionCamera();
    }

    private void Update()
    {
        UpdateTransitionCamera();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (PlayerCameraColliderUtility.TryResolvePlayerTransform(collision, playerTag, out Transform player))
        {
            HandlePlayerEntered(collision, player);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (PlayerCameraColliderUtility.TryResolvePlayerTransform(collision, playerTag, out Transform player))
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
        if (CanPreviewPendingRoom())
        {
            StartTransitionCamera(playerCommitPoint);
        }
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
        if (finalRoom == pendingToRoom && !CanPreviewPendingRoom())
        {
            finalRoom = pendingFromRoom;
        }

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
        transitionActive = true;
    }

    private void UpdateTransitionCamera()
    {
        if (transitionCommitted || playerTransform == null)
        {
            return;
        }

        if (!CanPreviewPendingRoom())
        {
            if (transitionActive)
            {
                StopTransitionCamera();
            }

            return;
        }

        if (!transitionActive)
        {
            Vector3 startPosition = ResolvePlayerCommitPoint(
                playerTransform,
                playerTransform.position);
            StartTransitionCamera(startPosition);
            if (!transitionActive)
            {
                return;
            }
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
            true,
            false);

        CameraPose pose;
        pose.Position = Vector3.Lerp(fromPose.Position, toPose.Position, portalRoomWeight);
        pose.OrthographicSize = Mathf.Max(
            minimumOrthographicSize,
            Mathf.Lerp(fromPose.OrthographicSize, toPose.OrthographicSize, portalRoomWeight));

        pose.Position.x = fromPoseAtEntry.Position.x;
        return pose;
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

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
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
            float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
            pose.Position = bounds.center;
            pose.Position.z = Camera.main != null ? Camera.main.transform.position.z : transform.position.z;
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
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
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

        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
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

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            pose.Position.z = mainCamera.transform.position.z;
        }

        return true;
    }

    private static bool TryFindFollowCamera(out CinemachineCamera followCamera)
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

    private static bool TryApplyPositionComposerPose(
        CinemachineCamera followCamera,
        Vector3 playerPosition,
        ref CameraPose pose)
    {
        CinemachinePositionComposer composer = followCamera.GetComponent<CinemachinePositionComposer>();
        if (composer == null)
        {
            return false;
        }

        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
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

    private bool TryCommitFullyEnteredTargetRoom()
    {
        if (!commitWhenPlayerFullyInsideTargetRoom ||
            playerTransform == null ||
            pendingToRoom == null ||
            !CanPreviewPendingRoom() ||
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

    private bool CanPreviewPendingRoom()
    {
        return RoomPortalAccessCondition.AllowsPreview(
            this,
            pendingFromRoom,
            pendingToRoom);
    }

    private bool IsPlayerFullyInsideRoom(RoomCameraTrigger room)
    {
        return PlayerCameraColliderUtility.TryGetCameraBounds(playerTransform, out Bounds cameraBounds)
            ? IsBoundsInsideRoom(room, cameraBounds)
            : room.ContainsPoint(playerTransform.position);
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

    private static Vector3 ResolvePlayerCommitPoint(Transform player, Vector3 fallbackPosition)
    {
        if (player == null)
        {
            return fallbackPosition;
        }

        if (PlayerCameraColliderUtility.TryGetCameraPoint(player, out Vector3 center))
        {
            return center;
        }

        return fallbackPosition;
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

}
