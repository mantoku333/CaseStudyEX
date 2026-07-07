using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using GameName.Enemy;
using NUnit.Framework;
using Player;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BackAttackDamageMultiplierTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<Object> objectsToDestroy = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;

        HitStopController[] hitStopControllers =
            Object.FindObjectsByType<HitStopController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < hitStopControllers.Length; i++)
        {
            if (hitStopControllers[i] != null)
            {
                Object.DestroyImmediate(hitStopControllers[i].gameObject);
            }
        }

        for (int i = objectsToDestroy.Count - 1; i >= 0; i--)
        {
            if (objectsToDestroy[i] != null)
            {
                PrepareForImmediateDestroy(objectsToDestroy[i]);
                Object.DestroyImmediate(objectsToDestroy[i]);
            }
        }

        objectsToDestroy.Clear();
    }

    [Test]
    public void EnemyController_FrontAttack_UsesBaseDamage()
    {
        EnemyController enemy = CreateEnemyController(10, 2f);
        AttackHitbox attacker = CreateAttackHitbox(new Vector2(1f, 0f), 1, new Vector2(-1f, 0f));

        enemy.OnAttacked(attacker, null);

        Assert.That(enemy.CurrentHealth, Is.EqualTo(9));
    }

    [Test]
    public void EnemyController_BackAttack_UsesMultiplier()
    {
        EnemyController enemy = CreateEnemyController(10, 2f);
        AttackHitbox attacker = CreateAttackHitbox(new Vector2(-1f, 0f), 1, new Vector2(1f, 0f));

        enemy.OnAttacked(attacker, null);

        Assert.That(enemy.CurrentHealth, Is.EqualTo(8));
    }

    [Test]
    public void EnemyController_BackAttack_RoundsDamageUp()
    {
        EnemyController enemy = CreateEnemyController(10, 1.5f);
        AttackHitbox attacker = CreateAttackHitbox(new Vector2(-1f, 0f), 1);

        enemy.OnAttacked(attacker, null);

        Assert.That(enemy.CurrentHealth, Is.EqualTo(8));
    }

    [Test]
    public void LastBoss_BackAttack_ScalesDamageAndAddsOneDownCount()
    {
        LastBossController boss = CreateLastBossController(10, 2f);
        AttackHitbox attacker = CreateAttackHitbox(new Vector2(-1f, 0f), 1);

        boss.OnAttacked(attacker, null);

        Assert.That(boss.CurrentHealth, Is.EqualTo(8));
        Assert.That(GetPrivateField<int>(boss, "downCount"), Is.EqualTo(1));
    }

    [TestCase(10, 0.5f, 5)]
    [TestCase(5, 0.5f, 3)]
    [TestCase(1, 0.5f, 1)]
    [TestCase(8, 0.25f, 2)]
    public void LastBoss_ShieldedDamage_UsesConfiguredMultiplierAndRoundsUp(
        int attackDamage,
        float shieldMultiplier,
        int expectedDamage)
    {
        LastBossController boss = CreateLastBossController(20, 2f, shieldMultiplier);
        AttackHitbox attacker = CreateAttackHitbox(new Vector2(1f, 0f), attackDamage);
        boss.ActivateEncounter();

        boss.OnAttacked(attacker, null);

        Assert.That(boss.CurrentHealth, Is.EqualTo(20 - expectedDamage));
        Assert.That(GetPrivateField<int>(boss, "downCount"), Is.Zero);
    }

    [Test]
    public void LastBoss_DownedDamage_DoesNotUseShieldMultiplier()
    {
        LastBossController boss = CreateLastBossController(20, 2f);
        AttackHitbox attacker = CreateAttackHitbox(new Vector2(1f, 0f), 10);
        boss.ActivateEncounter();
        SetPrivateEnumField(boss, "state", "Downed");

        boss.OnAttacked(attacker, null);

        Assert.That(boss.CurrentHealth, Is.EqualTo(10));
        Assert.That(GetPrivateField<int>(boss, "downCount"), Is.Zero);
    }

    [Test]
    public void LastBoss_ShieldedBackAttack_AppliesBackMultiplierBeforeShieldReduction()
    {
        LastBossController boss = CreateLastBossController(20, 2f);
        AttackHitbox attacker = CreateAttackHitbox(new Vector2(-1f, 0f), 5);
        boss.ActivateEncounter();

        boss.OnAttacked(attacker, null);

        Assert.That(boss.CurrentHealth, Is.EqualTo(15));
        Assert.That(GetPrivateField<int>(boss, "downCount"), Is.EqualTo(1));
    }

    [Test]
    public void LastBoss_HitThatTriggersDown_IsStillShieldReduced()
    {
        LastBossController boss = CreateLastBossController(20, 2f);
        AttackHitbox attacker = CreateAttackHitbox(new Vector2(-1f, 0f), 5);
        SetPrivateField(boss, "downCountThreshold", 1);
        boss.ActivateEncounter();

        boss.OnAttacked(attacker, null);

        Assert.That(boss.CurrentHealth, Is.EqualTo(15));
        Assert.That(GetPrivateField<object>(boss, "state").ToString(), Is.EqualTo("Downed"));
    }

    [Test]
    public void LastBoss_FrontHitDoesNotTriggerDownCounter()
    {
        LastBossController boss = CreateLastBossController(20, 2f);
        AttackHitbox attacker = CreateAttackHitbox(new Vector2(1f, 0f), 10);
        SetPrivateField(boss, "downCountThreshold", 1);
        boss.ActivateEncounter();

        boss.OnAttacked(attacker, null);

        Assert.That(boss.CurrentHealth, Is.EqualTo(15));
        Assert.That(GetPrivateField<int>(boss, "downCount"), Is.Zero);
        Assert.That(GetPrivateField<object>(boss, "state").ToString(), Is.Not.EqualTo("Downed"));
    }

    [Test]
    public void LastBoss_TenthBackAttackTriggersDown()
    {
        LastBossController boss = CreateLastBossController(100, 2f);
        AttackHitbox attacker = CreateAttackHitbox(new Vector2(-1f, 0f), 1);

        for (int i = 0; i < 9; i++)
        {
            boss.OnAttacked(attacker, null);
        }

        Assert.That(GetPrivateField<int>(boss, "downCount"), Is.EqualTo(9));
        Assert.That(GetPrivateField<object>(boss, "state").ToString(), Is.Not.EqualTo("Downed"));

        boss.OnAttacked(attacker, null);

        Assert.That(GetPrivateField<object>(boss, "state").ToString(), Is.EqualTo("Downed"));
    }

    [Test]
    public void LastBoss_ShieldedLethalDamage_StillTriggersDeath()
    {
        LastBossController boss = CreateLastBossController(4, 2f);
        AttackHitbox attacker = CreateAttackHitbox(new Vector2(1f, 0f), 8);
        boss.ActivateEncounter();

        boss.OnAttacked(attacker, null);

        Assert.That(boss.CurrentHealth, Is.Zero);
    }

    [Test]
    public void AttackDestructible_AttackDamageIgnoresBackAttackMultiplier()
    {
        GameObject target = CreateObject("Destructible", Vector2.zero);
        AttackDestructible destructible = target.AddComponent<AttackDestructible>();
        SetPrivateField(destructible, "hitPoints", 3);
        AttackHitbox attacker = CreateAttackHitbox(new Vector2(-1f, 0f), 1);

        destructible.OnAttacked(attacker, null);

        Assert.That(destructible.CurrentHitPoints, Is.EqualTo(2));
    }

    [Test]
    public void LeverSwitch_AttackPathDoesNotReadDamageMultiplier()
    {
        GameObject leverObject = CreateObject("Lever", Vector2.zero);
        leverObject.AddComponent<SpriteRenderer>();
        LeverSwitch2D lever = leverObject.AddComponent<LeverSwitch2D>();
        AttackHitbox attacker = CreateAttackHitbox(new Vector2(-1f, 0f), 10);

        LogAssert.Expect(LogType.Warning, new Regex("ShutterWallBlockRise"));

        lever.OnAttacked(attacker, null);
    }

    private EnemyController CreateEnemyController(int maxHealth, float backAttackMultiplier)
    {
        GameObject enemyObject = CreateObject("Enemy", Vector2.zero);
        enemyObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
        enemyObject.AddComponent<BoxCollider2D>();
        EnemyController enemy = enemyObject.AddComponent<EnemyController>();
        SetPrivateField(enemy, "maxHealth", maxHealth);
        SetPrivateField(enemy, "backAttackDamageMultiplier", backAttackMultiplier);
        InvokePrivate(enemy, "Awake");
        enemy.FaceDirection(1);
        return enemy;
    }

    private LastBossController CreateLastBossController(
        int maxHealth,
        float backAttackMultiplier,
        float shieldMultiplier = 0.5f)
    {
        GameObject bossObject = CreateObject("LastBoss", Vector2.zero);
        bossObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
        bossObject.AddComponent<BoxCollider2D>();
        LastBossController boss = bossObject.AddComponent<LastBossController>();
        SetPrivateField(boss, "maxHealth", maxHealth);
        SetPrivateField(boss, "backAttackDamageMultiplier", backAttackMultiplier);
        SetPrivateField(boss, "shieldDamageMultiplier", shieldMultiplier);
        SetPrivateField(boss, "downCountThreshold", 10);
        SetPrivateField(boss, "facingDirection", 1);
        InvokePrivate(boss, "Awake");
        return boss;
    }

    private AttackHitbox CreateAttackHitbox(Vector2 playerPosition, int damage, Vector2? attackPosition = null)
    {
        GameObject player = CreateObject("Player", playerPosition);
        Rigidbody2D rigidbody2D = player.AddComponent<Rigidbody2D>();
        rigidbody2D.gravityScale = 0f;
        CapsuleCollider2D bodyCollider = player.AddComponent<CapsuleCollider2D>();
        bodyCollider.size = Vector2.one;
        bodyCollider.direction = CapsuleDirection2D.Vertical;

        GameObject attackObject = CreateObject("AttackHitbox", attackPosition ?? playerPosition);
        attackObject.transform.SetParent(player.transform, true);
        BoxCollider2D attackCollider = attackObject.AddComponent<BoxCollider2D>();
        attackCollider.isTrigger = true;
        attackCollider.size = Vector2.one;

        AttackHitbox hitbox = attackObject.AddComponent<AttackHitbox>();
        InvokePrivate(hitbox, "Awake");

        PlayerStatsData stats = ScriptableObject.CreateInstance<PlayerStatsData>();
        objectsToDestroy.Add(stats);
        stats.SetPlayerAttackDamage(damage);
        hitbox.SetPlayerStatsData(stats);
        return hitbox;
    }

    private GameObject CreateObject(string name, Vector2 position)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.position = position;
        objectsToDestroy.Add(gameObject);
        return gameObject;
    }

    private static void PrepareForImmediateDestroy(Object target)
    {
        GameObject gameObject = target as GameObject;
        if (gameObject == null)
        {
            return;
        }

        LastBossController boss = gameObject.GetComponent<LastBossController>();
        if (boss != null)
        {
            SetPrivateField(boss, "telegraphObject", null);
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, $"Private field '{fieldName}' must exist.");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, $"Private field '{fieldName}' must exist.");
        return (T)field.GetValue(target);
    }

    private static void SetPrivateEnumField(object target, string fieldName, string enumValue)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, $"Private field '{fieldName}' must exist.");
        field.SetValue(target, System.Enum.Parse(field.FieldType, enumValue));
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstancePrivate);
        Assert.That(method, Is.Not.Null, $"Private method '{methodName}' must exist.");
        method.Invoke(target, null);
    }
}
