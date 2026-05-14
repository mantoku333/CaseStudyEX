using System.Collections.Generic;
using UnityEngine;

namespace GameName.Audio
{
    /// <summary>
    /// 敵SEを一元管理するランタイム用シングルトン。
    /// 攻撃・死亡SEは通常のOneShot、咆哮SEは専用AudioSourceで多重再生を防ぐ。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySfxManager : MonoBehaviour
    {
        // EnemySfxProfile.asset は Assets/Resources 直下に置く想定。
        private const string DefaultProfileResourcePath = "EnemySfxProfile";

        private static EnemySfxManager instance;

        [SerializeField] private EnemySfxProfile profile;

        private readonly HashSet<EnemySfxEmitter> growlCandidates = new HashSet<EnemySfxEmitter>();

        // オプション画面のSE音量調整に拾われるよう、どちらも非ループの2D AudioSourceとして作る。
        private AudioSource oneShotSource;
        private AudioSource growlSource;
        private float nextGrowlTime = -1f;
        // PlayOneShotはAudioSource.isPlayingだけだと重なり判定が不安定なため、終了予定時刻も保持する。
        private float growlPlayingUntil = -1f;
        private int growlCadenceCategory;

        public static EnemySfxManager Instance => instance;

        public static EnemySfxManager GetOrCreate(EnemySfxProfile preferredProfile = null)
        {
            if (instance == null)
            {
                // シーンに未配置でも、敵側から最初に呼ばれた時点で自動生成する。
                GameObject managerObject = new GameObject(nameof(EnemySfxManager));
                instance = managerObject.AddComponent<EnemySfxManager>();
            }

            instance.SetProfileIfMissing(preferredProfile);
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureAudioSources();
            SetProfileIfMissing(null);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Update()
        {
            int visibleGrowlCount = CountVisibleGrowlCandidates();
            if (visibleGrowlCount <= 0)
            {
                // 画面内に通常敵がいない間は、次回咆哮予約をリセットする。
                nextGrowlTime = -1f;
                growlCadenceCategory = 0;
                return;
            }

            // 画面内の通常敵数が1体か複数かで、咆哮の発生間隔を切り替える。
            int cadenceCategory = visibleGrowlCount >= 2 ? 2 : 1;
            if (nextGrowlTime < 0f || growlCadenceCategory != cadenceCategory)
            {
                ScheduleNextGrowl(visibleGrowlCount);
            }

            if (IsGrowlPlaying())
            {
                return;
            }

            if (Time.time < nextGrowlTime)
            {
                return;
            }

            PlayGrowl();
            ScheduleNextGrowl(visibleGrowlCount);
        }

        public void RegisterGrowlCandidate(EnemySfxEmitter emitter, EnemySfxProfile preferredProfile)
        {
            if (emitter == null)
            {
                return;
            }

            SetProfileIfMissing(preferredProfile);
            // HashSetで管理し、同じ敵が二重登録されても咆哮数が増えないようにする。
            growlCandidates.Add(emitter);
        }

        public void UnregisterGrowlCandidate(EnemySfxEmitter emitter)
        {
            if (emitter == null)
            {
                return;
            }

            growlCandidates.Remove(emitter);
        }

        public void PlayEnemyDead(EnemySfxProfile preferredProfile)
        {
            EnemySfxProfile sfxProfile = ResolveProfile(preferredProfile);
            PlayOneShot(sfxProfile?.EnemyDead, sfxProfile != null ? sfxProfile.EnemyDeadVolume : 1f);
        }

        public void PlayEnemyRangedAttack(EnemySfxProfile preferredProfile)
        {
            EnemySfxProfile sfxProfile = ResolveProfile(preferredProfile);
            PlayOneShot(sfxProfile?.EnemyRangedAttack, sfxProfile != null ? sfxProfile.EnemyRangedAttackVolume : 1f);
        }

        public void PlayEnemyTackleAttack(EnemySfxProfile preferredProfile)
        {
            EnemySfxProfile sfxProfile = ResolveProfile(preferredProfile);
            PlayOneShot(sfxProfile?.EnemyTackleAttack, sfxProfile != null ? sfxProfile.EnemyTackleAttackVolume : 1f);
        }

        public void PlayLastBossDead(EnemySfxProfile preferredProfile)
        {
            EnemySfxProfile sfxProfile = ResolveProfile(preferredProfile);
            PlayOneShot(sfxProfile?.LastBossDead, sfxProfile != null ? sfxProfile.LastBossDeadVolume : 1f);
        }

        public void PlayStageBossDead(EnemySfxProfile preferredProfile)
        {
            EnemySfxProfile sfxProfile = ResolveProfile(preferredProfile);
            PlayOneShot(sfxProfile?.StageBossDead, sfxProfile != null ? sfxProfile.StageBossDeadVolume : 1f);
        }

        private void PlayOneShot(AudioClip clip, float volumeScale)
        {
            if (clip == null)
            {
                return;
            }

            EnsureAudioSources();
            if (oneShotSource != null)
            {
                // volumeScaleでSEごとの個別音量をかける。AudioSource.volumeには全体SE音量が反映される。
                oneShotSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
            }
        }

        private void PlayGrowl()
        {
            EnemySfxProfile sfxProfile = ResolveProfile(null);
            AudioClip clip = sfxProfile != null ? sfxProfile.EnemyGrowl : null;
            if (clip == null || sfxProfile == null)
            {
                return;
            }

            EnsureAudioSources();
            if (growlSource == null || IsGrowlPlaying())
            {
                return;
            }

            // 咆哮は重ならないよう、専用AudioSourceで1回だけ鳴らす。
            growlSource.PlayOneShot(clip, Mathf.Clamp01(sfxProfile.EnemyGrowlVolume));
            growlPlayingUntil = Time.time + clip.length;
        }

        private bool IsGrowlPlaying()
        {
            return Time.time < growlPlayingUntil || (growlSource != null && growlSource.isPlaying);
        }

        private int CountVisibleGrowlCandidates()
        {
            // 破棄済み・無効化済みの敵を取り除いてから、画面内の通常敵だけ数える。
            growlCandidates.RemoveWhere(emitter => emitter == null || !emitter.isActiveAndEnabled);

            int count = 0;
            foreach (EnemySfxEmitter emitter in growlCandidates)
            {
                if (emitter != null && emitter.IsVisibleToMainCamera())
                {
                    count++;
                }
            }

            return count;
        }

        private void ScheduleNextGrowl(int visibleGrowlCount)
        {
            growlCadenceCategory = visibleGrowlCount >= 2 ? 2 : 1;
            // 1体なら6〜12秒、2体以上なら3〜7秒で次の咆哮を予約する。
            float minDelay = visibleGrowlCount >= 2 ? 3f : 6f;
            float maxDelay = visibleGrowlCount >= 2 ? 7f : 12f;
            nextGrowlTime = Time.time + Random.Range(minDelay, maxDelay);
        }

        private EnemySfxProfile ResolveProfile(EnemySfxProfile preferredProfile)
        {
            SetProfileIfMissing(preferredProfile);
            return profile;
        }

        private void SetProfileIfMissing(EnemySfxProfile preferredProfile)
        {
            if (profile != null)
            {
                return;
            }

            if (preferredProfile != null)
            {
                // prefab側に明示設定されたプロファイルを優先する。
                profile = preferredProfile;
                return;
            }

            // 明示設定がない場合は Resources から共通プロファイルを探す。
            profile = Resources.Load<EnemySfxProfile>(DefaultProfileResourcePath);
        }

        private void EnsureAudioSources()
        {
            if (oneShotSource == null)
            {
                oneShotSource = gameObject.AddComponent<AudioSource>();
                ConfigureSource(oneShotSource);
            }

            if (growlSource == null)
            {
                growlSource = gameObject.AddComponent<AudioSource>();
                ConfigureSource(growlSource);
            }
        }

        private static void ConfigureSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
        }
    }
}
