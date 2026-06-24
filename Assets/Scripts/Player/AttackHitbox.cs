using System;
using System.Collections.Generic;
using Player;
using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    private const string GroundLayerName = "Ground";
    private const string FallThroughFloorLayerName = "FallThroughFloor";
    private readonly HashSet<MonoBehaviour> hitReceivers = new HashSet<MonoBehaviour>();
    private readonly List<MonoBehaviour> receiverLookupBuffer = new List<MonoBehaviour>();
    private readonly Collider2D[] overlapResults = new Collider2D[16];
    private readonly RaycastHit2D[] wallProbeResults = new RaycastHit2D[16];

    [Header("Wall Blocking")]
    [SerializeField] private bool blockHitsThroughVerticalWalls = true;
    [SerializeField] private LayerMask verticalWallLayerMask;
    [SerializeField] private bool includeShutterWalls = true;
    [SerializeField, Range(0f, 1f)] private float verticalWallNormalMinX = 0.5f;

    private Collider2D hitboxCollider;
    private Rigidbody2D ownerRigidbody;
    private Collider2D ownerBodyCollider;
    private readonly List<Collider2D> ownerColliders = new List<Collider2D>();
    private ContactFilter2D overlapFilter;
    private ContactFilter2D wallProbeFilter;
    private int cachedWallMaskValue = int.MinValue;
    private int fallThroughFloorLayer = -1;
    private PlayerStatsData statsData;
    private PlayerAttackPower attackPower;
    private PlayerEquipmentController equipmentController;

    public event Action<Collider2D> OnHit;

    public int PlayerAttackDamage
    {
        get
        {
            int baseDamage = ResolveBaseAttackDamage();
            if (baseDamage <= 0)
            {
                return 0;
            }

            float multiplier = GetEquipmentAttackMultiplier();
            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
        }
    }

    public Vector2 AttackOriginPosition => ResolveAttackOrigin();

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider2D>();
        CacheOwnerColliders();
        EnsureWallLayerMask();
        RebuildWallProbeFilterIfNeeded();
        fallThroughFloorLayer = LayerMask.NameToLayer(FallThroughFloorLayerName);

        overlapFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        overlapFilter.SetLayerMask(Physics2D.AllLayers);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (verticalWallLayerMask.value == 0)
        {
            verticalWallLayerMask = BuildDefaultVerticalWallLayerMask();
        }

        verticalWallNormalMinX = Mathf.Clamp01(verticalWallNormalMinX);
        cachedWallMaskValue = int.MinValue;
    }
