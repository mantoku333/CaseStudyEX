using System.Collections.Generic;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Covers the two pieces of the new camera system that decide how it feels:
/// the per-axis confiner bounds rule, and room resolution with hysteresis.
///
/// The numbers used here are the real measurements from Fix_0925_NYAA.unity, so a regression
/// in either rule shows up as a failure described in the terms of the actual scene.
/// </summary>
public sealed class RoomCameraDirectorTests
{
    private const float Aspect = 16f / 9f;

    // Measured from the scene.
    private const float StandardRoomWidth = 38.3f;
    private const float StandardRoomHeight = 20.3f;
    private const float StandardOrtho = 12f;      // view 42.67 x 24
    private const float LongHallWidth = 163.1f;   // Col_105
    private const float TallRoomHeight = 28.7f;   // Col_109
    private const float TallRoomWidth = 38.8f;

    // Matches RoomCameraDirector.enterDepth's default.
    private const float EnterDepth = 3f;

    private readonly List<GameObject> spawned = new();

    [SetUp]
    public void SetUp()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        spawned.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
            {
                Object.DestroyImmediate(spawned[i]);
            }
        }

        spawned.Clear();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    // ---------------------------------------------------------------- bounds rule

    [Test]
    public void StandardRoom_FitsOnScreen_SoBothAxesArePinned()
    {
        var room = new Bounds(Vector3.zero, new Vector3(StandardRoomWidth, StandardRoomHeight, 0f));

        Assert.IsTrue(
            RoomCameraBoundsUtility.IsFullyPinned(room, StandardOrtho, Aspect),
            "A 38.3 x 20.3 room inside a 42.7 x 24 view must stay fully pinned, " +
            "otherwise the fixed framing the artists authored would start drifting.");
    }

    [Test]
    public void PinnedAxes_CentreOnAuthoredCameraPosition_SoHandPlacedFramingSurvives()
    {
        var room = new Bounds(Vector3.zero, new Vector3(StandardRoomWidth, StandardRoomHeight, 0f));
        // Col_106 sits 1.7 units off its room centre; that offset is deliberate framing.
        var cameraPosition = new Vector3(1.7f, 0.5f, -10f);

        Bounds bounds = RoomCameraBoundsUtility.Build(room, cameraPosition, StandardOrtho, Aspect);

        Assert.AreEqual(cameraPosition.x, bounds.center.x, 1e-4f, "Pinned X must keep the authored framing.");
        Assert.AreEqual(cameraPosition.y, bounds.center.y, 1e-4f, "Pinned Y must keep the authored framing.");
    }

    [Test]
    public void PinnedAxis_IsStrictlyLargerThanView_SoTheCheapBakeIsUsed()
    {
        var room = new Bounds(Vector3.zero, new Vector3(StandardRoomWidth, StandardRoomHeight, 0f));
        Bounds bounds = RoomCameraBoundsUtility.Build(room, Vector3.zero, StandardOrtho, Aspect);
        Vector2 view = RoomCameraBoundsUtility.GetViewSize(StandardOrtho, Aspect);

        // A shape that merely equals the view counts as an oversize window for the confiner,
        // which needs a far more expensive bake. Padding keeps it strictly larger.
        Assert.Greater(bounds.size.x, view.x, "Pinned X must exceed the view width.");
        Assert.Greater(bounds.size.y, view.y, "Pinned Y must exceed the view height.");
    }

    [Test]
    public void LongHall_FollowsXAndPinsY()
    {
        var room = new Bounds(Vector3.zero, new Vector3(LongHallWidth, StandardRoomHeight, 0f));
        Bounds bounds = RoomCameraBoundsUtility.Build(room, new Vector3(5f, 0f, -10f), 12.4f, Aspect);
        Vector2 view = RoomCameraBoundsUtility.GetViewSize(12.4f, Aspect);

        Assert.AreEqual(LongHallWidth, bounds.size.x, 1e-4f,
            "A 163 unit hall is wider than the view, so X must clamp to the hall and follow the player.");
        Assert.AreEqual(room.center.x, bounds.center.x, 1e-4f,
            "A following axis must stay aligned with the room it clamps against.");
        Assert.Greater(bounds.size.y, view.y, "The hall is shorter than the view, so Y stays pinned.");
    }

    [Test]
    public void TallRoom_PinsXAndFollowsY()
    {
        // Col_109: 38.8 x 28.7 against a 42.7 x 24 view. The player currently walks
        // 2.5 units off the top or bottom of the screen here.
        var room = new Bounds(Vector3.zero, new Vector3(TallRoomWidth, TallRoomHeight, 0f));
        Bounds bounds = RoomCameraBoundsUtility.Build(room, Vector3.zero, StandardOrtho, Aspect);
        Vector2 view = RoomCameraBoundsUtility.GetViewSize(StandardOrtho, Aspect);

        Assert.Greater(bounds.size.x, view.x, "X stays pinned because the room is narrower than the view.");
        Assert.AreEqual(TallRoomHeight, bounds.size.y, 1e-4f,
            "Y must follow the player so the room no longer lets the player leave the screen.");
    }

    [Test]
    public void GetOrthographicSizeToFit_ReportsTheSizeTallRoomsWouldNeed()
    {
        var room = new Bounds(Vector3.zero, new Vector3(TallRoomWidth, TallRoomHeight, 0f));
        float needed = RoomCameraBoundsUtility.GetOrthographicSizeToFit(room, Aspect);

        Assert.AreEqual(TallRoomHeight / 2f, needed, 1e-4f);
        Assert.Greater(needed, StandardOrtho, "Col_109 cannot be framed at ortho 12, which is why it clips.");
    }

    // ---------------------------------------------------------------- room resolution

    [Test]
    public void OverlappingRooms_KeepTheCurrentRoom_InsteadOfHandingOverToTheSmallerOne()
    {
        // Reproduces Col_215 / Col_216: a full-width overlap band only 0.8 units tall,
        // with the two rooms differing by 2% in area. The old rule gave the camera to
        // whichever happened to be smaller, regardless of where the player came from.
        RoomCameraVolume lower = CreateVolume("Col_215", new Vector2(0f, 0f), new Vector2(37.9f, 19.9f));
        RoomCameraVolume upper = CreateVolume("Col_216", new Vector2(0f, 19.55f), new Vector2(37.9f, 20.3f));

        var volumes = new List<RoomCameraVolume> { lower, upper };
        var pointInBand = new Vector3(0f, 9.9f, 0f);

        Assert.IsTrue(lower.ContainsPoint(pointInBand) && upper.ContainsPoint(pointInBand),
            "Test setup must actually overlap at this point.");
        Assert.Less(
            GetArea(lower), GetArea(upper),
            "Test setup: the current room must be the LARGER one, so that holding it proves " +
            "hysteresis rather than accidentally agreeing with the old smallest-area rule.");

        // Current room is the larger one. The old rule would hand the camera to the smaller
        // Col_215 the moment the player entered the shared band.
        RoomCameraVolume resolved = RoomCameraDirector.Resolve(volumes, upper, pointInBand, 0.5f, EnterDepth, true);

        Assert.AreSame(upper, resolved,
            "While the player is still inside the current room, the camera must not switch, " +
            "even though a smaller room also contains the point.");
    }

    [Test]
    public void LeavingARoom_SwitchesToTheRoomThePlayerIsDeepestInside()
    {
        RoomCameraVolume left = CreateVolume("Col_212", new Vector2(0f, 0f), new Vector2(38.1f, 20.3f));
        RoomCameraVolume right = CreateVolume("Col_213", new Vector2(38.2f, 0f), new Vector2(38.1f, 20.3f));

        var volumes = new List<RoomCameraVolume> { left, right };
        var deepInRight = new Vector3(38.2f, 0f, 0f);

        RoomCameraVolume resolved = RoomCameraDirector.Resolve(volumes, left, deepInRight, 0.5f, EnterDepth, true);

        Assert.AreSame(right, resolved, "Once clearly out of the old room, the new room must take over.");
    }

    [Test]
    public void DepthTieBreak_PrefersTheRoomThePlayerIsActuallyInside_NotTheSmallerOne()
    {
        // A small room overlapping the edge of a large one. "Smallest area wins" would pick the
        // small room from a point that is barely inside it; depth picks the room the player is in.
        RoomCameraVolume big = CreateVolume("Big", new Vector2(0f, 0f), new Vector2(60f, 20f));
        RoomCameraVolume small = CreateVolume("Small", new Vector2(29f, 0f), new Vector2(4f, 20f));

        var volumes = new List<RoomCameraVolume> { big, small };
        // 0.5 units inside the small room's left edge, but 2.5 units from the big room's edge.
        var point = new Vector3(27.5f, 0f, 0f);

        Assert.IsTrue(big.ContainsPoint(point) && small.ContainsPoint(point), "Test setup must overlap.");
        Assert.Less(
            GetArea(small), GetArea(big),
            "Test setup: the small room must be the one the old area rule would have chosen.");

        RoomCameraVolume resolved = RoomCameraDirector.Resolve(volumes, null, point, 0.5f, EnterDepth, true);

        Assert.AreSame(big, resolved, "Depth must win over area.");
    }

    [Test]
    public void GapsBetweenRooms_KeepTheLastRoom_SoTheCameraDoesNotSnapOutAndBack()
    {
        // The room colliders do not tile perfectly. The old code released to the follow camera
        // whenever no room contained the player, producing a visible snap in those seams.
        RoomCameraVolume room = CreateVolume("Col_101", new Vector2(0f, 0f), new Vector2(38.3f, 20.3f));
        var volumes = new List<RoomCameraVolume> { room };
        var outsideEverything = new Vector3(500f, 500f, 0f);

        RoomCameraVolume kept = RoomCameraDirector.Resolve(volumes, room, outsideEverything, 0.5f, EnterDepth, true);
        Assert.AreSame(room, kept, "With keepLastRoomInGaps on, the last room must be retained.");

        RoomCameraVolume dropped = RoomCameraDirector.Resolve(volumes, room, outsideEverything, 0.5f, EnterDepth, false);
        Assert.IsNull(dropped, "With the option off, the director must fall through to its fallback camera.");
    }

    [Test]
    public void ExitMargin_DelaysTheSwitchUntilThePlayerIsClearlyOut()
    {
        RoomCameraVolume room = CreateVolume("Col_101", new Vector2(0f, 0f), new Vector2(38.3f, 20.3f));
        var volumes = new List<RoomCameraVolume> { room };

        // 0.3 units past the right edge: still within the 1.0 margin.
        var justOutside = new Vector3(19.45f, 0f, 0f);
        Assert.AreSame(
            room,
            RoomCameraDirector.Resolve(volumes, room, justOutside, 1f, EnterDepth, false),
            "Inside the margin the room must be held.");

        // 2.0 units past the edge: beyond the margin, and no other room contains the point.
        var wellOutside = new Vector3(21.15f, 0f, 0f);
        Assert.IsNull(
            RoomCameraDirector.Resolve(volumes, room, wellOutside, 1f, EnterDepth, false),
            "Past the margin the room must be released.");
    }

    [Test]
    public void UnusableVolumes_AreIgnored()
    {
        RoomCameraVolume withoutCamera = CreateVolume("Broken", new Vector2(0f, 0f), new Vector2(20f, 20f), false);
        var volumes = new List<RoomCameraVolume> { withoutCamera };

        Assert.IsFalse(withoutCamera.IsUsable, "A volume with no camera is not usable.");
        Assert.IsNull(
            RoomCameraDirector.Resolve(volumes, null, Vector3.zero, 0.5f, EnterDepth, false),
            "A volume with no camera must never be activated.");
    }

    [Test]
    public void WalkingBackAndForthOverABoundary_DoesNotKeepFlippingTheCamera()
    {
        // The complaint this covers: crossing between areas repeatedly made the view lurch
        // back and forth. Two abutting standard rooms, 38 units between their cameras.
        RoomCameraVolume left = CreateVolume("Col_101", new Vector2(0f, 0f), new Vector2(38.3f, 20.3f));
        RoomCameraVolume right = CreateVolume("Col_102", new Vector2(38.3f, 0f), new Vector2(38.3f, 20.3f));
        var volumes = new List<RoomCameraVolume> { left, right };

        // Just over the seam. Exit margin alone covers this much.
        Assert.AreSame(
            left,
            RoomCameraDirector.Resolve(volumes, left, new Vector3(19.4f, 0f, 0f), 0.5f, EnterDepth, true),
            "A step over the boundary must not move the camera.");

        // Past the exit margin but still shallow in the new room. Before enterDepth existed
        // this handed the camera over, which is what made wandering the seam flip the view.
        Assert.AreSame(
            left,
            RoomCameraDirector.Resolve(volumes, left, new Vector3(20.0f, 0f, 0f), 0.5f, EnterDepth, true),
            "Being barely inside the next room must not be enough to take the camera.");

        // Properly into the next room: now it commits.
        Assert.AreSame(
            right,
            RoomCameraDirector.Resolve(volumes, left, new Vector3(23.0f, 0f, 0f), 0.5f, EnterDepth, true),
            "Once the player is genuinely inside, the new room must take over.");

        // And coming back is equally sticky, so the band is symmetric.
        Assert.AreSame(
            right,
            RoomCameraDirector.Resolve(volumes, right, new Vector3(18.6f, 0f, 0f), 0.5f, EnterDepth, true),
            "Stepping back over the seam must not flip the camera either.");
    }

    [Test]
    public void ThinRoom_CanStillTakeTheCamera_DespiteTheEnterDepthRequirement()
    {
        // A room narrower than 2 * enterDepth can never reach the fixed threshold, so the
        // requirement must scale down rather than lock the room out of ever activating.
        RoomCameraVolume corridor = CreateVolume("Col_Wide", new Vector2(0f, 0f), new Vector2(38.3f, 20.3f));
        RoomCameraVolume thin = CreateVolume("Col_Thin", new Vector2(21f, 0f), new Vector2(4f, 20.3f));
        var volumes = new List<RoomCameraVolume> { corridor, thin };

        Assert.Less(thin.GetMaxDepth() * 0.5f, EnterDepth,
            "Test setup: the thin room must be unable to reach the fixed enter depth.");

        // 1.2 units inside the thin room, which is past its scaled requirement of 1.0.
        RoomCameraVolume resolved = RoomCameraDirector.Resolve(
            volumes, corridor, new Vector3(20.2f, 0f, 0f), 0.5f, EnterDepth, true);

        Assert.AreSame(thin, resolved, "A thin room must still be reachable.");
    }

    // ---------------------------------------------------------------- helpers

    private static float GetArea(RoomCameraVolume volume)
    {
        Bounds bounds = volume.AreaBounds;
        return bounds.size.x * bounds.size.y;
    }

    private RoomCameraVolume CreateVolume(string name, Vector2 center, Vector2 size, bool withCamera = true)
    {
        var host = new GameObject(name);
        spawned.Add(host);
        host.transform.position = new Vector3(center.x, center.y, 0f);

        BoxCollider2D box = host.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = size;

        CinemachineCamera camera = null;
        if (withCamera)
        {
            var cameraHost = new GameObject(name + "_Cam");
            spawned.Add(cameraHost);
            cameraHost.transform.position = new Vector3(center.x, center.y, -10f);
            camera = cameraHost.AddComponent<CinemachineCamera>();
            LensSettings lens = camera.Lens;
            lens.OrthographicSize = StandardOrtho;
            camera.Lens = lens;
        }

        var volume = host.AddComponent<RoomCameraVolume>();
        volume.EditorAssign(camera, new Collider2D[] { box }, null, 20, 0);
        return volume;
    }
}
