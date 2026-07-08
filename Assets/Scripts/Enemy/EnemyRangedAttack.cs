using Metroidvania.Enemy;
using Player;
using System;
using UnityEngine;

namespace GameName.Enemy
{
    /// <summary>
    /// 遠距離敵専用の射撃攻撃ロジック。
    /// EnemyController と分離しているため、
    /// アタッチ／デタッチだけで攻撃挙動を差し替えられる。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    public sealed class EnemyRangedAttack : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField, Min(0.1f)] private float detectionRadius = 10f;

        [Header("Wind Up")]
        [SerializeField, Min(0.1f)] private float windupDuration = 2f;
        [SerializeField, Min(0f)] private float vibrationAmplitude = 0.05f;
        [SerializeField, Min(1f)] private float vibrationFrequency = 70f;

        [Header("Projectile")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField, Min(0.1f)] private float bulletSpeed = 3f;
        [SerializeField, Min(1)] private int projectileDamage = 10;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 10f;
        [SerializeField, Min(0f)] private float projectileGuidanceDuration = 1f;
        [SerializeField] private LayerMask projectileObstacleMask;
        [SerializeField, Min(0.01f)] private float fireVisualDuration = 0.15f;

        [Header("Debug")]
        [SerializeField] private bool drawDetectionGizmo = true;

        private enum AttackState
        {
            Idle,
            Windup,
            Fire
        }

        private EnemyController enemyController;
        private EnemyRangedWindupEffectPlayer windupEffectPlayer;
        private Collider2D bodyCollider;
        private Transform playerTransform;

        private AttackState attackState = AttackState.Idle;
        private float stateTimer;
        private float vibrationBaseX;
        private float vibrationElapsed;

        /// <summary>
        /// 弾の生成に成功したタイミングで通知する。遠距離攻撃SEの再生に使う。
        /// </summary>
        public event Action ProjectileFired;
        public event Action<Transform, float> WindupStarted;
        public event Action WindupEnded;

        public bool IsWindingUp => attackState == AttackState.Windup;
        public bool IsFiring => attackState == AttackState.Fire;

        private void Awake()
        {
            enemyController = GetComponent<EnemyController>();
            windupEffectPlayer = GetComponent<EnemyRangedWindupEffectPlayer>();
            bodyCollider = GetComponent<Collider2D>();

            if (enemyController == null)
            {
                Debug.LogWarning("EnemyRangedAttack requires EnemyController on the same GameObject.", this);
                enabled = false;
                return;
            }

            if (projectileObstacleMask.value == 0)
            {
                projectileObstacleMask = BuildDefaultObstacleMask();
            }
        }

        private void OnValidate()
        {
            detectionRadius = Mathf.Max(0.1f, detectionRadius);
            windupDuration = Mathf.Max(0.1f, windupDuration);
            bulletSpeed = Mathf.Max(0.1f, bulletSpeed);
            projectileDamage = Mathf.Max(1, projectileDamage);
            projectileLifetime = Mathf.Max(0.1f, projectileLifetime);
            projectileGuidanceDuration = Mathf.Max(0f, projectileGuidanceDuration);
            fireVisualDuration = Mathf.Max(0.01f, fireVisualDuration);

            if (projectileObstacleMask.value == 0)
            {
                projectileObstacleMask = BuildDefaultObstacleMask();
            }
        }

        private void OnEnable()
        {
            PauseEnemyMovement();
            attackState = AttackState.Idle;
            stateTimer = 0f;
        }

        private void OnDisable()
        {
            if (enemyController == null)
            {
                return;
            }

            enemyController.PauseMovement(false);
            enemyController.StopHorizontalMotion();
            if (attackState == AttackState.Windup)
            {
                WindupEnded?.Invoke();
            }

            attackState = AttackState.Idle;
            stateTimer = 0f;
        }

        private void Start()
        {
            CachePlayerTransform();
            PauseEnemyMovement();
        }

        private void FixedUpdate()
        {
            PauseEnemyMovement();
            if (EnemyGameplayPause.IsPaused())
            {
                return;
            }

            EnsurePlayerTransform();

            switch (attackState)
            {
                case AttackState.Idle:
                    UpdateIdleState();
                    break;
                case AttackState.Windup:
                    UpdateWindupState();
                    break;
                case AttackState.Fire:
                    UpdateFireState();
                    break;
            }
        }

        private void UpdateIdleState()
        {
            enemyController.StopHorizontalMotion();

            // プレイヤーが検知範囲に入ったら、攻撃準備前にプレイヤーの方向へ向きを合わせる。
            if (!IsPlayerInDetectionRadius())
            {
                return;
            }

            EnterWindupState();
        }

        private void EnterWindupState()
        {
            PauseEnemyMovement();
            enemyController.StopHorizontalMotion();
            FacePlayer();

            attackState = AttackState.Windup;
            stateTimer = windupDuration;
            vibrationElapsed = 0f;
            vibrationBaseX = enemyController.CurrentX;
            WindupStarted?.Invoke(firePoint != null ? firePoint : transform, windupDuration);
        }

        private void UpdateWindupState()
        {
            // 予備動作を開始した向きを保ったまま、プレイヤーが検知範囲内にいるかだけ確認する。
            if (!IsPlayerInDetectionRadius())
            {
                EnterIdleState();
                return;
            }

            stateTimer -= Time.fixedDeltaTime;
            vibrationElapsed += Time.fixedDeltaTime;

            float offset = Mathf.Sin(vibrationElapsed * vibrationFrequency * Mathf.PI * 2f) * vibrationAmplitude;
            enemyController.SetHorizontalPosition(vibrationBaseX + offset);
            enemyController.StopHorizontalMotion();

            if (stateTimer <= 0f)
            {
                EnterFireState();
            }
        }

