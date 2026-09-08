using UnityEngine;

[DisallowMultipleComponent]
public sealed class ElegantPointLiveParticleAttractor : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float duration = 1.45f;
    [SerializeField] private AnimationCurve attractionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Min(0f)] private float arrivalDistance = 0.08f;
    [SerializeField, Min(0f)] private float maxStartDelay = 0.28f;
    [SerializeField, Min(0f)] private float inheritedVelocitySeconds = 0.28f;
    [SerializeField, Min(0f)] private float inheritedVelocityDamping = 0.58f;
    [SerializeField, Min(0f)] private float curveOffset = 0.75f;
    [SerializeField, Min(0f)] private float swirlAmplitude = 0.22f;
    [SerializeField, Min(0f)] private float swirlFrequency = 2.2f;

    private ParticleSystem particleSystem;
    private ParticleSystem.Particle[] particles;
    private Vector3[] startWorldPositions;
    private Vector3[] startWorldVelocities;
    private Vector3[] curveOffsets;
    private float[] startDelays;
    private float[] swirlPhases;
    private Transform target;
    private float elapsed;
    private bool initialized;

    public bool Initialize(ParticleSystem sourceParticleSystem, Transform targetTransform, float attractDuration)
    {
        if (sourceParticleSystem == null || targetTransform == null)
        {
            return false;
        }

        particleSystem = sourceParticleSystem;
        target = targetTransform;
        duration = Mathf.Max(0.01f, attractDuration);

        int maxParticles = Mathf.Max(1, particleSystem.main.maxParticles);
        particles = new ParticleSystem.Particle[maxParticles];
        int particleCount = particleSystem.GetParticles(particles);
        if (particleCount <= 0)
        {
            return false;
        }

        startWorldPositions = new Vector3[particleCount];
        startWorldVelocities = new Vector3[particleCount];
        curveOffsets = new Vector3[particleCount];
        startDelays = new float[particleCount];
        swirlPhases = new float[particleCount];
        for (int i = 0; i < particleCount; i++)
        {
            startWorldPositions[i] = ParticleToWorldPosition(particles[i].position);
            startWorldVelocities[i] = ParticleToWorldDirection(particles[i].velocity);
            startDelays[i] = Hash01(i, 17) * maxStartDelay;
            swirlPhases[i] = Hash01(i, 53) * Mathf.PI * 2f;
            curveOffsets[i] = CreateCurveOffset(i, startWorldPositions[i], target.position);

            float minimumLifetime = duration + startDelays[i] + 0.15f;
            if (particles[i].remainingLifetime < minimumLifetime)
            {
                particles[i].remainingLifetime = minimumLifetime;
            }
        }

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.enabled = false;
        elapsed = 0f;
        initialized = true;
        return true;
    }

    private void Update()
    {
        if (!initialized || particleSystem == null || target == null)
        {
            Destroy(this);
            return;
        }

        elapsed += Time.deltaTime;
        int particleCount = particleSystem.GetParticles(particles);
        Vector3 targetPosition = target.position;
        bool hasVisibleParticle = false;

        for (int i = 0; i < particleCount; i++)
        {
            Vector3 startPosition = i < startWorldPositions.Length
                ? startWorldPositions[i]
                : ParticleToWorldPosition(particles[i].position);
            Vector3 startVelocity = i < startWorldVelocities.Length
                ? startWorldVelocities[i]
                : Vector3.zero;
            float startDelay = i < startDelays.Length ? startDelays[i] : 0f;
            float particleElapsed = elapsed - startDelay;
            Vector3 inheritedPosition = startPosition + startVelocity *
                (Mathf.Min(elapsed, inheritedVelocitySeconds) * inheritedVelocityDamping);

            Vector3 worldPosition;
            if (particleElapsed <= 0f)
            {
                worldPosition = inheritedPosition;
            }
            else
            {
                float particleTime = Mathf.Clamp01(particleElapsed / duration);
                float particleEase = attractionCurve != null
                    ? attractionCurve.Evaluate(particleTime)
                    : Smooth01(particleTime);
                Vector3 controlPosition = inheritedPosition +
                    (i < curveOffsets.Length ? curveOffsets[i] : Vector3.zero);
                worldPosition = QuadraticBezier(inheritedPosition, controlPosition, targetPosition, particleEase);

                float swirlFade = Mathf.Sin(particleTime * Mathf.PI);
                float phase = i < swirlPhases.Length ? swirlPhases[i] : 0f;
                Vector3 swirlDirection = CreateSwirlDirection(startPosition, targetPosition);
                worldPosition += swirlDirection *
                    (Mathf.Sin((particleTime * Mathf.PI * 2f * swirlFrequency) + phase) * swirlAmplitude * swirlFade);
            }

            particles[i].position = WorldToParticlePosition(worldPosition);

            if (Vector3.Distance(worldPosition, targetPosition) <= arrivalDistance || elapsed >= duration + maxStartDelay)
            {
                particles[i].remainingLifetime = 0f;
            }
            else
            {
                hasVisibleParticle = true;
            }
        }

        particleSystem.SetParticles(particles, particleCount);

        if (!hasVisibleParticle || elapsed >= duration + maxStartDelay)
        {
            Destroy(this);
        }
    }

    private Vector3 ParticleToWorldPosition(Vector3 particlePosition)
    {
        ParticleSystemSimulationSpace simulationSpace = particleSystem.main.simulationSpace;
        return simulationSpace == ParticleSystemSimulationSpace.World
            ? particlePosition
            : particleSystem.transform.TransformPoint(particlePosition);
    }

    private Vector3 WorldToParticlePosition(Vector3 worldPosition)
    {
        ParticleSystemSimulationSpace simulationSpace = particleSystem.main.simulationSpace;
        return simulationSpace == ParticleSystemSimulationSpace.World
            ? worldPosition
            : particleSystem.transform.InverseTransformPoint(worldPosition);
    }

    private Vector3 ParticleToWorldDirection(Vector3 particleVelocity)
    {
        ParticleSystemSimulationSpace simulationSpace = particleSystem.main.simulationSpace;
        return simulationSpace == ParticleSystemSimulationSpace.World
            ? particleVelocity
            : particleSystem.transform.TransformDirection(particleVelocity);
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

    private Vector3 CreateSwirlDirection(Vector3 startPosition, Vector3 targetPosition)
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
