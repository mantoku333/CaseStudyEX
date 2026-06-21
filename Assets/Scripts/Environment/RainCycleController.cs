using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Environment/Rain Cycle Controller")]
public sealed class RainCycleController : MonoBehaviour
{
    public enum InitialRainMode
    {
        StartAfterInterval,
        StartImmediately
    }

    [SerializeField] private GameObject[] rainVisualRoots = new GameObject[0];
    [SerializeField] private Renderer[] rainRenderers = new Renderer[0];
    [SerializeField] private ParticleSystem[] rainParticleSystems = new ParticleSystem[0];
    [SerializeField] private RainDamageArea[] rainDamageAreas = new RainDamageArea[0];
    [SerializeField] private bool enableScheduledRain = true;
    [SerializeField] private bool limitRainToActiveArea;
    [SerializeField, Min(0.01f)] private float rainDurationSeconds = 30f;
    [SerializeField, Min(0.01f)] private float cycleIntervalSeconds = 180f;
    [SerializeField] private InitialRainMode initialRainMode = InitialRainMode.StartAfterInterval;
    [SerializeField, Min(0f)] private float rainDrainOutSeconds = 1.5f;

    [Header("Rain Start SE")]
    [SerializeField] private AudioSource rainSeSource;
    [SerializeField] private AudioClip rainStartThunderClip;
    [SerializeField, Range(0f, 1f)] private float rainStartThunderVolume = 1f;

    private float firstRainStartTime;
    private float hideRainRenderersAtTime = -1f;
    private bool isRaining;
    private bool initialized;
    private bool hasAppliedRainState;
    private float forcedRainUntil = -1f;
    private bool isRainAreaActive;
    private readonly List<Renderer> cachedRainRenderers = new List<Renderer>();
    private readonly List<ParticleSystem> cachedRainParticleSystems = new List<ParticleSystem>();

    public bool IsRaining => isRaining;

    public void SetRainAreaActive(bool active)
    {
        isRainAreaActive = active;
        RefreshRainState();
    }

    public void StartRainFor(float durationSeconds)
    {
        float duration = Mathf.Max(0.01f, durationSeconds);
        forcedRainUntil = Mathf.Max(forcedRainUntil, Time.time + duration);
        SetRainActive(true);
    }

    private void Awake()
    {
        RebuildVisualCaches();
        InitializeSchedule();
        RefreshRainState();
    }

    private void Update()
    {
        RefreshRainState();
        RefreshRainDrainOut();
    }

    private void InitializeSchedule()
    {
        float firstDelay = initialRainMode == InitialRainMode.StartImmediately
            ? 0f
            : Mathf.Max(0.01f, cycleIntervalSeconds);

        firstRainStartTime = Time.time + firstDelay;
        initialized = true;
    }

    private void RefreshRainState()
    {
        if (!initialized)
        {
            InitializeSchedule();
        }

        SetRainActive(ShouldRainAt(Time.time));
    }

    private bool ShouldRainAt(float time)
    {
        if (limitRainToActiveArea && !isRainAreaActive)
        {
            return false;
        }

        if (time < forcedRainUntil)
        {
            return true;
        }

        if (!enableScheduledRain)
        {
            return false;
        }

        if (time < firstRainStartTime)
        {
            return false;
        }

        float interval = Mathf.Max(0.01f, cycleIntervalSeconds);
        float duration = Mathf.Max(0.01f, rainDurationSeconds);
        float elapsed = time - firstRainStartTime;
        float phase = Mathf.Repeat(elapsed, interval);
        return phase < duration;
    }

    private void SetRainActive(bool active)
    {
        if (hasAppliedRainState && isRaining == active)
        {
            return;
        }

        bool wasRaining = isRaining;
        bool hadAppliedRainState = hasAppliedRainState;
        isRaining = active;
        hasAppliedRainState = true;
        SetDamageAreasActive(active);

        if (active)
        {
            hideRainRenderersAtTime = -1f;
            SetRainRenderersEnabled(true);
            PlayRainParticleSystems();
            PlayRainStartThunder();
            return;
        }

        bool shouldDrainOut = hadAppliedRainState && wasRaining && rainDrainOutSeconds > 0f;
        StopRainParticleSystems(shouldDrainOut
            ? ParticleSystemStopBehavior.StopEmitting
            : ParticleSystemStopBehavior.StopEmittingAndClear);

        if (shouldDrainOut)
        {
            SetRainRenderersEnabled(true);
            hideRainRenderersAtTime = Time.time + GetRainDrainOutSeconds();
            return;
        }

        hideRainRenderersAtTime = -1f;
        SetRainRenderersEnabled(false);
    }

    private void PlayRainStartThunder()
    {
        if (rainStartThunderClip == null)
        {
            return;
        }

        if (rainSeSource == null)
        {
            rainSeSource = GetComponent<AudioSource>();
        }

        if (rainSeSource == null)
        {
            rainSeSource = gameObject.AddComponent<AudioSource>();
            rainSeSource.playOnAwake = false;
            rainSeSource.loop = false;
            rainSeSource.spatialBlend = 0f;
        }

        rainSeSource.PlayOneShot(rainStartThunderClip, Mathf.Clamp01(rainStartThunderVolume));
    }

