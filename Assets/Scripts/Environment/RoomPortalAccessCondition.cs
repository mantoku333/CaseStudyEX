using UnityEngine;

/// <summary>
/// Optional access condition shared by camera preview and room fog reveal.
/// Portals without this component remain unrestricted.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Environment/Room Portal Access Condition")]
public sealed class RoomPortalAccessCondition : MonoBehaviour
{
    [Header("Required State")]
    [SerializeField] private ShutterWallBlockRise requiredWall;
    [SerializeField] private BossAreaController requiredBoss;

    public bool CanPreview(RoomCameraTrigger fromRoom, RoomCameraTrigger toRoom)
    {
        if (requiredWall != null &&
            (!requiredWall.IsOpen || requiredWall.IsTransitioning))
        {
            return false;
        }

        if (requiredBoss != null && !requiredBoss.IsEncounterCompleted)
        {
            return false;
        }

        return true;
    }

    public static bool AllowsPreview(
        Component portal,
        RoomCameraTrigger fromRoom,
        RoomCameraTrigger toRoom)
    {
        if (portal == null)
        {
            return false;
        }

        RoomPortalAccessCondition condition =
            portal.GetComponent<RoomPortalAccessCondition>();
        return condition == null || condition.CanPreview(fromRoom, toRoom);
    }
}
