using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameName.Enemy
{
    [CreateAssetMenu(fileName = "EnemyUniqueDropTable", menuName = "GameName/Enemy/Enemy Unique Drop Table")]
    public sealed class EnemyUniqueDropTable : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)] private float dropChance = 0.3f;
        [SerializeField] private bool includeStageBoss;
        [SerializeField] private List<DropEntry> entries = new List<DropEntry>();

        public float DropChance => Mathf.Clamp01(dropChance);
        public bool IncludeStageBoss => includeStageBoss;
        public IReadOnlyList<DropEntry> Entries => entries;

#if UNITY_EDITOR
        private void OnValidate()
        {
            dropChance = Mathf.Clamp01(dropChance);
        }
#endif

        [Serializable]
        public sealed class DropEntry
        {
            [SerializeField] private GameObject itemPrefab;
            [SerializeField] private string progressFlagKey;

            public DropEntry()
            {
            }

            public DropEntry(GameObject itemPrefab, string progressFlagKey)
            {
                this.itemPrefab = itemPrefab;
                this.progressFlagKey = progressFlagKey;
            }

            public GameObject ItemPrefab => itemPrefab;
            public string ProgressFlagKey => progressFlagKey;
            public bool IsValid => itemPrefab != null && !string.IsNullOrWhiteSpace(progressFlagKey);
        }
    }
}
