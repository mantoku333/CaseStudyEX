namespace EditorTools
{
    public struct WaterFloorNeighborState
    {
        public bool waterLeft;
        public bool waterRight;
        public bool solidLeft;
        public bool solidRight;
    }

    public static class WaterFloorAutoTileResolver
    {
        public static WaterFloorFace Resolve(WaterFloorNeighborState neighbors)
        {
            if (!neighbors.waterLeft && !neighbors.waterRight)
            {
                return WaterFloorFace.B;
            }

            if (neighbors.waterLeft && neighbors.waterRight)
            {
                return WaterFloorFace.B;
            }

            if (!neighbors.waterLeft)
            {
                return neighbors.solidLeft ? WaterFloorFace.D : WaterFloorFace.A;
            }

            return neighbors.solidRight ? WaterFloorFace.E : WaterFloorFace.C;
        }
    }
}
