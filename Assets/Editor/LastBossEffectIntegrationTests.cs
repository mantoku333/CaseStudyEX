using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using GameName.Enemy;
using NUnit.Framework;
using UnityEditor;
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

    [Test]
    public void BuildFrames_CenteredCropInsetKeepsFrameOrderAndCount()
    {
        Texture2D texture = CreateTexture("GridCrop", 20, 20);
        var generatedSprites = new List<Sprite>();
        GridSpriteSheetClip clip = CreateClip(texture, 2, 2, 4, 30f);
        clip.CenteredCropInsetPixels = 2;

        Sprite[] frames = GridSpriteSheetUtility.BuildFrames(clip, generatedSprites);

        Assert.That(frames, Has.Length.EqualTo(4));
        Assert.That(frames[0].textureRect, Is.EqualTo(new Rect(2f, 12f, 6f, 6f)));
        Assert.That(frames[1].textureRect, Is.EqualTo(new Rect(12f, 12f, 6f, 6f)));
        Assert.That(frames[2].textureRect, Is.EqualTo(new Rect(2f, 2f, 6f, 6f)));
        Assert.That(frames[3].textureRect, Is.EqualTo(new Rect(12f, 2f, 6f, 6f)));

        GridSpriteSheetUtility.DestroyGeneratedSprites(generatedSprites);
    }

    [Test]
    public void BuildFrames_FrameCropPixelsKeepsFrameOrderAndCount()
    {
        Texture2D texture = CreateTexture("GridFrameCrop", 20, 20);
        var generatedSprites = new List<Sprite>();
        GridSpriteSheetClip clip = CreateClip(texture, 2, 2, 4, 30f);
        clip.UseFrameCrop = true;
        clip.FrameCropPixels = new RectInt(1, 2, 3, 4);

        Sprite[] frames = GridSpriteSheetUtility.BuildFrames(clip, generatedSprites);

        Assert.That(frames, Has.Length.EqualTo(4));
        Assert.That(frames[0].textureRect, Is.EqualTo(new Rect(1f, 12f, 3f, 4f)));
        Assert.That(frames[1].textureRect, Is.EqualTo(new Rect(11f, 12f, 3f, 4f)));
        Assert.That(frames[2].textureRect, Is.EqualTo(new Rect(1f, 2f, 3f, 4f)));
        Assert.That(frames[3].textureRect, Is.EqualTo(new Rect(11f, 2f, 3f, 4f)));

        GridSpriteSheetUtility.DestroyGeneratedSprites(generatedSprites);
    }

    [Test]
    public void BuildFrames_FrameCropReferenceScalesCropToImportedFrameSize()
    {
        Texture2D texture = CreateTexture("GridScaledFrameCrop", 10, 10);
        var generatedSprites = new List<Sprite>();
        GridSpriteSheetClip clip = CreateClip(texture, 1, 1, 1, 30f);
        clip.UseFrameCrop = true;
        clip.FrameCropPixels = new RectInt(20, 40, 40, 20);
        clip.FrameCropReferencePixels = new Vector2Int(100, 100);

        Sprite[] frames = GridSpriteSheetUtility.BuildFrames(clip, generatedSprites);

        Assert.That(frames, Has.Length.EqualTo(1));
        Assert.That(frames[0].textureRect, Is.EqualTo(new Rect(2f, 4f, 4f, 2f)));

        GridSpriteSheetUtility.DestroyGeneratedSprites(generatedSprites);
    }

    [Test]
    public void BuildFrames_FrameCropForSizingOnlyKeepsFullFrameRect()
    {
        Texture2D texture = CreateTexture("GridSizingOnlyCrop", 20, 10);
        var generatedSprites = new List<Sprite>();
        GridSpriteSheetClip clip = CreateClip(texture, 2, 1, 2, 30f);
        clip.UseFrameCrop = true;
        clip.UseFrameCropForSizingOnly = true;
        clip.FrameCropPixels = new RectInt(2, 1, 4, 8);

        Sprite[] frames = GridSpriteSheetUtility.BuildFrames(clip, generatedSprites);
        Vector2 visibleFrameSize = GridSpriteSheetUtility.ResolveVisibleFrameSize(clip);

        Assert.That(frames, Has.Length.EqualTo(2));
        Assert.That(frames[0].textureRect, Is.EqualTo(new Rect(0f, 0f, 10f, 10f)));
        Assert.That(frames[1].textureRect, Is.EqualTo(new Rect(10f, 0f, 10f, 10f)));
        Assert.That(visibleFrameSize.x, Is.EqualTo(0.04f).Within(0.001f));
        Assert.That(visibleFrameSize.y, Is.EqualTo(0.08f).Within(0.001f));

        GridSpriteSheetUtility.DestroyGeneratedSprites(generatedSprites);
    }

    [Test]
    public void BuildFrames_SpriteFramesUseImportedSpritesWithoutGeneratingCopies()
    {
        Sprite first = CreateSprite("ImportedFrame_0", 20, 10, 10f);
        Sprite second = CreateSprite("ImportedFrame_1", 12, 16, 8f);
        var generatedSprites = new List<Sprite>();
        GridSpriteSheetClip clip = CreateSpriteClip(new[] { first, second }, 30f);

        Sprite[] frames = GridSpriteSheetUtility.BuildFrames(clip, generatedSprites);
        Vector2 visibleFrameSize = GridSpriteSheetUtility.ResolveVisibleFrameSize(clip);

        Assert.That(frames, Has.Length.EqualTo(2));
        Assert.That(frames[0], Is.SameAs(first));
        Assert.That(frames[1], Is.SameAs(second));
        Assert.That(frames[0].textureRect, Is.EqualTo(first.textureRect));
        Assert.That(frames[0].pivot, Is.EqualTo(first.pivot));
        Assert.That(generatedSprites, Is.Empty);
        GridSpriteSheetUtility.DestroyGeneratedSprites(generatedSprites);
        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.Not.Null);
        Assert.That(visibleFrameSize.x, Is.EqualTo(2f).Within(0.001f));
        Assert.That(visibleFrameSize.y, Is.EqualTo(2f).Within(0.001f));
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
    public IEnumerator GridPlayer_CroppedLoopKeepsTargetScaleStableAcrossFrames()
    {
        Texture2D texture = CreateTexture("GridCroppedLoop", 20, 10);
        GameObject effectObject = CreateObject("GridCroppedLoopPlayer", Vector2.zero);
        effectObject.AddComponent<SpriteRenderer>();
        GridSpriteSheetPlayer player = effectObject.AddComponent<GridSpriteSheetPlayer>();
        GridSpriteSheetClip clip = CreateClip(texture, 2, 1, 2, 60f);
        clip.CenteredCropInsetPixels = 2;
        player.SetTargetWorldSize(new Vector2(2f, 2f));

        player.Play(clip, loop: true, holdLast: false, hideOnComplete: false);
        yield return null;
        Vector3 firstScale = effectObject.transform.localScale;
        yield return new WaitForSecondsRealtime((1f / 60f) + 0.05f);

        Assert.That(effectObject.transform.localScale.x, Is.EqualTo(firstScale.x).Within(0.001f));
        Assert.That(effectObject.transform.localScale.y, Is.EqualTo(firstScale.y).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator GridPlayer_TargetWorldSizeChangeRescalesCurrentFrameImmediately()
    {
        Texture2D texture = CreateTexture("GridResize", 10, 10);
        GameObject effectObject = CreateObject("GridResizePlayer", Vector2.zero);
        effectObject.AddComponent<SpriteRenderer>();
        GridSpriteSheetPlayer player = effectObject.AddComponent<GridSpriteSheetPlayer>();
        GridSpriteSheetClip clip = CreateClip(texture, 1, 1, 1, 60f);
        player.SetTargetWorldSize(new Vector2(1f, 1f));

        player.Play(clip, loop: true, holdLast: false, hideOnComplete: false);
        yield return null;

        Vector3 firstScale = effectObject.transform.localScale;
        player.SetTargetWorldSize(new Vector2(2f, 2f));

        Assert.That(effectObject.transform.localScale.x, Is.EqualTo(firstScale.x * 2f).Within(0.001f));
        Assert.That(effectObject.transform.localScale.y, Is.EqualTo(firstScale.y * 2f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator GridPlayer_SpriteFramesUseLargestFrameForStableTargetScale()
    {
        Sprite largeFrame = CreateSprite("SpriteScaleLarge", 100, 100, 100f);
        Sprite narrowFrame = CreateSprite("SpriteScaleNarrow", 50, 100, 100f);
        GameObject effectObject = CreateObject("GridSpriteStableScalePlayer", Vector2.zero);
        effectObject.AddComponent<SpriteRenderer>();
        GridSpriteSheetPlayer player = effectObject.AddComponent<GridSpriteSheetPlayer>();
        GridSpriteSheetClip clip = CreateSpriteClip(new[] { largeFrame, narrowFrame }, 60f);
        player.SetTargetWorldSize(new Vector2(2f, 2f));

        player.Play(clip, loop: true, holdLast: false, hideOnComplete: false);
        yield return null;
        Vector3 firstScale = effectObject.transform.localScale;
        yield return new WaitForSecondsRealtime((1f / 60f) + 0.05f);

        Assert.That(effectObject.transform.localScale.x, Is.EqualTo(firstScale.x).Within(0.001f));
        Assert.That(effectObject.transform.localScale.y, Is.EqualTo(firstScale.y).Within(0.001f));
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
    public IEnumerator EffectController_ShieldBreakPositionStaysLockedWhenFacingChanges()
    {
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "shieldBreakSpriteSheet", CreateTexture("ShieldBreakLocked", 6, 20));
        SetPrivateField(effects, "shieldBreakOffset", new Vector3(0.75f, 1f, 0f));

        effects.SetFacingDirection(1);
        effects.HandleDownStarted();
        yield return null;

        Transform shieldBreak = FindTransformNamed("LastBossShieldBreakEffect");
        Assert.That(shieldBreak, Is.Not.Null);
        Vector3 lockedPosition = shieldBreak.position;

        effects.SetFacingDirection(-1);
        InvokePrivate(effects, "LateUpdate");

        Assert.That(shieldBreak.position.x, Is.EqualTo(lockedPosition.x).Within(0.001f));
        Assert.That(shieldBreak.position.y, Is.EqualTo(lockedPosition.y).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator EffectController_ShieldLoopsAfterShieldIn()
    {
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "shieldInSpriteSheet", CreateTexture("ShieldInLoopStart", 10, 4));
        SetPrivateField(effects, "shieldLoopSpriteSheet", CreateTexture("ShieldLoopVisible", 10, 24));

        effects.HandleEncounterStarted();
        yield return new WaitForSecondsRealtime((10f / 30f) + 0.1f);

        SpriteRenderer shieldRenderer = FindRendererNamed("LastBossShieldEffect");
        Assert.That(shieldRenderer, Is.Not.Null);
        Assert.That(shieldRenderer.enabled, Is.True);
        Assert.That(shieldRenderer.sprite, Is.Not.Null);
        Assert.That(shieldRenderer.gameObject.GetComponent<GridSpriteSheetPlayer>().IsPlaying, Is.True);
    }

    [UnityTest]
    public IEnumerator EffectController_InspectorShieldSizeChangeRescalesActiveShield()
    {
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "shieldInSpriteSheet", CreateTexture("ShieldResizeIn", 10, 4));
        SetPrivateField(effects, "shieldLoopSpriteSheet", CreateTexture("ShieldResizeLoop", 10, 24));
        SetPrivateField(effects, "shieldSizeMultiplier", 1f);

        effects.HandleEncounterStarted();
        yield return null;

        SpriteRenderer shieldRenderer = FindRendererNamed("LastBossShieldEffect");
        Assert.That(shieldRenderer, Is.Not.Null);
        Vector3 firstScale = shieldRenderer.transform.localScale;

        SetPrivateField(effects, "shieldSizeMultiplier", 2f);
        InvokePrivate(effects, "OnValidate");

        Assert.That(shieldRenderer.transform.localScale.x, Is.EqualTo(firstScale.x * 2f).Within(0.001f));
        Assert.That(shieldRenderer.transform.localScale.y, Is.EqualTo(firstScale.y * 2f).Within(0.001f));

        effects.HandleEncounterStopped();
        effects.HandleEncounterStarted();
        yield return null;

        SpriteRenderer respawnedShieldRenderer = FindRendererNamed("LastBossShieldEffect");
        Assert.That(respawnedShieldRenderer, Is.Not.Null);
        Assert.That(respawnedShieldRenderer.transform.localScale.x, Is.EqualTo(firstScale.x * 2f).Within(0.001f));
        Assert.That(respawnedShieldRenderer.transform.localScale.y, Is.EqualTo(firstScale.y * 2f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator EffectController_AuraFlipsWhenFacingLeft()
    {
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "auraSpriteSheet", CreateTexture("AuraFlip", 10, 12));
        SetPrivateField(effects, "auraFacingPush", 0.35f);

        effects.SetFacingDirection(-1);
        effects.HandleResetToFull();
        yield return null;

        SpriteRenderer auraRenderer = FindRendererNamed("LastBossAuraEffect");
        Assert.That(auraRenderer, Is.Not.Null);
        Assert.That(auraRenderer.flipX, Is.True);
        Assert.That(auraRenderer.transform.position.x, Is.EqualTo(0.35f).Within(0.001f));

        effects.SetFacingDirection(1);
        yield return null;
        Assert.That(auraRenderer.flipX, Is.False);
        Assert.That(auraRenderer.transform.position.x, Is.EqualTo(-0.35f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator EffectController_InspectorAuraFacingPushChangeRepositionsActiveAura()
    {
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "auraSpriteSheet", CreateTexture("AuraInspectorPush", 10, 12));
        SetPrivateField(effects, "auraFacingPush", 0.35f);

        effects.SetFacingDirection(-1);
        effects.HandleResetToFull();
        yield return null;

        SpriteRenderer auraRenderer = FindRendererNamed("LastBossAuraEffect");
        Assert.That(auraRenderer, Is.Not.Null);
        Assert.That(auraRenderer.transform.position.x, Is.EqualTo(0.35f).Within(0.001f));

        SetPrivateField(effects, "auraFacingPush", 0.55f);
        InvokePrivate(effects, "OnValidate");

        Assert.That(auraRenderer.transform.position.x, Is.EqualTo(0.55f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator LastBoss_InitialAuraFacingUsesSpriteFlipBeforeEncounter()
    {
        GameObject bossObject = CreateObject("LastBossInitialFacing", Vector2.zero);
        bossObject.SetActive(false);
        SpriteRenderer renderer = bossObject.AddComponent<SpriteRenderer>();
        renderer.flipX = false;
        bossObject.AddComponent<BoxCollider2D>();
        bossObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
        LastBossEffectController effects = bossObject.AddComponent<LastBossEffectController>();
        SetPrivateField(effects, "auraSpriteSheet", CreateTexture("AuraInitialFacing", 10, 12));
        SetPrivateField(effects, "auraFacingPush", 0.35f);
        LastBossController boss = bossObject.AddComponent<LastBossController>();

        InvokePrivate(effects, "Awake");
        InvokePrivate(boss, "Awake");
        effects.HandleResetToFull();
        yield return null;

        SpriteRenderer auraRenderer = FindRendererNamed("LastBossAuraEffect");
        Assert.That(effects.FacingDirection, Is.EqualTo(-1));
        Assert.That(auraRenderer, Is.Not.Null);
        Assert.That(auraRenderer.flipX, Is.True);
        Assert.That(auraRenderer.transform.position.x, Is.EqualTo(0.35f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator EffectController_MagicCircleFlipsRightAndDoesNotRotate()
    {
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "magicCircleInSpriteSheet", CreateTexture("MagicInFlip", 10, 8));

        effects.SetFacingDirection(1);
        effects.BeginVerticalRangeCharge(new Vector2(2f, 0f), new Vector2(0f, 4f));
        yield return null;

        SpriteRenderer magicRenderer = FindRendererNamed("LastBossMagicCircleIn");
        Assert.That(magicRenderer, Is.Not.Null);
        Assert.That(magicRenderer.flipX, Is.True);
        Assert.That(magicRenderer.transform.rotation, Is.EqualTo(Quaternion.identity));

        effects.UpdateVerticalRangeCharge(new Vector2(-2f, 0f), new Vector2(0f, 4f));
        yield return null;
        Assert.That(magicRenderer.flipX, Is.False);
        Assert.That(magicRenderer.transform.rotation, Is.EqualTo(Quaternion.identity));

        effects.SetFacingDirection(1);
        yield return null;
        Assert.That(magicRenderer.flipX, Is.False);
        Assert.That(magicRenderer.transform.rotation, Is.EqualTo(Quaternion.identity));
    }

    [UnityTest]
    public IEnumerator EffectController_MagicCircleKeepsSpawnPositionDuringChargeUpdates()
    {
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "magicCircleInSpriteSheet", CreateTexture("MagicInPositionLock", 10, 8));

        Vector2 initialGroundLockPoint = new Vector2(2f, 0f);
        Vector2 initialRainSpawnPosition = new Vector2(0f, 4f);
        Vector3 expectedPosition = effects.ResolveMagicCirclePosition(initialGroundLockPoint, initialRainSpawnPosition);

        effects.BeginVerticalRangeCharge(initialGroundLockPoint, initialRainSpawnPosition);
        yield return null;

        SpriteRenderer magicRenderer = FindRendererNamed("LastBossMagicCircleIn");
        Assert.That(magicRenderer, Is.Not.Null);
        Assert.That(magicRenderer.transform.position, Is.EqualTo(expectedPosition));

        effects.UpdateVerticalRangeCharge(new Vector2(-8f, -3f), new Vector2(5f, 7f));
        yield return null;

        Assert.That(magicRenderer.transform.position, Is.EqualTo(expectedPosition));
    }

    [Test]
    public void EffectController_MagicCircleBladeSpawnUsesLowerFacingCorner()
    {
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "magicCircleWorldSize", new Vector2(4f, 4f));

        Vector2 rainSpawnPosition = new Vector2(0f, 4f);
        Vector3 leftCircleCenter = effects.ResolveMagicCirclePosition(new Vector2(-2f, 0f), rainSpawnPosition);
        Vector3 rightCircleCenter = effects.ResolveMagicCirclePosition(new Vector2(2f, 0f), rainSpawnPosition);

        Vector2 leftSpawn = effects.ResolveMagicCircleBladeSpawnPosition(new Vector2(-2f, 0f), rainSpawnPosition);
        Vector2 rightSpawn = effects.ResolveMagicCircleBladeSpawnPosition(new Vector2(2f, 0f), rainSpawnPosition);

        Assert.That(leftSpawn, Is.EqualTo((Vector2)leftCircleCenter + new Vector2(-2f, -2f)));
        Assert.That(rightSpawn, Is.EqualTo((Vector2)rightCircleCenter + new Vector2(2f, -2f)));
    }

    [Test]
    public void EffectController_FacingOffsetMirrorsOnlyLocalX()
    {
        LastBossEffectController effects = CreateEffectController();

        effects.SetFacingDirection(1);
        Assert.That(effects.ResolveFacingOffset(new Vector3(0.5f, 0.25f, 0f)), Is.EqualTo(new Vector3(0.5f, 0.25f, 0f)));

        effects.SetFacingDirection(-1);
        Assert.That(effects.ResolveFacingOffset(new Vector3(0.5f, 0.25f, 0f)), Is.EqualTo(new Vector3(-0.5f, 0.25f, 0f)));
    }

    [Test]
    public void EffectController_RangeIndicatorSizeUsesGroundBladeWidth()
    {
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "rangeEffectSizeMultiplier", 1.25f);
        SetPrivateField(effects, "rangeEffectFrameSizeMultiplier", Vector2.one);

        Vector2 size = effects.ResolveRangeIndicatorWorldSize(2f);

        Assert.That(size.x, Is.EqualTo(2.5f).Within(0.001f));
        Assert.That(size.y, Is.EqualTo(2.5f * 214f / 316f).Within(0.001f));
    }

    [Test]
    public void LastBossPrefab_RestoresEffectControllerAndTransparentPreviewBoxes()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/LastBoss.prefab");
        Assert.That(prefab, Is.Not.Null);

        LastBossController boss = prefab.GetComponent<LastBossController>();
        LastBossEffectController effects = prefab.GetComponent<LastBossEffectController>();

        Assert.That(boss, Is.Not.Null);
        Assert.That(effects, Is.Not.Null);
        Assert.That(GetPrivateField<LastBossSpriteAnimator>(boss, "spriteView"), Is.Not.Null);
        Assert.That(GetPrivateField<Color>(boss, "telegraphColor"), Is.EqualTo(new Color(1f, 1f, 1f, 0f)));
        Assert.That(GetPrivateField<Color>(boss, "attackColor"), Is.EqualTo(new Color(1f, 1f, 1f, 0f)));
        Assert.That(GetPrivateField<float>(effects, "auraFacingPush"), Is.EqualTo(0.55f).Within(0.001f));

        string[] textureFields =
        {
            "shieldInSpriteSheet",
            "shieldLoopSpriteSheet",
            "shieldBreakSpriteSheet",
            "auraSpriteSheet",
            "deathSpriteSheet",
            "slashSpriteSheet",
            "underAttackSpriteSheet",
            "rangeSpriteSheet",
            "magicCircleInSpriteSheet",
            "magicCircleOutSpriteSheet",
            "topAttackInSpriteSheet",
            "topAttackOutSpriteSheet"
        };

        for (int i = 0; i < textureFields.Length; i++)
        {
            Assert.That(
                GetPrivateField<Texture2D>(effects, textureFields[i]),
                Is.Not.Null,
                $"{textureFields[i]} should be assigned on the LastBoss prefab.");
        }

        AssertSpriteFrameCount(effects, "shieldInSpriteFrames", 40);
        AssertSpriteFrameCount(effects, "shieldLoopSpriteFrames", 60);
        AssertSpriteFrameCount(effects, "shieldBreakSpriteFrames", 154);
        AssertSpriteFrameCount(effects, "auraSpriteFrames", 30);
        AssertSpriteFrameCount(effects, "deathSpriteFrames", 587);
        AssertSpriteFrameCount(effects, "slashSpriteFrames", 30);
        Assert.That(GetPrivateField<Sprite[]>(effects, "underAttackSpriteFrames"), Has.Length.GreaterThanOrEqualTo(20));
        Assert.That(GridSpriteSheetUtility.BuildFrames(effects.GroundBladeClip), Has.Length.EqualTo(20));
        AssertSpriteFrameCount(effects, "rangeSpriteFrames", 90);
        AssertSpriteFrameCount(effects, "magicCircleInSpriteFrames", 19);
        AssertSpriteFrameCount(effects, "magicCircleOutSpriteFrames", 9);
        AssertSpriteFrameCount(effects, "topAttackInSpriteFrames", 23);
        AssertSpriteFrameCount(effects, "topAttackOutSpriteFrames", 20);
    }

    [UnityTest]
    public IEnumerator EffectController_CopiesSortingFromSpriteViewChildRenderer()
    {
        GameObject bossObject = CreateObject("LastBossChildRendererEffectsOwner", Vector2.zero);
        bossObject.AddComponent<BoxCollider2D>();
        GameObject spriteViewObject = CreateObject("SpriteView", Vector2.zero);
        spriteViewObject.SetActive(false);
        spriteViewObject.transform.SetParent(bossObject.transform, false);
        SpriteRenderer childRenderer = spriteViewObject.AddComponent<SpriteRenderer>();
        childRenderer.sortingOrder = 17;
        spriteViewObject.AddComponent<LastBossSpriteAnimator>();
        LastBossEffectController effects = bossObject.AddComponent<LastBossEffectController>();
        SetPrivateField(effects, "auraSpriteSheet", CreateTexture("AuraChildRenderer", 10, 12));
        SetPrivateField(effects, "auraSortingOrderOffset", 6);

        InvokePrivate(effects, "Awake");
        effects.HandleResetToFull();
        yield return null;

        SpriteRenderer auraRenderer = FindRendererNamed("LastBossAuraEffect");
        Assert.That(auraRenderer, Is.Not.Null);
        Assert.That(auraRenderer.sortingOrder, Is.EqualTo(childRenderer.sortingOrder + 6));
    }

    [UnityTest]
    public IEnumerator EffectController_RangeIndicatorUsesStableFullFrameAndConfiguredOffset()
    {
        Texture2D range = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Sprites/Effects/eff_range.png");
        Assert.That(range, Is.Not.Null);
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "rangeSpriteSheet", range);
        SetPrivateField(effects, "rangeOffset", new Vector3(0f, -0.35f, 0f));
        InvokePrivate(effects, "OnValidate");
        Sprite[] rangeFrames = GetPrivateField<Sprite[]>(effects, "rangeSpriteFrames");
        Assert.That(rangeFrames, Has.Length.EqualTo(90));

        effects.BeginHorizontalRangeCharge(new List<Vector2> { Vector2.zero }, 2f);
        yield return null;

        SpriteRenderer rangeRenderer = FindRendererNamed("LastBossRangeIndicator");
        Assert.That(rangeRenderer, Is.Not.Null);
        Assert.That(rangeRenderer.sprite, Is.Not.Null);
        Assert.That(rangeRenderer.sprite, Is.SameAs(rangeFrames[0]));
        Assert.That(rangeRenderer.sprite.pivot.y, Is.EqualTo(rangeFrames[0].pivot.y).Within(0.001f));
        Assert.That(rangeRenderer.transform.position.y, Is.EqualTo(-0.35f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator EffectController_ShieldUsesStableFullFrameCells()
    {
        const string shieldInPath = "Assets/Art/Sprites/Effects/eff_boss_shield_in.png";
        const string shieldLoopPath = "Assets/Art/Sprites/Effects/eff_boss_shield.png";
        Texture2D shieldIn = AssetDatabase.LoadAssetAtPath<Texture2D>(shieldInPath);
        Texture2D shieldLoop = AssetDatabase.LoadAssetAtPath<Texture2D>(shieldLoopPath);
        Assert.That(shieldIn, Is.Not.Null);
        Assert.That(shieldLoop, Is.Not.Null);
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "shieldInSpriteSheet", shieldIn);
        SetPrivateField(effects, "shieldLoopSpriteSheet", shieldLoop);
        InvokePrivate(effects, "OnValidate");
        Sprite[] shieldInFrames = GetPrivateField<Sprite[]>(effects, "shieldInSpriteFrames");
        Sprite[] shieldLoopFrames = GetPrivateField<Sprite[]>(effects, "shieldLoopSpriteFrames");
        Sprite[] playableShieldInFrames = LoadPrimarySpriteFramesByGrid(shieldInPath, 5, 2, 10);
        Assert.That(shieldInFrames, Has.Length.EqualTo(40));
        Assert.That(shieldLoopFrames, Has.Length.EqualTo(60));
        Assert.That(playableShieldInFrames, Has.Length.EqualTo(10));

        effects.HandleEncounterStarted();
        yield return null;

        SpriteRenderer shieldRenderer = FindRendererNamed("LastBossShieldEffect");
        Assert.That(shieldRenderer, Is.Not.Null);
        Assert.That(shieldRenderer.sprite, Is.Not.Null);
        Assert.That(shieldRenderer.sprite, Is.SameAs(playableShieldInFrames[0]));

        yield return new WaitForSecondsRealtime((playableShieldInFrames.Length / 30f) + 0.1f);

        GridSpriteSheetPlayer shieldPlayer = shieldRenderer.GetComponent<GridSpriteSheetPlayer>();
        Assert.That(shieldPlayer, Is.Not.Null);
        Assert.That(shieldRenderer.sprite, Is.Not.Null);
        Assert.That(shieldRenderer.sprite, Is.SameAs(shieldLoopFrames[shieldPlayer.CurrentFrameIndex]));

        Vector3 loopScale = shieldRenderer.transform.localScale;
        yield return new WaitForSecondsRealtime((1f / 30f) + 0.05f);
        Assert.That(shieldRenderer.transform.localScale.x, Is.EqualTo(loopScale.x).Within(0.001f));
        Assert.That(shieldRenderer.transform.localScale.y, Is.EqualTo(loopScale.y).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator EffectController_ShieldBreakUsesStableFullGridFrames()
    {
        const string shieldBreakPath = "Assets/Art/Sprites/Effects/eff_boss_shieldbreak.png";
        Texture2D shieldBreak = AssetDatabase.LoadAssetAtPath<Texture2D>(shieldBreakPath);
        Assert.That(shieldBreak, Is.Not.Null);
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "shieldBreakSpriteSheet", shieldBreak);
        InvokePrivate(effects, "OnValidate");
        Sprite[] shieldBreakFrames = GetPrivateField<Sprite[]>(effects, "shieldBreakSpriteFrames");
        Sprite[] playableShieldBreakFrames = LoadPrimarySpriteFramesByGrid(shieldBreakPath, 3, 10, 30);
        Assert.That(shieldBreakFrames, Has.Length.EqualTo(154));
        Assert.That(playableShieldBreakFrames, Has.Length.EqualTo(30));

        effects.HandleDownStarted();
        yield return null;

        SpriteRenderer shieldBreakRenderer = FindRendererNamed("LastBossShieldBreakEffect");
        Assert.That(shieldBreakRenderer, Is.Not.Null);
        int shieldBreakFrameWidth = shieldBreak.width / 3;
        int shieldBreakFrameHeight = shieldBreak.height / 10;
        Assert.That(shieldBreakRenderer.sprite, Is.Not.SameAs(playableShieldBreakFrames[0]));
        Assert.That(shieldBreakRenderer.sprite.texture, Is.SameAs(shieldBreak));
        Assert.That(
            shieldBreakRenderer.sprite.textureRect,
            Is.EqualTo(new Rect(0f, shieldBreak.height - shieldBreakFrameHeight, shieldBreakFrameWidth, shieldBreakFrameHeight)));
        Assert.That(shieldBreakRenderer.sprite.pivot.x / shieldBreakFrameWidth, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(shieldBreakRenderer.sprite.pivot.y / shieldBreakFrameHeight, Is.EqualTo(0.5f).Within(0.001f));

        yield return new WaitForSecondsRealtime((playableShieldBreakFrames.Length / 30f) + 0.2f);

        Assert.That(FindTransformNamed("LastBossShieldBreakEffect"), Is.Null);
    }

    [UnityTest]
    public IEnumerator EffectController_DeathUsesStableFullGridFrame()
    {
        Texture2D death = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Sprites/Effects/eff_boss_Destroy.png");
        Assert.That(death, Is.Not.Null);
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "deathSpriteSheet", death);
        InvokePrivate(effects, "OnValidate");
        Sprite[] deathFrames = GetPrivateField<Sprite[]>(effects, "deathSpriteFrames");
        Assert.That(deathFrames, Has.Length.EqualTo(587));

        bool played = effects.PlayDeath(null, null);
        Assert.That(played, Is.True);
        yield return null;

        SpriteRenderer deathRenderer = FindRendererNamed("LastBossDestroyEffect");
        Assert.That(deathRenderer, Is.Not.Null);
        Assert.That(deathRenderer.sprite, Is.Not.Null);
        int deathFrameWidth = death.width / 5;
        int deathFrameHeight = death.height / 9;
        Assert.That(deathRenderer.sprite, Is.Not.SameAs(deathFrames[0]));
        Assert.That(deathRenderer.sprite.texture, Is.SameAs(death));
        Assert.That(
            deathRenderer.sprite.textureRect,
            Is.EqualTo(new Rect(0f, death.height - deathFrameHeight, deathFrameWidth, deathFrameHeight)));
        Assert.That(deathRenderer.sprite.pivot.x / deathFrameWidth, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(deathRenderer.sprite.pivot.y / deathFrameHeight, Is.EqualTo(0.5f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator EffectController_DeathEffectKeepsSpawnPositionWhenBossBoundsChange()
    {
        GameObject bossObject = CreateObject("LastBossDeathPositionLock", Vector2.zero);
        BoxCollider2D collider = bossObject.AddComponent<BoxCollider2D>();
        collider.enabled = false;

        GameObject rendererObject = CreateObject("LastBossDeathPositionRenderer", new Vector2(2f, 0f));
        rendererObject.transform.SetParent(bossObject.transform, worldPositionStays: true);
        SpriteRenderer renderer = rendererObject.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSprite("LastBossDeathPositionBody", 100, 80, 10f);

        LastBossEffectController effects = bossObject.AddComponent<LastBossEffectController>();
        SetPrivateField(effects, "deathSpriteSheet", CreateTexture("DeathPositionLock", 10, 18));
        InvokePrivate(effects, "Awake");

        Assert.That(effects.PlayDeath(null, null), Is.True);
        yield return null;

        Transform deathEffect = FindTransformNamed("LastBossDestroyEffect");
        Assert.That(deathEffect, Is.Not.Null);
        Vector3 spawnedPosition = deathEffect.position;

        rendererObject.transform.position = new Vector3(8f, 0f, 0f);
        InvokePrivate(effects, "LateUpdate");

        Assert.That(deathEffect.position.x, Is.EqualTo(spawnedPosition.x).Within(0.001f));
        Assert.That(deathEffect.position.y, Is.EqualTo(spawnedPosition.y).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator EffectController_ShieldBreakKeepsSpawnPositionWhenBossBoundsChange()
    {
        GameObject bossObject = CreateObject("LastBossShieldBreakPositionLock", Vector2.zero);
        BoxCollider2D collider = bossObject.AddComponent<BoxCollider2D>();
        collider.enabled = false;

        GameObject rendererObject = CreateObject("LastBossShieldBreakPositionRenderer", new Vector2(2f, 0f));
        rendererObject.transform.SetParent(bossObject.transform, worldPositionStays: true);
        SpriteRenderer renderer = rendererObject.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSprite("LastBossShieldBreakPositionBody", 100, 80, 10f);

        LastBossEffectController effects = bossObject.AddComponent<LastBossEffectController>();
        SetPrivateField(effects, "shieldBreakSpriteSheet", CreateTexture("ShieldBreakPositionLock", 6, 20));
        InvokePrivate(effects, "Awake");

        effects.HandleDownStarted();
        yield return null;

        Transform shieldBreakEffect = FindTransformNamed("LastBossShieldBreakEffect");
        Assert.That(shieldBreakEffect, Is.Not.Null);
        Vector3 spawnedPosition = shieldBreakEffect.position;

        rendererObject.transform.position = new Vector3(8f, 0f, 0f);
        InvokePrivate(effects, "LateUpdate");

        Assert.That(shieldBreakEffect.position.x, Is.EqualTo(spawnedPosition.x).Within(0.001f));
        Assert.That(shieldBreakEffect.position.y, Is.EqualTo(spawnedPosition.y).Within(0.001f));
    }

    [Test]
    public void EffectController_BladeClipsUseVisibleArtCrops()
    {
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "underAttackSpriteSheet", CreateTexture("UnderAttackClipCrop", 10, 8));
        SetPrivateField(effects, "topAttackInSpriteSheet", CreateTexture("TopInClipCrop", 10, 10));
        SetPrivateField(effects, "topAttackOutSpriteSheet", CreateTexture("TopOutClipCrop", 10, 8));

        Assert.That(effects.GroundBladeClip.UseFrameCrop, Is.True);
        Assert.That(effects.GroundBladeClip.UseFrameCropForSizingOnly, Is.True);
        Assert.That(effects.GroundBladeClip.FrameCropPixels, Is.EqualTo(new RectInt(418, 0, 187, 1009)));
        Assert.That(effects.GroundBladeClip.FrameCropReferencePixels, Is.EqualTo(new Vector2Int(1024, 1024)));
        Assert.That(effects.RainBladeInClip.UseFrameCrop, Is.True);
        Assert.That(effects.RainBladeInClip.UseFrameCropForSizingOnly, Is.False);
        Assert.That(effects.RainBladeInClip.FrameCropPixels, Is.EqualTo(new RectInt(405, 20, 217, 995)));
        Assert.That(effects.RainBladeInClip.FrameCropReferencePixels, Is.EqualTo(new Vector2Int(1024, 1024)));
        Assert.That(effects.RainBladeOutClip.UseFrameCrop, Is.True);
        Assert.That(effects.RainBladeOutClip.UseFrameCropForSizingOnly, Is.False);
        Assert.That(effects.RainBladeOutClip.FrameCropPixels, Is.EqualTo(new RectInt(405, 20, 217, 995)));
        Assert.That(effects.RainBladeOutClip.FrameCropReferencePixels, Is.EqualTo(new Vector2Int(1024, 1024)));
    }

    [Test]
    public void EffectController_BladeClipsUseImportedGroundSlicesAndImportedRainSlices()
    {
        const string underAttackPath = "Assets/Art/Sprites/Effects/eff_under_attack.png";
        const string topAttackInPath = "Assets/Art/Sprites/Effects/eff_top_attack_in.png";
        Texture2D underAttack = AssetDatabase.LoadAssetAtPath<Texture2D>(underAttackPath);
        Texture2D topAttackIn = AssetDatabase.LoadAssetAtPath<Texture2D>(topAttackInPath);
        Assert.That(underAttack, Is.Not.Null);
        Assert.That(topAttackIn, Is.Not.Null);
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "underAttackSpriteSheet", underAttack);
        SetPrivateField(effects, "topAttackInSpriteSheet", topAttackIn);
        InvokePrivate(effects, "OnValidate");

        var generatedSprites = new List<Sprite>();
        Sprite[] groundFrames = GridSpriteSheetUtility.BuildFrames(effects.GroundBladeClip, generatedSprites);
        Sprite[] rainFrames = GridSpriteSheetUtility.BuildFrames(effects.RainBladeInClip, generatedSprites);
        Sprite[] importedGroundFrames = LoadPrimarySpriteFramesByGrid(underAttackPath, 5, 4, 20);
        Sprite[] importedRainFrames = LoadPrimarySpriteFramesByGrid(topAttackInPath, 5, 5, 23);
        Vector2 groundVisibleSize = GridSpriteSheetUtility.ResolveVisibleFrameSize(effects.GroundBladeClip);

        Assert.That(generatedSprites, Is.Empty);
        Assert.That(groundFrames, Has.Length.EqualTo(20));
        Assert.That(rainFrames, Has.Length.EqualTo(23));
        Assert.That(groundFrames[0], Is.SameAs(importedGroundFrames[0]));
        Assert.That(rainFrames[0], Is.SameAs(importedRainFrames[0]));
        Assert.That(rainFrames[22], Is.SameAs(importedRainFrames[22]));
        Assert.That(groundVisibleSize.x, Is.EqualTo(1.87f).Within(0.001f));
        Assert.That(groundVisibleSize.y, Is.EqualTo(10.09f).Within(0.001f));

        GridSpriteSheetUtility.DestroyGeneratedSprites(generatedSprites);
    }

    [UnityTest]
    public IEnumerator EffectController_NormalSlashOffsetIsFacingRelative()
    {
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "slashSpriteSheet", CreateTexture("SlashFacing", 10, 12));
        SetPrivateField(effects, "slashOffset", new Vector3(0.5f, 0.25f, 0f));

        effects.PlayNormalSlash(Vector2.zero, Vector2.one, 0f, 1);
        yield return null;
        Transform rightSlash = FindTransformNamed("LastBossSlashEffect");
        Assert.That(rightSlash, Is.Not.Null);
        Assert.That(rightSlash.position.x, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(rightSlash.position.y, Is.EqualTo(0.25f).Within(0.001f));
        Object.DestroyImmediate(rightSlash.gameObject);

        effects.PlayNormalSlash(Vector2.zero, Vector2.one, 0f, -1);
        yield return null;
        Transform leftSlash = FindTransformNamed("LastBossSlashEffect");
        Assert.That(leftSlash, Is.Not.Null);
        Assert.That(leftSlash.position.x, Is.EqualTo(-0.5f).Within(0.001f));
        Assert.That(leftSlash.position.y, Is.EqualTo(0.25f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator EffectController_StopNormalSlashDestroysActiveSlash()
    {
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "slashSpriteSheet", CreateTexture("SlashStop", 10, 12));

        effects.PlayNormalSlash(Vector2.zero, Vector2.one, 0f, 1);
        yield return null;
        Assert.That(FindTransformNamed("LastBossSlashEffect"), Is.Not.Null);

        effects.StopNormalSlash();
        yield return null;

        Assert.That(FindTransformNamed("LastBossSlashEffect"), Is.Null);
    }

    [UnityTest]
    public IEnumerator EffectController_NormalSlashKeepsFixedUniformScaleAcrossFrames()
    {
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "slashSpriteSheet", CreateTexture("SlashFullGrid", 50, 60));
        SetPrivateField(effects, "slashSpriteFrames", new[]
        {
            CreateSprite("SlashSquareFrame", 100, 100, 100f),
            CreateSprite("SlashFlatFrame", 95, 75, 100f)
        });
        SetPrivateField(effects, "slashSizeMultiplier", 1.1f);

        effects.PlayNormalSlash(Vector2.zero, Vector2.one, 0f, 1);
        yield return null;
        Transform slash = FindTransformNamed("LastBossSlashEffect");
        Assert.That(slash, Is.Not.Null);
        Assert.That(slash.localScale.x, Is.EqualTo(1.1f).Within(0.001f));
        Assert.That(slash.localScale.y, Is.EqualTo(1.1f).Within(0.001f));

        SpriteRenderer renderer = slash.GetComponent<SpriteRenderer>();
        Assert.That(renderer, Is.Not.Null);
        Assert.That(renderer.sprite, Is.Not.Null);
        Assert.That(renderer.sprite.textureRect, Is.EqualTo(new Rect(0f, 50f, 10f, 10f)));

        yield return new WaitForSecondsRealtime((1f / 30f) + 0.05f);
        Assert.That(slash.localScale.x, Is.EqualTo(1.1f).Within(0.001f));
        Assert.That(slash.localScale.y, Is.EqualTo(1.1f).Within(0.001f));
        Assert.That(renderer.sprite, Is.Not.Null);
        Assert.That(renderer.sprite.textureRect, Is.EqualTo(new Rect(10f, 50f, 10f, 10f)));
    }

    [UnityTest]
    public IEnumerator LastBoss_HideAttackVisualDoesNotStopNormalSlashEffect()
    {
        GameObject bossObject = CreateObject("LastBossSlashOwner", Vector2.zero);
        bossObject.SetActive(false);
        bossObject.AddComponent<SpriteRenderer>();
        bossObject.AddComponent<BoxCollider2D>();
        bossObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
        LastBossEffectController effects = bossObject.AddComponent<LastBossEffectController>();
        SetPrivateField(effects, "slashSpriteSheet", CreateTexture("SlashSurvivesAttackHide", 50, 60));
        LastBossController boss = bossObject.AddComponent<LastBossController>();
        bossObject.SetActive(true);
        InvokePrivate(effects, "Awake");
        InvokePrivate(boss, "Awake");

        effects.PlayNormalSlash(Vector2.zero, Vector2.one, 0f, 1);
        yield return null;
        Assert.That(FindTransformNamed("LastBossSlashEffect"), Is.Not.Null);

        InvokePrivate(boss, "HideAttackVisual");
        yield return null;

        Assert.That(FindTransformNamed("LastBossSlashEffect"), Is.Not.Null);
    }

    [Test]
    public void LastBoss_NormalAttackBoxOverlapsTowardBossByConfiguredInset()
    {
        GameObject bossObject = CreateObject("LastBoss", Vector2.zero);
        bossObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
        BoxCollider2D collider = bossObject.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        LastBossController boss = bossObject.AddComponent<LastBossController>();
        InvokePrivate(boss, "Awake");
        SetPrivateField(boss, "normalAttackSize", new Vector2(4f, 4f));
        SetPrivateField(boss, "normalAttackForwardInset", 2f);

        SetPrivateField(boss, "facingDirection", 1);
        object rightAttackBox = InvokePrivate(boss, "BuildAttackBox", GetBossAction("Normal"));
        Assert.That(GetAttackBoxCenter(rightAttackBox).x, Is.EqualTo(0.5f).Within(0.001f));

        SetPrivateField(boss, "facingDirection", -1);
        object leftAttackBox = InvokePrivate(boss, "BuildAttackBox", GetBossAction("Normal"));
        Assert.That(GetAttackBoxCenter(leftAttackBox).x, Is.EqualTo(-0.5f).Within(0.001f));
    }

    [Test]
    public void LastBoss_RainBladePreviewSpawnsOutsideFacingBottomCornerAndKeepsSlotSpacing()
    {
        GameObject bossObject = CreateObject("LastBossRainPreviewSlots", Vector2.zero);
        LastBossEffectController effects = bossObject.AddComponent<LastBossEffectController>();
        LastBossController boss = bossObject.AddComponent<LastBossController>();
        SetPrivateField(boss, "effectController", effects);
        SetPrivateField(boss, "verticalRainBladeCount", 3);
        SetPrivateField(effects, "magicCircleWorldSize", new Vector2(4f, 4f));

        GameObject magicCircleObject = CreateObject("MagicCircleActive", Vector2.zero);
        SetPrivateField(effects, "magicCircleObject", magicCircleObject);

        object attackBox = CreateAttackBox(Vector2.zero, new Vector2(6f, 6f), 0f);

        SetPrivateField(boss, "facingDirection", -1);
        effects.SetFacingDirection(-1);
        GetRainBladeSlotPoints(boss, attackBox, 0, out Vector2 leftSlot0, out _, out Vector2 leftAim0);
        GetRainBladeSlotPoints(boss, attackBox, 1, out Vector2 leftSlot1, out _, out Vector2 leftAim1);
        GetRainBladeSlotPoints(boss, attackBox, 2, out Vector2 leftSlot2, out _, out Vector2 leftAim2);
        Assert.That(Vector2.Distance(leftSlot1, new Vector2(-8f / 3f, -2f)), Is.LessThan(0.001f));
        Assert.That(leftSlot1 - leftSlot0, Is.EqualTo(new Vector2(3f, 0f)));
        Assert.That(leftSlot2 - leftSlot1, Is.EqualTo(new Vector2(3f, 0f)));
        Assert.That(leftAim0 - leftSlot0, Is.EqualTo(leftAim1 - leftSlot1));
        Assert.That(leftAim1 - leftSlot1, Is.EqualTo(leftAim2 - leftSlot2));
        Assert.That(Vector2.Distance(leftAim1 - leftSlot1, new Vector2(8f / 3f, 5f)), Is.LessThan(0.001f));

        SetPrivateField(boss, "facingDirection", 1);
        effects.SetFacingDirection(1);
        GetRainBladeSlotPoints(boss, attackBox, 0, out Vector2 rightSlot0, out _, out Vector2 rightAim0);
        GetRainBladeSlotPoints(boss, attackBox, 1, out Vector2 rightSlot1, out _, out Vector2 rightAim1);
        GetRainBladeSlotPoints(boss, attackBox, 2, out Vector2 rightSlot2, out _, out Vector2 rightAim2);
        Assert.That(Vector2.Distance(rightSlot1, new Vector2(8f / 3f, -2f)), Is.LessThan(0.001f));
        Assert.That(rightSlot1 - rightSlot0, Is.EqualTo(new Vector2(3f, 0f)));
        Assert.That(rightSlot2 - rightSlot1, Is.EqualTo(new Vector2(3f, 0f)));
        Assert.That(rightAim0 - rightSlot0, Is.EqualTo(rightAim1 - rightSlot1));
        Assert.That(rightAim1 - rightSlot1, Is.EqualTo(rightAim2 - rightSlot2));
        Assert.That(Vector2.Distance(rightAim1 - rightSlot1, new Vector2(-8f / 3f, 5f)), Is.LessThan(0.001f));
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

        blade.ConfigureGroundVisual(clip, uprightFrameIndex: 3, slotIndex: 0, frameSizeMultiplier: Vector2.one);
        Vector3 startScale = bladeObject.transform.localScale;
        Vector2 colliderSize = collider.size;
        blade.InitializeGround(null, 1, groundY: 0f, riseDuration: 0f);

        Assert.That(collider.enabled, Is.False);
        yield return new WaitForSecondsRealtime((4f / 30f) + 0.05f);

        Assert.That(collider.enabled, Is.True);
        Assert.That(bladeObject.transform.localScale, Is.EqualTo(startScale));
        Assert.That(collider.size, Is.EqualTo(colliderSize));
        Assert.That(FindChildRenderer(bladeObject, "BladeEffectVisual"), Is.Not.Null);
    }

    [UnityTest]
    public IEnumerator GroundBladeVisual_KeepsRisenTopThroughVanishFrames()
    {
        GameObject bladeObject = CreateObject("GroundBladeGrounded", Vector2.zero);
        BoxCollider2D collider = bladeObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1f, 2f);
        LastBossBladeAttack blade = bladeObject.AddComponent<LastBossBladeAttack>();
        InvokePrivate(blade, "Awake");
        GridSpriteSheetClip clip = CreateSpriteClip(new[]
        {
            CreateSprite("GroundBladeBottomFrame", 50, 50, 100f),
            CreateSprite("GroundBladeRiseFrameA", 75, 75, 100f),
            CreateSprite("GroundBladeRiseFrameB", 90, 90, 100f),
            CreateSprite("GroundBladeUprightFrame", 100, 100, 100f),
            CreateSprite("GroundBladeVanishFrame", 50, 50, 100f)
        }, 30f);
        const float groundY = 1.25f;

        blade.ConfigureGroundVisual(clip, uprightFrameIndex: 3, slotIndex: 0, frameSizeMultiplier: Vector2.one);
        blade.InitializeGround(null, 1, groundY, riseDuration: 0f);
        yield return null;

        SpriteRenderer visualRenderer = FindChildRenderer(bladeObject, "BladeEffectVisual");
        Assert.That(visualRenderer, Is.Not.Null);
        Assert.That(visualRenderer.sprite, Is.Not.Null);
        Assert.That(visualRenderer.bounds.min.y, Is.EqualTo(groundY).Within(0.001f));

        yield return new WaitForSecondsRealtime((3f / 30f) + 0.05f);

        Assert.That(visualRenderer.sprite.name, Is.EqualTo("GroundBladeUprightFrame"));
        Assert.That(visualRenderer.bounds.min.y, Is.EqualTo(groundY).Within(0.001f));
        float risenTopY = visualRenderer.bounds.max.y;

        yield return new WaitForSecondsRealtime((1f / 30f) + 0.05f);

        Assert.That(visualRenderer.sprite.name, Is.EqualTo("GroundBladeVanishFrame"));
        Assert.That(visualRenderer.bounds.max.y, Is.EqualTo(risenTopY).Within(0.001f));
        Assert.That(visualRenderer.bounds.min.y, Is.GreaterThan(groundY));

        yield return new WaitForSecondsRealtime((1f / 30f) + 0.05f);

        Assert.That(visualRenderer.enabled, Is.False);
        Assert.That(visualRenderer.sprite, Is.Null);
    }

    [UnityTest]
    public IEnumerator GroundBladeVisual_SizingOnlyCropPreservesVisibleAspectAndCollider()
    {
        GameObject bladeObject = CreateObject("GroundBladeAspect", Vector2.zero);
        BoxCollider2D collider = bladeObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1f, 2f);
        LastBossBladeAttack blade = bladeObject.AddComponent<LastBossBladeAttack>();
        InvokePrivate(blade, "Awake");
        GridSpriteSheetClip clip = CreateClip(CreateTexture("UnderAttackStableFrame", 20, 10), 2, 1, 2, 30f);
        clip.UseFrameCrop = true;
        clip.UseFrameCropForSizingOnly = true;
        clip.FrameCropPixels = new RectInt(6, 2, 2, 6);

        blade.ConfigureGroundVisual(clip, uprightFrameIndex: 0, slotIndex: 0, frameSizeMultiplier: Vector2.one);
        Vector2 colliderSize = collider.size;
        blade.InitializeGround(null, 1, groundY: 0f, riseDuration: 0f);
        yield return null;

        SpriteRenderer visualRenderer = FindChildRenderer(bladeObject, "BladeEffectVisual");
        Assert.That(visualRenderer, Is.Not.Null);
        Assert.That(visualRenderer.sprite, Is.Not.Null);
        Assert.That(visualRenderer.sprite.textureRect, Is.EqualTo(new Rect(0f, 0f, 10f, 10f)));
        Assert.That(visualRenderer.transform.localScale.x, Is.EqualTo(visualRenderer.transform.localScale.y).Within(0.001f));
        Assert.That(collider.size, Is.EqualTo(colliderSize));
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

        blade.ConfigureRainVisual(inClip, outClip, frameSizeMultiplier: Vector2.one);
        blade.InitializeRainPreview(
            null,
            1,
            targetPoint: Vector2.zero,
            previewAimPoint: Vector2.down,
            fallSpeed: 20f,
            groundDestroyDelay: 10f);

        yield return new WaitForSecondsRealtime(inClip.DurationSeconds + 0.05f);
        Vector3 startScale = bladeObject.transform.localScale;
        Vector2 colliderSize = collider.size;
        SpriteRenderer visualRenderer = FindChildRenderer(bladeObject, "BladeEffectVisual");
        Assert.That(visualRenderer, Is.Not.Null);
        Assert.That(visualRenderer.sprite, Is.Not.Null);

        blade.ReleaseRainBlade();
        InvokePrivate(blade, "MoveRainBlade");

        Assert.That(collider.enabled, Is.False);
        Assert.That(bladeObject.transform.localScale, Is.EqualTo(startScale));
        Assert.That(collider.size, Is.EqualTo(colliderSize));
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
    public void EffectController_DeathBoundsUseRendererWhenColliderDisabled()
    {
        GameObject bossObject = CreateObject("LastBossDeathBounds", Vector2.zero);
        SpriteRenderer renderer = bossObject.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSprite("LastBossDeathBody", 100, 80, 10f);
        BoxCollider2D collider = bossObject.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;
        LastBossEffectController effects = bossObject.AddComponent<LastBossEffectController>();
        InvokePrivate(effects, "Awake");

        collider.enabled = false;
        Bounds bounds = (Bounds)InvokePrivate(effects, "ResolveBossBounds");

        Assert.That(bounds.size.x, Is.EqualTo(10f).Within(0.001f));
        Assert.That(bounds.size.y, Is.EqualTo(8f).Within(0.001f));
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

    private Sprite CreateSprite(string name, int width, int height, float pixelsPerUnit)
    {
        Texture2D texture = CreateTexture($"{name}Texture", width, height);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit);
        sprite.name = name;
        objectsToDestroy.Add(sprite);
        return sprite;
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

    private static GridSpriteSheetClip CreateSpriteClip(Sprite[] sprites, float framesPerSecond)
    {
        return new GridSpriteSheetClip
        {
            SpriteFrames = sprites,
            FramesPerSecond = framesPerSecond
        };
    }

    private static Sprite[] LoadSortedSpriteFrames(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
        var sprites = new List<Sprite>();
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                sprites.Add(sprite);
            }
        }

        sprites.Sort(CompareSpriteNames);
        return sprites.ToArray();
    }

    private static Sprite[] LoadPrimarySpriteFramesByGrid(string assetPath, int columns, int rows, int frameCount)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        Assert.That(texture, Is.Not.Null);

        Sprite[] sprites = LoadSortedSpriteFrames(assetPath);
        int maxFrameCount = Mathf.Min(frameCount, columns * rows);
        Sprite[] primaryFrames = new Sprite[maxFrameCount];
        float[] primaryFrameAreas = new float[maxFrameCount];
        float cellWidth = texture.width / (float)columns;
        float cellHeight = texture.height / (float)rows;

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
            {
                continue;
            }

            Rect rect = sprite.rect;
            int column = Mathf.Clamp(Mathf.FloorToInt(rect.center.x / cellWidth), 0, columns - 1);
            int rowFromBottom = Mathf.Clamp(Mathf.FloorToInt(rect.center.y / cellHeight), 0, rows - 1);
            int row = rows - 1 - rowFromBottom;
            int frameIndex = row * columns + column;
            if (frameIndex < 0 || frameIndex >= maxFrameCount)
            {
                continue;
            }

            float area = rect.width * rect.height;
            if (primaryFrames[frameIndex] == null || area > primaryFrameAreas[frameIndex])
            {
                primaryFrames[frameIndex] = sprite;
                primaryFrameAreas[frameIndex] = area;
            }
        }

        var frames = new List<Sprite>(maxFrameCount);
        for (int i = 0; i < primaryFrames.Length; i++)
        {
            if (primaryFrames[i] != null)
            {
                frames.Add(primaryFrames[i]);
            }
        }

        return frames.ToArray();
    }

    private static int CompareSpriteNames(Sprite left, Sprite right)
    {
        string leftName = left != null ? left.name : string.Empty;
        string rightName = right != null ? right.name : string.Empty;
        bool leftHasNumber = TryReadTrailingNumber(leftName, out int leftNumber);
        bool rightHasNumber = TryReadTrailingNumber(rightName, out int rightNumber);

        if (leftHasNumber && rightHasNumber && leftNumber != rightNumber)
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.Compare(leftName, rightName, StringComparison.Ordinal);
    }

    private static bool TryReadTrailingNumber(string text, out int value)
    {
        value = 0;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        int start = text.Length - 1;
        while (start >= 0 && char.IsDigit(text[start]))
        {
            start--;
        }

        start++;
        return start < text.Length && int.TryParse(text.Substring(start), out value);
    }

    private static Vector2 ResolveLargestSpriteBounds(Sprite[] sprites)
    {
        Vector2 largestSize = Vector2.zero;
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null)
            {
                continue;
            }

            Vector2 frameSize = sprites[i].bounds.size;
            largestSize.x = Mathf.Max(largestSize.x, frameSize.x);
            largestSize.y = Mathf.Max(largestSize.y, frameSize.y);
        }

        return largestSize;
    }

    private static Vector2 ResolveCombinedFrameBoundsPivot(
        Texture2D texture,
        Sprite[] sprites,
        int columns,
        int rows,
        int frameIndex)
    {
        int frameWidth = texture.width / columns;
        int frameHeight = texture.height / rows;
        int column = frameIndex % columns;
        int row = frameIndex / columns;
        int rowFromBottom = rows - 1 - row;
        Rect cell = new Rect(column * frameWidth, rowFromBottom * frameHeight, frameWidth, frameHeight);
        Rect bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null || sprite.texture != texture || !cell.Contains(sprite.rect.center))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = sprite.rect;
                hasBounds = true;
                continue;
            }

            float minX = Mathf.Min(bounds.xMin, sprite.rect.xMin);
            float minY = Mathf.Min(bounds.yMin, sprite.rect.yMin);
            float maxX = Mathf.Max(bounds.xMax, sprite.rect.xMax);
            float maxY = Mathf.Max(bounds.yMax, sprite.rect.yMax);
            bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        Assert.That(hasBounds, Is.True, $"Expected imported bounds for frame {frameIndex}.");
        return new Vector2(
            Mathf.Clamp01((bounds.center.x - cell.xMin) / cell.width),
            Mathf.Clamp01((bounds.center.y - cell.yMin) / cell.height));
    }

    private static void AssertSpriteFrameCount(LastBossEffectController effects, string fieldName, int expectedCount)
    {
        Sprite[] frames = GetPrivateField<Sprite[]>(effects, fieldName);
        Assert.That(frames, Is.Not.Null, $"{fieldName} should be serialized on the LastBoss prefab.");
        Assert.That(frames, Has.Length.EqualTo(expectedCount), $"{fieldName} should match the playable frame count.");
        for (int i = 0; i < frames.Length; i++)
        {
            Assert.That(frames[i], Is.Not.Null, $"{fieldName}[{i}] should be assigned.");
        }
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

    private static Transform FindTransformNamed(string objectName)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == objectName)
            {
                return transforms[i];
            }
        }

        return null;
    }

    private static SpriteRenderer FindRendererNamed(string objectName)
    {
        Transform transform = FindTransformNamed(objectName);
        return transform != null ? transform.GetComponent<SpriteRenderer>() : null;
    }

    private static SpriteRenderer FindChildRenderer(GameObject root, string childName)
    {
        Transform child = root != null ? root.transform.Find(childName) : null;
        return child != null ? child.GetComponent<SpriteRenderer>() : null;
    }

    private static object GetBossAction(string actionName)
    {
        Type actionType = typeof(LastBossController).GetNestedType("BossAction", BindingFlags.NonPublic);
        Assert.That(actionType, Is.Not.Null, "BossAction must exist.");
        return Enum.Parse(actionType, actionName);
    }

    private static object CreateAttackBox(Vector2 center, Vector2 size, float angle)
    {
        Type attackBoxType = typeof(LastBossController).GetNestedType("AttackBox", BindingFlags.NonPublic);
        Assert.That(attackBoxType, Is.Not.Null, "AttackBox must exist.");
        ConstructorInfo constructor = attackBoxType.GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(Vector2), typeof(Vector2), typeof(float) },
            null);
        Assert.That(constructor, Is.Not.Null, "AttackBox constructor must exist.");
        return constructor.Invoke(new object[] { center, size, angle });
    }

    private static void GetRainBladeSlotPoints(
        LastBossController boss,
        object attackBox,
        int slotIndex,
        out Vector2 spawnPosition,
        out Vector2 targetPoint,
        out Vector2 previewAimPoint)
    {
        object[] arguments =
        {
            attackBox,
            slotIndex,
            Vector2.zero,
            Vector2.zero,
            Vector2.zero,
            true
        };
        InvokePrivate(boss, "GetRainBladeSlotPoints", arguments);
        spawnPosition = (Vector2)arguments[2];
        targetPoint = (Vector2)arguments[3];
        previewAimPoint = (Vector2)arguments[4];
    }

    private static Vector2 GetAttackBoxCenter(object attackBox)
    {
        PropertyInfo centerProperty = attackBox.GetType().GetProperty("Center", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(centerProperty, Is.Not.Null, "AttackBox.Center must exist.");
        return (Vector2)centerProperty.GetValue(attackBox);
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
