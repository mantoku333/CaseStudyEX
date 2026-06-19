using UnityEngine;
using Cysharp.Threading.Tasks;

public class DodgeController : MonoBehaviour
{
    [Header("回避距離")]
    [SerializeField] private float dodgeDistance = 3.0f;   //回避する距離

    [Header("回避時間")]
    [SerializeField] private float dodgeDuration = 0.1f;　 //回避にかかる時間

    [Header("回避クールタイム")]
    [SerializeField, Min(0f)] private float dodgeCooldown = 0.5f;

    private bool isDodging = false;   //回避中かどうかのフラグ
    private float nextDodgeTime;
    private bool dodgeMovementCancelled;
    private Rigidbody2D rigidBody2d;  //Rigidbody2Dコンポーネント
    private PlayerCollisionMover2D collisionMover;

    // ロック中のエリアから渡される、回避移動専用の境界情報。
    // 回避の目標地点を先に切り詰めることで、エリア拘束との押し戻し競合を防ぐ。
    private Object areaDodgeBoundsOwner;
    private Bounds areaDodgeBounds;
    private Vector2 areaDodgeInset;
    private bool hasAreaDodgeBounds;
    private bool areaDodgeConfineX;
    private bool areaDodgeConfineY;
    private bool areaDodgeConfineYForDynamicBodies;
    private Collider2D areaDodgeBodyCollider;

    private void Awake()
    {
        EnsureComponents();
    }

    private void EnsureComponents()
    {
        rigidBody2d = GetComponent<Rigidbody2D>();
        collisionMover = GetComponent<PlayerCollisionMover2D>();

        if (collisionMover == null)
        {
            // プレハブに付け忘れても、回避移動だけは必ず物理Sweep経由にする。
            collisionMover = gameObject.AddComponent<PlayerCollisionMover2D>();
        }
    }

    public void SetDodgeDistance(float distance)
    {
        dodgeDistance = Mathf.Max(0f, distance);
    }

    public float GetDodgeDistance()
    {
        return dodgeDistance;
    }

    public void SetDodgeDuration(float duration)
    {
        dodgeDuration = Mathf.Max(0.01f, duration);
    }

    public float GetDodgeDuration()
    {
        return dodgeDuration;
    }

    public void SetDodgeCooldown(float cooldown)
    {
        dodgeCooldown = Mathf.Max(0f, cooldown);
    }

    public float GetDodgeCooldown()
    {
        return dodgeCooldown;
    }

    public bool CanDodge()
    {
        return !isDodging && Time.time >= nextDodgeTime;
    }

    /// <summary>
    /// エリア拘束中の回避移動が、指定範囲の外へ出ないようにする。
    /// </summary>
    /// <param name="owner">この制限を設定したエリア。</param>
    /// <param name="bounds">回避を収めるワールド範囲。</param>
    /// <param name="inset">壁へのめり込みを避けるための内側余白。</param>
    /// <param name="confineX">横方向を制限するか。</param>
    /// <param name="confineY">縦方向を制限するか。</param>
    /// <param name="confineYForDynamicBodies">Dynamic Rigidbody2D でも縦方向を制限するか。</param>
    /// <param name="bodyCollider">プレイヤー本体コライダー。</param>
    public void SetAreaDodgeBounds(
        Object owner,
        Bounds bounds,
        Vector2 inset,
        bool confineX,
        bool confineY,
        bool confineYForDynamicBodies,
        Collider2D bodyCollider)
    {
        if (owner == null || bodyCollider == null)
        {
            return;
        }

        areaDodgeBoundsOwner = owner;
        areaDodgeBounds = bounds;
        areaDodgeInset = new Vector2(Mathf.Max(0f, inset.x), Mathf.Max(0f, inset.y));
        areaDodgeConfineX = confineX;
        areaDodgeConfineY = confineY;
        areaDodgeConfineYForDynamicBodies = confineYForDynamicBodies;
        areaDodgeBodyCollider = bodyCollider;
        hasAreaDodgeBounds = true;
    }

    /// <summary>
    /// 指定エリアが設定した回避移動制限を解除する。
    /// </summary>
    /// <param name="owner">解除を要求しているエリア。</param>
    public void ClearAreaDodgeBounds(Object owner)
    {
        if (!hasAreaDodgeBounds || areaDodgeBoundsOwner != owner)
        {
            return;
        }

        hasAreaDodgeBounds = false;
        areaDodgeBoundsOwner = null;
        areaDodgeBodyCollider = null;
    }

    public void CancelCurrentDodgeMovement()
    {
        if (!isDodging)
        {
            return;
        }

        dodgeMovementCancelled = true;
    }

