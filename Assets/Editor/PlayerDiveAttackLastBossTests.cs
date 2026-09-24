using System.Reflection;
using GameName.Enemy;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerDiveAttackLastBossTests
{
    private GameObject playerObject;
    private GameObject bossObject;

    [TearDown]
    public void TearDown()
    {
        if (playerObject != null)
        {
            Object.DestroyImmediate(playerObject);
        }

        if (bossObject != null)
        {
            Object.DestroyImmediate(bossObject);
        }
    }

    [Test]
    public void ApplyDamageAtPosition_WhenLastBossOverlaps_DamagesOnlyOncePerDive()
    {
        playerObject = new GameObject("Player");
        playerObject.AddComponent<Rigidbody2D>();
        PlayerDiveAttackController diveAttack = playerObject.AddComponent<PlayerDiveAttackController>();

        bossObject = new GameObject("LastBoss");
        bossObject.AddComponent<Rigidbody2D>();
        BoxCollider2D bossCollider = bossObject.AddComponent<BoxCollider2D>();
        LastBossController boss = bossObject.AddComponent<LastBossController>();
        InvokePrivate(boss, "Awake");

        playerObject.transform.position = Vector3.zero;
        bossObject.transform.position = Vector3.zero;
        Physics2D.SyncTransforms();

        Assert.That(InvokeApplyDamageAtPosition(diveAttack, out bool shouldBounce), Is.True);
        Assert.That(boss.CurrentHealth, Is.EqualTo(60));
        Assert.That(shouldBounce, Is.True);

        Assert.That(InvokeApplyDamageAtPosition(diveAttack, out shouldBounce), Is.True);
        Assert.That(boss.CurrentHealth, Is.EqualTo(60));
        Assert.That(shouldBounce, Is.False);
        Assert.That(bossCollider, Is.Not.Null);
    }

    [Test]
    public void ApplyDamageAtPosition_WhenLivingStageBossOverlaps_RequestsBounceWithoutKill()
    {
        playerObject = new GameObject("Player");
        playerObject.AddComponent<Rigidbody2D>();
        PlayerDiveAttackController diveAttack = playerObject.AddComponent<PlayerDiveAttackController>();

        bossObject = new GameObject("StageBoss");
        bossObject.AddComponent<Rigidbody2D>();
        bossObject.AddComponent<BoxCollider2D>();
        EnemyController boss = bossObject.AddComponent<EnemyController>();
        bossObject.AddComponent<StageBossAttack>();
        SetPrivateField(boss, "maxHealth", 220);
        InvokePrivate(boss, "Awake");

        playerObject.transform.position = Vector3.zero;
        bossObject.transform.position = Vector3.zero;
        Physics2D.SyncTransforms();

        Assert.That(InvokeApplyDamageAtPosition(diveAttack, out bool shouldBounce), Is.True);
        Assert.That(boss.CurrentHealth, Is.EqualTo(180));
        Assert.That(shouldBounce, Is.True);
    }

    private static bool InvokeApplyDamageAtPosition(
        PlayerDiveAttackController diveAttack,
        out bool shouldBounce)
    {
        MethodInfo method = typeof(PlayerDiveAttackController).GetMethod(
            "ApplyDamageAtPosition",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        object[] arguments =
        {
            Vector2.zero,
            2f,
            false,
            false,
            false,
            Vector3.zero,
            null
        };

        bool foundTarget = (bool)method.Invoke(diveAttack, arguments);
        shouldBounce = (bool)arguments[2];
        return foundTarget;
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(target, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}
