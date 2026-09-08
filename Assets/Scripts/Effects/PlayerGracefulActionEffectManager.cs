using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Playerの優雅アクションに合わせて、Lv1/Lv2のキラキラと昇格バーストを制御する。
/// エフェクト非表示時はEmissionのRate over Timeだけを0にし、その他のParticleSystem設定は変更しない。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-10000)]
public sealed class PlayerGracefulActionEffectManager : MonoBehaviour
{
    public enum GracefulAction
    {
        None,
        Glide,
        Dodge,
        DiveAttack,
        RecoilMove
    }

    private const int MinLevel = 0;
    private const int MaxLevel = 2;

    [Header("Player")]
    [SerializeField, Tooltip("未設定の場合、起動時に親またはシーン内からPlayerControllerを探します。")]
    private PlayerController targetPlayer;
    [SerializeField] private bool autoFindPlayerOnStart = true;
    [SerializeField, Tooltip("PlayerControllerの状態を見て、滑空/回避/落下攻撃/反動移動の開始を自動検知します。")]
    private bool detectActionsFromPlayerState = true;
    [SerializeField, Tooltip("Effect参照が空の場合、Player配下からFX_Sparkle/FX_Sparkle (1)/FX_Burstを探します。")]
    private bool autoFindEffectRoots = true;

    [Header("Effects")]
    [SerializeField, Tooltip("Lv1で表示するキラキラです。親TransformまたはParticleSystemを登録できます。")]
    private EffectGroup level1Sparkle = new EffectGroup("Lv1 Sparkle");
    [SerializeField, Tooltip("Lv2でLv1に追加して表示するキラキラです。親TransformまたはParticleSystemを登録できます。")]
    private EffectGroup level2Sparkle = new EffectGroup("Lv2 Sparkle");
    [SerializeField, Tooltip("Lv0→1、Lv1→2、Lv2中のコンボ成功時に1回再生するバーストです。")]
    private EffectGroup burstEffect = new EffectGroup("Burst");

    [Header("Timing")]
    [SerializeField, Min(0f), Tooltip("この秒数以内に次の優雅アクションを行うとLv2へ上がります。")]
    private float comboGraceSeconds = 1f;
    [SerializeField, Tooltip("有効にすると、直前と違う優雅アクションだけをコンボ成立として扱います。")]
    private bool requireDifferentActionForCombo = true;
    [SerializeField, Min(0f), Tooltip("最後の優雅アクションからこの秒数が経過するとLv0へ戻ります。")]
    private float resetAfterNoActionSeconds = 5f;
    [Header("Stop Reset")]
    [SerializeField, Tooltip("地上でPlayerが止まった時にLv0へ戻します。")]
    private bool resetWhenPlayerStops = true;
    [SerializeField, Min(0f), Tooltip("Rigidbody2Dの速度がこの値以下なら停止扱いにします。")]
    private float stoppedVelocityThreshold = 0.05f;
    [SerializeField, Min(0f), Tooltip("停止判定がこの秒数続いたらLv0へ戻します。")]
    private float stoppedResetDelay = 0.1f;

    [Header("Debug")]
    [SerializeField, Range(MinLevel, MaxLevel)] private int currentLevel;
    [SerializeField] private GracefulAction lastAction = GracefulAction.None;
    [SerializeField] private bool logLevelChanges;

    private Rigidbody2D targetRigidbody;
    private bool previousGliding;
    private bool previousDodging;
    private bool previousDiveAttacking;
    private bool previousRecoiling;
    private bool hasCapturedInitialActionState;
    private float lastGracefulActionTime = float.NegativeInfinity;
    private float stoppedTime;

    public int CurrentLevel => currentLevel;
    public GracefulAction LastAction => lastAction;

    public bool TryAttractLiveParticles(Transform target, float duration)
    {
        bool attractedAnyParticle = false;
        attractedAnyParticle |= level1Sparkle.TryAttractLiveParticles(target, duration);
        attractedAnyParticle |= level2Sparkle.TryAttractLiveParticles(target, duration);
        attractedAnyParticle |= burstEffect.TryAttractLiveParticles(target, duration);
        return attractedAnyParticle;
    }

    private void Awake()
    {
        ResolveReferences();
        StopBurstEffect();
        ApplyLevelEffects();
    }

