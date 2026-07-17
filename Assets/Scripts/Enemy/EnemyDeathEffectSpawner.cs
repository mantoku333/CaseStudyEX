using UnityEngine;

namespace GameName.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyController))]
    public sealed class EnemyDeathEffectSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject deathEffectPrefab;
        [SerializeField] private Vector3 spawnOffset;
        [SerializeField] private bool useColliderCenter = true;
        [SerializeField] private bool overrideDeathEffectScale;
        [SerializeField, Min(0.01f)] private float deathEffectScaleMultiplier = 1f;

        private EnemyController enemyController;
        private Collider2D bodyCollider;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();

            if (enemyController != null)
            {
                enemyController.Died += SpawnEffect;
            }
        }

        private void OnDisable()
        {
            if (enemyController != null)
            {
                enemyController.Died -= SpawnEffect;
            }
        }

        private void CacheComponents()
        {
            if (enemyController == null)
            {
                enemyController = GetComponent<EnemyController>();
            }

            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }
        }

        private void SpawnEffect()
        {
            if (deathEffectPrefab == null)
            {
                return;
            }

            Vector3 spawnPosition = transform.position;
            if (useColliderCenter && bodyCollider != null)
            {
                spawnPosition = bodyCollider.bounds.center;
            }

            GameObject effectInstance = Instantiate(
                deathEffectPrefab,
                spawnPosition + spawnOffset,
                Quaternion.identity);

            if (overrideDeathEffectScale)
            {
                effectInstance.transform.localScale *= Mathf.Max(0.01f, deathEffectScaleMultiplier);
            }
        }
    }
}
