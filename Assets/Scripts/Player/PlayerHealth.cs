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

        private int currentHealth;
        private float nextDamageTime;

        /// <summary>
        /// HPが変化したときに通知
        /// 第1引数: 現在HP
        /// 第2引数: 最大HP
        /// </summary>
        public event Action<int, int> HealthChanged;

        /// <summary>現在HP</summary>
        public int CurrentHealth => currentHealth;

        /// <summary>最大HP</summary>
        public int MaxHealth => statsData != null ? statsData.MaxHealth : 1;

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
                return;
            }

            currentHealth = Mathf.Max(0, currentHealth - damage);
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

            currentHealth = Mathf.Min(MaxHealth, currentHealth + value);
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
        }
    }
}
