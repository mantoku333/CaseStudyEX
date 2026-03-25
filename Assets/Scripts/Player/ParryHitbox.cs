using System.Collections.Generic;
using Metroidvania.Enemy;
using UnityEngine;

public class ParryHitbox : MonoBehaviour
{
    private List<GameObject> enemyAttacks = new List<GameObject>();     //接触管理

    private void OnTriggerEnter2D(Collider2D collision)
    {
        EnemyBullet bullet = collision.GetComponent<EnemyBullet>();
        if (bullet != null)
        {
            if (!enemyAttacks.Contains(collision.gameObject))
            {
                enemyAttacks.Add(collision.gameObject);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        EnemyBullet bullet = collision.GetComponent<EnemyBullet>();
        if (bullet != null)
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
            if (enemyAttacks[i] == null || !enemyAttacks[i].activeInHierarchy)
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
        enemyAttacks.Clear();
    }
}
