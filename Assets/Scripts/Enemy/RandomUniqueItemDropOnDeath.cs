using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameName.Enemy
{
    [DisallowMultipleComponent]
    public sealed class RandomUniqueItemDropOnDeath : MonoBehaviour
    {
        [Header("Drop Table")]
        [SerializeField] private EnemyUniqueDropTable dropTable;

        [Header("Drop Position")]
        [SerializeField] private Transform dropPoint;
        [SerializeField] private Vector3 dropOffset;

        [Header("Hop")]
        [SerializeField] private Vector2 randomHorizontalOffsetRange = new Vector2(-3f, 3f);
        [SerializeField, Min(0f)] private float hopHeight = 3f;
        [SerializeField, Min(0f)] private float hopDuration = 0.35f;
        [SerializeField] private bool disablePickupDuringHop = true;

        private static readonly HashSet<string> ActiveDropFlagKeys = new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<EnemyUniqueDropTable.DropEntry> CandidateEntries = new List<EnemyUniqueDropTable.DropEntry>();

        private IBossHealthSource healthSource;
        private IBossHealthSource subscribedHealthSource;
        private Collider2D bodyCollider;
        private bool hasRolled;

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearRuntimeState()
        {
            ActiveDropFlagKeys.Clear();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            hopHeight = Mathf.Max(0f, hopHeight);
            hopDuration = Mathf.Max(0f, hopDuration);

            if (randomHorizontalOffsetRange.x > randomHorizontalOffsetRange.y)
            {
                randomHorizontalOffsetRange = new Vector2(
                    randomHorizontalOffsetRange.y,
                    randomHorizontalOffsetRange.x);
            }
        }
#endif

        public static bool IsEligibleEnemy(GameObject enemyObject, bool includeStageBoss)
        {
            if (enemyObject == null || enemyObject.GetComponent<LastBossController>() != null)
            {
                return false;
            }

            if (enemyObject.GetComponent<StageBossAttack>() != null)
            {
                return includeStageBoss;
            }

            return enemyObject.GetComponent<EnemyController>() != null;
        }

        public static EnemyUniqueDropTable.DropEntry SelectDropEntry(
            IReadOnlyList<EnemyUniqueDropTable.DropEntry> entries,
            float dropChance,
            Predicate<string> isUnavailable,
            float chanceRoll,
            int randomIndexSeed)
        {
            if (entries == null || chanceRoll > Mathf.Clamp01(dropChance))
            {
                return null;
            }

            CandidateEntries.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                EnemyUniqueDropTable.DropEntry entry = entries[i];
                if (entry == null || !entry.IsValid)
                {
                    continue;
                }

                string flagKey = entry.ProgressFlagKey;
                if (isUnavailable != null && isUnavailable(flagKey))
                {
                    continue;
                }

                CandidateEntries.Add(entry);
            }

            if (CandidateEntries.Count == 0)
            {
                return null;
            }

            int index = (int)((uint)randomIndexSeed % CandidateEntries.Count);
            return CandidateEntries[index];
        }

        private void CacheReferences()
        {
            if (healthSource == null)
            {
                healthSource = FindHealthSourceOnThisObject();
            }

            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }
        }

        private IBossHealthSource FindHealthSourceOnThisObject()
        {
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IBossHealthSource source)
                {
                    return source;
                }
            }

            return null;
        }

        private void Subscribe()
        {
            if (healthSource == null || subscribedHealthSource == healthSource)
            {
                return;
            }

            Unsubscribe();
            healthSource.Died += TryDropItem;
            subscribedHealthSource = healthSource;
        }

        private void Unsubscribe()
        {
            if (subscribedHealthSource == null)
            {
                return;
            }

            subscribedHealthSource.Died -= TryDropItem;
            subscribedHealthSource = null;
        }

        private void TryDropItem()
        {
            if (hasRolled)
            {
                return;
            }

            hasRolled = true;

            if (dropTable == null)
            {
                Debug.LogWarning($"{nameof(RandomUniqueItemDropOnDeath)} on {name} has no drop table assigned.", this);
                return;
            }

            if (!IsEligibleEnemy(gameObject, dropTable.IncludeStageBoss))
            {
                return;
            }

            EnemyUniqueDropTable.DropEntry entry = SelectDropEntry(
                dropTable.Entries,
                dropTable.DropChance,
                IsDropUnavailable,
                UnityEngine.Random.value,
                UnityEngine.Random.Range(0, int.MaxValue));

            if (entry == null)
            {
                return;
            }

            SpawnEntry(entry);
        }

        private static bool IsDropUnavailable(string progressFlagKey)
        {
            return GameProgressFlags.Get(progressFlagKey) || ActiveDropFlagKeys.Contains(progressFlagKey);
        }

        internal static void ReleaseActiveDropFlag(string progressFlagKey)
        {
            if (!string.IsNullOrWhiteSpace(progressFlagKey))
            {
                ActiveDropFlagKeys.Remove(progressFlagKey);
            }
        }

        private void SpawnEntry(EnemyUniqueDropTable.DropEntry entry)
        {
            ActiveDropFlagKeys.Add(entry.ProgressFlagKey);

            Vector3 spawnPosition = GetDropPosition();
            Vector3 landingPosition = GetRandomLandingPosition(spawnPosition);
            GameObject droppedItem = Instantiate(entry.ItemPrefab, spawnPosition, entry.ItemPrefab.transform.rotation);

            UniqueDroppedItemRuntimeMarker marker = droppedItem.GetComponent<UniqueDroppedItemRuntimeMarker>();
            if (marker == null)
            {
                marker = droppedItem.AddComponent<UniqueDroppedItemRuntimeMarker>();
            }

            marker.Initialize(entry.ProgressFlagKey);

            DroppedItemHopMotion hopMotion = droppedItem.GetComponent<DroppedItemHopMotion>();
            if (hopMotion == null)
            {
                hopMotion = droppedItem.AddComponent<DroppedItemHopMotion>();
            }

            hopMotion.Play(spawnPosition, landingPosition, hopHeight, hopDuration, disablePickupDuringHop);
        }

        private Vector3 GetDropPosition()
        {
            if (dropPoint != null)
            {
                return dropPoint.position + dropOffset;
            }

            if (bodyCollider != null)
            {
                return bodyCollider.bounds.center + dropOffset;
            }

            return transform.position + dropOffset;
        }

        private Vector3 GetRandomLandingPosition(Vector3 spawnPosition)
        {
            float minOffset = Mathf.Min(randomHorizontalOffsetRange.x, randomHorizontalOffsetRange.y);
            float maxOffset = Mathf.Max(randomHorizontalOffsetRange.x, randomHorizontalOffsetRange.y);
            float xOffset = UnityEngine.Random.Range(minOffset, maxOffset);

            return spawnPosition + Vector3.right * xOffset;
        }

    }

    public sealed class UniqueDroppedItemRuntimeMarker : MonoBehaviour
    {
        private string progressFlagKey;

        public void Initialize(string flagKey)
        {
            progressFlagKey = flagKey;
        }

        private void OnDestroy()
        {
            RandomUniqueItemDropOnDeath.ReleaseActiveDropFlag(progressFlagKey);
        }
    }
}
