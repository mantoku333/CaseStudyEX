using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Camera transition volume for one-way drops from an upper room into a lower room.
/// The lower room is previewed while the player is falling through the portal.
/// </summary>
[DisallowMultipleComponent]
public sealed class FallRoomCameraPortal : MonoBehaviour
{
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
    [SerializeField, Range(0f, 1f)] private float startLowerRoomWeight = 0.35f;
    [SerializeField, Range(0f, 1f)] private float targetLowerRoomWeight = 1f;
    [SerializeField, Min(0f)] private float fallPreviewDuration = 0.75f;
    [SerializeField, Min(0f)] private float smoothTime = 0.12f;
    [SerializeField, Min(0f)] private float playerPadding = 2.5f;
    [SerializeField, Min(0f)] private float minimumOrthographicSize;
    [SerializeField] private bool commitWhenPlayerFullyInsideLowerRoom;
    [SerializeField] private bool commitToLowerWhenExitingBelowPortal = true;

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";

    private readonly HashSet<Collider2D> overlappingPlayerColliders = new();
    private readonly List<Collider2D> playerColliderBuffer = new();
    private Transform playerTransform;
    private CameraPose upperPoseAtEntry;
    private CameraPose lowerPoseAtEntry;
    private Vector3 followOffsetAtEntry;
    private Vector3 transitionVelocity;
    private float transitionZoomVelocity;
    private float transitionElapsed;
    private Camera cachedMainCamera;
    private float nextMainCameraRefreshTime;
    private bool transitionActive;
    private bool transitionCommitted;

    private void OnDisable()
    {
        overlappingPlayerColliders.Clear();
        playerTransform = null;
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
        upperPoseAtEntry = ResolveRoomPose(upperRoom, playerPosition);
        lowerPoseAtEntry = ResolveRoomPose(lowerRoom, playerPosition);
        followOffsetAtEntry = ResolveFollowOffsetAtEntry(playerPosition);
    }

    private void CommitExit(Vector3 playerPosition)
    {
        RoomCameraTrigger finalRoom = ResolveExitRoom(playerPosition);
        CommitToRoom(finalRoom);
    }

    private RoomCameraTrigger ResolveExitRoom(Vector3 playerPosition)
    {
        if (lowerRoom != null && lowerRoom.ContainsPoint(playerPosition))
        {
            return lowerRoom;
        }

        if (upperRoom != null && upperRoom.ContainsPoint(playerPosition))
        {
            return upperRoom;
        }

        if (commitToLowerWhenExitingBelowPortal &&
            transform.InverseTransformPoint(playerPosition).y < 0f)
        {
            return lowerRoom;
        }

        return upperRoom;
    }

    private void StartTransitionCamera(Vector3 playerPosition)
    {
        if (upperRoom == null || lowerRoom == null)
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

        if (TryCommitFullyEnteredLowerRoom())
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
        CameraPose upperPose = ResolveTransitionPose(upperRoom, upperPoseAtEntry, playerPosition, false);
        CameraPose lowerPose = ResolveTransitionPose(lowerRoom, lowerPoseAtEntry, playerPosition, false);

        CameraPose pose;
        float lowerWeight = ResolveLowerRoomWeight();
        pose.Position = Vector3.Lerp(upperPose.Position, lowerPose.Position, lowerWeight);
        pose.OrthographicSize = Mathf.Max(
            minimumOrthographicSize,
            Mathf.Lerp(upperPose.OrthographicSize, lowerPose.OrthographicSize, lowerWeight));

        KeepPlayerInsideView(ref pose, playerPosition);
        return pose;
    }

    private float ResolveLowerRoomWeight()
    {
        if (fallPreviewDuration <= 0f)
        {
            return targetLowerRoomWeight;
        }

        float t = Mathf.Clamp01(transitionElapsed / fallPreviewDuration);
        return Mathf.SmoothStep(startLowerRoomWeight, targetLowerRoomWeight, t);
    }

    private CameraPose ResolveRoomPose(RoomCameraTrigger room, Vector3 fallbackPosition)
    {
        CameraPose pose;
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

    private CameraPose ResolveCurrentViewPose(Vector3 fallbackPosition)
    {
        if (TryGetMainCamera(out Camera mainCamera))
        {
            CameraPose pose;
            pose.Position = mainCamera.transform.position;
            pose.OrthographicSize = mainCamera.orthographic
                ? mainCamera.orthographicSize
                : Mathf.Max(upperPoseAtEntry.OrthographicSize, minimumOrthographicSize);
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

        return ResolveTransitionPose(upperRoom, upperPoseAtEntry, fallbackPosition, false);
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

    private Vector3 ResolveFollowOffsetAtEntry(Vector3 playerPosition)
    {
        if (CameraManager.Instance != null &&
            CameraManager.Instance.TryGetFollowCameraPose(out Vector3 followPosition, out _))
        {
            return followPosition - playerPosition;
        }

        RoomCameraTrigger defaultRoom = lowerRoom != null && lowerRoom.UsesDefaultCameraWhenEntered
            ? lowerRoom
            : upperRoom;

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

    private bool TryCommitFullyEnteredLowerRoom()
    {
        if (!commitWhenPlayerFullyInsideLowerRoom ||
            playerTransform == null ||
            lowerRoom == null ||
            !IsPlayerFullyInsideRoom(lowerRoom))
        {
            return false;
        }

        CommitToRoom(lowerRoom);
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

        GameObject cameraObject = new GameObject("CN_FallPortalTransitionRuntime");
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

        cachedMainCamera = Camera.main;
        nextMainCameraRefreshTime = Time.unscaledTime + MainCameraRefreshInterval;
        mainCamera = cachedMainCamera;
        return mainCamera != null;
    }
}
