using GameName.Enemy;
using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    //[Header("攻撃力")]
    //[SerializeField] private int damage = 1;　//攻撃力

    private void OnTriggerEnter2D(Collider2D collision)
    {
        EnemyController enemy = collision.GetComponent<EnemyController>();

        if (enemy != null)
        {
            Debug.Log("敵に当たりました");
            Destroy(collision.gameObject); 
        }
    }
}
