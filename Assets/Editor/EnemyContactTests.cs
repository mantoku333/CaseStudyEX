using System.Collections.Generic;
using System.Reflection;
using GameName.Enemy;
using Metroidvania.Enemy;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class EnemyContactTests
{
    private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly List<Object> objectsToDestroy = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;

        HitStopController[] hitStopControllers =
            Object.FindObjectsByType<HitStopController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < hitStopControllers.Length; i++)
        {
            if (hitStopControllers[i] != null)
            {
                Object.DestroyImmediate(hitStopControllers[i].gameObject);
            }
        }

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
    public void ResolveContactDamage_UsesEnemyControllerDamageWhenAvailable()
    {
        EnemyContact enemyContact = CreateEnemyContact(15, 1);

        int damage = InvokePrivate<int>(enemyContact, "ResolveContactDamage");

        Assert.That(damage, Is.EqualTo(15));
    }

    [Test]
    public void IsOverlappingPlayer_UsesContactDamageOnlyTriggerColliders()
    {
        GameObject enemyObject = CreateObject("Enemy", Vector2.zero);
        EnemyContact enemyContact = enemyObject.AddComponent<EnemyContact>();
        GameObject damageOnlyObject = CreateObject("ContactDamageOnly", Vector2.zero);
        damageOnlyObject.transform.SetParent(enemyObject.transform, false);
        BoxCollider2D damageOnlyCollider = damageOnlyObject.AddComponent<BoxCollider2D>();
        damageOnlyCollider.isTrigger = true;
        damageOnlyCollider.size = Vector2.one;
        damageOnlyObject.AddComponent<ContactDamageOnlyCollider>();

        GameObject playerObject = CreateObject("Player", Vector2.zero);
        BoxCollider2D playerCollider = playerObject.AddComponent<BoxCollider2D>();
        playerCollider.size = Vector2.one;

        SetPrivateField(enemyContact, "enemyColliders", new Collider2D[0]);
        SetPrivateField(enemyContact, "damageOnlyColliders", new Collider2D[] { damageOnlyCollider });
        SetPrivateField(enemyContact, "playerBodyCollider", playerCollider);
        Physics2D.SyncTransforms();

        bool overlaps = InvokePrivate<bool>(enemyContact, "IsOverlappingPlayer");

        Assert.That(overlaps, Is.True);
    }

    private EnemyContact CreateEnemyContact(int controllerDamage, int fallbackContactDamage)
    {
        GameObject enemyObject = CreateObject("Enemy", Vector2.zero);
        enemyObject.AddComponent<Rigidbody2D>().gravityScale = 0f;
        enemyObject.AddComponent<BoxCollider2D>().size = Vector2.one;

        EnemyController enemyController = enemyObject.AddComponent<EnemyController>();
        SetPrivateField(enemyController, "damageToPlayer", controllerDamage);
        InvokePrivate(enemyController, "Awake");

        EnemyContact enemyContact = enemyObject.AddComponent<EnemyContact>();
        SetPrivateField(enemyContact, "contactDamage", fallbackContactDamage);
        InvokePrivate(enemyContact, "Awake");
        return enemyContact;
    }

    private GameObject CreateObject(string name, Vector2 position)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.position = position;
        objectsToDestroy.Add(gameObject);
        return gameObject;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, InstancePrivate);
        Assert.That(field, Is.Not.Null, $"Private field '{fieldName}' must exist.");
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstancePrivate);
        Assert.That(method, Is.Not.Null, $"Private method '{methodName}' must exist.");
        method.Invoke(target, null);
    }

    private static T InvokePrivate<T>(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, InstancePrivate);
        Assert.That(method, Is.Not.Null, $"Private method '{methodName}' must exist.");
        return (T)method.Invoke(target, null);
    }
}
