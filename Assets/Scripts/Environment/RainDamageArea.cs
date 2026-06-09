using Metroidvania.Player;
using Player;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Environment/Rain Damage Area 2D")]
public sealed class RainDamageArea : MonoBehaviour
{
    private const int HitCapacity = 32;
    private const int RainPathHitCapacity = 32;
    private const string GroundLayerName = "Ground";

    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField, Min(0f)] private float damageIntervalSeconds = 1f;
    [SerializeField] private string playerTag = "Player";
    // 雨エリア内でも、頭上まで雨が届かない場所ではダメージを受けないようにする。
    [SerializeField] private bool requireClearRainPath = true;
    [SerializeField] private LayerMask rainBlockLayerMask;
    [SerializeField] private bool includeShutterWallBlockers = true;
    [SerializeField, Min(0f)] private float rainPathProbeInset = 0.05f;
    [SerializeField] private Color gizmoColor = new Color(0.2f, 0.55f, 1f, 0.25f);

    private readonly Collider2D[] overlapHits = new Collider2D[HitCapacity];
    private readonly RaycastHit2D[] rainPathHits = new RaycastHit2D[RainPathHitCapacity];
    private ContactFilter2D overlapFilter;
    private ContactFilter2D rainPathFilter;
    private BoxCollider2D areaCollider;
    private int cachedRainPathMaskValue = int.MinValue;
    private bool isRainActive;

    public bool IsRainActive => isRainActive;

    public void SetRainActive(bool active)
    {
        isRainActive = active;
    }

    private void Reset()
    {
        EnsureTriggerCollider();
        EnsureRainBlockLayerMask();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        EnsureRainBlockLayerMask();
        BuildOverlapFilter();
        RebuildRainPathFilterIfNeeded();
    }

    private void FixedUpdate()
    {
        if (!isRainActive)
        {
            return;
        }

        DamagePlayersInArea();
    }

    private void DamagePlayersInArea()
    {
        if (areaCollider == null || !areaCollider.enabled)
        {
            return;
        }

        int hitCount = areaCollider.Overlap(overlapFilter, overlapHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = overlapHits[i];
            overlapHits[i] = null;
            TryDamagePlayer(hit);
        }
    }

    private void TryDamagePlayer(Collider2D other)
    {
        if (!TryResolvePlayer(
                other,
                out PlayerHealth playerHealth,
                out PlayerDamageFlash playerDamageFlash,
                out UmbrellaController umbrellaController,
                out Collider2D bodyCollider))
        {
            return;
        }

        if (!CanRainReachPlayer(bodyCollider, playerHealth))
        {
            return;
        }

        if (IsProtectedByUmbrella(umbrellaController))
        {
            return;
        }

        if (playerHealth.TryTakeDamage(damage, damageIntervalSeconds))
        {
            playerDamageFlash?.PlayFlashForced();
        }
    }

    private bool TryResolvePlayer(
        Collider2D candidate,
        out PlayerHealth playerHealth,
        out PlayerDamageFlash playerDamageFlash,
        out UmbrellaController umbrellaController,
        out Collider2D bodyCollider)
    {
        playerHealth = null;
        playerDamageFlash = null;
        umbrellaController = null;
        bodyCollider = null;

        if (!PlayerBodyColliderUtility.TryGetPlayerBodyFromCollider(candidate, out playerHealth, out bodyCollider))
        {
            return false;
        }

        if (!MatchesPlayerTag(bodyCollider.gameObject, playerHealth))
        {
            return false;
        }

        playerDamageFlash = ResolvePlayerDamageFlash(bodyCollider, playerHealth);
        umbrellaController = ResolveUmbrellaController(bodyCollider, playerHealth);
        return true;
    }

    private bool CanRainReachPlayer(Collider2D playerCollider, PlayerHealth playerHealth)
    {
        if (!requireClearRainPath || areaCollider == null || playerCollider == null)
        {
            return true;
        }

        Bounds areaBounds = areaCollider.bounds;
        Bounds playerBounds = playerCollider.bounds;
        float rainStartY = areaBounds.max.y;
        float playerHeadY = playerBounds.max.y;
        float probeDistance = rainStartY - playerHeadY;
        if (probeDistance <= Mathf.Epsilon)
        {
            return true;
        }

        float centerX = playerBounds.center.x;
        float probeInset = Mathf.Max(0f, rainPathProbeInset);
        float leftX = Mathf.Min(centerX, playerBounds.min.x + probeInset);
        float rightX = Mathf.Max(centerX, playerBounds.max.x - probeInset);

        // 体の左・中央・右のどこか一箇所でも空いていれば、雨が当たっている扱いにする。
        if (!IsRainPathBlocked(new Vector2(leftX, rainStartY), probeDistance, playerCollider, playerHealth) ||
            !IsRainPathBlocked(new Vector2(centerX, rainStartY), probeDistance, playerCollider, playerHealth) ||
            !IsRainPathBlocked(new Vector2(rightX, rainStartY), probeDistance, playerCollider, playerHealth))
        {
            return true;
        }

        return false;
    }

    private bool IsRainPathBlocked(
        Vector2 origin,
        float distance,
        Collider2D playerCollider,
        PlayerHealth playerHealth)
    {
        EnsureRainBlockLayerMask();
        RebuildRainPathFilterIfNeeded();

        int hitCount = Physics2D.Raycast(origin, Vector2.down, rainPathFilter, rainPathHits, distance);
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = rainPathHits[i];
            rainPathHits[i] = default;

            if (IsBlockingRainPathHit(hit, playerCollider, playerHealth))
            {
                ClearRainPathHits(0);
                return true;
            }
        }

        ClearRainPathHits(hitCount);
        return false;
    }

    private bool IsBlockingRainPathHit(
        RaycastHit2D hit,
        Collider2D playerCollider,
        PlayerHealth playerHealth)
    {
        Collider2D hitCollider = hit.collider;
        if (hitCollider == null ||
            hitCollider == areaCollider ||
            hitCollider == playerCollider ||
            hitCollider.isTrigger ||
            IsPlayerOwnedCollider(hitCollider, playerCollider, playerHealth))
        {
            return false;
        }

        // 通常の地形はレイヤーで、シャッター壁は既存コンポーネントで雨よけとして扱う。
        bool isConfiguredRainBlocker = IsInLayerMask(hitCollider.gameObject.layer, rainBlockLayerMask);
        bool isShutterWallBlocker =
            includeShutterWallBlockers &&
            hitCollider.GetComponentInParent<ShutterWallBlockRise>() != null;

        return isConfiguredRainBlocker || isShutterWallBlocker;
    }

    private static bool IsPlayerOwnedCollider(
        Collider2D hitCollider,
        Collider2D playerCollider,
        PlayerHealth playerHealth)
    {
        if (hitCollider == null)
        {
            return false;
        }

        Rigidbody2D playerRigidbody = playerCollider != null ? playerCollider.attachedRigidbody : null;
        if (playerRigidbody != null && hitCollider.attachedRigidbody == playerRigidbody)
        {
            return true;
        }

        PlayerHealth hitPlayerHealth = hitCollider.GetComponentInParent<PlayerHealth>();
        return hitPlayerHealth != null && hitPlayerHealth == playerHealth;
    }

    private static bool IsProtectedByUmbrella(UmbrellaController umbrellaController)
    {
        return umbrellaController != null &&
            umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Open;
    }

    private static PlayerDamageFlash ResolvePlayerDamageFlash(Collider2D playerCollider, PlayerHealth playerHealth)
    {
        PlayerDamageFlash damageFlash = playerCollider.GetComponent<PlayerDamageFlash>();
        if (damageFlash != null)
        {
            return damageFlash;
        }

        damageFlash = playerCollider.GetComponentInParent<PlayerDamageFlash>();
        if (damageFlash != null)
        {
            return damageFlash;
        }

        damageFlash = playerHealth.GetComponent<PlayerDamageFlash>();
        if (damageFlash != null)
        {
            return damageFlash;
        }

        damageFlash = playerHealth.GetComponentInParent<PlayerDamageFlash>();
        if (damageFlash != null)
        {
            return damageFlash;
        }

        return playerHealth.GetComponentInChildren<PlayerDamageFlash>(true);
    }

    private static UmbrellaController ResolveUmbrellaController(Collider2D playerCollider, PlayerHealth playerHealth)
    {
        UmbrellaController umbrellaController = playerCollider.GetComponent<UmbrellaController>();
        if (umbrellaController != null)
        {
            return umbrellaController;
        }

        umbrellaController = playerCollider.GetComponentInParent<UmbrellaController>();
        if (umbrellaController != null)
        {
            return umbrellaController;
        }

        umbrellaController = playerHealth.GetComponent<UmbrellaController>();
        if (umbrellaController != null)
        {
            return umbrellaController;
        }

        umbrellaController = playerHealth.GetComponentInChildren<UmbrellaController>(true);
        if (umbrellaController != null)
        {
            return umbrellaController;
        }

        Transform playerRoot = playerHealth.transform.root;
        return playerRoot != null
            ? playerRoot.GetComponentInChildren<UmbrellaController>(true)
            : null;
    }

    private bool MatchesPlayerTag(GameObject hitObject, PlayerHealth playerHealth)
    {
        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return true;
        }

        if (hitObject != null && hitObject.CompareTag(playerTag))
        {
            return true;
        }

        if (playerHealth.CompareTag(playerTag))
        {
            return true;
        }

        return playerHealth.transform.root != null && playerHealth.transform.root.CompareTag(playerTag);
    }

    private void EnsureTriggerCollider()
    {
        if (areaCollider == null)
        {
            areaCollider = GetComponent<BoxCollider2D>();
        }

        if (areaCollider != null)
        {
            areaCollider.isTrigger = true;
        }
    }

    private void BuildOverlapFilter()
    {
        overlapFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        overlapFilter.SetLayerMask(PlayerBodyColliderUtility.GetPlayerBodyLayerMask());
    }

    private void EnsureRainBlockLayerMask()
    {
        if (rainBlockLayerMask.value != 0)
        {
            return;
        }

        int groundLayer = LayerMask.NameToLayer(GroundLayerName);
        // 未設定時は Ground を使うことで、既存シーンのInspector変更なしで遮蔽物判定を有効にする。
        rainBlockLayerMask = groundLayer >= 0 ? 1 << groundLayer : Physics2D.DefaultRaycastLayers;
        cachedRainPathMaskValue = int.MinValue;
    }

    private void RebuildRainPathFilterIfNeeded()
    {
        // シャッター壁は Default レイヤーの場合があるため、必要な時だけ全レイヤーを探索する。
        LayerMask probeMask = includeShutterWallBlockers ? Physics2D.AllLayers : rainBlockLayerMask;
        if (cachedRainPathMaskValue == probeMask.value)
        {
            return;
        }

        rainPathFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = false
        };
        rainPathFilter.SetLayerMask(probeMask);
        cachedRainPathMaskValue = probeMask.value;
    }

    private void ClearRainPathHits(int usedCount)
    {
        for (int i = usedCount; i < rainPathHits.Length; i++)
        {
            rainPathHits[i] = default;
        }
    }

    private static bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        damage = Mathf.Max(1, damage);
        damageIntervalSeconds = Mathf.Max(0f, damageIntervalSeconds);
        rainPathProbeInset = Mathf.Max(0f, rainPathProbeInset);
        EnsureTriggerCollider();
        EnsureRainBlockLayerMask();
        BuildOverlapFilter();
        RebuildRainPathFilterIfNeeded();
    }

    private void OnDrawGizmosSelected()
    {
        EnsureTriggerCollider();
        if (areaCollider == null)
        {
            return;
        }

        Bounds bounds = areaCollider.bounds;
        Color previousColor = Gizmos.color;
        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(bounds.center, bounds.size);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
        Gizmos.color = previousColor;
    }
#endif
}
