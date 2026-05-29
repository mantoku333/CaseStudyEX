using System;
using System.Collections;
using System.Reflection;
using GameName.Enemy;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class RoomEnemyActivityManagerTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    private GameObject managerObject;
    private GameObject enemyObject;
    private RoomEnemyActivityManager manager;

    [SetUp]
    public void SetUp()
    {
        ClearRoomCameraTriggerStatics();
        managerObject = new GameObject("RoomEnemyActivityManagerTest");
        manager = managerObject.AddComponent<RoomEnemyActivityManager>();
    }

    [TearDown]
    public void TearDown()
    {
        if (enemyObject != null)
        {
            Object.DestroyImmediate(enemyObject);
        }

        if (managerObject != null)
        {
            Object.DestroyImmediate(managerObject);
        }

        ClearRoomCameraTriggerStatics();
    }

    [Test]
    public void SleepingEnemy_KeepsRootAndRendererActive()
    {
        EnemyController enemy = CreateEnemy(out Rigidbody2D rigidbody2D, out SpriteRenderer renderer);
        RoomEnemyActivityTestBehaviour extraBehaviour = enemyObject.AddComponent<RoomEnemyActivityTestBehaviour>();

        object managedEnemy = CreateManagedEnemy(
            enemy,
            new MonoBehaviour[] { enemy, extraBehaviour },
            new[] { true, true },
            new[] { rigidbody2D },
            new[] { true },
            desiredGameplayActive: false,
            appliedGameplayActive: true);

        InvokeSetManagedEnemyGameplayActive(managedEnemy, false);

        Assert.IsTrue(enemyObject.activeSelf);
        Assert.IsTrue(renderer.enabled);
        Assert.IsFalse(enemy.enabled);
        Assert.IsFalse(extraBehaviour.enabled);
        Assert.IsFalse(rigidbody2D.simulated);
    }

    [Test]
    public void ResumingEnemy_RestoresInitialScriptAndPhysicsStates()
    {
        EnemyController enemy = CreateEnemy(out Rigidbody2D rigidbody2D, out _);
        RoomEnemyActivityTestBehaviour initiallyEnabled = enemyObject.AddComponent<RoomEnemyActivityTestBehaviour>();
        RoomEnemyActivityTestDisabledBehaviour initiallyDisabled = enemyObject.AddComponent<RoomEnemyActivityTestDisabledBehaviour>();
        initiallyDisabled.enabled = false;

        object managedEnemy = CreateManagedEnemy(
            enemy,
            new MonoBehaviour[] { enemy, initiallyEnabled, initiallyDisabled },
            new[] { true, true, false },
            new[] { rigidbody2D },
            new[] { true },
            desiredGameplayActive: true,
            appliedGameplayActive: true);

        InvokeSetManagedEnemyGameplayActive(managedEnemy, false);
        InvokeSetManagedEnemyGameplayActive(managedEnemy, true);

        Assert.IsTrue(enemy.enabled);
        Assert.IsTrue(initiallyEnabled.enabled);
        Assert.IsFalse(initiallyDisabled.enabled);
        Assert.IsTrue(rigidbody2D.simulated);
    }

    [Test]
    public void ApplyEnemyActivity_WithNoResolvedRoom_DoesNotWakeSleepingEnemies()
    {
        EnemyController enemy = CreateEnemy(out Rigidbody2D rigidbody2D, out _);
        object managedEnemy = CreateManagedEnemy(
            enemy,
            new MonoBehaviour[] { enemy },
            new[] { true },
            new[] { rigidbody2D },
            new[] { true },
            desiredGameplayActive: false,
            appliedGameplayActive: false);

        AddManagedEnemy(managedEnemy);
        SetPrivateField(manager, "gatingActive", true);

        InvokePrivate(manager, "ApplyEnemyActivity");

        Assert.IsFalse((bool)GetPrivateField(managedEnemy, "DesiredGameplayActive"));
        Assert.IsFalse((bool)GetPrivateField(manager, "hasPendingEnemyStateChanges"));
    }

    private EnemyController CreateEnemy(out Rigidbody2D rigidbody2D, out SpriteRenderer renderer)
    {
        enemyObject = new GameObject("ManagedEnemy");
        renderer = enemyObject.AddComponent<SpriteRenderer>();
        rigidbody2D = enemyObject.AddComponent<Rigidbody2D>();
        return enemyObject.AddComponent<EnemyController>();
    }

    private object CreateManagedEnemy(
        EnemyController enemy,
        MonoBehaviour[] behaviours,
        bool[] behaviourStates,
        Rigidbody2D[] rigidbodies,
        bool[] simulatedStates,
        bool desiredGameplayActive,
        bool appliedGameplayActive)
    {
        Type managedEnemyType = typeof(RoomEnemyActivityManager).GetNestedType("ManagedEnemy", BindingFlags.NonPublic);
        object managedEnemy = Activator.CreateInstance(managedEnemyType, nonPublic: true);
        SetPrivateField(managedEnemy, "Enemy", enemy);
        SetPrivateField(managedEnemy, "Root", enemy.gameObject);
        SetPrivateField(managedEnemy, "InitialActiveSelf", true);
        SetPrivateField(managedEnemy, "DesiredGameplayActive", desiredGameplayActive);
        SetPrivateField(managedEnemy, "AppliedGameplayActive", appliedGameplayActive);
        SetPrivateField(managedEnemy, "GameplayBehaviours", behaviours);
        SetPrivateField(managedEnemy, "InitialBehaviourEnabled", behaviourStates);
        SetPrivateField(managedEnemy, "Rigidbodies", rigidbodies);
        SetPrivateField(managedEnemy, "InitialRigidbodySimulated", simulatedStates);
        return managedEnemy;
    }

    private void AddManagedEnemy(object managedEnemy)
    {
        IList managedEnemies = (IList)GetPrivateField(manager, "managedEnemies");
        managedEnemies.Add(managedEnemy);
    }

    private void InvokeSetManagedEnemyGameplayActive(object managedEnemy, bool active)
    {
        typeof(RoomEnemyActivityManager)
            .GetMethod("SetManagedEnemyGameplayActive", InstancePrivate)
            .Invoke(manager, new[] { managedEnemy, active });
    }

    private static void InvokePrivate(object target, string methodName)
    {
        target.GetType()
            .GetMethod(methodName, InstancePrivate)
            .Invoke(target, Array.Empty<object>());
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType()
            .GetField(fieldName, InstanceAny)
            .SetValue(target, value);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        return target.GetType()
            .GetField(fieldName, InstanceAny)
            .GetValue(target);
    }

    private static void ClearRoomCameraTriggerStatics()
    {
        Type roomTriggerType = typeof(RoomCameraTrigger);
        roomTriggerType.GetField("_activeTrigger", StaticPrivate).SetValue(null, null);
        roomTriggerType.GetField("_defaultTriggerOverlapCount", StaticPrivate).SetValue(null, 0);

        IList occupiedRoomTriggers = (IList)roomTriggerType
            .GetField("_occupiedRoomTriggers", StaticPrivate)
            .GetValue(null);
        occupiedRoomTriggers.Clear();
    }
}

public sealed class RoomEnemyActivityTestBehaviour : MonoBehaviour
{
}

public sealed class RoomEnemyActivityTestDisabledBehaviour : MonoBehaviour
{
}
