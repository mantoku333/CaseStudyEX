using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Playerのアクション状態に合わせて、子オブジェクト上の2つのParticleSystemを
/// 1つのAction Sparkleとしてまとめて制御するマネージャー。
/// このコンポーネントはFX_Action本体ではなく、任意の管理用GameObjectへ追加して使う。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ActionSparkleEffectManager : MonoBehaviour
{
    public enum SparkleAction
    {
        None,
        Glide,
        RecoilJump,
        Dodge,
        DiveAttack
    }

    private const int MinLevel = 0;
    private const int MaxLevel = 4;
    private const float EditorPreviewSimulationTime = 0.25f;

    [Header("References")]
    [SerializeField] private PlayerController targetPlayer;
    [SerializeField, Tooltip("2つのParticleSystemを子に持つAction Sparkleの親Transformです。")]
    private Transform sparkleEffectRoot;
    [SerializeField, Tooltip("未設定ならsparkleEffectRootの子ParticleSystemを自動取得します。")]
    private ParticleSystem[] sparkleParticles = Array.Empty<ParticleSystem>();

    [Header("Detection")]
    [SerializeField] private bool autoFindPlayerOnStart = true;
    [SerializeField, Min(0f)] private float comboGraceSeconds = 1.2f;

    [Header("Rendering")]
    [SerializeField, Tooltip("ParticleSystemRendererのSorting Orderを上書きして、Playerより前に表示しやすくします。")]
    private bool overrideParticleSorting = true;
    [SerializeField] private int particleSortingOrder = 50;

    [Header("Fallback Particle Values")]
    [SerializeField, Min(0f), Tooltip("Prefab側の放出量が0の場合に使う基準放出量です。")]
    private float fallbackRateOverTime = 16f;
    [SerializeField, Min(0.0001f), Tooltip("Prefab側のサイズが0の場合に使う基準サイズです。")]
    private float fallbackStartSize = 0.16f;
    [SerializeField, Min(0.0001f), Tooltip("Prefab側の寿命がほぼ0の場合に使う基準寿命です。")]
    private float fallbackStartLifetime = 0.7f;
    [SerializeField, Min(0f), Tooltip("Prefab側の速度が0の場合に使う基準速度です。")]
    private float fallbackStartSpeed = 2.6f;

    [Header("Runtime State")]
    [SerializeField, Range(MinLevel, MaxLevel)] private int eleganceLevel;
    [SerializeField] private SparkleAction currentAction = SparkleAction.None;

    [Header("Level Settings")]
    [SerializeField] private LevelSettings[] levelSettings = CreateDefaultLevelSettings();

    [Header("Action Settings")]
    [SerializeField] private ActionSettings glideSettings = new ActionSettings("Glide", 1f);
    [SerializeField] private ActionSettings recoilJumpSettings = new ActionSettings("RecoilJump", 1.15f);
    [SerializeField] private ActionSettings dodgeSettings = new ActionSettings("Dodge", 1.25f);
    [SerializeField] private ActionSettings diveAttackSettings = new ActionSettings("DiveAttack", 1.35f);

    [Header("Preview")]
    [SerializeField] private bool forcePreview;
    [SerializeField, Range(MinLevel, MaxLevel)] private int previewLevel = 1;
    [SerializeField] private SparkleAction previewAction = SparkleAction.Glide;

    private readonly List<ParticleBaseline> baselines = new List<ParticleBaseline>();
    private bool previousGliding;
    private bool previousRecoiling;
    private bool previousDodging;
    private bool previousDiveAttacking;
    private float lastElegantActionTime = float.NegativeInfinity;

    public int EleganceLevel => eleganceLevel;
    public SparkleAction CurrentAction => currentAction;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureManagersAfterSceneLoad()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerController player = players[i];
            if (player == null)
            {
                continue;
            }

            ActionSparkleEffectManager manager = player.GetComponent<ActionSparkleEffectManager>();
            if (manager == null)
            {
                manager = player.gameObject.AddComponent<ActionSparkleEffectManager>();
            }

            manager.SetTargetPlayer(player);
            manager.AutoAssignReferences();
        }
    }

    private void Awake()
    {
        EnsureSettings();
        ResolveReferences();
        CaptureBaselines();
        ApplyState();
    }

    private void OnEnable()
    {
        EnsureSettings();
        ResolveReferences();
        CapturePlayerState();
        ApplyState();
    }

    private void Update()
    {
        if (forcePreview)
        {
            SetLevelAndAction(previewLevel, previewAction);
            return;
        }

        ResolvePlayerIfNeeded();
        if (targetPlayer == null)
        {
            SetLevelAndAction(MinLevel, SparkleAction.None);
            return;
        }

        UpdateFromPlayerState();
        UpdateComboTimeout();
    }

    private void OnValidate()
    {
        eleganceLevel = Mathf.Clamp(eleganceLevel, MinLevel, MaxLevel);
        previewLevel = Mathf.Clamp(previewLevel, MinLevel, MaxLevel);
        comboGraceSeconds = Mathf.Max(0f, comboGraceSeconds);
        fallbackRateOverTime = Mathf.Max(0f, fallbackRateOverTime);
        fallbackStartSize = Mathf.Max(0.0001f, fallbackStartSize);
        fallbackStartLifetime = Mathf.Max(0.0001f, fallbackStartLifetime);
        fallbackStartSpeed = Mathf.Max(0f, fallbackStartSpeed);

        EnsureSettings();

        if (!Application.isPlaying && forcePreview)
        {
            SetLevelAndAction(previewLevel, previewAction);
        }
    }

    private void EnsureSettings()
    {
        if (levelSettings == null || levelSettings.Length != MaxLevel + 1)
        {
            LevelSettings[] defaults = CreateDefaultLevelSettings();
            LevelSettings[] resized = new LevelSettings[MaxLevel + 1];
            for (int i = 0; i < resized.Length; i++)
            {
                resized[i] = levelSettings != null && i < levelSettings.Length && levelSettings[i] != null
                    ? levelSettings[i]
                    : defaults[i];
            }

            levelSettings = resized;
        }

        for (int i = 0; i < levelSettings.Length; i++)
        {
            levelSettings[i]?.Clamp();
        }

        glideSettings?.Clamp();
        recoilJumpSettings?.Clamp();
        dodgeSettings?.Clamp();
        diveAttackSettings?.Clamp();
    }

    public void SetTargetPlayer(PlayerController player)
    {
        targetPlayer = player;
        CapturePlayerState();
    }

    public void SetSparkleEffectRoot(Transform root)
    {
        sparkleEffectRoot = root;
        sparkleParticles = Array.Empty<ParticleSystem>();
        ResolveParticles(true);
        CaptureBaselines();
        ApplyState();
    }

    public void SetEleganceLevel(int level)
    {
        SetLevelAndAction(level, currentAction);
    }

    public void ResetSparkle()
    {
        SetLevelAndAction(MinLevel, SparkleAction.None);
    }

    [ContextMenu("Auto Assign References")]
    public void AutoAssignReferences()
    {
        if (targetPlayer == null)
        {
            targetPlayer = GetComponentInParent<PlayerController>();
        }

        if (targetPlayer == null)
        {
            targetPlayer = FindFirstObjectByType<PlayerController>();
        }

        if (sparkleEffectRoot == null)
        {
            sparkleEffectRoot = FindSparkleEffectRoot();
        }

        RefreshParticlesFromEffectRoot();
        CapturePlayerState();
        ApplyState();
    }

    [ContextMenu("Refresh Particles From Effect Root")]
    public void RefreshParticlesFromEffectRoot()
    {
        sparkleParticles = Array.Empty<ParticleSystem>();
        ResolveParticles(true);
        CaptureBaselines();
    }

    public void PreviewLevel(int level, SparkleAction action)
    {
        forcePreview = true;
        previewLevel = Mathf.Clamp(level, MinLevel, MaxLevel);
        previewAction = action;
        SetLevelAndAction(previewLevel, previewAction);
    }

    public void StopPreview()
    {
        forcePreview = false;
        ResetSparkle();
    }

    public void NotifyElegantAction(SparkleAction action)
    {
        if (action == SparkleAction.None)
        {
            ResetSparkle();
            return;
        }

        AdvanceEleganceForAction(action);
        SetLevelAndAction(eleganceLevel, action);
    }

    [ContextMenu("Preview Lv1 Glide")]
    private void PreviewLv1Glide()
    {
        PreviewLevel(1, SparkleAction.Glide);
    }

    [ContextMenu("Preview Lv4 Glide")]
    private void PreviewLv4Glide()
    {
        PreviewLevel(4, SparkleAction.Glide);
    }

    [ContextMenu("Stop Sparkle")]
    private void StopSparkle()
    {
        StopPreview();
    }

    private void ResolveReferences()
    {
        ResolvePlayerIfNeeded();
        ResolveSparkleEffectRootIfNeeded();
        ResolveParticles(false);
    }

    private void ResolvePlayerIfNeeded()
    {
        if (targetPlayer != null || !autoFindPlayerOnStart)
        {
            return;
        }

        targetPlayer = FindFirstObjectByType<PlayerController>();
    }

    private void ResolveSparkleEffectRootIfNeeded()
    {
        if (sparkleEffectRoot != null)
        {
            return;
        }

        sparkleEffectRoot = FindSparkleEffectRoot();
    }

    private void ResolveParticles(bool forceRefresh)
    {
        if (!forceRefresh && sparkleParticles != null && sparkleParticles.Length > 0)
        {
            return;
        }

        if (sparkleEffectRoot == null)
        {
            return;
        }

        ParticleSystem rootParticle = sparkleEffectRoot.GetComponent<ParticleSystem>();
        ParticleSystem[] foundParticles = sparkleEffectRoot.GetComponentsInChildren<ParticleSystem>(true);
        List<ParticleSystem> childParticles = new List<ParticleSystem>(foundParticles.Length);
        for (int i = 0; i < foundParticles.Length; i++)
        {
            ParticleSystem particle = foundParticles[i];
            if (particle != null && particle != rootParticle)
            {
                childParticles.Add(particle);
            }
        }

        sparkleParticles = childParticles.Count > 0 ? childParticles.ToArray() : foundParticles;
    }

    private Transform FindSparkleEffectRoot()
    {
        Transform searchRoot = targetPlayer != null ? targetPlayer.transform : transform;
        Transform namedRoot = FindChildByName(searchRoot, "FX_Action");
        if (namedRoot != null)
        {
            return namedRoot;
        }

        ParticleSystem[] particles = searchRoot.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle == null)
            {
                continue;
            }

            Transform candidate = particle.transform.parent;
            if (candidate != null && candidate.GetComponentsInChildren<ParticleSystem>(true).Length >= 2)
            {
                return candidate;
            }
        }

        return null;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void CaptureBaselines()
    {
        baselines.Clear();
        if (sparkleParticles == null)
        {
            return;
        }

        for (int i = 0; i < sparkleParticles.Length; i++)
        {
            ParticleSystem particle = sparkleParticles[i];
            if (particle != null)
            {
                baselines.Add(CreateBaseline(particle));
            }
        }
    }

    private void UpdateFromPlayerState()
    {
        bool gliding = targetPlayer.IsGliding;
        bool recoiling = targetPlayer.IsRecoilBoosting;
        bool dodging = targetPlayer.IsDodging;
        bool diveAttacking = targetPlayer.IsDiveAttacking;

        if (gliding && !previousGliding)
        {
            AdvanceEleganceForAction(SparkleAction.Glide);
        }

        if (recoiling && !previousRecoiling)
        {
            AdvanceEleganceForAction(SparkleAction.RecoilJump);
        }

        if (dodging && !previousDodging)
        {
            AdvanceEleganceForAction(SparkleAction.Dodge);
        }

        if (diveAttacking && !previousDiveAttacking)
        {
            AdvanceEleganceForAction(SparkleAction.DiveAttack);
        }

        SetLevelAndAction(eleganceLevel, ResolveCurrentAction(gliding, recoiling, dodging, diveAttacking));

        previousGliding = gliding;
        previousRecoiling = recoiling;
        previousDodging = dodging;
        previousDiveAttacking = diveAttacking;
    }

    private SparkleAction ResolveCurrentAction(bool gliding, bool recoiling, bool dodging, bool diveAttacking)
    {
        if (diveAttacking) { return SparkleAction.DiveAttack; }
        if (dodging) { return SparkleAction.Dodge; }
        if (recoiling) { return SparkleAction.RecoilJump; }
        if (gliding) { return SparkleAction.Glide; }
        return SparkleAction.None;
    }

    private void AdvanceEleganceForAction(SparkleAction action)
    {
        bool insideComboWindow = Time.time <= lastElegantActionTime + comboGraceSeconds;
        lastElegantActionTime = Time.time;

        if (action == SparkleAction.Glide)
        {
            eleganceLevel = Mathf.Max(eleganceLevel, 1);
            return;
        }

        bool chainedFromGlide = previousGliding || currentAction == SparkleAction.Glide;
        eleganceLevel = eleganceLevel > 0 && (insideComboWindow || chainedFromGlide)
            ? Mathf.Clamp(eleganceLevel + 1, MinLevel, MaxLevel)
            : 1;
    }

    private void UpdateComboTimeout()
    {
        if (currentAction != SparkleAction.None || eleganceLevel == MinLevel)
        {
            return;
        }

        if (Time.time > lastElegantActionTime + comboGraceSeconds)
        {
            SetLevelAndAction(MinLevel, SparkleAction.None);
        }
    }

    private void SetLevelAndAction(int level, SparkleAction action)
    {
        eleganceLevel = Mathf.Clamp(level, MinLevel, MaxLevel);
        currentAction = action;
        ApplyState();
    }

    private void ApplyState()
    {
        ResolveParticles(false);

        bool shouldPlay = eleganceLevel > MinLevel && currentAction != SparkleAction.None;
        LevelSettings level = GetLevelSettings(eleganceLevel);
        ActionSettings action = GetActionSettings(currentAction);

        for (int i = 0; i < sparkleParticles.Length; i++)
        {
            ParticleSystem particle = sparkleParticles[i];
            if (particle == null)
            {
                continue;
            }

            ParticleBaseline baseline = GetBaseline(i, particle);
            ParticleSettings particleSettings = level.GetParticleSettings(i);
            bool shouldEmit = shouldPlay && level.emissionEnabled && particleSettings.enabled;
            ApplyRendererSettings(particle);
            ApplyParticleSettings(particle, baseline, level, particleSettings, action, shouldEmit);

            if (shouldEmit)
            {
                if (!particle.isPlaying)
                {
                    particle.Play(true);
                }

                SimulateEditorPreviewIfNeeded(particle);
            }
        }
    }

    private void ApplyRendererSettings(ParticleSystem particle)
    {
        if (!overrideParticleSorting || particle == null)
        {
            return;
        }

        ParticleSystemRenderer renderer = particle.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = particleSortingOrder;
        }
    }

    private void SimulateEditorPreviewIfNeeded(ParticleSystem particle)
    {
        if (Application.isPlaying || !forcePreview)
        {
            return;
        }

        particle.Simulate(EditorPreviewSimulationTime, true, true, false);
    }

    private void ApplyParticleSettings(
        ParticleSystem particle,
        ParticleBaseline baseline,
        LevelSettings level,
        ParticleSettings particleSettings,
        ActionSettings action,
        bool shouldEmit)
    {
        float actionSpeed = action != null ? action.speedMultiplier : 1f;

        ParticleSystem.MainModule main = particle.main;
        main.startSizeMultiplier = baseline.startSizeMultiplier * level.startSizeMultiplier * particleSettings.sizeMultiplier;
        main.startLifetimeMultiplier = baseline.startLifetimeMultiplier * level.startLifetimeMultiplier * particleSettings.lifetimeMultiplier;
        main.startSpeedMultiplier = baseline.startSpeedMultiplier * level.startSpeedMultiplier * particleSettings.speedMultiplier * actionSpeed;

        ParticleSystem.EmissionModule emission = particle.emission;
        emission.enabled = true;
        emission.rateOverTimeMultiplier = shouldEmit
            ? baseline.rateOverTimeMultiplier * level.rateOverTimeMultiplier * particleSettings.rateMultiplier
            : 0f;
        emission.SetBursts(shouldEmit ? baseline.bursts : Array.Empty<ParticleSystem.Burst>());

        if (level.overrideStartColor)
        {
            main.startColor = MultiplyColor(level.startColor, particleSettings.colorMultiplier);
        }
    }

    private ParticleBaseline GetBaseline(int index, ParticleSystem particle)
    {
        while (baselines.Count <= index)
        {
            baselines.Add(CreateBaseline(particle));
        }

        if (baselines[index].Particle != particle)
        {
            baselines[index] = CreateBaseline(particle);
        }

        return baselines[index];
    }

    private ParticleBaseline CreateBaseline(ParticleSystem particle)
    {
        return new ParticleBaseline(
            particle,
            fallbackRateOverTime,
            fallbackStartSize,
            fallbackStartLifetime,
            fallbackStartSpeed);
    }

    private void CapturePlayerState()
    {
        if (targetPlayer == null)
        {
            previousGliding = false;
            previousRecoiling = false;
            previousDodging = false;
            previousDiveAttacking = false;
            return;
        }

        previousGliding = targetPlayer.IsGliding;
        previousRecoiling = targetPlayer.IsRecoilBoosting;
        previousDodging = targetPlayer.IsDodging;
        previousDiveAttacking = targetPlayer.IsDiveAttacking;
    }

    private LevelSettings GetLevelSettings(int level)
    {
        int index = Mathf.Clamp(level, 0, levelSettings.Length - 1);
        return levelSettings[index];
    }

    private ActionSettings GetActionSettings(SparkleAction action)
    {
        switch (action)
        {
            case SparkleAction.Glide:
                return glideSettings;
            case SparkleAction.RecoilJump:
                return recoilJumpSettings;
            case SparkleAction.Dodge:
                return dodgeSettings;
            case SparkleAction.DiveAttack:
                return diveAttackSettings;
            default:
                return null;
        }
    }

    private static Color MultiplyColor(Color color, Color multiplier)
    {
        return new Color(
            color.r * multiplier.r,
            color.g * multiplier.g,
            color.b * multiplier.b,
            color.a * multiplier.a);
    }

    private static LevelSettings[] CreateDefaultLevelSettings()
    {
        return new[]
        {
            new LevelSettings("Lv0 Off", false, 0f, 0f, 0f, 0f),
            new LevelSettings("Lv1 Weak", true, 0.5f, 0.85f, 0.9f, 0.9f),
            new LevelSettings("Lv2 Flow", true, 0.8f, 1f, 1f, 1f),
            new LevelSettings("Lv3 Show", true, 1.2f, 1.15f, 1.05f, 1.1f),
            new LevelSettings("Lv4 Bloom", true, 1.6f, 1.3f, 1.1f, 1.25f)
        };
    }

    [Serializable]
    public sealed class LevelSettings
    {
        [SerializeField] private string label;
        public bool emissionEnabled = true;
        [Min(0f)] public float rateOverTimeMultiplier = 1f;
        [Min(0f)] public float startSizeMultiplier = 1f;
        [Min(0f)] public float startLifetimeMultiplier = 1f;
        [Min(0f)] public float startSpeedMultiplier = 1f;
        public bool overrideStartColor;
        public Color startColor = Color.white;
        public ParticleSettings[] particles = CreateDefaultParticleSettings();

        public LevelSettings(
            string label,
            bool emissionEnabled,
            float rateOverTimeMultiplier,
            float startSizeMultiplier,
            float startLifetimeMultiplier,
            float startSpeedMultiplier)
        {
            this.label = label;
            this.emissionEnabled = emissionEnabled;
            this.rateOverTimeMultiplier = rateOverTimeMultiplier;
            this.startSizeMultiplier = startSizeMultiplier;
            this.startLifetimeMultiplier = startLifetimeMultiplier;
            this.startSpeedMultiplier = startSpeedMultiplier;
        }

        public void Clamp()
        {
            rateOverTimeMultiplier = Mathf.Max(0f, rateOverTimeMultiplier);
            startSizeMultiplier = Mathf.Max(0f, startSizeMultiplier);
            startLifetimeMultiplier = Mathf.Max(0f, startLifetimeMultiplier);
            startSpeedMultiplier = Mathf.Max(0f, startSpeedMultiplier);

            if (particles == null || particles.Length == 0)
            {
                particles = CreateDefaultParticleSettings();
            }

            for (int i = 0; i < particles.Length; i++)
            {
                particles[i]?.Clamp();
            }
        }

        public ParticleSettings GetParticleSettings(int index)
        {
            if (particles == null || index < 0 || index >= particles.Length || particles[index] == null)
            {
                return ParticleSettings.Default;
            }

            return particles[index];
        }

        private static ParticleSettings[] CreateDefaultParticleSettings()
        {
            return new[]
            {
                new ParticleSettings("Particle 0"),
                new ParticleSettings("Particle 1")
            };
        }
    }

    [Serializable]
    public sealed class ParticleSettings
    {
        public static readonly ParticleSettings Default = new ParticleSettings("Default");

        [SerializeField] private string label;
        public bool enabled = true;
        [Min(0f)] public float rateMultiplier = 1f;
        [Min(0f)] public float sizeMultiplier = 1f;
        [Min(0f)] public float lifetimeMultiplier = 1f;
        [Min(0f)] public float speedMultiplier = 1f;
        public Color colorMultiplier = Color.white;

        public ParticleSettings(string label)
        {
            this.label = label;
        }

        public void Clamp()
        {
            rateMultiplier = Mathf.Max(0f, rateMultiplier);
            sizeMultiplier = Mathf.Max(0f, sizeMultiplier);
            lifetimeMultiplier = Mathf.Max(0f, lifetimeMultiplier);
            speedMultiplier = Mathf.Max(0f, speedMultiplier);
        }
    }

    [Serializable]
    public sealed class ActionSettings
    {
        [SerializeField] private string label;
        [Min(0f)] public float speedMultiplier = 1f;

        public ActionSettings(string label, float speedMultiplier)
        {
            this.label = label;
            this.speedMultiplier = speedMultiplier;
        }

        public void Clamp()
        {
            speedMultiplier = Mathf.Max(0f, speedMultiplier);
        }
    }

    private struct ParticleBaseline
    {
        public readonly ParticleSystem Particle;
        public readonly float startSizeMultiplier;
        public readonly float startLifetimeMultiplier;
        public readonly float startSpeedMultiplier;
        public readonly float rateOverTimeMultiplier;
        public readonly ParticleSystem.Burst[] bursts;

        public ParticleBaseline(
            ParticleSystem particle,
            float fallbackRateOverTime,
            float fallbackStartSize,
            float fallbackStartLifetime,
            float fallbackStartSpeed)
        {
            Particle = particle;

            ParticleSystem.MainModule main = particle.main;
            startSizeMultiplier = main.startSizeMultiplier > 0.0001f ? main.startSizeMultiplier : fallbackStartSize;
            startLifetimeMultiplier = main.startLifetimeMultiplier > 0.0001f ? main.startLifetimeMultiplier : fallbackStartLifetime;
            startSpeedMultiplier = main.startSpeedMultiplier > 0.0001f ? main.startSpeedMultiplier : fallbackStartSpeed;

            ParticleSystem.EmissionModule emission = particle.emission;
            rateOverTimeMultiplier = emission.rateOverTimeMultiplier > 0.0001f
                ? emission.rateOverTimeMultiplier
                : fallbackRateOverTime;

            bursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(bursts);
        }
    }
}
