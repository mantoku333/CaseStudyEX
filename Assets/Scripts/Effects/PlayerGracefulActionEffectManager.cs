using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Projects the successful action chain into the sparkle layers and the gauge absorption.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-10000)]
public sealed class PlayerGracefulActionEffectManager : MonoBehaviour
{
    public enum GracefulAction { None, Glide, Dodge, DiveAttack, RecoilMove, OverheadBackKill, DiveBounce, RecoilJump, DodgeProjectile }

    [Header("Player")]
    [SerializeField] private PlayerController targetPlayer;
    [SerializeField] private bool autoFindPlayerOnStart = true;
    [SerializeField] private bool autoFindEffectRoots = true;
    [Header("Effects")]
    [SerializeField] private bool useLegacySparkles;
    [SerializeField] private EffectGroup level1Sparkle = new EffectGroup("Lv1 Sparkle");
    [SerializeField] private EffectGroup level2Sparkle = new EffectGroup("Lv2 Sparkle");
    [SerializeField] private EffectGroup burstEffect = new EffectGroup("Burst");
    [Header("Debug")]
    [SerializeField, Min(0)] private int currentLevel;
    [SerializeField] private GracefulAction lastAction;
    [SerializeField] private bool logLevelChanges;
    private PlayerElegantPointController pointController;
    private PlayerElegantPointController subscribedController;
    private PlayerMovementTrail movementTrail;

    public int CurrentLevel => currentLevel;
    public GracefulAction LastAction => lastAction;

    private void Awake() { ResolveReferences(); }
    private void OnEnable()
    {
        ResolveReferences();
        SetLevel(pointController != null ? pointController.CurrentChainLength : 0);
    }

    private void Update() { ResolveReferences(); }

    private void OnDisable()
    {
        if (subscribedController != null)
        {
            subscribedController.LevelChanged -= SetLevel;
            subscribedController.ActionSucceeded -= OnActionSucceeded;
            subscribedController.ActionRefined -= OnActionRefined;
        }
        subscribedController = null;
    }

    private void ResolveReferences()
    {
        if (targetPlayer == null && autoFindPlayerOnStart)
            targetPlayer = GetComponentInParent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();
        if (targetPlayer == null) return;
        pointController = targetPlayer.GetComponent<PlayerElegantPointController>();
        if (isActiveAndEnabled && subscribedController != pointController)
        {
            OnDisable();
            subscribedController = pointController;
            if (subscribedController != null)
            {
                subscribedController.LevelChanged += SetLevel;
                subscribedController.ActionSucceeded += OnActionSucceeded;
                subscribedController.ActionRefined += OnActionRefined;
                SetLevel(subscribedController.CurrentChainLength);
            }
        }
        if (movementTrail == null)
            movementTrail = targetPlayer.GetComponentInChildren<PlayerMovementTrail>(true);
        if (!useLegacySparkles || !autoFindEffectRoots) return;
        level1Sparkle.SetRootIfEmpty(FindChildByName(targetPlayer.transform, "FX_Sparkle (1)"));
        level2Sparkle.SetRootIfEmpty(FindChildByName(targetPlayer.transform, "FX_Sparkle"));
        burstEffect.SetRootIfEmpty(FindChildByName(targetPlayer.transform, "FX_Burst"));
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root.name == childName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), childName);
            if (found != null) return found;
        }
        return null;
    }

    public void SetTargetPlayer(PlayerController player)
    {
        targetPlayer = player;
        ResolveReferences();
    }

    private void SetLevel(int level)
    {
        int before = currentLevel;
        currentLevel = Mathf.Max(0, level);
        if (currentLevel == 0) lastAction = GracefulAction.None;
        if (Application.isPlaying && useLegacySparkles)
        {
            if (currentLevel >= 1) level1Sparkle.PlayLoop();
            else level1Sparkle.StopLoop();
            if (currentLevel >= 2) level2Sparkle.PlayLoop();
            else level2Sparkle.StopLoop();
        }
        if (logLevelChanges && before != currentLevel)
            Debug.Log($"[Graceful Effects] Level {before} -> {currentLevel}", this);
    }

    private void OnActionSucceeded(ElegantActionType action, int level)
    {
        OnActionRefined(action, level);
        if (Application.isPlaying && useLegacySparkles) burstEffect.PlayOnce();
    }

    private void OnActionRefined(ElegantActionType action, int level)
    {
        switch (action)
        {
            case ElegantActionType.Glide: lastAction = GracefulAction.Glide; break;
            case ElegantActionType.Dodge: lastAction = GracefulAction.Dodge; break;
            case ElegantActionType.DodgeProjectile: lastAction = GracefulAction.DodgeProjectile; break;
            case ElegantActionType.DiveAttack: lastAction = GracefulAction.DiveAttack; break;
            case ElegantActionType.DiveBounce: lastAction = GracefulAction.DiveBounce; break;
            case ElegantActionType.RecoilMove: lastAction = GracefulAction.RecoilMove; break;
            case ElegantActionType.RecoilJump: lastAction = GracefulAction.RecoilJump; break;
            case ElegantActionType.OverheadBackKill: lastAction = GracefulAction.OverheadBackKill; break;
        }
    }

    public bool TryAttractLiveParticles(Transform target, float duration)
    {
        ResolveReferences();
        // The graceful trail is the effect the player just watched; absorb that, not a stand-in.
        bool attracted = movementTrail != null && movementTrail.TryAbsorbIntoGauge(target, duration);
        if (!useLegacySparkles) return attracted;
        attracted |= level1Sparkle.TryAttractLiveParticles(target, duration);
        attracted |= level2Sparkle.TryAttractLiveParticles(target, duration);
        attracted |= burstEffect.TryAttractLiveParticles(target, duration);
        return attracted;
    }

    [ContextMenu("Settle Current Chain")]
    public void ResetEffects()
    {
        if (pointController != null) pointController.SettleChain();
        SetLevel(0);
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
