using Player;
using UnityEngine;

[DisallowMultipleComponent]
public class WoodenBoxCrate : BreakableCrate
{
    [SerializeField, Min(0.01f)] private float breakEffectSizeMultiplier = 1f;
    [SerializeField, Min(1f)] private float rightSideLeftLaunchMultiplier = 2f;

    [Header("Idle Sprite")]
    [SerializeField] private Texture2D idleSpriteSheetTexture;
    [SerializeField, Min(1)] private int idleFrameColumns = 5;
    [SerializeField, Min(1)] private int idleFrameRows = 3;
    [SerializeField, Min(0)] private int idleFrameIndex = 0;
    [SerializeField, Min(1f)] private float idleFramePixelsPerUnit = 100f;
    [SerializeField] private bool fitIdleSpriteToCollider = true;

    private SpriteRenderer spriteRenderer;
    private Sprite idleSprite;

    protected override void Awake()
    {
        base.Awake();
        ApplyIdleSprite();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        idleFrameColumns = Mathf.Max(1, idleFrameColumns);
        idleFrameRows = Mathf.Max(1, idleFrameRows);
        idleFrameIndex = Mathf.Max(0, idleFrameIndex);
        idleFramePixelsPerUnit = Mathf.Max(1f, idleFramePixelsPerUnit);
    }
#endif

    protected override bool CanReceiveAttack(AttackHitbox attacker, Collider2D hitCollider)
    {
        if (attacker == null)
        {
            return false;
        }

        UmbrellaController umbrellaController = attacker.GetComponentInParent<UmbrellaController>();
        if (umbrellaController == null)
        {
            return false;
        }

        return umbrellaController.GetUmbrellaState() == UmbrellaController.UmbrellaState.Closed;
    }

    protected override void OnBreakEffectSpawned(GameObject breakEffectInstance, AttackHitbox attacker, Collider2D hitCollider)
    {
        if (breakEffectInstance == null)
        {
            return;
        }

        WoodenBoxBreakEffect breakEffect = breakEffectInstance.GetComponent<WoodenBoxBreakEffect>();
        if (breakEffect == null)
        {
            return;
        }

        Vector2 targetSize = ResolveEffectTargetSize();
        breakEffect.MatchWorldSize(targetSize * Mathf.Max(0.01f, breakEffectSizeMultiplier));
        breakEffect.Launch(ResolveBreakLaunchDirection(attacker, hitCollider));
    }

    private Vector2 ResolveEffectTargetSize()
    {
        Collider2D hitCollider = GetComponent<Collider2D>();
        if (hitCollider != null)
        {
            Bounds bounds = hitCollider.bounds;
            if (bounds.size.x > 0f && bounds.size.y > 0f)
            {
                return bounds.size;
            }
        }

        return Vector2.one;
    }

    private void ApplyIdleSprite()
    {
        CacheSpriteRenderer();
        if (spriteRenderer == null || idleSpriteSheetTexture == null)
        {
            return;
        }

        if (spriteRenderer.sprite != null)
        {
            return;
        }

        WoodenBoxSpriteSheetFrames.DestroyFrame(idleSprite);
        idleSprite = CreateFittedIdleSprite();
        if (idleSprite != null)
        {
            spriteRenderer.sprite = idleSprite;
        }
    }

    private Sprite CreateFittedIdleSprite()
    {
        Sprite sourceSprite = WoodenBoxSpriteSheetFrames.CreateFrame(
            idleSpriteSheetTexture,
            idleFrameColumns,
            idleFrameRows,
            idleFrameIndex,
            idleFramePixelsPerUnit);

        if (!fitIdleSpriteToCollider || sourceSprite == null)
        {
            return sourceSprite;
        }

        Vector2 targetLocalSize = ResolveIdleTargetLocalSize();
        Vector2 sourceSize = WoodenBoxSpriteSheetFrames.GetSpriteContentWorldSize(sourceSprite);
        if (targetLocalSize.x <= 0f || targetLocalSize.y <= 0f || sourceSize.x <= 0f || sourceSize.y <= 0f)
        {
            return sourceSprite;
        }

        float fitScale = Mathf.Max(sourceSize.x / targetLocalSize.x, sourceSize.y / targetLocalSize.y);
        if (fitScale <= 0f || Mathf.Approximately(fitScale, 1f))
        {
            return sourceSprite;
        }

        WoodenBoxSpriteSheetFrames.DestroyFrame(sourceSprite);
        return WoodenBoxSpriteSheetFrames.CreateFrame(
            idleSpriteSheetTexture,
            idleFrameColumns,
            idleFrameRows,
            idleFrameIndex,
            idleFramePixelsPerUnit * fitScale);
    }

