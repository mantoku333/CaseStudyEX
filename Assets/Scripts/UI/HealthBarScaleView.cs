using Player;
using UnityEngine;

namespace GameName.UI
{
    /// <summary>
    /// プレイヤーHPに応じてHPバーのXスケールを更新するクラス
    /// </summary>
    public class HealthBarScaleView : MonoBehaviour
    {
        [SerializeField] private PlayerHealth targetHealth;
        [SerializeField] private RectTransform barTransform;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool autoFindPlayerHealth = true;
        [SerializeField] private bool syncEveryFrame = true;

        private Vector3 initialScale = Vector3.one;
        private bool hasCachedInitialScale;
        private int lastCurrentHealth = int.MinValue;
        private int lastMaxHealth = int.MinValue;

        private void Awake()
        {
            ResolveBarTransform();
            CacheInitialScale();
            ResolveTargetHealth();
        }

        private void OnEnable()
        {
            SubscribeToHealth();
            RefreshBar();
        }

        private void Start()
        {
            if (targetHealth == null && autoFindPlayerHealth)
            {
                ResolveTargetHealth();
                SubscribeToHealth();
            }

            RefreshBar();
        }

        private void Update()
        {
            if (targetHealth == null)
            {
                if (!autoFindPlayerHealth)
                {
                    return;
                }

                ResolveTargetHealth();
                SubscribeToHealth();
                RefreshBar();
                return;
            }

            if (syncEveryFrame)
            {
                RefreshIfChanged(targetHealth.CurrentHealth, targetHealth.MaxHealth);
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromHealth();
        }

        private void OnDestroy()
        {
            UnsubscribeFromHealth();
        }

        /// <summary>
        /// Inspector 未設定時は自身の RectTransform をバー対象にする
        /// </summary>
        private void ResolveBarTransform()
        {
            if (barTransform != null)
            {
                return;
            }

            barTransform = transform as RectTransform;
        }

        private void CacheInitialScale()
        {
            if (barTransform == null)
            {
                return;
            }

            initialScale = barTransform.localScale;
            hasCachedInitialScale = true;
        }

        private void ResolveTargetHealth()
        {
            if (targetHealth != null)
            {
                return;
            }

            GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);

            if (playerObject != null)
            {
                targetHealth = playerObject.GetComponent<PlayerHealth>();

                if (targetHealth == null)
                {
                    targetHealth = playerObject.GetComponentInChildren<PlayerHealth>();
                }
            }

            if (targetHealth == null)
            {
                targetHealth = FindObjectOfType<PlayerHealth>();
            }
        }

        private void SubscribeToHealth()
        {
            if (targetHealth == null)
            {
                return;
            }

            targetHealth.HealthChanged -= OnHealthChanged;
            targetHealth.HealthChanged += OnHealthChanged;
        }

        private void UnsubscribeFromHealth()
        {
            if (targetHealth == null)
            {
                return;
            }

            targetHealth.HealthChanged -= OnHealthChanged;
        }

        private void OnHealthChanged(int currentHealth, int maxHealth)
        {
            RefreshIfChanged(currentHealth, maxHealth);
        }

        /// <summary>
        /// 現在のHP状態からバーを再描画する
        /// </summary>
        public void RefreshBar()
        {
            if (targetHealth == null || barTransform == null)
            {
                return;
            }

            RefreshIfChanged(targetHealth.CurrentHealth, targetHealth.MaxHealth, true);
        }

        /// <summary>
        /// 外部から PlayerHealth を差し替えたい場合に使う
        /// </summary>
        public void SetTargetHealth(PlayerHealth health)
        {
            if (targetHealth == health)
            {
                RefreshBar();
                return;
            }

            UnsubscribeFromHealth();
            targetHealth = health;
            lastCurrentHealth = int.MinValue;
            lastMaxHealth = int.MinValue;
            SubscribeToHealth();
            RefreshBar();
        }

        private void RefreshIfChanged(int currentHealth, int maxHealth, bool force = false)
        {
            if (!force && currentHealth == lastCurrentHealth && maxHealth == lastMaxHealth)
            {
                return;
            }

            ApplyScale(currentHealth, maxHealth);
            lastCurrentHealth = currentHealth;
            lastMaxHealth = maxHealth;
        }

        private void ApplyScale(int currentHealth, int maxHealth)
        {
            if (barTransform == null)
            {
                return;
            }

            if (!hasCachedInitialScale)
            {
                CacheInitialScale();
            }

            float ratio = maxHealth > 0 ? Mathf.Clamp01((float)currentHealth / maxHealth) : 0f;
            Vector3 scale = initialScale;
            scale.x = initialScale.x * ratio;
            barTransform.localScale = scale;
        }
    }
}
