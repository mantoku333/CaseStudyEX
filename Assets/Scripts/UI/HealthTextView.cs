using Player;
using TMPro;
using UnityEngine;

namespace GameName.UI
{
    /// <summary>
    /// プレイヤーHPをUIテキストに表示するクラス
    /// </summary>
    public class HealthTextView : MonoBehaviour
    {
        [SerializeField] private PlayerHealth targetHealth;
        [SerializeField] private TextMeshProUGUI healthText;

        /// <summary>
        /// イベント登録と初期表示を行う
        /// </summary>
        private void Start()
        {
            if (targetHealth == null || healthText == null)
            {
                return;
            }

            // HP変更時にUIを更新する
            targetHealth.HealthChanged += OnHealthChanged;

            // 開始時点のHPを即表示する
            OnHealthChanged(targetHealth.CurrentHealth, targetHealth.MaxHealth);
        }

        /// <summary>
        /// イベント解除を行う
        /// </summary>
        private void OnDestroy()
        {
            if (targetHealth != null)
            {
                targetHealth.HealthChanged -= OnHealthChanged;
            }
        }

        /// <summary>
        /// HPの数字表示を更新
        /// </summary>
        /// <param name="currentHealth">現在HP</param>
        /// <param name="maxHealth">最大HP</param>
        private void OnHealthChanged(int currentHealth, int maxHealth)
        {
            // 今回の要件では左上に単純な数字だけ表示すればよいため currentHealth のみ表示
            healthText.text = currentHealth.ToString();
        }
    }
}
