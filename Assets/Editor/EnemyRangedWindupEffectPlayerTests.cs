using System;
using System.Reflection;
using GameName.Enemy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class EnemyRangedWindupEffectPlayerTests
{
    private GameObject enemyObject;
    private EnemyController enemyController;
    private EnemyRangedWindupEffectPlayer effectPlayer;
    private Texture2D testTexture;

    [SetUp]
    public void SetUp()
    {
        enemyObject = new GameObject("EnemyRangedWindupEffectTest");
        enemyController = enemyObject.AddComponent<EnemyController>();
        enemyObject.AddComponent<EnemyRangedAttack>();
        effectPlayer = enemyObject.AddComponent<EnemyRangedWindupEffectPlayer>();
        testTexture = new Texture2D(60, 60, TextureFormat.RGBA32, false);
    }

    [TearDown]
    public void TearDown()
    {
        if (enemyObject != null)
        {
            UnityEngine.Object.DestroyImmediate(enemyObject);
        }

        if (testTexture != null)
        {
            UnityEngine.Object.DestroyImmediate(testTexture);
        }
    }

    [Test]
    public void TimingSheet_BuildsFiveBySixGridCappedAtTwentyEightFrames()
    {
        SetPrivateField(effectPlayer, "attackTimingSpriteSheet", testTexture);
        SetPrivateField(effectPlayer, "timingFrameColumns", 5);
        SetPrivateField(effectPlayer, "timingFrameRows", 6);
        SetPrivateField(effectPlayer, "timingFrameCount", 28);
        SetPrivateField(effectPlayer, "timingPixelsPerUnit", 10f);

        InvokePrivate(effectPlayer, "BuildFramesIfNeeded");

        Sprite[] timingFrames = GetPrivateField<Sprite[]>(effectPlayer, "timingFrames");
        Assert.That(timingFrames, Has.Length.EqualTo(28));
        Assert.That(timingFrames[0].rect, Is.EqualTo(new Rect(0f, 50f, 12f, 10f)));
        Assert.That(timingFrames[27].rect, Is.EqualTo(new Rect(24f, 0f, 12f, 10f)));
    }

    [Test]
    public void SharedClock_HidesTimingEarlyAndAlignsBothFinalFrames()
    {
        Sprite[] chargeFrames = CreateFrames(60);
        Sprite[] timingFrames = CreateFrames(28);
        SpriteRenderer chargeRenderer = new GameObject("ChargeRenderer").AddComponent<SpriteRenderer>();
        SpriteRenderer timingRenderer = new GameObject("TimingRenderer").AddComponent<SpriteRenderer>();
        chargeRenderer.transform.SetParent(enemyObject.transform);
        timingRenderer.transform.SetParent(enemyObject.transform);

        SetPrivateField(effectPlayer, "frames", chargeFrames);
        SetPrivateField(effectPlayer, "timingFrames", timingFrames);
        SetPrivateField(effectPlayer, "effectRenderer", chargeRenderer);
        SetPrivateField(effectPlayer, "timingEffectRenderer", timingRenderer);
        SetPrivateField(effectPlayer, "playbackDuration", 2f);

        float frameSeconds = 2f / 60f;
        SetPrivateField(effectPlayer, "playbackElapsed", frameSeconds * 31.99f);
        InvokePrivate(effectPlayer, "ApplyVisualFrames");
        Assert.That(timingRenderer.enabled, Is.False);

        SetPrivateField(effectPlayer, "playbackElapsed", frameSeconds * 32f);
        InvokePrivate(effectPlayer, "ApplyVisualFrames");
        Assert.That(chargeRenderer.sprite, Is.SameAs(chargeFrames[32]));
        Assert.That(timingRenderer.sprite, Is.SameAs(timingFrames[0]));
        Assert.That(timingRenderer.enabled, Is.True);

        SetPrivateField(effectPlayer, "playbackElapsed", frameSeconds * 59f);
        InvokePrivate(effectPlayer, "ApplyVisualFrames");
        Assert.That(chargeRenderer.sprite, Is.SameAs(chargeFrames[59]));
        Assert.That(timingRenderer.sprite, Is.SameAs(timingFrames[27]));

        DestroyFrames(chargeFrames);
        DestroyFrames(timingFrames);
    }

    [Test]
    public void SharedClock_DoesNotAdvanceWhilePaused()
    {
        Sprite[] chargeFrames = CreateFrames(60);
        SpriteRenderer chargeRenderer = new GameObject("ChargeRenderer").AddComponent<SpriteRenderer>();
        chargeRenderer.transform.SetParent(enemyObject.transform);

        SetPrivateField(effectPlayer, "frames", chargeFrames);
        SetPrivateField(effectPlayer, "effectRenderer", chargeRenderer);
        SetPrivateField(effectPlayer, "playbackDuration", 2f);
        SetPrivateField(effectPlayer, "playbackElapsed", 0.5f);

        InvokePrivate(effectPlayer, "AdvancePlayback", 0.25f, true);
        Assert.That(GetPrivateField<float>(effectPlayer, "playbackElapsed"), Is.EqualTo(0.5f));

        InvokePrivate(effectPlayer, "AdvancePlayback", 0.25f, false);
        Assert.That(GetPrivateField<float>(effectPlayer, "playbackElapsed"), Is.EqualTo(0.75f));

        DestroyFrames(chargeFrames);
    }

    [Test]
    public void TimingEffect_CentersAndFitsEnemyRenderer_ThenCleansUp()
    {
        GameObject visualObject = new GameObject("EnemyVisual");
        visualObject.transform.SetParent(enemyObject.transform);
        visualObject.transform.position = new Vector3(2f, 3f, 0f);
        SpriteRenderer sourceRenderer = visualObject.AddComponent<SpriteRenderer>();
        sourceRenderer.sprite = Sprite.Create(testTexture, new Rect(0f, 0f, 40f, 20f), new Vector2(0.5f, 0.5f), 10f);

        Sprite[] timingFrames = CreateFrames(28, 60f, 10f);
        SetPrivateField(effectPlayer, "timingFrames", timingFrames);
        SetPrivateField(effectPlayer, "timingFitScaleMultiplier", 1f);
        InvokePrivate(effectPlayer, "CreateTimingEffect", sourceRenderer);

        SpriteRenderer timingRenderer = GetPrivateField<SpriteRenderer>(effectPlayer, "timingEffectRenderer");
        Assert.That(timingRenderer, Is.Not.Null);
        Assert.That(timingRenderer.bounds.center.x, Is.EqualTo(sourceRenderer.bounds.center.x).Within(0.001f));
        Assert.That(timingRenderer.bounds.center.y, Is.EqualTo(sourceRenderer.bounds.center.y).Within(0.001f));
        Assert.That(timingRenderer.bounds.size.x, Is.EqualTo(sourceRenderer.bounds.size.x).Within(0.001f));
        Assert.That(timingRenderer.sortingOrder, Is.EqualTo(sourceRenderer.sortingOrder + 4));

        InvokePrivate(effectPlayer, "StopAndDestroyEffects");
        Assert.That(GetPrivateField<GameObject>(effectPlayer, "timingEffectObject"), Is.Null);
        Assert.That(GetPrivateField<SpriteRenderer>(effectPlayer, "timingEffectRenderer"), Is.Null);

        UnityEngine.Object.DestroyImmediate(sourceRenderer.sprite);
        DestroyFrames(timingFrames);
    }

    [Test]
    public void TimingForwardOffset_FollowsEnemyFacingDirection()
    {
        GameObject visualObject = new GameObject("EnemyVisual");
        visualObject.transform.SetParent(enemyObject.transform);
        visualObject.transform.position = new Vector3(3f, 2f, 0f);
        SpriteRenderer sourceRenderer = visualObject.AddComponent<SpriteRenderer>();
        sourceRenderer.sprite = Sprite.Create(testTexture, new Rect(0f, 0f, 20f, 20f), new Vector2(0.5f, 0.5f), 10f);

        SetPrivateField(effectPlayer, "timingLocalOffset", new Vector3(0f, 0.5f, 0f));
        SetPrivateField(effectPlayer, "timingForwardOffset", 1.25f);
        InvokePrivate(effectPlayer, "CacheComponents");

        enemyController.FaceDirection(1);
        Vector3 rightPosition = (Vector3)InvokePrivate(effectPlayer, "ResolveTimingWorldPosition", sourceRenderer);
        enemyController.FaceDirection(-1);
        Vector3 leftPosition = (Vector3)InvokePrivate(effectPlayer, "ResolveTimingWorldPosition", sourceRenderer);

        Assert.That(rightPosition.x, Is.EqualTo(sourceRenderer.bounds.center.x + 1.25f).Within(0.001f));
        Assert.That(leftPosition.x, Is.EqualTo(sourceRenderer.bounds.center.x - 1.25f).Within(0.001f));
        Assert.That(rightPosition.y, Is.EqualTo(sourceRenderer.bounds.center.y + 0.5f).Within(0.001f));
        Assert.That(leftPosition.y, Is.EqualTo(rightPosition.y).Within(0.001f));

        UnityEngine.Object.DestroyImmediate(sourceRenderer.sprite);
    }

    [Test]
    public void EnemyRangedPrefab_AssignsAttackTimingSheetAndGrid()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Enemy_Ranged.prefab");
        Assert.That(prefab, Is.Not.Null);

        EnemyRangedWindupEffectPlayer prefabPlayer = prefab.GetComponent<EnemyRangedWindupEffectPlayer>();
        Assert.That(prefabPlayer, Is.Not.Null);

        SerializedObject serializedPlayer = new SerializedObject(prefabPlayer);
        Texture2D timingSheet = serializedPlayer.FindProperty("attackTimingSpriteSheet").objectReferenceValue as Texture2D;
        Assert.That(timingSheet, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(timingSheet), Is.EqualTo("Assets/Art/Sprites/Effects/eff_attack_timing.png"));
        Assert.That(serializedPlayer.FindProperty("timingFrameColumns").intValue, Is.EqualTo(5));
        Assert.That(serializedPlayer.FindProperty("timingFrameRows").intValue, Is.EqualTo(6));
        Assert.That(serializedPlayer.FindProperty("timingFrameCount").intValue, Is.EqualTo(28));
    }

    private Sprite[] CreateFrames(int count, float width = 1f, float pixelsPerUnit = 1f)
    {
        Sprite[] sprites = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            sprites[i] = Sprite.Create(
                testTexture,
                new Rect(0f, 0f, width, 1f),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit);
        }

        return sprites;
    }

    private static void DestroyFrames(Sprite[] sprites)
    {
        foreach (Sprite sprite in sprites)
        {
            if (sprite != null)
            {
                UnityEngine.Object.DestroyImmediate(sprite);
            }
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
        return (T)field.GetValue(target);
    }

    private static object InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Missing method {methodName}");
        return method.Invoke(target, arguments);
    }
}