    private void OnEnable()
    {
        ResolveReferences();
        StopBurstEffect();
        hasCapturedInitialActionState = false;
        ApplyLevelEffects();
    }

    private void Update()
    {
        ResolveReferences();

        if (detectActionsFromPlayerState && targetPlayer != null)
        {
            if (!hasCapturedInitialActionState)
            {
                CapturePlayerActionState();
                hasCapturedInitialActionState = true;
                return;
            }

            UpdateFromPlayerState();
        }

        UpdateResetTimers();
    }

    private void OnValidate()
    {
        currentLevel = Mathf.Clamp(currentLevel, MinLevel, MaxLevel);
        comboGraceSeconds = Mathf.Max(0f, comboGraceSeconds);
        resetAfterNoActionSeconds = Mathf.Max(0f, resetAfterNoActionSeconds);
        stoppedVelocityThreshold = Mathf.Max(0f, stoppedVelocityThreshold);
        stoppedResetDelay = Mathf.Max(0f, stoppedResetDelay);

        level1Sparkle?.Validate();
        level2Sparkle?.Validate();
        burstEffect?.Validate();
    }

    public void SetTargetPlayer(PlayerController player)
    {
        targetPlayer = player;
        targetRigidbody = player != null ? player.GetComponent<Rigidbody2D>() : null;
        CapturePlayerActionState();
        hasCapturedInitialActionState = player != null;
    }

    /// <summary>
    /// 各アクション側から呼ぶための入口。自動検知を使わない場合も、この関数で同じレベル制御ができます。
    /// </summary>
    public void NotifyGracefulAction(GracefulAction action)
    {
        if (action == GracefulAction.None)
        {
            ResetEffects();
            return;
        }

        int previousLevel = currentLevel;
        bool isDifferentAction = !requireDifferentActionForCombo || action != lastAction;
        bool insideComboWindow = Time.time <= lastGracefulActionTime + comboGraceSeconds;
        bool comboSucceeded = insideComboWindow && isDifferentAction;

        lastGracefulActionTime = Time.time;
        lastAction = action;
        stoppedTime = 0f;

        if (currentLevel <= MinLevel)
        {
            currentLevel = 1;
            PlayBurst();
        }
        else if (currentLevel == 1)
        {
            currentLevel = comboSucceeded ? 2 : 1;
            if (currentLevel != previousLevel)
            {
                PlayBurst();
            }
        }
        else if (comboSucceeded)
        {
            PlayBurst();
        }

        ApplyLevelEffects();
        LogLevelChange(previousLevel, currentLevel, action);
    }

    [ContextMenu("Reset Effects")]
    public void ResetEffects()
    {
        int previousLevel = currentLevel;
        currentLevel = MinLevel;
        lastAction = GracefulAction.None;
        lastGracefulActionTime = float.NegativeInfinity;
        stoppedTime = 0f;
        ApplyLevelEffects();
        LogLevelChange(previousLevel, currentLevel, GracefulAction.None);
    }

    [ContextMenu("Preview Lv1")]
    public void PreviewLevel1()
    {
        currentLevel = 1;
        lastAction = GracefulAction.Glide;
        lastGracefulActionTime = Time.time;
        ApplyLevelEffects();
        PlayBurst();
    }

    [ContextMenu("Preview Lv2")]
    public void PreviewLevel2()
    {
        currentLevel = 2;
        lastAction = GracefulAction.Dodge;
        lastGracefulActionTime = Time.time;
        ApplyLevelEffects();
        PlayBurst();
    }

    private void ResolveReferences()
    {
        if (targetPlayer == null && autoFindPlayerOnStart)
        {
            targetPlayer = GetComponentInParent<PlayerController>();
            if (targetPlayer == null)
            {
                targetPlayer = FindFirstObjectByType<PlayerController>();
            }

            if (targetPlayer != null)
            {
                hasCapturedInitialActionState = false;
            }
        }

        if (targetRigidbody == null && targetPlayer != null)
        {
            targetRigidbody = targetPlayer.GetComponent<Rigidbody2D>();
        }

        if (autoFindEffectRoots && targetPlayer != null)
        {
            AutoAssignEffectRoots();
        }
    }

