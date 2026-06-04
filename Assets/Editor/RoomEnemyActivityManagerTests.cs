using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GameName.Enemy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class RoomEnemyActivityManagerTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

    private GameObject managerObject;
    private GameObject enemyObject;
    private RoomEnemyActivityManager manager;
    private readonly List<GameObject> createdObjects = new List<GameObject>();

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
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
        enemyObject = null;

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

    [Test]
    public void PlayerInsideHomeRoom_WakesEnemyEvenWhenActiveCameraRoomDiffers()
    {
        // プレイヤーが敵の所属ルーム内にいるなら、別カメラがアクティブでも敵を起こす。
        SetManagedScene();
        RoomCameraTrigger homeRoom = CreateRoom("HomeRoom", Vector2.zero, new Vector2(4f, 4f));
        RoomCameraTrigger activeRoom = CreateRoom("ActiveOverlapRoom", new Vector2(1f, 0f), new Vector2(4f, 4f));
        CreatePlayer(new Vector3(1f, 0f, 0f));

        EnemyController enemy = CreateEnemy(out Rigidbody2D rigidbody2D, out _);
        object managedEnemy = CreateManagedEnemy(
            enemy,
            new MonoBehaviour[] { enemy },
            new[] { true },
            new[] { rigidbody2D },
            new[] { true },
            desiredGameplayActive: false,
            appliedGameplayActive: false,
            room: homeRoom);

        AddManagedEnemy(managedEnemy);
        SetPrivateField(manager, "gatingActive", true);
        SetActiveRoom(activeRoom);

        InvokePrivate(manager, "HandleActiveRoomChanged", activeRoom);

        Assert.IsTrue((bool)GetPrivateField(managedEnemy, "DesiredGameplayActive"));
    }

    [Test]
    public void EnemyOutsideHomeRoom_WakesAndStartsReturnHome()
    {
        // 敵が所属ルーム外へ出た場合は、眠らせずに初期位置への帰還を開始する。
        SetManagedScene();
        RoomCameraTrigger homeRoom = CreateRoom("HomeRoom", Vector2.zero, new Vector2(4f, 4f));
        EnemyController enemy = CreateEnemy(out Rigidbody2D rigidbody2D, out _);
        MoveEnemy(enemy, rigidbody2D, new Vector3(5f, 0f, 0f));

        object managedEnemy = CreateManagedEnemy(
            enemy,
            new MonoBehaviour[] { enemy },
            new[] { true },
            new[] { rigidbody2D },
            new[] { true },
            desiredGameplayActive: false,
            appliedGameplayActive: false,
            room: homeRoom);

        AddManagedEnemy(managedEnemy);
        SetPrivateField(manager, "gatingActive", true);

        InvokePrivate(manager, "ApplyEnemyActivity");

        Assert.IsTrue((bool)GetPrivateField(managedEnemy, "DesiredGameplayActive"));
        Assert.IsTrue(enemy.IsReturningHome);
        Assert.AreEqual(0f, enemy.OriginalStartPosition.x, 0.001f);
    }

    [Test]
    public void EnemyInOverlappingActiveRoom_ReturnsToOriginalStart()
    {
        // ルーム重なり部で別カメラに映る敵は、その場に残さず所属ルームの初期位置へ戻す。
        SetManagedScene();
        RoomCameraTrigger activeRoom = CreateRoom("Room1", new Vector2(-1f, 0f), new Vector2(4f, 4f));
        RoomCameraTrigger homeRoom = CreateRoom("Room2", new Vector2(1f, 0f), new Vector2(4f, 4f));
        EnemyController enemy = CreateEnemy(out Rigidbody2D rigidbody2D, out _);
        MoveEnemy(enemy, rigidbody2D, new Vector3(0.75f, 0f, 0f));

        object managedEnemy = CreateManagedEnemy(
            enemy,
            new MonoBehaviour[] { enemy },
            new[] { true },
            new[] { rigidbody2D },
            new[] { true },
            desiredGameplayActive: false,
            appliedGameplayActive: false,
            room: homeRoom);

        AddManagedEnemy(managedEnemy);
        SetPrivateField(manager, "gatingActive", true);
        SetActiveRoom(activeRoom);

        InvokePrivate(manager, "ApplyEnemyActivity");

        Assert.IsTrue((bool)GetPrivateField(managedEnemy, "DesiredGameplayActive"));
        Assert.IsTrue(enemy.IsReturningHome);
        Assert.AreEqual(0f, enemy.OriginalStartPosition.x, 0.001f);
    }

    [Test]
    public void ReturningEnemy_DoesNotSleepWhenPlayerLeavesHomeRoom()
    {
        // 帰還中の敵は、プレイヤーやカメラが別ルームへ移っても物理・戦闘判定を止めない。
        SetManagedScene();
        RoomCameraTrigger homeRoom = CreateRoom("HomeRoom", Vector2.zero, new Vector2(4f, 4f));
        RoomCameraTrigger activeRoom = CreateRoom("OtherRoom", new Vector2(6f, 0f), new Vector2(4f, 4f));
        CreatePlayer(new Vector3(6f, 0f, 0f));

        EnemyController enemy = CreateEnemy(out Rigidbody2D rigidbody2D, out _);
        MoveEnemy(enemy, rigidbody2D, new Vector3(5f, 0f, 0f));
        enemy.StartReturnHome();

        object managedEnemy = CreateManagedEnemy(
            enemy,
            new MonoBehaviour[] { enemy },
            new[] { true },
            new[] { rigidbody2D },
            new[] { true },
            desiredGameplayActive: true,
            appliedGameplayActive: true,
            room: homeRoom);

        AddManagedEnemy(managedEnemy);
        SetPrivateField(manager, "gatingActive", true);
        SetActiveRoom(activeRoom);

        InvokePrivate(manager, "HandleActiveRoomChanged", activeRoom);

        Assert.IsTrue((bool)GetPrivateField(managedEnemy, "DesiredGameplayActive"));
        Assert.IsTrue(enemy.IsReturningHome);
    }

    [Test]
    public void EnemyInsideHomeRoom_SleepsWhenPlayerIsOutsideHomeRoom()
    {
        // 敵が所属ルーム内に戻っていて、プレイヤーがそのルーム外なら従来どおり眠らせる。
        SetManagedScene();
        RoomCameraTrigger homeRoom = CreateRoom("HomeRoom", Vector2.zero, new Vector2(4f, 4f));
        CreatePlayer(new Vector3(6f, 0f, 0f));

        EnemyController enemy = CreateEnemy(out Rigidbody2D rigidbody2D, out _);
        object managedEnemy = CreateManagedEnemy(
            enemy,
            new MonoBehaviour[] { enemy },
            new[] { true },
            new[] { rigidbody2D },
            new[] { true },
            desiredGameplayActive: true,
            appliedGameplayActive: true,
            room: homeRoom);

        AddManagedEnemy(managedEnemy);
        SetPrivateField(manager, "gatingActive", true);

        InvokePrivate(manager, "ApplyEnemyActivity");

        Assert.IsFalse((bool)GetPrivateField(managedEnemy, "DesiredGameplayActive"));
        Assert.IsTrue((bool)GetPrivateField(manager, "hasPendingEnemyStateChanges"));
    }

    [Test]
    public void EnemyController_ReturnHomeMovesTowardOriginalStartAndResetsPatrolOrigin()
    {
        // 帰還完了後の巡回基準が、タックル後の位置ではなく元の初期位置へ戻ることを確認する。
        GameObject returnEnemyObject = new GameObject("ReturnHomeEnemy");
        createdObjects.Add(returnEnemyObject);
        returnEnemyObject.transform.position = Vector3.zero;
        EnemyController enemy = returnEnemyObject.AddComponent<EnemyController>();
        SetPrivateField(enemy, "moveSpeed", 2f);

        returnEnemyObject.transform.position = new Vector3(1f, 0f, 0f);
        enemy.StartReturnHome();
        InvokePrivate(enemy, "FixedUpdate");

        Assert.Less(returnEnemyObject.transform.position.x, 1f);

        returnEnemyObject.transform.position = new Vector3(0.001f, 0f, 0f);
        InvokePrivate(enemy, "FixedUpdate");

        Vector3 patrolOrigin = (Vector3)GetPrivateField(enemy, "startPosition");
        Assert.IsFalse(enemy.IsReturningHome);
        Assert.AreEqual(0f, returnEnemyObject.transform.position.x, 0.001f);
        Assert.AreEqual(0f, patrolOrigin.x, 0.001f);
    }

    private EnemyController CreateEnemy(out Rigidbody2D rigidbody2D, out SpriteRenderer renderer)
    {
        enemyObject = new GameObject("ManagedEnemy");
        createdObjects.Add(enemyObject);
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
        bool appliedGameplayActive,
        RoomCameraTrigger room = null)
    {
        Type managedEnemyType = typeof(RoomEnemyActivityManager).GetNestedType("ManagedEnemy", BindingFlags.NonPublic);
        object managedEnemy = Activator.CreateInstance(managedEnemyType, nonPublic: true);
        SetPrivateField(managedEnemy, "Enemy", enemy);
        SetPrivateField(managedEnemy, "Root", enemy.gameObject);
        SetPrivateField(managedEnemy, "Room", room);
        SetPrivateField(managedEnemy, "OriginalStartPosition", enemy.OriginalStartPosition);
        SetPrivateField(managedEnemy, "InitialActiveSelf", true);
        SetPrivateField(managedEnemy, "DesiredGameplayActive", desiredGameplayActive);
        SetPrivateField(managedEnemy, "AppliedGameplayActive", appliedGameplayActive);
        SetPrivateField(managedEnemy, "TackleAttacks", enemy.GetComponentsInChildren<EnemyTackleAttack>(true));
        SetPrivateField(managedEnemy, "GameplayBehaviours", behaviours);
        SetPrivateField(managedEnemy, "InitialBehaviourEnabled", behaviourStates);
        SetPrivateField(managedEnemy, "Rigidbodies", rigidbodies);
        SetPrivateField(managedEnemy, "InitialRigidbodySimulated", simulatedStates);
        return managedEnemy;
    }

    private RoomCameraTrigger CreateRoom(string name, Vector2 center, Vector2 size)
    {
        GameObject roomObject = new GameObject(name);
        createdObjects.Add(roomObject);
        roomObject.transform.position = center;
        BoxCollider2D collider = roomObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = size;
        return roomObject.AddComponent<RoomCameraTrigger>();
    }

    private GameObject CreatePlayer(Vector3 position)
    {
        GameObject player = new GameObject("Player");
        createdObjects.Add(player);
        player.tag = "Player";
        player.transform.position = position;
        return player;
    }

    private static void MoveEnemy(EnemyController enemy, Rigidbody2D rigidbody2D, Vector3 position)
    {
        enemy.transform.position = position;
        rigidbody2D.position = position;
    }

    private void SetManagedScene()
    {
        SetPrivateField(manager, "managedScene", SceneManager.GetActiveScene());
    }

    private static void SetActiveRoom(RoomCameraTrigger room)
    {
        typeof(RoomCameraTrigger)
            .GetField("_activeTrigger", StaticPrivate)
            .SetValue(null, room);
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

    private static void InvokePrivate(object target, string methodName, params object[] args)
    {
        target.GetType()
            .GetMethod(methodName, InstancePrivate)
            .Invoke(target, args ?? Array.Empty<object>());
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
