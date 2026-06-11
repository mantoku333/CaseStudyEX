using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameName.Enemy
{
    [CreateAssetMenu(fileName = "EnemyUniqueDropTable", menuName = "GameName/Enemy/Enemy Unique Drop Table")]
    public sealed class EnemyUniqueDropTable : ScriptableObject
    {
        [SerializeField] private bool includeStageBoss;
        [SerializeField] private List<DropEntry> entries = new List<DropEntry>();

        public bool IncludeStageBoss => includeStageBoss;
        public IReadOnlyList<DropEntry> Entries => entries;

#if UNITY_EDITOR
        private void OnValidate()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i]?.ClampDropChance();
            }
        }
#endif

        [Serializable]
        public sealed class DropEntry
        {
            [SerializeField] private GameObject itemPrefab;
            [SerializeField] private string progressFlagKey;
            [Tooltip("Drop chance for this item. 0.03 = 3%.")]
            [SerializeField, Range(0f, 1f)] private float dropChance = 0.03f;

            public DropEntry()
            {
            }

            public DropEntry(GameObject itemPrefab, string progressFlagKey, float dropChance = 0.03f)
            {
                this.itemPrefab = itemPrefab;
                this.progressFlagKey = progressFlagKey;
                this.dropChance = Mathf.Clamp01(dropChance);
            }

            public GameObject ItemPrefab => itemPrefab;
            public string ProgressFlagKey => progressFlagKey;
            public float DropChance => Mathf.Clamp01(dropChance);
            public bool IsValid => itemPrefab != null && !string.IsNullOrWhiteSpace(progressFlagKey);

            internal void ClampDropChance()
            {
                dropChance = Mathf.Clamp01(dropChance);
            }
        }
    }
}
