using Metroidvania.Player;
using Player;
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
        private PlayerDamageFlash cachedPlayerFlash;
        private PlayerHealth cachedPlayerHealth;
        private float nextHitTime;

        private void Awake()
        {
            enemyColliders = GetComponents<Collider2D>();
            CachePlayerReferences();

            if (passThroughPlayer)
            {
                IgnorePhysicalCollisionWithPlayer();
            }
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

            if (!EnsurePlayerReferences())
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

            if (collision.gameObject.TryGetComponent<PlayerDamageFlash>(out PlayerDamageFlash damageFlash))
            {
                damageFlash.PlayFlash();
                return;
            }

            PlayerDamageFlash flashInParent = collision.gameObject.GetComponentInParent<PlayerDamageFlash>();
            if (flashInParent != null)
            {
                flashInParent.PlayFlash();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (passThroughPlayer)
            {
                return;
            }

            if (other.TryGetComponent<PlayerDamageFlash>(out PlayerDamageFlash damageFlash))
            {
                damageFlash.PlayFlash();
                return;
            }

            PlayerDamageFlash flashInParent = other.GetComponentInParent<PlayerDamageFlash>();
            if (flashInParent != null)
            {
                flashInParent.PlayFlash();
            }
        }

        private bool EnsurePlayerReferences()
        {
            if (playerColliders != null && playerColliders.Length > 0 &&
                (cachedPlayerFlash != null || cachedPlayerHealth != null))
            {
                return true;
            }

            CachePlayerReferences();
            IgnorePhysicalCollisionWithPlayer();

            return playerColliders != null && playerColliders.Length > 0 &&
                   (cachedPlayerFlash != null || cachedPlayerHealth != null);
        }

        private void CachePlayerReferences()
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player == null)
            {
                return;
            }

            playerColliders = player.GetComponentsInChildren<Collider2D>(true);
            cachedPlayerFlash = player.GetComponentInChildren<PlayerDamageFlash>(true);
            cachedPlayerHealth = player.GetComponentInChildren<PlayerHealth>(true);
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
            if (enemyColliders == null || playerColliders == null)
            {
                return false;
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

                    if (enemyCollider.Distance(playerCollider).isOverlapped)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void ApplyContactHit()
        {
            if (Time.time < nextHitTime)
            {
                return;
            }

            if (cachedPlayerFlash != null)
            {
                cachedPlayerFlash.PlayFlash();
            }

            if (applyDamageInPassThrough && cachedPlayerHealth != null)
            {
                cachedPlayerHealth.TakeDamage(contactDamage);
            }

            nextHitTime = Time.time + hitInterval;
        }
    }
}
