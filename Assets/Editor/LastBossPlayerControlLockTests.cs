using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GameName.Enemy;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class LastBossPlayerControlLockTests
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
    public void LastBoss_DoesNotLeaveInitialDelayWhilePlayerControlLocked()
    {
        ValidateLastBossPausesForPlayerControlLock(objectsToDestroy);
    }

    public static void RunCommandLineValidation()
    {
        List<Object> createdObjects = new List<Object>();
        string resultPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "Temp",
            "LastBossPlayerControlLockValidation.txt");

        try
        {
            ValidateLastBossPausesForPlayerControlLock(createdObjects);
            File.WriteAllText(resultPath, "PASS LastBoss stayed paused while player control was locked and resumed after unlock.");
            Debug.Log($"LastBoss player control lock validation passed. Results written to {resultPath}");
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
            DestroyCreatedObjects(createdObjects);
        }
    }

    private static void ValidateLastBossPausesForPlayerControlLock(List<Object> createdObjects)
    {
        PlayerController player = CreatePlayer(new Vector2(1f, 0f), createdObjects);
        LastBossController boss = CreateLastBoss(Vector2.zero, createdObjects);
        Rigidbody2D bossRigidbody = boss.GetComponent<Rigidbody2D>();
        SetPrivateField(boss, "initialActionDelay", 0f);

        player.SetExternalControlLocked(true);
        boss.ActivateEncounter();
        bossRigidbody.linearVelocity = Vector2.right * 5f;

        InvokePrivate(boss, "Update");
        InvokePrivate(boss, "FixedUpdate");

        Assert.That(bossRigidbody.linearVelocity.x, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(GetPrivateField(boss, "state").ToString(), Is.EqualTo("InitialDelay"));

        player.SetExternalControlLocked(false);

        InvokePrivate(boss, "Update");

        Assert.That(GetPrivateField(boss, "state").ToString(), Is.Not.EqualTo("InitialDelay"));
    }

    private static PlayerController CreatePlayer(Vector2 position, List<Object> createdObjects)
    {
        GameObject playerObject = new GameObject("Player");
        createdObjects.Add(playerObject);
        playerObject.tag = "Player";
        playerObject.transform.position = position;
        playerObject.AddComponent<Rigidbody2D>();
        playerObject.AddComponent<BoxCollider2D>();
        return playerObject.AddComponent<PlayerController>();
    }

    private static LastBossController CreateLastBoss(Vector2 position, List<Object> createdObjects)
    {
        GameObject bossObject = new GameObject("LastBoss");
        createdObjects.Add(bossObject);
        bossObject.transform.position = position;
        Rigidbody2D rigidbody2D = bossObject.AddComponent<Rigidbody2D>();
        rigidbody2D.gravityScale = 0f;
        bossObject.AddComponent<BoxCollider2D>();
        return bossObject.AddComponent<LastBossController>();
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, fieldName);
        return field.GetValue(target);
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

    private static void DestroyCreatedObjects(List<Object> createdObjects)
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }
}
