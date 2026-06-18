using System.Collections.Generic;
using UnityEngine;

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

    // Rigidbody2D ごとに、風エリア内にいるプレイヤー情報をキャッシュする。
    private readonly Dictionary<Rigidbody2D, PlayerWindTarget> targets = new Dictionary<Rigidbody2D, PlayerWindTarget>();
    // FixedUpdate 中に削除が必要になった対象を一時的に入れるリスト。
    private readonly List<Rigidbody2D> staleTargets = new List<Rigidbody2D>();

    private Collider2D windCollider;

    private void Awake()
    {
        windCollider = GetComponent<Collider2D>();
        if (windCollider != null)
        {
            // WindRise は足場ではなく風の判定なので、必ずトリガーとして扱う。
            windCollider.isTrigger = true;
        }
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
        if (windCollider == null)
        {
            return;
        }

        staleTargets.Clear();

        // 登録済みのプレイヤーに対して、条件を満たす間だけ風の速度補正を行う。
        foreach (KeyValuePair<Rigidbody2D, PlayerWindTarget> pair in targets)
        {
            Rigidbody2D body = pair.Key;
            PlayerWindTarget target = pair.Value;

            if (body == null || target == null || target.BodyCollider == null)
            {
                staleTargets.Add(body);
                continue;
            }

            if (!CanApplyWind(target))
            {
                continue;
            }

            ApplyWind(target);
        }

        // foreach 中に Dictionary を直接変更できないため、後でまとめて削除する。
        for (int i = 0; i < staleTargets.Count; i++)
        {
            targets.Remove(staleTargets[i]);
        }
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
        // プレイヤー以外のコライダーや Rigidbody を持たないものは対象外。
        if (!TryBuildTarget(other, out PlayerWindTarget target))
        {
            return;
        }

        if (targets.TryGetValue(target.Rigidbody, out PlayerWindTarget existingTarget))
        {
            // プレイヤーに複数コライダーがある場合に備えて、重なっているコライダーを個別管理する。
            existingTarget.OverlappingColliders.Add(other);
            return;
        }

        target.OverlappingColliders.Add(other);
        targets.Add(target.Rigidbody, target);
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

        target.OverlappingColliders.Remove(other);
        if (target.OverlappingColliders.Count == 0)
        {
            targets.Remove(body);
        }
    }

    private bool TryBuildTarget(Collider2D other, out PlayerWindTarget target)
    {
        target = null;

        // 子オブジェクトのコライダーに触れた場合でも、親の Rigidbody2D を風の対象にする。
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

        // 風の判定にはプレイヤー本体の Collider bounds を使う。
        Collider2D bodyCollider = body.GetComponent<Collider2D>();
        if (bodyCollider == null)
        {
            bodyCollider = other;
        }

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

    private bool CanApplyWind(PlayerWindTarget target)
    {
        // Rigidbody が無効なら物理速度を触らない。
        if (target.Rigidbody == null || !target.Rigidbody.simulated)
        {
            return false;
        }

        if (BossAreaController.ShouldSuppressWindRiseAt(windCollider.bounds.center))
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

        if (target.GunController != null && target.GunController.GetRecoiling())
        {
            StartRecoilRiseWindSuppression(target);
            return false;
        }

        if (ShouldSkipWindForRecoilRise(target))
        {
            return false;
        }

        if (target.DodgeController != null && target.DodgeController.IsDodging())
        {
            // 回避中は WindRise が横移動/無敵挙動を邪魔しないようにする。
            return false;
        }

        return true;
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
        // 無効化時に古い参照を残さない。
        targets.Clear();
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
        public readonly HashSet<Collider2D> OverlappingColliders = new HashSet<Collider2D>();
    }
}
