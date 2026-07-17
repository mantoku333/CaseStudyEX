using System.Collections.Generic;
using System.Reflection;
using GameName.Enemy;
using Metroidvania.Player;
using NUnit.Framework;
using Player;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class RainDamageAreaTests
{
    private readonly List<GameObject> objectsToDestroy = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        EnemyGameplayPause.ResetCache();
        Time.timeScale = 1f;
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        EnemyGameplayPause.ResetCache();

        for (int i = objectsToDestroy.Count - 1; i >= 0; i--)
        {
            if (objectsToDestroy[i] != null)
            {
                Object.DestroyImmediate(objectsToDestroy[i]);
            }
        }

        objectsToDestroy.Clear();
    }

    [Test]
    public void FixedUpdate_WhenPlayerIsExposed_DamagesPlayer()
    {
        RainDamageArea rainArea = CreateRainDamageArea(true);
        PlayerHealth playerHealth = CreatePlayer(Vector2.zero);
        Physics2D.SyncTransforms();

        InvokeFixedUpdate(rainArea);

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0));
    }

    [Test]
    public void FixedUpdate_WhenRainCooldownIsActive_EnemyDamageUsesIndependentCooldown()
    {
        RainDamageArea rainArea = CreateRainDamageArea(true);
        PlayerHealth playerHealth = CreatePlayer(Vector2.zero);
        playerHealth.AddMaxHealth(3, true);
        Physics2D.SyncTransforms();

        InvokeFixedUpdate(rainArea);
        bool didTakeEnemyDamage = playerHealth.TryTakeDamage(1, 10f);
        InvokeFixedUpdate(rainArea);
        bool didRepeatEnemyDamage = playerHealth.TryTakeDamage(1, 10f);

        Assert.That(didTakeEnemyDamage, Is.True);
        Assert.That(didRepeatEnemyDamage, Is.False);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(2));
    }

    [Test]
    public void FixedUpdate_WhenEnemyCooldownIsActive_RainDamageUsesIndependentCooldown()
    {
        RainDamageArea rainArea = CreateRainDamageArea(true);
        PlayerHealth playerHealth = CreatePlayer(Vector2.zero);
        playerHealth.AddMaxHealth(3, true);
        Physics2D.SyncTransforms();

        bool didTakeEnemyDamage = playerHealth.TryTakeDamage(1, 10f);
        InvokeFixedUpdate(rainArea);
        bool didRepeatEnemyDamage = playerHealth.TryTakeDamage(1, 10f);
        InvokeFixedUpdate(rainArea);

        Assert.That(didTakeEnemyDamage, Is.True);
        Assert.That(didRepeatEnemyDamage, Is.False);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(2));
    }

    [Test]
    public void RestoreFullHealth_ClearsRainAndEnemyDamageCooldowns()
    {
        PlayerHealth playerHealth = CreatePlayer(Vector2.zero);
        playerHealth.AddMaxHealth(4, true);

        Assert.That(playerHealth.TryTakeDamage(1, 10f), Is.True);
        Assert.That(playerHealth.TryTakeRainDamage(1, 10f), Is.True);

        playerHealth.RestoreFullHealth();

        Assert.That(playerHealth.TryTakeDamage(1, 10f), Is.True);
        Assert.That(playerHealth.TryTakeRainDamage(1, 10f), Is.True);
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(playerHealth.MaxHealth - 2));
    }

    [Test]
    public void FixedUpdate_WhenSolidCoverBlocksAllRainPaths_DoesNotDamagePlayer()
    {
        RainDamageArea rainArea = CreateRainDamageArea(true);
        PlayerHealth playerHealth = CreatePlayer(Vector2.zero);
        CreateGroundBox("FullRoof", new Vector2(0f, 2.5f), new Vector2(2f, 0.4f));
        Physics2D.SyncTransforms();

        InvokeFixedUpdate(rainArea);

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(1));
    }

    [Test]
    public void FixedUpdate_WhenSolidCoverBlocksOnlyOneRainPath_DamagesPlayer()
    {
        RainDamageArea rainArea = CreateRainDamageArea(true);
        PlayerHealth playerHealth = CreatePlayer(Vector2.zero);
        CreateGroundBox("PartialRoof", new Vector2(-0.45f, 2.5f), new Vector2(0.25f, 0.4f));
        Physics2D.SyncTransforms();

        InvokeFixedUpdate(rainArea);

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0));
    }

    [Test]
    public void FixedUpdate_WhenRainIsInactive_DoesNotDamagePlayer()
    {
        RainDamageArea rainArea = CreateRainDamageArea(false);
        PlayerHealth playerHealth = CreatePlayer(Vector2.zero);
        Physics2D.SyncTransforms();

        InvokeFixedUpdate(rainArea);

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(1));
    }

    [Test]
    public void FixedUpdate_WhenUmbrellaIsOpen_DoesNotDamagePlayer()
    {
        RainDamageArea rainArea = CreateRainDamageArea(true);
        PlayerHealth playerHealth = CreatePlayer(Vector2.zero);
        UmbrellaController umbrellaController = playerHealth.gameObject.AddComponent<UmbrellaController>();
        umbrellaController.SetUmbrellaState(UmbrellaController.UmbrellaState.Open, false);
        Physics2D.SyncTransforms();

        InvokeFixedUpdate(rainArea);

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(1));
    }

    [Test]
    public void EventControlLock_BlocksDamageAndFlashUntilGracePeriodExpires()
    {
        RainDamageArea rainArea = CreateRainDamageArea(true);
        PlayerHealth playerHealth = CreatePlayer(Vector2.zero);
        SpriteRenderer playerRenderer = playerHealth.gameObject.AddComponent<SpriteRenderer>();
        playerRenderer.color = Color.white;
        PlayerDamageFlash damageFlash = playerHealth.gameObject.AddComponent<PlayerDamageFlash>();
        InvokeLifecycleMethod(damageFlash, "Awake");
        PlayerController playerController = playerHealth.gameObject.AddComponent<PlayerController>();
        Physics2D.SyncTransforms();

        playerController.SetExternalControlLocked(true);
        InvokeLifecycleMethod(rainArea, "Update");
        float graceDeadline = GetPrivateField<float>(rainArea, "rainDamageBlockedUntilTime");
        InvokeFixedUpdate(rainArea);

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(1));
        Assert.That(playerRenderer.color, Is.EqualTo(Color.white));
        Assert.That(GetPrivateField<float>(damageFlash, "nextFlashTime"), Is.EqualTo(0f));
        Assert.That(graceDeadline - Time.time, Is.EqualTo(3f).Within(0.001f));

        playerController.SetExternalControlLocked(false);
        InvokeFixedUpdate(rainArea);

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(1));
        Assert.That(playerRenderer.color, Is.EqualTo(Color.white));
        Assert.That(GetPrivateField<float>(damageFlash, "nextFlashTime"), Is.EqualTo(0f));

        SetPrivateField(rainArea, "rainDamageBlockedUntilTime", Time.time - 0.01f);
        InvokeFixedUpdate(rainArea);

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0));
        Assert.That(GetPrivateField<float>(damageFlash, "nextFlashTime"), Is.GreaterThan(Time.time));
    }

    [Test]
    public void OptionsMenuTimeScalePause_DoesNotStartPostEventGracePeriod()
    {
        RainDamageArea rainArea = CreateRainDamageArea(true);
        PlayerHealth playerHealth = CreatePlayer(Vector2.zero);
        Physics2D.SyncTransforms();

        Time.timeScale = 0f;
        InvokeLifecycleMethod(rainArea, "Update");
        Time.timeScale = 1f;
        InvokeFixedUpdate(rainArea);

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0));
        Assert.That(GetPrivateField<float>(rainArea, "rainDamageBlockedUntilTime"), Is.EqualTo(0f));
    }

    private RainDamageArea CreateRainDamageArea(bool active)
    {
        // 実シーンを開かず、雨エリア・プレイヤー・屋根だけを作って到達判定を検証する。
        GameObject areaObject = CreateObject("RainDamageArea", new Vector2(0f, 2f));
        BoxCollider2D areaCollider = areaObject.AddComponent<BoxCollider2D>();
        areaCollider.isTrigger = true;
        areaCollider.size = new Vector2(6f, 8f);

        RainDamageArea rainArea = areaObject.AddComponent<RainDamageArea>();
        InvokeLifecycleMethod(rainArea, "Awake");
        rainArea.SetRainActive(active);
        return rainArea;
    }

    private PlayerHealth CreatePlayer(Vector2 position)
    {
        GameObject playerObject = CreateObject("Player", position);
        playerObject.tag = "Player";
        playerObject.layer = PlayerLayer();

        Rigidbody2D rigidbody2D = playerObject.AddComponent<Rigidbody2D>();
        rigidbody2D.gravityScale = 0f;

        CapsuleCollider2D bodyCollider = playerObject.AddComponent<CapsuleCollider2D>();
        bodyCollider.size = Vector2.one;
        bodyCollider.direction = CapsuleDirection2D.Vertical;

        PlayerHealth playerHealth = playerObject.AddComponent<PlayerHealth>();
        playerHealth.RestoreFullHealth();
        return playerHealth;
    }

    private GameObject CreateGroundBox(string name, Vector2 position, Vector2 size)
    {
        GameObject box = CreateObject(name, position);
        box.layer = GroundLayer();

        BoxCollider2D collider = box.AddComponent<BoxCollider2D>();
        collider.size = size;
        return box;
    }

    private GameObject CreateObject(string name, Vector2 position)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.position = position;
        objectsToDestroy.Add(gameObject);
        return gameObject;
    }

    private static void InvokeFixedUpdate(RainDamageArea rainArea)
    {
        InvokeLifecycleMethod(rainArea, "FixedUpdate");
    }

    private static void InvokeLifecycleMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(target, null);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        return (T)field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private static int PlayerLayer()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        Assert.GreaterOrEqual(playerLayer, 0, "Player layer must exist for RainDamageArea tests.");
        return playerLayer;
    }

    private static int GroundLayer()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        Assert.GreaterOrEqual(groundLayer, 0, "Ground layer must exist for RainDamageArea tests.");
        return groundLayer;
    }
}
