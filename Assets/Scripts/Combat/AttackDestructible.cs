using UnityEngine;

[DisallowMultipleComponent]
// Shared logic: receive player attack and break when durability reaches zero.
public class AttackDestructible : MonoBehaviour, IAttackReceiver
{
    [Header("Break Settings")]
    [SerializeField, Min(1)] private int hitPoints = 1;
    [SerializeField] private GameObject breakEffectPrefab;
    [SerializeField] private AudioClip breakSe;
    [SerializeField] private bool destroyOnBreak = true;

    private bool isBroken;

    public bool IsBroken => isBroken;
    public int CurrentHitPoints => hitPoints;

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        hitPoints = Mathf.Max(1, hitPoints);
    }
#endif

    public void OnAttacked(AttackHitbox attacker, Collider2D hitCollider)
    {
        if (!CanReceiveAttack(attacker, hitCollider))
        {
            return;
        }

        ApplyDamage(1, attacker, hitCollider);
    }

    public void ApplyDamage(int damage, AttackHitbox attacker = null, Collider2D hitCollider = null)
    {
        if (isBroken || damage <= 0)
        {
            return;
        }

        hitPoints = Mathf.Max(0, hitPoints - damage);
        if (hitPoints > 0)
        {
            return;
        }

        Break(attacker, hitCollider);
    }

    protected virtual void Break(AttackHitbox attacker, Collider2D hitCollider)
    {
        isBroken = true;
        GameObject breakEffectInstance = null;

        if (breakEffectPrefab != null)
        {
            breakEffectInstance = Instantiate((Object)breakEffectPrefab, transform.position, Quaternion.identity) as GameObject;
        }

        if (breakSe != null)
        {
            AudioSource.PlayClipAtPoint(breakSe, transform.position);
        }

        OnBreakEffectSpawned(breakEffectInstance, attacker, hitCollider);
        OnBroken(attacker, hitCollider);

        if (destroyOnBreak)
        {
            Destroy(gameObject);
        }
    }

    // Override if a concrete object needs custom behavior before destroy.
    protected virtual void OnBroken(AttackHitbox attacker, Collider2D hitCollider)
    {
    }

    protected virtual void OnBreakEffectSpawned(GameObject breakEffectInstance, AttackHitbox attacker, Collider2D hitCollider)
    {
    }

    protected virtual bool CanReceiveAttack(AttackHitbox attacker, Collider2D hitCollider)
    {
        return true;
    }
}
