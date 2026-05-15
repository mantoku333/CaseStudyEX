using UnityEngine;

namespace GameName.Audio
{
    /// <summary>
    /// 敵SEで使うAudioClipと、SEごとの個別音量をまとめて管理する設定アセット。
    /// Assets/Resources に置くことで、シーンに手動配置しなくても実行時に読み込める。
    /// </summary>
    [CreateAssetMenu(fileName = "EnemySfxProfile", menuName = "GameName/Audio/Enemy SFX Profile")]
    public sealed class EnemySfxProfile : ScriptableObject
    {
        [Header("Clips")]
        [Tooltip("通常敵（Enemy_Tackle / Enemy_Ranged）が死亡したときのSE。")]
        [SerializeField] private AudioClip enemyDead;
        [Tooltip("画面内に通常敵がいるとき、ランダム間隔で鳴らす咆哮SE。")]
        [SerializeField] private AudioClip enemyGrowl;
        [Tooltip("Enemy_Ranged が弾を発射したときのSE。")]
        [SerializeField] private AudioClip enemyRangedAttack;
        [Tooltip("Enemy_Tackle または StageBoss が突進を開始したときのSE。")]
        [SerializeField] private AudioClip enemyTackleAttack;
        [Tooltip("LastBoss が死亡したときのSE。")]
        [SerializeField] private AudioClip lastBossDead;
        [Tooltip("StageBoss が死亡したときのSE。")]
        [SerializeField] private AudioClip stageBossDead;

        [Header("Volume")]
        [Tooltip("通常敵死亡SEの個別音量。最終音量はオプション画面のSE音量とも掛け合わされる。")]
        [SerializeField, Range(0f, 1f)] private float enemyDeadVolume = 1f;
        [Tooltip("咆哮SEの個別音量。最終音量はオプション画面のSE音量とも掛け合わされる。")]
        [SerializeField, Range(0f, 1f)] private float enemyGrowlVolume = 1f;
        [Tooltip("遠距離敵攻撃SEの個別音量。最終音量はオプション画面のSE音量とも掛け合わされる。")]
        [SerializeField, Range(0f, 1f)] private float enemyRangedAttackVolume = 1f;
        [Tooltip("突進開始SEの個別音量。最終音量はオプション画面のSE音量とも掛け合わされる。")]
        [SerializeField, Range(0f, 1f)] private float enemyTackleAttackVolume = 1f;
        [Tooltip("LastBoss死亡SEの個別音量。最終音量はオプション画面のSE音量とも掛け合わされる。")]
        [SerializeField, Range(0f, 1f)] private float lastBossDeadVolume = 1f;
        [Tooltip("StageBoss死亡SEの個別音量。最終音量はオプション画面のSE音量とも掛け合わされる。")]
        [SerializeField, Range(0f, 1f)] private float stageBossDeadVolume = 1f;

        public AudioClip EnemyDead => enemyDead;
        public AudioClip EnemyGrowl => enemyGrowl;
        public AudioClip EnemyRangedAttack => enemyRangedAttack;
        public AudioClip EnemyTackleAttack => enemyTackleAttack;
        public AudioClip LastBossDead => lastBossDead;
        public AudioClip StageBossDead => stageBossDead;

        public float EnemyDeadVolume => enemyDeadVolume;
        public float EnemyGrowlVolume => enemyGrowlVolume;
        public float EnemyRangedAttackVolume => enemyRangedAttackVolume;
        public float EnemyTackleAttackVolume => enemyTackleAttackVolume;
        public float LastBossDeadVolume => lastBossDeadVolume;
        public float StageBossDeadVolume => stageBossDeadVolume;
    }
}
