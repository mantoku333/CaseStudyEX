using UnityEngine;

/// <summary>
/// Carries the particles that are already on screen into the HUD gauge. The takeover is
/// gradual on purpose: each particle keeps drifting as it was, is drawn onto the flight path
/// only as the pull grows, and finishes its own colour fade exactly as it reaches the gauge.
/// </summary>
[DisallowMultipleComponent]
public sealed class ElegantPointLiveParticleAttractor : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float duration = 1.45f;
    [SerializeField] private AnimationCurve attractionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Min(0f)] private float maxStartDelay = 0.45f;
    [SerializeField, Min(0f)] private float curveOffset = 0.75f;
    [SerializeField, Min(0f)] private float swirlAmplitude = 0.22f;
    [SerializeField, Min(0f)] private float swirlFrequency = 2.2f;
    [SerializeField, Range(0f, 1f)] private float arrivalSize = 0.45f;
    [SerializeField, Range(0f, 1f)] private float arrivalOpacity = 0.35f;

    private ParticleSystem source;
    private ParticleSystem.Particle[] particles;
    private Vector3[] startWorldPositions;
    private Vector3[] freeWorldPositions;
    private Vector3[] writtenWorldPositions;
    private Vector3[] curveOffsets;
    private float[] startDelays;
    private float[] swirlPhases;
    private float[] startSizes;
    private float[] startAlphas;
    private Transform target;
    private float elapsed;
    private bool initialized;

    public bool Initialize(ParticleSystem sourceParticleSystem, Transform targetTransform, float attractDuration)
    {
        if (sourceParticleSystem == null || targetTransform == null)
        {
            return false;
        }

        source = sourceParticleSystem;
        target = targetTransform;
        duration = Mathf.Max(0.01f, attractDuration);

        int maxParticles = Mathf.Max(1, source.main.maxParticles);
        particles = new ParticleSystem.Particle[maxParticles];
        int particleCount = source.GetParticles(particles);
        if (particleCount <= 0)
        {
            return false;
        }

        startWorldPositions = new Vector3[particleCount];
        freeWorldPositions = new Vector3[particleCount];
        writtenWorldPositions = new Vector3[particleCount];
        curveOffsets = new Vector3[particleCount];
        startDelays = new float[particleCount];
        swirlPhases = new float[particleCount];
        startSizes = new float[particleCount];
        startAlphas = new float[particleCount];
        for (int i = 0; i < particleCount; i++)
        {
            Vector3 world = ParticleToWorldPosition(particles[i].position);
            startWorldPositions[i] = world;
            freeWorldPositions[i] = world;
            writtenWorldPositions[i] = world;
            startDelays[i] = Hash01(i, 17) * maxStartDelay;
            swirlPhases[i] = Hash01(i, 53) * Mathf.PI * 2f;
            curveOffsets[i] = CreateCurveOffset(i, world, target.position);
            startSizes[i] = particles[i].startSize;
            startAlphas[i] = particles[i].startColor.a;
            StretchLifetime(ref particles[i], duration + startDelays[i]);
        }

        source.SetParticles(particles, particleCount);
        ParticleSystem.EmissionModule emission = source.emission;
        emission.enabled = false;
        elapsed = 0f;
        initialized = true;
        return true;
    }

    /// <summary>
    /// Rescales the remaining life onto the flight without moving the particle's position in
    /// its own colour and size curves. Extending the raw lifetime instead would snap a fading
    /// particle back to full brightness; this way it simply completes its fade in transit.
    /// </summary>
    private static void StretchLifetime(ref ParticleSystem.Particle particle, float flightSeconds)
    {
        float age = particle.startLifetime > 0f
            ? Mathf.Clamp01(1f - (particle.remainingLifetime / particle.startLifetime))
            : 0f;
        particle.startLifetime = flightSeconds / Mathf.Max(0.05f, 1f - age);
        particle.remainingLifetime = flightSeconds;
    }

    private void Update()
    {
        Advance(Time.deltaTime);
    }

    /// <summary>Also the editor verification entry point, so the flight can be stepped deterministically.</summary>
    public void Advance(float deltaTime)
    {
        if (!initialized || source == null || target == null)
        {
            Finish();
            return;
        }

        elapsed += deltaTime;
        int particleCount = source.GetParticles(particles);
        // Only the particles captured at settlement travel. A chain that restarts mid-flight
        // keeps its fresh trail instead of having it dragged into the gauge as well.
        int attracted = Mathf.Min(particleCount, startWorldPositions.Length);
        Vector3 targetPosition = target.position;
        bool hasVisibleParticle = false;

        for (int i = 0; i < attracted; i++)
        {
            // Whatever drift, rise and noise the system applied this frame still counts: keep a
            // free-floating position alongside the flight path so nothing freezes on takeover.
            Vector3 simulated = ParticleToWorldPosition(particles[i].position);
            freeWorldPositions[i] += simulated - writtenWorldPositions[i];

            float particleTime = Mathf.Clamp01((elapsed - startDelays[i]) / duration);
            float pull = attractionCurve != null ? attractionCurve.Evaluate(particleTime) : Smooth01(particleTime);
            Vector3 control = startWorldPositions[i] + curveOffsets[i];
            Vector3 flight = QuadraticBezier(startWorldPositions[i], control, targetPosition, pull);
            flight += CreateSwirlDirection(startWorldPositions[i], targetPosition) *
                (Mathf.Sin((particleTime * Mathf.PI * 2f * swirlFrequency) + swirlPhases[i]) *
                    swirlAmplitude * Mathf.Sin(particleTime * Mathf.PI));

            Vector3 worldPosition = Vector3.Lerp(freeWorldPositions[i], flight, Smooth01(particleTime));
            writtenWorldPositions[i] = worldPosition;
            particles[i].position = WorldToParticlePosition(worldPosition);
            particles[i].startSize = startSizes[i] * Mathf.Lerp(1f, arrivalSize, pull);
            Color32 color = particles[i].startColor;
            color.a = (byte)Mathf.RoundToInt(startAlphas[i] * Mathf.Lerp(1f, arrivalOpacity, pull));
            particles[i].startColor = color;
            if (particleTime < 1f) hasVisibleParticle = true;
        }

        source.SetParticles(particles, particleCount);

        if (!hasVisibleParticle || elapsed >= duration + maxStartDelay)
        {
            Finish();
        }
    }

    private void Finish()
    {
        if (Application.isPlaying) Destroy(this);
        else DestroyImmediate(this);
    }

    private Vector3 ParticleToWorldPosition(Vector3 particlePosition)
    {
        return source.main.simulationSpace == ParticleSystemSimulationSpace.World
            ? particlePosition
            : source.transform.TransformPoint(particlePosition);
    }

    private Vector3 WorldToParticlePosition(Vector3 worldPosition)
    {
        return source.main.simulationSpace == ParticleSystemSimulationSpace.World
            ? worldPosition
            : source.transform.InverseTransformPoint(worldPosition);
    }

    private Vector3 CreateCurveOffset(int index, Vector3 startPosition, Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - startPosition;
        Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f).normalized;
        if (perpendicular.sqrMagnitude <= 0.0001f)
        {
            perpendicular = Vector3.up;
        }

        float side = Hash01(index, 91) < 0.5f ? -1f : 1f;
        float lift = Mathf.Lerp(0.25f, 1f, Hash01(index, 131));
        return (perpendicular * side * curveOffset * Mathf.Lerp(0.45f, 1.15f, Hash01(index, 71))) +
               (Vector3.up * curveOffset * lift);
    }

    private static Vector3 CreateSwirlDirection(Vector3 startPosition, Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - startPosition;
        Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f).normalized;
        return perpendicular.sqrMagnitude > 0.0001f ? perpendicular : Vector3.up;
    }

    private static Vector3 QuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float time)
    {
        float inverseTime = 1f - time;
        return (inverseTime * inverseTime * start) +
               (2f * inverseTime * time * control) +
               (time * time * end);
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - (2f * value));
    }

    private static float Hash01(int index, int salt)
    {
        unchecked
        {
            uint hash = (uint)(index + 1) * 747796405u + (uint)salt * 2891336453u;
            hash = ((hash >> ((int)(hash >> 28) + 4)) ^ hash) * 277803737u;
            hash = (hash >> 22) ^ hash;
            return (hash & 0x00FFFFFF) / 16777215f;
        }
    }
}
