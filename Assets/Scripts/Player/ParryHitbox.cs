using System.Collections.Generic;
using GameName.Enemy;
using Metroidvania.Enemy;
using UnityEngine;

public class ParryHitbox : MonoBehaviour
{
    // パリィ判定に接触している敵攻撃を保持する。
    // 通常弾はEnemyBullet、LastBossの範囲攻撃はLastBossAttackParryTargetで判別する。
    private List<GameObject> enemyAttacks = new List<GameObject>();     //接触管理
    private readonly Collider2D[] overlapResults = new Collider2D[16];
    private Collider2D hitboxCollider;
    private ContactFilter2D overlapFilter;

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

    public void ScanCurrentOverlaps()
    {
        if (hitboxCollider == null || !hitboxCollider.enabled)
        {
            return;
        }

        Physics2D.SyncTransforms();
        int overlapCount = hitboxCollider.Overlap(overlapFilter, overlapResults);
        for (int i = 0; i < overlapCount; i++)
        {
            AddEnemyAttackIfNeeded(overlapResults[i]);
            overlapResults[i] = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        AddEnemyAttackIfNeeded(collision);
    }

    private void AddEnemyAttackIfNeeded(Collider2D collision)
    {
        if (collision == null || !IsEnemyAttack(collision))
        {
            return;
        }

        if (!enemyAttacks.Contains(collision.gameObject))
        {
            enemyAttacks.Add(collision.gameObject);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (IsEnemyAttack(collision))
        {
            if (enemyAttacks.Contains(collision.gameObject))
            {
                enemyAttacks.Remove(collision.gameObject);
            }
        }
    }

    /// <summary>
    /// 敵の攻撃に接触しているかを返す
    /// </summary>
    /// <returns></returns>
    public bool HasEnemyAttack()
    {
        ScanCurrentOverlaps();

        //無効をオブジェクト削除
        for (int i = enemyAttacks.Count - 1; i >= 0; i--)
        {
            if (!IsTrackedAttackActive(enemyAttacks[i]))
            {
                enemyAttacks.RemoveAt(i);
            }
        }

        if (enemyAttacks.Count > 0)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 取得した敵の攻撃のリストを返す
    /// </summary>
    /// <returns></returns>
    public List<GameObject> GetEnemyAttacks()
    {
        return enemyAttacks;
    }

    /// <summary>
    /// 敵の攻撃のリストをリセットする
    /// </summary>
    public void ClearEnemyAttacks()
    {
        // LastBossの範囲攻撃予兆は同じオブジェクトが数秒残るため、
        // 連打中も再パリィできるよう、通常弾だけリストから外す。
        for (int i = enemyAttacks.Count - 1; i >= 0; i--)
        {
            GameObject attackObject = enemyAttacks[i];
            if (attackObject == null)
            {
                enemyAttacks.RemoveAt(i);
                continue;
            }

            EnemyBullet enemyBullet = attackObject.GetComponent<EnemyBullet>();
            if (enemyBullet != null)
            {
                enemyBullet.DestroyByParry();
                enemyAttacks.RemoveAt(i);
                continue;
            }

            if (attackObject.GetComponent<LastBossAttackParryTarget>() == null)
            {
                enemyAttacks.RemoveAt(i);
            }
        }
    }

    private static bool IsEnemyAttack(Collider2D collision)
    {
        // LastBossの範囲攻撃は弾ではないため、専用マーカーも敵攻撃として扱う。
        return collision.GetComponent<EnemyBullet>() != null ||
               collision.GetComponent<LastBossAttackParryTarget>() != null;
    }

    private bool IsTrackedAttackActive(GameObject attackObject)
    {
        if (attackObject == null || !attackObject.activeInHierarchy)
        {
            return false;
        }

        if (hitboxCollider == null || !hitboxCollider.enabled)
        {
            return false;
        }

        Collider2D attackCollider = attackObject.GetComponent<Collider2D>();
        if (attackCollider == null || !attackCollider.enabled)
        {
            return false;
        }

        return hitboxCollider.Distance(attackCollider).isOverlapped;
    }
}