        private void EnterFireState()
        {
            // 予備動作を開始した向きを保ったまま発射する。
            enemyController.SetHorizontalPosition(vibrationBaseX);
            enemyController.StopHorizontalMotion();
            WindupEnded?.Invoke();

            FireProjectile();

            attackState = AttackState.Fire;
            stateTimer = fireVisualDuration;
        }

        private void UpdateFireState()
        {
            stateTimer -= Time.fixedDeltaTime;
            enemyController.StopHorizontalMotion();
            // 発射演出中も向きを保ち、次の予備動作を開始するときだけ向きを更新する。
            bool playerInRange = IsPlayerInDetectionRadius();

            if (stateTimer > 0f)
            {
                return;
            }

            if (playerInRange)
            {
                EnterWindupState();
                return;
            }

            EnterIdleState();
        }

        private void EnterIdleState()
        {
            if (attackState == AttackState.Windup)
            {
                enemyController.SetHorizontalPosition(vibrationBaseX);
                WindupEnded?.Invoke();
            }

            attackState = AttackState.Idle;
            stateTimer = 0f;
            enemyController.StopHorizontalMotion();
        }

        private void FireProjectile()
        {
            if (bulletPrefab == null || playerTransform == null)
            {
                return;
            }

            Vector3 spawnPosition = ResolveProjectileSpawnPosition();

            GameObject bulletObject = Instantiate(bulletPrefab, spawnPosition, Quaternion.identity);
            EnemyBullet bullet = bulletObject.GetComponent<EnemyBullet>();
            if (bullet == null)
            {
                Rigidbody2D bulletRigidbody = bulletObject.GetComponent<Rigidbody2D>();
                if (bulletRigidbody != null)
                {
                    Vector2 fallbackDirection = ((Vector2)playerTransform.position - (Vector2)spawnPosition).normalized;
                    bulletRigidbody.linearVelocity = fallbackDirection * bulletSpeed;
                }

                // EnemyBulletが付いていない弾でも、Rigidbody2Dで発射できた場合は攻撃SEを鳴らす。
                ProjectileFired?.Invoke();
                return;
            }

            bullet.Initialize(
                playerTransform,
                transform,
                detectionRadius,
                bulletSpeed,
                projectileDamage,
                projectileLifetime,
                projectileObstacleMask,
                true,
                projectileGuidanceDuration,
                Vector2.right * enemyController.FacingDirection);

            // 弾の初期化完了後に通知し、実際に発射できた攻撃だけSE対象にする。
            ProjectileFired?.Invoke();
        }

        private Vector3 GetBodyCenter()
        {
            return bodyCollider != null ? bodyCollider.bounds.center : transform.position;
        }

        private Vector3 ResolveProjectileSpawnPosition()
        {
            Transform spawnAnchor = firePoint != null ? firePoint : transform;
            if (windupEffectPlayer != null)
            {
                return windupEffectPlayer.ResolveEffectWorldPosition(spawnAnchor);
            }

            return firePoint != null ? firePoint.position : GetBodyCenter();
        }

        private bool IsPlayerInDetectionRadius()
        {
            if (playerTransform == null)
            {
                return false;
            }

            Vector2 enemyPosition = transform.position;
            Vector2 playerPosition = playerTransform.position;
            float radius = Mathf.Max(0.1f, detectionRadius);
            return (playerPosition - enemyPosition).sqrMagnitude <= radius * radius;
        }

        /// <summary>
        /// プレイヤーの左右位置に合わせて EnemyController の向きを更新する。
        /// </summary>
        private void FacePlayer()
        {
            if (playerTransform == null || enemyController == null)
            {
                return;
            }

            float deltaX = playerTransform.position.x - transform.position.x;
            if (Mathf.Abs(deltaX) <= 0.01f)
            {
                return;
            }

            enemyController.FaceDirection(deltaX >= 0f ? 1 : -1);
        }

        private void PauseEnemyMovement()
        {
            if (enemyController == null)
            {
                return;
            }

            enemyController.PauseMovement(true);
            enemyController.StopHorizontalMotion();
        }

        private bool EnsurePlayerTransform()
        {
            if (playerTransform != null)
            {
                return true;
            }

            CachePlayerTransform();
            return playerTransform != null;
        }

        private void CachePlayerTransform()
        {
            GameObject playerObject = null;
            if (!string.IsNullOrEmpty(playerTag))
            {
                playerObject = GameObject.FindGameObjectWithTag(playerTag);
            }

            if (playerObject != null)
            {
                playerTransform = playerObject.transform;
                return;
            }

            PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
            playerTransform = playerHealth != null ? playerHealth.transform : null;
        }

        private static LayerMask BuildDefaultObstacleMask()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            int fallThroughLayer = LayerMask.NameToLayer("FallThroughFloor");

            int mask = 0;
            if (groundLayer >= 0)
            {
                mask |= 1 << groundLayer;
            }

            if (fallThroughLayer >= 0)
            {
                mask |= 1 << fallThroughLayer;
            }

            return mask;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDetectionGizmo)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, detectionRadius));
        }
    }
}
