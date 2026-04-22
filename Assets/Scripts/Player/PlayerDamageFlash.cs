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

        private Color[] defaultColors;
        private Coroutine flashCoroutine;

        private void Awake()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            }

            defaultColors = new Color[targetRenderers.Length];

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] != null)
                {
                    defaultColors[i] = targetRenderers[i].color;
                }
            }
        }

        /// <summary>
        /// 被弾時の赤点滅演出を再生
        /// </summary>
        public void PlayFlash()
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }

            flashCoroutine = StartCoroutine(FlashCoroutine());
        }

        private IEnumerator FlashCoroutine()
        {
            SetColor(flashColor);

            yield return new WaitForSeconds(flashDuration);

            RestoreDefaultColors();
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

                targetRenderers[i].color = defaultColors[i];
            }
        }
    }
}
