using System.Collections;
using UnityEngine;

namespace Metroidvania.Player
{
    /// <summary>
    /// プレイヤーが被弾した際に、SpriteRendererの色を一瞬赤くするクラス
    /// HP処理とは分離し、リアクション表示専用として使用
    /// </summary>
    public sealed class PlayerDamageFlash : MonoBehaviour
    {
        [Header("Flash Settings")]
        [SerializeField] private SpriteRenderer[] targetRenderers;
        [SerializeField] private Color flashColor = Color.red;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.08f;
        [SerializeField, Min(1)] private int flashRepeatCount = 3;
        [SerializeField, Min(0f)] private float normalDuration = 0.08f;
        [SerializeField, Min(0f)] private float flashCooldownSeconds = 3f;


        private Color[] restoreColors;
        private Coroutine flashCoroutine;
        private float nextFlashTime;

        private void Awake()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            }

            restoreColors = new Color[targetRenderers.Length];

            CaptureCurrentColors();
        }

        /// <summary>
        /// 被弾時の赤点滅演出を再生
        /// </summary>
        public void PlayFlash()
        {
            PlayFlashInternal(false);
        }

        /// <summary>
        /// HP 側でダメージ成立済みの時に、表示用クールダウンを無視して赤点滅を再生する。
        /// </summary>
        public void PlayFlashForced()
        {
            PlayFlashInternal(true);
        }

        private void PlayFlashInternal(bool ignoreCooldown)
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                return;
            }

            if (!ignoreCooldown && Time.time < nextFlashTime)
            {
                return;
            }


            nextFlashTime = Time.time + flashCooldownSeconds;

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
