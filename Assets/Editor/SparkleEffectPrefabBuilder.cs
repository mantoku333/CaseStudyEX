using System.IO;
using UnityEditor;
using UnityEngine;

public static class SparkleEffectPrefabBuilder
{
    private const string EffectFolderPath = "Assets/Prefabs/Effects";
    private const string MaterialFolderPath = "Assets/Materials";
    private const string TextureFolderPath = "Assets/Art/Sprites/Effects";
    private const string PrefabPath = EffectFolderPath + "/FX_Sparkle_Hit.prefab";
    private const string MaterialPath = MaterialFolderPath + "/FX_Sparkle_Additive.mat";
    private const string TexturePath = TextureFolderPath + "/Tex_StarParticle.png";
    private const string SparkleShaderName = "GameName/FX/Sparkle Particle";

    [MenuItem("Tools/Effects/Create Star Particle Texture")]
    public static void CreateStarParticleTexture()
    {
        Texture2D texture = GenerateStarTexture(128);
        byte[] pngBytes = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);

        EnsureFolder(TextureFolderPath);
        File.WriteAllBytes(TexturePath, pngBytes);
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
        ConfigureStarTextureImporter(TexturePath);

        Material material = LoadOrCreateSparkleMaterial();
        Texture2D importedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        material.SetTexture("_MainTex", importedTexture);
        EditorUtility.SetDirty(material);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = importedTexture;

        Debug.Log("Created star particle texture: " + TexturePath);
    }

    [MenuItem("Tools/Effects/Create Sparkle Hit Prefab")]
    public static void CreateSparkleHitPrefab()
    {
        EnsureFolder(EffectFolderPath);
        EnsureFolder(MaterialFolderPath);
        CreateStarParticleTexture();

        Material sparkleMaterial = LoadOrCreateSparkleMaterial();
        GameObject root = new GameObject("FX_Sparkle_Hit");

        ParticleSystem burst = root.AddComponent<ParticleSystem>();
        SetupBurstSparkles(burst, sparkleMaterial);

        GameObject dustObject = new GameObject("FloatingDust");
        dustObject.transform.SetParent(root.transform, false);
        ParticleSystem dust = dustObject.AddComponent<ParticleSystem>();
        SetupFloatingDust(dust, sparkleMaterial);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        Debug.Log("Created sparkle hit effect prefab: " + PrefabPath);
    }

    private static void SetupBurstSparkles(ParticleSystem particleSystem, Material material)
    {
        ParticleSystem.MainModule main = particleSystem.main;
        main.duration = 0.55f;
        main.loop = false;
        main.startDelay = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.94f, 0.55f, 1f),
            new Color(0.75f, 0.95f, 1f, 1f));
        main.gravityModifier = -0.12f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(16f, 24f)),
        });

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.22f;
        shape.radiusThickness = 0.7f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = BuildFadeGradient(
            new Color(1f, 0.98f, 0.78f, 0f),
            new Color(1f, 0.96f, 0.62f, 1f),
            new Color(0.7f, 0.95f, 1f, 0f));

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.14f, 1f),
            new Keyframe(0.72f, 0.55f),
            new Keyframe(1f, 0f)));

        ParticleSystem.RotationOverLifetimeModule rotationOverLifetime = particleSystem.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-4.5f, 4.5f);

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 25;
        renderer.sharedMaterial = material;
    }

    private static void SetupFloatingDust(ParticleSystem particleSystem, Material material)
    {
        ParticleSystem.MainModule main = particleSystem.main;
        main.duration = 0.65f;
        main.loop = false;
        main.startDelay = 0.04f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.75f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 1f, 0.8f),
            new Color(0.9f, 0.92f, 1f, 0.65f));
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, new ParticleSystem.MinMaxCurve(10f, 16f)),
        });

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.35f;
        shape.radiusThickness = 0.25f;

        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = particleSystem.velocityOverLifetime;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocityOverLifetime.orbitalZ = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        velocityOverLifetime.enabled = true;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = BuildFadeGradient(
            new Color(1f, 1f, 1f, 0f),
            new Color(0.92f, 0.96f, 1f, 0.7f),
            new Color(0.85f, 0.92f, 1f, 0f));

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.2f, 1f),
            new Keyframe(1f, 0f)));

        ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 24;
        renderer.sharedMaterial = material;
    }

    private static ParticleSystem.MinMaxGradient BuildFadeGradient(Color start, Color middle, Color end)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(start, 0f),
                new GradientColorKey(middle, 0.25f),
                new GradientColorKey(end, 1f),
            },
            new[]
            {
                new GradientAlphaKey(start.a, 0f),
                new GradientAlphaKey(middle.a, 0.18f),
                new GradientAlphaKey(end.a, 1f),
            });

        return new ParticleSystem.MinMaxGradient(gradient);
    }

    private static Material LoadOrCreateSparkleMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null)
        {
            ConfigureSparkleMaterial(material);
            return material;
        }

        Shader shader = Shader.Find(SparkleShaderName);
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        material = new Material(shader)
        {
            name = "FX_Sparkle_Additive",
        };

        ConfigureSparkleMaterial(material);
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    private static void ConfigureSparkleMaterial(Material material)
    {
        Shader sparkleShader = Shader.Find(SparkleShaderName);
        if (sparkleShader != null)
        {
            material.shader = sparkleShader;
        }

        material.SetColor("_TintColor", Color.white);
        material.SetFloat("_Brightness", 1.4f);

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture != null)
        {
            material.SetTexture("_MainTex", texture);
        }

        EditorUtility.SetDirty(material);
    }

    private static Texture2D GenerateStarTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Tex_StarParticle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float halfSize = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 normalized = (new Vector2(x, y) - center) / halfSize;
                float xAbs = Mathf.Abs(normalized.x);
                float yAbs = Mathf.Abs(normalized.y);
                float astroid = Mathf.Pow(xAbs / 0.78f, 0.62f) + Mathf.Pow(yAbs / 1.02f, 0.62f);
                float silhouette = Saturate((1.05f - astroid) / 0.28f);
                float alpha = Mathf.SmoothStep(0f, 1f, silhouette);
                float centerGlow = Mathf.Pow(Saturate(1f - normalized.magnitude * 1.65f), 1.55f);
                float brightness = Mathf.Lerp(0.82f, 1f, centerGlow);

                Color color = new Color(brightness, brightness, brightness, alpha);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return texture;
    }

    private static void ConfigureStarTextureImporter(string texturePath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static float Saturate(float value)
    {
        return Mathf.Clamp01(value);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        string folderName = Path.GetFileName(folderPath);

        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
