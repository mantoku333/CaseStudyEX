using System.Collections;
using System.IO;
using System.Reflection;
using GameName.Enemy;
using Metroidvania.Enemy;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class FixBeta03EnemyActivityManagerSceneTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string ScenePath = "Assets/Scenes/Fix_Beta03.unity";

    private GameObject managerObject;

    [TearDown]
    public void TearDown()
    {
        if (managerObject != null)
        {
            Object.DestroyImmediate(managerObject);
        }
    }

    [Test]
    public void FixBeta03_EnemyActivityManagerAssignsAndSleepsOffRoomEnemies()
    {
        ValidateFixBeta03EnemyActivityManager(CreateManager());
    }

    public static void RunCommandLineValidation()
    {
        GameObject commandLineManagerObject = null;
        string resultPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "FixBeta03EnemyActivityManagerSceneValidation.txt");

        try
        {
            commandLineManagerObject = new GameObject("FixBeta03EnemyActivityManagerCommandLineValidation");
            ValidationResult result = ValidateFixBeta03EnemyActivityManager(commandLineManagerObject.AddComponent<RoomEnemyActivityManager>());
            File.WriteAllText(
                resultPath,
                $"PASS eligible={result.EligibleEnemyCount} managerRoom={result.RoomCoverage.ManagerRoomEnemyCount} anyRoom={result.RoomCoverage.AnyRoomEnemyCount} noRoom={result.RoomCoverage.NoRoomEnemyCount} managerRooms={result.RoomCoverage.ManagerRoomCount} allRooms={result.RoomCoverage.AllRoomCount} initialSleeping={result.InitialSleepingCount} wakeAfterPlayerMove={result.WakeAfterPlayerMoveCount}");
            Debug.Log($"Fix_Beta03 enemy activity validation passed. Results written to {resultPath}");
            EditorApplication.Exit(0);
        }
        catch (System.Exception exception)
        {
            File.WriteAllText(resultPath, $"FAIL {exception}");
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
        finally
        {
            if (commandLineManagerObject != null)
            {
                Object.DestroyImmediate(commandLineManagerObject);
            }
        }
    }

    private static ValidationResult ValidateFixBeta03EnemyActivityManager(RoomEnemyActivityManager manager)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Assert.That(scene.IsValid(), Is.True, "Fix_Beta03 scene must load for enemy activity validation.");

        int eligibleEnemyCount = CountEligibleEnemies(scene);
        Assert.That(eligibleEnemyCount, Is.GreaterThan(0), "Fix_Beta03 should contain room-managed enemies.");
        RoomCoverage roomCoverage = CountEnemyRoomCoverage(scene);
        Debug.Log(
            $"Fix_Beta03 enemy room coverage: eligible={eligibleEnemyCount}, managerRoom={roomCoverage.ManagerRoomEnemyCount}, anyRoom={roomCoverage.AnyRoomEnemyCount}, noRoom={roomCoverage.NoRoomEnemyCount}, managerRooms={roomCoverage.ManagerRoomCount}, allRooms={roomCoverage.AllRoomCount}");

        InvokePrivate(manager, "RefreshForCurrentScene");
        SetPrivateField(manager, "gatingActive", true);
        InvokePrivate(manager, "ApplyEnemyActivity");

        IList managedEnemies = (IList)GetPrivateField(manager, "managedEnemies");
        Assert.That(managedEnemies.Count, Is.EqualTo(eligibleEnemyCount), "Every non-boss enemy should resolve into a managed room.");

        int desiredActiveCount = CountManagedEnemiesWithDesiredState(managedEnemies, desiredActive: true);
        int desiredSleepingCount = managedEnemies.Count - desiredActiveCount;

        Assert.That(desiredSleepingCount, Is.GreaterThan(managedEnemies.Count / 2), "Most enemies should be slept when the player is in only one room.");

        MovePlayerToFirstManagedEnemyRoom(scene, managedEnemies);
        SetPrivateField(manager, "playerTransform", null);
        InvokePrivate(manager, "ApplyEnemyActivity");

        desiredActiveCount = CountManagedEnemiesWithDesiredState(managedEnemies, desiredActive: true);
        Assert.That(desiredActiveCount, Is.GreaterThan(0), "An enemy room should wake when the player enters that room.");

        return new ValidationResult
        {
            EligibleEnemyCount = eligibleEnemyCount,
            RoomCoverage = roomCoverage,
            InitialSleepingCount = desiredSleepingCount,
            WakeAfterPlayerMoveCount = desiredActiveCount
        };
    }

    private RoomEnemyActivityManager CreateManager()
    {
        managerObject = new GameObject("FixBeta03EnemyActivityManagerTest");
        return managerObject.AddComponent<RoomEnemyActivityManager>();
    }

    private static int CountEligibleEnemies(Scene scene)
    {
        int count = 0;
        EnemyController[] enemies = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyController enemy = enemies[i];
            if (enemy == null ||
                enemy.gameObject.scene != scene ||
                enemy.GetComponent<StageBossAttack>() != null ||
                enemy.GetComponent<LastBossController>() != null)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static RoomCoverage CountEnemyRoomCoverage(Scene scene)
    {
        RoomCameraTrigger[] allRooms = Object.FindObjectsByType<RoomCameraTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        RoomCameraTrigger[] managerRooms = FilterManagerRooms(allRooms, scene);
        int anyRoomEnemyCount = 0;
        int managerRoomEnemyCount = 0;
        int noRoomEnemyCount = 0;

        EnemyController[] enemies = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyController enemy = enemies[i];
            if (enemy == null ||
                enemy.gameObject.scene != scene ||
                enemy.GetComponent<StageBossAttack>() != null ||
                enemy.GetComponent<LastBossController>() != null)
            {
                continue;
            }

            bool inAnyRoom = IsInAnyRoom(enemy.transform.position, allRooms, scene);
            bool inManagerRoom = IsInAnyRoom(enemy.transform.position, managerRooms, scene);
            if (inManagerRoom)
            {
                managerRoomEnemyCount++;
            }

            if (inAnyRoom)
            {
                anyRoomEnemyCount++;
            }
            else
            {
                noRoomEnemyCount++;
            }
        }

        return new RoomCoverage
        {
            AllRoomCount = CountSceneRooms(allRooms, scene),
            ManagerRoomCount = managerRooms.Length,
            AnyRoomEnemyCount = anyRoomEnemyCount,
            ManagerRoomEnemyCount = managerRoomEnemyCount,
            NoRoomEnemyCount = noRoomEnemyCount
        };
    }

    private static RoomCameraTrigger[] FilterManagerRooms(RoomCameraTrigger[] allRooms, Scene scene)
    {
        if (allRooms == null)
        {
            return new RoomCameraTrigger[0];
        }

        var managerRooms = new System.Collections.Generic.List<RoomCameraTrigger>();
        for (int i = 0; i < allRooms.Length; i++)
        {
            RoomCameraTrigger room = allRooms[i];
            if (room != null &&
                room.gameObject.scene == scene &&
                room.isActiveAndEnabled)
            {
                managerRooms.Add(room);
            }
        }

        return managerRooms.ToArray();
    }

    private static int CountSceneRooms(RoomCameraTrigger[] rooms, Scene scene)
    {
        int count = 0;
        if (rooms == null)
        {
            return count;
        }

        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] != null && rooms[i].gameObject.scene == scene)
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsInAnyRoom(Vector3 position, RoomCameraTrigger[] rooms, Scene scene)
    {
        if (rooms == null)
        {
            return false;
        }

        for (int i = 0; i < rooms.Length; i++)
        {
            RoomCameraTrigger room = rooms[i];
            if (room != null &&
                room.gameObject.scene == scene &&
                room.isActiveAndEnabled &&
                room.ContainsPoint(position))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountManagedEnemiesWithDesiredState(IList managedEnemies, bool desiredActive)
    {
        int count = 0;
        for (int i = 0; i < managedEnemies.Count; i++)
        {
            object managedEnemy = managedEnemies[i];
            if ((bool)GetPrivateField(managedEnemy, "DesiredGameplayActive") == desiredActive)
            {
                count++;
            }
        }

        return count;
    }

    private static void MovePlayerToFirstManagedEnemyRoom(Scene scene, IList managedEnemies)
    {
        Assert.That(managedEnemies.Count, Is.GreaterThan(0), "A managed enemy is required to choose a room position.");

        object managedEnemy = managedEnemies[0];
        EnemyController enemy = (EnemyController)GetPrivateField(managedEnemy, "Enemy");
        Assert.That(enemy, Is.Not.Null, "Managed enemy must keep its EnemyController reference.");

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || player.scene != scene)
        {
            player = new GameObject("FixBeta03SceneTestPlayer");
            player.tag = "Player";
        }

        player.transform.position = enemy.transform.position;
        Rigidbody2D playerRigidbody = player.GetComponent<Rigidbody2D>();
        if (playerRigidbody != null)
        {
            playerRigidbody.position = enemy.transform.position;
        }
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstancePrivate);
        Assert.That(method, Is.Not.Null, $"Private method '{methodName}' must exist.");
        method.Invoke(target, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceAny);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' must exist.");
        field.SetValue(target, value);
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstanceAny);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' must exist.");
        return field.GetValue(target);
    }

    private struct RoomCoverage
    {
        public int AllRoomCount;
        public int ManagerRoomCount;
        public int AnyRoomEnemyCount;
        public int ManagerRoomEnemyCount;
        public int NoRoomEnemyCount;
    }

    private struct ValidationResult
    {
        public int EligibleEnemyCount;
        public RoomCoverage RoomCoverage;
        public int InitialSleepingCount;
        public int WakeAfterPlayerMoveCount;
    }
}
