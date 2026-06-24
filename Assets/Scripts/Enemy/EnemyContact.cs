using Metroidvania.Player;
using Player;
using System;
using UnityEngine;

namespace Metroidvania.Enemy
{
    /// <summary>
    /// 敵本体にプレイヤーが接触した際、
    /// プレイヤーへ被弾演出を出すためのクラス
    /// </summary>
    public sealed class EnemyContact : MonoBehaviour
    {
        [Header("Pass Through")]
        [SerializeField] private bool passThroughPlayer = true;
        [SerializeField] private string playerTag = "Player";

        [Header("Contact Hit")]
        [SerializeField, Min(0.05f)] private float hitInterval = 0.2f;
        [SerializeField] private bool applyDamageInPassThrough = true;
        [SerializeField, Min(1)] private int contactDamage = 1;

        private Collider2D[] enemyColliders;
        private Collider2D[] playerColliders;
        private Collider2D playerBodyCollider;
        private PlayerDamageFlash cachedPlayerFlash;
        private PlayerHealth cachedPlayerHealth;
        private float nextHitTime;

        private GameName.Enemy.EnemyController enemyController;　　//(中江)

        public event Action<PlayerHealth, Collider2D> ContactDamageApplied;

        public void ReapplyPassThroughPlayerCollision()
        {
            if (!passThroughPlayer)
            {
                return;
            }

            RefreshEnemyColliders();
            CachePlayerReferences();
            IgnorePhysicalCollisionWithPlayer();
        }

        private void Awake()
        {
            RefreshEnemyColliders();
            ResolveEnemyController();
            CachePlayerReferences();

            if (passThroughPlayer)
            {
                IgnorePhysicalCollisionWithPlayer();
            }
        }

        private void OnEnable()
        {
            if (!passThroughPlayer)
            {
                return;
            }

            RefreshEnemyColliders();
            CachePlayerReferences();
            IgnorePhysicalCollisionWithPlayer();
        }

        private void Start()
        {
            if (passThroughPlayer)
            {
                IgnorePhysicalCollisionWithPlayer();
            }
        }

        private void FixedUpdate()
        {
            if (!passThroughPlayer)
            {
                return;
            }

            if (GameName.Enemy.EnemyGameplayPause.IsPaused())
            {
                return;
            }

            if (!EnsurePlayerReferences())
            {
                return;
            }

            if (Time.time < nextHitTime)
            {
                return;
            }

            if (!IsOverlappingPlayer())
            {
                return;
            }

            ApplyContactHit();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (passThroughPlayer)
            {
                return;
            }

            TryPlayBodyHitFlash(collision);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (passThroughPlayer)
            {
                return;
            }

            TryPlayBodyHitFlash(other);
        }

        private bool EnsurePlayerReferences()
        {
            if (playerColliders != null && playerColliders.Length > 0 && playerBodyCollider != null &&
                (cachedPlayerFlash != null || cachedPlayerHealth != null))
            {
                return true;
            }

            CachePlayerReferences();
            IgnorePhysicalCollisionWithPlayer();

            return playerColliders != null && playerColliders.Length > 0 &&
                   playerBodyCollider != null &&
                   (cachedPlayerFlash != null || cachedPlayerHealth != null);
        }

        private void RefreshEnemyColliders()
        {
            enemyColliders = GetComponents<Collider2D>();
        }

        private void CachePlayerReferences()
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player == null)
            {
                return;
            }

            playerColliders = player.GetComponentsInChildren<Collider2D>(true);
            cachedPlayerHealth = player.GetComponent<PlayerHealth>();
            if (cachedPlayerHealth == null)
            {
                cachedPlayerHealth = player.GetComponentInChildren<PlayerHealth>(true);
            }

