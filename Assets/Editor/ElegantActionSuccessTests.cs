using System.Collections.Generic;
using System.Reflection;
using GameName.Enemy;
using Metroidvania.Enemy;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class ElegantActionSuccessTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(-2f, 2f, 0f, true)]
    [TestCase(2f, -2f, 0f, true)]
    [TestCase(-2f, -0.1f, 0f, false)]
    [TestCase(-2f, 2f, 3f, false)]
    [TestCase(0f, 0f, 0f, false)]
    public void Passage_RequiresCrossingNotTouchingOrPassingAbove(float start, float end, float y, bool expected)
    {
        Assert.That(ElegantActionGeometry.PassedThrough(new Bounds(Vector3.zero, Vector3.one),
            new Vector2(start, y), new Vector2(end, y), Vector2.one * 0.25f), Is.EqualTo(expected));
    }

    [TestCase(1, 2f, -2f, 1f, true)]
    [TestCase(-1, -2f, 2f, 1f, true)]
    [TestCase(1, -2f, 2f, 1f, false)]
    [TestCase(1, 2f, -2f, 0.4f, false)]
    [TestCase(1, 2f, -2f, 4f, false)]
    public void HeadCrossing_RequiresFrontToBackWithinHeightBand(int facing, float from, float to, float height, bool expected)
    {
        Assert.That(ElegantActionGeometry.CrossedOverHead(new Bounds(Vector3.zero, Vector3.one), facing,
            new Vector2(from, height), new Vector2(to, height), 0.05f, 2f), Is.EqualTo(expected));
    }

    [TestCase(false, false, 1)]
    [TestCase(true, false, 1)]
    [TestCase(true, true, 0)]
    public void SweptDodge_DetectsEnemyAndHostileBulletOnlyOnce(bool projectile, bool reflected, int expected)
    {
        var root = new GameObject("Success sensor test");
        var target = new GameObject("Passage target");
        Vector3 origin = new Vector3(10000, 10000, 0);
        root.transform.position = origin + Vector3.left * 2f;
        root.AddComponent<BoxCollider2D>().size = Vector2.one * 0.5f;
        target.transform.position = origin;
        target.AddComponent<BoxCollider2D>();
        var sensor = root.AddComponent<ElegantActionSuccessSensor>();
        if (projectile)
        {
            var bullet = target.AddComponent<EnemyBullet>();
            Set(bullet, "isReflectedByPlayer", reflected);
        }
        else
        {
            var enemy = target.AddComponent<EnemyController>();
            Set(enemy, "currentHealth", 10);
        }
        int successes = 0;
        sensor.Succeeded += _ => successes++;
        try
        {
            Physics2D.SyncTransforms();
            Invoke(sensor, "OnEnable");
            Invoke(sensor, "BeginDodge");
            root.transform.position = origin + Vector3.right * 2f;
            Physics2D.SyncTransforms();
            Bounds body = root.GetComponent<Collider2D>().bounds;
            Invoke(sensor, "SampleDodge", body, (Vector2)body.center);
            Invoke(sensor, "SampleDodge", body, (Vector2)body.center);
            Assert.That(successes, Is.EqualTo(expected));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(target); }
    }

    [Test]
    public void Controller_SuccessesPublishLevelsOneThroughFour()
    {
        var root = new GameObject("Chain event test");
        var controller = root.AddComponent<PlayerElegantPointController>();
        var levels = new List<int>();
        controller.LevelChanged += levels.Add;
        try
        {
            controller.NotifySuccessfulAction(ElegantActionType.Glide);
            controller.NotifyActionStarted(ElegantActionType.DiveAttack);
            controller.NotifyAttackHit(ElegantActionType.DiveAttack);
            controller.NotifyAttackHit(ElegantActionType.DiveAttack);
            controller.NotifySuccessfulAction(ElegantActionType.Dodge);
            controller.NotifySuccessfulAction(ElegantActionType.RecoilMove);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, levels);
            controller.NotifyActionStarted(ElegantActionType.Dodge);
            Assert.That(controller.CurrentChainLength, Is.EqualTo(4), "Input alone cannot count.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void OverheadHistory_IsEnemySpecificAndConsumedOnce()
    {
        var root = new GameObject("Head crossing sensor");
        var target = new GameObject("Head crossing target");
        Vector3 origin = new Vector3(11000, 10000, 0);
        root.transform.position = origin;
        target.transform.position = origin;
        target.AddComponent<BoxCollider2D>();
        var enemy = target.AddComponent<EnemyController>();
        Set(enemy, "currentHealth", 10);
        Set(enemy, "moveDirection", 1);
        var sensor = root.AddComponent<ElegantActionSuccessSensor>();
        try
        {
            Physics2D.SyncTransforms();
            Invoke(sensor, "OnEnable");
            Set(sensor, "previousFeet", (Vector2)(origin + new Vector3(2f, 1f)));
            Invoke(sensor, "SampleOverhead", (Vector2)(origin + new Vector3(-2f, 1f)));
            Assert.That(sensor.ConsumeOverheadCrossing(enemy), Is.True);
            Assert.That(sensor.ConsumeOverheadCrossing(enemy), Is.False);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(target); }
    }

    [TestCase(false, true, true, 0)]
    [TestCase(true, false, true, 0)]
    [TestCase(true, true, false, 0)]
    [TestCase(true, true, true, 1)]
    public void NormalAttack_RequiresSameEnemyHeadCrossingAndRearKill(bool killed, bool behind, bool crossed, int expected)
    {
        var root = new GameObject("Rear kill test");
        var target = new GameObject("Crossed enemy");
        var enemy = target.AddComponent<BoxCollider2D>();
        var controller = root.AddComponent<PlayerElegantPointController>();
        var sensor = root.GetComponent<ElegantActionSuccessSensor>();
        Set(controller, "successSensor", sensor);
        try
        {
            if (crossed)
            {
                var history = (Dictionary<Component, float>)typeof(ElegantActionSuccessSensor)
                    .GetField("crossedEnemies", Private).GetValue(sensor);
                history[enemy] = Time.time;
            }
            controller.NotifyActionStarted(ElegantActionType.NormalAttack);
            Invoke(controller, "HandleNormalAttackResult", enemy, killed, behind);
            controller.NotifyAttackHit(ElegantActionType.NormalAttack);
            controller.NotifyAttackEnded(ElegantActionType.NormalAttack, true);
            Assert.That(controller.CurrentChainLength, Is.EqualTo(expected));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(target); }
    }

    [Test]
    public void NextUnsuccessfulAttack_CannotHoldPreviousSuccessWindowOpen()
    {
        var chain = new ElegantActionChain();
        chain.RegisterSuccess(ElegantActionType.Glide);
        chain.RegisterAttackStart(ElegantActionType.DiveAttack);
        chain.RegisterAttackHit(ElegantActionType.DiveAttack);
        Assert.That(chain.LatestSuccessBelongsToCurrentAttack, Is.True);
        chain.RegisterAttackEnd(ElegantActionType.DiveAttack, true);
        chain.RegisterAttackStart(ElegantActionType.DiveAttack);
        Assert.That(chain.LatestSuccessBelongsToCurrentAttack, Is.False);
        Assert.That(chain.Advance(1f, false), Is.EqualTo(20));
    }

    [TestCase(0.5f, false, 0)]
    [TestCase(2.5f, false, 1)]
    [TestCase(2.5f, true, 1)]
    [TestCase(8f, false, 0)]
    public void Recoil_RequiresDistanceAndCountsFinalSampleOnce(float distance, bool jump, int expected)
    {
        var root = new GameObject("Recoil success test");
        var gun = root.AddComponent<GunController>();
        var controller = root.AddComponent<PlayerElegantPointController>();
        Set(controller, "recoilController", gun);
        Set(gun, "<CurrentRecoilIsJump>k__BackingField", jump);
        try
        {
            Invoke(controller, "HandleAirborneRecoilStarted");
            root.transform.position = Vector3.right * distance;
            Invoke(controller, "UpdateRecoilSuccess");
            Invoke(controller, "UpdateRecoilSuccess");
            Assert.That(controller.CurrentChainLength, Is.EqualTo(expected));
            if (expected > 0) Assert.That(controller.LastAction,
                Is.EqualTo(jump ? ElegantActionType.RecoilJump : ElegantActionType.RecoilMove));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void InterruptedGlideSession_ExpiresAndResumesAtLevelOne()
    {
        int balance = ElegantPointWallet.Balance;
        var root = new GameObject("Glide timeout integration");
        var umbrella = root.AddComponent<UmbrellaController>();
        var points = root.AddComponent<PlayerElegantPointController>();
        Set(points, "glideController", umbrella);
        var levels = new List<int>();
        points.LevelChanged += levels.Add;
        try
        {
            Set(umbrella, "<IsGlideActionActive>k__BackingField", true);
            Set(umbrella, "<IsGlideMotionActive>k__BackingField", true);
            points.NotifySuccessfulAction(ElegantActionType.Glide);
            Invoke(points, "AdvanceActions", 2f);
            Assert.That(points.CurrentChainLength, Is.EqualTo(1), "Actual sustained glide holds its window open.");
            Set(umbrella, "<IsGlideMotionActive>k__BackingField", false);
            Invoke(points, "AdvanceActions", 0.6f);
            Invoke(points, "AdvanceActions", 0f);
            Assert.That(points.CurrentChainLength, Is.EqualTo(1), "Pause must not consume the window.");
            Invoke(points, "AdvanceActions", 0.5f);
            Assert.That(points.CurrentChainLength, Is.Zero, "Session membership alone must not keep the chain alive.");
            Set(umbrella, "<IsGlideMotionActive>k__BackingField", true);
            Invoke(points, "AdvanceActions", 0.02f);
            Assert.That(points.CurrentChainLength, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { 1, 0, 1 }, levels);
            points.NotifySuccessfulAction(ElegantActionType.RecoilMove);
            Set(umbrella, "<IsGlideMotionActive>k__BackingField", false);
            Invoke(points, "AdvanceActions", 0.02f);
            Invoke(points, "AdvanceActions", 1.1f);
            Assert.That(points.CurrentChainLength, Is.Zero);
            Set(umbrella, "<IsGlideMotionActive>k__BackingField", true);
            Invoke(points, "AdvanceActions", 0.02f);
            Assert.That(points.CurrentChainLength, Is.EqualTo(1), "The second chain must not restart as level two.");
        }
        finally
        {
            Object.DestroyImmediate(root);
            ElegantPointWallet.Clear();
            ElegantPointWallet.Add(balance);
        }
    }

    [Test]
    public void SuccessDuringGlide_DoesNotSettleWhileTheGlideContinues()
    {
        int balance = ElegantPointWallet.Balance;
        var root = new GameObject("Glide continuation integration");
        var umbrella = root.AddComponent<UmbrellaController>();
        var points = root.AddComponent<PlayerElegantPointController>();
        Set(points, "glideController", umbrella);
        try
        {
            Set(umbrella, "<IsGlideActionActive>k__BackingField", true);
            Set(umbrella, "<IsGlideMotionActive>k__BackingField", true);
            points.NotifySuccessfulAction(ElegantActionType.Glide);
            points.NotifySuccessfulAction(ElegantActionType.Dodge);
            Invoke(points, "AdvanceActions", 3f);
            Assert.That(points.CurrentChainLength, Is.EqualTo(2),
                "A later success must not end the chain while the glide itself is still going.");
            Set(umbrella, "<IsGlideMotionActive>k__BackingField", false);
            Invoke(points, "AdvanceActions", 1.1f);
            Assert.That(points.CurrentChainLength, Is.Zero, "Ending the glide settles the chain.");
        }
        finally
        {
            Object.DestroyImmediate(root);
            ElegantPointWallet.Clear();
            ElegantPointWallet.Add(balance);
        }
    }

    private static void Set(object instance, string field, object value) => instance.GetType().GetField(field, Private).SetValue(instance, value);
    private static void Invoke(object instance, string method, params object[] args) => instance.GetType().GetMethod(method, Private).Invoke(instance, args);
}
