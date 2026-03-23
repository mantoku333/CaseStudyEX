using UnityEngine;
using System.Collections.Generic;

public class ParryHitbox : MonoBehaviour
{
    //--------------接触管理------------------
    private List<GameObject> enemyAttacks = new List<GameObject>();

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Finish"))
        {
            if (!enemyAttacks.Contains(collision.gameObject))
            {
                enemyAttacks.Add(collision.gameObject);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Finish"))
        {
            if (enemyAttacks.Contains(collision.gameObject))
            {
                enemyAttacks.Remove(collision.gameObject);
            }
        }
    }

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

    public List<GameObject> GetEnemyAttacks()
    {
        return enemyAttacks;
    }
    
    public void ClearEnemyAttacks()
    {
        enemyAttacks.Clear();
    }
}
