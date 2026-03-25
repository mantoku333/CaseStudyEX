using System.Collections;
using UnityEngine;

namespace GameName.Player
{
    /// <summary>
    /// プレイヤーの射撃、反動、リロードを管理するクラス
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerShooter2D : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private PlayerStatsData statsData;

        [Header("Shot Settings")]
        [SerializeField] private Transform firePoint;
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private float bulletSpeed = 15f;
        [SerializeField, Min(1)] private int magazineSize = 6;

        private Rigidbody2D rb;
        private float nextFireTime;
        private bool isReloading;
        private int currentAmmo;

        /// <summary>
        /// 初期化
        /// </summary>
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            // ゲーム開始時は弾を満タンにしておく
            currentAmmo = magazineSize;
        }

        /// <summary>
        /// 入力を監視し、射撃とリロードを受け付け
        /// </summary>
        private void Update()
        {
            // ステータス未設定なら処理しない
            if (statsData == null)
            {
                return;
            }

            // 攻撃ボタンを押している間、発射を試みる
            if (Input.GetButton("Fire1"))
            {
                TryFire();
            }

            // Rキーで手動リロード
            if (Input.GetKeyDown(KeyCode.R))
            {
                StartReload();
            }
        }

        /// <summary>
        /// 発射可能かを確認し、可能なら弾を撃つ
        /// </summary>
        private void TryFire()
        {
            // リロード中は撃てない
            if (isReloading)
            {
                return;
            }

            // 攻撃速度制限
            // 次の発射可能時刻より前なら撃てない
            if (Time.time < nextFireTime)
            {
                return;
            }

            // 残弾が無ければ自動リロード
            if (currentAmmo <= 0)
            {
                StartReload();
                return;
            }

            // 1発消費
            currentAmmo--;

            // 攻撃速度から次に撃てる時刻を計算
            // 例: 4発/秒なら 0.25秒ごとに撃てる
            nextFireTime = Time.time + (1f / statsData.AttackPerSecond);

            // プレイヤーの向いている方向を発射方向にする
            Vector2 fireDirection = transform.lossyScale.x >= 0f ? Vector2.right : Vector2.left;

            // FirePoint が指定されていればそこから発射、無ければ自分の位置から発射
            Vector3 spawnPosition = firePoint != null ? firePoint.position : transform.position;

            // 弾Prefabが設定されている場合だけ生成
            if (bulletPrefab != null)
            {
                GameObject bullet = Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);

                // 見た目の向きを発射方向に合わせる
                bullet.transform.right = fireDirection;

                // Rigidbody2D が付いている弾なら速度を与える
                if (bullet.TryGetComponent<Rigidbody2D>(out Rigidbody2D bulletRb))
                {
                    bulletRb.linearVelocity = fireDirection * bulletSpeed;
                }
            }

            // 発射時にプレイヤー自身へ逆方向の反動を加える
            rb.AddForce(-fireDirection * statsData.GunRecoilForce, ForceMode2D.Impulse);
        }

        /// <summary>
        /// リロード処理を開始
        /// </summary>
        private void StartReload()
        {
            // すでにリロード中でなければ開始
            if (!isReloading)
            {
                StartCoroutine(ReloadCoroutine());
            }
        }

        /// <summary>
        /// 一定時間待機して弾数を満タンに戻す
        /// </summary>
        /// <returns>コルーチン</returns>
        private IEnumerator ReloadCoroutine()
        {
            isReloading = true;

            // リロード時間だけ待機
            yield return new WaitForSeconds(statsData.ReloadSeconds);

            // 弾を満タンに戻す
            currentAmmo = magazineSize;

            isReloading = false;
        }
    }
}
