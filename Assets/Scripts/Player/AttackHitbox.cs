using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    [Header("攻撃力")]
    [SerializeField] private int damage = 1;　//攻撃力

    private void OnTriggerEnter2D(Collider2D collision)
    {
        //敵に当たったらダメージを与える処理
        //まだ判定を決められていないので、とりあえずFinishタグで管理
        //後で変更する
        // Enemyに当たったらダメージ
        //if (collision.CompareTag("Finish"))
        //{
        //    Debug.Log("Enemy Hit!");
        //}
    }
}
