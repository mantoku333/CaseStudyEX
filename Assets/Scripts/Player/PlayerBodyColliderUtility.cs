using Player;
using UnityEngine;

namespace Metroidvania.Player
{
    /// <summary>
    /// プレイヤーの被弾判定を「PlayerHealth と同じ GameObject にある本体 Collider2D」に統一するための共通処理。
    /// 傘や攻撃用の子コライダーを、プレイヤー本体の被弾判定として扱わないようにする。
    /// </summary>
    public static class PlayerBodyColliderUtility
    {
        private const string PlayerLayerName = "Player";

        public static LayerMask GetPlayerBodyLayerMask()
        {
            int playerLayer = LayerMask.NameToLayer(PlayerLayerName);
            return playerLayer >= 0 ? 1 << playerLayer : Physics2D.DefaultRaycastLayers;
        }

        /// <summary>
        /// PlayerHealth が付いているルート上の非トリガー Collider2D を、本体コライダーとして取得する。
        /// </summary>
        public static bool TryGetBodyCollider(PlayerHealth playerHealth, out Collider2D bodyCollider)
        {
            bodyCollider = null;

            if (playerHealth == null)
            {
                return false;
            }

            bodyCollider = playerHealth.GetComponent<Collider2D>();
            return bodyCollider != null && bodyCollider.enabled && !bodyCollider.isTrigger;
        }

        public static bool TryGetPlayerBodyFromCollider(
            Collider2D hitCollider,
            out PlayerHealth playerHealth,
            out Collider2D bodyCollider)
        {
            playerHealth = null;
            bodyCollider = null;

            if (hitCollider == null)
            {
                return false;
            }

            playerHealth = hitCollider.GetComponent<PlayerHealth>();
            if (playerHealth == null)
            {
                playerHealth = hitCollider.GetComponentInParent<PlayerHealth>();
            }

            Rigidbody2D attachedRigidbody = hitCollider.attachedRigidbody;
            if (playerHealth == null && attachedRigidbody != null)
            {
                playerHealth = attachedRigidbody.GetComponent<PlayerHealth>();
                if (playerHealth == null)
                {
                    playerHealth = attachedRigidbody.GetComponentInParent<PlayerHealth>();
                }
            }

            if (!TryGetBodyCollider(playerHealth, out bodyCollider))
            {
                return false;
            }

            // 子オブジェクトの傘・攻撃・パリィ判定ではなく、本体コライダーに直接当たった時だけ true。
            return hitCollider == bodyCollider;
        }

        public static bool IsPlayerBodyCollider(Collider2D hitCollider)
        {
            return TryGetPlayerBodyFromCollider(hitCollider, out _, out _);
        }
    }
}
