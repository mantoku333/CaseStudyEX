using UnityEngine;

namespace Player
{
    /// <summary>
    /// プレイヤーの各種ステータスをまとめて保持するデータ
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStatsData", menuName = "Player/Player Stats Data")]
    public class PlayerStatsData : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(0f)] private float glideMoveSpeed = 3.5f;
        [SerializeField, Min(0f)] private float fallSpeed = 10f;
        [SerializeField, Min(0f)] private float jumpForce = 8f;

        [Header("Combat")]
        [SerializeField, Min(0.01f)] private float attackPerSecond = 4f;
        [SerializeField, Min(0f)] private float gunRecoilForce = 1.5f;
        [SerializeField, Min(0f)] private float reloadSeconds = 1.2f;

        [Header("Health")]
        [SerializeField, Min(1)] private int maxHealth = 3;

        /// <summary>通常時の移動速度</summary>
        public float MoveSpeed => moveSpeed;

        /// <summary>滑空中の横移動速度</summary>
        public float GlideMoveSpeed => glideMoveSpeed;

        /// <summary>最大落下速度</summary>
        public float FallSpeed => fallSpeed;

        /// <summary>ジャンプ初速</summary>
        public float JumpForce => jumpForce;

        /// <summary>1秒あたりに撃てる回数</summary>
        public float AttackPerSecond => attackPerSecond;

        /// <summary>発射時の反動の強さ</summary>
        public float GunRecoilForce => gunRecoilForce;

        /// <summary>リロードにかかる秒数</summary>
        public float ReloadSeconds => reloadSeconds;

        /// <summary>最大体力</summary>
        public int MaxHealth => maxHealth;
    }
}
