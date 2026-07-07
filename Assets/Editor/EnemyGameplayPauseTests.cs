using System.Collections.Generic;
using System.Reflection;
using GameName.Enemy;
using Metroidvania.Enemy;
using NUnit.Framework;
using Player;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class EnemyGameplayPauseTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<Object> objectsToDestroy = new List<Object>();

    [SetUp]
    public void SetUp()
    {
        EnemyGameplayPause.ResetCache();
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
    public void PatrolEnemy_StopsWhilePlayerControlLocked_AndResumesAfterUnlock()
    {
        PlayerController player = CreatePlayer(Vector2.right * 8f);
        EnemyController enemy = CreateEnemy(Vector2.zero, out Rigidbody2D enemyRigidbody);

        player.SetExternalControlLocked(true);
        InvokePrivate(enemy, "FixedUpdate");

        Assert.That(enemyRigidbody.linearVelocity.x, Is.EqualTo(0f).Within(0.0001f));

        player.SetExternalControlLocked(false);
        InvokePrivate(enemy, "FixedUpdate");

        Assert.That(Mathf.Abs(enemyRigidbody.linearVelocity.x), Is.GreaterThan(0.0001f));
    }

    [Test]
    public void PatrolEnemy_DoesNotPauseForInactiveLockedPlayerController()
    {
        PlayerController inactivePlayer = CreatePlayer(Vector2.left * 8f);
        inactivePlayer.SetExternalControlLocked(true);
        inactivePlayer.gameObject.SetActive(false);
        EnemyGameplayPause.ResetCache();

        CreatePlayer(Vector2.right * 8f);
        EnemyController enemy = CreateEnemy(Vector2.zero, out Rigidbody2D enemyRigidbody);

        InvokePrivate(enemy, "FixedUpdate");

        Assert.That(Mathf.Abs(enemyRigidbody.linearVelocity.x), Is.GreaterThan(0.0001f));
    }

    [Test]
    public void EnemyContact_DoesNotDamageWhilePlayerControlLocked_AndDamagesAfterUnlock()
    {
        PlayerController player = CreatePlayer(Vector2.zero);
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        EnemyContact enemyContact = CreateContactEnemy(Vector2.zero);

        player.SetExternalControlLocked(true);
        InvokePrivate(enemyContact, "ApplyContactHit");

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(playerHealth.MaxHealth));

        player.SetExternalControlLocked(false);
        InvokePrivate(enemyContact, "ApplyContactHit");

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0));
    }

    [Test]
    public void TackleEnemy_DoesNotEnterWindupWhilePlayerControlLocked()
    {
        PlayerController player = CreatePlayer(Vector2.right);
        EnemyController enemy = CreateEnemy(Vector2.zero, out _);
        EnemyTackleAttack tackleAttack = enemy.gameObject.AddComponent<EnemyTackleAttack>();
        InvokePrivate(tackleAttack, "Awake");

        Physics2D.SyncTransforms();

        player.SetExternalControlLocked(true);
        InvokePrivate(tackleAttack, "FixedUpdate");

        Assert.That(tackleAttack.IsWindingUp, Is.False);

        player.SetExternalControlLocked(false);
        InvokePrivate(tackleAttack, "FixedUpdate");

        Assert.That(tackleAttack.IsWindingUp, Is.True);
    }

    [Test]
    public void RangedEnemy_DoesNotCompleteWindupWhilePlayerControlLocked()
    {
        PlayerController player = CreatePlayer(Vector2.right);
        EnemyController enemy = CreateEnemy(Vector2.zero, out _);
        EnemyRangedAttack rangedAttack = enemy.gameObject.AddComponent<EnemyRangedAttack>();
        int projectileFireCount = 0;
        rangedAttack.ProjectileFired += () => projectileFireCount++;
        SetPrivateField(rangedAttack, "windupDuration", Time.fixedDeltaTime);
        InvokePrivate(rangedAttack, "Awake");
        InvokePrivate(rangedAttack, "Start");

        player.SetExternalControlLocked(false);
        InvokePrivate(rangedAttack, "FixedUpdate");
        Assert.That(rangedAttack.IsWindingUp, Is.True);

        player.SetExternalControlLocked(true);
        InvokePrivate(rangedAttack, "FixedUpdate");
        InvokePrivate(rangedAttack, "FixedUpdate");

        Assert.That(rangedAttack.IsWindingUp, Is.True);
        Assert.That(projectileFireCount, Is.EqualTo(0));
    }

    [Test]
    public void EnemyBullet_FreezesAndBlocksPlayerDamageWhileLocked_ThenResumes()
    {
        PlayerController player = CreatePlayer(Vector2.right);
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        Collider2D playerCollider = player.GetComponent<Collider2D>();
        EnemyBullet bullet = CreateBullet(Vector2.zero, out Rigidbody2D bulletRigidbody);
        bullet.Initialize(Vector2.right, 4f);

        Assert.That(bulletRigidbody.linearVelocity.x, Is.GreaterThan(0.0001f));

        player.SetExternalControlLocked(true);
        InvokePrivate(bullet, "FixedUpdate");
        bool consumedHit = InvokePrivate<bool>(bullet, "TryApplyPlayerHit", playerCollider);

        Assert.That(consumedHit, Is.True);
        Assert.That(bulletRigidbody.linearVelocity, Is.EqualTo(Vector2.zero));
        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(playerHealth.MaxHealth));

        player.SetExternalControlLocked(false);
        InvokePrivate(bullet, "FixedUpdate");

        Assert.That(bulletRigidbody.linearVelocity.x, Is.GreaterThan(0.0001f));
    }

    [Test]
    public void GuidedEnemyBullet_PauseDoesNotConsumeGuidance_AndResumesCleanly()
    {
        PlayerController player = CreatePlayer(Vector2.right * 3f);
        GameObject owner = CreateObject("EnemyOwner", Vector2.zero);
        owner.AddComponent<BoxCollider2D>();
        EnemyBullet bullet = CreateBullet(Vector2.zero, out Rigidbody2D bulletRigidbody);
        bullet.Initialize(
            player.transform,
            owner.transform,
            5f,
            4f,
            10,
            10f,
            0,
            true,
            1f,
            Vector2.right);

        float elapsedBeforePause = 1f - Time.fixedDeltaTime * 0.5f;
        SetPrivateField(bullet, "guidanceElapsed", elapsedBeforePause);
        Vector2 velocityBeforePause = bulletRigidbody.linearVelocity;

        player.SetExternalControlLocked(true);
        InvokePrivate(bullet, "FixedUpdate");
        InvokePrivate(bullet, "FixedUpdate");

        Assert.That(bulletRigidbody.linearVelocity, Is.EqualTo(Vector2.zero));
        Assert.That(GetPrivateField<float>(bullet, "guidanceElapsed"), Is.EqualTo(elapsedBeforePause));
        Assert.That(GetPrivateField<Vector2>(bullet, "velocityBeforeEnemyPause"), Is.EqualTo(velocityBeforePause));
        Assert.That(GetPrivateField<bool>(bullet, "isGuidanceActive"), Is.True);

        player.SetExternalControlLocked(false);
        InvokePrivate(bullet, "FixedUpdate");

        Assert.That(GetPrivateField<bool>(bullet, "isGuidanceActive"), Is.False);
        Assert.That(GetPrivateField<bool>(bullet, "useTerrainAvoidance"), Is.False);
        Assert.That(GetPrivateField<bool>(bullet, "isEscapingOwner"), Is.True);
        Assert.That(bulletRigidbody.linearVelocity, Is.EqualTo(velocityBeforePause));
    }

    private PlayerController CreatePlayer(Vector2 position)
    {
        GameObject playerObject = CreateObject("Player", position);
        playerObject.tag = "Player";
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
        {
            playerObject.layer = playerLayer;
        }

        Rigidbody2D playerRigidbody = playerObject.AddComponent<Rigidbody2D>();
        playerRigidbody.gravityScale = 0f;
        playerObject.AddComponent<BoxCollider2D>();
        PlayerHealth playerHealth = playerObject.AddComponent<PlayerHealth>();
        playerHealth.RestoreFullHealth();
        return playerObject.AddComponent<PlayerController>();
    }

    private EnemyController CreateEnemy(Vector2 position, out Rigidbody2D enemyRigidbody)
    {
        GameObject enemyObject = CreateObject("Enemy", position);
        enemyRigidbody = enemyObject.AddComponent<Rigidbody2D>();
        enemyRigidbody.gravityScale = 0f;
        enemyObject.AddComponent<BoxCollider2D>();
        EnemyController enemy = enemyObject.AddComponent<EnemyController>();
        InvokePrivate(enemy, "Awake");
        InvokePrivate(enemy, "Start");
        return enemy;
    }

    private EnemyContact CreateContactEnemy(Vector2 position)
    {
        EnemyController enemy = CreateEnemy(position, out _);
        SetPrivateField(enemy, "damageToPlayer", 1);
        EnemyContact enemyContact = enemy.gameObject.AddComponent<EnemyContact>();
        InvokePrivate(enemyContact, "Awake");
        return enemyContact;
    }

    private EnemyBullet CreateBullet(Vector2 position, out Rigidbody2D bulletRigidbody)
    {
        GameObject bulletObject = CreateObject("EnemyBullet", position);
        bulletRigidbody = bulletObject.AddComponent<Rigidbody2D>();
        bulletRigidbody.gravityScale = 0f;
        CircleCollider2D bulletCollider = bulletObject.AddComponent<CircleCollider2D>();
        bulletCollider.isTrigger = true;
        EnemyBullet bullet = bulletObject.AddComponent<EnemyBullet>();
        InvokePrivate(bullet, "Awake");
        return bullet;
    }

    private GameObject CreateObject(string name, Vector2 position)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.position = position;
        objectsToDestroy.Add(gameObject);
        return gameObject;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, fieldName);
        return (T)field.GetValue(target);
    }

    private static object InvokePrivate(object target, string methodName, params object[] parameters)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstancePrivate);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(target, parameters);
    }

    private static T InvokePrivate<T>(object target, string methodName, params object[] parameters)
    {
        return (T)InvokePrivate(target, methodName, parameters);
    }
}
