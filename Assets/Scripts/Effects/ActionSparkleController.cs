using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Playerの優雅アクション状態に応じて、共通Action Sparkleの量と向きを制御する。
/// FX_Action.prefabのルートへ追加し、targetPlayerにPlayerControllerを割り当てて使う。
/// </summary>
[DisallowMultipleComponent]
public sealed class ActionSparkleController : MonoBehaviour
{
    public enum SparkleAction
    {
        None,
        Glide,
        RecoilJump,
        Dodge,
        DiveAttack
    }

    private const int MinEleganceLevel = 0;
    private const int MaxEleganceLevel = 4;

    [Header("Player")]
    [SerializeField] private PlayerController targetPlayer;
    [SerializeField] private bool autoFindPlayerOnStart = true;
    [SerializeField] private bool followPlayer = true;
    [SerializeField] private bool mirrorOffsetByFacing = true;
    [SerializeField, Min(0f)] private float comboGraceSeconds = 1.2f;

    [Header("Particle Systems")]
    [SerializeField, Tooltip("FX_Actionのルート自身にあるParticleSystemをAction Sparkle本体として扱うかです。重なった子ParticleSystemだけを使う場合はOFFにします。")]
    private bool includeRootParticleSystem;

    [SerializeField, Tooltip("ルートParticleSystemを使わない場合、誤って発光しないよう停止します。")]
    private bool stopIgnoredRootParticleSystem = true;

    [SerializeField] private ParticleSystem[] actionParticleSystems = Array.Empty<ParticleSystem>();
    [SerializeField] private ParticleSystem[] comboTrailParticleSystems = Array.Empty<ParticleSystem>();

    [Header("Prefab Particle Settings")]
    [SerializeField, Tooltip("ONにするとFX_Action内の既存ParticleSystem設定を温存し、レベル設定は倍率として反映します。")]
    private bool preservePrefabParticleSettings = true;

    [Header("Elegance Levels")]
    [SerializeField] private int eleganceLevel;
    [SerializeField] private LevelSettings[] levelSettings = CreateDefaultLevelSettings();

    [Header("Action Presets")]
    [SerializeField] private ActionPreset glidePreset = ActionPreset.CreateGlide();
    [SerializeField] private ActionPreset recoilJumpPreset = ActionPreset.CreateRecoilJump();
    [SerializeField] private ActionPreset dodgePreset = ActionPreset.CreateDodge();
    [SerializeField] private ActionPreset diveAttackPreset = ActionPreset.CreateDiveAttack();

    [Header("Debug Preview")]
    [SerializeField, Tooltip("ONにするとPlayer状態を見ず、指定したレベルとアクションで強制再生します。")]
    private bool forcePreview;
    [SerializeField, Range(MinEleganceLevel, MaxEleganceLevel)] private int previewEleganceLevel = 1;
    [SerializeField] private SparkleAction previewAction = SparkleAction.Glide;

    private SparkleAction currentAction = SparkleAction.None;
    private bool previousGliding;
    private bool previousRecoiling;
    private bool previousDodging;
    private bool previousDiveAttacking;
    private float lastElegantActionTime = float.NegativeInfinity;
    private Transform targetTransform;
    private Quaternion initialLocalRotation;
    private ParticleSystem rootParticleSystem;

    public int EleganceLevel => eleganceLevel;
    public SparkleAction CurrentAction => currentAction;

    private void Awake()
    {
        initialLocalRotation = transform.localRotation;
        ResolveParticleSystemsIfNeeded();
        SetEleganceLevel(eleganceLevel, true);
    }

    private void Start()
    {
        ResolveTargetPlayerIfNeeded();
    }

    private void OnEnable()
    {
        CapturePlayerState();
        ApplyCurrentSettings(true);
    }

    private void Update()
    {
        if (forcePreview)
        {
            ApplyPreview();
            return;
        }

        ResolveTargetPlayerIfNeeded();

        if (targetPlayer == null)
        {
            SetCurrentAction(SparkleAction.None);
            return;
        }

        UpdateFromPlayerState();
        UpdateFollowTransform();
        UpdateComboTimeout();
    }

