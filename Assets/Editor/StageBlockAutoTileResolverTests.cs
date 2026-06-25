using System.Collections.Generic;
using EditorTools;
using NUnit.Framework;
using UnityEngine;

[Category("Gameplay")]
public sealed class StageBlockAutoTileResolverTests
{
    [Test]
    public void Resolve_ThreeByThreeRectangle_UsesExpectedFaces()
    {
        HashSet<Vector2Int> solids = CreateSolids(
            (0, 0), (1, 0), (2, 0),
            (0, 1), (1, 1), (2, 1),
            (0, 2), (1, 2), (2, 2));

        Assert.AreEqual(StageBlockFace.A, Resolve(solids, 0, 2));
        Assert.AreEqual(StageBlockFace.B, Resolve(solids, 1, 2));
        Assert.AreEqual(StageBlockFace.C, Resolve(solids, 2, 2));
        Assert.AreEqual(StageBlockFace.D, Resolve(solids, 0, 1));
        Assert.AreEqual(StageBlockFace.E, Resolve(solids, 1, 1));
        Assert.AreEqual(StageBlockFace.F, Resolve(solids, 2, 1));
        Assert.AreEqual(StageBlockFace.G, Resolve(solids, 0, 0));
        Assert.AreEqual(StageBlockFace.H, Resolve(solids, 1, 0));
        Assert.AreEqual(StageBlockFace.I, Resolve(solids, 2, 0));
    }

    [Test]
    public void Resolve_SurroundedBlock_UsesMiddleFace()
    {
        StageBlockNeighborState neighbors = new StageBlockNeighborState
        {
            left = true,
            right = true,
            up = true,
            down = true
        };

        Assert.AreEqual(StageBlockFace.E, StageBlockAutoTileResolver.Resolve(neighbors));
    }

    [Test]
    public void Resolve_FloatingWallWithoutFloor_UsesMiddleFace()
    {
        StageBlockNeighborState neighbors = new StageBlockNeighborState
        {
            up = true
        };

        Assert.AreEqual(StageBlockFace.E, StageBlockAutoTileResolver.Resolve(neighbors));
    }

    [Test]
    public void Resolve_CeilingFloorConnectorContinuingRight_UsesRightWallFace()
    {
        StageBlockNeighborState neighbors = new StageBlockNeighborState
        {
            up = true,
            down = true,
            upRight = true,
            downRight = true
        };

        Assert.AreEqual(StageBlockFace.D, StageBlockAutoTileResolver.Resolve(neighbors));
    }

    [Test]
    public void Resolve_CeilingFloorConnectorContinuingLeft_UsesLeftWallFace()
    {
        StageBlockNeighborState neighbors = new StageBlockNeighborState
        {
            up = true,
            down = true,
            upLeft = true,
            downLeft = true
        };

        Assert.AreEqual(StageBlockFace.F, StageBlockAutoTileResolver.Resolve(neighbors));
    }

    [Test]
    public void ExpandByOneCell_ExpandsEditedBoundsByOneCellBorder()
    {
        BoundsInt editedBounds = StageBlockAutoTileResolver.CreateInclusiveBounds(
            new Vector3Int(2, 3, 0),
            new Vector3Int(4, 6, 0));

        BoundsInt expandedBounds = StageBlockAutoTileResolver.ExpandByOneCell(editedBounds);

        Assert.AreEqual(1, expandedBounds.xMin);
        Assert.AreEqual(2, expandedBounds.yMin);
        Assert.AreEqual(5, expandedBounds.size.x);
        Assert.AreEqual(6, expandedBounds.size.y);
    }

    [Test]
    public void ShouldPaintCell_PlaceAndReplace_AllowsEmptyCells()
    {
        Assert.IsTrue(StageBlockAutoTileResolver.ShouldPaintCell(false, false));
    }

    [Test]
    public void ShouldPaintCell_ReplaceExistingOnly_SkipsEmptyCells()
    {
        Assert.IsFalse(StageBlockAutoTileResolver.ShouldPaintCell(true, false));
    }

    [Test]
    public void ShouldPaintCell_ReplaceExistingOnly_AllowsExistingBlocks()
    {
        Assert.IsTrue(StageBlockAutoTileResolver.ShouldPaintCell(true, true));
    }

    private static StageBlockFace Resolve(HashSet<Vector2Int> solids, int x, int y)
    {
        Vector2Int cell = new Vector2Int(x, y);
        return StageBlockAutoTileResolver.Resolve(new StageBlockNeighborState
        {
            left = solids.Contains(cell + Vector2Int.left),
            right = solids.Contains(cell + Vector2Int.right),
            up = solids.Contains(cell + Vector2Int.up),
            down = solids.Contains(cell + Vector2Int.down),
            upLeft = solids.Contains(cell + Vector2Int.up + Vector2Int.left),
            upRight = solids.Contains(cell + Vector2Int.up + Vector2Int.right),
            downLeft = solids.Contains(cell + Vector2Int.down + Vector2Int.left),
            downRight = solids.Contains(cell + Vector2Int.down + Vector2Int.right)
        });
    }

    private static HashSet<Vector2Int> CreateSolids(params (int x, int y)[] cells)
    {
        HashSet<Vector2Int> solids = new HashSet<Vector2Int>();
        foreach ((int x, int y) cell in cells)
        {
            solids.Add(new Vector2Int(cell.x, cell.y));
        }

        return solids;
    }
}
