using System.Collections.Generic;
using Player;
using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    private readonly HashSet<MonoBehaviour> hitReceivers = new HashSet<MonoBehaviour>();
    private readonly Collider2D[] overlapResults = new Collider2D[16];
    private Collider2D hitboxCollider;
    private ContactFilter2D overlapFilter;
    private PlayerStatsData statsData;

    public int PlayerAttackDamage => statsData != null
        ? statsData.PlayerAttackDamage
        : 0;

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider2D>();
        overlapFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        overlapFilter.SetLayerMask(Physics2D.AllLayers);
    }

    public void ResetHitState()
    {
        hitReceivers.Clear();
    }

    public void SetPlayerStatsData(PlayerStatsData playerData)
    {
        statsData = playerData;
    }

    public void ScanCurrentOverlaps()
    {
        if (hitboxCollider == null || !hitboxCollider.enabled)
        {
            return;
        }

        int overlapCount = hitboxCollider.Overlap(overlapFilter, overlapResults);
        for (int i = 0; i < overlapCount; i++)
        {
            ProcessHit(overlapResults[i]);
            overlapResults[i] = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        ProcessHit(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        ProcessHit(collision);
    }

    private void ProcessHit(Collider2D collision)
    {
        if (collision == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = collision.GetComponentsInParent<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IAttackReceiver receiver && hitReceivers.Add(behaviours[i]))
            {
                receiver.OnAttacked(this, collision);
            }
        }
    }
}
