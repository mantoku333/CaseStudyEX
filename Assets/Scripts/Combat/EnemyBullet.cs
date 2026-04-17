using Metroidvania.Player;
using Player;
using UnityEngine;

namespace Metroidvania.Enemy
{
    /// <summary>
    /// 敵が発射する弾の挙動を管理するクラス
    /// プレイヤーに当たった時、プレイヤーへ被弾演出を出して自身を消す
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyBullet : MonoBehaviour
    {
        [Header("Lifetime")]
        [SerializeField, Min(0.1f)] private float lifeTime = 5f;

        [Header("Collision")]
        [SerializeField] private LayerMask destroyOnHitLayers = ~0;

        private Rigidbody2D rb2D;
        private bool initialized;

        private void Awake()
        {
            rb2D = GetComponent<Rigidbody2D>();
        }

        private void Start()
        {
            Destroy(gameObject, lifeTime);
        }

        /// <summary>
        /// 発射方向と速度を設定
        /// </summary>
        /// <param name="direction">移動方向</param>
        /// <param name="speed">移動速度</param>
        public void Initialize(Vector2 direction, float speed)
        {
            rb2D.linearVelocity = direction.normalized * speed;
            initialized = true;

            // 弾の見た目を進行方向へ向ける
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.right = direction;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // プレイヤーに当たったら赤フラッシュを出して消える
            if (other.TryGetComponent<PlayerDamageFlash>(out PlayerDamageFlash damageFlash))
            {
                damageFlash.PlayFlash();
                Destroy(gameObject);
                return;
            }

            // 親にPlayerDamageFlashが付いている場合にも対応
            PlayerDamageFlash flashInParent = other.GetComponentInParent<PlayerDamageFlash>();
            if (flashInParent != null)
            {
                flashInParent.PlayFlash();
                Destroy(gameObject);
                return;
            }

            // それ以外でも指定レイヤーに当たったら消す
            if (IsInLayerMask(other.gameObject.layer, destroyOnHitLayers))
            {
                Destroy(gameObject);
            }

            if (other.gameObject.CompareTag("Player"))
            {
                //PlayerHealthコンポーネントを取得
                PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();

                //PlayerHealthコンポーネントが取得できない場合、親オブジェクトから取得を試みる
                if (playerHealth == null)
                {
                    playerHealth = other.GetComponentInParent<PlayerHealth>();
                }

                if (playerHealth == null) { return; }

                //ダメージ
                playerHealth.TakeDamage(1);

                Debug.Log($"Playerがダメージを受けました　HP: {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}");
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.gameObject.TryGetComponent<PlayerDamageFlash>(out PlayerDamageFlash damageFlash))
            {
                damageFlash.PlayFlash();
                Destroy(gameObject);
                return;
            }

            PlayerDamageFlash flashInParent = collision.gameObject.GetComponentInParent<PlayerDamageFlash>();
            if (flashInParent != null)
            {
                flashInParent.PlayFlash();
                Destroy(gameObject);
                return;
            }

            if (IsInLayerMask(collision.gameObject.layer, destroyOnHitLayers))
            {
                Destroy(gameObject);
            }
        }

        private static bool IsInLayerMask(int layer, LayerMask layerMask)
        {
            return (layerMask.value & (1 << layer)) != 0;
        }
    }
}