    private void RefreshRainDrainOut()
    {
        if (hideRainRenderersAtTime < 0f || Time.time < hideRainRenderersAtTime)
        {
            return;
        }

        hideRainRenderersAtTime = -1f;
        SetRainRenderersEnabled(false);
    }

    private float GetRainDrainOutSeconds()
    {
        float drainOutSeconds = Mathf.Max(0f, rainDrainOutSeconds);

        if (cachedRainParticleSystems.Count == 0)
        {
            RebuildVisualCaches();
        }

        for (int i = cachedRainParticleSystems.Count - 1; i >= 0; i--)
        {
            ParticleSystem particleSystem = cachedRainParticleSystems[i];
            if (particleSystem == null)
            {
                cachedRainParticleSystems.RemoveAt(i);
                continue;
            }

            ParticleSystem.MainModule main = particleSystem.main;
            drainOutSeconds = Mathf.Max(drainOutSeconds, main.startLifetime.constantMax);
        }

        return drainOutSeconds;
    }

    private void RebuildVisualCaches()
    {
        cachedRainRenderers.Clear();
        cachedRainParticleSystems.Clear();
        AddRainRenderers(rainRenderers);
        AddRainParticleSystems(rainParticleSystems);

        if (rainVisualRoots == null)
        {
            return;
        }

        for (int i = 0; i < rainVisualRoots.Length; i++)
        {
            GameObject root = rainVisualRoots[i];
            if (root == null)
            {
                continue;
            }

            AddRainRenderers(root.GetComponentsInChildren<Renderer>(true));
            AddRainParticleSystems(root.GetComponentsInChildren<ParticleSystem>(true));
        }
    }

    private void AddRainRenderers(Renderer[] renderers)
    {
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            AddRainRenderer(renderers[i]);
        }
    }

    private void AddRainRenderer(Renderer renderer)
    {
        if (renderer != null && !cachedRainRenderers.Contains(renderer))
        {
            cachedRainRenderers.Add(renderer);
        }
    }

    private void AddRainParticleSystems(ParticleSystem[] particleSystems)
    {
        if (particleSystems == null)
        {
            return;
        }

        for (int i = 0; i < particleSystems.Length; i++)
        {
            AddRainParticleSystem(particleSystems[i]);
        }
    }

    private void AddRainParticleSystem(ParticleSystem particleSystem)
    {
        if (particleSystem != null && !cachedRainParticleSystems.Contains(particleSystem))
        {
            cachedRainParticleSystems.Add(particleSystem);
        }
    }

    private void SetRainRenderersEnabled(bool active)
    {
        if (cachedRainRenderers.Count == 0)
        {
            RebuildVisualCaches();
        }

        for (int i = cachedRainRenderers.Count - 1; i >= 0; i--)
        {
            Renderer renderer = cachedRainRenderers[i];
            if (renderer == null)
            {
                cachedRainRenderers.RemoveAt(i);
                continue;
            }

            renderer.enabled = active;
        }
    }

    private void PlayRainParticleSystems()
    {
        if (cachedRainParticleSystems.Count == 0)
        {
            RebuildVisualCaches();
        }

        for (int i = cachedRainParticleSystems.Count - 1; i >= 0; i--)
        {
            ParticleSystem particleSystem = cachedRainParticleSystems[i];
            if (particleSystem == null)
            {
                cachedRainParticleSystems.RemoveAt(i);
                continue;
            }

            particleSystem.Play(false);
        }
    }

    private void StopRainParticleSystems(ParticleSystemStopBehavior stopBehavior)
    {
        if (cachedRainParticleSystems.Count == 0)
        {
            RebuildVisualCaches();
        }

        for (int i = cachedRainParticleSystems.Count - 1; i >= 0; i--)
        {
            ParticleSystem particleSystem = cachedRainParticleSystems[i];
            if (particleSystem == null)
            {
                cachedRainParticleSystems.RemoveAt(i);
                continue;
            }

            particleSystem.Stop(false, stopBehavior);
        }
    }

    private void SetDamageAreasActive(bool active)
    {
        if (rainDamageAreas == null)
        {
            return;
        }

        for (int i = 0; i < rainDamageAreas.Length; i++)
        {
            if (rainDamageAreas[i] != null)
            {
                rainDamageAreas[i].SetRainActive(active);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        rainDurationSeconds = Mathf.Max(0.01f, rainDurationSeconds);
        cycleIntervalSeconds = Mathf.Max(0.01f, cycleIntervalSeconds);
        rainDrainOutSeconds = Mathf.Max(0f, rainDrainOutSeconds);
        rainStartThunderVolume = Mathf.Clamp01(rainStartThunderVolume);
    }
#endif
}
