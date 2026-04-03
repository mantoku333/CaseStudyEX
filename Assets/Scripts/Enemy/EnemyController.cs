using Player;
using UnityEngine;

namespace GameName.Enemy
{
    /// <summary>
    /// シンプルな敵の巡回移動と接触ダメージを管理するクラス
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float patrolDistance = 2f;
        [SerializeField] private int damageToPlayer = 1;

        private Vector3 startPosition;
        private int moveDirection = 1;

        /// <summary>
        /// 初期位置を保存
        /// </summary>
        private void Start()
        {
            startPosition = transform.position;
        }

        /// <summary>
        /// 左右の簡易巡回を行う
        /// </summary>
        private void Update()
        {
            // 現在向いている方向へ移動
            transform.Translate(Vector3.right * moveDirection * moveSpeed * Time.deltaTime);

            // 開始位置から一定距離離れたら反転する
            float distanceFromStart = transform.position.x - startPosition.x;
            if (Mathf.Abs(distanceFromStart) >= patrolDistance)
            {
                moveDirection *= -1;
            }
        }

        /// <summary>
        /// プレイヤーに接触した際にダメージを与える
        /// </summary>
        /// <param name="collision">衝突情報</param>
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.gameObject.TryGetComponent<PlayerHealth>(out PlayerHealth playerHealth))
            {
                playerHealth.TakeDamage(damageToPlayer);
            }
        }
    }
}
