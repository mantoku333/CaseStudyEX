using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// WindRise のトリガー内で、傘を開いている空中のプレイヤーを上昇させ、
/// コライダー上端付近でふわふわ浮かせるための風エリア制御。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WindRiseZone : MonoBehaviour
{
    [Header("Target")]
    // 風の影響を受ける対象タグ。通常は Player のみ。
    [SerializeField] private string playerTag = "Player";
    // true の場合、傘が開いているときだけ上昇/浮遊させる。
    [SerializeField] private bool requireUmbrellaOpen = true;
    // true の場合、地面に立っているプレイヤーには風を当てない。
    [SerializeField] private bool requireAirborne = true;

    [Header("Rise")]
    // 中央〜上部へ向かうときの上向き加速度。
    [SerializeField, Min(0f)] private float upwardAcceleration = 35f;
    // 上昇速度の上限。大きくすると一気に上まで運ばれる。
    [SerializeField, Min(0f)] private float maxRiseSpeed = 8f;

    [Header("Hover")]
    // コライダー上端から少し下げた位置を浮遊目標にするためのオフセット。
    [SerializeField, Min(0f)] private float hoverOffsetFromTop = 0.25f;
    // 目標位置に近づいたら上昇処理から浮遊処理へ切り替える範囲。
    [SerializeField, Min(0.01f)] private float hoverBandHeight = 0.5f;
    // 目標位置へ戻ろうとする力。大きいほど上端付近へ強く引き戻す。
    [SerializeField, Min(0f)] private float hoverSpring = 18f;
    // 浮遊中の速度を抑える減衰。大きいほど上下の揺れが落ち着く。
    [SerializeField, Min(0f)] private float hoverDamping = 6f;
    // 浮遊中の上下揺れの大きさ。
    [SerializeField, Min(0f)] private float bobAmplitude = 0.2f;
    // 浮遊中の上下揺れの速さ。
    [SerializeField, Min(0f)] private float bobFrequency = 1.5f;
    [SerializeField, Min(0f)] private float recoilRiseWindIgnoreSeconds = 0.6f;
    [SerializeField, Min(0f)] private float recoilRiseMinUpwardSpeed = 0.05f;

    // All zones share one player state and one velocity write per physics step.
    // In particular, recoil suppression must survive a handoff to another zone.
    private static readonly HashSet<WindRiseZone> activeZones = new HashSet<WindRiseZone>();
    private static readonly Dictionary<Rigidbody2D, PlayerWindTarget> targets = new Dictionary<Rigidbody2D, PlayerWindTarget>();
    private static readonly List<Rigidbody2D> staleTargets = new List<Rigidbody2D>();
    private static readonly List<WindRiseZone> sortedZones = new List<WindRiseZone>();
    private static readonly List<WindRiseZone> reachableZones = new List<WindRiseZone>();
    private static double lastResolvedFixedTime = double.NegativeInfinity;

    private Collider2D windCollider;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetWindState()
    {
        targets.Clear();
        staleTargets.Clear();
        sortedZones.Clear();
        reachableZones.Clear();
        lastResolvedFixedTime = double.NegativeInfinity;
        // Retain live registrations when both domain and scene reload are disabled.
        activeZones.RemoveWhere(zone => zone == null || !zone.isActiveAndEnabled);
    }

    private void Awake()
    {
        windCollider = GetComponent<Collider2D>();
        if (windCollider != null)
        {
            // WindRise は足場ではなく風の判定なので、必ずトリガーとして扱う。
            windCollider.isTrigger = true;
        }
    }

    private void OnEnable()
    {
        windCollider = GetComponent<Collider2D>();
        activeZones.Add(this);
    }

    private void OnValidate()
    {
        // Inspector から不正な値が入っても、物理挙動が壊れないように丸める。
        upwardAcceleration = Mathf.Max(0f, upwardAcceleration);
        maxRiseSpeed = Mathf.Max(0f, maxRiseSpeed);
        hoverOffsetFromTop = Mathf.Max(0f, hoverOffsetFromTop);
        hoverBandHeight = Mathf.Max(0.01f, hoverBandHeight);
        hoverSpring = Mathf.Max(0f, hoverSpring);
        hoverDamping = Mathf.Max(0f, hoverDamping);
        bobAmplitude = Mathf.Max(0f, bobAmplitude);
        bobFrequency = Mathf.Max(0f, bobFrequency);
        recoilRiseWindIgnoreSeconds = Mathf.Max(0f, recoilRiseWindIgnoreSeconds);
        recoilRiseMinUpwardSpeed = Mathf.Max(0f, recoilRiseMinUpwardSpeed);

        Collider2D targetCollider = GetComponent<Collider2D>();
        if (targetCollider != null)
        {
            // 編集中に collider を戻してしまっても、WindRise は常にトリガーにする。
            targetCollider.isTrigger = true;
        }
    }

    private void FixedUpdate()
    {
        if (lastResolvedFixedTime == Time.fixedTimeAsDouble)
        {
            return;
        }

        lastResolvedFixedTime = Time.fixedTimeAsDouble;
        ResolveWindStep();
    }

    private static void ResolveWindStep()
    {
        sortedZones.Clear();
        foreach (WindRiseZone zone in activeZones)
        {
            if (zone != null && zone.IsZoneActive()) sortedZones.Add(zone);
        }
        sortedZones.Sort(CompareZonePriority);
        staleTargets.Clear();

        foreach (KeyValuePair<Rigidbody2D, PlayerWindTarget> pair in targets)
        {
            Rigidbody2D body = pair.Key;
            PlayerWindTarget target = pair.Value;

            if (body == null || !body.simulated || !body.gameObject.activeInHierarchy ||
                target.BodyCollider == null || !target.BodyCollider.isActiveAndEnabled)
            {
                staleTargets.Add(body);
                continue;
            }

            WindRiseZone controllingZone = ResolveControllingZone(target, out bool hasOverlap);
            if (!hasOverlap)
            {
                // Validate geometry every step: trigger exits are not guaranteed
                // when a collider, player, or zone is disabled or destroyed.
                staleTargets.Add(body);
            }
            else if (controllingZone != null && controllingZone.CanApplyWind(target))
            {
                controllingZone.ApplyWind(target);
            }
        }

        for (int i = 0; i < staleTargets.Count; i++)
        {
            targets.Remove(staleTargets[i]);
        }
    }

    private static WindRiseZone ResolveControllingZone(PlayerWindTarget target, out bool hasOverlap)
    {
        hasOverlap = false;
        reachableZones.Clear();
        Bounds playerBounds = target.BodyCollider.bounds;
        PhysicsScene2D physicsScene = target.Rigidbody.gameObject.scene.GetPhysicsScene2D();

        // Ascending world-space tops form an acyclic path through any number of
        // touching/overlapping zones. A path always starts at a real body overlap.
        foreach (WindRiseZone zone in sortedZones)
        {
            if (zone.gameObject.scene.GetPhysicsScene2D() != physicsScene ||
                !zone.MatchesPlayer(target.Rigidbody)) continue;

            bool reachable = CollidersOverlap(zone.windCollider, target.BodyCollider);
            hasOverlap |= reachable;
            if (!zone.IsEligibleTarget(target)) continue;

            Bounds bounds = zone.windCollider.bounds;
            for (int i = 0; !reachable && i < reachableZones.Count; i++)
            {
                WindRiseZone lowerZone = reachableZones[i];
                Bounds lowerBounds = lowerZone.windCollider.bounds;
                // Stay in the player's vertical corridor; a distant branch of a
                // connected wind network must not attract the player sideways.
                float corridorMin = Mathf.Max(playerBounds.min.x, Mathf.Max(bounds.min.x, lowerBounds.min.x));
                float corridorMax = Mathf.Min(playerBounds.max.x, Mathf.Min(bounds.max.x, lowerBounds.max.x));
                reachable = bounds.max.y > lowerBounds.max.y && corridorMin < corridorMax &&
                    CollidersOverlap(lowerZone.windCollider, zone.windCollider);
            }

            if (reachable) reachableZones.Add(zone);
        }

        return reachableZones.Count == 0 ? null : reachableZones[reachableZones.Count - 1];
    }

    private bool IsZoneActive()
    {
        return isActiveAndEnabled && windCollider != null && windCollider.isActiveAndEnabled;
    }

    private bool MatchesPlayer(Rigidbody2D body)
    {
        return string.IsNullOrEmpty(playerTag) || body.CompareTag(playerTag);
    }

    private static bool CollidersOverlap(Collider2D first, Collider2D second)
    {
        ColliderDistance2D distance = first.Distance(second);
        // Use the physics contact tolerance, including edge-touching stacks.
        // Unlike an AABB-only test, this rejects disconnected rotated/polygon zones.
        return distance.isValid && distance.distance <= Physics2D.defaultContactOffset;
    }

    private static int CompareZonePriority(WindRiseZone first, WindRiseZone second)
    {
        Bounds a = first.windCollider.bounds;
        Bounds b = second.windCollider.bounds;
        // Geometry and wind settings break ties, so creation/callback order never
        // selects a different influence. Equal keys produce identical wind motion.
        return (a.max.y, -first.hoverOffsetFromTop, a.min.y, a.min.x, a.max.x,
            first.upwardAcceleration, first.maxRiseSpeed, first.hoverBandHeight,
            first.hoverSpring, first.hoverDamping, first.bobAmplitude, first.bobFrequency,
            first.recoilRiseWindIgnoreSeconds, first.recoilRiseMinUpwardSpeed).CompareTo(
            (b.max.y, -second.hoverOffsetFromTop, b.min.y, b.min.x, b.max.x,
            second.upwardAcceleration, second.maxRiseSpeed, second.hoverBandHeight,
            second.hoverSpring, second.hoverDamping, second.bobAmplitude, second.bobFrequency,
            second.recoilRiseWindIgnoreSeconds, second.recoilRiseMinUpwardSpeed));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        AddTarget(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        AddTarget(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        RemoveTarget(other);
    }

    private void AddTarget(Collider2D other)
    {
        // プレイヤー本体以外（攻撃・パリィ判定など）は対象外。
        if (!IsZoneActive() || !TryBuildTarget(other, out PlayerWindTarget target))
        {
            return;
        }

        if (!targets.ContainsKey(target.Rigidbody)) targets.Add(target.Rigidbody, target);
    }

    private void RemoveTarget(Collider2D other)
    {
        // WindRise から完全に出たかどうかを、Rigidbody 単位で判定する。
        Rigidbody2D body = ResolveRigidbody(other);
        if (body == null)
        {
            return;
        }

        if (!targets.TryGetValue(body, out PlayerWindTarget target))
        {
            return;
        }

        // An ignored child exit must not remove a body that is still in the wind.
        // Another zone can keep the shared target alive after this zone is left.
        if (other != target.BodyCollider) return;
        foreach (WindRiseZone zone in activeZones)
        {
            if (zone != null && zone.IsZoneActive() && zone.MatchesPlayer(body) &&
                CollidersOverlap(zone.windCollider, target.BodyCollider)) return;
        }
        targets.Remove(body);
    }

    private bool TryBuildTarget(Collider2D other, out PlayerWindTarget target)
    {
        target = null;

        // 攻撃・パリィ・GroundCheck などの子コライダーでは WindRise を反応させない。
        Rigidbody2D body = ResolveRigidbody(other);
        if (body == null)
        {
            return false;
        }

        GameObject playerObject = body.gameObject;
        if (!string.IsNullOrEmpty(playerTag) && !playerObject.CompareTag(playerTag))
        {
            return false;
        }

        Collider2D bodyCollider = body.GetComponent<Collider2D>();
        if (bodyCollider == null || other != bodyCollider)
        {
            return false;
        }

        if (targets.TryGetValue(body, out target)) return true;

        target = new PlayerWindTarget
        {
            Rigidbody = body,
            BodyCollider = bodyCollider,
            UmbrellaController = body.GetComponentInChildren<UmbrellaController>(),
            GroundCheck = body.GetComponentInChildren<GroundCheck>(),
            GunController = body.GetComponentInChildren<GunController>(),
            DodgeController = body.GetComponent<DodgeController>()
        };

        return true;
    }

    private static Rigidbody2D ResolveRigidbody(Collider2D other)
    {
        if (other == null)
        {
            return null;
        }

        // attachedRigidbody があれば最優先。なければ親階層から探す。
        if (other.attachedRigidbody != null)
        {
            return other.attachedRigidbody;
        }

        return other.GetComponentInParent<Rigidbody2D>();
    }

    private bool IsEligibleTarget(PlayerWindTarget target)
    {
        // Rigidbody が無効なら物理速度を触らない。
        if (target.Rigidbody == null || !target.Rigidbody.simulated)
        {
            return false;
        }

        if (BossAreaController.ShouldSuppressWindRise(windCollider))
        {
            return false;
        }

        if (requireUmbrellaOpen)
        {
            // 傘が閉じているときは、通常のジャンプ/落下を邪魔しない。
            if (target.UmbrellaController == null ||
                target.UmbrellaController.GetUmbrellaState() != UmbrellaController.UmbrellaState.Open)
            {
                return false;
            }
        }

        if (requireAirborne && target.GroundCheck != null && target.GroundCheck.IsGround())
        {
            // 地上では WindRise による押し上げを止める。
            return false;
        }

        return true;
    }

    private bool CanApplyWind(PlayerWindTarget target)
    {
        if (!IsEligibleTarget(target)) return false;

        if (target.GunController != null && target.GunController.GetRecoiling())
        {
            StartRecoilRiseWindSuppression(target);
            return false;
        }

        if (ShouldSkipWindForRecoilRise(target))
        {
            return false;
        }

        // 回避中は WindRise が横移動/無敵挙動を邪魔しないようにする。
        return target.DodgeController == null || !target.DodgeController.IsDodging();
    }

    private void StartRecoilRiseWindSuppression(PlayerWindTarget target)
    {
        target.IsRecoilRiseWindSuppressed = true;
        target.RecoilRiseWindIgnoreUntil = Time.time + recoilRiseWindIgnoreSeconds;
    }

    private bool ShouldSkipWindForRecoilRise(PlayerWindTarget target)
    {
        if (!target.IsRecoilRiseWindSuppressed)
        {
            return false;
        }

        if (target.Rigidbody == null ||
            Time.time > target.RecoilRiseWindIgnoreUntil ||
            target.Rigidbody.linearVelocity.y <= recoilRiseMinUpwardSpeed)
        {
            target.IsRecoilRiseWindSuppressed = false;
            return false;
        }

        return true;
    }

    private void ApplyWind(PlayerWindTarget target)
    {
        Rigidbody2D body = target.Rigidbody;
        Bounds playerBounds = target.BodyCollider.bounds;

        // プレイヤーの中心を、WindRise の上端付近にある浮遊目標へ近づける。
        float windTop = windCollider.bounds.max.y;
        float bob = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
        float hoverTargetCenterY = windTop - hoverOffsetFromTop + bob;
        float playerCenterY = playerBounds.center.y;
        float distanceToHover = hoverTargetCenterY - playerCenterY;

        Vector2 velocity = body.linearVelocity;
        float hoverStartDistance = hoverBandHeight * 0.5f;

        if (distanceToHover > hoverStartDistance)
        {
            // 目標より十分下にいる間は、上向き速度をなめらかに増やす。
            velocity.y = Mathf.MoveTowards(
                velocity.y,
                maxRiseSpeed,
                upwardAcceleration * Time.fixedDeltaTime
            );
        }
        else
        {
            // 上端付近ではバネ+減衰でふわふわ浮かせる。
            float hoverAcceleration = (distanceToHover * hoverSpring) - (velocity.y * hoverDamping);
            velocity.y += hoverAcceleration * Time.fixedDeltaTime;
            velocity.y = Mathf.Clamp(velocity.y, -maxRiseSpeed, maxRiseSpeed);
        }

        body.linearVelocity = velocity;
    }

    private void OnDisable()
    {
        activeZones.Remove(this);
        if (activeZones.Count == 0)
        {
            targets.Clear();
            sortedZones.Clear();
            reachableZones.Clear();
        }
    }

    /// <summary>
    /// WindRise の影響対象として必要なプレイヤー側コンポーネント一式。
    /// </summary>
    private sealed class PlayerWindTarget
    {
        public Rigidbody2D Rigidbody;
        public Collider2D BodyCollider;
        public UmbrellaController UmbrellaController;
        public GroundCheck GroundCheck;
        public GunController GunController;
        public DodgeController DodgeController;
        public bool IsRecoilRiseWindSuppressed;
        public float RecoilRiseWindIgnoreUntil;
    }
}
