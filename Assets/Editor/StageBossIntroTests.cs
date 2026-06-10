using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GameName.Enemy;
using NUnit.Framework;
using Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class StageBossIntroTests
{
    private const BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<Object> objectsToDestroy = new List<Object>();
    private Action<BossAreaController> encounterStartedHandler;

    [TearDown]
    public void TearDown()
    {
        if (encounterStartedHandler != null)
        {
            BossAreaController.EncounterStarted -= encounterStartedHandler;
            encounterStartedHandler = null;
        }

        GameProgressFlags.ClearAll();

        for (int i = objectsToDestroy.Count - 1; i >= 0; i--)
        {
            if (objectsToDestroy[i] != null)
            {
                Object.DestroyImmediate(objectsToDestroy[i]);
            }
        }

        objectsToDestroy.Clear();
    }

    [UnityTest]
    [Timeout(3000)]
    public IEnumerator Awake_WhenStageBossIntroEnabled_HidesStageBossUntilIntro()
    {
        // 入室前はStageBossだけが見えず、当たり判定も動かないことを確認する。
        StageBossAttack stageBoss = CreateStageBoss(Vector2.zero, out SpriteRenderer renderer, out Collider2D collider, out Rigidbody2D rigidbody2D);

        GameObject areaObject = CreateInactiveBossAreaObject();
        BossAreaController bossArea = areaObject.GetComponent<BossAreaController>();
        ConfigureStageBossArea(bossArea, stageBoss, revealDuration: 0.05f, hpLeadInSeconds: 0.05f);

        InvokePrivate(bossArea, "Awake");
        yield return null;

        Assert.That(renderer.enabled, Is.False);
        Assert.That(collider.enabled, Is.False);
        Assert.That(rigidbody2D.simulated, Is.False);
        Assert.That(stageBoss.IsEncounterActive, Is.False);
    }

    [Test]
    public void TryStartEncounter_ForStageBoss_DelaysHpEventAndActivationUntilIntroCompletes()
    {
        // 2秒待ち・接地後ロック・ミラージュ・HP表示・BGM/AI開始の順序をまとめて検証する。
        Rigidbody2D playerRigidbody = CreatePlayer(new Vector2(-2f, 0f), out PlayerController playerController);
        playerRigidbody.linearVelocity = new Vector2(3f, 1f);
        StageBgmController stageBgm = CreateStageBgm(out AudioSource bgmSource, out AudioClip bossBgm);
        StageBossAttack stageBoss = CreateStageBoss(new Vector2(2f, 0f), out SpriteRenderer renderer, out Collider2D bossCollider, out Rigidbody2D bossRigidbody2D);
        EnemyController enemyController = stageBoss.GetComponent<EnemyController>();

        GameObject areaObject = CreateInactiveBossAreaObject();
        BossAreaController bossArea = areaObject.GetComponent<BossAreaController>();
        ConfigureStageBossArea(
            bossArea,
            stageBoss,
            revealDuration: 1f,
            hpLeadInSeconds: 0.05f,
            entryDelaySeconds: 0.05f,
            stageBgm: stageBgm,
            bossBgm: bossBgm);
        InvokePrivate(bossArea, "Awake");

        int encounterStartedCount = 0;
        encounterStartedHandler = _ => encounterStartedCount++;
        BossAreaController.EncounterStarted += encounterStartedHandler;

        IEnumerator routine = (IEnumerator)InvokePrivate(bossArea, "PlayStageBossIntroRoutine");

        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(encounterStartedCount, Is.EqualTo(0));
        Assert.That(stageBoss.IsEncounterActive, Is.False);
        Assert.That(playerRigidbody.constraints, Is.EqualTo(RigidbodyConstraints2D.None));
        Assert.That(playerController.IsExternalControlLocked, Is.False);
        Assert.That(bgmSource.clip, Is.Not.EqualTo(bossBgm));

        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(encounterStartedCount, Is.EqualTo(0));
        Assert.That(stageBoss.IsEncounterActive, Is.False);
        Assert.That(playerRigidbody.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeAll));
        Assert.That(playerRigidbody.linearVelocity, Is.EqualTo(Vector2.zero));
        Assert.That(playerController.IsExternalControlLocked, Is.True);
        Assert.That(playerController.IsExternalFacingLocked, Is.True);
        Assert.That(playerController.IsFacingRight, Is.True);
        Assert.That(enemyController.FacingDirection, Is.EqualTo(-1));
        Assert.That(bgmSource.clip, Is.Not.EqualTo(bossBgm));

        IEnumerator revealEnumerator = routine.Current as IEnumerator;
        Assert.That(revealEnumerator, Is.Not.Null);
        Assert.That(revealEnumerator.MoveNext(), Is.True);
        Assert.That(renderer.color.a, Is.LessThan(1f));
        Assert.That(HasVisibleMirageGhost(stageBoss.transform), Is.True);

        RunNestedEnumerator(revealEnumerator);
        Assert.That(HasMirageGhost(stageBoss.transform), Is.False);
        Assert.That(renderer.color.a, Is.EqualTo(1f).Within(0.001f));

        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(encounterStartedCount, Is.EqualTo(1));
        Assert.That(stageBoss.IsEncounterActive, Is.False);
        Assert.That(bossCollider.enabled, Is.False);
        Assert.That(bossRigidbody2D.simulated, Is.False);
        Assert.That(bgmSource.clip, Is.Not.EqualTo(bossBgm));

        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(stageBoss.IsEncounterActive, Is.True);
        Assert.That(enemyController.FacingDirection, Is.EqualTo(-1));
        Assert.That(bossCollider.enabled, Is.True);
        Assert.That(bossRigidbody2D.simulated, Is.True);
        Assert.That(bgmSource.clip, Is.EqualTo(bossBgm));

        BossAreaController.EncounterStarted -= encounterStartedHandler;
        encounterStartedHandler = null;
    }

    [Test]
    public void TryStartEncounter_ForStageBoss_FreezesPlayerDuringIntro()
    {
        // ロック開始後にプレイヤー入力・速度・向きが演出用に固定されることを確認する。
        Rigidbody2D playerRigidbody = CreatePlayer(new Vector2(-2f, 0f), out PlayerController playerController);
        playerRigidbody.linearVelocity = new Vector2(3f, 1f);

        StageBossAttack stageBoss = CreateStageBoss(new Vector2(2f, 0f), out _, out _, out _);
        GameObject areaObject = CreateInactiveBossAreaObject();
        BossAreaController bossArea = areaObject.GetComponent<BossAreaController>();
        ConfigureStageBossArea(bossArea, stageBoss, revealDuration: 0f, hpLeadInSeconds: 0.05f, entryDelaySeconds: 0.05f);
        InvokePrivate(bossArea, "Awake");
        InvokePrivate(bossArea, "CachePlayerReferences");

        IEnumerator routine = (IEnumerator)InvokePrivate(bossArea, "PlayStageBossIntroRoutine");

        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(playerRigidbody.constraints, Is.EqualTo(RigidbodyConstraints2D.None));
        Assert.That(playerController.IsExternalControlLocked, Is.False);

        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(playerRigidbody.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeAll));
        Assert.That(playerRigidbody.linearVelocity, Is.EqualTo(Vector2.zero));
        Assert.That(playerController.IsExternalControlLocked, Is.True);
        Assert.That(playerController.IsExternalFacingLocked, Is.True);
        Assert.That(playerController.IsFacingRight, Is.True);

        RunNestedEnumerator(routine.Current);
        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(routine.MoveNext(), Is.False);

        Assert.That(playerRigidbody.constraints, Is.EqualTo(RigidbodyConstraints2D.None));
        Assert.That(playerRigidbody.linearVelocity, Is.EqualTo(Vector2.zero));
        Assert.That(playerController.IsExternalControlLocked, Is.False);
        Assert.That(playerController.IsExternalFacingLocked, Is.False);
    }

    [Test]
    public void TryStartEncounter_ForStageBoss_WaitsForGroundBeforeFreezingPlayer()
    {
        // 空中入室時はGroundCheckが接地を返すまで、プレイヤーを空中固定しない。
        Rigidbody2D playerRigidbody = CreatePlayer(new Vector2(-2f, 0f), out PlayerController playerController);
        playerRigidbody.linearVelocity = new Vector2(1f, -3f);
        AddGroundCheckProbe(playerRigidbody.transform, out GameObject groundObject);

        StageBossAttack stageBoss = CreateStageBoss(new Vector2(2f, 0f), out _, out _, out _);
        GameObject areaObject = CreateInactiveBossAreaObject();
        BossAreaController bossArea = areaObject.GetComponent<BossAreaController>();
        ConfigureStageBossArea(bossArea, stageBoss, revealDuration: 0f, hpLeadInSeconds: 0f, entryDelaySeconds: 0f);
        InvokePrivate(bossArea, "Awake");
        InvokePrivate(bossArea, "CachePlayerReferences");

        IEnumerator routine = (IEnumerator)InvokePrivate(bossArea, "PlayStageBossIntroRoutine");

        Assert.That(routine.MoveNext(), Is.True);
        IEnumerator groundedWait = routine.Current as IEnumerator;
        Assert.That(groundedWait, Is.Not.Null, "Expected a grounded wait enumerator before lock.");
        Assert.That(groundedWait.MoveNext(), Is.True);
        Assert.That(playerRigidbody.constraints, Is.EqualTo(RigidbodyConstraints2D.None));
        Assert.That(playerController.IsExternalControlLocked, Is.False);

        groundObject.transform.position = playerRigidbody.transform.position + Vector3.down * 0.5f;
        Physics2D.SyncTransforms();
        Assert.That(groundedWait.MoveNext(), Is.False);

        Assert.That(routine.MoveNext(), Is.True);
        Assert.That(playerRigidbody.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeAll));
        Assert.That(playerRigidbody.linearVelocity, Is.EqualTo(Vector2.zero));
        Assert.That(playerController.IsExternalControlLocked, Is.True);

        RunNestedEnumerator(routine.Current);
        Assert.That(routine.MoveNext(), Is.False);
        Assert.That(playerRigidbody.constraints, Is.EqualTo(RigidbodyConstraints2D.None));
    }

    [Test]
    public void TryStartEncounter_ForStageBoss_StartsRoomStateButDelaysPlayerLockAndHpEvent()
    {
        // 入室直後は部屋・カメラ側だけ開始し、HPイベントや操作ロックは遅延させる。
        Rigidbody2D playerRigidbody = CreatePlayer(new Vector2(-2f, 0f), out PlayerController playerController);
        StageBossAttack stageBoss = CreateStageBoss(new Vector2(2f, 0f), out _, out _, out _);

        GameObject areaObject = CreateInactiveBossAreaObject();
        BossAreaController bossArea = areaObject.GetComponent<BossAreaController>();
        ConfigureStageBossArea(bossArea, stageBoss, revealDuration: 0f, hpLeadInSeconds: 0.05f, entryDelaySeconds: 2f);
        areaObject.SetActive(true);

        int encounterStartedCount = 0;
        encounterStartedHandler = _ => encounterStartedCount++;
        BossAreaController.EncounterStarted += encounterStartedHandler;

        InvokePrivate(bossArea, "TryStartEncounter");

        Assert.That(GetPrivateField<bool>(bossArea, "encounterStarted"), Is.True);
        Assert.That(encounterStartedCount, Is.EqualTo(0));
        Assert.That(stageBoss.IsEncounterActive, Is.False);
        Assert.That(playerRigidbody.constraints, Is.EqualTo(RigidbodyConstraints2D.None));
        Assert.That(playerController.IsExternalControlLocked, Is.False);

        BossAreaController.EncounterStarted -= encounterStartedHandler;
        encounterStartedHandler = null;
    }

    [Test]
    public void OnTriggerEnter2D_WhenPlayerOnlyPartiallyInside_DoesNotStartOrMovePlayer()
    {
        // 入口に半分だけ触れた状態では、戦闘開始も拘束による位置補正も起きないことを確認する。
        Rigidbody2D playerRigidbody = CreatePlayer(
            new Vector2(-5.25f, 0f),
            out PlayerController playerController,
            addPlayerHealth: true);
        Collider2D playerCollider = playerRigidbody.GetComponent<Collider2D>();
        Vector2 originalPosition = playerRigidbody.position;

        StageBossAttack stageBoss = CreateStageBoss(new Vector2(2f, 0f), out _, out _, out _);
        GameObject areaObject = CreateInactiveBossAreaObject();
        BossAreaController bossArea = areaObject.GetComponent<BossAreaController>();
        ConfigureStageBossArea(bossArea, stageBoss, revealDuration: 0f, hpLeadInSeconds: 0f, entryDelaySeconds: 2f);
        areaObject.SetActive(true);
        Physics2D.SyncTransforms();

        InvokePrivate(bossArea, "OnTriggerEnter2D", playerCollider);

        Assert.That(GetPrivateField<bool>(bossArea, "encounterStarted"), Is.False);
        Assert.That(stageBoss.IsEncounterActive, Is.False);
        Assert.That(playerRigidbody.position, Is.EqualTo(originalPosition));
        Assert.That(playerRigidbody.constraints, Is.EqualTo(RigidbodyConstraints2D.None));
        Assert.That(playerController.IsExternalControlLocked, Is.False);
    }

    [Test]
    public void OnTriggerStay2D_WhenPlayerLeavesThenFullyEnters_StartsWithoutMovingPlayer()
    {
        // 端に触れてすぐ離れた後でも、完全に入り直した Stay でだけ戦闘を開始する。
        Rigidbody2D playerRigidbody = CreatePlayer(
            new Vector2(-5.25f, 0f),
            out PlayerController playerController,
            addPlayerHealth: true);
        Collider2D playerCollider = playerRigidbody.GetComponent<Collider2D>();

        StageBossAttack stageBoss = CreateStageBoss(new Vector2(2f, 0f), out _, out _, out _);
        GameObject areaObject = CreateInactiveBossAreaObject();
        BossAreaController bossArea = areaObject.GetComponent<BossAreaController>();
        ConfigureStageBossArea(bossArea, stageBoss, revealDuration: 0f, hpLeadInSeconds: 0f, entryDelaySeconds: 2f);
        areaObject.SetActive(true);
        Physics2D.SyncTransforms();

        InvokePrivate(bossArea, "OnTriggerEnter2D", playerCollider);
        Assert.That(GetPrivateField<bool>(bossArea, "encounterStarted"), Is.False);

        playerRigidbody.position = new Vector2(-6.25f, 0f);
        Physics2D.SyncTransforms();
        InvokePrivate(bossArea, "OnTriggerStay2D", playerCollider);
        Assert.That(GetPrivateField<bool>(bossArea, "encounterStarted"), Is.False);
        Assert.That(playerController.IsExternalControlLocked, Is.False);

        Vector2 fullyInsidePosition = new Vector2(-4f, 0f);
        playerRigidbody.position = fullyInsidePosition;
        Physics2D.SyncTransforms();
        InvokePrivate(bossArea, "OnTriggerStay2D", playerCollider);

        Assert.That(GetPrivateField<bool>(bossArea, "encounterStarted"), Is.True);
        Assert.That(stageBoss.IsEncounterActive, Is.False);
        Assert.That(playerRigidbody.position, Is.EqualTo(fullyInsidePosition));
        Assert.That(playerRigidbody.constraints, Is.EqualTo(RigidbodyConstraints2D.None));
        Assert.That(playerController.IsExternalControlLocked, Is.False);
    }

    [Test]
    public void TryStartEncounter_ForLastBoss_StartsImmediately()
    {
        // LastBossはStageBossイントロを通らず、従来通り即時開始する。
        LastBossController lastBoss = CreateLastBoss(Vector2.zero);
        GameObject areaObject = CreateInactiveBossAreaObject();
        BossAreaController bossArea = areaObject.GetComponent<BossAreaController>();
        ConfigureLastBossArea(bossArea, lastBoss);
        areaObject.SetActive(true);

        int encounterStartedCount = 0;
        encounterStartedHandler = _ => encounterStartedCount++;
        BossAreaController.EncounterStarted += encounterStartedHandler;

        InvokePrivate(bossArea, "TryStartEncounter");

        Assert.That(encounterStartedCount, Is.EqualTo(1));
        Assert.That(lastBoss.IsEncounterActive, Is.True);

        BossAreaController.EncounterStarted -= encounterStartedHandler;
        encounterStartedHandler = null;
    }

    [UnityTest]
    [Timeout(3000)]
    public IEnumerator Awake_WhenStageBossAlreadyDefeated_HidesBossAndSkipsIntroVisibility()
    {
        // 撃破保存済みのStageBossはイントロ用の再表示をせず、非表示のままにする。
        const string defeatedFlag = "test_stage_boss_intro_defeated";
        GameProgressFlags.Set(defeatedFlag, true);

        StageBossAttack stageBoss = CreateStageBoss(Vector2.zero, out _, out _, out _);
        GameObject areaObject = CreateInactiveBossAreaObject();
        BossAreaController bossArea = areaObject.GetComponent<BossAreaController>();
        ConfigureStageBossArea(bossArea, stageBoss, revealDuration: 0.05f, hpLeadInSeconds: 0.05f);
        SetPrivateField(bossArea, "bossDefeatedFlagKey", defeatedFlag);

        InvokePrivate(bossArea, "Awake");
        yield return null;

        Assert.That(stageBoss.gameObject.activeSelf, Is.False);
        Assert.That(stageBoss.IsEncounterActive, Is.False);
    }

    private StageBossAttack CreateStageBoss(
        Vector2 position,
        out SpriteRenderer renderer,
        out Collider2D collider,
        out Rigidbody2D rigidbody2D)
    {
        GameObject bossObject = new GameObject("StageBoss");
        bossObject.transform.position = position;
        objectsToDestroy.Add(bossObject);

        renderer = bossObject.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateTestSprite();
        collider = bossObject.AddComponent<BoxCollider2D>();
        rigidbody2D = bossObject.AddComponent<Rigidbody2D>();
        rigidbody2D.gravityScale = 0f;

        EnemyController enemyController = bossObject.AddComponent<EnemyController>();
        StageBossAttack stageBoss = bossObject.AddComponent<StageBossAttack>();
        InvokePrivate(enemyController, "Awake");
        InvokePrivate(stageBoss, "Awake");
        SetPrivateField(enemyController, "rigidbody2D", rigidbody2D);
        SetPrivateField(enemyController, "bodyCollider", collider);
        SetPrivateField(enemyController, "spriteRenderer", renderer);
        SetPrivateField(stageBoss, "enemyController", enemyController);
        SetPrivateField(stageBoss, "bodyCollider", collider);
        return stageBoss;
    }

    private LastBossController CreateLastBoss(Vector2 position)
    {
        GameObject bossObject = new GameObject("LastBoss");
        bossObject.transform.position = position;
        objectsToDestroy.Add(bossObject);

        bossObject.AddComponent<SpriteRenderer>();
        bossObject.AddComponent<BoxCollider2D>();
        bossObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
        return bossObject.AddComponent<LastBossController>();
    }

    private StageBgmController CreateStageBgm(out AudioSource bgmSource, out AudioClip bossBgm)
    {
        // BGM開始タイミングだけを確認するための最小構成StageBgm。
        GameObject bgmObject = new GameObject("StageBgm");
        bgmObject.SetActive(false);
        objectsToDestroy.Add(bgmObject);

        bgmSource = bgmObject.AddComponent<AudioSource>();
        StageBgmController stageBgm = bgmObject.AddComponent<StageBgmController>();
        bossBgm = AudioClip.Create("StageBossIntroTestBossBgm", 8, 1, 8000, false);
        objectsToDestroy.Add(bossBgm);

        SetPrivateField(stageBgm, "bgmSource", bgmSource);
        InvokePrivate(stageBgm, "Awake");
        bgmObject.SetActive(true);
        return stageBgm;
    }

    private Sprite CreateTestSprite()
    {
        Texture2D texture = new Texture2D(2, 2);
        texture.SetPixels(new[]
        {
            Color.white,
            Color.white,
            Color.white,
            Color.white
        });
        texture.Apply();
        objectsToDestroy.Add(texture);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 2f, 2f),
            new Vector2(0.5f, 0.5f),
            1f);
        objectsToDestroy.Add(sprite);
        return sprite;
    }

    private Rigidbody2D CreatePlayer(Vector2 position)
    {
        return CreatePlayer(position, out _);
    }

    private Rigidbody2D CreatePlayer(Vector2 position, out PlayerController playerController)
    {
        return CreatePlayer(position, out playerController, addPlayerHealth: false);
    }

    private Rigidbody2D CreatePlayer(
        Vector2 position,
        out PlayerController playerController,
        bool addPlayerHealth)
    {
        GameObject playerObject = new GameObject("Player");
        playerObject.SetActive(false);
        playerObject.tag = "Player";
        playerObject.transform.position = position;
        objectsToDestroy.Add(playerObject);

        Rigidbody2D rigidbody2D = playerObject.AddComponent<Rigidbody2D>();
        rigidbody2D.gravityScale = 0f;
        playerObject.AddComponent<BoxCollider2D>();
        if (addPlayerHealth)
        {
            playerObject.AddComponent<PlayerHealth>();
        }

        playerObject.AddComponent<DodgeController>();
        playerController = playerObject.AddComponent<PlayerController>();
        playerController.enabled = false;
        PlayerInput playerInput = playerObject.GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            playerInput.enabled = false;
        }

        InvokePrivate(playerController, "Awake");
        playerObject.SetActive(true);
        return rigidbody2D;
    }

    private void AddGroundCheckProbe(Transform playerRoot, out GameObject groundObject)
    {
        // 実際のGroundCheck.IsGround()を使って、空中/接地の切り替えをテストする。
        GameObject groundCheckObject = new GameObject("GroundCheck");
        groundCheckObject.transform.SetParent(playerRoot, false);
        groundCheckObject.transform.localPosition = Vector3.down * 0.5f;
        objectsToDestroy.Add(groundCheckObject);

        BoxCollider2D groundCheckCollider = groundCheckObject.AddComponent<BoxCollider2D>();
        groundCheckCollider.isTrigger = true;
        groundCheckCollider.size = new Vector2(0.5f, 0.1f);

        GroundCheck groundCheck = groundCheckObject.AddComponent<GroundCheck>();
        SetPrivateField(groundCheck, "groundLayer", (LayerMask)(1 << 0));
        InvokePrivate(groundCheck, "Awake");

        groundObject = new GameObject("Ground");
        groundObject.layer = 0;
        groundObject.transform.position = new Vector3(playerRoot.position.x, playerRoot.position.y - 20f, 0f);
        objectsToDestroy.Add(groundObject);

        BoxCollider2D groundCollider = groundObject.AddComponent<BoxCollider2D>();
        groundCollider.size = new Vector2(3f, 0.25f);
        Physics2D.SyncTransforms();
    }

    private GameObject CreateInactiveBossAreaObject()
    {
        GameObject areaObject = new GameObject("BossArea");
        areaObject.SetActive(false);
        objectsToDestroy.Add(areaObject);

        BoxCollider2D areaCollider = areaObject.AddComponent<BoxCollider2D>();
        areaCollider.isTrigger = true;
        areaCollider.size = new Vector2(10f, 6f);
        areaObject.AddComponent<BossAreaController>();
        return areaObject;
    }

    private static void ConfigureStageBossArea(
        BossAreaController bossArea,
        StageBossAttack stageBoss,
        float revealDuration,
        float hpLeadInSeconds,
        float entryDelaySeconds = 0f,
        StageBgmController stageBgm = null,
        AudioClip bossBgm = null)
    {
        SetPrivateField(bossArea, "bossRoot", stageBoss.transform);
        SetPrivateField(bossArea, "stageBossAttack", stageBoss);
        SetPrivateField(bossArea, "lastBossController", null);
        SetPrivateField(bossArea, "playStageBossIntro", true);
        SetPrivateField(bossArea, "hideStageBossUntilIntro", true);
        SetPrivateField(bossArea, "stageBossEntryDelaySeconds", entryDelaySeconds);
        SetPrivateField(bossArea, "stageBossNormalBgmFadeOutSeconds", 0.05f);
        SetPrivateField(bossArea, "waitForStageBossPlayerGroundedBeforeLock", true);
        SetPrivateField(bossArea, "stageBossRevealDuration", revealDuration);
        SetPrivateField(bossArea, "stageBossHpLeadInSeconds", hpLeadInSeconds);
        SetPrivateField(bossArea, "stageBgm", stageBgm);
        SetPrivateField(bossArea, "bossBgm", bossBgm);
        SetPrivateField(bossArea, "disableTriggerAfterStart", false);
    }

    private static void ConfigureLastBossArea(
        BossAreaController bossArea,
        LastBossController lastBoss)
    {
        SetPrivateField(bossArea, "bossRoot", lastBoss.transform);
        SetPrivateField(bossArea, "stageBossAttack", null);
        SetPrivateField(bossArea, "lastBossController", lastBoss);
        SetPrivateField(bossArea, "playStageBossIntro", true);
        SetPrivateField(bossArea, "disableTriggerAfterStart", false);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"{fieldName} must exist.");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, PrivateInstance);
        Assert.That(field, Is.Not.Null, $"{fieldName} must exist.");
        return (T)field.GetValue(target);
    }

    private static object InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, PrivateInstance);
        Assert.That(method, Is.Not.Null, $"{methodName} must exist.");
        return method.Invoke(target, arguments);
    }

    private static void RunNestedEnumerator(object nestedEnumerator)
    {
        // UnityのネストしたIEnumeratorをEditModeで手動進行させる。
        IEnumerator enumerator = nestedEnumerator as IEnumerator;
        Assert.That(enumerator, Is.Not.Null, "Expected a nested reveal enumerator.");

        int guard = 0;
        while (enumerator.MoveNext())
        {
            guard++;
            Assert.That(guard, Is.LessThan(1000), "Nested enumerator did not complete.");
        }
    }

    private static bool HasVisibleMirageGhost(Transform root)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].name.Contains("MirageGhost") && renderers[i].color.a > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMirageGhost(Transform root)
    {
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].name.Contains("MirageGhost"))
            {
                return true;
            }
        }

        return false;
    }
}
