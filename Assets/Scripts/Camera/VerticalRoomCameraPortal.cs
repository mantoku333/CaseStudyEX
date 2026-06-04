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
    private Transform playerTransform;
    private RoomCameraTrigger pendingFromRoom;
    private RoomCameraTrigger pendingToRoom;
    private CameraPose fromPoseAtEntry;
    private CameraPose toPoseAtEntry;
    private Vector3 followOffsetAtEntry;
    private Vector3 transitionVelocity;
    private float transitionZoomVelocity;
    private float transitionElapsed;
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
                CommitExit(player.position);
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

        BeginTransition(player.position);
        StartTransitionCamera(player.position);
    }

    private void BeginTransition(Vector3 playerPosition)
    {
        pendingFromRoom = ResolveCurrentRoom(playerPosition);
        pendingToRoom = ResolveDestinationRoom(pendingFromRoom);
        fromPoseAtEntry = ResolveRoomPose(pendingFromRoom, playerPosition);
        toPoseAtEntry = ResolveRoomPose(pendingToRoom, playerPosition);
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

        if (lowerRoom != null && lowerRoom.ContainsPoint(playerPosition))
        {
            return lowerRoom;
        }

        if (upperRoom != null && upperRoom.ContainsPoint(playerPosition))
        {
            return upperRoom;
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
        CameraPose targetPose = BuildTransitionPose(playerTransform.position);
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
        CameraPose fromPose = ResolveTransitionPose(pendingFromRoom, fromPoseAtEntry, playerPosition, false);
        CameraPose toPose = ResolveTransitionPose(pendingToRoom, toPoseAtEntry, playerPosition, false);

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

    private CameraPose ResolveRoomPose(RoomCameraTrigger room, Vector3 fallbackPosition)
    {
        CameraPose pose;
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

    private CameraPose ResolveCurrentViewPose(Vector3 fallbackPosition)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            CameraPose pose;
            pose.Position = mainCamera.transform.position;
            pose.OrthographicSize = mainCamera.orthographic
                ? mainCamera.orthographicSize
                : Mathf.Max(fromPoseAtEntry.OrthographicSize, minimumOrthographicSize);
            return pose;
        }

        CinemachineCamera camera = GetTransitionCamera(false);
        if (camera != null)
        {
            CameraPose pose;
            pose.Position = camera.transform.position;
            pose.OrthographicSize = camera.Lens.OrthographicSize;
            return pose;
        }

        return ResolveTransitionPose(pendingFromRoom, fromPoseAtEntry, fallbackPosition, false);
    }

    private CameraPose ResolveTransitionPose(
        RoomCameraTrigger room,
        CameraPose poseAtEntry,
        Vector3 playerPosition,
        bool allowDefaultAreaPreview)
    {
        if (room == null || !room.UsesDefaultCameraWhenEntered)
        {
            return poseAtEntry;
        }

        Vector3 followPosition = playerPosition + followOffsetAtEntry;
        poseAtEntry.Position.x = followPosition.x;
        poseAtEntry.Position.y = followPosition.y;

        if (!allowDefaultAreaPreview ||
            !room.PreviewDefaultCameraByAreaBounds ||
            !room.TryGetAreaBounds(out Bounds bounds))
        {
            return poseAtEntry;
        }

        Vector3 previewCenter = Vector3.Lerp(
            followPosition,
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

    private Vector3 ResolveFollowOffsetAtEntry(Vector3 playerPosition)
    {
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
        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
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
        Collider2D[] colliders = playerTransform.GetComponentsInChildren<Collider2D>();
        bool checkedCollider = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
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
        RoomCameraTrigger activeRoom = RoomCameraTrigger.ActiveRoom;
        if (activeRoom == upperRoom || activeRoom == lowerRoom)
        {
            return activeRoom;
        }

        if (upperRoom != null && upperRoom.ContainsPoint(playerPosition))
        {
            return upperRoom;
        }

        if (lowerRoom != null && lowerRoom.ContainsPoint(playerPosition))
        {
            return lowerRoom;
        }

        return ResolveRoomOnPortalSide(playerPosition);
    }

    private RoomCameraTrigger ResolveDestinationRoom(RoomCameraTrigger fromRoom)
    {
        if (fromRoom == upperRoom && direction != PortalDirection.LowerToUpperOnly)
        {
            return lowerRoom;
        }

        if (fromRoom == lowerRoom && direction != PortalDirection.UpperToLowerOnly)
        {
            return upperRoom;
        }

        return null;
    }

    private RoomCameraTrigger ResolveRoomOnPortalSide(Vector3 playerPosition)
    {
        Vector3 upperCenter = ResolveRoomCenter(upperRoom, transform.position + Vector3.up);
        Vector3 lowerCenter = ResolveRoomCenter(lowerRoom, transform.position + Vector3.down);
        Vector3 lowerToUpper = upperCenter - lowerCenter;
        lowerToUpper.z = 0f;

        if (lowerToUpper.sqrMagnitude <= 0.001f)
        {
            lowerToUpper = Vector3.up;
        }

        Vector3 portalToPlayer = playerPosition - transform.position;
        portalToPlayer.z = 0f;
        return Vector3.Dot(portalToPlayer, lowerToUpper) >= 0f ? upperRoom : lowerRoom;
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
