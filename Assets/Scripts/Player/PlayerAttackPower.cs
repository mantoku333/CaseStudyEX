using System;
using UnityEngine;

namespace Player
{
    public class PlayerAttackPower : MonoBehaviour, ISaveDataModule
    {
        private const string SectionKey = "player_attack_power_v1";

        [SerializeField] private PlayerStatsData statsData;
        [SerializeField] private int attackDamageBonus;

        [Header("Debug")]
        [SerializeField] private bool logAttackPowerDebug;

        public int Priority => 231;

        public int AttackDamage
        {
            get
            {
                TryResolveStatsData();
                int baseAttackDamage = statsData != null ? statsData.PlayerAttackDamage : 1;
                return Mathf.Max(1, baseAttackDamage + attackDamageBonus);
            }
        }

        public int AttackDamageBonus => attackDamageBonus;

        private void Awake()
        {
            TryResolveStatsData();
        }

        private void OnEnable()
        {
            SaveManager.RegisterModule(this);
        }

        private void OnDisable()
        {
            SaveManager.UnregisterModule(this);
        }

        public void AddAttackDamage(int value)
        {
            if (value <= 0)
            {
                return;
            }

            LogAttackPowerDebug($"攻撃力増加前: {AttackDamage}");
            attackDamageBonus += value;
            LogAttackPowerDebug($"攻撃力増加後: {AttackDamage}");
        }

        public void Capture(SaveGameData saveData)
        {
            if (saveData == null)
            {
                return;
            }

            var payload = new PlayerAttackPowerPayload
            {
                attackDamageBonus = attackDamageBonus
            };

            saveData.SetCustomSectionJson(SectionKey, JsonUtility.ToJson(payload));
        }

        public void Restore(SaveGameData saveData)
        {
            if (saveData == null)
            {
                return;
            }

            string json = saveData.GetCustomSectionJson(SectionKey);
            if (string.IsNullOrWhiteSpace(json))
            {
                attackDamageBonus = 0;
                return;
            }

            try
            {
                PlayerAttackPowerPayload payload = JsonUtility.FromJson<PlayerAttackPowerPayload>(json);
                attackDamageBonus = Mathf.Max(0, payload.attackDamageBonus);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PlayerAttackPower] Failed to parse saved attack power. {exception}", this);
            }
        }

        private void TryResolveStatsData()
        {
            if (statsData != null)
            {
                return;
            }

            var playerController = GetComponent<global::PlayerController>();
            if (playerController != null)
            {
                statsData = playerController.GetPlayerStatsData();
            }
        }

        private void LogAttackPowerDebug(string message)
        {
            if (logAttackPowerDebug)
            {
                Debug.Log(message, this);
            }
        }

        [Serializable]
        private struct PlayerAttackPowerPayload
        {
            public int attackDamageBonus;
        }
    }
}
