using System;
using System.Collections.Generic;
using System.Reflection;
using EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public sealed class StageEditorGrassColumnTests
{
    private readonly List<Object> objectsToDestroy = new List<Object>();
    private StageEditorPalette palette;
    private Tilemap tilemap;
    private StageEditorWindow window;

    [SetUp]
    public void SetUp()
    {
        palette = AssetDatabase.LoadAssetAtPath<StageEditorPalette>("Assets/Editor/StageEditorPalette.asset");
        Assert.That(palette, Is.Not.Null);
        Assert.That(palette.Stage2Blocks.HasColumnTiles(), Is.True);

        GameObject grid = CreateObject("Grass Column Test Grid");
        grid.AddComponent<Grid>();
        tilemap = CreateTilemap(grid.transform);
        window = ScriptableObject.CreateInstance<StageEditorWindow>();
        objectsToDestroy.Add(window);
        SetField("palette", palette);
        SetField("targetStageTilemap", tilemap);
        SetField("selectedStageBlockSetIndex", 1);
        SetEnum("currentPlacementType", "Stage");
        SetEnum("stageBlockPaintMode", "AutoBlock");
        SetEnum("stageTilePaintKind", "StageBlock");
        SetEnum("stagePaintScope", "PlaceAndReplace");
    }

    [TearDown]
    public void TearDown()
    {
        for (int index = objectsToDestroy.Count - 1; index >= 0; index--)
        {
            Object target = objectsToDestroy[index];
            if (target != null)
            {
                Undo.ClearUndo(target);
                Object.DestroyImmediate(target);
            }
        }

        objectsToDestroy.Clear();
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(100)]
    public void AutoBrush_BuildsExpectedColumn(int height)
    {
        for (int y = 0; y < height; y++)
        {
            Invoke("PaintTile", new Vector3Int(0, y, 0));
        }

        AssertColumn(0, 0, height);
    }

    [Test]
    public void BentStroke_MatchesExamplesInEitherOrder(
        [Values] bool growsUp, [Values] bool mirror, [Values] bool reverseOrder)
    {
        List<Vector3Int> stroke = CreateBentStroke(growsUp);
        if (reverseOrder) stroke.Reverse();
        foreach (Vector3Int cell in stroke)
        {
            Invoke("PaintTile", mirror ? new Vector3Int(4 - cell.x, cell.y, 0) : cell);
        }

        AssertBentStroke(growsUp, mirror);
        Recalculate(new Vector3Int(2, 2, 0));
        AssertBentStroke(growsUp, mirror);
    }

    [TestCase(2)]
    [TestCase(100)]
    public void LongHorizontalArm_KeepsExposedTopFacesWhenVerticalConnectionChanges(int height)
    {
        Invoke("ApplyTileEditRectangle", Vector3Int.zero, new Vector3Int(12, 0, 0));
        Invoke("ApplyTileEditRectangle", Vector3Int.zero, new Vector3Int(0, height - 1, 0));
        AssertLayout(Vector3Int.zero, "GBBBBBBBBBBBC");
        AssertFace(0, height - 1, StageBlockFace.J);
        for (int y = 1; y < height - 1; y++) AssertFace(0, y, StageBlockFace.K);

        Invoke("EraseTile", new Vector3Int(0, 1, 0));
        AssertLayout(Vector3Int.zero, "ABBBBBBBBBBBC");
        Invoke("PaintTile", new Vector3Int(0, 1, 0));
        AssertLayout(Vector3Int.zero, "GBBBBBBBBBBBC");
    }

    [Test]
    public void HorizontalArm_UsesExposedTopFaces(
        [Values(2, 3, 4)] int width, [Values] bool mirror, [Values] bool reverseOrder)
    {
        List<Vector3Int> stroke = new List<Vector3Int> { new Vector3Int(1, 2, 0) };
        for (int x = 1; x <= width; x++) stroke.Add(new Vector3Int(x, 1, 0));
        if (reverseOrder) stroke.Reverse();
        foreach (Vector3Int cell in stroke)
        {
            Invoke("PaintTile", mirror ? new Vector3Int(width + 1 - cell.x, cell.y, 0) : cell);
        }

        string emptyRow = new string('O', width + 2);
        AssertMirroredLayout(mirror, emptyRow, "OJ" + new string('O', width),
            "OG" + new string('B', width - 2) + "CO", emptyRow);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void EditingColumn_RepairsLegacyArmBeyondEditedBounds(bool erase)
    {
        SeedLegacyHorizontalArm();
        Invoke(erase ? "EraseTile" : "PaintTile", new Vector3Int(0, erase ? 1 : 2, 0));
        AssertLayout(Vector3Int.zero, erase ? "ABBBBBBBBBBBC" : "GBBBBBBBBBBBC");
        AssertFace(12, 0, StageBlockFace.C);
    }

    [Test]
    public void ThinConnectorBetweenWiderRows_UsesK()
    {
        Invoke("ApplyTileEditRectangle", Vector3Int.zero, new Vector3Int(2, 0, 0));
        Invoke("ApplyTileEditRectangle", new Vector3Int(0, 2, 0), new Vector3Int(2, 2, 0));
        Invoke("PaintTile", new Vector3Int(1, 1, 0));
        AssertLayout(Vector3Int.zero, "ABC", "OKO", "AHC");
    }

    [Test]
    public void GrassRectangle_PreservesStandardFaces()
    {
        Invoke("ApplyTileEditRectangle", Vector3Int.zero, new Vector3Int(2, 2, 0));
        AssertLayout(Vector3Int.zero, "ABC", "DEF", "GHI");
    }

    [Test]
    public void HorizontalRowWithConnectionsAboveAndBelow_KeepsLegacyFaces()
    {
        Invoke("ApplyTileEditRectangle", Vector3Int.zero, new Vector3Int(4, 0, 0));
        Invoke("PaintTile", new Vector3Int(0, 1, 0));
        Invoke("PaintTile", new Vector3Int(4, -1, 0));
        AssertLayout(new Vector3Int(0, -1, 0), "JOOOO", "GBBBC", "OOOOM");
    }

    [TestCase(0)]
    [TestCase(6)]
    [TestCase(12)]
    public void SideConnection_RefreshesEntireColumnAndRestoresAfterErase(int connectionY)
    {
        SeedColumn(0, 0, 13);
        StageBlockColumnConversion.ConvertTilemap(tilemap, palette.Stage2Blocks, false);
        Vector3Int connection = new Vector3Int(1, connectionY, 0);

        Invoke("PaintTile", connection);
        AssertColumnWithSideConnection(connectionY);
        Invoke("EraseTile", connection);
        AssertColumn(0, 0, 13);
    }

    [TestCase("Stage1")]
    [TestCase("Water")]
    [TestCase("Unknown")]
    public void Conversion_UsesLocalOccupancyAndPreservesForeignNeighbors(string blockerKind)
    {
        TileBase blocker = CreateBlocker(blockerKind);
        Vector3Int[] positions =
        {
            new Vector3Int(-1, 0, 0), new Vector3Int(1, 6, 0), new Vector3Int(-1, 12, 0),
            new Vector3Int(0, -1, 0), new Vector3Int(0, 13, 0)
        };

        foreach (Vector3Int position in positions)
        {
            SeedColumn(0, 0, 13);
            tilemap.SetTile(position, blocker);
            StageBlockColumnConversion.Result result = StageBlockColumnConversion.ConvertTilemap(
                tilemap, palette.Stage2Blocks, false);
            Assert.That(result.ConvertedColumns, Is.EqualTo(1));
            Assert.That(result.ConvertedTiles, Is.EqualTo(position.x == 0 ? 13 : 12), $"Blocker at {position}");
            for (int y = 0; y < 13; y++)
            {
                StageBlockFace face = position.x != 0 && position.y == y ? StageBlockFace.E :
                    y == 0 && position.y != -1 ? StageBlockFace.M :
                    y == 12 && position.y != 13 ? StageBlockFace.J : StageBlockFace.K;
                AssertFace(0, y, face);
            }

            Assert.That(tilemap.GetTile(position), Is.SameAs(blocker));
            tilemap.SetTile(position, null);
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Conversion_BentColumn_ChangesOnlyJKMCells(bool growsUp)
    {
        foreach (Vector3Int cell in CreateBentStroke(growsUp))
        {
            tilemap.SetTile(cell, palette.Stage2Blocks.GetTile(StageBlockFace.E));
        }

        StageBlockColumnConversion.Result result = StageBlockColumnConversion.ConvertTilemap(
            tilemap, palette.Stage2Blocks, false);
        Assert.That(result.ConvertedColumns, Is.EqualTo(1));
        Assert.That(result.ConvertedTiles, Is.EqualTo(3));
        AssertLayout(Vector3Int.zero, growsUp
            ? new[] { "OOOOO", "OOJOO", "OOKOO", "OOKOO", "OOEEO", "OOOOO" }
            : new[] { "OOOOO", "OEEOO", "OOKOO", "OOKOO", "OOMOO", "OOOOO" });
        Assert.That(StageBlockColumnConversion.ConvertTilemap(tilemap, palette.Stage2Blocks, false).ConvertedTiles,
            Is.Zero);
    }

    [Test]
    public void Conversion_ConnectorBetweenWiderRows_ChangesOnlyMiddleTile()
    {
        for (int x = 0; x < 3; x++)
        {
            tilemap.SetTile(new Vector3Int(x, 0, 0), palette.Stage2Blocks.GetTile(StageBlockFace.E));
            tilemap.SetTile(new Vector3Int(x, 2, 0), palette.Stage2Blocks.GetTile(StageBlockFace.E));
        }

        tilemap.SetTile(new Vector3Int(1, 1, 0), palette.Stage2Blocks.GetTile(StageBlockFace.D));
        StageBlockColumnConversion.Result result = StageBlockColumnConversion.ConvertTilemap(
            tilemap, palette.Stage2Blocks, false);
        Assert.That(result.ConvertedTiles, Is.EqualTo(1));
        AssertLayout(Vector3Int.zero, "EEE", "OKO", "EEE");
    }

    [Test]
    public void DiagonalsAndOtherTilemaps_DoNotBlockColumns()
    {
        SeedColumn(0, 0, 4);
        TileBase blocker = CreateBlocker("Unknown");
        tilemap.SetTile(new Vector3Int(-1, -1, 0), blocker);
        tilemap.SetTile(new Vector3Int(1, 4, 0), blocker);
        Tilemap otherTilemap = CreateTilemap(tilemap.transform.parent);
        otherTilemap.SetTile(new Vector3Int(1, 2, 0), blocker);

        Recalculate(new Vector3Int(0, 1, 0));
        AssertColumn(0, 0, 4);
    }

    [Test]
    public void EraseAndJoin_UpdatesBothSegmentsBeyondLocalBounds()
    {
        SeedColumn(0, 0, 13);
        Recalculate(new Vector3Int(0, 6, 0));
        Invoke("EraseTile", new Vector3Int(0, 6, 0));
        AssertColumn(0, 0, 6);
        AssertColumn(0, 7, 6);

        Invoke("PaintTile", new Vector3Int(0, 6, 0));
        AssertColumn(0, 0, 13);
    }

    [Test]
    public void Erase_WithIncompleteOtherSetSelected_StillRepairsStage2()
    {
        SeedColumn(0, 0, 13);
        Vector3Int blocker = new Vector3Int(1, 6, 0);
        tilemap.SetTile(blocker, CreateBlocker("Unknown"));
        SetField("selectedStageBlockSetIndex", 0);

        Invoke("EraseTile", blocker);
        AssertColumn(0, 0, 13);
    }

    [TestCase("PlaceAndReplace", 4)]
    [TestCase("ReplaceExistingOnly", 2)]
    public void Rectangle_HonorsPaintScope(string scope, int expectedHeight)
    {
        SeedColumn(0, 0, 2);
        SetEnum("stagePaintScope", scope);
        Invoke("ApplyTileEditRectangle", Vector3Int.zero, new Vector3Int(0, 3, 0));
        AssertColumn(0, 0, expectedHeight);
        Assert.That(tilemap.HasTile(new Vector3Int(0, expectedHeight, 0)), Is.False);
    }

    [Test]
    public void RectangleErase_SplitsAColumnIntoTwoPairs()
    {
        SeedColumn(0, 0, 7);
        SetEnum("currentPlacementType", "Erase");
        Invoke("ApplyTileEditRectangle", new Vector3Int(0, 2, 0), new Vector3Int(0, 4, 0));
        AssertColumn(0, 0, 2);
        AssertColumn(0, 5, 2);
        Assert.That(tilemap.HasTile(new Vector3Int(0, 3, 0)), Is.False);
    }

    [Test]
    public void BrushReplaceOnly_DoesNotFillAGap()
    {
        SeedColumn(0, 0, 2);
        SetEnum("stagePaintScope", "ReplaceExistingOnly");
        Invoke("PaintTile", new Vector3Int(0, 2, 0));
        Assert.That(tilemap.HasTile(new Vector3Int(0, 2, 0)), Is.False);
        Invoke("PaintTile", Vector3Int.zero);
        AssertColumn(0, 0, 2);
    }

    [Test]
    public void UndoRedo_PreservesColumnFacesAwayFromSideConnection()
    {
        SeedColumn(0, 0, 13);
        StageBlockColumnConversion.ConvertTilemap(tilemap, palette.Stage2Blocks, false);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Invoke("RegisterTileDragUndoIfNeeded");
        Vector3Int connection = new Vector3Int(1, 6, 0);
        Invoke("PaintTile", connection);
        Undo.FlushUndoRecordObjects();
        Undo.CollapseUndoOperations(group);
        AssertColumnWithSideConnection(6);

        Undo.PerformUndo();
        AssertColumn(0, 0, 13);
        Assert.That(tilemap.HasTile(connection), Is.False);
        Undo.PerformRedo();
        AssertColumnWithSideConnection(6);
        Assert.That(tilemap.HasTile(connection), Is.True);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void BrushRelease_PaintsFinalCellAndUndoRedoRestoresEntireStroke(bool growsUp)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        SetEnum("tileEditMode", "Brush");
        SetField("isTileDragging", true);
        List<Vector3Int> stroke = CreateBentStroke(growsUp);
        for (int index = 0; index < stroke.Count - 1; index++) Invoke("ApplyBrushCell", stroke[index]);
        Vector3Int release = stroke[stroke.Count - 1];
        Assert.That(tilemap.HasTile(release), Is.False);
        SetField("dragCurrentCell", release);
        Invoke("CompleteTileDrag");
        Assert.That(GetField("isTileDragging"), Is.False);
        Assert.That(GetField("hasRegisteredTileDragUndo"), Is.False);
        AssertBentStroke(growsUp, false);

        Undo.FlushUndoRecordObjects();
        Undo.CollapseUndoOperations(group);
        Undo.PerformUndo();
        foreach (Vector3Int cell in stroke) Assert.That(tilemap.HasTile(cell), Is.False, cell.ToString());
        Undo.PerformRedo();
        AssertBentStroke(growsUp, false);
    }

    [TestCase("PlaceAndReplace", true)]
    [TestCase("ReplaceExistingOnly", false)]
    public void BrushRelease_HonorsPaintScopeAndDoesNotInterpolate(string scope, bool paintsRelease)
    {
        SetEnum("tileEditMode", "Brush");
        SetEnum("stagePaintScope", scope);
        SetField("isTileDragging", true);
        Invoke("ApplyBrushCell", Vector3Int.zero);
        SetField("dragCurrentCell", new Vector3Int(0, 5, 0));
        Invoke("CompleteTileDrag");
        Assert.That(tilemap.HasTile(new Vector3Int(0, 5, 0)), Is.EqualTo(paintsRelease));
        for (int y = 1; y < 5; y++) Assert.That(tilemap.HasTile(new Vector3Int(0, y, 0)), Is.False);
    }

    [Test]
    public void UndoRedo_IncludesFarEndOfHorizontalArm()
    {
        SeedLegacyHorizontalArm();
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Invoke("ApplyBrushCell", new Vector3Int(0, 2, 0));
        AssertLayout(Vector3Int.zero, "GBBBBBBBBBBBC");
        AssertFace(0, 1, StageBlockFace.K);
        Undo.FlushUndoRecordObjects();
        Undo.CollapseUndoOperations(group);
        Undo.PerformUndo();
        AssertLayout(Vector3Int.zero, "GHHHHHHHHHHHI");
        AssertFace(0, 1, StageBlockFace.J);
        Undo.PerformRedo();
        AssertLayout(Vector3Int.zero, "GBBBBBBBBBBBC");
        AssertFace(0, 1, StageBlockFace.K);
    }

    [TestCase("ManualFace")]
    [TestCase("WaterFloor")]
    public void NonAutoPainting_DoesNotRecalculateExistingColumn(string mode)
    {
        SeedColumn(0, 0, 4);
        StageBlockColumnConversion.ConvertTilemap(tilemap, palette.Stage2Blocks, false);
        if (mode == "ManualFace")
        {
            SetEnum("stageBlockPaintMode", mode);
        }
        else
        {
            SetEnum("stageTilePaintKind", mode);
        }

        Invoke("PaintTile", new Vector3Int(1, 1, 0));
        AssertColumn(0, 0, 4);
    }

    [Test]
    public void Recalculation_LeavesUntouchedColumnsAloneAndIsRepeatable()
    {
        SeedColumn(0, 0, 4);
        SeedColumn(10, 0, 4);
        Recalculate(Vector3Int.zero);
        AssertColumn(0, 0, 4);
        AssertNoColumnFaces(10, 0, 4);
        Recalculate(Vector3Int.zero);
        AssertColumn(0, 0, 4);
        AssertNoColumnFaces(10, 0, 4);
    }

    [Test]
    public void MissingOptionalFace_KeepsLegacyValidationAndRendering()
    {
        StageEditorPalette incompletePalette = Object.Instantiate(palette);
        objectsToDestroy.Add(incompletePalette);
        SerializedObject serialized = new SerializedObject(incompletePalette);
        serialized.FindProperty("stage2Blocks").FindPropertyRelative("k").objectReferenceValue = null;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(incompletePalette.Stage2Blocks.HasAllTiles(), Is.True);
        Assert.That(incompletePalette.Stage2Blocks.HasColumnTiles(), Is.False);
        SetField("palette", incompletePalette);
        Invoke("ApplyTileEditRectangle", Vector3Int.zero, new Vector3Int(0, 3, 0));
        AssertNoColumnFaces(0, 0, 4);
        Assert.That(StageBlockColumnConversion.ConvertTilemap(tilemap, incompletePalette.Stage2Blocks).ConvertedTiles,
            Is.Zero);
    }

    [Test]
    public void Conversion_PreservesUnrelatedCellsAndCellProperties_AndIsRepeatable()
    {
        SeedColumn(0, 0, 4);
        SeedColumn(10, 0, 4);
        TileBase blocker = CreateBlocker("Unknown");
        tilemap.SetTile(new Vector3Int(11, 1, 0), blocker);
        tilemap.SetTile(new Vector3Int(20, 0, 0), palette.Stage1Blocks.FirstAvailableTile());
        Vector3Int decoratedCell = new Vector3Int(0, 1, 0);
        Matrix4x4 transform = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 180), Vector3.one);
        tilemap.SetTileFlags(decoratedCell, TileFlags.None);
        tilemap.SetColor(decoratedCell, Color.cyan);
        tilemap.SetTransformMatrix(decoratedCell, transform);
        tilemap.SetTileFlags(decoratedCell, TileFlags.LockAll);
        BoundsInt originalBounds = tilemap.cellBounds;
        TileBase[] originalTiles = tilemap.GetTilesBlock(originalBounds);

        StageBlockColumnConversion.Result result = StageBlockColumnConversion.ConvertTilemap(
            tilemap, palette.Stage2Blocks, false);
        Assert.That(result.ConvertedColumns, Is.EqualTo(2));
        Assert.That(result.ConvertedTiles, Is.EqualTo(7));
        AssertColumn(0, 0, 4);
        AssertFace(10, 0, StageBlockFace.M);
        AssertFace(10, 1, StageBlockFace.E);
        AssertFace(10, 2, StageBlockFace.K);
        AssertFace(10, 3, StageBlockFace.J);
        Assert.That(tilemap.GetColor(decoratedCell), Is.EqualTo(Color.cyan));
        Assert.That(tilemap.GetTransformMatrix(decoratedCell), Is.EqualTo(transform));
        Assert.That(tilemap.GetTileFlags(decoratedCell), Is.EqualTo(TileFlags.LockAll));
        Assert.That(tilemap.cellBounds, Is.EqualTo(originalBounds));
        int index = 0;
        foreach (Vector3Int cell in originalBounds.allPositionsWithin)
        {
            Assert.That(tilemap.HasTile(cell), Is.EqualTo(originalTiles[index] != null), cell.ToString());
            if (cell.x != 0 && !(cell.x == 10 && cell.y >= 0 && cell.y < 4 && cell.y != 1))
            {
                Assert.That(tilemap.GetTile(cell), Is.SameAs(originalTiles[index]), cell.ToString());
            }

            index++;
        }

        StageBlockColumnConversion.Result second = StageBlockColumnConversion.ConvertTilemap(
            tilemap, palette.Stage2Blocks, false);
        Assert.That(second.ConvertedColumns, Is.Zero);
        Assert.That(second.ConvertedTiles, Is.Zero);
    }

    [Test]
    public void ImportedTiles_AlignToCellsAndRetainFullGridCollisions()
    {
        Tile reference = (Tile)palette.Stage2Blocks.GetTile(StageBlockFace.A);
        foreach (StageBlockFace face in new[] { StageBlockFace.J, StageBlockFace.K, StageBlockFace.M })
        {
            Tile tile = (Tile)palette.Stage2Blocks.GetTile(face);
            Assert.That(tile.sprite, Is.Not.Null);
            Assert.That(tile.sprite.name, Is.EqualTo($"Tile_GrassBlock_{face}_0"));
            Assert.That(tile.sprite.bounds.size.x, Is.EqualTo(1).Within(0.001f));
            Assert.That(tile.sprite.bounds.size.y, Is.EqualTo(1).Within(0.001f));
            Assert.That(tile.sprite.pivot, Is.EqualTo(new Vector2(256, 256)));
            Assert.That(tile.transform, Is.EqualTo(reference.transform));
            Assert.That(tile.flags, Is.EqualTo(reference.flags));
            Assert.That(tile.colliderType, Is.EqualTo(Tile.ColliderType.Grid));
        }

        SeedColumn(0, 0, 4);
        StageBlockColumnConversion.ConvertTilemap(tilemap, palette.Stage2Blocks, false);
        TilemapCollider2D collider = tilemap.gameObject.AddComponent<TilemapCollider2D>();
        collider.ProcessTilemapChanges();
        Physics2D.SyncTransforms();
        Assert.That(collider.bounds.size.x, Is.EqualTo(1).Within(0.001f));
        Assert.That(collider.bounds.size.y, Is.EqualTo(4).Within(0.001f));
        for (int y = 0; y < 4; y++)
        {
            Assert.That(collider.OverlapPoint(new Vector2(0.5f, y + 0.5f)), Is.True);
        }
    }

    private void SeedColumn(int x, int bottom, int height)
    {
        for (int y = bottom; y < bottom + height; y++)
        {
            tilemap.SetTile(new Vector3Int(x, y, 0), palette.Stage2Blocks.GetTile(StageBlockFace.E));
        }
    }

    private void SeedLegacyHorizontalArm()
    {
        for (int x = 0; x <= 12; x++)
        {
            StageBlockFace face = x == 0 ? StageBlockFace.G : x == 12 ? StageBlockFace.I : StageBlockFace.H;
            tilemap.SetTile(new Vector3Int(x, 0, 0), palette.Stage2Blocks.GetTile(face));
        }

        tilemap.SetTile(new Vector3Int(0, 1, 0), palette.Stage2Blocks.GetTile(StageBlockFace.J));
    }

    private static List<Vector3Int> CreateBentStroke(bool growsUp)
    {
        return growsUp
            ? new List<Vector3Int>
            {
                new Vector3Int(3, 1, 0), new Vector3Int(2, 1, 0), new Vector3Int(2, 2, 0),
                new Vector3Int(2, 3, 0), new Vector3Int(2, 4, 0)
            }
            : new List<Vector3Int>
            {
                new Vector3Int(1, 4, 0), new Vector3Int(2, 4, 0), new Vector3Int(2, 3, 0),
                new Vector3Int(2, 2, 0), new Vector3Int(2, 1, 0)
            };
    }

    private void AssertBentStroke(bool growsUp, bool mirror)
    {
        string[] rows = growsUp
            ? new[] { "OOOOO", "OOJOO", "OOKOO", "OOKOO", "OOGCO", "OOOOO" }
            : new[] { "OOOOO", "OACOO", "OOKOO", "OOKOO", "OOMOO", "OOOOO" };
        AssertMirroredLayout(mirror, rows);
    }

    private void AssertMirroredLayout(bool mirror, params string[] rows)
    {
        if (mirror)
        {
            for (int y = 0; y < rows.Length; y++)
            {
                char[] row = rows[y].ToCharArray();
                Array.Reverse(row);
                for (int x = 0; x < row.Length; x++)
                {
                    row[x] = row[x] == 'A' ? 'C' : row[x] == 'C' ? 'A' :
                        row[x] == 'G' ? 'I' : row[x] == 'I' ? 'G' : row[x];
                }

                rows[y] = new string(row);
            }
        }

        AssertLayout(Vector3Int.zero, rows);
    }

    private void AssertLayout(Vector3Int bottomLeft, params string[] rows)
    {
        for (int row = 0; row < rows.Length; row++)
        {
            for (int x = 0; x < rows[row].Length; x++)
            {
                Vector3Int cell = bottomLeft + new Vector3Int(x, rows.Length - row - 1, 0);
                if (rows[row][x] == 'O')
                {
                    Assert.That(tilemap.HasTile(cell), Is.False, cell.ToString());
                }
                else
                {
                    AssertFace(cell.x, cell.y, (StageBlockFace)Enum.Parse(typeof(StageBlockFace), rows[row][x].ToString()));
                }
            }
        }
    }

    private void AssertFace(int x, int y, StageBlockFace face)
    {
        Assert.That(tilemap.GetTile(new Vector3Int(x, y, 0)), Is.SameAs(palette.Stage2Blocks.GetTile(face)),
            $"Cell ({x}, {y}) should use {face}");
    }

    private void AssertColumnWithSideConnection(int connectionY)
    {
        for (int y = 0; y < 13; y++)
        {
            StageBlockFace face = y == connectionY
                ? y == 0 ? StageBlockFace.G : y == 12 ? StageBlockFace.A : StageBlockFace.D
                : y == 0 ? StageBlockFace.M : y == 12 ? StageBlockFace.J : StageBlockFace.K;
            AssertFace(0, y, face);
        }
    }

    private void AssertColumn(int x, int bottom, int height)
    {
        for (int y = bottom; y < bottom + height; y++)
        {
            StageBlockFace face = height == 1 ? StageBlockFace.E :
                y == bottom ? StageBlockFace.M : y == bottom + height - 1 ? StageBlockFace.J : StageBlockFace.K;
            Assert.That(tilemap.GetTile(new Vector3Int(x, y, 0)), Is.SameAs(palette.Stage2Blocks.GetTile(face)),
                $"Cell ({x}, {y}) should use {face}");
        }
    }

    private void AssertNoColumnFaces(int x, int bottom, int height)
    {
        for (int y = bottom; y < bottom + height; y++)
        {
            TileBase tile = tilemap.GetTile(new Vector3Int(x, y, 0));
            Assert.That(tile, Is.Not.Null);
            Assert.That(tile, Is.Not.SameAs(palette.Stage2Blocks.GetTile(StageBlockFace.J)));
            Assert.That(tile, Is.Not.SameAs(palette.Stage2Blocks.GetTile(StageBlockFace.K)));
            Assert.That(tile, Is.Not.SameAs(palette.Stage2Blocks.GetTile(StageBlockFace.M)));
        }
    }

    private TileBase CreateBlocker(string kind)
    {
        if (kind == "Stage1") return palette.Stage1Blocks.FirstAvailableTile();
        if (kind == "Water") return palette.WaterFloorTiles.FirstAvailableTile();
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        objectsToDestroy.Add(tile);
        return tile;
    }

    private void Recalculate(Vector3Int editedCell)
    {
        Invoke("RecalculateAutoBlockTiles", StageBlockAutoTileResolver.ExpandByOneCell(
            StageBlockAutoTileResolver.CreateInclusiveBounds(editedCell, editedCell)));
    }

    private GameObject CreateObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        objectsToDestroy.Add(gameObject);
        return gameObject;
    }

    private Tilemap CreateTilemap(Transform parent)
    {
        GameObject gameObject = CreateObject("Grass Column Test Tilemap");
        gameObject.transform.SetParent(parent);
        return gameObject.AddComponent<Tilemap>();
    }

    private void SetField(string name, object value)
    {
        typeof(StageEditorWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, value);
    }

    private object GetField(string name)
    {
        return typeof(StageEditorWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
    }

    private void SetEnum(string name, string value)
    {
        FieldInfo field = typeof(StageEditorWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(window, Enum.Parse(field.FieldType, value));
    }

    private void Invoke(string name, params object[] args)
    {
        typeof(StageEditorWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, args);
    }
}

public sealed class StageBlockColumnConversionSceneTests
{
    [Test]
    public void SavedFixCapcom_AlreadyUsesColumnFacesAndNeedsNoFurtherConversion()
    {
        StageEditorPalette palette = AssetDatabase.LoadAssetAtPath<StageEditorPalette>("Assets/Editor/StageEditorPalette.asset");
        Assert.That(palette, Is.Not.Null);
        Scene scene = SceneManager.GetSceneByPath(StageBlockColumnConversion.FixCapcomScenePath);
        bool openedScene = !scene.IsValid() || !scene.isLoaded;
        if (openedScene)
        {
            scene = EditorSceneManager.OpenScene(StageBlockColumnConversion.FixCapcomScenePath, OpenSceneMode.Additive);
        }

        try
        {
            int columnTiles = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Tilemap tilemap in root.GetComponentsInChildren<Tilemap>(true))
                {
                    Assert.That(StageBlockColumnConversion.ConvertTilemap(tilemap, palette.Stage2Blocks, false).ConvertedTiles,
                        Is.Zero, $"Saved tilemap {tilemap.name} still needs conversion");
                    foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
                    {
                        TileBase tile = tilemap.GetTile(cell);
                        if (tile == palette.Stage2Blocks.GetTile(StageBlockFace.J) ||
                            tile == palette.Stage2Blocks.GetTile(StageBlockFace.K) ||
                            tile == palette.Stage2Blocks.GetTile(StageBlockFace.M))
                        {
                            columnTiles++;
                        }
                    }
                }
            }

            Assert.That(columnTiles, Is.GreaterThan(0));
            Debug.Log($"[Grass Columns] Reopened Fix_CAPCOM: verified {columnTiles} J/K/M tiles; no further conversion needed.");
        }
        finally
        {
            if (openedScene) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
