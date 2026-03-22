using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    [Header("攻撃力")]
    [SerializeField] private int damage = 1;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Enemyに当たったらダメージ
        if (collision.CompareTag("Finish"))
        {
            Debug.Log("Enemy Hit!");
        }
    }
}
