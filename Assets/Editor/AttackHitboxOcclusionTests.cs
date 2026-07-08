using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class AttackHitboxOcclusionTests
{
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
    public void ScanCurrentOverlaps_WhenTargetBehindVerticalGroundWall_DoesNotAttack()
    {
        AttackHitbox hitbox = CreateAttackHitbox(Vector2.zero, new Vector2(1.5f, 0f), new Vector2(4f, 2f));
        CountingAttackReceiver receiver = CreateReceiver("Target", new Vector2(3f, 0f), Vector2.one, out _);
        CreateGroundBox("VerticalWall", new Vector2(1.5f, 0f), new Vector2(0.2f, 3f));
        Physics2D.SyncTransforms();

        hitbox.ScanCurrentOverlaps();

        Assert.That(receiver.AttackCount, Is.EqualTo(0));
    }

    [Test]
    public void ScanCurrentOverlaps_WhenTargetBeforeVerticalGroundWall_AttacksOnce()
    {
        AttackHitbox hitbox = CreateAttackHitbox(Vector2.zero, new Vector2(1.25f, 0f), new Vector2(4f, 2f));
        CountingAttackReceiver receiver = CreateReceiver("Target", new Vector2(0.9f, 0f), Vector2.one, out _);
        CreateGroundBox("VerticalWall", new Vector2(2f, 0f), new Vector2(0.2f, 3f));
        Physics2D.SyncTransforms();

        hitbox.ScanCurrentOverlaps();

        Assert.That(receiver.AttackCount, Is.EqualTo(1));
    }

    [Test]
    public void ScanCurrentOverlaps_WhenHorizontalGroundBetweenPlayerAndTarget_DoesNotBlock()
    {
        AttackHitbox hitbox = CreateAttackHitbox(Vector2.zero, new Vector2(0f, 1.5f), new Vector2(3f, 6f));
        CountingAttackReceiver receiver = CreateReceiver("Target", new Vector2(0f, 3f), Vector2.one, out _);
        CreateGroundBox("Floor", new Vector2(0f, 1.5f), new Vector2(3f, 0.2f));
        Physics2D.SyncTransforms();

        hitbox.ScanCurrentOverlaps();

        Assert.That(receiver.AttackCount, Is.EqualTo(1));
    }

    [Test]
    public void ScanCurrentOverlaps_WhenFallThroughPlatformBetweenPlayerAndTarget_DoesNotBlock()
    {
        AttackHitbox hitbox = CreateAttackHitbox(Vector2.zero, new Vector2(1.5f, 0f), new Vector2(4f, 2f));
        CountingAttackReceiver receiver = CreateReceiver("Target", new Vector2(3f, 0f), Vector2.one, out _);
        CreateFallThroughPlatform("FallThroughPlatform", new Vector2(1.5f, 0f), new Vector2(0.2f, 3f));
        Physics2D.SyncTransforms();

        hitbox.ScanCurrentOverlaps();

        Assert.That(receiver.AttackCount, Is.EqualTo(1));
    }

    [Test]
    public void ScanCurrentOverlaps_WhenTargetBehindShutterWallOnDefaultLayer_DoesNotAttack()
    {
        AttackHitbox hitbox = CreateAttackHitbox(Vector2.zero, new Vector2(1.5f, 0f), new Vector2(4f, 2f));
        CountingAttackReceiver receiver = CreateReceiver("Target", new Vector2(3f, 0f), Vector2.one, out _);
        CreateShutterWall("ShutterWall", new Vector2(1.5f, 0f), new Vector2(0.2f, 3f));
        Physics2D.SyncTransforms();

        hitbox.ScanCurrentOverlaps();

        Assert.That(receiver.AttackCount, Is.EqualTo(0));
    }

    [Test]
    public void HitReceiver_IsOnlyAttackedOnceAcrossOverlapScanEnterAndStay()
    {
        AttackHitbox hitbox = CreateAttackHitbox(Vector2.zero, new Vector2(1.5f, 0f), new Vector2(4f, 2f));
        CountingAttackReceiver receiver = CreateReceiver("Target", new Vector2(1f, 0f), Vector2.one, out Collider2D targetCollider);
        Physics2D.SyncTransforms();

        hitbox.ScanCurrentOverlaps();
        InvokeTriggerMethod(hitbox, "OnTriggerEnter2D", targetCollider);
        InvokeTriggerMethod(hitbox, "OnTriggerStay2D", targetCollider);

        Assert.That(receiver.AttackCount, Is.EqualTo(1));
    }

    [Test]
    public void ScanCurrentOverlaps_WhenColliderIsContactDamageOnly_DoesNotAttack()
    {
        AttackHitbox hitbox = CreateAttackHitbox(Vector2.zero, new Vector2(1f, 0f), new Vector2(3f, 2f));
        CountingAttackReceiver receiver = CreateReceiver(
            "Target",
            new Vector2(6f, 0f),
            Vector2.one,
            out _);
        GameObject damageOnly = CreateBox("ContactDamageOnly", new Vector2(1f, 0f), Vector2.one);
        damageOnly.transform.SetParent(receiver.transform, true);
        damageOnly.GetComponent<BoxCollider2D>().isTrigger = true;
        damageOnly.AddComponent<Metroidvania.Enemy.ContactDamageOnlyCollider>();
        Physics2D.SyncTransforms();

        hitbox.ScanCurrentOverlaps();

        Assert.That(receiver.AttackCount, Is.EqualTo(0));
    }

    private AttackHitbox CreateAttackHitbox(Vector2 playerPosition, Vector2 attackPosition, Vector2 attackSize)
    {
        GameObject player = CreateObject("Player", playerPosition);
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
        {
            player.layer = playerLayer;
        }

        Rigidbody2D rigidbody2D = player.AddComponent<Rigidbody2D>();
        rigidbody2D.gravityScale = 0f;

        CapsuleCollider2D bodyCollider = player.AddComponent<CapsuleCollider2D>();
        bodyCollider.size = Vector2.one;
        bodyCollider.direction = CapsuleDirection2D.Vertical;

        GameObject attackObject = CreateObject("AttackHitbox", attackPosition);
        attackObject.transform.SetParent(player.transform, true);

        BoxCollider2D attackCollider = attackObject.AddComponent<BoxCollider2D>();
        attackCollider.isTrigger = true;
        attackCollider.size = attackSize;

        return attackObject.AddComponent<AttackHitbox>();
    }

    private CountingAttackReceiver CreateReceiver(
        string name,
        Vector2 position,
        Vector2 size,
        out Collider2D receiverCollider)
    {
        GameObject receiverObject = CreateObject(name, position);
        receiverCollider = receiverObject.AddComponent<BoxCollider2D>();
        ((BoxCollider2D)receiverCollider).size = size;
        return receiverObject.AddComponent<CountingAttackReceiver>();
    }

    private GameObject CreateGroundBox(string name, Vector2 position, Vector2 size)
    {
        GameObject box = CreateBox(name, position, size);
        box.layer = GroundLayer();
        return box;
    }

    private GameObject CreateFallThroughPlatform(string name, Vector2 position, Vector2 size)
    {
        GameObject platform = CreateBox(name, position, size);
        platform.layer = FallThroughFloorLayer();
        platform.AddComponent<PlatformEffector2D>();
        return platform;
    }

    private GameObject CreateShutterWall(string name, Vector2 position, Vector2 size)
    {
        GameObject shutterRoot = CreateObject(name, Vector2.zero);

        GameObject shutterBlock = CreateBox("ShutterBlock", position, size);
        shutterBlock.transform.SetParent(shutterRoot.transform, true);

        shutterRoot.AddComponent<ShutterWallBlockRise>();
        return shutterRoot;
    }

    private GameObject CreateBox(string name, Vector2 position, Vector2 size)
    {
        GameObject box = CreateObject(name, position);
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

    private static void InvokeTriggerMethod(AttackHitbox hitbox, string methodName, Collider2D collider)
    {
        MethodInfo method = typeof(AttackHitbox).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(hitbox, new object[] { collider });
    }

    private static int GroundLayer()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        Assert.GreaterOrEqual(groundLayer, 0, "Ground layer must exist for AttackHitbox occlusion tests.");
        return groundLayer;
    }

    private static int FallThroughFloorLayer()
    {
        int layer = LayerMask.NameToLayer("FallThroughFloor");
        Assert.GreaterOrEqual(layer, 0, "FallThroughFloor layer must exist for AttackHitbox occlusion tests.");
        return layer;
    }

    private sealed class CountingAttackReceiver : MonoBehaviour, IAttackReceiver
    {
        public int AttackCount { get; private set; }

        public void OnAttacked(AttackHitbox attacker, Collider2D hitCollider)
        {
            AttackCount++;
        }
    }
}
