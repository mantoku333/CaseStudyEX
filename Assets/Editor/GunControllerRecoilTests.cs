using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class GunControllerRecoilTests
{
    private const BindingFlags InstancePrivate =
        BindingFlags.Instance | BindingFlags.NonPublic;

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

    [TestCase(-8f)]
    [TestCase(8f)]
    public void ShootRecoil_IgnoresVelocityBeforeShot(float velocityBeforeShotX)
    {
        GunController gun = CreateGun(out Rigidbody2D rigidbody2D);
        gun.SetAirRecoilPower(20f);
        rigidbody2D.linearVelocity = new Vector2(velocityBeforeShotX, 6f);

        bool applied = InvokeTryApplyRecoil(gun, Vector2.right, 1f);

        Assert.That(applied, Is.True);
        Assert.That(rigidbody2D.linearVelocity.x, Is.EqualTo(-20f).Within(0.001f));
        Assert.That(rigidbody2D.linearVelocity.y, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void JumpRecoil_IgnoresVelocityBeforeJump()
    {
        GunController gun = CreateGun(out Rigidbody2D rigidbody2D);
        gun.SetAirRecoilPower(20f);
        rigidbody2D.linearVelocity = new Vector2(8f, -12f);

        gun.JumpRecoil();

        Assert.That(rigidbody2D.linearVelocity.x, Is.EqualTo(0f).Within(0.001f));
        Assert.That(rigidbody2D.linearVelocity.y, Is.EqualTo(20f).Within(0.001f));
    }

    [Test]
    public void HorizontalRecoil_PreservesMomentumUntilNormalMoveSpeed()
    {
        GunController gun = CreateGun(out Rigidbody2D rigidbody2D);
        gun.SetAirRecoilPower(20f);
        InvokeTryApplyRecoil(gun, Vector2.right, 1f);

        Assert.That(gun.ShouldPreserveHorizontalRecoil(3.5f), Is.True);

        rigidbody2D.linearVelocity = new Vector2(-3.5f, 0f);

        Assert.That(gun.ShouldPreserveHorizontalRecoil(3.5f), Is.False);
    }

    [Test]
    public void SecondAirRecoil_HasSameLaunchSpeedAsFirstRecoil()
    {
        GunController gun = CreateGun(out Rigidbody2D rigidbody2D);
        gun.SetAirRecoilPower(20f);
        gun.SetRecoilCoolTimes(0f, 0f);

        gun.Shoot(Vector2.right);
        float firstLaunchSpeed = rigidbody2D.linearVelocity.magnitude;

        rigidbody2D.linearVelocity = Vector2.zero;
        gun.Shoot(Vector2.right);
        float secondLaunchSpeed = rigidbody2D.linearVelocity.magnitude;

        Assert.That(firstLaunchSpeed, Is.EqualTo(20f).Within(0.001f));
        Assert.That(secondLaunchSpeed, Is.EqualTo(firstLaunchSpeed).Within(0.001f));
    }

    [Test]
    public void RestoreAllRecoilUses_ClearsCooldownAndRestartsFirstRecoil()
    {
        GunController gun = CreateGun(out _);
        gun.SetRecoilCoolTimes(0.25f, 0.75f);

        gun.Shoot(Vector2.right);

        Assert.That(gun.CurrentCoolTime, Is.EqualTo(0.25f).Within(0.001f));

        gun.RestoreAllRecoilUses();

        Assert.That(gun.CurrentCoolTime, Is.EqualTo(0f).Within(0.001f));
        Assert.That(gun.IsReloading, Is.False);

        gun.Shoot(Vector2.right);

        Assert.That(gun.CurrentCoolTime, Is.EqualTo(0.25f).Within(0.001f));
    }

    [Test]
    public void AirborneRecoil_RaisesOneStartAndStaysActiveThroughGroundFlicker()
    {
        GunController gun = CreateGun(out _);
        TestPlayerStateProvider stateProvider = new TestPlayerStateProvider
        {
            IsGrounded = false
        };
        SetPrivateField(gun, "playerStateProvider", stateProvider);
        int startCount = 0;
        gun.AirborneRecoilStarted += () => startCount++;

        bool applied = InvokeTryApplyRecoil(gun, Vector2.right, 1f);

        Assert.That(applied, Is.True);
        Assert.That(startCount, Is.EqualTo(1));
        Assert.That(gun.IsAirborneRecoilActive, Is.True);

        stateProvider.IsGrounded = true;
        InvokePrivate(gun, "UpdateAirborneRecoilLifecycle");

        Assert.That(startCount, Is.EqualTo(1));
        Assert.That(gun.IsAirborneRecoilActive, Is.True);

        InvokePrivate(gun, "EndRecoil");

        Assert.That(gun.IsAirborneRecoilActive, Is.False);
    }

    [Test]
    public void GroundedRecoil_RaisesStartOnlyAfterBecomingAirborne()
    {
        GunController gun = CreateGun(out _);
        TestPlayerStateProvider stateProvider = new TestPlayerStateProvider
        {
            IsGrounded = true
        };
        SetPrivateField(gun, "playerStateProvider", stateProvider);
        int startCount = 0;
        gun.AirborneRecoilStarted += () => startCount++;

        bool applied = InvokeTryApplyRecoil(gun, Vector2.right, 1f);

        Assert.That(applied, Is.True);
        Assert.That(startCount, Is.Zero);
        Assert.That(gun.IsAirborneRecoilActive, Is.False);

        stateProvider.IsGrounded = false;
        InvokePrivate(gun, "UpdateAirborneRecoilLifecycle");
        InvokePrivate(gun, "UpdateAirborneRecoilLifecycle");

        Assert.That(startCount, Is.EqualTo(1));
        Assert.That(gun.IsAirborneRecoilActive, Is.True);
    }

    private GunController CreateGun(out Rigidbody2D rigidbody2D)
    {
        GameObject player = new GameObject("Player");
        objectsToDestroy.Add(player);
        rigidbody2D = player.AddComponent<Rigidbody2D>();
        rigidbody2D.gravityScale = 0f;

        GameObject gunObject = new GameObject("Gun");
        gunObject.transform.SetParent(player.transform);
        GunController gun = gunObject.AddComponent<GunController>();

        InvokePrivate(gun, "Start");
        return gun;
    }

    private static bool InvokeTryApplyRecoil(
        GunController gun,
        Vector2 direction,
        float multiplier)
    {
        MethodInfo method = typeof(GunController).GetMethod(
            "TryApplyRecoil",
            InstancePrivate);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(gun, new object[] { direction, multiplier });
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstancePrivate);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    private sealed class TestPlayerStateProvider : Player.IPlayerViewStateProvider
    {
        public bool IsGrounded { get; set; }
        public bool IsMoving => false;
        public bool IsGliding => false;
        public bool IsUmbrellaOpen => false;
        public bool IsFacingRight => true;
        public bool IsDodging => false;
        public bool IsParrying => false;
        public bool IsUmbrellaChanging => false;
        public bool IsAttacking => false;
        public bool IsDiveAttacking => false;
        public bool IsDiveAttackLanding => false;
        public bool IsDiveAttackBouncing => false;
        public bool IsRecoilBoosting => false;
    }
}
