using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Bidirectional camera transition volume placed between two room areas.
/// While the player stays inside, a temporary camera previews the transition
/// without fully committing to the next room.
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomCameraPortal : MonoBehaviour
{
    private struct CameraPose
    {
        public Vector3 Position;
        public float OrthographicSize;
    }

    private static CinemachineCamera runtimeTransitionCamera;

    [Header("Rooms")]
    [SerializeField] private RoomCameraTrigger roomA;
    [SerializeField] private RoomCameraTrigger roomB;

    [Header("Transition Camera")]
    [SerializeField] private CinemachineCamera transitionCamera;
    [SerializeField] private int transitionPriority = 30;
    [SerializeField, Range(0f, 1f)] private float targetRoomWeight = 0.45f;
    [SerializeField, Min(0f)] private float smoothTime = 0.15f;
    [SerializeField, Min(0f)] private float playerPadding = 2f;
    [SerializeField, Min(0f)] private float minimumOrthographicSize;
    [SerializeField] private bool commitWhenPlayerFullyInsideRoom = true;

    [Header("Boss Room Preview")]
    [SerializeField] private bool limitBossRoomPreview = true;
    [SerializeField] private bool treatTargetAsBossRoom;
    [SerializeField, Range(0f, 1f)] private float bossRoomTargetWeight = 0.18f;
    [SerializeField, Range(0f, 1f)] private float bossRoomZoomWeight = 0.18f;
    [SerializeField, Range(0.3f, 1f)] private float bossRoomOrthographicSizeMultiplier = 0.8f;
    [SerializeField, Min(0f)] private float bossRoomRevealDistance = 1.5f;

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
                CommitPendingTransition(player.position);
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

        BeginPendingTransition(player.position);
        StartTransitionCamera(player.position);
    }

    private void BeginPendingTransition(Vector3 playerPosition)
    {
        pendingFromRoom = ResolveCurrentRoom(playerPosition);
        pendingToRoom = ResolveOppositeRoom(pendingFromRoom);
        fromPoseAtEntry = ResolveRoomPose(pendingFromRoom, playerPosition);
        toPoseAtEntry = ResolveRoomPose(pendingToRoom, playerPosition);
        followOffsetAtEntry = ResolveFollowOffsetAtEntry(playerPosition);
    }

    private void CommitPendingTransition(Vector3 playerPosition)
    {
        RoomCameraTrigger finalRoom = ResolveRoomContaining(playerPosition);
        if (finalRoom == null)
        {
            finalRoom = pendingFromRoom;
        }
        else if (pendingFromRoom != null &&
                 pendingToRoom != null &&
                 finalRoom != pendingFromRoom &&
                 finalRoom != pendingToRoom)
        {
            finalRoom = pendingFromRoom;
        }

        CommitToRoom(finalRoom);
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
        if (transitionCommitted || !transitionActive || playerTransform == null)
        {
            return;
        }

        if (TryCommitFullyEnteredRoom())
        {
            return;
        }

        CinemachineCamera camera = GetTransitionCamera();
        if (camera == null)
        {
            return;
        }

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

        float currentSize = camera.Lens.OrthographicSize;
        float nextSize = Mathf.SmoothDamp(
            currentSize,
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
            false);
        CameraPose toPose = ResolveTransitionPose(
            pendingToRoom,
            toPoseAtEntry,
            playerPosition,
            true);

        CameraPose pose;
        bool targetIsBossRoom = ShouldLimitTargetRoomPreview(pendingToRoom);

        if (targetIsBossRoom)
        {
            pose = BuildBossRoomTransitionPose(fromPose, toPose, playerPosition);
        }
        else
        {
            pose.Position = Vector3.Lerp(fromPose.Position, toPose.Position, targetRoomWeight);
            pose.OrthographicSize = Mathf.Max(
                minimumOrthographicSize,
                fromPose.OrthographicSize,
                toPose.OrthographicSize);
            KeepPlayerInsideViewForTransition(ref pose, playerPosition, fromPose, toPose);
        }

        return pose;
    }

    private bool ShouldLimitTargetRoomPreview(RoomCameraTrigger room)
    {
        if (!limitBossRoomPreview)
        {
            return false;
        }

        return treatTargetAsBossRoom || (room != null && room.IsBossRoom);
    }

    private CameraPose BuildBossRoomTransitionPose(
        CameraPose fromPose,
        CameraPose toPose,
        Vector3 playerPosition)
    {
        CameraPose pose;
        pose.Position = Vector3.Lerp(fromPose.Position, toPose.Position, bossRoomTargetWeight);
        float previewSize = Mathf.Lerp(fromPose.OrthographicSize, toPose.OrthographicSize, bossRoomZoomWeight);
        float maxBossPreviewSize = fromPose.OrthographicSize * bossRoomOrthographicSizeMultiplier;
        pose.OrthographicSize = Mathf.Max(
            minimumOrthographicSize,
            Mathf.Min(previewSize, maxBossPreviewSize));

        KeepPlayerInsideViewForTransition(ref pose, playerPosition, fromPose, toPose);
        ClampBossRoomPreview(ref pose, fromPose, toPose, bossRoomTargetWeight);
        ClampBossRoomViewEdge(ref pose, fromPose, toPose);
        return pose;
    }

    private static void ClampBossRoomPreview(
        ref CameraPose pose,
        CameraPose fromPose,
        CameraPose toPose,
        float maxTargetWeight)
    {
        Vector3 fromToTarget = toPose.Position - fromPose.Position;
        fromToTarget.z = 0f;
        float targetDistance = fromToTarget.magnitude;
        if (targetDistance <= 0.001f)
        {
            return;
        }

        Vector3 targetDirection = fromToTarget / targetDistance;
        Vector3 fromToPreview = pose.Position - fromPose.Position;
        fromToPreview.z = 0f;

        float previewDistanceTowardTarget = Vector3.Dot(fromToPreview, targetDirection);
        float maxPreviewDistanceTowardTarget = targetDistance * Mathf.Clamp01(maxTargetWeight);
        if (previewDistanceTowardTarget <= maxPreviewDistanceTowardTarget)
        {
            return;
        }

        Vector3 excess = targetDirection * (previewDistanceTowardTarget - maxPreviewDistanceTowardTarget);
        pose.Position -= excess;
    }

    private void ClampBossRoomViewEdge(ref CameraPose pose, CameraPose fromPose, CameraPose toPose)
    {
        if (pendingFromRoom == null || !pendingFromRoom.TryGetAreaBounds(out Bounds fromBounds))
        {
            return;
        }

        Vector3 direction = toPose.Position - fromPose.Position;
        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        float halfHeight = pose.OrthographicSize;
        float halfWidth = pose.OrthographicSize * aspect;

        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
        {
            if (direction.x >= 0f)
            {
                float maxX = fromBounds.max.x + bossRoomRevealDistance - halfWidth;
                pose.Position.x = Mathf.Min(pose.Position.x, maxX);
            }
            else
            {
                float minX = fromBounds.min.x - bossRoomRevealDistance + halfWidth;
                pose.Position.x = Mathf.Max(pose.Position.x, minX);
            }

            return;
        }

        if (direction.y >= 0f)
        {
            float maxY = fromBounds.max.y + bossRoomRevealDistance - halfHeight;
            pose.Position.y = Mathf.Min(pose.Position.y, maxY);
        }
        else
        {
            float minY = fromBounds.min.y - bossRoomRevealDistance + halfHeight;
            pose.Position.y = Mathf.Max(pose.Position.y, minY);
        }
    }

    private CameraPose ResolveRoomPose(RoomCameraTrigger room, Vector3 fallbackPosition)
    {
        CameraPose pose;
        if (room != null && room.TryGetCameraPose(out pose.Position, out pose.OrthographicSize))
        {
            return pose;
        }

        if (Camera.main != null)
        {
            pose.Position = Camera.main.transform.position;
            pose.OrthographicSize = Camera.main.orthographic ? Camera.main.orthographicSize : 10f;
            return pose;
        }

        pose.Position = fallbackPosition;
        pose.Position.z = -10f;
        pose.OrthographicSize = 10f;
        return pose;
    }

    private CameraPose ResolveCurrentViewPose(Vector3 fallbackPosition)
    {
        CameraPose pose;
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            pose.Position = mainCamera.transform.position;
            pose.OrthographicSize = mainCamera.orthographic
                ? mainCamera.orthographicSize
                : Mathf.Max(fromPoseAtEntry.OrthographicSize, minimumOrthographicSize);
            return pose;
        }

        CinemachineCamera camera = GetTransitionCamera(false);
        if (camera != null)
        {
            pose.Position = camera.transform.position;
            pose.OrthographicSize = camera.Lens.OrthographicSize;
            return pose;
        }

        pose = ResolveTransitionPose(pendingFromRoom, fromPoseAtEntry, fallbackPosition, false);
        pose.OrthographicSize = Mathf.Max(pose.OrthographicSize, minimumOrthographicSize);
        return pose;
    }

    private CameraPose ResolveTransitionPose(
        RoomCameraTrigger room,
        CameraPose poseAtEntry,
        Vector3 playerPosition,
        bool allowDefaultAreaPreview)
    {
        if (room != null && room.UsesDefaultCameraWhenEntered)
        {
            poseAtEntry = ResolveDefaultRoomPreviewPose(
                room,
                poseAtEntry,
                playerPosition,
                allowDefaultAreaPreview);
        }

        return poseAtEntry;
    }

    private CameraPose ResolveDefaultRoomPreviewPose(
        RoomCameraTrigger room,
        CameraPose poseAtEntry,
        Vector3 playerPosition,
        bool allowAreaPreview)
    {
        Vector3 followPosition = playerPosition + followOffsetAtEntry;
        poseAtEntry.Position.x = followPosition.x;
        poseAtEntry.Position.y = followPosition.y;

        if (!allowAreaPreview ||
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

        RoomCameraTrigger defaultRoom = null;
        if (pendingFromRoom != null && pendingFromRoom.UsesDefaultCameraWhenEntered)
        {
            defaultRoom = pendingFromRoom;
        }
        else if (pendingToRoom != null && pendingToRoom.UsesDefaultCameraWhenEntered)
        {
            defaultRoom = pendingToRoom;
        }

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

        if (CameraManager.Instance != null)
        {
            CameraManager.Instance.TrySetFollowCameraPose(
                camera.transform.position,
                camera.Lens.OrthographicSize);
        }
    }

    private void KeepPlayerInsideViewForTransition(
        ref CameraPose pose,
        Vector3 playerPosition,
        CameraPose fromPose,
        CameraPose toPose)
    {
        Vector3 direction = toPose.Position - fromPose.Position;
        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            KeepPlayerInsideView(ref pose, playerPosition, true, true);
            return;
        }

        bool horizontalTransition = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y);
        KeepPlayerInsideView(
            ref pose,
            playerPosition,
            horizontalTransition,
            !horizontalTransition);
    }

    private void KeepPlayerInsideView(
        ref CameraPose pose,
        Vector3 playerPosition,
        bool constrainX,
        bool constrainY)
    {
        float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
        float halfHeight = Mathf.Max(0.1f, pose.OrthographicSize - playerPadding);
        float halfWidth = Mathf.Max(0.1f, pose.OrthographicSize * aspect - playerPadding);

        if (constrainX && playerPosition.x < pose.Position.x - halfWidth)
        {
            pose.Position.x = playerPosition.x + halfWidth;
        }
        else if (constrainX && playerPosition.x > pose.Position.x + halfWidth)
        {
            pose.Position.x = playerPosition.x - halfWidth;
        }

        if (constrainY && playerPosition.y < pose.Position.y - halfHeight)
        {
            pose.Position.y = playerPosition.y + halfHeight;
        }
        else if (constrainY && playerPosition.y > pose.Position.y + halfHeight)
        {
            pose.Position.y = playerPosition.y - halfHeight;
        }
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

    private bool TryCommitFullyEnteredRoom()
    {
        if (!commitWhenPlayerFullyInsideRoom || playerTransform == null)
        {
            return false;
        }

        if (pendingToRoom == null || !IsPlayerFullyInsideRoom(pendingToRoom))
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
        if (room == null || playerTransform == null)
        {
            return false;
        }

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

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            checkedCollider = true;
            if (!IsBoundsInsideRoom(room, collider.bounds))
            {
                return false;
            }
        }

        return checkedCollider
            ? true
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

        GameObject cameraObject = new GameObject("CN_PortalTransitionRuntime");
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
        if (roomA == null || roomB == null)
        {
            return null;
        }

        RoomCameraTrigger activeRoom = RoomCameraTrigger.ActiveRoom;
        if (activeRoom == roomA)
        {
            return roomA;
        }

        if (activeRoom == roomB)
        {
            return roomB;
        }

        return ResolveRoomContaining(playerPosition);
    }

    private RoomCameraTrigger ResolveRoomContaining(Vector3 playerPosition)
    {
        if (roomA == null || roomB == null)
        {
            return null;
        }

        if (roomA.ContainsPoint(playerPosition))
        {
            return roomA;
        }

        if (roomB.ContainsPoint(playerPosition))
        {
            return roomB;
        }

        return null;
    }

    private RoomCameraTrigger ResolveOppositeRoom(RoomCameraTrigger room)
    {
        if (room == roomA)
        {
            return roomB;
        }

        if (room == roomB)
        {
            return roomA;
        }

        return null;
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
