using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Optional trigger placed on a seam between two rooms, for when a room should become active
/// slightly before the player physically crosses into it. Widen the collider to switch earlier.
///
/// This replaces all five portal classes:
///   <c>RoomCameraPortal</c>, <c>VerticalRoomCameraPortal</c>, <c>FallRoomCameraPortal</c>,
///   <c>RoomCameraSwitchPortal</c>, <c>RoomCameraGate</c>
///
/// Those together were about 2,500 lines, of which 286 were byte-identical between the
/// horizontal and vertical variants. They were large because they blended camera poses by
/// hand. Nothing here blends: it asks <see cref="RoomCameraDirector"/> for a room and lets
/// CinemachineBrain do the transition.
/// </summary>
[AddComponentMenu("Cinemachine/Room Camera Early Switch")]
[DisallowMultipleComponent]
public sealed class RoomCameraEarlySwitch : MonoBehaviour
{
    [SerializeField, Tooltip("Room that becomes active while the player is inside this trigger.")]
    private RoomCameraVolume targetRoom;

    [SerializeField, Tooltip(
        "Hold the room until the player leaves the trigger. When off, the room is requested once " +
        "on entry and normal room resolution takes over immediately afterwards.")]
    private bool holdWhileInside = true;

    [SerializeField]
    private string playerTag = "Player";

    private int overlapCount;

    private void OnDisable()
    {
        overlapCount = 0;
        RoomCameraDirector.ReleaseRoom(this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        overlapCount++;
        Apply();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other))
        {
            return;
        }

        overlapCount = Mathf.Max(0, overlapCount - 1);
        if (overlapCount == 0)
        {
            RoomCameraDirector.ReleaseRoom(this);
        }
    }

    private void Apply()
    {
        if (targetRoom == null)
        {
            Debug.LogWarning($"[{name}] RoomCameraEarlySwitch has no target room assigned.", this);
            return;
        }

        if (!holdWhileInside)
        {
            // One-shot: nudge the director, then let it resolve normally.
            RoomCameraDirector.RequestRoom(this, targetRoom);
            RoomCameraDirector.ReleaseRoom(this);
            return;
        }

        RoomCameraDirector.RequestRoom(this, targetRoom);
    }

    private bool IsPlayer(Collider2D other)
    {
        return PlayerCameraColliderUtility.TryResolvePlayerTransform(other, playerTag, out _);
    }

#if UNITY_EDITOR
    internal void EditorAssign(RoomCameraVolume room, bool hold)
    {
        targetRoom = room;
        holdWhileInside = hold;
    }

    private void OnDrawGizmosSelected()
    {
        if (targetRoom == null)
        {
            return;
        }

        Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.9f);
        Gizmos.DrawLine(transform.position, targetRoom.AreaBounds.center);
    }
#endif
}
