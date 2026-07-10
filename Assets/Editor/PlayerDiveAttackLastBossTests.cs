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

        Assert.That(InvokeApplyDamageAtPosition(diveAttack), Is.True);
        Assert.That(boss.CurrentHealth, Is.EqualTo(60));

        Assert.That(InvokeApplyDamageAtPosition(diveAttack), Is.True);
        Assert.That(boss.CurrentHealth, Is.EqualTo(60));
        Assert.That(bossCollider, Is.Not.Null);
    }

    private static bool InvokeApplyDamageAtPosition(PlayerDiveAttackController diveAttack)
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

        return (bool)method.Invoke(diveAttack, arguments);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(target, null);
    }
}
