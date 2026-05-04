using UnityEngine;

[DisallowMultipleComponent]
public class WoodenBoxCrate : BreakableCrate
{
    [SerializeField, Min(0.01f)] private float breakEffectSizeMultiplier = 1f;

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
    }

    private Vector2 ResolveEffectTargetSize()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            Bounds bounds = spriteRenderer.bounds;
            if (bounds.size.x > 0f && bounds.size.y > 0f)
            {
                return bounds.size;
            }
        }

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
}
