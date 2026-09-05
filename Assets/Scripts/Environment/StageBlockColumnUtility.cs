using System;
using System.Collections.Generic;
using UnityEngine;

namespace EditorTools
{
    /// <summary>Shared column geometry for interactive painting and existing-scene conversion.</summary>
    public static class StageBlockColumnUtility
    {
        /// <summary>Finds each complete vertical run touching the seeds, including cells outside their bounds.</summary>
        public static List<BoundsInt> CollectColumns(BoundsInt seedBounds, Func<Vector3Int, bool> isColumnBlock)
        {
            List<BoundsInt> columns = new List<BoundsInt>();
            HashSet<Vector3Int> visited = new HashSet<Vector3Int>();

            foreach (Vector3Int seed in seedBounds.allPositionsWithin)
            {
                if (visited.Contains(seed) || !isColumnBlock(seed))
                {
                    continue;
                }

                Vector3Int bottom = seed;
                while (isColumnBlock(bottom + Vector3Int.down))
                {
                    bottom += Vector3Int.down;
                }

                Vector3Int top = seed;
                while (isColumnBlock(top + Vector3Int.up))
                {
                    top += Vector3Int.up;
                }

                BoundsInt column = new BoundsInt(bottom.x, bottom.y, bottom.z, 1, top.y - bottom.y + 1, 1);
                columns.Add(column);
                foreach (Vector3Int cell in column.allPositionsWithin)
                {
                    visited.Add(cell);
                }
            }

            return columns;
        }

        /// <summary>Requires at least two cells and an empty edge border; diagonals do not count.</summary>
        public static bool IsIsolatedColumn(BoundsInt column, Func<Vector3Int, bool> hasTile)
        {
            if (column.size.x != 1 || column.size.z != 1 || column.size.y < 2)
            {
                return false;
            }

            Vector3Int below = new Vector3Int(column.xMin, column.yMin - 1, column.zMin);
            Vector3Int above = new Vector3Int(column.xMin, column.yMax, column.zMin);
            if (hasTile(below) || hasTile(above))
            {
                return false;
            }

            foreach (Vector3Int cell in column.allPositionsWithin)
            {
                if (hasTile(cell + Vector3Int.left) || hasTile(cell + Vector3Int.right))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
