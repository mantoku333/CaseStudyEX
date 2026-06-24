using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Player;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class RainDamageAreaTests
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

    [Test]
    public void FixedUpdate_WhenPlayerIsExposed_DamagesPlayer()
    {
        RainDamageArea rainArea = CreateRainDamageArea(true);
        PlayerHealth playerHealth = CreatePlayer(Vector2.zero);
        Physics2D.SyncTransforms();

        InvokeFixedUpdate(rainArea);

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0));
    }

    [Test]
    public void FixedUpdate_WhenSolidCoverBlocksAllRainPaths_DoesNotDamagePlayer()
    {
        RainDamageArea rainArea = CreateRainDamageArea(true);
        PlayerHealth playerHealth = CreatePlayer(Vector2.zero);
        CreateGroundBox("FullRoof", new Vector2(0f, 2.5f), new Vector2(2f, 0.4f));
        Physics2D.SyncTransforms();

        InvokeFixedUpdate(rainArea);

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(1));
    }

    [Test]
    public void FixedUpdate_WhenSolidCoverBlocksOnlyOneRainPath_DamagesPlayer()
    {
        RainDamageArea rainArea = CreateRainDamageArea(true);
        PlayerHealth playerHealth = CreatePlayer(Vector2.zero);
        CreateGroundBox("PartialRoof", new Vector2(-0.45f, 2.5f), new Vector2(0.25f, 0.4f));
        Physics2D.SyncTransforms();

        InvokeFixedUpdate(rainArea);

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0));
    }

    [Test]
    public void FixedUpdate_WhenRainIsInactive_DoesNotDamagePlayer()
    {
        RainDamageArea rainArea = CreateRainDamageArea(false);
        PlayerHealth playerHealth = CreatePlayer(Vector2.zero);
        Physics2D.SyncTransforms();

        InvokeFixedUpdate(rainArea);

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(1));
    }

    [Test]
    public void FixedUpdate_WhenUmbrellaIsOpen_DoesNotDamagePlayer()
    {
        RainDamageArea rainArea = CreateRainDamageArea(true);
        PlayerHealth playerHealth = CreatePlayer(Vector2.zero);
        UmbrellaController umbrellaController = playerHealth.gameObject.AddComponent<UmbrellaController>();
        umbrellaController.SetUmbrellaState(UmbrellaController.UmbrellaState.Open, false);
        Physics2D.SyncTransforms();

        InvokeFixedUpdate(rainArea);

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(1));
    }

    private RainDamageArea CreateRainDamageArea(bool active)
    {
        // 実シーンを開かず、雨エリア・プレイヤー・屋根だけを作って到達判定を検証する。
        GameObject areaObject = CreateObject("RainDamageArea", new Vector2(0f, 2f));
        BoxCollider2D areaCollider = areaObject.AddComponent<BoxCollider2D>();
        areaCollider.isTrigger = true;
        areaCollider.size = new Vector2(6f, 8f);

        RainDamageArea rainArea = areaObject.AddComponent<RainDamageArea>();
        InvokeLifecycleMethod(rainArea, "Awake");
        rainArea.SetRainActive(active);
        return rainArea;
    }

    private PlayerHealth CreatePlayer(Vector2 position)
    {
        GameObject playerObject = CreateObject("Player", position);
        playerObject.tag = "Player";
        playerObject.layer = PlayerLayer();

        Rigidbody2D rigidbody2D = playerObject.AddComponent<Rigidbody2D>();
        rigidbody2D.gravityScale = 0f;

        CapsuleCollider2D bodyCollider = playerObject.AddComponent<CapsuleCollider2D>();
        bodyCollider.size = Vector2.one;
        bodyCollider.direction = CapsuleDirection2D.Vertical;

        PlayerHealth playerHealth = playerObject.AddComponent<PlayerHealth>();
        playerHealth.RestoreFullHealth();
        return playerHealth;
    }

    private GameObject CreateGroundBox(string name, Vector2 position, Vector2 size)
    {
        GameObject box = CreateObject(name, position);
        box.layer = GroundLayer();

        BoxCollider2D collider = box.AddComponent<BoxCollider2D>();
        collider.size = size;
        return box;
    }

    private GameObject CreateObject(string name, Vector2 position)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.position = position;
        objectsToDestroy.Add(gameObject);
        return gameObject;
    }

    private static void InvokeFixedUpdate(RainDamageArea rainArea)
    {
        InvokeLifecycleMethod(rainArea, "FixedUpdate");
    }

    private static void InvokeLifecycleMethod(RainDamageArea rainArea, string methodName)
    {
        MethodInfo method = typeof(RainDamageArea).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(rainArea, null);
    }

    private static int PlayerLayer()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        Assert.GreaterOrEqual(playerLayer, 0, "Player layer must exist for RainDamageArea tests.");
        return playerLayer;
    }

    private static int GroundLayer()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        Assert.GreaterOrEqual(groundLayer, 0, "Ground layer must exist for RainDamageArea tests.");
        return groundLayer;
    }
}
