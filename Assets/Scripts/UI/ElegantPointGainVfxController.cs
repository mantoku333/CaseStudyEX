using UnityEngine;

[DisallowMultipleComponent]
public sealed class ElegantPointGainVfxController : MonoBehaviour
{
    [SerializeField] private ElegantPointHudView hudView;
    [SerializeField] private ElegantPointGainAttractorVfx attractorPrefab;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField] private Vector3 gaugeWorldOffset = new Vector3(0f, 0f, 0f);
    [SerializeField, Min(0.01f)] private float liveParticleAttractionDuration = 1.45f;
    [SerializeField, Range(0f, 1f)] private float gaugeArrivalFraction = 0.82f;

    private Transform attractionTarget;

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
    }

    private void HandleElegantPointGained(ElegantPointGainEvent gainEvent)
    {
        ResolveReferences();
        UpdateAttractionTargetPosition();

        if (attractionTarget == null)
        {
            return;
        }

        if (TryAttractExistingActionParticles(gainEvent.WorldPosition))
        {
            HoldGauge(liveParticleAttractionDuration);
            return;
        }

        Vector3 spawnPosition = gainEvent.WorldPosition + spawnOffset;
        ElegantPointGainAttractorVfx instance = CreateAttractor(spawnPosition);
        instance.Initialize(spawnPosition, attractionTarget, gainEvent.Amount);
        HoldGauge(instance.TravelDuration);
    }

    private bool TryAttractExistingActionParticles(Vector3 origin)
    {
        PlayerGracefulActionEffectManager[] managers = FindObjectsByType<PlayerGracefulActionEffectManager>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        PlayerGracefulActionEffectManager closestManager = null;
        float closestSqrDistance = float.PositiveInfinity;

        for (int i = 0; i < managers.Length; i++)
        {
            PlayerGracefulActionEffectManager manager = managers[i];
            if (manager == null)
            {
                continue;
            }

            float sqrDistance = (manager.transform.position - origin).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closestManager = manager;
            }
        }

        return closestManager != null &&
               closestManager.TryAttractLiveParticles(attractionTarget, liveParticleAttractionDuration);
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
        // The trail is what the player just watched; the fallback burst wears the same palette.
        main.startColor = new ParticleSystem.MinMaxGradient(GracefulPalette.CreateSpectrum())
            { mode = ParticleSystemGradientMode.RandomColor };

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
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(0.75f, 0.5f),
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

    // The gauge fills when the light actually reaches it, not when the chain settles. The
    // wallet is already correct by then; only the bar waits.
    private void HoldGauge(float travelSeconds)
    {
        if (hudView != null) hudView.HoldGainUntilAbsorbed(travelSeconds * gaugeArrivalFraction);
    }
}
