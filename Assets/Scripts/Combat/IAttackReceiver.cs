using UnityEngine;

public interface IAttackReceiver
{
    void OnAttacked(AttackHitbox attacker, Collider2D hitCollider);
}
