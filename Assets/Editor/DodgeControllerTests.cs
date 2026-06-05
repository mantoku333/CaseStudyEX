using System.Collections;
using System.Collections.Generic;
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

    private DodgeController CreatePlayer(Vector2 position, out Rigidbody2D rigidbody2D)
    {
        GameObject playerObject = new GameObject("Player");
        playerObject.transform.position = position;
        objectsToDestroy.Add(playerObject);

        rigidbody2D = playerObject.AddComponent<Rigidbody2D>();
        rigidbody2D.gravityScale = 0f;
        rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CapsuleCollider2D capsule = playerObject.AddComponent<CapsuleCollider2D>();
        capsule.size = Vector2.one;
        capsule.direction = CapsuleDirection2D.Vertical;

        return playerObject.AddComponent<DodgeController>();
    }

    private static void AssertPosition(Rigidbody2D rigidbody2D, Vector2 expectedPosition)
    {
        Assert.That(rigidbody2D.position.x, Is.EqualTo(expectedPosition.x).Within(0.001f));
        Assert.That(rigidbody2D.position.y, Is.EqualTo(expectedPosition.y).Within(0.001f));
    }
}
