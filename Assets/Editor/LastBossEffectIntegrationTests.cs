using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GameName.Enemy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class LastBossEffectIntegrationTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<Object> objectsToDestroy = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        DestroySpawnedEffectObjects();

        for (int i = objectsToDestroy.Count - 1; i >= 0; i--)
        {
            Object target = objectsToDestroy[i];
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
        }

        objectsToDestroy.Clear();
    }

    private static void DestroySpawnedEffectObjects()
    {
        string[] effectNames =
        {
            "LastBossAuraEffect",
            "LastBossShieldEffect",
            "LastBossShieldBreakEffect",
            "LastBossDestroyEffect",
            "LastBossSlashEffect",
            "LastBossRangeIndicator",
            "LastBossMagicCircleIn",
            "LastBossMagicCircleOut"
        };

        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = transforms.Length - 1; i >= 0; i--)
        {
            Transform transform = transforms[i];
            if (transform == null)
            {
                continue;
            }

            for (int j = 0; j < effectNames.Length; j++)
            {
                if (transform.name == effectNames[j])
                {
                    Object.DestroyImmediate(transform.gameObject);
                    break;
                }
            }
        }
    }

    [Test]
    public void BuildFrames_UsesRowMajorTopToBottomOrder()
    {
        Texture2D texture = CreateTexture("GridRowMajor", 20, 20);
        var generatedSprites = new List<Sprite>();
        GridSpriteSheetClip clip = CreateClip(texture, 2, 2, 4, 30f);

        Sprite[] frames = GridSpriteSheetUtility.BuildFrames(clip, generatedSprites);

        Assert.That(frames, Has.Length.EqualTo(4));
        Assert.That(frames[0].textureRect, Is.EqualTo(new Rect(0f, 10f, 10f, 10f)));
        Assert.That(frames[1].textureRect, Is.EqualTo(new Rect(10f, 10f, 10f, 10f)));
        Assert.That(frames[2].textureRect, Is.EqualTo(new Rect(0f, 0f, 10f, 10f)));
        Assert.That(frames[3].textureRect, Is.EqualTo(new Rect(10f, 0f, 10f, 10f)));

        GridSpriteSheetUtility.DestroyGeneratedSprites(generatedSprites);
    }

    [UnityTest]
    public IEnumerator GridPlayer_HoldsLastFrameAndCompletes()
    {
        Texture2D texture = CreateTexture("GridHold", 4, 2);
        GameObject effectObject = CreateObject("GridHoldPlayer", Vector2.zero);
        effectObject.AddComponent<SpriteRenderer>();
        GridSpriteSheetPlayer player = effectObject.AddComponent<GridSpriteSheetPlayer>();
        GridSpriteSheetClip clip = CreateClip(texture, 2, 1, 2, 60f);
        int lastFrame = -1;
        bool completed = false;

        bool started = player.Play(
            clip,
            loop: false,
            holdLast: true,
            hideOnComplete: false,
            frameChanged: frameIndex => lastFrame = frameIndex,
            completed: () => completed = true);

        Assert.That(started, Is.True);
        yield return new WaitForSecondsRealtime(clip.DurationSeconds + 0.05f);

        SpriteRenderer renderer = effectObject.GetComponent<SpriteRenderer>();
        Assert.That(completed, Is.True);
        Assert.That(lastFrame, Is.EqualTo(1));
        Assert.That(renderer.enabled, Is.True);
        Assert.That(renderer.sprite, Is.Not.Null);
    }

    [UnityTest]
    public IEnumerator GridPlayer_LoopsUntilStoppedAndRebuildsGeneratedFrames()
    {
        Texture2D texture = CreateTexture("GridLoop", 4, 2);
        GameObject effectObject = CreateObject("GridLoopPlayer", Vector2.zero);
        effectObject.AddComponent<SpriteRenderer>();
        GridSpriteSheetPlayer player = effectObject.AddComponent<GridSpriteSheetPlayer>();
        GridSpriteSheetClip clip = CreateClip(texture, 2, 1, 2, 60f);
        int frameCallbacks = 0;
        bool completed = false;

        player.Play(
            clip,
            loop: true,
            holdLast: false,
            hideOnComplete: false,
            frameChanged: _ => frameCallbacks++,
            completed: () => completed = true);

        yield return new WaitForSecondsRealtime(clip.DurationSeconds * 2.5f);

        Assert.That(frameCallbacks, Is.GreaterThan(2));
        Assert.That(completed, Is.False);
        Assert.That(player.IsPlaying, Is.True);

        player.Play(clip, loop: false, holdLast: false, hideOnComplete: true);
        IList<Sprite> generatedSprites = GetPrivateField<IList<Sprite>>(player, "generatedSprites");
        Assert.That(generatedSprites.Count, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator EffectController_ShieldBreakPlaysOncePerDownCycle()
    {
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "shieldInSpriteSheet", CreateTexture("ShieldIn", 10, 4));
        SetPrivateField(effects, "shieldLoopSpriteSheet", CreateTexture("ShieldLoop", 10, 24));
        SetPrivateField(effects, "shieldBreakSpriteSheet", CreateTexture("ShieldBreak", 6, 20));
        SetPrivateField(effects, "auraSpriteSheet", CreateTexture("Aura", 10, 12));

        effects.HandleEncounterStarted();
        yield return null;

        Assert.That(CountObjectsNamed("LastBossShieldEffect"), Is.EqualTo(1));

        effects.HandleDownStarted();
        yield return null;
        Assert.That(CountObjectsNamed("LastBossShieldBreakEffect"), Is.EqualTo(1));

        effects.HandleDownStarted();
        yield return null;
        Assert.That(CountObjectsNamed("LastBossShieldBreakEffect"), Is.EqualTo(1));

        effects.HandleDownEnded();
        yield return null;
        effects.HandleDownStarted();
        yield return null;

        Assert.That(CountObjectsNamed("LastBossShieldBreakEffect"), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator GroundBladeVisual_EnablesColliderOnConfiguredFrame()
    {
        GameObject bladeObject = CreateObject("GroundBlade", Vector2.zero);
        BoxCollider2D collider = bladeObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1f, 2f);
        LastBossBladeAttack blade = bladeObject.AddComponent<LastBossBladeAttack>();
        InvokePrivate(blade, "Awake");
        GridSpriteSheetClip clip = CreateClip(CreateTexture("UnderAttack", 8, 2), 4, 1, 4, 30f);

        blade.ConfigureGroundVisual(clip, uprightFrameIndex: 3, slotIndex: 0, sizeMultiplier: 1f);
        blade.InitializeGround(null, 1, groundY: 0f, riseDuration: 0f);

        Assert.That(collider.enabled, Is.False);
        yield return new WaitForSecondsRealtime((4f / 30f) + 0.05f);

        Assert.That(collider.enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator RainBladeVisual_HoldsInFrameThenPlaysOutAndDestroysOnLand()
    {
        GameObject bladeObject = CreateObject("RainBlade", Vector2.zero);
        BoxCollider2D collider = bladeObject.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        LastBossBladeAttack blade = bladeObject.AddComponent<LastBossBladeAttack>();
        InvokePrivate(blade, "Awake");
        GridSpriteSheetClip inClip = CreateClip(CreateTexture("TopIn", 10, 2), 5, 1, 5, 60f);
        GridSpriteSheetClip outClip = CreateClip(CreateTexture("TopOut", 8, 2), 4, 1, 4, 60f);

        blade.ConfigureRainVisual(inClip, outClip, sizeMultiplier: 1f);
        blade.InitializeRainPreview(
            null,
            1,
            targetPoint: Vector2.zero,
            previewAimPoint: Vector2.down,
            fallSpeed: 20f,
            groundDestroyDelay: 10f);

        yield return new WaitForSecondsRealtime(inClip.DurationSeconds + 0.05f);
        Assert.That(bladeObject.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);

        blade.ReleaseRainBlade();
        InvokePrivate(blade, "MoveRainBlade");

        Assert.That(collider.enabled, Is.False);
        yield return new WaitForSecondsRealtime(outClip.DurationSeconds + 0.1f);
        yield return null;

        Assert.That(bladeObject == null, Is.True);
    }

    [UnityTest]
    public IEnumerator LastBossDeath_FiresImmediatelyHidesOnFrameFiveAndDestroysAfterEffect()
    {
        GameObject bossObject = CreateObject("LastBoss", Vector2.zero);
        bossObject.SetActive(false);
        SpriteRenderer renderer = bossObject.AddComponent<SpriteRenderer>();
        bossObject.AddComponent<BoxCollider2D>();
        Rigidbody2D rigidbody2D = bossObject.AddComponent<Rigidbody2D>();
        rigidbody2D.gravityScale = 0f;
        LastBossEffectController effects = bossObject.AddComponent<LastBossEffectController>();
        SetPrivateField(effects, "deathSpriteSheet", CreateTexture("BossDestroy", 10, 18));
        LastBossController boss = bossObject.AddComponent<LastBossController>();
        bossObject.SetActive(true);
        InvokePrivate(effects, "Awake");
        InvokePrivate(boss, "Awake");
        bool died = false;
        boss.Died += () => died = true;

        InvokePrivate(boss, "Die");

        Assert.That(died, Is.True);
        Assert.That(bossObject.GetComponent<Collider2D>().enabled, Is.False);
        Assert.That(rigidbody2D.simulated, Is.False);
        Assert.That(renderer.enabled, Is.True);

        yield return new WaitForSecondsRealtime((5f / 30f) + 0.05f);
        Assert.That(renderer.enabled, Is.False);
        Assert.That(bossObject == null, Is.False);

        yield return new WaitForSecondsRealtime((45f / 30f) + 0.1f);
        yield return null;
        Assert.That(bossObject == null, Is.True);
    }

    [Test]
    public void MagicCirclePosition_OffsetsBeyondRainSpawnAwayFromGround()
    {
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "magicCircleBeyondSpawnDistance", 2f);

        Vector3 position = effects.ResolveMagicCirclePosition(Vector2.zero, new Vector2(0f, 4f));

        Assert.That(position.x, Is.EqualTo(0f).Within(0.001f));
        Assert.That(position.y, Is.EqualTo(6f).Within(0.001f));
    }

    private LastBossEffectController CreateEffectController()
    {
        GameObject bossObject = CreateObject("LastBossEffectsOwner", Vector2.zero);
        bossObject.AddComponent<SpriteRenderer>();
        bossObject.AddComponent<BoxCollider2D>();
        LastBossEffectController effects = bossObject.AddComponent<LastBossEffectController>();
        SetPrivateField(effects, "framesPerSecond", 30f);
        return effects;
    }

    private Texture2D CreateTexture(string name, int width, int height)
    {
        Texture2D texture = new Texture2D(width, height);
        texture.name = name;
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();
        objectsToDestroy.Add(texture);
        return texture;
    }

    private GameObject CreateObject(string name, Vector2 position)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.position = position;
        objectsToDestroy.Add(gameObject);
        return gameObject;
    }

    private static GridSpriteSheetClip CreateClip(
        Texture2D texture,
        int columns,
        int rows,
        int frameCount,
        float framesPerSecond)
    {
        return new GridSpriteSheetClip
        {
            SpriteSheet = texture,
            Columns = columns,
            Rows = rows,
            FrameCount = frameCount,
            FramesPerSecond = framesPerSecond,
            PixelsPerUnit = 100f,
            Pivot = new Vector2(0.5f, 0.5f)
        };
    }

    private static int CountObjectsNamed(string objectName)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == objectName)
            {
                count++;
            }
        }

        return count;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, $"{fieldName} must exist.");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, $"{fieldName} must exist.");
        return (T)field.GetValue(target);
    }

    private static object InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstancePrivate);
        Assert.That(method, Is.Not.Null, $"{methodName} must exist.");
        return method.Invoke(target, arguments);
    }
}
