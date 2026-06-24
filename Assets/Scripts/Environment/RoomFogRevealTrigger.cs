using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Environment/Room Fog Reveal Trigger")]
public sealed class RoomFogRevealTrigger : MonoBehaviour
{
    [SerializeField] private RoomCameraTrigger[] targetRooms = new RoomCameraTrigger[0];
    [SerializeField] private string playerTag = "Player";

    public void Configure(RoomCameraTrigger[] rooms)
    {
        targetRooms = rooms ?? new RoomCameraTrigger[0];
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (TryResolvePlayerTransform(collision.transform, out _))
        {
            RevealDestinationRooms();
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (TryResolvePlayerTransform(collision.transform, out _))
        {
            RevealDestinationRooms();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider != null &&
            TryResolvePlayerTransform(collision.collider.transform, out _))
        {
            RevealDestinationRooms();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (TryResolvePlayerTransform(other.transform, out _))
        {
            RevealDestinationRooms();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (TryResolvePlayerTransform(other.transform, out _))
        {
            RevealDestinationRooms();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider != null &&
            TryResolvePlayerTransform(collision.collider.transform, out _))
        {
            RevealDestinationRooms();
        }
    }

    private void RevealDestinationRooms()
    {
        if (targetRooms == null || targetRooms.Length == 0)
        {
            return;
        }

        RoomCameraTrigger activeRoom = RoomCameraTrigger.ActiveRoom;
        bool activeRoomIsEndpoint = false;

        for (int i = 0; i < targetRooms.Length; i++)
        {
            if (targetRooms[i] == activeRoom)
            {
                activeRoomIsEndpoint = true;
                break;
            }
        }

        for (int i = 0; i < targetRooms.Length; i++)
        {
            RoomCameraTrigger targetRoom = targetRooms[i];
            if (targetRoom == null)
            {
                continue;
            }

            if (activeRoomIsEndpoint && targetRoom == activeRoom)
            {
                continue;
            }

            if (!RoomPortalAccessCondition.AllowsPreview(
                    this,
                    activeRoom,
                    targetRoom))
            {
                continue;
            }

            RoomFogRevealManager.RevealRoom(targetRoom);
        }
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
