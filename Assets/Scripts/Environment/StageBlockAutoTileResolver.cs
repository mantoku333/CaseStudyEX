using UnityEngine;

namespace EditorTools
{
    public struct StageBlockNeighborState
    {
        public bool left;
        public bool right;
        public bool up;
        public bool down;
        public bool upLeft;
        public bool upRight;
        public bool downLeft;
        public bool downRight;
    }

    public static class StageBlockAutoTileResolver
    {
        public static bool ShouldPaintCell(bool replaceExistingOnly, bool hasExistingBlock)
        {
            return !replaceExistingOnly || hasExistingBlock;
        }

        public static StageBlockFace Resolve(StageBlockNeighborState neighbors)
        {
            bool exposedLeft = !neighbors.left;
            bool exposedRight = !neighbors.right;
            bool exposedUp = !neighbors.up;
            bool exposedDown = !neighbors.down;

            if (exposedLeft && exposedRight && exposedDown)
            {
                return StageBlockFace.E;
            }

            if (neighbors.up && neighbors.down && exposedLeft && exposedRight)
            {
                if (neighbors.upRight && neighbors.downRight)
                {
                    return StageBlockFace.D;
                }

                if (neighbors.upLeft && neighbors.downLeft)
                {
                    return StageBlockFace.F;
                }

                return StageBlockFace.E;
            }

            if (exposedUp)
            {
                if (exposedLeft)
                {
                    return StageBlockFace.A;
                }

                if (exposedRight)
                {
                    return StageBlockFace.C;
                }

                return StageBlockFace.B;
            }

            if (exposedDown)
            {
                if (exposedLeft)
                {
                    return StageBlockFace.G;
                }

                if (exposedRight)
                {
                    return StageBlockFace.I;
                }

                return StageBlockFace.H;
            }

            if (exposedLeft)
            {
                return StageBlockFace.D;
            }

            if (exposedRight)
            {
                return StageBlockFace.F;
            }

            return StageBlockFace.E;
        }

        public static BoundsInt CreateInclusiveBounds(Vector3Int startCell, Vector3Int endCell)
        {
            int minX = Mathf.Min(startCell.x, endCell.x);
            int maxX = Mathf.Max(startCell.x, endCell.x);
            int minY = Mathf.Min(startCell.y, endCell.y);
            int maxY = Mathf.Max(startCell.y, endCell.y);

            return new BoundsInt(
                minX,
                minY,
                0,
                maxX - minX + 1,
                maxY - minY + 1,
                1);
        }

        public static BoundsInt ExpandByOneCell(BoundsInt bounds)
        {
            return new BoundsInt(
                bounds.xMin - 1,
                bounds.yMin - 1,
                0,
                bounds.size.x + 2,
                bounds.size.y + 2,
                1);
        }
    }
}
