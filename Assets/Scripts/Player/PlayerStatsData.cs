using NaughtyAttributes;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Player tunable parameters.
    /// Mainly adjusted by planning/design members.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerStatsData", menuName = "Player/Player Stats Data")]
    public class PlayerStatsData : ScriptableObject
    {
        private const float MinDuration = 0.01f;

        [BoxGroup("移動"), Label("移動速度"), Tooltip("地上移動速度"), SerializeField, Min(0f)] private float moveSpeed = 5f;
        [BoxGroup("移動"), Label("滑空移動速度"), Tooltip("傘を開いて空中移動中の横移動速度"), SerializeField, Min(0f)] private float glideMoveSpeed = 3.5f;
        [BoxGroup("移動"), Label("滑空落下上限"), Tooltip("傘開き中の最大落下速度(絶対値)"), SerializeField, Min(0f)] private float fallSpeed = 10f;
        [BoxGroup("移動"), Label("ジャンプ力"), Tooltip("地上ジャンプ初速"), SerializeField, Min(0f)] private float jumpForce = 8f;
        [BoxGroup("移動"), Label("回避距離"), Tooltip("回避移動距離"), SerializeField, Min(0f)] private float dodgeDistance = 3f;
        [BoxGroup("移動"), Label("回避時間"), Tooltip("回避時間(秒)"), SerializeField, Min(MinDuration)] private float dodgeDuration = 0.1f;

        [BoxGroup("戦闘"), Label("攻撃回数/秒"), Tooltip("傘攻撃の秒間回数"), SerializeField, Min(MinDuration)] private float attackPerSecond = 4f;
        [BoxGroup("戦闘"), Label("傘攻撃持続"), Tooltip("傘攻撃当たり判定の継続時間(秒)"), SerializeField, Min(MinDuration)] private float umbrellaAttackDuration = 0.2f;
        [BoxGroup("戦闘"), Label("銃反動"), Tooltip("射撃反動/リコイルジャンプの反動量"), SerializeField, Min(0f)] private float gunRecoilForce = 1.5f;
        [BoxGroup("戦闘"), Label("反動時間"), Tooltip("反動状態の継続時間(秒)"), SerializeField, Min(MinDuration)] private float gunRecoilDuration = 0.1f;
        [BoxGroup("戦闘"), Label("リロード秒数"), Tooltip("次の射撃/リコイルまでの待ち時間"), SerializeField, Min(0f)] private float reloadSeconds = 1.2f;

        [BoxGroup("パリィ"), Label("パリィ有効時間"), Tooltip("パリィ有効時間(秒)"), SerializeField, Min(MinDuration)] private float parryDuration = 0.1f;
        [BoxGroup("パリィ"), Label("フラッシュ時間"), Tooltip("パリィ演出フラッシュ時間(秒)"), SerializeField, Min(MinDuration)] private float parryFlashDuration = 0.1f;

        [BoxGroup("体力"), Label("最大HP"), Tooltip("最大HP"), SerializeField, Min(1)] private int maxHealth = 3;

        public float MoveSpeed => moveSpeed;
        public float GlideMoveSpeed => glideMoveSpeed;
        public float FallSpeed => fallSpeed;
        public float JumpForce => jumpForce;
        public float DodgeDistance => dodgeDistance;
        public float DodgeDuration => dodgeDuration;
        public float AttackPerSecond => attackPerSecond;
        public float UmbrellaAttackDuration => umbrellaAttackDuration;
        public float GunRecoilForce => gunRecoilForce;
        public float GunRecoilDuration => gunRecoilDuration;
        public float ReloadSeconds => reloadSeconds;
        public float ParryDuration => parryDuration;
        public float ParryFlashDuration => parryFlashDuration;
        public int MaxHealth => maxHealth;
        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            glideMoveSpeed = Mathf.Max(0f, glideMoveSpeed);
            fallSpeed = Mathf.Max(0f, fallSpeed);
            jumpForce = Mathf.Max(0f, jumpForce);
            dodgeDistance = Mathf.Max(0f, dodgeDistance);
            dodgeDuration = Mathf.Max(MinDuration, dodgeDuration);

            attackPerSecond = Mathf.Max(MinDuration, attackPerSecond);
            umbrellaAttackDuration = Mathf.Max(MinDuration, umbrellaAttackDuration);
            gunRecoilForce = Mathf.Max(0f, gunRecoilForce);
            gunRecoilDuration = Mathf.Max(MinDuration, gunRecoilDuration);
            reloadSeconds = Mathf.Max(0f, reloadSeconds);

            parryDuration = Mathf.Max(MinDuration, parryDuration);
            parryFlashDuration = Mathf.Max(MinDuration, parryFlashDuration);
            maxHealth = Mathf.Max(1, maxHealth);
        }
    }
}