    private void AutoAssignEffectRoots()
    {
        Transform searchRoot = targetPlayer.transform;
        level1Sparkle.SetRootIfEmpty(FindChildByName(searchRoot, "FX_Sparkle (1)") ?? FindChildByName(searchRoot, "FX_Lv1") ?? FindChildByName(searchRoot, "FX_Level1"));
        level2Sparkle.SetRootIfEmpty(FindChildByName(searchRoot, "FX_Sparkle") ?? FindChildByName(searchRoot, "FX_Lv2") ?? FindChildByName(searchRoot, "FX_Level2"));
        burstEffect.SetRootIfEmpty(FindChildByName(searchRoot, "FX_Burst"));
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

    private void UpdateFromPlayerState()
    {
        bool gliding = targetPlayer.IsGliding;
        bool dodging = targetPlayer.IsDodging;
        bool diveAttacking = targetPlayer.IsDiveAttacking;
        bool recoiling = targetPlayer.IsRecoilBoosting;

        if (gliding && !previousGliding)
        {
            NotifyGracefulAction(GracefulAction.Glide);
        }

        if (dodging && !previousDodging)
        {
            NotifyGracefulAction(GracefulAction.Dodge);
        }

        if (diveAttacking && !previousDiveAttacking)
        {
            NotifyGracefulAction(GracefulAction.DiveAttack);
        }

        if (recoiling && !previousRecoiling)
        {
            NotifyGracefulAction(GracefulAction.RecoilMove);
        }

        if (currentLevel > MinLevel && (gliding || dodging || diveAttacking || recoiling))
        {
            lastGracefulActionTime = Time.time;
        }

        previousGliding = gliding;
        previousDodging = dodging;
        previousDiveAttacking = diveAttacking;
        previousRecoiling = recoiling;
    }

    private void CapturePlayerActionState()
    {
        if (targetPlayer == null)
        {
            previousGliding = false;
            previousDodging = false;
            previousDiveAttacking = false;
            previousRecoiling = false;
            return;
        }

        previousGliding = targetPlayer.IsGliding;
        previousDodging = targetPlayer.IsDodging;
        previousDiveAttacking = targetPlayer.IsDiveAttacking;
        previousRecoiling = targetPlayer.IsRecoilBoosting;
    }

    private void UpdateResetTimers()
    {
        if (currentLevel <= MinLevel)
        {
            return;
        }

        if (resetAfterNoActionSeconds > 0f && Time.time > lastGracefulActionTime + resetAfterNoActionSeconds)
        {
            ResetEffects();
            return;
        }

        if (!resetWhenPlayerStops || !IsPlayerStopped())
        {
            stoppedTime = 0f;
            return;
        }

        stoppedTime += Time.deltaTime;
        if (stoppedTime >= stoppedResetDelay)
        {
            ResetEffects();
        }
    }

    private bool IsPlayerStopped()
    {
        if (targetPlayer == null)
        {
            return false;
        }

        if (!targetPlayer.IsGrounded || targetPlayer.IsGliding || targetPlayer.IsDodging ||
            targetPlayer.IsDiveAttacking || targetPlayer.IsRecoilBoosting)
        {
            return false;
        }

        if (targetPlayer.IsMoving)
        {
            return false;
        }

        return targetRigidbody == null || targetRigidbody.linearVelocity.sqrMagnitude <= stoppedVelocityThreshold * stoppedVelocityThreshold;
    }

    private void ApplyLevelEffects()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (currentLevel >= 1)
        {
            level1Sparkle.PlayLoop();
        }
        else
        {
            level1Sparkle.StopLoop();
        }

        if (currentLevel >= 2)
        {
            level2Sparkle.PlayLoop();
        }
        else
        {
            level2Sparkle.StopLoop();
        }
    }