#endif

    public void ResetHitState()
    {
        hitReceivers.Clear();
    }

    public void SetPlayerStatsData(PlayerStatsData playerData)
    {
        statsData = playerData;
    }

    private int ResolveBaseAttackDamage()
    {
        if (attackPower == null)
        {
            attackPower = GetComponentInParent<PlayerAttackPower>();
        }

        if (attackPower != null)
        {
            return attackPower.AttackDamage;
        }

        return statsData != null ? statsData.PlayerAttackDamage : 0;
    }

    private float GetEquipmentAttackMultiplier()
    {
        if (equipmentController == null)
        {
            equipmentController = GetComponentInParent<PlayerEquipmentController>();
        }

        return equipmentController != null
            ? Mathf.Max(0f, equipmentController.AttackPowerMultiplier)
            : 1f;
    }

    public void ScanCurrentOverlaps()
    {
        if (hitboxCollider == null || !hitboxCollider.enabled)
        {
            return;
        }

        int overlapCount = hitboxCollider.Overlap(overlapFilter, overlapResults);
        for (int i = 0; i < overlapCount; i++)
        {
            ProcessHit(overlapResults[i]);
            overlapResults[i] = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        ProcessHit(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        ProcessHit(collision);
    }

    private void ProcessHit(Collider2D collision)
    {
        if (collision == null)
        {
            return;
        }

        if (IsHitBlockedByVerticalWall(collision))
        {
            return;
        }

        receiverLookupBuffer.Clear();
        collision.GetComponentsInParent(false, receiverLookupBuffer);

        for (int i = 0; i < receiverLookupBuffer.Count; i++)
        {
            MonoBehaviour behaviour = receiverLookupBuffer[i];
            if (behaviour is IAttackReceiver receiver && hitReceivers.Add(behaviour))
            {
                receiver.OnAttacked(this, collision);
                OnHit?.Invoke(collision);
            }
        }

        receiverLookupBuffer.Clear();
    }

    private bool IsHitBlockedByVerticalWall(Collider2D targetCollider)
    {
        if (!blockHitsThroughVerticalWalls || targetCollider == null)
        {
            return false;
        }

        Vector2 origin = ResolveAttackOrigin();
        Vector2 target = targetCollider.bounds.center;
        Vector2 direction = target - origin;
        float distance = direction.magnitude;
        if (distance <= Mathf.Epsilon)
        {
            return false;
        }

        EnsureWallLayerMask();
        RebuildWallProbeFilterIfNeeded();

        int hitCount = Physics2D.Raycast(origin, direction / distance, wallProbeFilter, wallProbeResults, distance);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = wallProbeResults[i];
            wallProbeResults[i] = default;

            if (IsBlockingVerticalWallHit(hit, targetCollider))
            {
                ClearWallProbeResults(0);
                return true;
            }
        }

        ClearWallProbeResults(hitCount);
        return false;
    }

    private Vector2 ResolveAttackOrigin()
    {
        if (ownerBodyCollider != null)
        {
            return ownerBodyCollider.bounds.center;
        }

        if (ownerRigidbody != null)
        {
            return ownerRigidbody.position;
        }

        return transform.position;
    }

    private bool IsBlockingVerticalWallHit(RaycastHit2D hit, Collider2D targetCollider)
    {
        Collider2D hitCollider = hit.collider;
        if (hitCollider == null ||
            hitCollider == hitboxCollider ||
            hitCollider == targetCollider ||
            hitCollider.isTrigger ||
            IsOwnerCollider(hitCollider) ||
            IsFallThroughFloor(hitCollider) ||
            hitCollider.GetComponent<PlatformEffector2D>() != null ||
            hitCollider.GetComponentInParent<PlatformEffector2D>() != null)
        {
            return false;
        }

        bool isConfiguredLayerWall = IsInLayerMask(hitCollider.gameObject.layer, verticalWallLayerMask);
        bool isShutterWall = includeShutterWalls && hitCollider.GetComponentInParent<ShutterWallBlockRise>() != null;
        if (!isConfiguredLayerWall && !isShutterWall)
        {
            return false;
        }

        return Mathf.Abs(hit.normal.x) >= verticalWallNormalMinX;
    }

    private void CacheOwnerColliders()
    {
        ownerRigidbody = GetComponentInParent<Rigidbody2D>();
        if (ownerRigidbody == null)
        {
            return;
        }

        ownerColliders.Clear();
        ownerRigidbody.GetComponentsInChildren(true, ownerColliders);
        for (int i = 0; i < ownerColliders.Count; i++)
        {
            Collider2D candidate = ownerColliders[i];
            if (candidate != null && candidate.enabled && !candidate.isTrigger && candidate.attachedRigidbody == ownerRigidbody)
            {
                ownerBodyCollider = candidate;
                return;
            }
        }
    }

    private bool IsOwnerCollider(Collider2D candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate.attachedRigidbody != null && candidate.attachedRigidbody == ownerRigidbody)
        {
            return true;
        }

        for (int i = 0; i < ownerColliders.Count; i++)
        {
            if (ownerColliders[i] == candidate)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsFallThroughFloor(Collider2D candidate)
    {
        return fallThroughFloorLayer >= 0 && candidate != null && candidate.gameObject.layer == fallThroughFloorLayer;
    }

    private void EnsureWallLayerMask()
    {
        if (verticalWallLayerMask.value == 0)
        {
            verticalWallLayerMask = BuildDefaultVerticalWallLayerMask();
        }
    }

    private void RebuildWallProbeFilterIfNeeded()
    {
        LayerMask wallMask = includeShutterWalls ? Physics2D.AllLayers : verticalWallLayerMask;
        if (cachedWallMaskValue == wallMask.value)
        {
            return;
        }

        wallProbeFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = false
        };
        wallProbeFilter.SetLayerMask(wallMask);
        cachedWallMaskValue = wallMask.value;
    }

    private static LayerMask BuildDefaultVerticalWallLayerMask()
    {
        int groundLayer = LayerMask.NameToLayer(GroundLayerName);
        return groundLayer >= 0 ? 1 << groundLayer : Physics2D.DefaultRaycastLayers;
    }

    private static bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    private void ClearWallProbeResults(int usedCount)
    {
        for (int i = usedCount; i < wallProbeResults.Length; i++)
        {
            wallProbeResults[i] = default;
        }
    }
}
