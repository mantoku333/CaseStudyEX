using UnityEngine;

/// <summary>
/// プレイヤーの高速移動用ヘルパー。
/// MovePosition で一気に移動する前に本体コライダーを Cast し、壁に当たる場合は壁沿いへスライドさせる。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class PlayerCollisionMover2D : MonoBehaviour
{
    private const string GroundLayerName = "Ground";
    private const float MinMoveDistance = 0.0001f;
    private const float BlockingNormalDotThreshold = -0.0001f;

    // 基本は Ground レイヤーのみを固い壁として扱う。FallThroughFloor やトリガーは移動阻害に使わない。
    [SerializeField] private LayerMask solidLayerMask;
    // 壁ぴったりまで進めると接触誤差で埋まることがあるため、少しだけ手前で止める。
    [SerializeField, Min(0f)] private float skinWidth = 0.03f;
    // 角やC字地形で、1回目の衝突後に残り移動を壁沿いへ再投影する回数。
    [SerializeField, Range(1, 4)] private int slideIterations = 2;
    [SerializeField, Min(0f)] private float maxOverlapResolveDistance = 0.2f;
    [SerializeField, Range(1, 4)] private int overlapResolveIterations = 2;

    private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
    private readonly Collider2D[] overlapHits = new Collider2D[8];
    private readonly ContactPoint2D[] contactHits = new ContactPoint2D[8];

    private Rigidbody2D rigidBody2d;
    private Collider2D bodyCollider;
    private ContactFilter2D solidFilter;
    private int cachedMaskValue = int.MinValue;

    private void Awake()
    {
        EnsureRuntimeDefaults();
        CacheComponents();
        RebuildFilterIfNeeded();
    }

    private void Reset()
    {
        solidLayerMask = BuildDefaultSolidLayerMask();
        skinWidth = 0.03f;
        slideIterations = 2;
        maxOverlapResolveDistance = 0.2f;
        overlapResolveIterations = 2;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (solidLayerMask.value == 0)
        {
            solidLayerMask = BuildDefaultSolidLayerMask();
        }

        skinWidth = Mathf.Max(0f, skinWidth);
        slideIterations = Mathf.Clamp(slideIterations, 1, 4);
        maxOverlapResolveDistance = Mathf.Max(0f, maxOverlapResolveDistance);
        overlapResolveIterations = Mathf.Clamp(overlapResolveIterations, 1, 4);
        cachedMaskValue = int.MinValue;
    }
#endif

    public void SetSolidLayerMask(LayerMask mask)
    {
        solidLayerMask = mask;
        cachedMaskValue = int.MinValue;
    }

    public void SetSkinWidth(float value)
    {
        skinWidth = Mathf.Max(0f, value);
    }

    public Vector2 MoveWithSlide(Vector2 desiredDelta)
    {
        if (!CanMove())
        {
            return Vector2.zero;
        }

        ResolveInitialOverlaps();

        // すでにめり込みかけている場合も、微小移動だけで Cast せずに終わらせる。
        if (desiredDelta.sqrMagnitude <= MinMoveDistance * MinMoveDistance)
        {
            return Vector2.zero;
        }

        Vector2 startPosition = rigidBody2d.position;
        Vector2 appliedDelta = CalculateSlideDelta(desiredDelta);
        rigidBody2d.MovePosition(startPosition + appliedDelta);
        return appliedDelta;
    }

    public Vector2 ProjectVelocityForNextFixedStep(Vector2 velocity)
    {
        if (!CanMove())
        {
            return velocity;
        }

        ResolveInitialOverlaps();

        // リコイルの速度を次の FixedUpdate で移動する距離に変換し、同じ壁スライド計算を通す。
        float fixedDeltaTime = Time.fixedDeltaTime;
        if (fixedDeltaTime <= 0f)
        {
            return velocity;
        }

        Vector2 desiredDelta = velocity * fixedDeltaTime;
        if (desiredDelta.sqrMagnitude <= MinMoveDistance * MinMoveDistance)
        {
            return velocity;
        }

        return CalculateSlideDelta(desiredDelta) / fixedDeltaTime;
    }

    public Vector2 ProjectRecoilVelocityForNextFixedStep(Vector2 velocity)
    {
        if (!CanMove())
        {
            return velocity;
        }

        RebuildFilterIfNeeded();
        velocity = ProjectVelocityAwayFromCurrentSolidContacts(velocity);

        float fixedDeltaTime = Time.fixedDeltaTime;
        if (fixedDeltaTime <= 0f)
        {
            return velocity;
        }

        Vector2 desiredDelta = velocity * fixedDeltaTime;
        if (desiredDelta.sqrMagnitude <= MinMoveDistance * MinMoveDistance)
        {
            return velocity;
        }

        return CalculateRecoilSlideDelta(desiredDelta) / fixedDeltaTime;
    }

    public Vector2 CalculateSlideDelta(Vector2 desiredDelta)
    {
        if (!CanMove())
        {
            return Vector2.zero;
        }

        RebuildFilterIfNeeded();

        // Collider2D.Cast は現在位置基準なので、反復中だけ Rigidbody の位置を仮移動して判定する。
        // 最後に必ず開始位置へ戻し、実際の移動は MoveWithSlide 側の MovePosition に任せる。
        Vector2 startPosition = rigidBody2d.position;
        Vector2 totalDelta = Vector2.zero;
        Vector2 remainingDelta = desiredDelta;

        for (int i = 0; i < slideIterations; i++)
        {
            float remainingDistance = remainingDelta.magnitude;
            if (remainingDistance <= MinMoveDistance)
            {
                break;
            }

            rigidBody2d.position = startPosition + totalDelta;
            Physics2D.SyncTransforms();

            Vector2 direction = remainingDelta / remainingDistance;
            if (!TryCast(direction, remainingDistance + skinWidth, out RaycastHit2D hit))
            {
                // 進行方向に壁がなければ、残り距離をそのまま適用できる。
                totalDelta += remainingDelta;
                break;
            }

            // 衝突点の少し手前まで進め、残り距離は壁の法線方向を消して接線方向だけ残す。
            float safeDistance = Mathf.Max(0f, hit.distance - skinWidth);
            Vector2 safeDelta = direction * Mathf.Min(safeDistance, remainingDistance);
            totalDelta += safeDelta;

            float consumedDistance = safeDelta.magnitude;
            float leftoverDistance = Mathf.Max(0f, remainingDistance - consumedDistance);
            if (leftoverDistance <= MinMoveDistance)
            {
                break;
            }

            Vector2 leftoverDelta = direction * leftoverDistance;
            remainingDelta = ProjectOntoSurface(leftoverDelta, hit.normal);
        }

        rigidBody2d.position = startPosition;
        Physics2D.SyncTransforms();
        return totalDelta;
    }

    private Vector2 CalculateRecoilSlideDelta(Vector2 desiredDelta)
    {
        if (!CanMove())
        {
            return Vector2.zero;
        }

        RebuildFilterIfNeeded();

        Vector2 startPosition = rigidBody2d.position;
        try
        {
            Vector2 totalDelta = Vector2.zero;
            Vector2 remainingDelta = desiredDelta;

            for (int i = 0; i < slideIterations; i++)
            {
                float remainingDistance = remainingDelta.magnitude;
                if (remainingDistance <= MinMoveDistance)
                {
                    break;
                }

                rigidBody2d.position = startPosition + totalDelta;
                Physics2D.SyncTransforms();

                Vector2 direction = remainingDelta / remainingDistance;
                if (!TryCast(direction, remainingDistance + skinWidth, out RaycastHit2D hit))
                {
                    totalDelta += remainingDelta;
                    break;
                }

                float safeDistance = Mathf.Max(0f, hit.distance - skinWidth);
                Vector2 safeDelta = direction * Mathf.Min(safeDistance, remainingDistance);
                totalDelta += safeDelta;

                float consumedDistance = safeDelta.magnitude;
                float leftoverDistance = Mathf.Max(0f, remainingDistance - consumedDistance);
                if (leftoverDistance <= MinMoveDistance)
                {
                    break;
                }

                Vector2 leftoverDelta = direction * leftoverDistance;
                remainingDelta = ProjectRecoilOntoSurface(leftoverDelta, hit);
            }

            return totalDelta;
        }
        finally
        {
            rigidBody2d.position = startPosition;
            Physics2D.SyncTransforms();
        }
    }

    public void ResolveInitialOverlaps()
    {
        if (!CanMove())
        {
            return;
        }

        RebuildFilterIfNeeded();

        // C字地形などで開始時にわずかに埋まっていた場合、Cast 前に外側へ押し戻す。
        for (int iteration = 0; iteration < overlapResolveIterations; iteration++)
        {
            int overlapCount = bodyCollider.Overlap(solidFilter, overlapHits);
            bool resolvedAny = false;

            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D overlap = overlapHits[i];
                overlapHits[i] = null;

                if (!IsValidHitCollider(overlap))
                {
                    continue;
                }

                ColliderDistance2D distance = bodyCollider.Distance(overlap);
                if (!distance.isOverlapped)
                {
                    continue;
                }

                Vector2 pushDirection = distance.pointA - distance.pointB;
                if (pushDirection.sqrMagnitude <= MinMoveDistance * MinMoveDistance)
                {
                    pushDirection = (Vector2)bodyCollider.bounds.center - (Vector2)overlap.bounds.center;
                }

                if (pushDirection.sqrMagnitude <= MinMoveDistance * MinMoveDistance)
                {
                    pushDirection = Vector2.up;
                }

                float pushDistance = Mathf.Abs(distance.distance) + skinWidth;
                if (maxOverlapResolveDistance > 0f)
                {
                    pushDistance = Mathf.Min(pushDistance, maxOverlapResolveDistance);
                }

                rigidBody2d.position += pushDirection.normalized * pushDistance;
                resolvedAny = true;
            }

            Physics2D.SyncTransforms();
            ClearOverlapBuffer(overlapCount);

            if (!resolvedAny)
            {
                break;
            }
        }
    }

    private bool CanMove()
    {
        EnsureRuntimeDefaults();
        CacheComponents();
        return rigidBody2d != null &&
               bodyCollider != null &&
               bodyCollider.enabled &&
               !bodyCollider.isTrigger;
    }

    private void EnsureRuntimeDefaults()
    {
        if (slideIterations <= 0)
        {
            slideIterations = 2;

            if (skinWidth <= 0f)
            {
                skinWidth = 0.03f;
            }

            if (maxOverlapResolveDistance <= 0f)
            {
                maxOverlapResolveDistance = 0.2f;
            }
        }

        if (overlapResolveIterations <= 0)
        {
            overlapResolveIterations = 2;
        }

        skinWidth = Mathf.Max(0f, skinWidth);
        slideIterations = Mathf.Clamp(slideIterations, 1, 4);
        maxOverlapResolveDistance = Mathf.Max(0f, maxOverlapResolveDistance);
        overlapResolveIterations = Mathf.Clamp(overlapResolveIterations, 1, 4);
    }

    private void CacheComponents()
    {
        if (rigidBody2d == null)
        {
            rigidBody2d = GetComponent<Rigidbody2D>();
        }

        if (bodyCollider == null)
        {
            Collider2D[] colliders = GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].enabled && !colliders[i].isTrigger)
                {
                    bodyCollider = colliders[i];
                    break;
                }
            }
        }
    }

    private bool TryCast(Vector2 direction, float distance, out RaycastHit2D closestHit)
    {
        closestHit = default;

        // プレイヤー本体と同じ形状で Sweep することで、点レイキャストでは拾えない角のめり込みを防ぐ。
        int hitCount = bodyCollider.Cast(direction, solidFilter, castHits, distance);
        float closestDistance = float.PositiveInfinity;
        bool foundHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = castHits[i];
            castHits[i] = default;

            if (!IsValidHitCollider(hit.collider))
            {
                continue;
            }

            if (!IsBlockingHit(direction, hit))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                foundHit = true;
            }
        }

        ClearCastBuffer(hitCount);
        return foundHit;
    }

    private bool IsValidHitCollider(Collider2D hitCollider)
    {
        if (hitCollider == null ||
            hitCollider == bodyCollider ||
            hitCollider.isTrigger)
        {
            return false;
        }

        // 自分自身や子コライダーを壁として扱わない。
        Rigidbody2D hitRigidbody = hitCollider.attachedRigidbody;
        return hitRigidbody == null || hitRigidbody != rigidBody2d;
    }

    private bool IsBlockingHit(Vector2 direction, RaycastHit2D hit)
    {
        if (IsVerticalSideContact(hit))
        {
            return IsMovingIntoVerticalSide(direction, hit.collider);
        }

        Vector2 normal = hit.normal;
        if (normal.sqrMagnitude <= MinMoveDistance * MinMoveDistance)
        {
            return true;
        }

        normal.Normalize();
        return Vector2.Dot(direction, normal) < BlockingNormalDotThreshold;
    }

    private bool IsVerticalSideContact(RaycastHit2D hit)
    {
        if (hit.collider == null || bodyCollider == null)
        {
            return false;
        }

        Bounds bodyBounds = bodyCollider.bounds;
        Bounds hitBounds = hit.collider.bounds;
        float verticalOverlap =
            Mathf.Min(bodyBounds.max.y, hitBounds.max.y) -
            Mathf.Max(bodyBounds.min.y, hitBounds.min.y);

        if (verticalOverlap <= MinMoveDistance)
        {
            return false;
        }

        float rightSideGap = Mathf.Abs(bodyBounds.max.x - hitBounds.min.x);
        float leftSideGap = Mathf.Abs(hitBounds.max.x - bodyBounds.min.x);
        float sideContactTolerance = skinWidth + 0.01f;

        if (Mathf.Min(rightSideGap, leftSideGap) <= sideContactTolerance)
        {
            return true;
        }

        return Mathf.Abs(hit.normal.x) > Mathf.Abs(hit.normal.y);
    }

    private bool IsMovingIntoVerticalSide(Vector2 direction, Collider2D hitCollider)
    {
        if (hitCollider == null || bodyCollider == null)
        {
            return false;
        }

        bool wallIsRight = hitCollider.bounds.center.x >= bodyCollider.bounds.center.x;
        return wallIsRight
            ? direction.x > MinMoveDistance
            : direction.x < -MinMoveDistance;
    }

    private void RebuildFilterIfNeeded()
    {
        if (solidLayerMask.value == 0)
        {
            solidLayerMask = BuildDefaultSolidLayerMask();
        }

        if (cachedMaskValue == solidLayerMask.value)
        {
            return;
        }

        solidFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = false
        };
        solidFilter.SetLayerMask(solidLayerMask);
        cachedMaskValue = solidLayerMask.value;
    }

    private static LayerMask BuildDefaultSolidLayerMask()
    {
        int groundLayer = LayerMask.NameToLayer(GroundLayerName);
        return groundLayer >= 0 ? 1 << groundLayer : Physics2D.DefaultRaycastLayers;
    }

    private static Vector2 ProjectOntoSurface(Vector2 delta, Vector2 normal)
    {
        if (normal.sqrMagnitude <= MinMoveDistance * MinMoveDistance)
        {
            return Vector2.zero;
        }

        normal.Normalize();
        return delta - normal * Vector2.Dot(delta, normal);
    }

    private Vector2 ProjectVelocityAwayFromCurrentSolidContacts(Vector2 velocity)
    {
        if (velocity.sqrMagnitude <= MinMoveDistance * MinMoveDistance)
        {
            return velocity;
        }

        int contactCount = bodyCollider.GetContacts(solidFilter, contactHits);
        for (int i = 0; i < contactCount; i++)
        {
            ContactPoint2D contact = contactHits[i];
            contactHits[i] = default;

            if (!IsValidHitCollider(contact.collider) && !IsValidHitCollider(contact.otherCollider))
            {
                continue;
            }

            velocity = ProjectAwayFromContactNormal(velocity, contact.normal, contact.collider, contact.otherCollider);
        }

        ClearContactBuffer(contactCount);
        return velocity;
    }

    private Vector2 ProjectAwayFromContactNormal(
        Vector2 velocity,
        Vector2 normal,
        Collider2D contactCollider,
        Collider2D otherCollider)
    {
        if (normal.sqrMagnitude <= MinMoveDistance * MinMoveDistance)
        {
            return velocity;
        }

        normal.Normalize();

        Collider2D solidCollider = contactCollider == bodyCollider ? otherCollider : contactCollider;
        if (solidCollider != null)
        {
            Vector2 awayFromSolid =
                (Vector2)bodyCollider.bounds.center -
                (Vector2)solidCollider.bounds.center;

            if (Vector2.Dot(normal, awayFromSolid) < 0f)
            {
                normal = -normal;
            }
        }

        float intoContactSpeed = Vector2.Dot(velocity, normal);
        if (intoContactSpeed >= 0f)
        {
            return velocity;
        }

        return velocity - normal * intoContactSpeed;
    }

    private Vector2 ProjectRecoilOntoSurface(Vector2 delta, RaycastHit2D hit)
    {
        if (IsVerticalSideContact(hit))
        {
            if (!IsMovingIntoVerticalSide(delta, hit.collider))
            {
                return delta;
            }

            return new Vector2(0f, delta.y);
        }

        return ProjectOntoSurface(delta, hit.normal);
    }

    private void ClearCastBuffer(int usedCount)
    {
        for (int i = usedCount; i < castHits.Length; i++)
        {
            castHits[i] = default;
        }
    }

    private void ClearContactBuffer(int usedCount)
    {
        for (int i = usedCount; i < contactHits.Length; i++)
        {
            contactHits[i] = default;
        }
    }

    private void ClearOverlapBuffer(int usedCount)
    {
        for (int i = usedCount; i < overlapHits.Length; i++)
        {
            overlapHits[i] = null;
        }
    }
}
