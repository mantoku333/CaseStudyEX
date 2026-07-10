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
        if (PlayerCameraColliderUtility.TryResolvePlayerTransform(collision, playerTag, out _))
        {
            HandlePlayerEntered();
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
        if (PlayerCameraColliderUtility.TryResolvePlayerTransform(other, playerTag, out _))
        {
            HandlePlayerEntered();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (PlayerCameraColliderUtility.TryResolvePlayerTransform(other, playerTag, out _))
        {
            overlapCount = Mathf.Max(0, overlapCount - 1);
        }
    }

    private void HandlePlayerEntered()
    {
        bool wasOutsideGate = overlapCount == 0;
        overlapCount++;

        if (!wasOutsideGate || targetRoom == null)
        {
            return;
        }

        targetRoom.ActivateCamera();
    }
}