    private void OnValidate()
    {
        eleganceLevel = Mathf.Clamp(eleganceLevel, MinEleganceLevel, MaxEleganceLevel);
        comboGraceSeconds = Mathf.Max(0f, comboGraceSeconds);

        if (levelSettings == null || levelSettings.Length != MaxEleganceLevel + 1)
        {
            LevelSettings[] defaults = CreateDefaultLevelSettings();
            LevelSettings[] resized = new LevelSettings[MaxEleganceLevel + 1];

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

        glidePreset?.Clamp();
        recoilJumpPreset?.Clamp();
        dodgePreset?.Clamp();
        diveAttackPreset?.Clamp();
    }

    public void SetTargetPlayer(PlayerController player)
    {
        targetPlayer = player;
        targetTransform = player != null ? player.transform : null;
        CapturePlayerState();
        ApplyCurrentSettings(true);
    }

    public void SetEleganceLevel(int level)
    {
        SetEleganceLevel(level, true);
    }

    public void ResetElegance()
    {
        SetCurrentAction(SparkleAction.None);
        SetEleganceLevel(MinEleganceLevel, true);
    }

    public void NotifyElegantAction(SparkleAction action)
    {
        if (action == SparkleAction.None)
        {
            ResetElegance();
            return;
        }

        AdvanceEleganceForAction(action);
        SetCurrentAction(action);
    }

    [ContextMenu("Preview Lv1 Glide")]
    private void PreviewLv1Glide()
    {
        Preview(1, SparkleAction.Glide);
    }

    [ContextMenu("Preview Lv4 Glide")]
    private void PreviewLv4Glide()
    {
        Preview(4, SparkleAction.Glide);
    }

    [ContextMenu("Stop Preview")]
    private void StopPreview()
    {
        forcePreview = false;
        ResetElegance();
    }

    private void Preview(int level, SparkleAction action)
    {
        forcePreview = true;
        previewEleganceLevel = Mathf.Clamp(level, MinEleganceLevel, MaxEleganceLevel);
        previewAction = action;
        ApplyPreview();
    }

    private void ApplyPreview()
    {
        SetEleganceLevel(previewEleganceLevel, false);
        SetCurrentAction(previewAction);
        ApplyCurrentSettings(true);
    }

    private void ResolveTargetPlayerIfNeeded()
    {
        if (targetPlayer != null)
        {
            targetTransform = targetPlayer.transform;
            return;
        }

        if (!autoFindPlayerOnStart)
        {
            targetTransform = null;
            return;
        }

        targetPlayer = FindFirstObjectByType<PlayerController>();
        targetTransform = targetPlayer != null ? targetPlayer.transform : null;
        CapturePlayerState();
    }

    private void ResolveParticleSystemsIfNeeded()
    {
        if (actionParticleSystems == null || actionParticleSystems.Length == 0)
        {
            rootParticleSystem = GetComponent<ParticleSystem>();
            ParticleSystem[] foundParticleSystems = GetComponentsInChildren<ParticleSystem>(true);

            if (includeRootParticleSystem || rootParticleSystem == null)
            {
                actionParticleSystems = foundParticleSystems;
                return;
            }

            List<ParticleSystem> childParticleSystems = new List<ParticleSystem>(foundParticleSystems.Length);
            for (int i = 0; i < foundParticleSystems.Length; i++)
            {
                ParticleSystem particleSystem = foundParticleSystems[i];
                if (particleSystem != null && particleSystem != rootParticleSystem)
                {
                    childParticleSystems.Add(particleSystem);
                }
            }

            StopRootParticleSystemIfIgnored();

            actionParticleSystems = childParticleSystems.Count > 0
                ? childParticleSystems.ToArray()
                : foundParticleSystems;
        }
    }

    private void StopRootParticleSystemIfIgnored()
    {
        if (includeRootParticleSystem || !stopIgnoredRootParticleSystem || rootParticleSystem == null)
        {
            return;
        }

        rootParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

        SparkleAction resolvedAction = ResolveCurrentAction(gliding, recoiling, dodging, diveAttacking);
        SetCurrentAction(resolvedAction);

        previousGliding = gliding;
        previousRecoiling = recoiling;
        previousDodging = dodging;
        previousDiveAttacking = diveAttacking;
    }

    private SparkleAction ResolveCurrentAction(
        bool gliding,
        bool recoiling,
        bool dodging,
        bool diveAttacking)
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
            SetEleganceLevel(Mathf.Max(eleganceLevel, 1), true);
            return;
        }

