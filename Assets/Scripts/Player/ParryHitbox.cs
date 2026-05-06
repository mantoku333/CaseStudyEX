using System.Collections.Generic;
using GameName.Enemy;
using Metroidvania.Enemy;
using UnityEngine;

public class ParryHitbox : MonoBehaviour
{
    // パリィ判定に接触している敵攻撃を保持する。
    // 通常弾はEnemyBullet、LastBossの範囲攻撃はLastBossAttackParryTargetで判別する。
    private List<GameObject> enemyAttacks = new List<GameObject>();     //接触管理

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (IsEnemyAttack(collision))
        {
            if (!enemyAttacks.Contains(collision.gameObject))
            {
                enemyAttacks.Add(collision.gameObject);
            }
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
            if (enemyAttacks[i] == null || enemyAttacks[i].GetComponent<LastBossAttackParryTarget>() == null)
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

    private static bool IsTrackedAttackActive(GameObject attackObject)
    {
        if (attackObject == null || !attackObject.activeInHierarchy)
        {
            return false;
        }

        LastBossAttackParryTarget lastBossAttack = attackObject.GetComponent<LastBossAttackParryTarget>();
        if (lastBossAttack == null)
        {
            return true;
        }

        // LastBoss予兆はオブジェクト自体を使い回すので、Colliderの有効状態で判定する。
        Collider2D attackCollider = lastBossAttack.GetComponent<Collider2D>();
        return attackCollider != null && attackCollider.enabled;
    }
}
