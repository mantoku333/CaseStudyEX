using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class GracefulTrailVerification
{
    [MenuItem("Tools/Effects/Verify Graceful Movement Trail")]
    public static void Verify()
    {
        const string output = "Temp/GracefulTrailVerification";
        Directory.CreateDirectory(output);
        var preview = new PreviewRenderUtility();
        GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GracefulTrailBuilder.PrefabPath));
        preview.AddSingleGO(root);
        try
        {
            Scene scene = SceneManager.GetSceneByPath(GracefulTrailBuilder.ScenePath);
            if (scene.isLoaded)
            {
                var player = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<PlayerController>(true)).Single();
                root.transform.localScale = player.transform.Find("FX_Trail").lossyScale;
            }
            var particles = root.GetComponentsInChildren<ParticleSystem>();
            Require(particles.Length == 20, "Expected five sprite layers, five live ribbons, and ten fading ribbons.");
            foreach (ParticleSystem system in particles)
            {
                Require(system.main.simulationSpace == ParticleSystemSimulationSpace.World, "Particles must remain in world space.");
                Material material = system.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                Require(material != null && material.mainTexture != null && material.shader.isSupported, "Missing particle material or texture.");
                Require(!ShaderUtil.ShaderHasError(material.shader), "Particle shader has compilation errors.");
                system.useAutoRandomSeed = false;
                system.randomSeed = (uint)(Array.IndexOf(particles, system) + 1) * 153;
            }
            ParticleSystem petals = particles.Single(p => p.name == "Petals");
            Require(petals.main.startRotation3D && petals.rotationOverLifetime.separateAxes &&
                petals.rotationOverLifetime.y.constantMax > 0f, "Petals must turn out of the sprite plane.");
            Require(petals.GetComponent<ParticleSystemRenderer>().sharedMaterial.GetFloat("_Halo") > 0f,
                "Petals need a soft silhouette glow.");

            var controller = root.GetComponent<PlayerMovementTrail>();
            controller.SetPreviewLevel(1);
            Require(!new SerializedObject(controller).FindProperty("onlyDuringGracefulActions").boolValue,
                "Action-only gating must be disabled for visual tuning.");
            root.transform.position = new Vector3(-12f, 0f, 0f);
            Invoke(controller, "Awake");
            Invoke(controller, "OnEnable");
            Step(controller, particles, 0.2f, Vector3.zero);
            Require(!controller.IsEmitting && particles.Sum(p => p.particleCount) == 0, "Idle must not emit.");

            Step(controller, particles, 0.3f, Vector3.right * 7f, false);
            Require(!controller.IsEmitting && particles.Sum(p => p.particleCount) == 0, "Explicitly disabled effects must not emit.");
            root.transform.position = new Vector3(-8.15f, 0f, 0f);
            Invoke(controller, "OnEnable");

            Step(controller, particles, 1.45f, Vector3.right * 7f);
            Require(controller.IsEmitting, "Movement must emit.");
            int movingCount = particles.Sum(p => p.particleCount);
            Require(movingCount > 20, "Moving effect did not generate enough particles: " + movingCount);
            Render(preview, output + "/Moving.png", new Color(0.045f, 0.055f, 0.09f), 1200, 650);
            Render(preview, output + "/MovingLight.png", new Color(0.45f, 0.48f, 0.52f), 1200, 650);
            RenderPlayerScale(preview, root, output);

            VerifyLifetimePreservation(controller, particles, true);
            Step(controller, particles, 0.08f, Vector3.zero);
            Require(!controller.IsEmitting && particles.Sum(p => p.particleCount) > 0, "Stopping must retain the fading particles.");
            Render(preview, output + "/Stopping.png", new Color(0.045f, 0.055f, 0.09f), 1200, 650);
            Step(controller, particles, 0.22f, Vector3.zero);
            Render(preview, output + "/Fade030.png", new Color(0.045f, 0.055f, 0.09f), 1200, 650, requireVisible: false);
            Step(controller, particles, 0.3f, Vector3.zero);
            Render(preview, output + "/Fade060.png", new Color(0.045f, 0.055f, 0.09f), 1200, 650, requireVisible: false);
            Require(particles.Any(p => !p.trails.enabled && p.particleCount > 0), "Long-lived particles must retain their natural tail after stopping.");
            Step(controller, particles, 1.2f, Vector3.zero);
            Require(particles.Sum(p => p.particleCount) == 0, "Stopped particles must completely expire.");

            Step(controller, particles, 0.4f, Vector3.left * 7f);
            Require(controller.IsEmitting && particles.Sum(p => p.particleCount) > 0, "Restart must emit when moving left.");
            root.transform.position += Vector3.right * 20f;
            controller.Tick(1f / 60f, true);
            Require(!controller.IsEmitting && particles.Sum(p => p.particleCount) == 0, "Teleport must clear the old stroke.");
            Step(controller, particles, 0.4f, Vector3.left * 7f);
            Invoke(controller, "OnDisable");
            Require(particles.Sum(p => p.particleCount) == 0, "Disable must clear particles.");
            Invoke(controller, "OnEnable");
            Step(controller, particles, 0.2f, Vector3.zero);
            Require(!controller.IsEmitting && particles.Sum(p => p.particleCount) == 0, "Re-enable must stay idle until movement.");
            float physicsTime = 0f;
            for (int i = 0; i < 144; i++)
            {
                physicsTime += 1f / 144f;
                if (physicsTime >= 0.02f)
                {
                    controller.transform.position += Vector3.right * 0.14f;
                    physicsTime -= 0.02f;
                }
                controller.Tick(1f / 144f, true);
                if (i > 3) Require(controller.IsEmitting, "Physics/render timing must not break ribbons.");
            }
            controller.Tick(0f, true);
            Require(controller.IsEmitting, "Pause must preserve the particle state.");
            VerifyEmissionHeight(controller, particles, output);
            VerifyBursts(preview, root, controller, particles, output);
            VerifyLevels(preview, root, controller, particles, output);
            RenderSceneContext(output);
            File.WriteAllText(output + "/Result.txt", "PASS: four levels, smooth blending, level-zero natural expiry, fresh-chain restart, point-controller integration without legacy manager, level-four cap; shaders, movement, lifetime preservation, teleport, pause, 50 Hz physics at 144 fps, intermittent ribbons and fine particles. Moving particles=" + movingCount);
        }
        finally { preview.Cleanup(); }
    }

    // The trail belongs at the waist. A doubled offset once dropped it to the ankles, which
    // reads as the effect leaking out of the character's feet.
    private static void VerifyEmissionHeight(PlayerMovementTrail controller, ParticleSystem[] particles, string output)
    {
        GameObject body = CreatePlayerReference(controller.gameObject);
        if (body == null) return;
        try
        {
            Bounds bounds = body.GetComponent<SpriteRenderer>().bounds;
            Invoke(controller, "OnEnable");
            controller.SetPreviewLevel(1);
            Step(controller, particles, 0.25f, Vector3.right * 7f);
            float total = 0f;
            int count = 0;
            foreach (ParticleSystem system in particles.Where(p => !p.trails.enabled))
            {
                var buffer = new ParticleSystem.Particle[system.particleCount];
                int live = system.GetParticles(buffer);
                for (int i = 0; i < live; i++) { total += buffer[i].position.y; count++; }
            }
            Require(count > 0, "Emission height check needs particles.");
            float fraction = Mathf.InverseLerp(bounds.min.y, bounds.max.y, total / count);
            File.WriteAllText(output + "/EmissionHeight.txt",
                $"averageY={total / count:F3}, body={bounds.min.y:F3}..{bounds.max.y:F3}, fraction={fraction:P1}");
            Require(fraction > 0.28f && fraction < 0.72f,
                $"The trail must leave from the waist, not the feet or the head: {fraction:P0} up the body");
        }
        finally { UnityEngine.Object.DestroyImmediate(body); }
    }

    private static void VerifyLevels(PreviewRenderUtility preview, GameObject root, PlayerMovementTrail controller, ParticleSystem[] particles, string output)
    {
        float previousRate = 0f;
        var rates = new float[5];
        var sizes = new float[5];
        var petalSizes = new float[5];
        var petalRates = new float[5];
        var radii = new float[5];
        var tints = new float[5];
        var ribbons = new int[5];
        controller.SetPreviewLevel(1);
        root.transform.position = Vector3.left * 10.15f;
        Invoke(controller, "OnEnable");
        for (int level = 1; level <= 4; level++)
        {
            controller.SetPreviewLevel(level);
            GameObject body = CreatePlayerReference(root);
            Step(controller, particles, 1.45f, Vector3.right * 7f);
            ParticleSystem dust = particles.Single(p => p.name == "FineSparkles");
            float rate = dust.emission.rateOverDistance.constant;
            Require(rate > previousRate, "Each level must increase fine sparkle density.");
            previousRate = rate;
            rates[level] = rate;
            sizes[level] = particles.Single(p => p.name == "SoftGlow").main.startSize.constantMax;
            petalSizes[level] = particles.Single(p => p.name == "Petals").main.startSize.constantMax;
            petalRates[level] = particles.Single(p => p.name == "Petals").emission.rateOverDistance.constant;
            radii[level] = dust.shape.radius;
            for (int frame = 0; frame < 90; frame++)
            {
                Step(controller, particles, 1f / 60f, Vector3.right * 7f);
                ribbons[level] = Mathf.Max(ribbons[level], particles.Count(p =>
                    p.trails.enabled && !p.name.StartsWith("Fading_") && p.particleCount > 0));
                tints[level] = Mathf.Max(tints[level], RibbonColourSpread(particles));
            }
            Render(preview, output + $"/Level{level}.png", new Color(0.045f, 0.055f, 0.09f), 1280, 720,
                4f, true, (body != null ? body.GetComponent<SpriteRenderer>().bounds.center : root.transform.position) + new Vector3(-2.6f, 0f, -10f));
            UnityEngine.Object.DestroyImmediate(body);
        }
        // Level one is the untouched baseline. Every applied value must equal what the prefab
        // authors, with no multiplier of any kind.
        var authored = AssetDatabase.LoadAssetAtPath<GameObject>(GracefulTrailBuilder.PrefabPath)
            .GetComponentsInChildren<ParticleSystem>(true);
        ParticleSystem authoredDust = authored.Single(p => p.name == "FineSparkles");
        Require(Mathf.Approximately(rates[1], authoredDust.emission.rateOverDistance.constant) &&
            Mathf.Approximately(radii[1], authoredDust.shape.radius) &&
            Mathf.Approximately(petalRates[1], authored.Single(p => p.name == "Petals").emission.rateOverDistance.constant) &&
            Mathf.Approximately(petalSizes[1], authored.Single(p => p.name == "Petals").main.startSize.constantMax) &&
            Mathf.Approximately(sizes[1], authored.Single(p => p.name == "SoftGlow").main.startSize.constantMax) &&
            Mathf.Approximately(tints[1], 0f),
            "Level one must be left exactly as authored, ribbon colour included.");

        // The level is carried by the ribbon, not by the particle count. Counts, sizes and
        // spread stop growing at level two; more and bigger sprites read as noise.
        Require(rates[4] <= rates[2] * 1.35f && petalRates[4] <= petalRates[2] * 1.05f,
            $"Particle counts must not pile up above level two: fine {rates[2]:F2} -> {rates[4]:F2}, " +
            $"petals {petalRates[2]:F3} -> {petalRates[4]:F3}");
        Require(petalSizes.Skip(1).All(size => Mathf.Approximately(size, petalSizes[1])) &&
            Mathf.Approximately(radii[4], radii[2]),
            $"Petal size and spread must stop at level two: petals " +
            $"{string.Join(" / ", petalSizes.Skip(1).Select(size => size.ToString("F4")))}, radius {radii[2]:F3} -> {radii[4]:F3}");
        Require(ribbons[3] > ribbons[2] && ribbons[4] > ribbons[3],
            $"Each level above two must add ribbon lines: {ribbons[2]} -> {ribbons[3]} -> {ribbons[4]}");
        Require(tints[2] > 0f && tints[3] > tints[2] && tints[4] > tints[3],
            $"The ribbon must take on more colour with each level: {tints[1]:F3} / {tints[2]:F3} / {tints[3]:F3} / {tints[4]:F3}");
        Require(sizes[4] > sizes[2], $"The soft halo may swell with the level: {sizes[2]:F3} -> {sizes[4]:F3}");
        File.WriteAllText(output + "/LevelImpact.txt", string.Join("\n", Enumerable.Range(1, 4)
            .Select(level => $"level={level}, fineRate={rates[level]:F3}, radius={radii[level]:F3}, " +
                $"glowSize={sizes[level]:F4}, petalSize={petalSizes[level]:F4}, petalRate={petalRates[level]:F3}, " +
                $"peakRibbons={ribbons[level]}, ribbonColour={tints[level]:F3}")));
        controller.SetPreviewLevel(1);
        Invoke(controller, "OnEnable");
        Step(controller, particles, 0.3f, Vector3.right * 7f);
        var petal = particles.Single(p => p.name == "Petals");
        int before = petal.particleCount;
        controller.SetPreviewLevel(4);
        controller.transform.position += Vector3.right * 0.1f;
        controller.Tick(0.01f, true);
        Require(controller.VisualLevel < 2f, "Level changes must blend instead of jumping.");
        Require(petal.particleCount == before, "Changing level must not clear existing petals.");
        controller.SetPreviewLevel(0);
        Step(controller, particles, 0.05f, Vector3.right * 7f);
        Require(!controller.IsEmitting && particles.Sum(p => p.particleCount) > 0, "Level zero must release, not erase particles.");
        Step(controller, particles, 2f, Vector3.right * 7f);
        Require(particles.Sum(p => p.particleCount) == 0, "Level zero must not spawn while moving.");
        controller.SetPreviewLevel(9);
        Require(controller.CurrentLevel == 4, "Visual levels must cap at four.");
        Invoke(controller, "OnEnable");
        controller.SetPreviewLevel(0);
        controller.Tick(0.02f, true);
        controller.SetPreviewLevel(1);
        controller.Tick(0.02f, true);
        Require(controller.VisualLevel == 1f, "A fresh chain must restart at the understated level one.");
        var pointObject = new GameObject("Trail level integration test");
        try
        {
            var points = pointObject.AddComponent<PlayerElegantPointController>();
            var settings = new SerializedObject(controller);
            settings.FindProperty("pointController").objectReferenceValue = points;
            settings.ApplyModifiedPropertiesWithoutUndo();
            controller.ClearPreviewLevel();
            Require(controller.CurrentLevel == 0, "No successful action must mean no effect.");
            for (int level = 1; level <= 5; level++)
            {
                points.NotifySuccessfulAction(ElegantActionType.Glide);
                Require(controller.CurrentLevel == Mathf.Min(level, 4), "Trail must follow points without the legacy FX manager.");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(pointObject); }
    }

    // How far the ribbon has moved off pure white: the widest channel gap on a live stroke.
    private static float RibbonColourSpread(ParticleSystem[] particles)
    {
        float spread = 0f;
        foreach (ParticleSystem system in particles.Where(p => p.trails.enabled && p.particleCount > 0))
        {
            var buffer = new ParticleSystem.Particle[system.particleCount];
            int live = system.GetParticles(buffer);
            for (int i = 0; i < live; i++)
            {
                Color color = buffer[i].startColor;
                spread = Mathf.Max(spread, Mathf.Max(color.r, Mathf.Max(color.g, color.b)) -
                    Mathf.Min(color.r, Mathf.Min(color.g, color.b)));
            }
        }
        return spread;
    }

    private static void RenderSceneContext(string output)
    {
        Scene scene = SceneManager.GetSceneByPath(GracefulTrailBuilder.ScenePath);
        if (!scene.isLoaded) return;
        var player = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<PlayerController>(true)).Single();
        var source = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(camera => camera.CompareTag("MainCamera") && camera.orthographic);
        if (source == null) return;
        Transform anchor = player.transform.Find("FX_Trail");
        GameObject effect = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GracefulTrailBuilder.PrefabPath));
        var cameraObject = new GameObject("Trail verification camera");
        effect.hideFlags = cameraObject.hideFlags = HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(effect, scene);
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        RenderTexture target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        try
        {
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.enabled = false;
            var sourceData = source.GetComponent<UniversalAdditionalCameraData>();
            if (sourceData != null)
                EditorUtility.CopySerialized(sourceData, cameraObject.AddComponent<UniversalAdditionalCameraData>());
            camera.aspect = 1280f / 720f;
            effect.transform.localScale = anchor.lossyScale;
            effect.transform.position = anchor.position + Vector3.left * (7f * 1.45f);
            var particles = effect.GetComponentsInChildren<ParticleSystem>();
            var body = player.transform.Find("SpriteView").GetComponent<SpriteRenderer>();
            camera.transform.SetPositionAndRotation(body.bounds.center + new Vector3(-2f, 0f, -10f), Quaternion.identity);
            foreach (ParticleSystem system in particles)
            {
                system.useAutoRandomSeed = false;
                system.randomSeed = (uint)(Array.IndexOf(particles, system) + 1) * 153;
                var renderer = system.GetComponent<ParticleSystemRenderer>();
                renderer.sortingLayerID = body.sortingLayerID;
                renderer.sortingOrder = body.sortingOrder + 1;
            }
            var controller = effect.GetComponent<PlayerMovementTrail>();
            controller.SetPreviewLevel(4);
            Invoke(controller, "Awake");
            Invoke(controller, "OnEnable");
            Step(controller, particles, 1.45f, Vector3.right * 7f);
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            Require(RenderPipeline.SupportsRenderRequest(camera, request), "Scene capture requires URP render requests.");
            foreach (float zoom in new[] { source.orthographicSize, 5f })
            {
                camera.orthographicSize = zoom;
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                File.WriteAllBytes(output + (zoom == source.orthographicSize ? "/SceneGameplayScale.png" : "/SceneCloseup.png"), texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(effect);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    private static void VerifyLifetimePreservation(PlayerMovementTrail controller, ParticleSystem[] particles, bool graceful)
    {
        var sprites = particles.Where(p => !p.trails.enabled).ToArray();
        var before = sprites.Select(p =>
        {
            var buffer = new ParticleSystem.Particle[p.particleCount];
            p.GetParticles(buffer);
            return buffer;
        }).ToArray();
        Require(before.SelectMany(p => p).Any(p => p.remainingLifetime > 0.5f), "Lifetime check needs young particles.");
        controller.Tick(0.08f, graceful);
        Require(!controller.IsEmitting, "Release must stop emission.");
        for (int system = 0; system < sprites.Length; system++)
        {
            var after = new ParticleSystem.Particle[sprites[system].particleCount];
            sprites[system].GetParticles(after);
            Require(after.Length == before[system].Length, "Stopping must not delete particles.");
            for (int i = 0; i < after.Length; i++)
                Require(after[i].randomSeed == before[system][i].randomSeed &&
                    after[i].startLifetime == before[system][i].startLifetime &&
                    after[i].remainingLifetime == before[system][i].remainingLifetime,
                    "Stopping must not rewrite individual particle lifetimes.");
        }
    }

    private static void Step(PlayerMovementTrail controller, ParticleSystem[] particles, float seconds, Vector3 velocity, bool graceful = true)
    {
        const float dt = 1f / 60f;
        for (int i = 0; i < Mathf.RoundToInt(seconds / dt); i++)
        {
            controller.transform.position += velocity * dt;
            controller.Tick(dt, graceful);
            foreach (ParticleSystem system in particles)
                system.Simulate(dt, false, false, false);
        }
    }

    private static void RenderPlayerScale(PreviewRenderUtility preview, GameObject root, string output)
    {
        GameObject spriteObject = CreatePlayerReference(root);
        if (spriteObject == null) return;
        Vector3 framing = spriteObject.GetComponent<SpriteRenderer>().bounds.center + new Vector3(-2.6f, 0f, -10f);
        Render(preview, output + "/PlayerScale.png", new Color(0.045f, 0.055f, 0.09f), 1200, 720, 3.6f, cameraPosition: framing);
        Scene scene = SceneManager.GetSceneByPath(GracefulTrailBuilder.ScenePath);
        Camera gameplayCamera = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Camera>(true))
            .FirstOrDefault(camera => camera.CompareTag("MainCamera") && camera.orthographic);
        if (gameplayCamera != null)
            Render(preview, output + "/GameplayScale.png", new Color(0.045f, 0.055f, 0.09f), 1280, 720, gameplayCamera.orthographicSize, cameraPosition: framing);
        UnityEngine.Object.DestroyImmediate(spriteObject);
    }

    private static GameObject CreatePlayerReference(GameObject root)
    {
        Scene scene = SceneManager.GetSceneByPath(GracefulTrailBuilder.ScenePath);
        if (!scene.isLoaded) return null;
        var player = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<PlayerController>(true)).Single();
        Transform anchor = player.transform.Find("FX_Trail");
        Require(anchor != null && Quaternion.Angle(anchor.rotation, Quaternion.identity) < 0.1f, "Scene trail must lie in the XY plane.");
        var body = player.transform.Find("SpriteView").GetComponent<SpriteRenderer>();
        var spriteObject = new GameObject("Player scale reference");
        spriteObject.transform.SetParent(root.transform, false);
        spriteObject.transform.position = root.transform.position + body.transform.position - anchor.position;
        Vector3 effectScale = root.transform.lossyScale;
        Vector3 bodyScale = body.transform.lossyScale;
        spriteObject.transform.localScale = new Vector3(bodyScale.x / effectScale.x, bodyScale.y / effectScale.y, bodyScale.z / effectScale.z);
        spriteObject.transform.rotation = body.transform.rotation;
        var sprite = spriteObject.AddComponent<SpriteRenderer>();
        sprite.sprite = body.sprite;
        sprite.flipX = body.flipX;
        sprite.sharedMaterial = body.sharedMaterial;
        sprite.sortingOrder = 2;
        return spriteObject;
    }

    private static void VerifyBursts(PreviewRenderUtility preview, GameObject root, PlayerMovementTrail controller, ParticleSystem[] particles, string output)
    {
        root.transform.position = new Vector3(-12f, 0f, 0f);
        Invoke(controller, "OnEnable");
        GameObject spriteObject = CreatePlayerReference(root);
        int minRibbons = int.MaxValue;
        int maxRibbons = 0;
        for (int frame = 0; frame < 120; frame++)
        {
            Step(controller, particles, 1f / 30f, frame < 60 ? Vector3.right * 7f : Vector3.zero);
            int live = particles.Count(p => p.trails.enabled && p.particleCount > 0);
            if (frame > 4 && frame < 60) minRibbons = Mathf.Min(minRibbons, live);
            if (frame > 4 && frame < 60 && live == 0)
                Require(particles.Single(p => p.name == "FineSparkles").particleCount > 0,
                    "Fine sparkles must remain visible between ribbon bursts.");
            maxRibbons = Mathf.Max(maxRibbons, live);
            Render(preview, output + $"/Burst{frame:D3}.png", new Color(0.045f, 0.055f, 0.09f), 960, 540,
                4f, false, (spriteObject != null ? spriteObject.GetComponent<SpriteRenderer>().bounds.center : root.transform.position)
                    + new Vector3(-2.6f, 0f, -10f));
        }
        Require(minRibbons == 0, "Ribbon bursts need visible intervals with no ribbon.");
        Require(maxRibbons == 2, "Bursts should vary between one and two ribbons.");
        UnityEngine.Object.DestroyImmediate(spriteObject);
        Require(particles.Sum(p => p.particleCount) == 0, "All particles must expire naturally after two seconds stopped.");
        Step(controller, particles, 0.5f, Vector3.right * 7f);
        controller.transform.position += Vector3.right * 0.1f;
        VerifyLifetimePreservation(controller, particles, false);
        Step(controller, particles, 1.8f, Vector3.right * 7f, false);
        Require(particles.Sum(p => p.particleCount) == 0, "Ending the action while still moving must allow natural expiry without new births.");
    }

    private static void Render(PreviewRenderUtility preview, string path, Color background, int width, int height, float size = 1.6f,
        bool requireVisible = true, Vector3? cameraPosition = null)
    {
        preview.BeginStaticPreview(new Rect(0, 0, width, height));
        preview.camera.orthographic = true;
        preview.camera.orthographicSize = size;
        preview.camera.transform.position = cameraPosition ?? new Vector3(-0.7f, 0.15f, -10f);
        preview.camera.transform.rotation = Quaternion.identity;
        preview.camera.clearFlags = CameraClearFlags.SolidColor;
        preview.camera.backgroundColor = background;
        preview.camera.nearClipPlane = 0.1f;
        preview.camera.farClipPlane = 30f;
        preview.Render(true);
        Texture2D texture = preview.EndStaticPreview();
        Color[] pixels = texture.GetPixels();
        int visible = pixels.Count(pixel => pixel.r > background.r + 0.15f || pixel.g > background.g + 0.15f || pixel.b > background.b + 0.15f);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        if (requireVisible) Require(visible > 100, "Trail render appears blank: " + path);
    }

    private static void Invoke(PlayerMovementTrail controller, string method)
    {
        typeof(PlayerMovementTrail).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