    /// <summary>
    /// プレイヤーの回避動作を実行する関数
    /// </summary>
    /// <param name="direction">回避する方向。</param>
    public async UniTaskVoid Dodge(Vector2 direction)
    {
        if (!CanDodge()) { return; }

        EnsureComponents();

        if (rigidBody2d == null) { return; }

        isDodging = true;
        nextDodgeTime = Time.time + dodgeCooldown;
        dodgeMovementCancelled = false;

        Vector2 velocity = rigidBody2d.linearVelocity;
        velocity.x = 0.0f;
        rigidBody2d.linearVelocity = velocity;

        Vector2 startPos = rigidBody2d.position;
        Vector2 targetPos = startPos;

        if (direction != Vector2.zero)
        {
            targetPos = ResolveReachableDodgeTarget(startPos, direction.normalized * dodgeDistance);
        }

        // 回避アニメーションは通常通り再生しつつ、移動先だけをロック範囲内に収める。
        targetPos = ClampPositionToAreaDodgeBounds(targetPos);

        float elapsedTime = 0.0f;

        while (elapsedTime < dodgeDuration)
        {
            float t = elapsedTime / dodgeDuration;

            MoveToDodgePosition(Vector2.Lerp(startPos, targetPos, t));

            await UniTask.Yield(PlayerLoopTiming.FixedUpdate);
            elapsedTime += Time.fixedDeltaTime;
        }

        MoveToDodgePosition(targetPos);

        dodgeMovementCancelled = false;
        isDodging = false;
    }

    /// <summary>
    /// 今回避中かどうかを返す関数
    /// </summary>
    /// <returns>回避中なら true。</returns>
    public bool IsDodging()
    {
        return isDodging;
    }

    private Vector2 ResolveReachableDodgeTarget(Vector2 startPosition, Vector2 desiredDelta)
    {
        Vector2 clampedDesiredTarget = ClampPositionToAreaDodgeBounds(startPosition + desiredDelta);
        Vector2 clampedDesiredDelta = clampedDesiredTarget - startPosition;

        if (collisionMover == null || clampedDesiredDelta.sqrMagnitude <= 0f)
        {
            return clampedDesiredTarget;
        }

        clampedDesiredDelta = collisionMover.ProjectVectorAwayFromSolidContacts(clampedDesiredDelta);
        if (clampedDesiredDelta.sqrMagnitude <= 0f)
        {
            return startPosition;
        }

        return startPosition + collisionMover.CalculateSlideDelta(clampedDesiredDelta);
    }

    private void MoveToDodgePosition(Vector2 targetPosition)
    {
        if (dodgeMovementCancelled)
        {
            return;
        }

        if (rigidBody2d == null)
        {
            return;
        }

        Vector2 clampedTargetPosition = ClampPositionToAreaDodgeBounds(targetPosition);
        // 衝突を考慮した最終目標は回避開始時に一度だけ解決する。
        // 毎FixedUpdateでSweepし直すと、大きなステージ座標や重いTilemap付近でFPS低下を起こしやすい。
        rigidBody2d.MovePosition(clampedTargetPosition);
    }

    private Vector2 ClampPositionToAreaDodgeBounds(Vector2 position)
    {
        // 本体コライダーの現在の相対位置を使い、子トリガーに影響されない範囲で回避先を補正する。
        if (!hasAreaDodgeBounds || areaDodgeBodyCollider == null || rigidBody2d == null)
        {
            return position;
        }

        Bounds bodyBounds = areaDodgeBodyCollider.bounds;
        Vector2 currentRigidbodyPosition = rigidBody2d.position;
        Vector2 minOffset = (Vector2)bodyBounds.min - currentRigidbodyPosition;
        Vector2 maxOffset = (Vector2)bodyBounds.max - currentRigidbodyPosition;
        Vector2 clamped = position;

        if (areaDodgeConfineX)
        {
            float minX = areaDodgeBounds.min.x + areaDodgeInset.x - minOffset.x;
            float maxX = areaDodgeBounds.max.x - areaDodgeInset.x - maxOffset.x;
            clamped.x = minX <= maxX ? Mathf.Clamp(clamped.x, minX, maxX) : areaDodgeBounds.center.x;
        }

        if (areaDodgeConfineY && ShouldConfineYForAreaDodge())
        {
            float minY = areaDodgeBounds.min.y + areaDodgeInset.y - minOffset.y;
            float maxY = areaDodgeBounds.max.y - areaDodgeInset.y - maxOffset.y;
            clamped.y = minY <= maxY ? Mathf.Clamp(clamped.y, minY, maxY) : areaDodgeBounds.center.y;
        }

        return clamped;
    }

    private bool ShouldConfineYForAreaDodge()
    {
        if (rigidBody2d == null)
        {
            return true;
        }

        if (rigidBody2d.bodyType != RigidbodyType2D.Dynamic)
        {
            return true;
        }

        return areaDodgeConfineYForDynamicBodies;
    }
}
