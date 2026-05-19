using Metroidvania.Player;
using Player;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class DamageFloor : MonoBehaviour
{
    private const int TopSensorHitCapacity = 32;
    private const float MinTopSensorHeight = 0.04f;

    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Min(0f)] private float topContactTolerance = 0.08f;

    private readonly Collider2D[] topSensorHits = new Collider2D[TopSensorHitCapacity];
    private ContactFilter2D topSensorContactFilter;
    private Collider2D floorCollider;

    private void Awake()
    {
        floorCollider = GetComponent<Collider2D>();
        BuildTopSensorContactFilter();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 着地した瞬間にも反応できるよう、衝突情報から上面接触だけを即時判定する。
        TryDamagePlayerFromCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // 動いている間は衝突情報でも判定するが、静止中の継続ダメージは FixedUpdate の上面センサーに任せる。
        TryDamagePlayerFromCollision(collision);
    }

    private void FixedUpdate()
    {
        // Rigidbody2D が寝て OnCollisionStay2D が安定しない場合でも、上面に立っている間は毎物理フレーム確認する。
        DamagePlayersInTopSensor();
    }

    private void DamagePlayersInTopSensor()
    {
        if (floorCollider == null || !floorCollider.enabled)
        {
            return;
        }

        Bounds floorBounds = floorCollider.bounds;
        float tolerance = Mathf.Max(0f, topContactTolerance);
        float contactOffset = Mathf.Max(0f, Physics2D.defaultContactOffset);
        float sensorHeight = Mathf.Max(MinTopSensorHeight, Mathf.Max(tolerance * 2f, contactOffset * 2f));

        // 床の上面をまたぐ薄い箱で、プレイヤー本体コライダーだけを探す。
        Vector2 sensorCenter = new Vector2(
            floorBounds.center.x,
            floorBounds.max.y + (sensorHeight * 0.5f) - tolerance);
        Vector2 sensorSize = new Vector2(
            floorBounds.size.x + (tolerance * 2f),
            sensorHeight);

        int hitCount = Physics2D.OverlapBox(sensorCenter, sensorSize, 0f, topSensorContactFilter, topSensorHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D candidate = topSensorHits[i];
            topSensorHits[i] = null;

            if (!TryResolvePlayerCollider(
                    candidate,
                    out PlayerHealth playerHealth,
                    out PlayerDamageFlash playerDamageFlash,
                    out Collider2D playerCollider))
            {
                continue;
            }

            if (!IsPlayerBodyStandingOnTop(playerCollider, floorCollider))
            {
                continue;
            }

            TryDamagePlayer(playerHealth, playerDamageFlash);
        }
    }

    private void TryDamagePlayerFromCollision(Collision2D collision)
    {
        if (!TryGetPlayerContact(
                collision,
                out PlayerHealth playerHealth,
                out PlayerDamageFlash playerDamageFlash,
                out Collider2D playerCollider,
                out Collider2D hitFloorCollider))
        {
            return;
        }

        if (!IsTopContact(collision, playerCollider, hitFloorCollider))
        {
            return;
        }

        TryDamagePlayer(playerHealth, playerDamageFlash);
    }

    private void TryDamagePlayer(PlayerHealth playerHealth, PlayerDamageFlash playerDamageFlash)
    {
        if (playerHealth == null)
        {
            return;
        }

        if (playerHealth.TryTakeDamage(damage))
        {
            // 実際に HP が減った時だけ強制点滅し、点滅側の古いクールダウンで表示が止まらないようにする。
            playerDamageFlash?.PlayFlashForced();
        }
    }

    private bool IsPlayerBodyStandingOnTop(Collider2D playerCollider, Collider2D hitFloorCollider)
    {
        if (playerCollider == null || hitFloorCollider == null)
        {
            return false;
        }

        Bounds floorBounds = hitFloorCollider.bounds;
        Bounds playerBounds = playerCollider.bounds;
        float tolerance = Mathf.Max(0f, topContactTolerance);
        float allowedGap = Mathf.Max(tolerance, Physics2D.defaultContactOffset * 2f);
        float floorTop = floorBounds.max.y;
        float playerBottom = playerBounds.min.y;

        // プレイヤー本体の下端が床上面より下に潜っている場合は、横や下からの接触として扱う。
        if (playerBottom < floorTop - tolerance)
        {
            return false;
        }

        // 上面から離れすぎている場合は、ジャンプ中や通過中として扱う。
        if (playerBottom > floorTop + allowedGap)
        {
            return false;
        }

        float horizontalOverlap =
            Mathf.Min(playerBounds.max.x, floorBounds.max.x) -
            Mathf.Max(playerBounds.min.x, floorBounds.min.x);
        if (horizontalOverlap <= 0f)
        {
            return false;
        }

        // Bounds だけでは接触オフセットを誤判定しやすいため、実際のコライダー距離でも確認する。
        ColliderDistance2D distance = playerCollider.Distance(hitFloorCollider);
        return distance.isOverlapped || distance.distance <= allowedGap;
    }

    private bool TryGetPlayerContact(
        Collision2D collision,
        out PlayerHealth playerHealth,
        out PlayerDamageFlash playerDamageFlash,
        out Collider2D playerCollider,
        out Collider2D hitFloorCollider)
    {
        playerHealth = null;
        playerDamageFlash = null;
        playerCollider = null;
        hitFloorCollider = floorCollider;

        if (hitFloorCollider == null)
        {
            return false;
        }

        return TryResolvePlayerCollider(collision.collider, out playerHealth, out playerDamageFlash, out playerCollider)
            || TryResolvePlayerCollider(collision.otherCollider, out playerHealth, out playerDamageFlash, out playerCollider);
    }

    private bool TryResolvePlayerCollider(
        Collider2D candidate,
        out PlayerHealth playerHealth,
        out PlayerDamageFlash playerDamageFlash,
        out Collider2D playerCollider)
    {
        playerHealth = null;
        playerDamageFlash = null;
        playerCollider = null;

        if (candidate == null || candidate == floorCollider)
        {
            return false;
        }

        // 傘や攻撃判定などの子コライダーではなく、PlayerHealth と同じ GameObject の本体コライダーだけを対象にする。
        if (!PlayerBodyColliderUtility.TryGetPlayerBodyFromCollider(candidate, out playerHealth, out playerCollider))
        {
            return false;
        }

        if (!MatchesPlayerTag(playerCollider.gameObject, playerHealth))
        {
            return false;
        }

        playerDamageFlash = ResolvePlayerDamageFlash(playerCollider, playerHealth);
        return true;
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

    private bool MatchesPlayerTag(GameObject hitObject, PlayerHealth playerHealth)
    {
        if (string.IsNullOrEmpty(playerTag))
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

    private bool IsTopContact(Collision2D collision, Collider2D playerCollider, Collider2D hitFloorCollider)
    {
        Bounds floorBounds = hitFloorCollider.bounds;
        Bounds playerBounds = playerCollider.bounds;
        float tolerance = Mathf.Max(0f, topContactTolerance);
        float floorTop = floorBounds.max.y;

        if (playerBounds.min.y < floorTop - tolerance)
        {
            return false;
        }

        // 横や下から触れた接触点を除外し、床の上面付近に接触点がある場合だけダメージを許可する。
        float minX = floorBounds.min.x - tolerance;
        float maxX = floorBounds.max.x + tolerance;

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector2 point = collision.GetContact(i).point;
            if (point.x < minX || point.x > maxX)
            {
                continue;
            }

            if (Mathf.Abs(point.y - floorTop) <= tolerance)
            {
                return true;
            }
        }

        return false;
    }

    private void BuildTopSensorContactFilter()
    {
        // レイヤー設定に依存せず拾い、あとでプレイヤー本体コライダーかどうかを厳密に絞り込む。
        topSensorContactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        topSensorContactFilter.SetLayerMask(Physics2D.AllLayers);
    }

#if UNITY_EDITOR
    private void Reset()
    {
        Collider2D targetCollider = GetComponent<Collider2D>();
        if (targetCollider != null)
        {
            targetCollider.isTrigger = false;
        }
    }

    private void OnValidate()
    {
        damage = Mathf.Max(1, damage);
        topContactTolerance = Mathf.Max(0f, topContactTolerance);
        BuildTopSensorContactFilter();
    }
#endif
}
