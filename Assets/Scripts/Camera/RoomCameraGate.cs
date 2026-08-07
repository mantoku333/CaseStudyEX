using UnityEngine;

/// <summary>
/// One-way camera transition volume.
/// Place it only where touching this collider should switch to targetRoom.
/// </summary>
public sealed class RoomCameraGate : MonoBehaviour
{
    [SerializeField] private RoomCameraTrigger targetRoom;
    [SerializeField] private string playerTag = "Player";

    private int overlapCount;

    private void OnDisable()
    {
        overlapCount = 0;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (PlayerCameraColliderUtility.TryResolvePlayerTransform(
                collision,
                playerTag,
                out Transform player))
        {
            HandlePlayerEntered(player);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (PlayerCameraColliderUtility.TryResolvePlayerTransform(collision, playerTag, out _))
        {
            overlapCount = Mathf.Max(0, overlapCount - 1);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (PlayerCameraColliderUtility.TryResolvePlayerTransform(
                other,
                playerTag,
                out Transform player))
        {
            HandlePlayerEntered(player);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (PlayerCameraColliderUtility.TryResolvePlayerTransform(other, playerTag, out _))
        {
            overlapCount = Mathf.Max(0, overlapCount - 1);
        }
    }

    private void HandlePlayerEntered(Transform player)
    {
        bool wasOutsideGate = overlapCount == 0;
        overlapCount++;

        if (!wasOutsideGate || targetRoom == null)
        {
            return;
        }

        Vector3 revealOrigin = player != null ? player.position : transform.position;
        if (player != null &&
            PlayerCameraColliderUtility.TryGetCameraPoint(player, out Vector3 cameraPoint))
        {
            revealOrigin = cameraPoint;
        }

        RoomFogRevealManager.PreviewRoomFromPortal(targetRoom, revealOrigin);
        targetRoom.ActivateCamera();
    }
}
