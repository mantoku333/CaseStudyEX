using UnityEngine;

[DisallowMultipleComponent]
public sealed class ElegantPointGainAttractorVfx : MonoBehaviour
{
    private const float DefaultDestroyDelay = 0.35f;

    [SerializeField, Min(0.01f)] private float travelDuration = 1.15f;
    [SerializeField] private AnimationCurve travelCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Min(0f)] private float arcHeight = 1.8f;
    [SerializeField, Min(0f)] private float arrivalDistance = 0.12f;
    [SerializeField, Min(0f)] private float destroyDelay = 0.65f;

    public float TravelDuration => travelDuration;

    private Transform target;
    private Vector3 startPosition;
    private float elapsed;
    private bool initialized;
    private bool stopping;
    private ParticleSystem[] particleSystems;

    public void Initialize(Vector3 origin, Transform targetTransform, int amount)
    {
        transform.position = origin;
        startPosition = origin;
        target = targetTransform;
        elapsed = 0f;
        initialized = target != null;
        stopping = false;
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);

        float amountScale = Mathf.Clamp(amount / 20f, 1.6f, 2.6f);
        transform.localScale *= amountScale;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particle = particleSystems[i];
            if (particle == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = particle.main;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            particle.Clear(true);
            particle.Play(true);
        }
    }

    private void Update()
    {
        if (!initialized)
        {
            Destroy(gameObject, destroyDelay);
            return;
        }

        elapsed += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(elapsed / travelDuration);
        float easedTime = travelCurve != null ? travelCurve.Evaluate(normalizedTime) : normalizedTime;
        Vector3 endPosition = target.position;
        Vector3 position = Vector3.LerpUnclamped(startPosition, endPosition, easedTime);
        position.y += Mathf.Sin(normalizedTime * Mathf.PI) * arcHeight;
        transform.position = position;

        if (!stopping && (normalizedTime >= 1f || Vector3.Distance(transform.position, endPosition) <= arrivalDistance))
        {
            StopParticles();
            Destroy(gameObject, destroyDelay);
        }
    }

    private void StopParticles()
    {
        stopping = true;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particle = particleSystems[i];
            if (particle != null)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
