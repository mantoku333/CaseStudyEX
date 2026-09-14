using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Measures the ribbon during a sustained glide. Every line draws a short stroke that grows,
/// freezes and fades; leaving one on permanently reads as a strand of hair stuck to the
/// character. What must change with the level is the timing: the lines take turns around the
/// burst cycle so something is always on screen, without any single line staying on.
/// </summary>
public static class GracefulTrailRibbonContinuityVerification
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    private sealed class Sample
    {
        public int Level;
        public float Coverage;
        public int BlankFrames;
        public int Frames;
        public float BusiestDuty;
        public float MeanLive;
        public float WorstGapRatio;
        public float[] Duties;
    }

    [MenuItem("Tools/Effects/Verify Ribbon Continuity")]
    public static void Verify()
    {
        Directory.CreateDirectory("Temp/GracefulTrailRegression");
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GracefulTrailBuilder.PrefabPath));
        root.transform.localScale = Vector3.one * 1.59f;
        var trail = root.GetComponent<PlayerMovementTrail>();
        var systems = root.GetComponentsInChildren<ParticleSystem>();
        var lines = systems.Where(p => p.trails.enabled && !p.name.StartsWith("Fading_")).ToArray();
        var fades = systems.Where(p => p.name.StartsWith("Fading_")).ToArray();
        var samples = new List<Sample>();
        var report = new StringBuilder();
        try
        {
            typeof(PlayerMovementTrail).GetMethod("Awake", Private).Invoke(trail, null);
            // The extra lines belong to level three, not to the climb towards it. The level
            // blends over 0.45s per step, so a line that opens at 2.05 shows up during level two.
            foreach (GracefulParticleRibbon driver in root.GetComponentsInChildren<GracefulParticleRibbon>())
            {
                if (driver.name == "Ribbon_Main" || driver.name == "Ribbon_Accent") continue;
                for (float level = 2.05f; level <= 2.8f; level += 0.05f)
                    Require(!driver.IsBurstActive(0.5f, 0.6f, 0.9f, level, true, 1f),
                        $"{driver.name} must not appear before level three: active at {level:F2}");
                report.AppendLine($"{driver.name}: silent through level 2.80");
            }

            const float dt = 1f / 60f;
            foreach (int level in new[] { 1, 2, 3, 4 })
            {
                trail.SetPreviewLevel(level);
                typeof(PlayerMovementTrail).GetMethod("OnEnable", Private).Invoke(trail, null);
                var sample = new Sample { Level = level, Duties = new float[lines.Length] };
                int covered = 0;
                var perLine = new int[lines.Length];
                for (int frame = 0; frame < 240; frame++)
                {
                    // A glide: constant diagonal descent, never stopping.
                    root.transform.position += new Vector3(5f, -2f, 0f) * dt;
                    trail.Tick(dt, true);
                    foreach (ParticleSystem system in systems) system.Simulate(dt, false, false, false);
                    if (frame < 60) continue;
                    sample.Frames++;
                    int liveLines = 0;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].particleCount <= 0) continue;
                        liveLines++;
                        perLine[i]++;
                        Measure(lines[i], sample);
                    }
                    if (liveLines > 0) covered++;
                    if (liveLines == 0 && fades.All(f => f.particleCount == 0)) sample.BlankFrames++;
                    sample.MeanLive += liveLines;
                }

                sample.Coverage = 100f * covered / sample.Frames;
                sample.MeanLive /= sample.Frames;
                for (int i = 0; i < lines.Length; i++)
                {
                    sample.Duties[i] = 100f * perLine[i] / sample.Frames;
                    sample.BusiestDuty = Mathf.Max(sample.BusiestDuty, sample.Duties[i]);
                }
                samples.Add(sample);
                report.AppendLine($"level={level}: coverage={sample.Coverage:F1}%, blankFrames={sample.BlankFrames}/{sample.Frames}, " +
                    $"busiestLine={sample.BusiestDuty:F0}%, meanLiveLines={sample.MeanLive:F2}/{lines.Length}, " +
                    $"settledGap/width={sample.WorstGapRatio:F2}, " +
                    $"perLine=[{string.Join(", ", lines.Select((l, i) => $"{l.name}:{sample.Duties[i]:F0}%"))}]");
            }
        }
        finally { Object.DestroyImmediate(root); }

        File.WriteAllText("Temp/GracefulTrailRegression/RibbonContinuity.txt", report.ToString());
        Debug.Log(report.ToString());
        foreach (Sample sample in samples)
        {
            if (sample.Level <= 2)
            {
                // Levels one and two are meant to break into wisps and must stay untouched.
                Require(sample.Coverage < 70f,
                    $"Levels one and two keep their intermittent wisps: level {sample.Level} at {sample.Coverage:F1}%");
                continue;
            }

            Require(sample.Coverage >= 99.9f && sample.BlankFrames == 0,
                $"A glide at level {sample.Level} must never show a seam: {sample.Coverage:F1}% covered, {sample.BlankFrames} blank frames");
            Require(sample.BusiestDuty <= 90f,
                $"No line may stay on permanently at level {sample.Level}; that reads as hair: busiest line {sample.BusiestDuty:F0}%");
            // Taking turns, not drawing a solid curtain: only a couple of strokes at a time.
            Require(sample.MeanLive <= 3.2f,
                $"Too many lines are on at once at level {sample.Level}: {sample.MeanLive:F2} on average");
            Require(sample.WorstGapRatio < 0.5f,
                $"Settled strokes must stay dense at level {sample.Level}: gap/width {sample.WorstGapRatio:F2}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new System.InvalidOperationException(message);
    }

    // Newborn strokes are legitimately short and thin, so only settled ones are measured.
    private static void Measure(ParticleSystem line, Sample sample)
    {
        var buffer = new ParticleSystem.Particle[line.particleCount];
        int count = line.GetParticles(buffer);
        if (count < 40) return;
        float maxGap = 0f, totalSize = 0f;
        for (int i = 1; i < count; i++)
            maxGap = Mathf.Max(maxGap, Vector3.Distance(buffer[i].position, buffer[i - 1].position));
        for (int i = 0; i < count; i++) totalSize += buffer[i].startSize;
        float averageSize = totalSize / count;
        if (averageSize > 0f) sample.WorstGapRatio = Mathf.Max(sample.WorstGapRatio, maxGap / averageSize);
    }
}
