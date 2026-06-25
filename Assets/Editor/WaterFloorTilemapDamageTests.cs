using System.Collections.Generic;
using System.Reflection;
using EditorTools;
using NUnit.Framework;
using Player;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public sealed class WaterFloorTilemapDamageTests
{
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
    public void FixedUpdate_WhenPlayerStandsOnWaterTile_DamagesPlayer()
    {
        Tile waterTile = CreateTile("Water");
        WaterFloorTilemapDamage waterDamage = CreateWaterTilemapDamage(waterTile, waterTile);
        PlayerHealth playerHealth = CreatePlayer(new Vector2(0.5f, 1.5f));
        Physics2D.SyncTransforms();

        InvokeLifecycleMethod(waterDamage, "FixedUpdate");

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(0));
    }

    [Test]
    public void FixedUpdate_WhenPlayerStandsOnNonWaterTile_DoesNotDamagePlayer()
    {
        Tile waterTile = CreateTile("Water");
        Tile nonWaterTile = CreateTile("Stone");
        WaterFloorTilemapDamage waterDamage = CreateWaterTilemapDamage(waterTile, nonWaterTile);
        PlayerHealth playerHealth = CreatePlayer(new Vector2(0.5f, 1.5f));
        Physics2D.SyncTransforms();

        InvokeLifecycleMethod(waterDamage, "FixedUpdate");

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(1));
    }

    [Test]
    public void FixedUpdate_WhenPlayerIsBesideWaterTile_DoesNotDamagePlayer()
    {
        Tile waterTile = CreateTile("Water");
        WaterFloorTilemapDamage waterDamage = CreateWaterTilemapDamage(waterTile, waterTile);
        PlayerHealth playerHealth = CreatePlayer(new Vector2(1.6f, 1.5f));
        Physics2D.SyncTransforms();

        InvokeLifecycleMethod(waterDamage, "FixedUpdate");

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(1));
    }

    [Test]
    public void FixedUpdate_WhenPlayerIsBelowWaterTile_DoesNotDamagePlayer()
    {
        Tile waterTile = CreateTile("Water");
        WaterFloorTilemapDamage waterDamage = CreateWaterTilemapDamage(waterTile, waterTile);
        PlayerHealth playerHealth = CreatePlayer(new Vector2(0.5f, 0.4f));
        Physics2D.SyncTransforms();

        InvokeLifecycleMethod(waterDamage, "FixedUpdate");

        Assert.That(playerHealth.CurrentHealth, Is.EqualTo(1));
    }

    private WaterFloorTilemapDamage CreateWaterTilemapDamage(Tile waterTile, Tile placedTile)
    {
        GameObject gridObject = CreateObject("Grid", Vector2.zero);
        gridObject.AddComponent<Grid>();

        GameObject tilemapObject = CreateObject("WaterTilemap", Vector2.zero);
        tilemapObject.transform.SetParent(gridObject.transform);
        Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
        tilemapObject.AddComponent<TilemapRenderer>();
        tilemap.SetTile(Vector3Int.zero, placedTile);

        WaterFloorTilemapDamage waterDamage = tilemapObject.AddComponent<WaterFloorTilemapDamage>();
        WaterFloorTileSet waterTiles = new WaterFloorTileSet();
        waterTiles.SetTiles(waterTile, waterTile, waterTile, waterTile, waterTile);
        waterDamage.SetWaterTiles(waterTiles);
        InvokeLifecycleMethod(waterDamage, "Awake");
        return waterDamage;
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

    private Tile CreateTile(string name)
    {
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.name = name;
        tile.colliderType = Tile.ColliderType.Sprite;
        objectsToDestroy.Add(tile);
        return tile;
    }

    private GameObject CreateObject(string name, Vector2 position)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.position = position;
        objectsToDestroy.Add(gameObject);
        return gameObject;
    }

    private static void InvokeLifecycleMethod(WaterFloorTilemapDamage waterDamage, string methodName)
    {
        MethodInfo method = typeof(WaterFloorTilemapDamage).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(waterDamage, null);
    }

    private static int PlayerLayer()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        Assert.GreaterOrEqual(playerLayer, 0, "Player layer must exist for WaterFloorTilemapDamage tests.");
        return playerLayer;
    }
}
