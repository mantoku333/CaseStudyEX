using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class GracefulTrailBuilder
{
    public const string ScenePath = "Assets/Scenes/FixScenes/Fuyuno_Fix_UI.unity";
    public const string PrefabPath = "Assets/Prefabs/Effects/FX_GracefulMovementTrail.prefab";
    private const string ArtPath = "Assets/Art/VFX/GracefulTrail";

    [MenuItem("Tools/Effects/Install Graceful Movement Trail")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Install the trail outside Play mode.");

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        PlayerController player = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PlayerController>(true)).Single();
        GameObject prefab = BuildPrefab();
        Transform placeholder = player.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(child => child.name == "FX_Trail");
        if (placeholder == null)
        {
            GameObject root = new GameObject("FX_Trail");
            Undo.RegisterCreatedObjectUndo(root, "Create FX_Trail");
            root.transform.SetParent(player.transform, false);
            placeholder = root.transform;
        }

        Undo.SetTransformParent(placeholder, player.transform, "Attach trail to player");
        Undo.RecordObject(placeholder, "Position movement trail");
        SpriteRenderer body = player.transform.Find("SpriteView")?.GetComponent<SpriteRenderer>();
        if (body != null && body.sprite != null)
        {
            Vector3 waist = body.bounds.center + Vector3.down * body.bounds.size.y * 0.12f;
            placeholder.position = new Vector3(waist.x, waist.y, player.transform.position.z);
        }
        placeholder.localRotation = Quaternion.identity;
        placeholder.localScale = Vector3.one;

        foreach (ParticleSystem legacy in placeholder.GetComponents<ParticleSystem>())
        {
            Undo.RecordObject(legacy, "Replace placeholder emission");
            legacy.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = legacy.main;
            main.playOnAwake = false;
            var emission = legacy.emission;
            emission.enabled = false;
            ParticleSystemRenderer renderer = legacy.GetComponent<ParticleSystemRenderer>();
            Undo.RecordObject(renderer, "Replace placeholder renderer");
            renderer.enabled = false;
        }

        Transform oldTrail = placeholder.Find("GracefulTrail");
        if (oldTrail != null) Undo.DestroyObjectImmediate(oldTrail.gameObject);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, placeholder);
        Undo.RegisterCreatedObjectUndo(instance, "Install graceful trail");
        instance.name = "GracefulTrail";
        var controller = new SerializedObject(instance.GetComponent<PlayerMovementTrail>());
        controller.FindProperty("movementTarget").objectReferenceValue = player.transform;
        controller.FindProperty("gracefulEffects").objectReferenceValue = player.GetComponent<PlayerGracefulActionEffectManager>();
        controller.ApplyModifiedPropertiesWithoutUndo();

        foreach (ParticleSystemRenderer renderer in instance.GetComponentsInChildren<ParticleSystemRenderer>())
        {
            renderer.sortingLayerID = body != null ? body.sortingLayerID : 0;
            renderer.sortingOrder = (body != null ? body.sortingOrder : 2) + 1;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }

        PrefabUtility.RecordPrefabInstancePropertyModifications(placeholder);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = placeholder.gameObject;
        Debug.Log($"Graceful trail installed: {scene.path}, player={player.transform.position}, " +
            $"scale={player.transform.lossyScale}, FX_Trail={placeholder.localPosition}, body={body?.bounds}");
    }

    [MenuItem("Tools/Effects/Rebuild Graceful Trail Prefab")]
    public static void RebuildPrefab()
    {
        BuildPrefab();
    }

    public static GameObject BuildPrefab()
    {
        Directory.CreateDirectory(ArtPath);
        AssetDatabase.Refresh();
        Texture2D spectrum = CreateSpectrum();
        Texture2D pearl = CreateMask("Pearl", 128, 128, (x, y) =>
            Mathf.Clamp01(Mathf.Exp(-(x * x + y * y) * 24f) * 0.8f +
                Mathf.Max(0f, Mathf.Exp(-(x * x + y * y) * 6f) - Mathf.Exp(-6f)) * 0.2f));
        Texture2D glow = CreateMask("Glow", 128, 128, (x, y) =>
            Mathf.Clamp01(Mathf.Exp(-(x * x + y * y) * 8f) - Mathf.Exp(-8f)));
        Texture2D star = CreateMask("Star", 256, 256, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float core = Mathf.Exp(-r * r * 35f);
            float cross = Mathf.Exp(-Mathf.Abs(x) * 35f - Mathf.Abs(y) * 2.5f) +
                Mathf.Exp(-Mathf.Abs(y) * 35f - Mathf.Abs(x) * 2.5f);
            return Mathf.Clamp01((core + cross + Mathf.Exp(-r * r * 6f) * 0.18f) *
                Mathf.SmoothStep(0f, 1f, (1f - r) / 0.18f));
        });
        Texture2D petal = ImportTexture(ArtPath + "/PetalBroad.png", 256);
        Material ribbonMaterial = CreateMaterial("IridescentRibbon", spectrum, 1.1f, "GameName/FX/Graceful Ribbon");
        Material moteMaterial = CreateMaterial("Pearl", pearl, 1.25f);
        Material glowMaterial = CreateMaterial("Glow", glow, 1f);
        Material starMaterial = CreateMaterial("Star", star, 1.5f);
        Material petalMaterial = CreateMaterial("Petal", petal, 1.6f);
        petalMaterial.SetFloat("_Halo", 0.45f);
        EditorUtility.SetDirty(petalMaterial);
        AssetDatabase.SaveAssetIfDirty(petalMaterial);
        float height = ReferenceHeight();
        // Install already places FX_Trail at the waist. Layers inside the prefab must stay on
        // that origin; offsetting by the waist a second time drops the trail to the ankles,
        // and the player's scale multiplies the mistake.
        const float lift = 0f;

        GameObject root = new GameObject("FX_GracefulMovementTrail");
        try
        {
            // Levels one and two draw the same two lines. The extra lines arrive at level three
            // and four, and their short fade-in spans keep them out of the climb towards it.
            CreateRibbon(root.transform, "Ribbon_Main", ribbonMaterial, height * 0.08f, height, 0f, lift + 0.1f,
                0f, 1f, 0f, 1f, 0f, false);
            CreateRibbon(root.transform, "Ribbon_Accent", ribbonMaterial, height * 0.045f, height, 2.6f, lift - 0.13f,
                0f, 1f, 0.07f, 0.75f, 0.18f, true);
            CreateRibbon(root.transform, "Ribbon_Veil", ribbonMaterial, height * 0.065f, height, 4.2f, lift + 0.05f,
                2.8f, 0.2f, 0.16f, 0.72f, 0.36f, false);
            CreateRibbon(root.transform, "Ribbon_Flourish", ribbonMaterial, height * 0.055f, height, 1.35f, lift + 0.22f,
                2.85f, 0.15f, 0.24f, 0.95f, 0.55f, false);
            CreateRibbon(root.transform, "Ribbon_Halo", ribbonMaterial, height * 0.095f, height, 5.3f, lift - 0.24f,
                3.5f, 0.5f, 0.1f, 1.15f, 0.73f, false);
            ParticleSystem motes = CreateParticles(root.transform, "LightMotes", moteMaterial,
                0.65f, 1.1f, height * 0.06f, height * 0.11f, 1.5f, 1.4f, height * 0.13f);
            SetPalette(motes, 0.65f);
            AddDrift(motes, 0.05f, 0.25f, 0.18f);

            ParticleSystem stars = CreateParticles(root.transform, "StarGlints", starMaterial,
                0.55f, 1f, height * 0.11f, height * 0.18f, 0.5f, 0.45f, height * 0.16f);
            SetPalette(stars, 0.75f);
            AddDrift(stars, 0.08f, 0.32f, 0.1f);
            var starRotation = stars.main;
            starRotation.startRotation = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            ParticleSystem petals = CreateParticles(root.transform, "Petals", petalMaterial,
                1f, 1.6f, 0.42f, 0.72f, 0.35f, 0.42f, 0.6f);
            SetPalette(petals, 0.75f);
            AddDrift(petals, 0.12f, 0.35f, 0.2f);
            var petalMain = petals.main;
            petalMain.startRotation3D = true;
            petalMain.startRotationX = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            petalMain.startRotationY = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            petalMain.startRotationZ = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            var rotation = petals.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(-0.18f, 0.18f);
            rotation.y = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            rotation.z = new ParticleSystem.MinMaxCurve(-0.65f, 0.65f);

            ParticleSystem halos = CreateParticles(root.transform, "SoftGlow", glowMaterial,
                0.9f, 1.35f, height * 0.25f, height * 0.4f, 0.4f, 0.35f, height * 0.17f);
            SetPalette(halos, 0.07f);
            AddDrift(halos, 0.14f, 0.3f, 0.12f);

            ParticleSystem dust = CreateParticles(root.transform, "FineSparkles", moteMaterial,
                0.4f, 0.85f, height * 0.02f, height * 0.04f, 8f, 5f, height * 0.16f);
            SetPalette(dust, 0.85f);
            AddDrift(dust, 0.05f, 0.28f, 0.12f);
            foreach (ParticleSystem layer in new[] { motes, stars, petals, halos, dust })
                layer.transform.localPosition = Vector3.up * lift;

            PlayerMovementTrail trail = root.AddComponent<PlayerMovementTrail>();
            var settings = new SerializedObject(trail);
            SetReferences(settings.FindProperty("particles"), new[] { motes, stars, petals, halos, dust });
            settings.ApplyModifiedPropertiesWithoutUndo();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return prefab;
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void CreateRibbon(Transform parent, string name, Material material,
        float width, float height, float phase, float offset,
        float minimumLevel, float fadeInSpan, float burstDelay, float burstSpan, float phaseFraction, bool requiresDoubleBurst)
    {
        ParticleSystem system = CreateRibbonSystem(parent, name, material);
        system.transform.localPosition = new Vector3(0f, offset, 0f);
        ParticleSystem fadeA = CreateRibbonSystem(system.transform, "Fading_A", material);
        ParticleSystem fadeB = CreateRibbonSystem(system.transform, "Fading_B", material);
        foreach (ParticleSystem fading in new[] { fadeA, fadeB })
        {
            var color = fading.colorOverLifetime;
            color.enabled = true;
            color.color = Gradient(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.22f),
                    new GradientAlphaKey(0.3f, 0.6f), new GradientAlphaKey(0f, 1f) });
        }
        var driver = new SerializedObject(system.gameObject.AddComponent<GracefulParticleRibbon>());
        driver.FindProperty("ribbon").objectReferenceValue = system;
        SetReferences(driver.FindProperty("fadingRibbons"), new[] { fadeA, fadeB });
        driver.FindProperty("width").floatValue = width;
        driver.FindProperty("maximumLength").floatValue = height * 1.7f;
        driver.FindProperty("sway").floatValue = height * 0.06f;
        driver.FindProperty("phase").floatValue = phase;
        driver.FindProperty("opacity").floatValue = phase == 0f ? 0.48f : 0.34f;
        driver.FindProperty("fadeSeconds").floatValue = phase == 0f ? 0.46f : 0.34f;
        driver.FindProperty("minimumLevel").floatValue = minimumLevel;
        driver.FindProperty("fadeInSpan").floatValue = fadeInSpan;
        driver.FindProperty("burstDelay").floatValue = burstDelay;
        driver.FindProperty("burstSpan").floatValue = burstSpan;
        driver.FindProperty("phaseFraction").floatValue = phaseFraction;
        driver.FindProperty("requiresDoubleBurst").boolValue = requiresDoubleBurst;
        driver.ApplyModifiedPropertiesWithoutUndo();
    }

    private static ParticleSystem CreateRibbonSystem(Transform parent, string name, Material material)
    {
        ParticleSystem system = CreateParticles(parent, name, material, 100f, 100f, 1f, 1f, 0f, 0f, 0f);
        var main = system.main;
        main.startColor = Color.white;
        main.maxParticles = 160;
        var emission = system.emission;
        emission.enabled = false;
        var size = system.sizeOverLifetime;
        size.enabled = false;
        var colorModule = system.colorOverLifetime;
        colorModule.enabled = false;
        var trails = system.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.Ribbon;
        trails.ribbonCount = 1;
        trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
        trails.sizeAffectsWidth = true;
        trails.inheritParticleColor = true;
        trails.widthOverTrail = 1f;
        trails.colorOverLifetime = Color.white;
        trails.colorOverTrail = Color.white;
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.None;
        renderer.trailMaterial = material;
        return system;
    }

    private static ParticleSystem CreateParticles(Transform parent, string name, Material material,
        float lifeMin, float lifeMax, float sizeMin, float sizeMax, float rateTime, float rateDistance, float radius)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        var system = root.AddComponent<ParticleSystem>();
        system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.duration = 2f;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        // Level four multiplies the birth rates; a low cap would silently swallow the flourish.
        main.maxParticles = 320;
        main.stopAction = ParticleSystemStopAction.None;
        var emission = system.emission;
        emission.rateOverTime = rateTime;
        emission.rateOverDistance = rateDistance;
        var shape = system.shape;
        shape.enabled = radius > 0f;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 1f;
        var color = system.colorOverLifetime;
        color.enabled = true;
        color.color = Gradient(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(0.75f, 0.5f), new GradientAlphaKey(0.2f, 0.8f), new GradientAlphaKey(0f, 1f) });
        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.7f), new Keyframe(0.16f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0.75f)));
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 3;
        renderer.maxParticleSize = 0.5f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return system;
    }

    private static void AddDrift(ParticleSystem system, float minRise, float maxRise, float noiseStrength)
    {
        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        velocity.y = new ParticleSystem.MinMaxCurve(minRise, maxRise);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var noise = system.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = noiseStrength;
        noise.strengthY = noiseStrength;
        noise.strengthZ = 0f;
        noise.frequency = 0.7f;
        noise.scrollSpeed = 0.3f;
        noise.quality = ParticleSystemNoiseQuality.Medium;
        noise.damping = true;
    }

    private static void SetPalette(ParticleSystem system, float alpha = 1f)
    {
        var main = system.main;
        Gradient spectrum = PastelSpectrum(alpha);
        main.startColor = new ParticleSystem.MinMaxGradient(spectrum) { mode = ParticleSystemGradientMode.RandomColor };
    }

    private static float ReferenceHeight()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (scene.isLoaded)
        {
            var player = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<PlayerController>(true)).FirstOrDefault();
            var body = player != null ? player.transform.Find("SpriteView")?.GetComponent<SpriteRenderer>() : null;
            if (body != null && body.bounds.size.y > 0.1f) return body.bounds.size.y;
        }
        return 5.5f;
    }

    private static Gradient PastelSpectrum(float alpha = 1f) => GracefulPalette.CreateSpectrum(alpha);

    private static Texture2D CreateSpectrum()
    {
        const string path = ArtPath + "/PastelSpectrum.png";
        var texture = new Texture2D(256, 4, TextureFormat.RGBA32, false);
        Gradient spectrum = PastelSpectrum();
        var pixels = new Color[256 * 4];
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 256; x++) pixels[y * 256 + x] = spectrum.Evaluate(x / 255f);
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        return ImportTexture(path, 256);
    }

    private static Gradient Gradient(GradientColorKey[] colors, GradientAlphaKey[] alphas)
    {
        var gradient = new Gradient();
        gradient.SetKeys(colors, alphas);
        return gradient;
    }

    private static void SetReferences<T>(SerializedProperty property, T[] values) where T : UnityEngine.Object
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static Texture2D CreateMask(string name, int width, int height, Func<float, float, float> mask)
    {
        string path = ArtPath + "/" + name + ".png";
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var colors = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                colors[y * width + x] = new Color(1f, 1f, 1f,
                    mask(x / (width - 1f) * 2f - 1f, y / (height - 1f) * 2f - 1f));
        texture.SetPixels(colors);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        return ImportTexture(path, 256);
    }

    private static Texture2D ImportTexture(string path, int maxSize)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = maxSize;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material CreateMaterial(string name, Texture2D texture, float brightness, string shaderName = "GameName/FX/Graceful Particle")
    {
        string path = ArtPath + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(shaderName);
        if (shader == null) throw new InvalidOperationException("Missing trail shader: " + shaderName);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", Color.white);
        material.SetFloat("_Brightness", brightness);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }
}
