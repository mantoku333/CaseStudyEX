using System.Collections;
using System.Collections.Generic;
using GameName.Enemy;
using Player;
using UnityEngine;

/// <summary>
/// 滑空中に下入力＋攻撃で発動する落下攻撃。
/// プレイヤー本体へ追加して使う想定の、プランナー調整用コンポーネント。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerDiveAttackController : MonoBehaviour
{
    [Header("発動条件")]
    [SerializeField, Tooltip("無効にすると落下攻撃を発動しません。")]
    private bool 落下攻撃を使用する = true;

    [SerializeField, Min(1), Tooltip("地面から何グリッド以上離れていれば発動できるかです。2なら地面から2ブロック以上で発動できます。")]
    private int 発動に必要な地面からの高さグリッド数 = 2;

    [SerializeField, Tooltip("高さ判定に使うGridです。未設定ならシーン内のGridを自動取得します。")]
    private GridLayout 高さ判定に使うグリッド;

    [SerializeField, Tooltip("高さ判定で地面として扱うレイヤーです。未設定なら Ground / FallThroughFloor を自動使用します。")]
    private LayerMask 高さ判定用地面レイヤー;

    [SerializeField, Min(0.1f), Tooltip("高さ判定で下方向に地面を探す最大距離です。")]
    private float 高さ判定最大距離 = 30f;

    [Header("落下中の移動")]
    [SerializeField, Min(0.1f), Tooltip("発動後、地面に当たるまで下方向へ移動する速さです。")]
    private float 落下速度 = 24f;

    [SerializeField, Range(0f, 1f), Tooltip("落下中に元の横速度をどれだけ残すかです。0で真下、1で横速度を維持します。")]
    private float 落下中の横速度維持率 = 0.1f;

    [SerializeField, Min(0.05f), Tooltip("地面判定が取れなかった場合の安全終了時間です。")]
    private float 最大落下時間 = 1.2f;

    [Header("着地攻撃判定")]
    [SerializeField, Min(1), Tooltip("着地地点の敵へ与えるダメージ量です。")]
    private int ダメージ量 = 40;

    [SerializeField, Min(0.05f), Tooltip("着地地点を中心に敵を探す半径です。")]
    private float 攻撃半径 = 1.25f;

    [SerializeField, Tooltip("攻撃対象にするレイヤーです。0の場合も、範囲内から EnemyController を探します。")]
    private LayerMask 攻撃対象レイヤー = ~0;

    [Header("落下中攻撃判定")]
    [SerializeField, Tooltip("有効にすると、着地前でも落下先の敵に落下攻撃を当てます。敵接触ダメージよりプレイヤー攻撃を優先するための判定です。")]
    private bool 落下中にも攻撃判定を出す = true;

    [SerializeField, Min(0.05f), Tooltip("落下中に敵を拾う攻撃判定の半径です。着地攻撃より少し小さめが推奨です。")]
    private float 落下中攻撃半径 = 0.85f;

    [SerializeField, Tooltip("落下中攻撃判定の中心位置です。プレイヤー座標からのオフセットで、足元または傘先に合わせます。")]
    private Vector2 落下中攻撃判定オフセット = new Vector2(0f, -0.9f);

    [Header("撃破時の跳ね返り")]
    [SerializeField, Tooltip("落下攻撃で敵を倒したとき、上へ跳ね返るかどうかです。")]
    private bool 敵撃破時に跳ねる = true;

    [SerializeField, Min(0f), Tooltip("敵を倒したときに上へ跳ねる強さです。再度滑空へつなげやすくします。")]
    private float 跳ね上がり速度 = 12f;

    [SerializeField, Min(0f), Tooltip("跳ね上がった直後、左右入力で軌道調整できる時間です。")]
    private float 跳ね上がり操作時間 = 0.35f;

    [SerializeField, Min(0f), Tooltip("跳ね上がり中の左右調整速度です。")]
    private float 跳ね上がり左右調整速度 = 4f;

    [Header("攻撃優先")]
    [SerializeField, Min(0f), Tooltip("落下攻撃の着地判定中だけ、敵からの被弾よりプレイヤー攻撃を優先する時間です。空振り時は即解除します。")]
    private float 攻撃成立時の被弾無効時間 = 0.16f;

    [Header("見た目")]
    [SerializeField, Tooltip("落下攻撃中に表示する1枚絵です。")]
    private Sprite 落下攻撃スプライト;

    [SerializeField, Tooltip("差し替え対象の SpriteRenderer です。未設定なら子から自動取得します。")]
    private SpriteRenderer 対象スプライトレンダラー;

    [SerializeField, Tooltip("落下攻撃中だけ Animator を止め、1枚絵を維持します。")]
    private bool 落下中はAnimatorを停止する = true;

    [SerializeField, Tooltip("落下攻撃スプライト表示中の位置補正です。画像の中心ズレをここで吸収します。")]
    private Vector3 スプライト位置補正 = new Vector3(0f, 0f, 0f);

    [SerializeField, Tooltip("落下攻撃スプライト表示中のスケール倍率です。画像サイズ差をここで吸収します。")]
    private Vector3 スプライトスケール倍率 = Vector3.one;

    [Header("敵ヒットエフェクト")]
    [SerializeField, Tooltip("敵に当たった時だけ再生するエフェクトのスプライトシートです。パリィ成功エフェクトの青い方を指定してください。")]
    private Texture2D 敵ヒットエフェクトスプライトシート;

    [SerializeField, Min(1), Tooltip("敵ヒットエフェクトの横方向フレーム数です。")]
    private int 敵ヒットエフェクト横フレーム数 = 5;

    [SerializeField, Min(1), Tooltip("敵ヒットエフェクトの縦方向フレーム数です。")]
    private int 敵ヒットエフェクト縦フレーム数 = 4;

    [SerializeField, Min(1), Tooltip("敵ヒットエフェクトで再生する総フレーム数です。")]
    private int 敵ヒットエフェクト再生フレーム数 = 20;

    [SerializeField, Min(0.01f), Tooltip("敵ヒットエフェクト1フレームあたりの秒数です。")]
    private float 敵ヒットエフェクトフレーム秒数 = 0.033f;

    [SerializeField, Min(1f), Tooltip("敵ヒットエフェクトのPixels Per Unitです。")]
    private float 敵ヒットエフェクトPixelsPerUnit = 100f;

    [SerializeField, Tooltip("敵ヒットエフェクトのスプライトピボットです。")]
    private Vector2 敵ヒットエフェクトピボット = new Vector2(0.67f, 0.5f);

    [SerializeField, Tooltip("敵ヒットエフェクトの位置補正です。敵の中心からのワールド座標オフセットです。")]
    private Vector3 敵ヒットエフェクト位置補正 = Vector3.zero;

    [SerializeField, Tooltip("敵ヒットエフェクトの回転です。初期値は下向きです。")]
    private Vector3 敵ヒットエフェクト回転 = new Vector3(0f, 0f, 90f);

    [SerializeField, Tooltip("敵ヒットエフェクトのスケールです。")]
    private Vector3 敵ヒットエフェクトスケール = Vector3.one;

    [SerializeField, Tooltip("敵ヒットエフェクトの描画順を、ヒット対象の最前面SpriteRendererからどれだけ前に出すかです。")]
    private int 敵ヒットエフェクト描画順オフセット = 4;

    [SerializeField, Tooltip("敵ヒットエフェクトの最低Sorting Orderです。")]
    private int 敵ヒットエフェクト最低描画順 = 20;

    [SerializeField, Tooltip("有効にすると、ヒット対象のマテリアルを敵ヒットエフェクトにも使います。")]
    private bool 敵ヒットエフェクト対象マテリアルをコピー = true;

    [Header("SE")]
    [SerializeField, Tooltip("未設定ならプレイヤーの AudioSource を使います。")]
    private AudioSource SE再生AudioSource;

    [SerializeField, Tooltip("落下攻撃を開始した瞬間のSEです。")]
    private AudioClip 落下開始SE;

    [SerializeField, Tooltip("地面に着地した瞬間のSEです。")]
    private AudioClip 着地SE;

    [SerializeField, Tooltip("敵に当たった瞬間のSEです。")]
    private AudioClip 敵ヒットSE;

    [SerializeField, Range(0f, 2f), Tooltip("落下開始SEの音量です。")]
    private float 落下開始SE音量 = 1f;

    [SerializeField, Range(0f, 2f), Tooltip("着地SEの音量です。")]
    private float 着地SE音量 = 1f;

    [SerializeField, Range(0f, 2f), Tooltip("敵ヒットSEの音量です。")]
    private float 敵ヒットSE音量 = 1f;

    private readonly Collider2D[] overlapResults = new Collider2D[32];
    private readonly HashSet<EnemyController> hitEnemies = new HashSet<EnemyController>();
    private readonly HashSet<AttackDestructible> hitDestructibles = new HashSet<AttackDestructible>();
    private readonly List<Sprite> generatedHitEffectSprites = new List<Sprite>();

    private Rigidbody2D rigidBody2d;
    private GroundCheck groundCheck;
    private PlayerEquipmentController equipmentController;
    private PlayerHealth playerHealth;
    private Animator targetAnimator;
    private Transform spriteTransform;
    private Sprite previousSprite;
    private Vector3 previousLocalPosition;
    private Vector3 previousLocalScale;
    private bool previousAnimatorEnabled;
    private bool hasVisualOverride;
    private bool isDiveAttacking;
    private float diveStartedTime;
    private float bounceControlEndTime;
    private bool warnedMissingGrid;
    private Sprite[] hitEffectFrames;

    public bool IsDiveAttacking => isDiveAttacking;
    public bool IsBounceControlActive => Time.time < bounceControlEndTime;
    public float BounceControlSpeed => 跳ね上がり左右調整速度;

    private void Awake()
    {
        rigidBody2d = GetComponent<Rigidbody2D>();
        groundCheck = GetComponentInChildren<GroundCheck>();
        equipmentController = GetComponent<PlayerEquipmentController>();
        playerHealth = GetComponent<PlayerHealth>();
        ResolveGridIfNeeded();
        ResolveGroundLayerMaskIfNeeded();

        if (対象スプライトレンダラー == null)
        {
            対象スプライトレンダラー = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (対象スプライトレンダラー != null)
        {
            spriteTransform = 対象スプライトレンダラー.transform;
            targetAnimator = 対象スプライトレンダラー.GetComponent<Animator>();
            if (targetAnimator == null)
            {
                targetAnimator = 対象スプライトレンダラー.GetComponentInParent<Animator>();
            }
        }

        if (SE再生AudioSource == null)
        {
            SE再生AudioSource = GetComponent<AudioSource>();
        }
    }

    private void OnValidate()
    {
        発動に必要な地面からの高さグリッド数 = Mathf.Max(1, 発動に必要な地面からの高さグリッド数);
        高さ判定最大距離 = Mathf.Max(0.1f, 高さ判定最大距離);
        落下速度 = Mathf.Max(0.1f, 落下速度);
        最大落下時間 = Mathf.Max(0.05f, 最大落下時間);
        ダメージ量 = Mathf.Max(1, ダメージ量);
        攻撃半径 = Mathf.Max(0.05f, 攻撃半径);
        落下中攻撃半径 = Mathf.Max(0.05f, 落下中攻撃半径);
        跳ね上がり速度 = Mathf.Max(0f, 跳ね上がり速度);
        跳ね上がり操作時間 = Mathf.Max(0f, 跳ね上がり操作時間);
        跳ね上がり左右調整速度 = Mathf.Max(0f, 跳ね上がり左右調整速度);
        攻撃成立時の被弾無効時間 = Mathf.Max(0f, 攻撃成立時の被弾無効時間);
        敵ヒットエフェクト横フレーム数 = Mathf.Max(1, 敵ヒットエフェクト横フレーム数);
        敵ヒットエフェクト縦フレーム数 = Mathf.Max(1, 敵ヒットエフェクト縦フレーム数);
        敵ヒットエフェクト再生フレーム数 = Mathf.Max(1, 敵ヒットエフェクト再生フレーム数);
        敵ヒットエフェクトフレーム秒数 = Mathf.Max(0.01f, 敵ヒットエフェクトフレーム秒数);
        敵ヒットエフェクトPixelsPerUnit = Mathf.Max(1f, 敵ヒットエフェクトPixelsPerUnit);
        敵ヒットエフェクトピボット = new Vector2(
            Mathf.Clamp01(敵ヒットエフェクトピボット.x),
            Mathf.Clamp01(敵ヒットエフェクトピボット.y));
        落下開始SE音量 = Mathf.Clamp(落下開始SE音量, 0f, 2f);
        着地SE音量 = Mathf.Clamp(着地SE音量, 0f, 2f);
        敵ヒットSE音量 = Mathf.Clamp(敵ヒットSE音量, 0f, 2f);
    }

    private void OnDisable()
    {
        if (isDiveAttacking)
        {
            EndDiveAttack(false);
        }

        RestoreVisual();
        bounceControlEndTime = 0f;
    }

    private void FixedUpdate()
    {
        if (!isDiveAttacking || rigidBody2d == null)
        {
            return;
        }

        Vector2 velocity = rigidBody2d.linearVelocity;
        velocity.x *= 落下中の横速度維持率;
        velocity.y = -Mathf.Abs(落下速度);
        rigidBody2d.linearVelocity = velocity;

        UpdateDiveAttackHit();
        if (!isDiveAttacking)
        {
            return;
        }

        if (HasLanded() || Time.time >= diveStartedTime + 最大落下時間)
        {
            EndDiveAttack(true);
        }
    }

    public bool TryStartDiveAttack()
    {
        if (!落下攻撃を使用する || isDiveAttacking || rigidBody2d == null)
        {
            return false;
        }

        isDiveAttacking = true;
        diveStartedTime = Time.time;
        bounceControlEndTime = 0f;
        hitEnemies.Clear();
        hitDestructibles.Clear();

        ApplyVisualOverride();
        PlaySE(落下開始SE, 落下開始SE音量);

        Vector2 velocity = rigidBody2d.linearVelocity;
        velocity.x *= 落下中の横速度維持率;
        velocity.y = -Mathf.Abs(落下速度);
        rigidBody2d.linearVelocity = velocity;
        return true;
    }

    public void ApplyBounceHorizontalControl(float horizontalInput)
    {
        if (!IsBounceControlActive || rigidBody2d == null)
        {
            return;
        }

        horizontalInput = Mathf.Clamp(horizontalInput, -1f, 1f);
        Vector2 velocity = rigidBody2d.linearVelocity;
        velocity.x = horizontalInput * 跳ね上がり左右調整速度;
        rigidBody2d.linearVelocity = velocity;
    }

    public bool CanStartDiveAttackFromAir()
    {
        if (!落下攻撃を使用する || isDiveAttacking)
        {
            return false;
        }

        if (groundCheck != null && groundCheck.IsGround())
        {
            return false;
        }

        return IsHighEnoughAboveGroundByGrid();
    }

    private bool HasLanded()
    {
        return groundCheck != null && groundCheck.IsGround();
    }

    private bool IsHighEnoughAboveGroundByGrid()
    {
        ResolveGridIfNeeded();
        ResolveGroundLayerMaskIfNeeded();

        if (高さ判定に使うグリッド == null)
        {
            if (!warnedMissingGrid)
            {
                Debug.LogWarning("落下攻撃: 高さ判定に使うGridが見つかりません。", this);
                warnedMissingGrid = true;
            }

            return false;
        }

        RaycastHit2D groundHit = Physics2D.Raycast(
            transform.position,
            Vector2.down,
            高さ判定最大距離,
            高さ判定用地面レイヤー);

        if (groundHit.collider == null)
        {
            return false;
        }

        Vector3Int playerCell = 高さ判定に使うグリッド.WorldToCell(transform.position);
        Vector3 groundCellProbePosition = groundHit.point + Vector2.down * 0.01f;
        Vector3Int groundCell = 高さ判定に使うグリッド.WorldToCell(groundCellProbePosition);
        int gridHeightFromGround = playerCell.y - groundCell.y;
        return gridHeightFromGround >= 発動に必要な地面からの高さグリッド数;
    }

    private void EndDiveAttack(bool applyLanding)
    {
        if (!isDiveAttacking && !applyLanding)
        {
            return;
        }

        isDiveAttacking = false;
        RestoreVisual();

        if (!applyLanding)
        {
            return;
        }

        PlaySE(着地SE, 着地SE音量);
        playerHealth?.RequestAttackPriorityInvulnerability(攻撃成立時の被弾無効時間);
        bool foundAnyTarget = ApplyDamageAtPosition(
            transform.position,
            攻撃半径,
            out bool killedAnyEnemy,
            out bool appliedNewHit,
            out bool appliedNewEnemyHit,
            out Vector3 enemyHitEffectPosition,
            out Collider2D enemyHitCollider);
        if (appliedNewHit)
        {
            PlaySE(敵ヒットSE, 敵ヒットSE音量);
        }

        if (appliedNewEnemyHit)
        {
            PlayEnemyHitEffect(enemyHitEffectPosition, enemyHitCollider);
        }

        if (!foundAnyTarget)
        {
            playerHealth?.ClearAttackPriorityInvulnerability();
        }

        if (killedAnyEnemy && 敵撃破時に跳ねる)
        {
            BounceUp();
        }
    }

    private void UpdateDiveAttackHit()
    {
        if (!落下中にも攻撃判定を出す)
        {
            return;
        }

        Vector2 attackCenter = (Vector2)transform.position + 落下中攻撃判定オフセット;
        playerHealth?.RequestAttackPriorityInvulnerability(攻撃成立時の被弾無効時間);
        bool foundAnyTarget = ApplyDamageAtPosition(
            attackCenter,
            落下中攻撃半径,
            out bool killedAnyEnemy,
            out bool appliedNewHit,
            out bool appliedNewEnemyHit,
            out Vector3 enemyHitEffectPosition,
            out Collider2D enemyHitCollider);
        if (!foundAnyTarget)
        {
            playerHealth?.ClearAttackPriorityInvulnerability();
            return;
        }

        if (appliedNewHit)
        {
            PlaySE(敵ヒットSE, 敵ヒットSE音量);
        }

        if (appliedNewEnemyHit)
        {
            PlayEnemyHitEffect(enemyHitEffectPosition, enemyHitCollider);
        }

        if (killedAnyEnemy && 敵撃破時に跳ねる)
        {
            EndDiveAttack(false);
            BounceUp();
        }
    }

    private bool ApplyDamageAtPosition(
        Vector2 center,
        float radius,
        out bool killedAnyEnemy,
        out bool appliedNewHit,
        out bool appliedNewEnemyHit,
        out Vector3 enemyHitEffectPosition,
        out Collider2D enemyHitCollider)
    {
        killedAnyEnemy = false;
        appliedNewHit = false;
        appliedNewEnemyHit = false;
        enemyHitEffectPosition = center;
        enemyHitCollider = null;
        bool foundAnyTarget = false;

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        filter.SetLayerMask(攻撃対象レイヤー);

        int hitCount = Physics2D.OverlapCircle(center, radius, filter, overlapResults);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = overlapResults[i];
            overlapResults[i] = null;
            if (hitCollider == null)
            {
                continue;
            }

            EnemyController enemy = hitCollider.GetComponentInParent<EnemyController>();
            if (enemy != null)
            {
                foundAnyTarget = true;
                if (!hitEnemies.Add(enemy))
                {
                    continue;
                }

                appliedNewHit = true;
                if (!appliedNewEnemyHit)
                {
                    appliedNewEnemyHit = true;
                    enemyHitEffectPosition = ResolveEnemyHitEffectPosition(center, hitCollider);
                    enemyHitCollider = hitCollider;
                }

                int healthBefore = enemy.CurrentHealth;
                enemy.TakeDamage(ダメージ量);
                if (healthBefore > 0 && enemy.CurrentHealth <= 0)
                {
                    killedAnyEnemy = true;
                    equipmentController?.NotifyEnemyKilledByPlayerAttack();
                }

                HitStopController.RequestPlayerToEnemy();
                continue;
            }

            AttackDestructible destructible = hitCollider.GetComponentInParent<AttackDestructible>();
            if (destructible != null)
            {
                foundAnyTarget = true;
                if (!hitDestructibles.Add(destructible))
                {
                    continue;
                }

                appliedNewHit = true;
                destructible.ApplyDamage(ダメージ量, null, hitCollider);
            }
        }

        return foundAnyTarget;
    }

    private Vector3 ResolveEnemyHitEffectPosition(Vector2 attackCenter, Collider2D hitCollider)
    {
        if (hitCollider == null)
        {
            return (Vector3)attackCenter + 敵ヒットエフェクト位置補正;
        }

        Vector2 hitPoint = hitCollider.ClosestPoint(attackCenter);
        if (!hitCollider.OverlapPoint(attackCenter))
        {
            return (Vector3)hitPoint + 敵ヒットエフェクト位置補正;
        }

        Vector2 pointFromEnemyCenter = hitCollider.ClosestPoint(transform.position);
        if ((pointFromEnemyCenter - (Vector2)hitCollider.bounds.center).sqrMagnitude > 0.0001f)
        {
            return (Vector3)pointFromEnemyCenter + 敵ヒットエフェクト位置補正;
        }

        return (Vector3)attackCenter + 敵ヒットエフェクト位置補正;
    }

    private void PlayEnemyHitEffect(Vector3 effectPosition, Collider2D hitCollider)
    {
        BuildHitEffectFramesIfNeeded();
        if (hitEffectFrames == null || hitEffectFrames.Length == 0)
        {
            return;
        }

        GameObject effectObject = new GameObject("DiveAttackEnemyHitEffect");
        effectObject.transform.position = effectPosition;
        effectObject.transform.rotation = Quaternion.Euler(敵ヒットエフェクト回転);
        effectObject.transform.localScale = 敵ヒットエフェクトスケール;

        SpriteRenderer renderer = effectObject.AddComponent<SpriteRenderer>();
        ApplyHitEffectRendererSettings(renderer, hitCollider);
        StartCoroutine(PlayHitEffectRoutine(effectObject, renderer));
    }

    private void ApplyHitEffectRendererSettings(SpriteRenderer renderer, Collider2D hitCollider)
    {
        SpriteRenderer sourceRenderer = ResolveFrontmostRenderer(hitCollider);
        if (sourceRenderer == null)
        {
            renderer.sortingOrder = 敵ヒットエフェクト最低描画順;
            return;
        }

        renderer.sortingLayerID = sourceRenderer.sortingLayerID;
        renderer.sortingOrder = Mathf.Max(
            sourceRenderer.sortingOrder + 敵ヒットエフェクト描画順オフセット,
            敵ヒットエフェクト最低描画順);

        if (敵ヒットエフェクト対象マテリアルをコピー && sourceRenderer.sharedMaterial != null)
        {
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
        }
    }

    private static SpriteRenderer ResolveFrontmostRenderer(Collider2D hitCollider)
    {
        if (hitCollider == null)
        {
            return null;
        }

        Transform targetRoot = hitCollider.transform;
        EnemyController enemy = hitCollider.GetComponentInParent<EnemyController>();
        if (enemy != null)
        {
            targetRoot = enemy.transform;
        }

        SpriteRenderer[] renderers = targetRoot.GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer frontmost = null;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (frontmost == null || CompareRendererSort(renderer, frontmost) > 0)
            {
                frontmost = renderer;
            }
        }

        return frontmost;
    }

    private static int CompareRendererSort(SpriteRenderer left, SpriteRenderer right)
    {
        int leftLayerValue = SortingLayer.GetLayerValueFromID(left.sortingLayerID);
        int rightLayerValue = SortingLayer.GetLayerValueFromID(right.sortingLayerID);
        if (leftLayerValue != rightLayerValue)
        {
            return leftLayerValue.CompareTo(rightLayerValue);
        }

        return left.sortingOrder.CompareTo(right.sortingOrder);
    }

    private IEnumerator PlayHitEffectRoutine(GameObject effectObject, SpriteRenderer renderer)
    {
        for (int i = 0; i < hitEffectFrames.Length; i++)
        {
            if (renderer == null)
            {
                yield break;
            }

            Sprite frame = hitEffectFrames[i];
            if (frame != null)
            {
                renderer.sprite = frame;
            }

            yield return new WaitForSeconds(敵ヒットエフェクトフレーム秒数);
        }

        Destroy(effectObject);
    }

    private void BuildHitEffectFramesIfNeeded()
    {
        if (hitEffectFrames == null || hitEffectFrames.Length == 0)
        {
            hitEffectFrames = BuildHitEffectFrames();
        }
    }

    private Sprite[] BuildHitEffectFrames()
    {
        if (敵ヒットエフェクトスプライトシート == null)
        {
            return System.Array.Empty<Sprite>();
        }

        int frameWidth = 敵ヒットエフェクトスプライトシート.width / 敵ヒットエフェクト横フレーム数;
        int frameHeight = 敵ヒットエフェクトスプライトシート.height / 敵ヒットエフェクト縦フレーム数;
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            return System.Array.Empty<Sprite>();
        }

        int maxFrameCount = Mathf.Min(
            敵ヒットエフェクト再生フレーム数,
            敵ヒットエフェクト横フレーム数 * 敵ヒットエフェクト縦フレーム数);
        Sprite[] frames = new Sprite[maxFrameCount];
        int index = 0;

        for (int row = 0; row < 敵ヒットエフェクト縦フレーム数 && index < maxFrameCount; row++)
        {
            int y = 敵ヒットエフェクトスプライトシート.height - ((row + 1) * frameHeight);

            for (int column = 0; column < 敵ヒットエフェクト横フレーム数 && index < maxFrameCount; column++)
            {
                Rect rect = new Rect(column * frameWidth, y, frameWidth, frameHeight);
                Sprite sprite = Sprite.Create(
                    敵ヒットエフェクトスプライトシート,
                    rect,
                    敵ヒットエフェクトピボット,
                    敵ヒットエフェクトPixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);

                frames[index++] = sprite;
                generatedHitEffectSprites.Add(sprite);
            }
        }

        return frames;
    }

    private void BounceUp()
    {
        if (rigidBody2d == null)
        {
            return;
        }

        Vector2 velocity = rigidBody2d.linearVelocity;
        velocity.y = Mathf.Max(velocity.y, 跳ね上がり速度);
        rigidBody2d.linearVelocity = velocity;
        bounceControlEndTime = Time.time + 跳ね上がり操作時間;
    }

    private void ApplyVisualOverride()
    {
        if (落下攻撃スプライト == null || 対象スプライトレンダラー == null)
        {
            return;
        }

        previousSprite = 対象スプライトレンダラー.sprite;
        if (spriteTransform != null)
        {
            previousLocalPosition = spriteTransform.localPosition;
            previousLocalScale = spriteTransform.localScale;
        }

        if (targetAnimator != null)
        {
            previousAnimatorEnabled = targetAnimator.enabled;
            if (落下中はAnimatorを停止する)
            {
                targetAnimator.enabled = false;
            }
        }

        対象スプライトレンダラー.sprite = 落下攻撃スプライト;

        if (spriteTransform != null)
        {
            spriteTransform.localPosition = previousLocalPosition + スプライト位置補正;
            spriteTransform.localScale = new Vector3(
                previousLocalScale.x * スプライトスケール倍率.x,
                previousLocalScale.y * スプライトスケール倍率.y,
                previousLocalScale.z * スプライトスケール倍率.z);
        }

        hasVisualOverride = true;
    }

    private void RestoreVisual()
    {
        if (!hasVisualOverride)
        {
            return;
        }

        if (対象スプライトレンダラー != null)
        {
            対象スプライトレンダラー.sprite = previousSprite;
        }

        if (spriteTransform != null)
        {
            spriteTransform.localPosition = previousLocalPosition;
            spriteTransform.localScale = previousLocalScale;
        }

        if (targetAnimator != null)
        {
            targetAnimator.enabled = previousAnimatorEnabled;
        }

        hasVisualOverride = false;
    }

    private void PlaySE(AudioClip clip, float volume)
    {
        if (SE再生AudioSource == null || clip == null || volume <= 0f)
        {
            return;
        }

        SE再生AudioSource.PlayOneShot(clip, volume);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < generatedHitEffectSprites.Count; i++)
        {
            if (generatedHitEffectSprites[i] != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(generatedHitEffectSprites[i]);
                }
                else
                {
                    DestroyImmediate(generatedHitEffectSprites[i]);
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.45f);
        Gizmos.DrawWireSphere(transform.position, 攻撃半径);

        Gizmos.color = new Color(0.3f, 0.65f, 1f, 0.55f);
        Gizmos.DrawWireSphere((Vector2)transform.position + 落下中攻撃判定オフセット, 落下中攻撃半径);
    }

    private void ResolveGridIfNeeded()
    {
        if (高さ判定に使うグリッド != null)
        {
            return;
        }

        高さ判定に使うグリッド = FindObjectOfType<GridLayout>();
    }

    private void ResolveGroundLayerMaskIfNeeded()
    {
        if (高さ判定用地面レイヤー.value != 0)
        {
            return;
        }

        int mask = 0;
        AddLayerToMask("Ground", ref mask);
        AddLayerToMask("FallThroughFloor", ref mask);
        高さ判定用地面レイヤー = mask != 0 ? mask : Physics2D.AllLayers;
    }

    private static void AddLayerToMask(string layerName, ref int mask)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
        {
            mask |= 1 << layer;
        }
    }
}
