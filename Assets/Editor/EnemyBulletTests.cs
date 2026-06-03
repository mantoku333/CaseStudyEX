using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Metroidvania.Enemy;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class EnemyBulletTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<GameObject> objectsToDestroy = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
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
    public void Initialize_WithUnreachableTargetBehindWall_EntersTerminalImpact()
    {
        GameObject owner = CreateObject("EnemyOwner", Vector2.zero);
        GameObject target = CreateTarget("PlayerTarget", new Vector2(0f, 3f));
        CreateGroundObstacle("BlockingCeiling", new Vector2(0f, 1.25f), new Vector2(12f, 0.5f));
        EnemyBullet bullet = CreateBullet(Vector2.zero, out Rigidbody2D bulletRigidbody);

        Physics2D.SyncTransforms();

        bullet.Initialize(
            target.transform,
            owner.transform,
            5f,
            10f,
            10,
            10f,
            GroundMask(),
            true);

        Assert.IsTrue(GetPrivateField<bool>(bullet, "isTerminalImpacting"));
        Assert.IsFalse(GetPrivateField<bool>(bullet, "useTerrainAvoidance"));
        Assert.AreEqual(0, GetPrivateField<IList>(bullet, "currentPath").Count);
        Assert.Greater(GetPrivateField<float>(bullet, "terminalImpactMaxDistance"), 0f);
        Assert.Less(GetPrivateField<float>(bullet, "terminalImpactMaxDistance"), 3f);
        Assert.Greater(bulletRigidbody.linearVelocity.y, 0f);
        Assert.Less(Mathf.Abs(bulletRigidbody.linearVelocity.x), 0.001f);
    }

    [Test]
    public void Initialize_WithDirectPath_KeepsTerrainAvoidancePathing()
    {
        GameObject owner = CreateObject("EnemyOwner", Vector2.zero);
        GameObject target = CreateTarget("PlayerTarget", new Vector2(3f, 0f));
        EnemyBullet bullet = CreateBullet(Vector2.zero, out Rigidbody2D bulletRigidbody);

        Physics2D.SyncTransforms();

        bullet.Initialize(
            target.transform,
            owner.transform,
            5f,
            10f,
            10,
            10f,
            GroundMask(),
            true);

        Assert.IsFalse(GetPrivateField<bool>(bullet, "isTerminalImpacting"));
        Assert.IsTrue(GetPrivateField<bool>(bullet, "useTerrainAvoidance"));
        Assert.AreEqual(1, GetPrivateField<IList>(bullet, "currentPath").Count);
        Assert.Greater(bulletRigidbody.linearVelocity.x, 0f);
        Assert.Less(Mathf.Abs(bulletRigidbody.linearVelocity.y), 0.001f);
    }

    private EnemyBullet CreateBullet(Vector2 position, out Rigidbody2D rigidbody2D)
    {
        GameObject bulletObject = CreateObject("EnemyBullet", position);
        rigidbody2D = bulletObject.AddComponent<Rigidbody2D>();
        rigidbody2D.gravityScale = 0f;
        CircleCollider2D collider = bulletObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.1f;

        EnemyBullet bullet = bulletObject.AddComponent<EnemyBullet>();
        InvokePrivate(bullet, "Awake");
        return bullet;
    }

    private GameObject CreateTarget(string name, Vector2 position)
    {
        GameObject target = CreateObject(name, position);
        BoxCollider2D collider = target.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.5f, 1f);
        return target;
    }

    private GameObject CreateGroundObstacle(string name, Vector2 position, Vector2 size)
    {
        GameObject obstacle = CreateObject(name, position);
        obstacle.layer = GroundLayer();
        BoxCollider2D collider = obstacle.AddComponent<BoxCollider2D>();
        collider.size = size;
        return obstacle;
    }

    private GameObject CreateObject(string name, Vector2 position)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.position = position;
        objectsToDestroy.Add(gameObject);
        return gameObject;
    }

    private static LayerMask GroundMask()
    {
        return 1 << GroundLayer();
    }

    private static int GroundLayer()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        Assert.GreaterOrEqual(groundLayer, 0, "Ground layer must exist for EnemyBullet terrain tests.");
        return groundLayer;
    }

    private static T GetPrivateField<T>(EnemyBullet bullet, string fieldName)
    {
        return (T)typeof(EnemyBullet)
            .GetField(fieldName, InstancePrivate)
            .GetValue(bullet);
    }

    private static void InvokePrivate(EnemyBullet bullet, string methodName)
    {
        typeof(EnemyBullet)
            .GetMethod(methodName, InstancePrivate)
            .Invoke(bullet, null);
    }
}
