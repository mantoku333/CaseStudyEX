using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class PlayerMovementTrail : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private Transform movementTarget;
    [SerializeField, Min(0f)] private float minimumSpeed = 0.08f;
    [SerializeField, Min(0.1f)] private float teleportDistance = 4f;
    [SerializeField, Min(0f)] private float stopDelay = 0.06f;
    [SerializeField, HideInInspector] private int minimumGracefulLevel;
    [SerializeField, HideInInspector] private PlayerGracefulActionEffectManager gracefulEffects;
    [SerializeField, HideInInspector] private bool onlyDuringGracefulActions;

    [Header("Short Ribbon Bursts")]
    [SerializeField, Min(0.1f)] private float burstInterval = 1.05f;
    [SerializeField, Min(0.05f)] private float burstDuration = 0.46f;

    [Header("Particles")]
    [SerializeField] private ParticleSystem[] particles;
    private Vector3 previousPosition;
    private GracefulParticleRibbon[] ribbonDrivers;
    private bool emitting;
    private float stationaryTime;
    private PlayerController player;
    private float burstClock;
    private float nextBurst;
    private int burstIndex;
    private bool doubleBurst;
    private float burstCycle;
    [Header("Successful Action Levels")]
    [SerializeField] private PlayerElegantPointController pointController;
    [SerializeField, Min(0.05f)] private float levelBlendSeconds = 0.45f;
    private float visualLevel = 1f;
    private int previewLevel = -1;
    private int previousLevel;
    private float[] baseRates, baseDistances, baseRadii;
    private ParticleSystem.MinMaxCurve[] baseSizes;
    private Gradient[] levelColors;
    private Gradient[] blendedColors;
    private GradientColorKey[][] paletteKeys;
    private GradientAlphaKey[][] paletteAlpha;
    private float appliedLevel = float.NaN;
    private PlayerElegantPointController subscribedPoints;
    private int pendingSuccesses;
    private bool initialized;
    public int CurrentLevel => previewLevel >= 0 ? previewLevel : pointController != null ? Mathf.Clamp(pointController.CurrentChainLength, 0, 4) : 0;
    public float VisualLevel => visualLevel;

    // Explicit preview entry point; gameplay always reads the successful-action controller.
    public void SetPreviewLevel(int level) { previewLevel = Mathf.Clamp(level, 0, 4); }
    public void ClearPreviewLevel() { previewLevel = -1; }

    public bool IsEmitting => emitting;

    private void Awake()
    {
        if (initialized) return;
        player = GetComponentInParent<PlayerController>();
        if (movementTarget == null)
        {
            movementTarget = player != null ? player.transform : transform;
        }

        if (gracefulEffects == null)
            gracefulEffects = movementTarget.GetComponent<PlayerGracefulActionEffectManager>();

        if (particles == null || particles.Length == 0)
            particles = GetComponentsInChildren<ParticleSystem>(true);

        ribbonDrivers = GetComponentsInChildren<GracefulParticleRibbon>(true);
        if (pointController == null) pointController = movementTarget.GetComponent<PlayerElegantPointController>();
        baseRates = new float[particles.Length];
        baseDistances = new float[particles.Length];
        baseRadii = new float[particles.Length];
        baseSizes = new ParticleSystem.MinMaxCurve[particles.Length];
        levelColors = new Gradient[particles.Length];
        blendedColors = new Gradient[particles.Length];
        paletteKeys = new GradientColorKey[particles.Length][];
        paletteAlpha = new GradientAlphaKey[particles.Length][];
        appliedLevel = float.NaN;
        for (int i = 0; i < particles.Length; i++)
        {
            baseRates[i] = particles[i].emission.rateOverTime.constant;
            baseDistances[i] = particles[i].emission.rateOverDistance.constant;
            baseRadii[i] = particles[i].shape.radius;
            baseSizes[i] = particles[i].main.startSize;
            Gradient authored = particles[i].main.startColor.gradient;
            // Unity's getter can expose a native gradient owned by the particle module.
            // Snapshot its values; never keep that mutable native object as the baseline.
            if (authored != null)
            {
                levelColors[i] = new Gradient();
                levelColors[i].SetKeys(authored.colorKeys, authored.alphaKeys);
                levelColors[i].mode = authored.mode;
            }
            if (levelColors[i] != null)
            {
                blendedColors[i] = new Gradient();
                paletteKeys[i] = levelColors[i].colorKeys;
                paletteAlpha[i] = levelColors[i].alphaKeys;
            }
        }
        initialized = true;
    }

    private void OnEnable()
    {
        Awake();
        BindPointController();
        pendingSuccesses = 0;
        previousPosition = movementTarget.position;
        stationaryTime = stopDelay;
        emitting = false;
        burstClock = -1f;
        nextBurst = 0f;
        burstCycle = burstInterval;
        burstIndex = 0;
        visualLevel = Mathf.Max(1, CurrentLevel);
        previousLevel = CurrentLevel;
        foreach (ParticleSystem system in particles)
        {
            if (system == null) continue;
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var emission = system.emission;
            emission.enabled = false;
            system.Play(false);
        }
        foreach (GracefulParticleRibbon driver in ribbonDrivers) driver.Initialize();
    }

    private void LateUpdate()
    {
        Tick(Time.deltaTime, true);
    }

    // Also used by the editor preview to exercise the real movement/stop behavior.
    public void Tick(float deltaTime, bool gracefulAction)
    {
        BindPointController();
        if (movementTarget == null || deltaTime <= 0f) return;
        int level = CurrentLevel;
        if (level > 0 && previousLevel == 0) visualLevel = 1f;
        if (level > 0)
            visualLevel = Mathf.MoveTowards(visualLevel, level, deltaTime / levelBlendSeconds);
        previousLevel = level;
        ApplyLevel();
        EmitSuccessFeedback();

        Vector3 position = movementTarget.position;
        float distance = Vector2.Distance(position, previousPosition);
        previousPosition = position;

        if (distance > teleportDistance)
        {
            nextBurst = 0f;
            burstClock = -1f;
            stationaryTime = stopDelay;
            SetEmission(false);
            foreach (ParticleSystem system in particles)
                if (system != null) system.Clear(false);
            foreach (GracefulParticleRibbon driver in ribbonDrivers) driver.Clear();
            return;
        }

        bool levelAllowed = CurrentLevel > 0;
        // Non-interpolated physics can leave several render frames at the same position.
        stationaryTime = distance > minimumSpeed * deltaTime ? 0f : stationaryTime + deltaTime;
        bool moving = (distance > minimumSpeed * deltaTime || stationaryTime < stopDelay) && levelAllowed && gracefulAction;
        SetEmission(moving);
        if (moving)
        {
            nextBurst -= deltaTime;
            if (nextBurst <= 0f)
            {
                burstClock = 0f;
                doubleBurst = visualLevel >= 1.8f || burstIndex % 2 == 1;
                nextBurst = burstInterval * Mathf.Lerp(1f, 0.78f, (visualLevel - 1f) / 3f) + Mathf.Sin(burstIndex * 2.4f) * 0.05f;
                burstCycle = nextBurst;
                burstIndex++;
                EmitFlourish();
            }
            else burstClock += deltaTime;
        }
        else
        {
            nextBurst = 0f;
            burstClock = -1f;
        }
        float ribbonDuration = burstDuration * Mathf.Lerp(1f, 1.28f, (visualLevel - 1f) / 3f);
        float spread = BurstSpread;
        foreach (GracefulParticleRibbon driver in ribbonDrivers)
        {
            driver.SetVisualLevel(visualLevel);
            driver.Advance(deltaTime, moving &&
                driver.IsBurstActive(burstClock, ribbonDuration, burstCycle, visualLevel, doubleBurst, spread));
        }
    }

    /// <summary>Levels three and four answer a flourish with a burst the player cannot miss.</summary>
    private void EmitFlourish()
    {
        float flourish = Flourish;
        if (flourish <= 0f) return;
        // A glint on the beat, not a handful. The ribbon is what grows with the level.
        Emit(Mathf.RoundToInt(Mathf.Lerp(0f, 4f, flourish)), 0, Mathf.RoundToInt(Mathf.Lerp(0f, 1f, flourish)), 0);
    }

    // Zero at level two and below; the understated levels keep their original response.
    private float Flourish => Mathf.InverseLerp(2f, 4f, visualLevel);

    // Where level two sits on the one-to-four blend.
    private const float LevelTwoBlend = 1f / 3f;

    // Levels one and two keep their paired bursts; above that the lines take turns around the
    // cycle so their short strokes overlap instead of appearing and vanishing together.
    private float BurstSpread => Mathf.InverseLerp(2f, 3f, visualLevel);

    private void ApplyLevel()
    {
        if (Mathf.Approximately(appliedLevel, visualLevel)) return;
        appliedLevel = visualLevel;
        float t = (visualLevel - 1f) / 3f;
        float flourish = Flourish;
        // Sprite count, size and spread are a level-one-to-two idea and stop there. Above level
        // two the ribbon carries the level; piling on more and bigger sprites reads as noise.
        // Level one is the untouched baseline: every factor below is exactly 1 when t is 0.
        float sprite = Mathf.Min(t, LevelTwoBlend);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem system = particles[i];
            bool petal = system.name == "Petals";
            bool glow = system.name == "SoftGlow";
            bool dust = system.name == "FineSparkles";
            float density = Mathf.Lerp(1f, petal ? 1.85f : glow ? 1.6f : 2.15f, sprite) *
                (dust ? Mathf.Lerp(1f, 1.2f, flourish) : 1f);
            var emission = system.emission;
            emission.rateOverTime = baseRates[i] * density;
            emission.rateOverDistance = baseDistances[i] * density;
            var main = system.main;
            var size = baseSizes[i];
            // The soft halo has no silhouette, so it is the one layer that may keep swelling.
            size.constantMin *= petal ? 1f : Mathf.Lerp(1f, glow ? 1.25f : 1.08f, glow ? t : sprite);
            size.constantMax *= petal ? 1f : Mathf.Lerp(1f, glow ? 1.4f : 1.15f, glow ? t : sprite) *
                (glow ? Mathf.Lerp(1f, 1.25f, flourish) : 1f);
            main.startSize = size;
            var shape = system.shape;
            shape.radius = baseRadii[i] * Mathf.Lerp(1f, 1.5f, sprite);
            // Change birth colors only: existing petals never recolor when the level changes.
            Gradient palette = levelColors[i];
            if (palette != null)
            {
                for (int key = 0; key < paletteKeys[i].Length; key++)
                {
                    float time = paletteKeys[i][key].time;
                    paletteKeys[i][key] = new GradientColorKey(
                        PastelColor(palette.Evaluate(time), t), time);
                }
                blendedColors[i].SetKeys(paletteKeys[i], paletteAlpha[i]);
                main.startColor = new ParticleSystem.MinMaxGradient(blendedColors[i])
                    { mode = ParticleSystemGradientMode.RandomColor };
            }
        }
    }

    private static Color PastelColor(Color baseline, float t)
    {
        Color accent = new Color(
            Mathf.Clamp01(1f - (1f - baseline.r) * 2.6f),
            Mathf.Clamp01(1f - (1f - baseline.g) * 2.6f),
            Mathf.Clamp01(1f - (1f - baseline.b) * 2.6f), baseline.a);
        return Color.Lerp(baseline, accent, Mathf.Clamp01(t));
    }

    private void BindPointController()
    {
        if (pointController == null && movementTarget != null)
            pointController = movementTarget.GetComponent<PlayerElegantPointController>();
        if (subscribedPoints == pointController) return;
        if (subscribedPoints != null) subscribedPoints.ActionSucceeded -= OnActionSucceeded;
        subscribedPoints = pointController;
        if (subscribedPoints != null) subscribedPoints.ActionSucceeded += OnActionSucceeded;
    }

    private void OnActionSucceeded(ElegantActionType action, int level)
    {
        // Settlement and the next success can occur before the next LateUpdate.
        // Do not require polling to have observed the intermediate level zero.
        if (level == 1) previousLevel = 0;
        pendingSuccesses = Mathf.Min(pendingSuccesses + 1, 4);
        nextBurst = 0f;
    }

    private void EmitSuccessFeedback()
    {
        if (pendingSuccesses == 0) return;
        // A successful hit or dodge must read even when movement pauses for hit-stop.
        float reward = pendingSuccesses * Mathf.Lerp(1f, 1.4f, Flourish);
        Emit(Mathf.RoundToInt(5f * reward), Mathf.RoundToInt(2f * reward),
            Mathf.RoundToInt(reward), Mathf.RoundToInt(reward));
        pendingSuccesses = 0;
    }

    /// <summary>
    /// Draws the live trail into the HUD gauge so a point gain reads as the same effect,
    /// not as a separate burst. Returns false when there is nothing left to send.
    /// </summary>
    public bool TryAbsorbIntoGauge(Transform target, float duration)
    {
        if (target == null) return false;
        Awake();
        // The chain has settled, so nothing new is born. Release a parting handful at the
        // level that was just earned, then let every live particle travel to the gauge.
        Emit(8, 3, 2, 1);
        SetEmission(false);
        bool absorbed = false;
        foreach (ParticleSystem system in particles)
        {
            if (system == null || system.trails.enabled || system.particleCount <= 0) continue;
            ElegantPointLiveParticleAttractor attractor = system.GetComponent<ElegantPointLiveParticleAttractor>();
            if (attractor == null) attractor = system.gameObject.AddComponent<ElegantPointLiveParticleAttractor>();
            absorbed |= attractor.Initialize(system, target, duration);
        }
        return absorbed;
    }

    private void Emit(int fineSparkles, int lightMotes, int starGlints, int petals)
    {
        foreach (ParticleSystem system in particles)
        {
            int count = system.name switch
            {
                "FineSparkles" => fineSparkles,
                "LightMotes" => lightMotes,
                "StarGlints" => starGlints,
                "Petals" => petals,
                _ => 0
            };
            if (count <= 0) continue;
            if (!system.isPlaying) system.Play(false);
            system.Emit(count);
        }
    }

    private void SetEmission(bool value)
    {
        if (emitting == value) return;
        emitting = value;

        // Only stop new births. Each existing particle keeps its own lifetime and fade.
        foreach (ParticleSystem system in particles)
        {
            if (system == null) continue;
            var emission = system.emission;
            emission.enabled = value;
        }
    }

    private void OnDisable()
    {
        if (subscribedPoints != null) subscribedPoints.ActionSucceeded -= OnActionSucceeded;
        subscribedPoints = null;
        pendingSuccesses = 0;
        emitting = false;
        if (particles != null)
            foreach (ParticleSystem system in particles)
                if (system != null)
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (ribbonDrivers != null)
            foreach (GracefulParticleRibbon driver in ribbonDrivers) driver.Clear();
    }
}
