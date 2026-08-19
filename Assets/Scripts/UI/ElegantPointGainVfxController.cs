using UnityEngine;

[DisallowMultipleComponent]
public sealed class ElegantPointGainVfxController : MonoBehaviour
{
    [SerializeField] private ElegantPointHudView hudView;
    [SerializeField] private ElegantPointGainAttractorVfx attractorPrefab;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField] private Vector3 gaugeWorldOffset = new Vector3(0f, 0f, 0f);
    [SerializeField, Min(0f)] private float gaugePulseScale = 1.08f;
    [SerializeField, Min(0.01f)] private float gaugePulseDuration = 0.16f;

    private Transform attractionTarget;
    private RectTransform pulsingGauge;
    private Vector3 pulsingGaugeBaseScale = Vector3.one;
    private float pulseTimer;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ElegantPointGainEvents.Gained += HandleElegantPointGained;
    }

    private void OnDisable()
    {
        ElegantPointGainEvents.Gained -= HandleElegantPointGained;
        if (pulsingGauge != null)
        {
            pulsingGauge.localScale = pulsingGaugeBaseScale;
        }
    }

    private void OnDestroy()
    {
        if (attractionTarget != null)
        {
            Destroy(attractionTarget.gameObject);
        }
    }

    private void Update()
    {
        UpdateAttractionTargetPosition();
        UpdateGaugePulse();
    }

    private void HandleElegantPointGained(ElegantPointGainEvent gainEvent)
    {
        ResolveReferences();
        UpdateAttractionTargetPosition();

        if (attractorPrefab == null || attractionTarget == null)
        {
            return;
        }

        Vector3 spawnPosition = gainEvent.WorldPosition + spawnOffset;
        ElegantPointGainAttractorVfx instance = CreateAttractor(spawnPosition);
        instance.Initialize(spawnPosition, attractionTarget, gainEvent.Amount);
        pulseTimer = gaugePulseDuration;
    }

    private ElegantPointGainAttractorVfx CreateAttractor(Vector3 spawnPosition)
    {
        if (attractorPrefab != null)
        {
            return Instantiate(attractorPrefab, spawnPosition, Quaternion.identity);
        }

        GameObject attractorObject = new GameObject("FX_ElegantPoint_GainAttract (Runtime)");
        attractorObject.transform.position = spawnPosition;

        ParticleSystem particle = attractorObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particle.main;
        main.duration = 0.7f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.9f, 1f, 1f),
            new Color(0.9f, 0.25f, 1f, 1f));

        ParticleSystem.EmissionModule emission = particle.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 36)
        });

        ParticleSystem.ShapeModule shape = particle.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.65f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particle.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.95f, 1f), 0f),
                new GradientColorKey(new Color(0.8f, 0.2f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = attractorObject.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 80;
        }

        return attractorObject.AddComponent<ElegantPointGainAttractorVfx>();
    }

    private void ResolveReferences()
    {
        if (hudView == null)
        {
            hudView = GetComponent<ElegantPointHudView>();
            if (hudView == null)
            {
                hudView = GetComponentInParent<ElegantPointHudView>();
            }
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (attractionTarget == null)
        {
            GameObject targetObject = new GameObject("Elegant Point Attraction Target");
            targetObject.hideFlags = HideFlags.HideInHierarchy;
            attractionTarget = targetObject.transform;
        }
    }

    private void UpdateAttractionTargetPosition()
    {
        if (hudView == null || attractionTarget == null)
        {
            return;
        }

        RectTransform targetRect = hudView.AbsorbTargetRect;
        if (targetRect == null)
        {
            return;
        }

        if (pulsingGauge != targetRect)
        {
            if (pulsingGauge != null)
            {
                pulsingGauge.localScale = pulsingGaugeBaseScale;
            }

            pulsingGauge = targetRect;
            pulsingGaugeBaseScale = pulsingGauge.localScale;
        }

        Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        if (cameraToUse == null)
        {
            return;
        }

        Vector3 screenPosition = RectTransformUtility.WorldToScreenPoint(null, targetRect.position);
        float depth = Mathf.Abs(cameraToUse.transform.position.z);
        Vector3 worldPosition = cameraToUse.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
        attractionTarget.position = worldPosition + gaugeWorldOffset;
    }

    private void UpdateGaugePulse()
    {
        if (pulseTimer <= 0f || pulsingGauge == null)
        {
            return;
        }

        pulseTimer = Mathf.Max(0f, pulseTimer - Time.deltaTime);
        float normalizedTime = 1f - (pulseTimer / gaugePulseDuration);
        float pulse = Mathf.Sin(normalizedTime * Mathf.PI) * Mathf.Max(0f, gaugePulseScale - 1f);
        pulsingGauge.localScale = pulsingGaugeBaseScale * (1f + pulse);
    }
}
