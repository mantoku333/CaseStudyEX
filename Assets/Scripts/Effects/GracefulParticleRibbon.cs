using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GracefulParticleRibbon : MonoBehaviour
{
    private const int VertexCount = 160;
    [SerializeField] private ParticleSystem ribbon;
    [SerializeField] private ParticleSystem[] fadingRibbons;
    [SerializeField, Min(0.1f)] private float historySeconds = 0.55f;
    [SerializeField, Min(0.1f)] private float maximumLength = 9.35f;
    [SerializeField, Min(0.01f)] private float width = 0.14f;
    [SerializeField, Min(0f)] private float sway = 0.65f;
    [SerializeField] private float phase;
    [SerializeField, Range(0f, 1f)] private float opacity = 0.46f;
    [SerializeField, Min(0.1f)] private float fadeSeconds = 0.46f;

    [Header("Level Appearance")]
    [SerializeField, Min(0f)] private float minimumLevel;
    [SerializeField, Min(0.05f)] private float fadeInSpan = 1f;
    [SerializeField, Min(0f)] private float burstDelay;
    [SerializeField, Min(0.05f)] private float burstSpan = 1f;
    [SerializeField, Range(0f, 1f)] private float phaseFraction;
    [SerializeField] private bool requiresDoubleBurst;

    private struct Sample
    {
        public Vector3 position;
        public float time;
    }

    private readonly List<Sample> history = new List<Sample>(256);
    private readonly ParticleSystem.Particle[] vertices = new ParticleSystem.Particle[VertexCount];
    private readonly Vector3[] positions = new Vector3[VertexCount];
    private ParticleSystem.Particle[][] fadingVertices;
    private float[] fadeTimes;
    private int[] fadeCounts;
    private float elapsed;
    private bool wasEmitting;
    private Vector3 previousOrigin;
    private int currentCount;
    private float levelSpread = 1f, levelLength = 1f, levelOpacity = 1f, tintLevel = 1f;

    /// <summary>Levels one and two keep the understated curve; three and four add the flourish.</summary>
    public void SetVisualLevel(float level)
    {
        float t = Mathf.Clamp01((level - 1f) / 3f);
        float flourish = Mathf.InverseLerp(2f, 4f, level);
        tintLevel = level;
        // Wider and longer on top of more lines turns the trail into hair. Above level two the
        // extra lines and their colour carry the level; each stroke keeps its own proportions.
        levelSpread = Mathf.Lerp(1f, 2.15f, t);
        levelLength = Mathf.Lerp(1f, 1.5f, t);
        levelOpacity = Mathf.Lerp(1f, 0.85f, t) * Mathf.Lerp(1f, 1.2f, flourish) * FadeIn(level);
    }

    // A ribbon reserved for a higher level fades in over its own span rather than popping on.
    // The span is short so the extra lines arrive at their level, not on the way up to it.
    private float FadeIn(float level) =>
        minimumLevel > 0f ? Mathf.Clamp01((level - minimumLevel) / fadeInSpan) : 1f;

    /// <summary>
    /// Every line still draws a short stroke that grows, freezes and fades; leaving one on
    /// permanently reads as a strand of hair stuck to the character. What changes with the
    /// level is when each line takes its turn: above level two the lines are dealt around the
    /// cycle instead of all restarting together, so their strokes overlap and the seams stop
    /// showing while no single strand stays on screen.
    /// </summary>
    public bool IsBurstActive(float burstClock, float duration, float cycle, float level, bool doubleBurst, float spread)
    {
        if (burstClock < 0f || (requiresDoubleBurst && !doubleBurst)) return false;
        if (minimumLevel > 0f && level <= minimumLevel + 0.05f) return false;
        float delay = Mathf.Lerp(burstDelay, cycle * phaseFraction, spread);
        return burstClock >= delay && burstClock < delay + duration * burstSpan;
    }

    public int ActiveFadeCount
    {
        get
        {
            int count = 0;
            if (fadeCounts != null)
                foreach (int size in fadeCounts) if (size > 0) count++;
            return count;
        }
    }

    public void Initialize()
    {
        fadingVertices = new ParticleSystem.Particle[fadingRibbons.Length][];
        fadeTimes = new float[fadingRibbons.Length];
        fadeCounts = new int[fadingRibbons.Length];
        for (int i = 0; i < fadingRibbons.Length; i++)
            fadingVertices[i] = new ParticleSystem.Particle[VertexCount];
        Clear();
        ribbon.Play(false);
        foreach (ParticleSystem system in fadingRibbons) system.Play(false);
    }

    public void Clear()
    {
        history.Clear();
        wasEmitting = false;
        currentCount = 0;
        elapsed = 0f;
        previousOrigin = transform.position;
        if (ribbon != null) ribbon.Clear(false);
        if (fadingRibbons == null) return;
        for (int i = 0; i < fadingRibbons.Length; i++)
        {
            if (fadingRibbons[i] != null) fadingRibbons[i].Clear(false);
            if (fadeCounts != null) fadeCounts[i] = 0;
        }
    }

    public void Advance(float deltaTime, bool emit)
    {
        elapsed += deltaTime;
        AdvanceFades(deltaTime);
        Vector3 origin = transform.position;
        if (emit)
        {
            if (!wasEmitting)
            {
                history.Clear();
                history.Add(new Sample { position = previousOrigin, time = elapsed - deltaTime });
            }

            if (Vector3.Distance(origin, history[history.Count - 1].position) > 0.015f)
                history.Add(new Sample { position = origin, time = elapsed });

            while (history.Count > 2 && (elapsed - history[1].time > historySeconds * levelLength || history.Count > 256))
                history.RemoveAt(0);

            BuildStroke();
        }
        else if (wasEmitting)
        {
            FreezeStroke();
            history.Clear();
            ribbon.Clear(false);
            currentCount = 0;
        }
        wasEmitting = emit;
        previousOrigin = origin;
    }

    private void BuildStroke()
    {
        if (history.Count < 2) return;
        float length = 0f;
        int oldest = history.Count - 1;
        while (oldest > 0 && length < maximumLength * levelLength)
        {
            length += Vector3.Distance(history[oldest].position, history[oldest - 1].position);
            oldest--;
        }
        if (length < 0.03f) return;
        currentCount = Mathf.Clamp(Mathf.CeilToInt(length / 0.015f) + 1, 4, VertexCount);
        int segment = history.Count - 2;
        float newestTime = history[history.Count - 1].time;
        float oldestTime = history[oldest].time;

        // Resample the whole stroke. Adjacent vertices share one smooth path and age order.
        for (int i = 0; i < currentCount; i++)
        {
            float u = i / (currentCount - 1f);
            float time = Mathf.Lerp(newestTime, oldestTime, u);
            while (segment > oldest && history[segment].time > time) segment--;
            float t = Mathf.InverseLerp(history[segment].time, history[segment + 1].time, time);
            Vector3 a = history[Mathf.Max(oldest, segment - 1)].position;
            Vector3 b = history[segment].position;
            Vector3 c = history[segment + 1].position;
            Vector3 d = history[Mathf.Min(history.Count - 1, segment + 2)].position;
            positions[i] = 0.5f * ((2f * b) + (-a + c) * t +
                (2f * a - 5f * b + 4f * c - d) * t * t + (-a + 3f * b - 3f * c + d) * t * t * t);
        }

        for (int i = 0; i < currentCount; i++)
        {
            float u = i / (currentCount - 1f);
            Vector3 tangent = positions[Mathf.Max(0, i - 1)] - positions[Mathf.Min(currentCount - 1, i + 1)];
            Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f).normalized;
            float wave = Mathf.Sin(u * 3.8f + phase) +
                0.08f * Mathf.Sin(u * 6f - elapsed * 0.6f + phase);
            float envelope = Mathf.SmoothStep(0f, 1f, u / 0.18f) * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.62f) / 0.38f));
            float feather = Mathf.SmoothStep(0f, 1f, u / 0.12f) * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.72f) / 0.28f));
            float growth = Mathf.SmoothStep(0f, 1f, length / 1.2f);
            float fold = 0.55f + 0.45f * Mathf.Pow(Mathf.Sin(u * 4.5f + 0.25f + phase * 0.3f), 2f);
            // The level shows in the ribbon, not in the particle count: white at level one,
            // gaining a pale wash of colour along the stroke as the chain grows.
            Color tint = GracefulPalette.RibbonTint(u + phase * 0.06f, tintLevel);
            vertices[i] = new ParticleSystem.Particle
            {
                position = positions[i] + normal * (wave * sway * Mathf.Sqrt(levelSpread) * Mathf.Sin(u * Mathf.PI) * growth),
                velocity = Vector3.zero,
                startLifetime = 100f,
                remainingLifetime = 99f - u,
                startSize = width * levelSpread * (0.18f + 0.82f * envelope) * growth * fold,
                startColor = new Color(tint.r, tint.g, tint.b,
                    opacity * levelOpacity * feather * (0.65f + 0.35f * fold)),
                randomSeed = (uint)i + 1
            };
        }
        ribbon.SetParticles(vertices, currentCount);
    }

    private void FreezeStroke()
    {
        if (currentCount == 0) return;
        int slot = 0;
        for (int i = 0; i < fadeCounts.Length; i++)
        {
            if (fadeCounts[i] == 0) { slot = i; break; }
            if (fadeTimes[i] > fadeTimes[slot]) slot = i;
        }
        System.Array.Copy(vertices, fadingVertices[slot], currentCount);
        // Release the connected stroke once. Unity then owns its lifetime and color fade.
        // The tiny age offset preserves ribbon ordering without tearing the mesh apart.
        for (int i = 0; i < currentCount; i++)
        {
            fadingVertices[slot][i].startLifetime = fadeSeconds;
            fadingVertices[slot][i].remainingLifetime = fadeSeconds - i * 0.000001f;
            fadingVertices[slot][i].velocity = Vector3.up * 0.12f;
        }
        fadeCounts[slot] = currentCount;
        fadeTimes[slot] = 0f;
        fadingRibbons[slot].SetParticles(fadingVertices[slot], currentCount);
    }

    private void AdvanceFades(float deltaTime)
    {
        for (int slot = 0; slot < fadeCounts.Length; slot++)
        {
            if (fadeCounts[slot] == 0) continue;
            fadeTimes[slot] += deltaTime;
            fadeCounts[slot] = fadingRibbons[slot].particleCount;
        }
    }
}