        bool chainedFromGlide = previousGliding || currentAction == SparkleAction.Glide;
        int nextLevel = eleganceLevel > 0 && (insideComboWindow || chainedFromGlide) ? eleganceLevel + 1 : 1;
        SetEleganceLevel(nextLevel, true);
    }

    private void UpdateComboTimeout()
    {
        if (currentAction != SparkleAction.None || eleganceLevel == MinEleganceLevel)
        {
            return;
        }

        if (Time.time > lastElegantActionTime + comboGraceSeconds)
        {
            SetEleganceLevel(MinEleganceLevel, true);
        }
    }

    private void SetCurrentAction(SparkleAction action)
    {
        if (currentAction == action)
        {
            return;
        }

        currentAction = action;
        ApplyCurrentSettings(true);
    }

    private void SetEleganceLevel(int level, bool apply)
    {
        int clampedLevel = Mathf.Clamp(level, MinEleganceLevel, MaxEleganceLevel);
        if (eleganceLevel == clampedLevel && !apply)
        {
            return;
        }

        eleganceLevel = clampedLevel;

        if (apply)
        {
            ApplyCurrentSettings(false);
        }
    }

    private void ApplyCurrentSettings(bool includeActionPreset)
    {
        ResolveParticleSystemsIfNeeded();

        LevelSettings settings = GetLevelSettings(eleganceLevel);
        ActionPreset preset = GetActionPreset(currentAction);
        bool shouldPlayActionSparkle = eleganceLevel > MinEleganceLevel && currentAction != SparkleAction.None;

        for (int i = 0; i < actionParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = actionParticleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            ApplyLevelSettings(particleSystem, settings, preset, i);

            if (shouldPlayActionSparkle)
            {
                if (!particleSystem.isPlaying)
                {
                    particleSystem.Play(true);
                }
            }
            else if (particleSystem.isPlaying || particleSystem.isEmitting)
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        SetParticleSystemsPlaying(comboTrailParticleSystems, eleganceLevel >= MaxEleganceLevel && shouldPlayActionSparkle);

        if (includeActionPreset)
        {
            ApplyActionTransform(preset);
        }
    }

    private void ApplyLevelSettings(
        ParticleSystem particleSystem,
        LevelSettings settings,
        ActionPreset preset,
        int particleLayerIndex)
    {
        if (settings == null)
        {
            return;
        }

        ParticleLayerSettings layerSettings = settings.GetParticleLayerSettings(particleLayerIndex);
        bool layerEnabled = layerSettings == null || layerSettings.enabled;
        float layerRateMultiplier = layerSettings != null ? layerSettings.rateMultiplier : 1f;
        float layerSizeMultiplier = layerSettings != null ? layerSettings.sizeMultiplier : 1f;
        float layerLifetimeMultiplier = layerSettings != null ? layerSettings.lifetimeMultiplier : 1f;
        float layerSpeedMultiplier = layerSettings != null ? layerSettings.speedMultiplier : 1f;
        Color layerColorMultiplier = layerSettings != null ? layerSettings.colorMultiplier : Color.white;

        ParticleSystem.MainModule main = particleSystem.main;
        float actionSpeedMultiplier = preset != null ? preset.speedMultiplier : 1f;

        if (preservePrefabParticleSettings)
        {
            main.startLifetimeMultiplier = settings.startLifetime * layerLifetimeMultiplier;
            main.startSpeedMultiplier = settings.startSpeed * layerSpeedMultiplier * actionSpeedMultiplier;
            main.startSizeMultiplier = settings.startSize * layerSizeMultiplier;
        }
        else
        {
            main.startLifetime = settings.startLifetime * layerLifetimeMultiplier;
            main.startSpeed = settings.startSpeed * layerSpeedMultiplier * actionSpeedMultiplier;
            main.startSize = settings.startSize * layerSizeMultiplier;
            main.gravityModifier = settings.gravityModifier;
            main.simulationSpeed = settings.simulationSpeed;
            main.maxParticles = settings.maxParticles;
        }

        if (settings.overrideStartColor)
        {
            main.startColor = MultiplyColor(settings.startColor, layerColorMultiplier);
        }

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = settings.emissionEnabled && layerEnabled;
        if (preservePrefabParticleSettings)
        {
            emission.rateOverTimeMultiplier = settings.rateOverTime * layerRateMultiplier;
        }
        else
        {
            emission.rateOverTime = settings.rateOverTime * layerRateMultiplier;
        }

        if (!preservePrefabParticleSettings || settings.overrideShape || (preset != null && preset.overrideShape))
        {
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = settings.shapeEnabled;
            shape.shapeType = preset != null && preset.overrideShape ? preset.shapeType : settings.shapeType;
            shape.radius = preset != null && preset.overrideShape ? preset.shapeRadius : settings.shapeRadius;
            shape.angle = settings.shapeAngle;
            shape.scale = settings.shapeScale;
        }

        if (!preservePrefabParticleSettings || settings.overrideNoise)
        {
            ParticleSystem.NoiseModule noise = particleSystem.noise;
            noise.enabled = settings.noiseEnabled;
            noise.strength = settings.noiseStrength;
            noise.frequency = settings.noiseFrequency;
            noise.scrollSpeed = settings.noiseScrollSpeed;
        }

        if (!preservePrefabParticleSettings || settings.overrideColorOverLifetime)
        {
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = settings.colorOverLifetimeEnabled;
            colorOverLifetime.color = settings.colorOverLifetime;
        }
    }

    private void ApplyActionTransform(ActionPreset preset)
    {
        transform.localRotation = preset == null
            ? initialLocalRotation
            : initialLocalRotation * Quaternion.Euler(preset.localRotationEuler);
    }

    private void UpdateFollowTransform()
    {
        if (!followPlayer || targetTransform == null)
        {
            return;
        }

        ActionPreset preset = GetActionPreset(currentAction);
        Vector3 offset = preset != null ? preset.localOffset : Vector3.zero;

        if (mirrorOffsetByFacing && targetPlayer != null && !targetPlayer.IsFacingRight)
        {
            offset.x *= -1f;
        }

        if (transform.parent == targetTransform)
        {
            transform.localPosition = offset;
        }
        else
        {
            transform.position = targetTransform.TransformPoint(offset);
        }
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
        if (levelSettings == null || levelSettings.Length == 0)
        {
            return null;
        }

        int index = Mathf.Clamp(level, 0, levelSettings.Length - 1);
        return levelSettings[index];
    }

    private ActionPreset GetActionPreset(SparkleAction action)
    {
        switch (action)
        {
            case SparkleAction.Glide:
                return glidePreset;
            case SparkleAction.RecoilJump:
                return recoilJumpPreset;
            case SparkleAction.Dodge:
                return dodgePreset;
            case SparkleAction.DiveAttack:
                return diveAttackPreset;
            default:
                return null;
        }
    }

    private static void SetParticleSystemsPlaying(ParticleSystem[] particleSystems, bool playing)
    {
        if (particleSystems == null)
        {
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
            {
                continue;
            }

            if (playing)
            {
                if (!particleSystem.isPlaying)
                {
                    particleSystem.Play(true);
                }
            }
            else if (particleSystem.isPlaying || particleSystem.isEmitting)
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    private static LevelSettings[] CreateDefaultLevelSettings()
    {
        return new[]
        {
            new LevelSettings("Lv0 Off", false, 0f, 0f, 0f, 0f, new Color(1f, 1f, 1f, 0f)),
            new LevelSettings("Lv1 Weak", true, 0.45f, 0.85f, 0.9f, 0.85f, Color.white),
            new LevelSettings("Lv2 Flow", true, 0.75f, 1f, 1f, 1f, Color.white),
            new LevelSettings("Lv3 Show", true, 1.15f, 1.12f, 1.05f, 1.1f, Color.white),
            new LevelSettings("Lv4 Bloom", true, 1.6f, 1.28f, 1.1f, 1.25f, Color.white)
        };
    }

    private static Color MultiplyColor(Color color, Color multiplier)
    {
        return new Color(
            color.r * multiplier.r,
            color.g * multiplier.g,
            color.b * multiplier.b,
            color.a * multiplier.a);
    }

    [Serializable]
    public sealed class LevelSettings
    {
        [SerializeField] private string label;

        [Header("Emission")]
        public bool emissionEnabled = true;
        [Tooltip("Prefab Particle SettingsがONの場合はRate over Time倍率、OFFの場合は絶対値です。")]
        [Min(0f)] public float rateOverTime = 10f;

        [Header("Main")]
        [Tooltip("Prefab Particle SettingsがONの場合はStart Size倍率、OFFの場合は絶対値です。")]
        [Min(0f)] public float startSize = 0.08f;
        [Tooltip("Prefab Particle SettingsがONの場合はStart Lifetime倍率、OFFの場合は絶対値です。")]
        [Min(0f)] public float startLifetime = 1.2f;
        [Tooltip("Prefab Particle SettingsがONの場合はStart Speed倍率、OFFの場合は絶対値です。")]
        [Min(0f)] public float startSpeed = 1.5f;
        public bool overrideStartColor;
        public Color startColor = Color.white;
        [Min(0f)] public float gravityModifier;
        [Min(0f)] public float simulationSpeed = 1f;
        [Min(1)] public int maxParticles = 200;

        [Header("Shape")]
        public bool overrideShape;
        public bool shapeEnabled = true;
        public ParticleSystemShapeType shapeType = ParticleSystemShapeType.Cone;
        [Min(0f)] public float shapeRadius = 0.15f;
        [Range(0f, 90f)] public float shapeAngle = 25f;
        public Vector3 shapeScale = Vector3.one;

        [Header("Noise")]
        public bool overrideNoise;
        public bool noiseEnabled = false;
        [Min(0f)] public float noiseStrength = 0.15f;
        [Min(0f)] public float noiseFrequency = 0.6f;
        public float noiseScrollSpeed = 0.25f;

        [Header("Color Over Lifetime")]
        public bool overrideColorOverLifetime;
        public bool colorOverLifetimeEnabled = true;
        public Gradient colorOverLifetime = CreateDefaultGradient();

        [Header("Particle Layers")]
        public ParticleLayerSettings[] particleLayers = CreateDefaultParticleLayers();

        public LevelSettings(
            string label,
            bool emissionEnabled,
            float rateOverTime,
            float startSize,
            float startLifetime,
            float startSpeed,
            Color startColor)
        {
            this.label = label;
            this.emissionEnabled = emissionEnabled;
            this.rateOverTime = rateOverTime;
            this.startSize = startSize;
            this.startLifetime = startLifetime;
            this.startSpeed = startSpeed;
            this.startColor = startColor;
        }

        public void Clamp()
        {
            rateOverTime = Mathf.Max(0f, rateOverTime);
            startSize = Mathf.Max(0f, startSize);
            startLifetime = Mathf.Max(0f, startLifetime);
            startSpeed = Mathf.Max(0f, startSpeed);
            gravityModifier = Mathf.Max(0f, gravityModifier);
            simulationSpeed = Mathf.Max(0f, simulationSpeed);
            maxParticles = Mathf.Max(1, maxParticles);
            shapeRadius = Mathf.Max(0f, shapeRadius);
            shapeAngle = Mathf.Clamp(shapeAngle, 0f, 90f);
            noiseStrength = Mathf.Max(0f, noiseStrength);
            noiseFrequency = Mathf.Max(0f, noiseFrequency);

            if (particleLayers == null || particleLayers.Length == 0)
            {
                particleLayers = CreateDefaultParticleLayers();
            }

            for (int i = 0; i < particleLayers.Length; i++)
            {
                particleLayers[i]?.Clamp();
            }
        }

        public ParticleLayerSettings GetParticleLayerSettings(int index)
        {
            if (particleLayers == null || index < 0 || index >= particleLayers.Length)
            {
                return null;
            }

            return particleLayers[index];
        }

        private static Gradient CreateDefaultGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 0.7f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static ParticleLayerSettings[] CreateDefaultParticleLayers()
        {
            return new[]
            {
                new ParticleLayerSettings { label = "Layer 0", rateMultiplier = 1f, sizeMultiplier = 1f, lifetimeMultiplier = 1f, speedMultiplier = 1f },
                new ParticleLayerSettings { label = "Layer 1", rateMultiplier = 1f, sizeMultiplier = 1f, lifetimeMultiplier = 1f, speedMultiplier = 1f }
            };
        }
    }

    [Serializable]
    public sealed class ParticleLayerSettings
    {
        public string label;
        public bool enabled = true;
        [Min(0f)] public float rateMultiplier = 1f;
        [Min(0f)] public float sizeMultiplier = 1f;
        [Min(0f)] public float lifetimeMultiplier = 1f;
        [Min(0f)] public float speedMultiplier = 1f;
        public Color colorMultiplier = Color.white;

        public void Clamp()
        {
            rateMultiplier = Mathf.Max(0f, rateMultiplier);
            sizeMultiplier = Mathf.Max(0f, sizeMultiplier);
            lifetimeMultiplier = Mathf.Max(0f, lifetimeMultiplier);
            speedMultiplier = Mathf.Max(0f, speedMultiplier);
        }
    }

    [Serializable]
    public sealed class ActionPreset
    {
        public string label;
        public Vector3 localOffset;
        public Vector3 localRotationEuler;
        [Min(0f)] public float speedMultiplier = 1f;
        public bool overrideShape;
        public ParticleSystemShapeType shapeType = ParticleSystemShapeType.Cone;
        [Min(0f)] public float shapeRadius = 0.15f;

        public void Clamp()
        {
            speedMultiplier = Mathf.Max(0f, speedMultiplier);
            shapeRadius = Mathf.Max(0f, shapeRadius);
        }

        public static ActionPreset CreateGlide()
        {
            return new ActionPreset
            {
                label = "Glide",
                localOffset = new Vector3(-0.35f, 0.55f, 0f),
                localRotationEuler = new Vector3(0f, 0f, 180f),
                speedMultiplier = 0.9f,
                overrideShape = false,
                shapeType = ParticleSystemShapeType.Cone,
                shapeRadius = 0.18f
            };
        }

        public static ActionPreset CreateRecoilJump()
        {
            return new ActionPreset
            {
                label = "Recoil Jump",
                localOffset = new Vector3(0f, -0.35f, 0f),
                localRotationEuler = Vector3.zero,
                speedMultiplier = 1.25f,
                overrideShape = false,
                shapeType = ParticleSystemShapeType.Cone,
                shapeRadius = 0.2f
            };
        }

        public static ActionPreset CreateDodge()
        {
            return new ActionPreset
            {
                label = "Dodge",
                localOffset = new Vector3(-0.45f, 0.15f, 0f),
                localRotationEuler = new Vector3(0f, 0f, 90f),
                speedMultiplier = 1.35f,
                overrideShape = false,
                shapeType = ParticleSystemShapeType.Cone,
                shapeRadius = 0.16f
            };
        }

        public static ActionPreset CreateDiveAttack()
        {
            return new ActionPreset
            {
                label = "Dive Attack",
                localOffset = new Vector3(0f, 0.55f, 0f),
                localRotationEuler = new Vector3(0f, 0f, 180f),
                speedMultiplier = 1.5f,
                overrideShape = false,
                shapeType = ParticleSystemShapeType.Cone,
                shapeRadius = 0.22f
            };
        }
    }
}