    private Vector2 ResolveIdleTargetLocalSize()
    {
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null && boxCollider.size.x > 0f && boxCollider.size.y > 0f)
        {
            return boxCollider.size;
        }

        Collider2D hitCollider = GetComponent<Collider2D>();
        if (hitCollider != null)
        {
            Vector3 scale = transform.lossyScale;
            Bounds bounds = hitCollider.bounds;
            float x = Mathf.Abs(scale.x) > 0.0001f ? bounds.size.x / Mathf.Abs(scale.x) : bounds.size.x;
            float y = Mathf.Abs(scale.y) > 0.0001f ? bounds.size.y / Mathf.Abs(scale.y) : bounds.size.y;
            if (x > 0f && y > 0f)
            {
                return new Vector2(x, y);
            }
        }

        return Vector2.one;
    }

    private Vector2 ResolveBreakLaunchDirection(AttackHitbox attacker, Collider2D hitCollider)
    {
        if (attacker == null)
        {
            return Vector2.zero;
        }

        Collider2D targetCollider = hitCollider != null ? hitCollider : GetComponent<Collider2D>();
        Vector2 targetCenter = targetCollider != null ? targetCollider.bounds.center : transform.position;
        if (TryResolvePlayerPosition(attacker, out Vector2 playerPosition))
        {
            float playerDeltaX = playerPosition.x - targetCenter.x;
            if (Mathf.Abs(playerDeltaX) > 0.01f)
            {
                return playerDeltaX > 0f
                    ? Vector2.left * rightSideLeftLaunchMultiplier
                    : Vector2.right;
            }
        }

        Vector2 directionFromPlayer = targetCenter - attacker.AttackOriginPosition;
        if (Mathf.Abs(directionFromPlayer.x) > 0.01f)
        {
            return CreateHorizontalLaunchDirection(Mathf.Sign(directionFromPlayer.x));
        }

        Collider2D attackerCollider = attacker.GetComponent<Collider2D>();
        if (attackerCollider != null && targetCollider != null)
        {
            Vector2 directionFromAttackHitbox = targetCenter - (Vector2)attackerCollider.bounds.center;
            if (Mathf.Abs(directionFromAttackHitbox.x) > 0.01f)
            {
                return CreateHorizontalLaunchDirection(Mathf.Sign(directionFromAttackHitbox.x));
            }
        }

        IPlayerViewStateProvider viewStateProvider = attacker.GetComponentInParent<IPlayerViewStateProvider>();
        if (viewStateProvider != null)
        {
            return viewStateProvider.IsFacingRight ? Vector2.right : Vector2.left * rightSideLeftLaunchMultiplier;
        }

        Vector2 attackOrigin = attacker.AttackOriginPosition;
        Vector2 direction = (Vector2)transform.position - attackOrigin;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = (Vector2)transform.position - (Vector2)attacker.transform.position;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return Vector2.zero;
        }

        if (Mathf.Abs(direction.x) > 0.01f)
        {
            return CreateHorizontalLaunchDirection(Mathf.Sign(direction.x));
        }

        return direction.normalized;
    }

    private bool TryResolvePlayerPosition(AttackHitbox attacker, out Vector2 playerPosition)
    {
        playerPosition = Vector2.zero;
        if (attacker == null)
        {
            return false;
        }

        IPlayerViewStateProvider viewStateProvider = attacker.GetComponentInParent<IPlayerViewStateProvider>();
        if (viewStateProvider is Component viewStateComponent)
        {
            playerPosition = viewStateComponent.transform.position;
            return true;
        }

        Rigidbody2D ownerRigidbody = attacker.GetComponentInParent<Rigidbody2D>();
        if (ownerRigidbody != null)
        {
            playerPosition = ownerRigidbody.position;
            return true;
        }

        return false;
    }

    private Vector2 CreateHorizontalLaunchDirection(float directionX)
    {
        if (directionX < 0f)
        {
            return Vector2.left * rightSideLeftLaunchMultiplier;
        }

        if (directionX > 0f)
        {
            return Vector2.right;
        }

        return Vector2.zero;
    }

    private void CacheSpriteRenderer()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void OnDestroy()
    {
        WoodenBoxSpriteSheetFrames.DestroyFrame(idleSprite);
    }
}
