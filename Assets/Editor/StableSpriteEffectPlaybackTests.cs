using System.Collections;
using System.Reflection;
using GameName.Enemy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class StableSpriteEffectPlaybackTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(5120, 5120, 100f, 250f)]
    [TestCase(5120, 6144, 100f, 300f)]
    [TestCase(1024, 1024, 100f, 100f)]
    public void FullResolutionSheets_KeepThe2048ImportWorldSize(
        int textureWidth,
        int textureHeight,
        float basePixelsPerUnit,
        float expectedPixelsPerUnit)
    {
        float result = SpriteSheetResolutionUtility.GetSizeCompensatedPixelsPerUnit(
            textureWidth,
            textureHeight,
            basePixelsPerUnit);

        Assert.That(result, Is.EqualTo(expectedPixelsPerUnit).Within(0.001f));
    }

    [Test]
    public void StageBoss_AloneUsesLastBossMatchedDeathEffectScale()
    {
        float expectedStageBossScale = ResolveLastBossMatchedSharedEffectScale();
        AssertDeathEffectScale(
            "Assets/Prefabs/Enemies/StageBoss.prefab",
            expectedOverride: true,
            expectedScale: expectedStageBossScale);
        AssertDeathEffectScale(
            "Assets/Prefabs/Enemies/Enemy_Tackle.prefab",
            expectedOverride: false,
            expectedScale: 1f);
        AssertDeathEffectScale(
            "Assets/Prefabs/Enemies/Enemy_Ranged.prefab",
            expectedOverride: false,
            expectedScale: 1f);
    }

    [Test]
    public void EnemyDestroyEffect_FrameChangesKeepRendererAnchorLocked()
    {
        GameObject root = new GameObject("EnemyDestroyEffectTest");
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0.35f, -0.2f, 0f);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        EnemyDestroyEffectPlayer player = root.AddComponent<EnemyDestroyEffectPlayer>();
        Texture2D texture = CreateTexture();
        Sprite firstFrame = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.zero, 100f);
        Sprite secondFrame = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.one, 100f);

        try
        {
            SetField(player, "targetRenderer", renderer);
            SetField(player, "spriteSheetTexture", null);
            SetField(player, "frames", new[] { firstFrame, secondFrame });
            SetField(player, "destroyOnComplete", false);
            SetField(player, "centerFramesOnOrigin", true);

            Vector3 expectedPosition = visual.transform.localPosition;
            IEnumerator routine = InvokeRoutine(player, "PlayRoutine");

            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(visual.transform.localPosition, Is.EqualTo(expectedPosition));
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(visual.transform.localPosition, Is.EqualTo(expectedPosition));
        }
        finally
        {
            Object.DestroyImmediate(firstFrame);
            Object.DestroyImmediate(secondFrame);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ItemEffect_FrameChangesKeepConfiguredVisualAnchorLocked()
    {
        GameObject root = new GameObject("ItemEffectTest");
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        ItemEffectController controller = root.AddComponent<ItemEffectController>();
        ItemEffectSettings settings = ScriptableObject.CreateInstance<ItemEffectSettings>();
        settings.visualOffset = new Vector3(0.4f, 1.25f, 0f);
        Texture2D texture = CreateTexture();
        Sprite firstFrame = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.zero, 100f);
        Sprite secondFrame = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.one, 100f);

        try
        {
            SetField(controller, "settings", settings);
            SetField(controller, "effectTransform", visual.transform);
            SetField(controller, "effectRenderer", renderer);

            Vector3[] changingLegacyOffsets =
            {
                new Vector3(-2f, 3f, 0f),
                new Vector3(4f, -5f, 0f)
            };
            MethodInfo method = typeof(ItemEffectController).GetMethod("PlayFrames", InstancePrivate);
            Assert.That(method, Is.Not.Null);
            IEnumerator routine = (IEnumerator)method.Invoke(
                controller,
                new object[] { new[] { firstFrame, secondFrame }, changingLegacyOffsets, 0.01f, null, null });

            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(visual.transform.localPosition, Is.EqualTo(settings.visualOffset));
            Assert.That(routine.MoveNext(), Is.True);
            Assert.That(visual.transform.localPosition, Is.EqualTo(settings.visualOffset));
        }
        finally
        {
            Object.DestroyImmediate(firstFrame);
            Object.DestroyImmediate(secondFrame);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(settings);
            Object.DestroyImmediate(root);
        }
    }

    private static Texture2D CreateTexture()
    {
        Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    private static IEnumerator InvokeRoutine(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstancePrivate);
        Assert.That(method, Is.Not.Null);
        return (IEnumerator)method.Invoke(target, null);
    }

    private static float ResolveLastBossMatchedSharedEffectScale()
    {
        GameObject lastBossContents = null;
        GameObject sharedEffectContents = null;

        try
        {
            lastBossContents = PrefabUtility.LoadPrefabContents(
                "Assets/Prefabs/Enemies/LastBoss.prefab");
            Collider2D lastBossCollider = lastBossContents.GetComponent<Collider2D>();
            Assert.That(lastBossCollider, Is.Not.Null);
            lastBossCollider.enabled = false;

            LastBossEffectController lastBossEffects =
                lastBossContents.GetComponent<LastBossEffectController>();
            Assert.That(lastBossEffects, Is.Not.Null);
            float deathSizeMultiplier = GetField<float>(lastBossEffects, "deathSizeMultiplier");
            MethodInfo resolveBossSquareSize = typeof(LastBossEffectController).GetMethod(
                "ResolveBossSquareSize",
                InstancePrivate);
            Assert.That(resolveBossSquareSize, Is.Not.Null);
            Vector2 lastBossDeathWorldSize = (Vector2)resolveBossSquareSize.Invoke(
                lastBossEffects,
                new object[] { deathSizeMultiplier });

            sharedEffectContents = PrefabUtility.LoadPrefabContents(
                "Assets/Prefabs/Effects/EnemyDestroyEffect.prefab");
            EnemyDestroyEffectPlayer sharedEffectPlayer =
                sharedEffectContents.GetComponent<EnemyDestroyEffectPlayer>();
            Assert.That(sharedEffectPlayer, Is.Not.Null);

            Texture2D spriteSheet = GetField<Texture2D>(sharedEffectPlayer, "spriteSheetTexture");
            int frameColumns = GetField<int>(sharedEffectPlayer, "frameColumns");
            float basePixelsPerUnit = GetField<float>(sharedEffectPlayer, "pixelsPerUnit");
            SpriteRenderer effectRenderer = GetField<SpriteRenderer>(sharedEffectPlayer, "targetRenderer");
            Assert.That(spriteSheet, Is.Not.Null);
            Assert.That(effectRenderer, Is.Not.Null);

            float compensatedPixelsPerUnit =
                SpriteSheetResolutionUtility.GetSizeCompensatedPixelsPerUnit(
                    spriteSheet,
                    basePixelsPerUnit);
            float sharedEffectFrameWorldWidth =
                (spriteSheet.width / frameColumns) /
                compensatedPixelsPerUnit *
                Mathf.Abs(effectRenderer.transform.lossyScale.x);

            Assert.That(sharedEffectFrameWorldWidth, Is.GreaterThan(0f));
            return lastBossDeathWorldSize.x / sharedEffectFrameWorldWidth;
        }
        finally
        {
            if (sharedEffectContents != null)
            {
                PrefabUtility.UnloadPrefabContents(sharedEffectContents);
            }

            if (lastBossContents != null)
            {
                PrefabUtility.UnloadPrefabContents(lastBossContents);
            }
        }
    }

    private static void AssertDeathEffectScale(
        string prefabPath,
        bool expectedOverride,
        float expectedScale)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null, prefabPath);

        EnemyDeathEffectSpawner spawner = prefab.GetComponent<EnemyDeathEffectSpawner>();
        Assert.That(spawner, Is.Not.Null, prefabPath);
        Assert.That(
            GetField<bool>(spawner, "overrideDeathEffectScale"),
            Is.EqualTo(expectedOverride),
            prefabPath);
        Assert.That(
            GetField<float>(spawner, "deathEffectScaleMultiplier"),
            Is.EqualTo(expectedScale).Within(0.000001f),
            prefabPath);
    }

    private static T GetField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
