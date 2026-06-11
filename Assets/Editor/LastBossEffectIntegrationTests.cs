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
        Assert.That(auraRenderer.transform.position.x, Is.EqualTo(-0.35f).Within(0.001f));

        effects.SetFacingDirection(1);
        yield return null;
        Assert.That(auraRenderer.flipX, Is.False);
        Assert.That(auraRenderer.transform.position.x, Is.EqualTo(0.35f).Within(0.001f));
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
        Assert.That(auraRenderer.transform.position.x, Is.EqualTo(-0.35f).Within(0.001f));
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

    [UnityTest]
    public IEnumerator EffectController_RangeIndicatorUsesVisibleArtCropAndConfiguredOffset()
    {
        Texture2D range = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Sprites/Effects/eff_range.png");
        Assert.That(range, Is.Not.Null);
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "rangeSpriteSheet", range);
        SetPrivateField(effects, "rangeOffset", new Vector3(0f, -0.35f, 0f));

        effects.BeginHorizontalRangeCharge(new List<Vector2> { Vector2.zero }, 2f);
        yield return null;

        SpriteRenderer rangeRenderer = FindRendererNamed("LastBossRangeIndicator");
        Assert.That(rangeRenderer, Is.Not.Null);
        Assert.That(rangeRenderer.sprite, Is.Not.Null);
        Assert.That(
            rangeRenderer.sprite.textureRect,
            Is.EqualTo(ExpectedFrameRect(range, 10, 9, 0, new RectInt(98, 0, 316, 214), new Vector2Int(512, 512))));
        Assert.That(rangeRenderer.transform.position.y, Is.EqualTo(-0.35f).Within(0.001f));
    }

    [UnityTest]
    public IEnumerator EffectController_ShieldUsesPaddedStableCrops()
    {
        Texture2D shieldIn = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Sprites/Effects/eff_boss_shield_in.png");
        Texture2D shieldLoop = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Sprites/Effects/eff_boss_shield.png");
        Assert.That(shieldIn, Is.Not.Null);
        Assert.That(shieldLoop, Is.Not.Null);
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "shieldInSpriteSheet", shieldIn);
        SetPrivateField(effects, "shieldLoopSpriteSheet", shieldLoop);

        effects.HandleEncounterStarted();
        yield return null;

        SpriteRenderer shieldRenderer = FindRendererNamed("LastBossShieldEffect");
        Assert.That(shieldRenderer, Is.Not.Null);
        Assert.That(shieldRenderer.sprite, Is.Not.Null);
        Assert.That(
            shieldRenderer.sprite.textureRect,
            Is.EqualTo(ExpectedFrameRect(shieldIn, 5, 2, 0, new RectInt(97, 97, 830, 830), new Vector2Int(1024, 1024))));

        yield return new WaitForSecondsRealtime((10f / 30f) + 0.1f);

        GridSpriteSheetPlayer shieldPlayer = shieldRenderer.GetComponent<GridSpriteSheetPlayer>();
        Assert.That(shieldPlayer, Is.Not.Null);
        Assert.That(shieldRenderer.sprite, Is.Not.Null);
        Assert.That(
            shieldRenderer.sprite.textureRect,
            Is.EqualTo(ExpectedFrameRect(shieldLoop, 5, 12, shieldPlayer.CurrentFrameIndex, new RectInt(94, 94, 836, 836), new Vector2Int(1024, 1024))));
    }

    [UnityTest]
    public IEnumerator EffectController_DeathUsesSquareStableCrop()
    {
        Texture2D death = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Sprites/Effects/eff_boss_Destroy.png");
        Assert.That(death, Is.Not.Null);
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "deathSpriteSheet", death);

        bool played = effects.PlayDeath(null, null);
        Assert.That(played, Is.True);
        yield return null;

        SpriteRenderer deathRenderer = FindRendererNamed("LastBossDestroyEffect");
        Assert.That(deathRenderer, Is.Not.Null);
        Assert.That(deathRenderer.sprite, Is.Not.Null);
        Assert.That(
            deathRenderer.sprite.textureRect,
            Is.EqualTo(ExpectedFrameRect(death, 5, 9, 0, new RectInt(20, 20, 984, 984), new Vector2Int(1024, 1024))));
    }

    [Test]
    public void EffectController_BladeClipsUseVisibleArtCrops()
    {
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "underAttackSpriteSheet", CreateTexture("UnderAttackClipCrop", 10, 8));
        SetPrivateField(effects, "topAttackInSpriteSheet", CreateTexture("TopInClipCrop", 10, 10));
        SetPrivateField(effects, "topAttackOutSpriteSheet", CreateTexture("TopOutClipCrop", 10, 8));

        Assert.That(effects.GroundBladeClip.UseFrameCrop, Is.True);
        Assert.That(effects.GroundBladeClip.FrameCropPixels, Is.EqualTo(new RectInt(418, 0, 187, 1009)));
        Assert.That(effects.GroundBladeClip.FrameCropReferencePixels, Is.EqualTo(new Vector2Int(1024, 1024)));
        Assert.That(effects.RainBladeInClip.UseFrameCrop, Is.True);
        Assert.That(effects.RainBladeInClip.FrameCropPixels, Is.EqualTo(new RectInt(405, 20, 217, 995)));
        Assert.That(effects.RainBladeInClip.FrameCropReferencePixels, Is.EqualTo(new Vector2Int(1024, 1024)));
        Assert.That(effects.RainBladeOutClip.UseFrameCrop, Is.True);
        Assert.That(effects.RainBladeOutClip.FrameCropPixels, Is.EqualTo(new RectInt(405, 20, 217, 995)));
        Assert.That(effects.RainBladeOutClip.FrameCropReferencePixels, Is.EqualTo(new Vector2Int(1024, 1024)));
    }

    [Test]
    public void EffectController_BladeCropsScaleAgainstImportedTextureSize()
    {
        Texture2D underAttack = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Sprites/Effects/eff_under_attack.png");
        Texture2D topAttackIn = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Sprites/Effects/eff_top_attack_in.png");
        Assert.That(underAttack, Is.Not.Null);
        Assert.That(topAttackIn, Is.Not.Null);
        LastBossEffectController effects = CreateEffectController();
        SetPrivateField(effects, "underAttackSpriteSheet", underAttack);
        SetPrivateField(effects, "topAttackInSpriteSheet", topAttackIn);

        var generatedSprites = new List<Sprite>();
        Sprite[] groundFrames = GridSpriteSheetUtility.BuildFrames(effects.GroundBladeClip, generatedSprites);
        Sprite[] rainFrames = GridSpriteSheetUtility.BuildFrames(effects.RainBladeInClip, generatedSprites);

        float groundFrameWidth = underAttack.width / 5f;
        float groundFrameHeight = underAttack.height / 4f;
        Assert.That(groundFrames[0].textureRect.width, Is.EqualTo(Mathf.RoundToInt(187f * groundFrameWidth / 1024f)).Within(1f));
        Assert.That(groundFrames[0].textureRect.height, Is.EqualTo(Mathf.RoundToInt(1009f * groundFrameHeight / 1024f)).Within(1f));
        Assert.That(groundFrames[0].textureRect.x, Is.LessThan(groundFrameWidth - groundFrames[0].textureRect.width));

        float rainFrameWidth = topAttackIn.width / 5f;
        Assert.That(rainFrames[0].textureRect.width, Is.EqualTo(Mathf.RoundToInt(217f * rainFrameWidth / 1024f)).Within(1f));
        Assert.That(rainFrames[0].textureRect.x, Is.LessThan(rainFrameWidth - rainFrames[0].textureRect.width));

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

    private static Rect ExpectedFrameRect(
        Texture2D texture,
        int columns,
        int rows,
        int frameIndex,
        RectInt crop,
        Vector2Int referenceSize)
    {
        int frameWidth = texture.width / columns;
        int frameHeight = texture.height / rows;
        int row = frameIndex / columns;
        int column = frameIndex % columns;
        RectInt scaledCrop = ExpectedScaledCrop(crop, referenceSize, frameWidth, frameHeight);
        int y = texture.height - ((row + 1) * frameHeight);

        return new Rect(
            (column * frameWidth) + scaledCrop.x,
            y + scaledCrop.y,
            scaledCrop.width,
            scaledCrop.height);
    }

    private static RectInt ExpectedScaledCrop(
        RectInt crop,
        Vector2Int referenceSize,
        int frameWidth,
        int frameHeight)
    {
        float scaleX = frameWidth / (float)referenceSize.x;
        float scaleY = frameHeight / (float)referenceSize.y;
        return new RectInt(
            Mathf.RoundToInt(crop.x * scaleX),
            Mathf.RoundToInt(crop.y * scaleY),
            Mathf.Max(1, Mathf.RoundToInt(crop.width * scaleX)),
            Mathf.Max(1, Mathf.RoundToInt(crop.height * scaleY)));
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