            // 接触ダメージの重なり判定は、子コライダーではなくプレイヤー本体だけを見る。
            PlayerBodyColliderUtility.TryGetBodyCollider(cachedPlayerHealth, out playerBodyCollider);
            cachedPlayerFlash = ResolvePlayerDamageFlash(cachedPlayerHealth);
        }

        private void IgnorePhysicalCollisionWithPlayer()
        {
            if (enemyColliders == null || playerColliders == null)
            {
                return;
            }

            for (int i = 0; i < enemyColliders.Length; i++)
            {
                Collider2D enemyCollider = enemyColliders[i];
                if (enemyCollider == null || !enemyCollider.enabled || enemyCollider.isTrigger)
                {
                    continue;
                }

                for (int j = 0; j < playerColliders.Length; j++)
                {
                    Collider2D playerCollider = playerColliders[j];
                    if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger)
                    {
                        continue;
                    }

                    Physics2D.IgnoreCollision(enemyCollider, playerCollider, true);
                }
            }
        }

        private bool IsOverlappingPlayer()
        {
            if (enemyColliders == null || playerBodyCollider == null ||
                !playerBodyCollider.enabled || playerBodyCollider.isTrigger)
            {
                return false;
            }

            // passThroughPlayer 中も、ダメージ判定は本体コライダーとの重なりだけに限定する。
            for (int i = 0; i < enemyColliders.Length; i++)
            {
                Collider2D enemyCollider = enemyColliders[i];
                if (enemyCollider == null || !enemyCollider.enabled || enemyCollider.isTrigger)
                {
                    continue;
                }

                if (enemyCollider.Distance(playerBodyCollider).isOverlapped)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyContactHit()
        {
            if (GameName.Enemy.EnemyGameplayPause.IsPaused())
            {
                return;
            }

            if (enemyController != null && enemyController.IsContactDamageIgnored())
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[EnemyContact] パリィ後なので接触ダメージ無効 frame={Time.frameCount}");
#endif
                return;
            }

            bool didDamage = false;
            if (applyDamageInPassThrough && cachedPlayerHealth != null)
            {
                didDamage = cachedPlayerHealth.TryTakeDamage(ResolveContactDamage());

            }

            // フラッシュは HP クールダウンを通過して、実際にダメージが入った時だけ再生する。
            if (didDamage)
            {
                HitStopController.RequestEnemyToPlayer();
                cachedPlayerFlash?.PlayFlashForced();
                ContactDamageApplied?.Invoke(cachedPlayerHealth, playerBodyCollider);
            }

            nextHitTime = Time.time + hitInterval;
        }

        private static PlayerDamageFlash ResolvePlayerDamageFlash(PlayerHealth playerHealth)
        {
            if (playerHealth == null)
            {
                return null;
            }

            PlayerDamageFlash damageFlash = playerHealth.GetComponent<PlayerDamageFlash>();
            if (damageFlash != null)
            {
                return damageFlash;
            }

            return playerHealth.GetComponentInChildren<PlayerDamageFlash>(true);
        }

        private int ResolveContactDamage()
        {
            ResolveEnemyController();
            return enemyController != null ? enemyController.DamageToPlayer : contactDamage;
        }

        private void ResolveEnemyController()
        {
            if (enemyController != null)
            {
                return;
            }

            enemyController = GetComponent<GameName.Enemy.EnemyController>();
        }

        private static bool TryGetBodyHitFlash(Collider2D hitCollider, out PlayerDamageFlash damageFlash)
        {
            damageFlash = null;

            // 物理接触モードでも、傘や攻撃判定に触れただけでは被弾演出を出さない。
            if (!PlayerBodyColliderUtility.TryGetPlayerBodyFromCollider(
                    hitCollider,
                    out PlayerHealth playerHealth,
                    out _))
            {
                return false;
            }

            damageFlash = ResolvePlayerDamageFlash(playerHealth);
            return damageFlash != null;
        }

        private static void TryPlayBodyHitFlash(Collision2D collision)
        {
            if (collision == null)
            {
                return;
            }

            if (TryGetBodyHitFlash(collision.collider, out PlayerDamageFlash damageFlash) ||
                TryGetBodyHitFlash(collision.otherCollider, out damageFlash))
            {
                damageFlash.PlayFlash();
            }
        }

        private static void TryPlayBodyHitFlash(Collider2D hitCollider)
        {
            if (TryGetBodyHitFlash(hitCollider, out PlayerDamageFlash damageFlash))
            {
                damageFlash.PlayFlash();
            }
        }
    }
}
