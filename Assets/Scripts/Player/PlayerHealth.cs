using System;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// プレイヤーの体力を管理するクラス
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private PlayerStatsData statsData;
        [SerializeField, Min(0f)] private float damageCooldownSeconds = 3f;
        [SerializeField] private int maxHealthBonus = 0;

        private int currentHealth;
        private float nextDamageTime;
        private bool deathNotified;

        /// <summary>
        /// HPが変化したときに通知
        /// 第1引数: 現在HP
        /// 第2引数: 最大HP
        /// </summary>
        public event Action<int, int> HealthChanged;

        /// <summary>
        /// HPが0になったときに1回だけ通知
        /// </summary>
        public event Action Died;

        /// <summary>現在HP</summary>
        public int CurrentHealth => currentHealth;

        /// <summary>最大HP</summary>
        public int MaxHealth
        {
            get
            {
                TryResolveStatsData();
                int baseMaxHealth = 1;

                if (statsData != null)
                {
                    baseMaxHealth = statsData.MaxHealth;
                }

                return baseMaxHealth + maxHealthBonus;
            }
        }

        private void Awake()
        {
            TryResolveStatsData();
        }

        /// <summary>
        /// 初期HPを設定
        /// </summary>
        private void Start()
        {
            currentHealth = MaxHealth;
            NotifyHealthChanged();
        }

        /// <summary>
        /// ダメージを受けてHPを減らす
        /// </summary>
        /// <param name="damage">受けるダメージ量</param>
        public void TakeDamage(int damage)
        {
            // 無効なダメージ、またはすでに死亡しているなら何もしない
            if (damage <= 0 || currentHealth <= 0)
            {
                return;
            }

            if (Time.time < nextDamageTime)
            {
                Debug.Log("ダメージクールタイム中です");
                return;
            }

            Debug.Log($"ダメージ前 HP: {currentHealth} / {MaxHealth}");
            Debug.Log($"受けるダメージ量: {damage}");

            currentHealth = Mathf.Max(0, currentHealth - damage);

            Debug.Log($"ダメージ後 HP: {currentHealth} / {MaxHealth}");

            nextDamageTime = Time.time + damageCooldownSeconds;

            NotifyHealthChanged();
        }


        /// <summary>
        /// HPを回復
        /// </summary>
        /// <param name="value">回復量</param>
        public void Heal(int value)
        {
            // 無効な回復値、または死亡中なら回復しない
            if (value <= 0 || currentHealth <= 0)
            {
                return;
            }
            Debug.Log($"回復前 HP: {currentHealth} / {MaxHealth}");

            currentHealth = Mathf.Min(MaxHealth, currentHealth + value);

            Debug.Log($"回復後 HP: {currentHealth} / {MaxHealth}");

            NotifyHealthChanged();
        }

        /// <summary>
        /// HPの最大値を増加
        /// </summary>
        public void AddMaxHealth(int value, bool healAddedAmount)
        {
            if (value <= 0){ return; }

            Debug.Log($"上限増加前 HP: {currentHealth} / {MaxHealth}");

            maxHealthBonus += value;

            if (healAddedAmount)
            {
                currentHealth = Mathf.Min(MaxHealth, currentHealth + value);
            }
            else
            {
                currentHealth = Mathf.Min(currentHealth, MaxHealth);
            }

            Debug.Log($"上限増加後 HP: {currentHealth} / {MaxHealth}");

            NotifyHealthChanged();
        }

        /// <summary>
        /// セーブデータから現在HPを復元する
        /// </summary>
        /// <param name="value">復元するHP</param>
        public void SetCurrentHealthFromSave(int value)
        {
            currentHealth = Mathf.Clamp(value, 0, MaxHealth);
            NotifyHealthChanged();
        }

        /// <summary>
        /// HP変更イベントを通知
        /// </summary>
        private void NotifyHealthChanged()
        {
            HealthChanged?.Invoke(currentHealth, MaxHealth);

            if (currentHealth <= 0)
            {
                if (!deathNotified)
                {
                    deathNotified = true;
                    Died?.Invoke();
                }
            }
            else
            {
                deathNotified = false;
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
    }
}
