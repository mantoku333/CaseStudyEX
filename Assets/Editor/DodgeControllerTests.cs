using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class DodgeControllerTests
{
    private readonly List<GameObject> objectsToDestroy = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;

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
    public IEnumerator CancelCurrentDodgeMovement_WhenDodgeIsActive_KeepsWarpDestinationUntilDodgeEnds()
    {
        DodgeController dodgeController = CreatePlayer(Vector2.zero, out Rigidbody2D rigidbody2D);
        dodgeController.SetDodgeDistance(3f);
        dodgeController.SetDodgeDuration(0.2f);

        dodgeController.Dodge(Vector2.right);

        Assert.That(dodgeController.IsDodging(), Is.True);

        Vector2 warpDestination = new Vector2(10f, -2f);
        dodgeController.CancelCurrentDodgeMovement();
        rigidbody2D.position = warpDestination;
        rigidbody2D.transform.position = warpDestination;
        rigidbody2D.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();

        yield return new WaitForFixedUpdate();
        yield return null;

        AssertPosition(rigidbody2D, warpDestination);
        Assert.That(dodgeController.IsDodging(), Is.True);

        float timeoutTime = Time.realtimeSinceStartup + 1f;
        while (dodgeController.IsDodging() && Time.realtimeSinceStartup < timeoutTime)
        {
            yield return new WaitForFixedUpdate();
        }

        Assert.That(dodgeController.IsDodging(), Is.False);
        AssertPosition(rigidbody2D, warpDestination);
    }

    [Test]
    public void ResolveReachableDodgeTarget_WhenTargetIsInsideThickWall_StopsBeforeFirstBlockingBlock()
    {
        DodgeController dodgeController = CreatePlayer(Vector2.zero, out Rigidbody2D rigidbody2D);
        Collider2D playerCollider = rigidbody2D.GetComponent<Collider2D>();
        dodgeController.SetDodgeDistance(4f);

        CreateGroundBox("ThickWall", new Vector2(2.5f, 0f), new Vector2(3f, 5f));
        Physics2D.SyncTransforms();

        InvokePrivate(dodgeController, "EnsureComponents");
        Vector2 targetPosition = (Vector2)InvokePrivate(
            dodgeController,
            "ResolveReachableDodgeTarget",
            rigidbody2D.position,
            Vector2.right * dodgeController.GetDodgeDistance());

        Assert.That(targetPosition.x, Is.GreaterThan(0.4f));
        Assert.That(targetPosition.x, Is.LessThanOrEqualTo(0.5f));

        rigidbody2D.position = targetPosition;
        rigidbody2D.transform.position = targetPosition;
        Physics2D.SyncTransforms();
        Assert.That(CountGroundOverlaps(playerCollider), Is.EqualTo(0));
    }

    [Test]
    public void ResolveReachableDodgeTarget_WhenStartedAtFarStageCoordinatesAndWallIsNearby_StopsBeforeWall()
    {
        Vector2 startPosition = new Vector2(600f, 100f);
        DodgeController dodgeController = CreatePlayer(startPosition, out Rigidbody2D rigidbody2D);
        Collider2D playerCollider = rigidbody2D.GetComponent<Collider2D>();
        dodgeController.SetDodgeDistance(4f);

        CreateGroundBox("FarCoordinateThickWall", startPosition + new Vector2(2.5f, 0f), new Vector2(3f, 5f));
        Physics2D.SyncTransforms();

        InvokePrivate(dodgeController, "EnsureComponents");
        Vector2 targetPosition = (Vector2)InvokePrivate(
            dodgeController,
            "ResolveReachableDodgeTarget",
            rigidbody2D.position,
            Vector2.right * dodgeController.GetDodgeDistance());

        Assert.That(targetPosition.x, Is.GreaterThan(startPosition.x + 0.4f));
        Assert.That(targetPosition.x, Is.LessThanOrEqualTo(startPosition.x + 0.5f));

        rigidbody2D.position = targetPosition;
        rigidbody2D.transform.position = targetPosition;
        Physics2D.SyncTransforms();
        Assert.That(CountGroundOverlaps(playerCollider), Is.EqualTo(0));
    }

    private DodgeController CreatePlayer(Vector2 position, out Rigidbody2D rigidbody2D)
    {
        GameObject playerObject = new GameObject("Player");
        playerObject.transform.position = position;
        objectsToDestroy.Add(playerObject);

        rigidbody2D = playerObject.AddComponent<Rigidbody2D>();
        rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
        rigidbody2D.gravityScale = 0f;
        rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CapsuleCollider2D capsule = playerObject.AddComponent<CapsuleCollider2D>();
        capsule.size = Vector2.one;
        capsule.direction = CapsuleDirection2D.Vertical;

        return playerObject.AddComponent<DodgeController>();
    }

    private GameObject CreateGroundBox(string name, Vector2 position, Vector2 size)
    {
        GameObject box = new GameObject(name);
        box.transform.position = position;
        box.layer = GroundLayer();
        objectsToDestroy.Add(box);

        BoxCollider2D collider = box.AddComponent<BoxCollider2D>();
        collider.size = size;
        return box;
    }

    private static int CountGroundOverlaps(Collider2D playerCollider)
    {
        Collider2D[] overlaps = new Collider2D[8];
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = false
        };
        filter.SetLayerMask(1 << GroundLayer());
        return playerCollider.Overlap(filter, overlaps);
    }

    private static int GroundLayer()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        Assert.GreaterOrEqual(groundLayer, 0, "Ground layer must exist for dodge tests.");
        return groundLayer;
    }

    private static object InvokePrivate(DodgeController dodgeController, string methodName, params object[] arguments)
    {
        MethodInfo method = typeof(DodgeController).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null, $"{methodName} must exist.");
        return method.Invoke(dodgeController, arguments);
    }

    private static void AssertPosition(Rigidbody2D rigidbody2D, Vector2 expectedPosition)
    {
        Assert.That(rigidbody2D.position.x, Is.EqualTo(expectedPosition.x).Within(0.001f));
        Assert.That(rigidbody2D.position.y, Is.EqualTo(expectedPosition.y).Within(0.001f));
    }
}
