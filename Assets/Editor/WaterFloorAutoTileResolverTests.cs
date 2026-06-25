using EditorTools;
using NUnit.Framework;

public sealed class WaterFloorAutoTileResolverTests
{
    [Test]
    public void Resolve_SingleWaterTile_UsesMiddleFace()
    {
        Assert.AreEqual(WaterFloorFace.B, WaterFloorAutoTileResolver.Resolve(new WaterFloorNeighborState()));
    }

    [Test]
    public void Resolve_OpenFiveTileRun_UsesOpenCapsAndMiddleFaces()
    {
        Assert.AreEqual(WaterFloorFace.A, Resolve(false, true, false, false));
        Assert.AreEqual(WaterFloorFace.B, Resolve(true, true, false, false));
        Assert.AreEqual(WaterFloorFace.B, Resolve(true, true, false, false));
        Assert.AreEqual(WaterFloorFace.B, Resolve(true, true, false, false));
        Assert.AreEqual(WaterFloorFace.C, Resolve(true, false, false, false));
    }

    [Test]
    public void Resolve_BlockedFiveTileRun_UsesBlockedCapsAndMiddleFaces()
    {
        Assert.AreEqual(WaterFloorFace.D, Resolve(false, true, true, false));
        Assert.AreEqual(WaterFloorFace.B, Resolve(true, true, false, false));
        Assert.AreEqual(WaterFloorFace.B, Resolve(true, true, false, false));
        Assert.AreEqual(WaterFloorFace.B, Resolve(true, true, false, false));
        Assert.AreEqual(WaterFloorFace.E, Resolve(true, false, false, true));
    }

    [Test]
    public void Resolve_MixedBlockedAndOpenEnds_UsesPerSideCaps()
    {
        Assert.AreEqual(WaterFloorFace.D, Resolve(false, true, true, false));
        Assert.AreEqual(WaterFloorFace.C, Resolve(true, false, false, false));
        Assert.AreEqual(WaterFloorFace.A, Resolve(false, true, false, false));
        Assert.AreEqual(WaterFloorFace.E, Resolve(true, false, false, true));
    }

    [Test]
    public void Resolve_NonWaterNeighbors_DoNotConnect()
    {
        Assert.AreEqual(WaterFloorFace.B, Resolve(false, false, true, true));
    }

    private static WaterFloorFace Resolve(bool waterLeft, bool waterRight, bool solidLeft, bool solidRight)
    {
        return WaterFloorAutoTileResolver.Resolve(new WaterFloorNeighborState
        {
            waterLeft = waterLeft,
            waterRight = waterRight,
            solidLeft = solidLeft,
            solidRight = solidRight
        });
    }
}
