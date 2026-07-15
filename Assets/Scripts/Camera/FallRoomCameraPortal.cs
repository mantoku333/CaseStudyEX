using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Direct vertical room switch for fall boundaries.
/// Unlike RoomCameraPortal, this component never creates a transition camera.
/// Cinemachine blends directly from the active area camera to the adjacent one.
/// </summary>
[DisallowMultipleComponent]
public sealed class FallRoomCameraPortal : MonoBehaviour
{
    [Header("Rooms")]
    [SerializeField] private RoomCameraTrigger upperRoom;
    [SerializeField] private RoomCameraTrigger lowerRoom;

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";

    private readonly HashSet<Collider2D> overlappingPlayerColliders = new();
    private RoomCameraTrigger entryRoom;
    private RoomCameraTrigger destinationRoom;

    private void OnDisable()
    {
        overlappingPlayerColliders.Clear();
        entryRoom = null;
        destinationRoom = null;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!PlayerCameraColliderUtility.TryResolvePlayerTransform(collision, playerTag, out Transform player))
        {
            return;
        }

        bool wasOutsidePortal = overlappingPlayerColliders.Count == 0;
        overlappingPlayerColliders.Add(collision);
        if (!wasOutsidePortal)
        {
            return;
        }

        Vector3 playerPosition = ResolvePlayerCommitPoint(player, player.position);
        entryRoom = ResolveCurrentRoom(playerPosition);
        destinationRoom = ResolveOppositeRoom(entryRoom);

        if (destinationRoom != null &&
            RoomPortalAccessCondition.AllowsPreview(this, entryRoom, destinationRoom))
        {
            destinationRoom.ActivateCamera();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!PlayerCameraColliderUtility.TryResolvePlayerTransform(collision, playerTag, out Transform player))
        {
            return;
        }

        overlappingPlayerColliders.Remove(collision);
        if (overlappingPlayerColliders.Count != 0)
        {
            return;
        }

        Vector3 playerPosition = ResolvePlayerCommitPoint(player, player.position);
        RoomCameraTrigger finalRoom = ResolveRoomOnPortalSide(playerPosition);
        if (finalRoom == destinationRoom &&
            !RoomPortalAccessCondition.AllowsPreview(this, entryRoom, destinationRoom))
        {
            finalRoom = entryRoom;
        }

        finalRoom?.ActivateCamera();
        entryRoom = null;
        destinationRoom = null;
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

    private RoomCameraTrigger ResolveOppositeRoom(RoomCameraTrigger room)
    {
        if (room == upperRoom)
        {
            return lowerRoom;
        }

        if (room == lowerRoom)
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

    private static Vector3 ResolvePlayerCommitPoint(Transform player, Vector3 fallbackPosition)
    {
        if (player == null)
        {
            return fallbackPosition;
        }

        return PlayerCameraColliderUtility.TryGetCameraPoint(player, out Vector3 center)
            ? center
            : fallbackPosition;
    }

    private static Vector3 ResolveRoomCenter(RoomCameraTrigger room, Vector3 fallback)
    {
        if (room != null && room.TryGetAreaBounds(out Bounds bounds))
        {
            return bounds.center;
        }

        return room != null ? room.transform.position : fallback;
    }

}
