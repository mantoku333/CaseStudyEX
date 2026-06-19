using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Player;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PlayerControllerMovementTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<Object> objectsToDestroy = new List<Object>();

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
    public void Move_WhenMoveInputIsRight_SetsRightVelocityWithoutCollisionClamp()
    {
        PlayerController controller = CreatePlayer(out Rigidbody2D rigidbody2D);
        SetPrivateField(controller, "moveInput", 1f);

        InvokePrivate(controller, "Move");

        Assert.That(rigidbody2D.linearVelocity.x, Is.EqualTo(5f).Within(0.001f));
    }

    [Test]
    public void Move_WhenMovingRightWhileJumping_PreservesVerticalVelocity()
    {
        PlayerController controller = CreatePlayer(out Rigidbody2D rigidbody2D);
        rigidbody2D.linearVelocity = new Vector2(0f, 7f);
        SetPrivateField(controller, "moveInput", 1f);

        InvokePrivate(controller, "Move");

        Assert.That(rigidbody2D.linearVelocity.x, Is.EqualTo(5f).Within(0.001f));
        Assert.That(rigidbody2D.linearVelocity.y, Is.EqualTo(7f).Within(0.001f));
    }

    [TestCase(1f, true)]
    [TestCase(-1f, false)]
    public void AttackFacing_WhenMoving_UsesMovementDirection(float moveInput, bool expectedFacingRight)
    {
        PlayerController controller = CreatePlayer(out _);
        SetPrivateField(controller, "moveInput", moveInput);
        SetPrivateField(controller, "isFacingRight", !expectedFacingRight);

        InvokePrivate(controller, "UpdateAttackFacingFromMovementOrAim");

        Assert.That(controller.IsFacingRight, Is.EqualTo(expectedFacingRight));
    }

    [TestCase(1f, 1f)]
    [TestCase(-1f, -1f)]
    public void ResolveAttackDirection_WhenMoving_ReturnsHorizontalMovementDirection(
        float moveInput,
        float expectedDirectionX)
    {
        PlayerController controller = CreatePlayer(out _);
        SetPrivateField(controller, "moveInput", moveInput);

        bool resolved = InvokeTryResolveAttackDirection(controller, out Vector2 attackDirection);

        Assert.That(resolved, Is.True);
        Assert.That(attackDirection, Is.EqualTo(new Vector2(expectedDirectionX, 0f)));
    }

    private PlayerController CreatePlayer(out Rigidbody2D rigidbody2D)
    {
        GameObject playerObject = new GameObject("Player");
        playerObject.SetActive(false);
        objectsToDestroy.Add(playerObject);

        rigidbody2D = playerObject.AddComponent<Rigidbody2D>();
        rigidbody2D.gravityScale = 0f;
        playerObject.AddComponent<BoxCollider2D>();

        GameObject umbrellaObject = new GameObject("Umbrella");
        umbrellaObject.transform.SetParent(playerObject.transform);
        objectsToDestroy.Add(umbrellaObject);
        UmbrellaController umbrellaController = umbrellaObject.AddComponent<UmbrellaController>();

        PlayerController controller = playerObject.AddComponent<PlayerController>();
        PlayerStatsData stats = ScriptableObject.CreateInstance<PlayerStatsData>();
        objectsToDestroy.Add(stats);
        stats.SetMoveSpeed(5f);
        stats.SetGlideMoveSpeed(3f);

        SetPrivateField(controller, "rigidBody2d", rigidbody2D);
        SetPrivateField(controller, "playerStatsData", stats);
        SetPrivateField(controller, "umbrellaController", umbrellaController);
        SetPrivateField(controller, "collisionMover", null);
        SetPrivateField(controller, "isGround", false);
        playerObject.SetActive(true);

        return controller;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private static object InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstancePrivate);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(target, null);
    }

    private static bool InvokeTryResolveAttackDirection(
        PlayerController controller,
        out Vector2 attackDirection)
    {
        MethodInfo method = controller.GetType().GetMethod(
            "TryResolveAttackDirection",
            InstancePrivate);
        Assert.That(method, Is.Not.Null);

        object[] arguments = { Vector2.zero };
        bool resolved = (bool)method.Invoke(controller, arguments);
        attackDirection = (Vector2)arguments[0];
        return resolved;
    }
}
