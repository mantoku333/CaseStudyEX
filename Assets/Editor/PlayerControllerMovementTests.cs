using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Player;
using UnityEngine;
using Object = UnityEngine.Object;

[Category("Gameplay")]
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

    [Test]
    public void GroundState_WhenDescendingSampleBrieflyMissesGround_RemainsGrounded()
    {
        PlayerController controller = CreatePlayer(out Rigidbody2D rigidbody2D);
        rigidbody2D.linearVelocity = Vector2.down;
        SetPrivateField(controller, "groundedLossGraceSeconds", 0.08f);

        InvokePrivate(controller, "ApplyGroundSample", true, 0.02f);
        InvokePrivate(controller, "ApplyGroundSample", false, 0.02f);
        InvokePrivate(controller, "ApplyGroundSample", false, 0.02f);

        Assert.That(controller.IsGrounded, Is.True);
    }

    [Test]
    public void GroundState_WhenMovingUpward_DoesNotRetainLandingGrace()
    {
        PlayerController controller = CreatePlayer(out Rigidbody2D rigidbody2D);
        SetPrivateField(controller, "groundedLossGraceSeconds", 0.08f);

        InvokePrivate(controller, "ApplyGroundSample", true, 0.02f);
        rigidbody2D.linearVelocity = Vector2.up;
        InvokePrivate(controller, "ApplyGroundSample", false, 0.02f);

        Assert.That(controller.IsGrounded, Is.False);
    }

    [Test]
    public void UmbrellaGlide_WhenGrounded_DoesNotReapplyDownwardVelocity()
    {
        PlayerController controller = CreatePlayer(out Rigidbody2D rigidbody2D);
        UmbrellaController umbrellaController =
            controller.GetComponentInChildren<UmbrellaController>();
        PlayerAbilityController abilityController =
            controller.gameObject.AddComponent<PlayerAbilityController>();
        GroundedStateProvider stateProvider = new GroundedStateProvider
        {
            IsGrounded = true
        };

        SetPrivateField(abilityController, "canGlide", true);
        SetPrivateField(umbrellaController, "playerAbilityController", abilityController);
        SetPrivateField(umbrellaController, "playerStateProvider", stateProvider);
        SetPrivateField(umbrellaController, "rigidBody2D", rigidbody2D);
        umbrellaController.SetFallSpeed(3f);
        umbrellaController.SetUmbrellaState(UmbrellaController.UmbrellaState.Open, false);
        rigidbody2D.linearVelocity = new Vector2(0f, -10f);

        InvokePrivate(umbrellaController, "Glide");

        Assert.That(rigidbody2D.linearVelocity.y, Is.EqualTo(-10f).Within(0.001f));
    }

    [Test]
    public void LandingAnimation_WhenDescendingGroundSampleBrieflyDrops_RemainsLocked()
    {
        PlayerController controller = CreatePlayer(out Rigidbody2D rigidbody2D);
        PlayerSpriteAnimator spriteAnimator =
            controller.gameObject.AddComponent<PlayerSpriteAnimator>();
        SetPrivateField(spriteAnimator, "_playerRigidbody", rigidbody2D);
        SetPrivateField(spriteAnimator, "_hasPreviousGrounded", true);
        SetPrivateField(spriteAnimator, "_previousGrounded", false);

        InvokePrivate(spriteAnimator, "UpdateLandingLock", true);
        rigidbody2D.linearVelocity = Vector2.down;
        InvokePrivate(spriteAnimator, "UpdateLandingLock", false);

        Assert.That(GetPrivateField<bool>(spriteAnimator, "_landingLocked"), Is.True);
    }

    [Test]
    public void LandingAnimation_WhenMovingUpward_ReleasesLockImmediately()
    {
        PlayerController controller = CreatePlayer(out Rigidbody2D rigidbody2D);
        PlayerSpriteAnimator spriteAnimator =
            controller.gameObject.AddComponent<PlayerSpriteAnimator>();
        SetPrivateField(spriteAnimator, "_playerRigidbody", rigidbody2D);
        SetPrivateField(spriteAnimator, "_hasPreviousGrounded", true);
        SetPrivateField(spriteAnimator, "_previousGrounded", true);
        SetPrivateField(spriteAnimator, "_landingLocked", true);
        rigidbody2D.linearVelocity = Vector2.up;

        InvokePrivate(spriteAnimator, "UpdateLandingLock", false);

        Assert.That(GetPrivateField<bool>(spriteAnimator, "_landingLocked"), Is.False);
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

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, fieldName);
        return (T)field.GetValue(target);
    }

    private static object InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstancePrivate);
        Assert.That(method, Is.Not.Null, methodName);
        return method.Invoke(target, arguments);
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

    private sealed class GroundedStateProvider : IPlayerViewStateProvider
    {
        public bool IsGrounded { get; set; }
        public bool IsMoving => false;
        public bool IsGliding => !IsGrounded;
        public bool IsUmbrellaOpen => true;
        public bool IsFacingRight => true;
        public bool IsDodging => false;
        public bool IsParrying => false;
        public bool IsUmbrellaChanging => false;
        public bool IsAttacking => false;
        public bool IsRecoilBoosting => false;
    }
}
