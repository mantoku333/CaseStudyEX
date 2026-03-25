using UnityEngine;
using UnityEngine.InputSystem;

namespace Metroidvania.Enemy
{
    /// <summary>
    /// パリィ・通常攻撃検証用の敵射撃クラス
    /// Qキー入力で弾を1発発射
    /// </summary>
    public sealed class EnemyShooter : MonoBehaviour
    {
        [Header("Bullet Settings")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float bulletSpeed = 8f;
        [SerializeField] private Vector2 fireDirection = Vector2.left;

        private void Update()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            // Qキーを押した瞬間だけ弾を発射する
            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                Fire();
            }
        }

        /// <summary>
        /// 弾を1発生成して発射
        /// </summary>
        public void Fire()
        {
            if (bulletPrefab == null)
            {
                Debug.LogWarning("Bullet Prefab が設定されていません", this);
                return;
            }

            Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;

            GameObject bulletObject = Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);

            EnemyBullet bullet = bulletObject.GetComponent<EnemyBullet>();
            if (bullet != null)
            {
                Vector2 normalizedDirection = fireDirection.sqrMagnitude > 0f
                    ? fireDirection.normalized
                    : Vector2.left;

                bullet.Initialize(normalizedDirection, bulletSpeed);
            }
        }
    }
}
