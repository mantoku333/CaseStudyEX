using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class GracefulTrailRegressionVerification
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    [MenuItem("Tools/Effects/Verify Trail Continuous Transitions")]
    public static void Verify()
    {
        Directory.CreateDirectory("Temp/GracefulTrailRegression");
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GracefulTrailBuilder.PrefabPath));
        var trail = root.GetComponent<PlayerMovementTrail>();
        var systems = root.GetComponentsInChildren<ParticleSystem>();
        var report = new System.Text.StringBuilder();
        bool passed = true;
        try
        {
            typeof(PlayerMovementTrail).GetMethod("Awake", Private).Invoke(trail, null);
            trail.SetPreviewLevel(1);
            typeof(PlayerMovementTrail).GetMethod("OnEnable", Private).Invoke(trail, null);
            foreach (int fps in new[] { 30, 60, 144 })
            for (int cycle = 0; cycle < 3; cycle++)
            foreach (int level in new[] { 1, 2, 3, 4, 0 })
            {
                trail.SetPreviewLevel(level);
                float minimum = 1f;
                int peakRibbons = 0;
                for (int frame = 0; frame < fps * 3; frame++)
                {
                    root.transform.position += Vector3.right * (7f / fps);
                    trail.Tick(1f / fps, true);
                    peakRibbons = Mathf.Max(peakRibbons, systems.Count(p => p.trails.enabled && p.particleCount > 0));
                    foreach (var system in systems)
                    {
                        system.Simulate(1f / fps, false, false, false);
                        if (system.trails.enabled) continue;
                        var gradient = system.main.startColor.gradient;
                        if (gradient == null) continue;
                        foreach (var key in gradient.colorKeys)
                        {
                            passed &= Finite(key.color.r) && Finite(key.color.g) && Finite(key.color.b);
                            minimum = Mathf.Min(minimum, key.color.r, key.color.g, key.color.b);
                        }
                    }
                }
                var dust = systems.Single(p => p.name == "FineSparkles");
                report.AppendLine($"fps={fps}, cycle={cycle}, level={level}, visual={trail.VisualLevel:F3}, minRGB={minimum:F6}, fineCount={dust.particleCount}, fineRate={dust.emission.rateOverDistance.constant:F3}, peakRibbons={peakRibbons}");
                passed &= minimum >= 0.75f && (level == 0 ? systems.All(p => p.particleCount == 0) : dust.particleCount > 0 && peakRibbons > 0);
            }
            passed &= VerifySuccessFeedback(root, trail, systems, report);
            passed &= VerifyGaugeAbsorption(root, trail, systems, report);
            GracefulTrailRibbonContinuityVerification.Verify();
            report.AppendLine(passed ? "PASS" : "FAIL");
            File.WriteAllText("Temp/GracefulTrailRegression/Result.txt", report.ToString());
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        if (!passed) throw new InvalidOperationException("Continuous transition regression failed; see Temp/GracefulTrailRegression/Result.txt");
    }

    // The gauge gain must be the trail itself travelling to the HUD, not a separate burst.
    private static bool VerifyGaugeAbsorption(GameObject root, PlayerMovementTrail trail, ParticleSystem[] systems, System.Text.StringBuilder report)
    {
        const float dt = 1f / 60f;
        var gauge = new GameObject("Gauge absorption target");
        try
        {
            trail.SetPreviewLevel(3);
            typeof(PlayerMovementTrail).GetMethod("OnEnable", Private).Invoke(trail, null);
            var sprites = systems.Where(system => !system.trails.enabled).ToArray();
            for (int frame = 0; frame < 90; frame++)
            {
                root.transform.position += Vector3.right * (7f * dt);
                trail.Tick(dt, true);
                foreach (var system in sprites) system.Simulate(dt, false, false, false);
            }

            gauge.transform.position = root.transform.position + new Vector3(-9f, 6f, 0f);
            int captured = sprites.Sum(system => system.particleCount);
            if (!trail.TryAbsorbIntoGauge(gauge.transform, 1.2f)) return false;
            var attractors = sprites.Select(system => system.GetComponent<ElegantPointLiveParticleAttractor>()).ToArray();
            if (attractors.Any(attractor => attractor == null) || trail.IsEmitting) return false;
            if (sprites.Any(system => system.emission.enabled)) return false;

            float start = AverageDistance(sprites, gauge.transform.position);
            float startAlpha = AverageAlpha(sprites);
            float closest = start;
            float dimmest = startAlpha;
            float firstStep = 0f;
            for (int frame = 0; frame < 150; frame++)
            {
                foreach (var attractor in attractors) if (attractor != null) attractor.Advance(dt);
                foreach (var system in sprites) system.Simulate(dt, false, false, false);
                if (sprites.Sum(system => system.particleCount) == 0) break;
                if (frame == 0) firstStep = Mathf.Abs(AverageDistance(sprites, gauge.transform.position) - start);
                closest = Mathf.Min(closest, AverageDistance(sprites, gauge.transform.position));
                dimmest = Mathf.Min(dimmest, AverageAlpha(sprites));
            }

            int remaining = sprites.Sum(system => system.particleCount);
            report.AppendLine($"gauge absorption: captured={captured}, startDistance={start:F3}, " +
                $"closestDistance={closest:F3}, firstFrameStep={firstStep:F4}, " +
                $"startAlpha={startAlpha:F1}, dimmestAlpha={dimmest:F1}, remaining={remaining}");
            // The handover must be gentle (no jump on the first frame), the particles must
            // actually reach the gauge, fade on the way, and expire rather than pile up.
            return captured > 0 && firstStep < 0.2f && closest < start * 0.5f &&
                dimmest < startAlpha * 0.8f && remaining == 0;
        }
        finally { UnityEngine.Object.DestroyImmediate(gauge); }
    }

    private static float AverageAlpha(ParticleSystem[] systems)
    {
        float total = 0f;
        int count = 0;
        foreach (ParticleSystem system in systems)
        {
            var buffer = new ParticleSystem.Particle[system.particleCount];
            int live = system.GetParticles(buffer);
            for (int i = 0; i < live; i++)
            {
                total += buffer[i].startColor.a;
                count++;
            }
        }
        return count == 0 ? 0f : total / count;
    }

    private static float AverageDistance(ParticleSystem[] systems, Vector3 target)
    {
        float total = 0f;
        int count = 0;
        foreach (ParticleSystem system in systems)
        {
            var buffer = new ParticleSystem.Particle[system.particleCount];
            int live = system.GetParticles(buffer);
            for (int i = 0; i < live; i++)
            {
                total += Vector3.Distance(buffer[i].position, target);
                count++;
            }
        }
        return count == 0 ? 0f : total / count;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;

    private static bool VerifySuccessFeedback(GameObject root, PlayerMovementTrail trail, ParticleSystem[] systems, System.Text.StringBuilder report)
    {
        var pointObject = new GameObject("Success feedback source");
        float timeScale = Time.timeScale;
        try
        {
            var points = pointObject.AddComponent<PlayerElegantPointController>();
            typeof(PlayerMovementTrail).GetField("pointController", Private).SetValue(trail, points);
            trail.ClearPreviewLevel();
            trail.Tick(0.01f, true);
            foreach (ElegantActionType action in Enum.GetValues(typeof(ElegantActionType)))
            {
                if (action == ElegantActionType.NormalAttack) continue;
                foreach (var system in systems) system.Clear(false);
                points.NotifySuccessfulAction(action);
                Time.timeScale = 0f;
                trail.Tick(0f, true);
                if (systems.Any(p => !p.trails.enabled && p.particleCount > 0)) return false;
                Time.timeScale = timeScale;
                trail.Tick(1f / 60f, true);
                int fine = systems.Single(p => p.name == "FineSparkles").particleCount;
                int stars = systems.Single(p => p.name == "StarGlints").particleCount;
                report.AppendLine($"stationary success={action}, fine={fine}, stars={stars}, visualTarget={trail.CurrentLevel}");
                if (fine < 5 || stars < 1) return false;
                int before = fine;
                trail.Tick(1f / 60f, true);
                if (systems.Single(p => p.name == "FineSparkles").particleCount != before) return false;
            }
            trail.Tick(2f, true);
            if (trail.VisualLevel != 4f) return false;
            var chain = (ElegantActionChain)typeof(PlayerElegantPointController).GetField("chain", Private).GetValue(points);
            chain.ResetWithoutReward();
            points.NotifySuccessfulAction(ElegantActionType.Glide);
            trail.Tick(1f / 60f, true);
            report.AppendLine($"same-frame chain restart: target={trail.CurrentLevel}, visual={trail.VisualLevel}");
            return trail.CurrentLevel == 1 && trail.VisualLevel == 1f;
        }
        finally { Time.timeScale = timeScale; UnityEngine.Object.DestroyImmediate(pointObject); }
    }
}
