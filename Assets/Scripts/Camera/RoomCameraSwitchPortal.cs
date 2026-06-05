using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dedicated camera switch collider. It only activates the configured room camera.
/// Teleporting and player control are handled by other components.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Camera/Room Camera Switch Portal")]
public sealed class RoomCameraSwitchPortal : MonoBehaviour
{
    [SerializeField] private RoomCameraTrigger targetRoom;
    [SerializeField] private string playerTag = "Player";

    private readonly HashSet<Collider2D> overlappingPlayerColliders2D = new();
    private readonly HashSet<Collider> overlappingPlayerColliders = new();

    private void OnDisable()
    {
        overlappingPlayerColliders2D.Clear();
        overlappingPlayerColliders.Clear();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (TryResolvePlayerTransform(collision.transform, out _))
        {
            HandlePlayerEntered(collision);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (TryResolvePlayerTransform(collision.transform, out _))
        {
            HandlePlayerEntered(collision);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (TryResolvePlayerTransform(collision.transform, out _))
        {
            overlappingPlayerColliders2D.Remove(collision);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Collider2D playerCollider = collision.collider;
        if (playerCollider != null &&
            TryResolvePlayerTransform(playerCollider.transform, out _))
        {
            HandlePlayerEntered(playerCollider);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        Collider2D playerCollider = collision.collider;
        if (playerCollider != null &&
            TryResolvePlayerTransform(playerCollider.transform, out _))
        {
            HandlePlayerEntered(playerCollider);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        Collider2D playerCollider = collision.collider;
        if (playerCollider != null &&
            TryResolvePlayerTransform(playerCollider.transform, out _))
        {
            overlappingPlayerColliders2D.Remove(playerCollider);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (TryResolvePlayerTransform(other.transform, out _))
        {
            HandlePlayerEntered(other);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (TryResolvePlayerTransform(other.transform, out _))
        {
            HandlePlayerEntered(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (TryResolvePlayerTransform(other.transform, out _))
        {
            overlappingPlayerColliders.Remove(other);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Collider playerCollider = collision.collider;
        if (playerCollider != null &&
            TryResolvePlayerTransform(playerCollider.transform, out _))
        {
            HandlePlayerEntered(playerCollider);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        Collider playerCollider = collision.collider;
        if (playerCollider != null &&
            TryResolvePlayerTransform(playerCollider.transform, out _))
        {
            HandlePlayerEntered(playerCollider);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        Collider playerCollider = collision.collider;
        if (playerCollider != null &&
            TryResolvePlayerTransform(playerCollider.transform, out _))
        {
            overlappingPlayerColliders.Remove(playerCollider);
        }
    }

    private void HandlePlayerEntered(Collider2D playerCollider)
    {
        bool wasOutsidePortal =
            overlappingPlayerColliders2D.Count == 0 &&
            overlappingPlayerColliders.Count == 0;
        overlappingPlayerColliders2D.Add(playerCollider);

        if (wasOutsidePortal)
        {
            ActivateTargetRoom();
        }
    }

    private void HandlePlayerEntered(Collider playerCollider)
    {
        bool wasOutsidePortal =
            overlappingPlayerColliders2D.Count == 0 &&
            overlappingPlayerColliders.Count == 0;
        overlappingPlayerColliders.Add(playerCollider);

        if (wasOutsidePortal)
        {
            ActivateTargetRoom();
        }
    }

    private void ActivateTargetRoom()
    {
        if (targetRoom == null)
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