    private void PlayBurst()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        burstEffect.PlayOnce();
    }

    private void StopBurstEffect()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        burstEffect.StopAndClear();
    }

    private void LogLevelChange(int previousLevel, int nextLevel, GracefulAction action)
    {
        if (!logLevelChanges || previousLevel == nextLevel)
        {
            return;
        }

        Debug.Log(
            $"[PlayerGracefulActionEffectManager] Level {previousLevel} -> {nextLevel} / Action: {action}",
            this);
    }

    [Serializable]
    private sealed class EffectGroup
    {
        [SerializeField] private string label;
        [SerializeField, Tooltip("このTransform以下のParticleSystemをまとめて制御します。")]
        private Transform root;
        [SerializeField, Tooltip("rootを使わず、個別にParticleSystemを指定したい場合はこちらに入れます。")]
        private ParticleSystem[] particles = Array.Empty<ParticleSystem>();
        [SerializeField, Tooltip("有効時に再生開始時Clearします。バースト向けです。")]
        private bool clearBeforePlay = true;
        private readonly Dictionary<ParticleSystem, float> originalRateOverTimeMultipliers = new Dictionary<ParticleSystem, float>();

        public EffectGroup(string label)
        {
            this.label = label;
        }

        public void Validate()
        {
        }

        public void SetRootIfEmpty(Transform candidate)
        {
            if (root == null && (particles == null || particles.Length == 0) && candidate != null)
            {
                root = candidate;
            }
        }

        public void PlayLoop()
        {
            ParticleSystem[] resolvedParticles = ResolveParticles();
            for (int i = 0; i < resolvedParticles.Length; i++)
            {
                ParticleSystem particle = resolvedParticles[i];
                if (particle == null)
                {
                    continue;
                }

                RestoreRateOverTime(particle);
                ParticleSystem.EmissionModule emission = particle.emission;
                emission.enabled = true;
                if (!particle.isPlaying)
                {
                    particle.Play(true);
                }
            }
        }

        public void StopLoop()
        {
            ParticleSystem[] resolvedParticles = ResolveParticles();
            for (int i = 0; i < resolvedParticles.Length; i++)
            {
                ParticleSystem particle = resolvedParticles[i];
                if (particle != null)
                {
                    SetRateOverTime(particle, 0f);
                }
            }
        }

        public void StopAndClear()
        {
            ParticleSystem[] resolvedParticles = ResolveParticles();
            for (int i = 0; i < resolvedParticles.Length; i++)
            {
                ParticleSystem particle = resolvedParticles[i];
                if (particle != null)
                {
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particle.Clear(true);
                }
            }
        }

        public void PlayOnce()
        {
            ParticleSystem[] resolvedParticles = ResolveParticles();
            for (int i = 0; i < resolvedParticles.Length; i++)
            {
                ParticleSystem particle = resolvedParticles[i];
                if (particle == null)
                {
                    continue;
                }

                RestoreRateOverTime(particle);
                ParticleSystem.EmissionModule emission = particle.emission;
                emission.enabled = true;
                if (clearBeforePlay)
                {
                    particle.Clear(true);
                }

                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(true);
            }
        }

        public bool TryAttractLiveParticles(Transform target, float duration)
        {
            if (target == null)
            {
                return false;
            }

            bool attractedAnyParticle = false;
            ParticleSystem[] resolvedParticles = ResolveParticles();
            for (int i = 0; i < resolvedParticles.Length; i++)
            {
                ParticleSystem particle = resolvedParticles[i];
                if (particle == null || particle.particleCount <= 0)
                {
                    continue;
                }

                ElegantPointLiveParticleAttractor attractor =
                    particle.GetComponent<ElegantPointLiveParticleAttractor>();
                if (attractor == null)
                {
                    attractor = particle.gameObject.AddComponent<ElegantPointLiveParticleAttractor>();
                }

                attractedAnyParticle |= attractor.Initialize(particle, target, duration);
            }

            return attractedAnyParticle;
        }

        private ParticleSystem[] ResolveParticles()
        {
            if (particles != null && particles.Length > 0)
            {
                return particles;
            }

            return root != null
                ? root.GetComponentsInChildren<ParticleSystem>(true)
                : Array.Empty<ParticleSystem>();
        }

        private void RestoreRateOverTime(ParticleSystem particle)
        {
            SetRateOverTime(particle, GetOriginalRateOverTime(particle));
        }

        private void SetRateOverTime(ParticleSystem particle, float rateOverTimeMultiplier)
        {
            GetOriginalRateOverTime(particle);
            ParticleSystem.EmissionModule emission = particle.emission;
            emission.rateOverTimeMultiplier = rateOverTimeMultiplier;
        }

        private float GetOriginalRateOverTime(ParticleSystem particle)
        {
            if (!originalRateOverTimeMultipliers.TryGetValue(particle, out float originalRate))
            {
                originalRate = particle.emission.rateOverTimeMultiplier;
                originalRateOverTimeMultipliers.Add(particle, originalRate);
            }

            return originalRate;
        }
    }
}
