using System.Collections.Generic;
using System.Reflection;
using EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public sealed class StageEditorWaterFloorRecalculationTests
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
    public void RecalculateWaterFloorTilesAroundBounds_UpdatesTouchedRunOnly()
    {
        StageEditorPalette palette = AssetDatabase.LoadAssetAtPath<StageEditorPalette>("Assets/Editor/StageEditorPalette.asset");
        Assert.That(palette, Is.Not.Null);
        Assert.That(palette.WaterFloorTiles, Is.Not.Null);
        Assert.That(palette.WaterFloorTiles.HasAllTiles(), Is.True);

        TileBase solidBlock = palette.Stage2Blocks.GetTile(StageBlockFace.B);
        Assert.That(solidBlock, Is.Not.Null);

        Tilemap tilemap = CreateTilemap();
        WaterFloorTileSet waterTiles = palette.WaterFloorTiles;
        tilemap.SetTile(new Vector3Int(-1, 0, 0), solidBlock);
        tilemap.SetTile(new Vector3Int(0, 0, 0), waterTiles.GetTile(WaterFloorFace.B));
        tilemap.SetTile(new Vector3Int(1, 0, 0), waterTiles.GetTile(WaterFloorFace.B));
        tilemap.SetTile(new Vector3Int(2, 0, 0), waterTiles.GetTile(WaterFloorFace.B));

        tilemap.SetTile(new Vector3Int(10, 2, 0), waterTiles.GetTile(WaterFloorFace.D));
        tilemap.SetTile(new Vector3Int(11, 2, 0), waterTiles.GetTile(WaterFloorFace.E));

        StageEditorWindow window = ScriptableObject.CreateInstance<StageEditorWindow>();
        objectsToDestroy.Add(window);
        SetPrivateField(window, "palette", palette);
        SetPrivateField(window, "targetStageTilemap", tilemap);

        InvokeRecalculateWater(window, new BoundsInt(0, 0, 0, 3, 1, 1));

        Assert.That(tilemap.GetTile(new Vector3Int(0, 0, 0)), Is.SameAs(waterTiles.GetTile(WaterFloorFace.D)));
        Assert.That(tilemap.GetTile(new Vector3Int(1, 0, 0)), Is.SameAs(waterTiles.GetTile(WaterFloorFace.B)));
        Assert.That(tilemap.GetTile(new Vector3Int(2, 0, 0)), Is.SameAs(waterTiles.GetTile(WaterFloorFace.C)));
        Assert.That(tilemap.GetTile(new Vector3Int(10, 2, 0)), Is.SameAs(waterTiles.GetTile(WaterFloorFace.D)));
        Assert.That(tilemap.GetTile(new Vector3Int(11, 2, 0)), Is.SameAs(waterTiles.GetTile(WaterFloorFace.E)));
    }

    [Test]
    public void PaintWaterFloor_DoesNotRetileAdjacentStageBlock()
    {
        StageEditorPalette palette = AssetDatabase.LoadAssetAtPath<StageEditorPalette>("Assets/Editor/StageEditorPalette.asset");
        Assert.That(palette, Is.Not.Null);
        Assert.That(palette.WaterFloorTiles, Is.Not.Null);
        Assert.That(palette.WaterFloorTiles.HasAllTiles(), Is.True);

        TileBase stageB = palette.Stage2Blocks.GetTile(StageBlockFace.B);
        Assert.That(stageB, Is.Not.Null);

        Tilemap tilemap = CreateTilemap();
        Vector3Int stageCell = new Vector3Int(0, 0, 0);
        Vector3Int waterCell1 = new Vector3Int(1, 0, 0);
        Vector3Int waterCell2 = new Vector3Int(2, 0, 0);
        tilemap.SetTile(stageCell, stageB);

        StageEditorWindow window = ScriptableObject.CreateInstance<StageEditorWindow>();
        objectsToDestroy.Add(window);
        SetPrivateField(window, "palette", palette);
        SetPrivateField(window, "targetStageTilemap", tilemap);
        SetPrivateEnumField(window, "stageTilePaintKind", "WaterFloor");

        InvokePaintTile(window, waterCell1);
        InvokePaintTile(window, waterCell2);

        WaterFloorTileSet waterTiles = palette.WaterFloorTiles;
        Assert.That(tilemap.GetTile(stageCell), Is.SameAs(stageB));
        Assert.That(tilemap.GetTile(waterCell1), Is.SameAs(waterTiles.GetTile(WaterFloorFace.D)));
        Assert.That(tilemap.GetTile(waterCell2), Is.SameAs(waterTiles.GetTile(WaterFloorFace.C)));
    }

    [Test]
    public void RecalculateAutoBlockTiles_CountsWaterFloorAsSolidTopNeighbor()
    {
        StageEditorPalette palette = AssetDatabase.LoadAssetAtPath<StageEditorPalette>("Assets/Editor/StageEditorPalette.asset");
        Assert.That(palette, Is.Not.Null);
        Assert.That(palette.WaterFloorTiles, Is.Not.Null);
        Assert.That(palette.WaterFloorTiles.HasAllTiles(), Is.True);

        TileBase stageB = palette.Stage2Blocks.GetTile(StageBlockFace.B);
        TileBase stageE = palette.Stage2Blocks.GetTile(StageBlockFace.E);
        Assert.That(stageB, Is.Not.Null);
        Assert.That(stageE, Is.Not.Null);

        Tilemap tilemap = CreateTilemap();
        Vector3Int centerCell = new Vector3Int(0, 0, 0);
        tilemap.SetTile(new Vector3Int(0, 1, 0), palette.WaterFloorTiles.GetTile(WaterFloorFace.B));
        tilemap.SetTile(new Vector3Int(-1, 0, 0), stageE);
        tilemap.SetTile(centerCell, stageB);
        tilemap.SetTile(new Vector3Int(1, 0, 0), stageE);
        tilemap.SetTile(new Vector3Int(0, -1, 0), stageE);

        StageEditorWindow window = ScriptableObject.CreateInstance<StageEditorWindow>();
        objectsToDestroy.Add(window);
        SetPrivateField(window, "palette", palette);
        SetPrivateField(window, "targetStageTilemap", tilemap);

        InvokeRecalculateAutoBlocks(window, new BoundsInt(0, 0, 0, 1, 1, 1));

        Assert.That(tilemap.GetTile(centerCell), Is.SameAs(stageE));
    }

    private Tilemap CreateTilemap()
    {
        GameObject gridObject = CreateObject("Grid");
        gridObject.AddComponent<Grid>();

        GameObject tilemapObject = CreateObject("Tilemap");
        tilemapObject.transform.SetParent(gridObject.transform);
        return tilemapObject.AddComponent<Tilemap>();
    }

    private GameObject CreateObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        objectsToDestroy.Add(gameObject);
        return gameObject;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = typeof(StageEditorWindow).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static void SetPrivateEnumField(object target, string fieldName, string enumValue)
    {
        FieldInfo field = typeof(StageEditorWindow).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);

        object parsedValue = System.Enum.Parse(field.FieldType, enumValue);
        field.SetValue(target, parsedValue);
    }

    private static void InvokeRecalculateWater(StageEditorWindow window, BoundsInt bounds)
    {
        MethodInfo method = typeof(StageEditorWindow).GetMethod(
            "RecalculateWaterFloorTilesAroundBounds",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(window, new object[] { bounds });
    }

    private static void InvokeRecalculateAutoBlocks(StageEditorWindow window, BoundsInt bounds)
    {
        MethodInfo method = typeof(StageEditorWindow).GetMethod(
            "RecalculateAutoBlockTiles",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(window, new object[] { bounds });
    }

    private static void InvokePaintTile(StageEditorWindow window, Vector3Int cell)
    {
        MethodInfo method = typeof(StageEditorWindow).GetMethod(
            "PaintTile",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(window, new object[] { cell });
    }
}
