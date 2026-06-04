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
        if (TryResolvePlayerTransform(collision.transform, out _))
        {
            HandlePlayerEntered();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (TryResolvePlayerTransform(collision.transform, out _))
        {
            overlapCount = Mathf.Max(0, overlapCount - 1);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (TryResolvePlayerTransform(other.transform, out _))
        {
            HandlePlayerEntered();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (TryResolvePlayerTransform(other.transform, out _))
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
