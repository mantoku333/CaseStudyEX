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

    private static readonly List<RoomCameraSwitchPortal> activePortals = new();
    private readonly HashSet<Collider2D> overlappingPlayerColliders2D = new();
    private readonly HashSet<Collider> overlappingPlayerColliders = new();
    private readonly Collider2D[] polledColliderBuffer2D = new Collider2D[32];
    private readonly List<Collider2D> portalColliders2D = new();
    private ContactFilter2D overlapFilter2D;
    private bool activatedDuringCurrentOverlap;

    private void Awake()
    {
        overlapFilter2D = new ContactFilter2D
        {
            useTriggers = true
        };
        RefreshPortalColliders2D();
    }

    private void OnEnable()
    {
        if (!activePortals.Contains(this))
        {
            activePortals.Add(this);
        }

        RefreshPortalColliders2D();
    }

    private void OnDisable()
    {
        activePortals.Remove(this);
        overlappingPlayerColliders2D.Clear();
        overlappingPlayerColliders.Clear();
        activatedDuringCurrentOverlap = false;
    }

    private void FixedUpdate()
    {
        PollOverlappingPlayerColliders2D();
    }

    private void LateUpdate()
    {
        PollOverlappingPlayerColliders2D();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (PlayerCameraColliderUtility.TryResolvePlayerTransform(
                collision,
                playerTag,
                out Transform player))
        {
            HandlePlayerEntered(collision, player);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (PlayerCameraColliderUtility.TryResolvePlayerTransform(
                collision,
                playerTag,
                out Transform player))
        {
            HandlePlayerEntered(collision, player);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (PlayerCameraColliderUtility.TryResolvePlayerTransform(collision, playerTag, out _))
        {
            overlappingPlayerColliders2D.Remove(collision);
            ResetActivationWhenOutsidePortal();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        Collider2D playerCollider = collision.collider;
        if (playerCollider != null &&
            PlayerCameraColliderUtility.TryResolvePlayerTransform(
                playerCollider,
                playerTag,
                out Transform player))
        {
            HandlePlayerEntered(playerCollider, player);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        Collider2D playerCollider = collision.collider;
        if (playerCollider != null &&
            PlayerCameraColliderUtility.TryResolvePlayerTransform(
                playerCollider,
                playerTag,
                out Transform player))
        {
            HandlePlayerEntered(playerCollider, player);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        Collider2D playerCollider = collision.collider;
        if (playerCollider != null &&
            PlayerCameraColliderUtility.TryResolvePlayerTransform(playerCollider, playerTag, out _))
        {
            overlappingPlayerColliders2D.Remove(playerCollider);
            ResetActivationWhenOutsidePortal();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (PlayerCameraColliderUtility.TryResolvePlayerTransform(
                other,
                playerTag,
                out Transform player))
        {
            HandlePlayerEntered(other, player);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (PlayerCameraColliderUtility.TryResolvePlayerTransform(
                other,
                playerTag,
                out Transform player))
        {
            HandlePlayerEntered(other, player);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (PlayerCameraColliderUtility.TryResolvePlayerTransform(other, playerTag, out _))
        {
            overlappingPlayerColliders.Remove(other);
            ResetActivationWhenOutsidePortal();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Collider playerCollider = collision.collider;
        if (playerCollider != null &&
            PlayerCameraColliderUtility.TryResolvePlayerTransform(
                playerCollider,
                playerTag,
                out Transform player))
        {
            HandlePlayerEntered(playerCollider, player);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        Collider playerCollider = collision.collider;
        if (playerCollider != null &&
            PlayerCameraColliderUtility.TryResolvePlayerTransform(
                playerCollider,
                playerTag,
                out Transform player))
        {
            HandlePlayerEntered(playerCollider, player);
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        Collider playerCollider = collision.collider;
        if (playerCollider != null &&
            PlayerCameraColliderUtility.TryResolvePlayerTransform(playerCollider, playerTag, out _))
        {
            overlappingPlayerColliders.Remove(playerCollider);
            ResetActivationWhenOutsidePortal();
        }
    }

    private void HandlePlayerEntered(Collider2D playerCollider, Transform player)
    {
        overlappingPlayerColliders2D.Add(playerCollider);
        ActivateTargetRoomOncePerOverlap(player);
    }

    private void HandlePlayerEntered(Collider playerCollider, Transform player)
    {
        overlappingPlayerColliders.Add(playerCollider);
        ActivateTargetRoomOncePerOverlap(player);
    }

    private void PollOverlappingPlayerColliders2D()
    {
        if (targetRoom == null)
        {
            return;
        }

        if (portalColliders2D.Count == 0)
        {
            RefreshPortalColliders2D();
        }

        Transform overlappingPlayer = null;
        overlappingPlayerColliders2D.Clear();

        for (int i = 0; i < portalColliders2D.Count; i++)
        {
            Collider2D portalCollider = portalColliders2D[i];
            if (portalCollider == null || !portalCollider.enabled)
            {
                continue;
            }

            int overlapCount = portalCollider.Overlap(overlapFilter2D, polledColliderBuffer2D);

            for (int j = 0; j < overlapCount; j++)
            {
                Collider2D overlap = polledColliderBuffer2D[j];
                polledColliderBuffer2D[j] = null;
                if (overlap == null ||
                    !PlayerCameraColliderUtility.TryResolvePlayerTransform(
                        overlap,
                        playerTag,
                        out Transform player))
                {
                    continue;
                }

                overlappingPlayer ??= player;
                overlappingPlayerColliders2D.Add(overlap);
            }
        }

        if (overlappingPlayer != null)
        {
            ActivateTargetRoomOncePerOverlap(overlappingPlayer);
        }
        else if (overlappingPlayerColliders.Count == 0)
        {
            activatedDuringCurrentOverlap = false;
        }
    }

    public static bool TryActivateAtPlayerPosition(Transform player)
    {
        if (player == null)
        {
            return false;
        }

        bool activated = false;
        for (int i = activePortals.Count - 1; i >= 0; i--)
        {
            RoomCameraSwitchPortal portal = activePortals[i];
            if (portal == null)
            {
                activePortals.RemoveAt(i);
                continue;
            }

            if (!portal.isActiveAndEnabled)
            {
                continue;
            }

            if (portal.TryActivateIfPlayerOverlaps(player))
            {
                activated = true;
            }
        }

        return activated;
    }

    private bool TryActivateIfPlayerOverlaps(Transform player)
    {
        if (targetRoom == null || player == null)
        {
            return false;
        }

        RefreshPortalColliders2D();

        if (!PlayerCameraColliderUtility.TryFindCameraCollider(player, out Collider2D playerCameraCollider))
        {
            return false;
        }

        for (int i = 0; i < portalColliders2D.Count; i++)
        {
            Collider2D portalCollider = portalColliders2D[i];
            if (portalCollider == null || !portalCollider.enabled)
            {
                continue;
            }

            if (!portalCollider.bounds.Intersects(playerCameraCollider.bounds))
            {
                continue;
            }

            ActivateTargetRoomFromTeleport(player);
            return true;
        }

        return false;
    }

    private void ActivateTargetRoomFromTeleport(Transform player)
    {
        if (targetRoom == null || !CanActivateTargetRoom())
        {
            return;
        }

        activatedDuringCurrentOverlap = true;
        PreviewTargetRoom(player);
        if (RoomCameraTrigger.ActiveRoom == targetRoom)
        {
            return;
        }

        ActivateTargetRoom();
    }

    private void RefreshPortalColliders2D()
    {
        portalColliders2D.Clear();
        GetComponents(portalColliders2D);
    }

    private void ResetActivationWhenOutsidePortal()
    {
        if (overlappingPlayerColliders2D.Count == 0 &&
            overlappingPlayerColliders.Count == 0)
        {
            activatedDuringCurrentOverlap = false;
        }
    }

    private void ActivateTargetRoomOncePerOverlap(Transform player)
    {
        if (targetRoom == null ||
            activatedDuringCurrentOverlap ||
            !CanActivateTargetRoom())
        {
            return;
        }

        activatedDuringCurrentOverlap = true;
        PreviewTargetRoom(player);
        if (RoomCameraTrigger.ActiveRoom == targetRoom)
        {
            return;
        }

        ActivateTargetRoom();
    }

    private void ActivateTargetRoom()
    {
        if (targetRoom == null)
        {
            return;
        }

        targetRoom.ActivateCamera();
    }

    private void PreviewTargetRoom(Transform player)
    {
        Vector3 revealOrigin = player != null ? player.position : transform.position;
        if (player != null &&
            PlayerCameraColliderUtility.TryGetCameraPoint(player, out Vector3 cameraPoint))
        {
            revealOrigin = cameraPoint;
        }

        RoomFogRevealManager.PreviewRoomFromPortal(targetRoom, revealOrigin);
    }

    private bool CanActivateTargetRoom()
    {
        return RoomPortalAccessCondition.AllowsPreview(
            this,
            RoomCameraTrigger.ActiveRoom,
            targetRoom);
    }
}
