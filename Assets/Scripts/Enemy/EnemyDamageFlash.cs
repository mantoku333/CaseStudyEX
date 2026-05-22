using System.Collections;
using UnityEngine;

namespace GameName.Enemy
{
    /// <summary>
    /// 敵を赤く点滅させるコンポーネント。ダメージを受けたときに呼び出すことで、敵がダメージを受けたことを視覚的に表現
    /// 接触ダメージそのものは別のコンポーネントで処理することを想定
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyDamageFlash : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private bool autoCollectRenderers = true;
        [SerializeField] private SpriteRenderer[] targetRenderers;

        [Header("Flash Settings")]
        [SerializeField] private Color flashColor = Color.red;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.14f;
        [SerializeField, Min(1)] private int flashRepeatCount = 2;
        [SerializeField, Min(0f)] private float normalDuration = 0.05f;
        [SerializeField, Min(0f)] private float flashCooldownSeconds = 0.02f;

        private Color[] restoreColors;
        private Coroutine flashCoroutine;
        private float nextFlashTime;

        private void Awake()
        {
            ResolveRenderers();
            restoreColors = new Color[targetRenderers.Length];
        }

        public void PlayFlash()
        {
            ResolveRenderers();
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                return;
            }

            if (Time.time < nextFlashTime)
            {
                return;
            }

            nextFlashTime = Time.time + flashCooldownSeconds;

            if (flashCoroutine == null)
            {
                CaptureCurrentColors();
            }

            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }

            flashCoroutine = StartCoroutine(FlashCoroutine());
        }

        private IEnumerator FlashCoroutine()
        {
            int repeatCount = Mathf.Max(1, flashRepeatCount);
            for (int i = 0; i < repeatCount; i++)
            {
                SetColor(flashColor);
                yield return new WaitForSeconds(flashDuration);
                RestoreDefaultColors();

                if (i < repeatCount - 1 && normalDuration > 0f)
                {
                    yield return new WaitForSeconds(normalDuration);
                }
            }

            flashCoroutine = null;
        }

        private void ResolveRenderers()
        {
            if (!autoCollectRenderers && targetRenderers != null && targetRenderers.Length > 0)
            {
                return;
            }

            if (!autoCollectRenderers)
            {
                targetRenderers = targetRenderers ?? new SpriteRenderer[0];
                return;
            }

            if (targetRenderers != null && targetRenderers.Length > 0)
            {
                return;
            }

            targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void SetColor(Color color)
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] == null)
                {
                    continue;
                }

                targetRenderers[i].color = color;
            }
        }

        private void RestoreDefaultColors()
        {
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] == null)
                {
                    continue;
                }

                targetRenderers[i].color = restoreColors[i];
            }
        }

        private void CaptureCurrentColors()
        {
            if (restoreColors == null || restoreColors.Length != targetRenderers.Length)
            {
                restoreColors = new Color[targetRenderers.Length];
            }

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] == null)
                {
                    continue;
                }

                restoreColors[i] = targetRenderers[i].color;
            }
        }
    }
}
