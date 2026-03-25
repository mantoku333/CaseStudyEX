using Metroidvania.Player;
using UnityEngine;

namespace Metroidvania.Enemy
{
    /// <summary>
    /// 敵本体にプレイヤーが接触した際、
    /// プレイヤーへ被弾演出を出すためのクラス
    /// </summary>
    public sealed class EnemyContact : MonoBehaviour
    {
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.gameObject.TryGetComponent<PlayerDamageFlash>(out PlayerDamageFlash damageFlash))
            {
                damageFlash.PlayFlash();
                return;
            }

            PlayerDamageFlash flashInParent = collision.gameObject.GetComponentInParent<PlayerDamageFlash>();
            if (flashInParent != null)
            {
                flashInParent.PlayFlash();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent<PlayerDamageFlash>(out PlayerDamageFlash damageFlash))
            {
                damageFlash.PlayFlash();
                return;
            }

            PlayerDamageFlash flashInParent = other.GetComponentInParent<PlayerDamageFlash>();
            if (flashInParent != null)
            {
                flashInParent.PlayFlash();
            }
        }
    }
}
