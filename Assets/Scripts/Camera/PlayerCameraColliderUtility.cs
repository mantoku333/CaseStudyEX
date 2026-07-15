using UnityEngine;

public static class PlayerCameraColliderUtility
{
    private const string CameraColliderName = "CameraCol";

    public static bool TryResolvePlayerTransform(Collider2D sourceCollider, string playerTag, out Transform player)
    {
        player = null;
        if (!IsCameraCollider(sourceCollider) ||
            !TryResolveTaggedParent(sourceCollider.transform, playerTag, out player))
        {
            return false;
        }

        return true;
    }

    public static bool TryResolvePlayerTransform(Collider sourceCollider, string playerTag, out Transform player)
    {
        player = null;
        if (!IsCameraCollider(sourceCollider) ||
            !TryResolveTaggedParent(sourceCollider.transform, playerTag, out player))
        {
            return false;
        }

        return true;
    }

    public static bool TryFindCameraCollider(Transform player, out Collider2D cameraCollider)
    {
        cameraCollider = null;
        if (player == null)
        {
            return false;
        }

        Collider2D[] colliders = player.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (IsCameraCollider(collider))
            {
                cameraCollider = collider;
                return true;
            }
        }

        return false;
    }

    public static bool TryGetCameraBounds(Transform player, out Bounds bounds)
    {
        if (TryFindCameraCollider(player, out Collider2D cameraCollider))
        {
            bounds = cameraCollider.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    public static bool TryGetCameraPoint(Transform player, out Vector3 point)
    {
        if (TryGetCameraBounds(player, out Bounds bounds))
        {
            point = bounds.center;
            return true;
        }

        point = default;
        return false;
    }

    public static bool TryGetCameraPointFromPlayerTag(string playerTag, out Vector3 point)
    {
        point = default;
        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return false;
        }

        try
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObject == null)
            {
                return false;
            }

            return TryGetCameraPoint(playerObject.transform, out point);
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private static bool IsCameraCollider(Collider2D collider)
    {
        return collider != null &&
               collider.enabled &&
               collider.gameObject.name == CameraColliderName;
    }

    private static bool IsCameraCollider(Collider collider)
    {
        return collider != null &&
               collider.enabled &&
               collider.gameObject.name == CameraColliderName;
    }

    private static bool TryResolveTaggedParent(Transform source, string playerTag, out Transform player)
    {
        player = null;
        if (source == null || string.IsNullOrWhiteSpace(playerTag))
        {
            return false;
        }

        Transform current = source;
        while (current != null)
        {
            if (current.CompareTag(playerTag))
            {
                player = current;
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
