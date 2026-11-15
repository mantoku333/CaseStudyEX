using UnityEngine;

/// <summary>
/// Builds the bounding shape that <c>CinemachineConfiner2D</c> uses for a room camera.
///
/// The rule is per-axis and uniform for every room:
///
///   room size on an axis &lt;= view size  -> the confiner pins the camera on that axis
///   room size on an axis &gt;  view size  -> the camera follows the player, clamped to the room
///
/// That single rule covers all three room shapes in this project without a per-room mode flag:
///
///   38 x 20 standard room (view 42.7 x 24) -> pinned on both axes  = today's fixed framing
///   163 x 20 long hall                     -> follows X, pinned Y  = horizontal follow
///   38.8 x 28.7 tall room                  -> pinned X, follows Y  = vertical follow
///
/// On a pinned axis the camera ends up at the bounding shape's centre, so the centre is taken
/// from the camera's authored position instead of the room centre. That keeps hand-placed
/// framing (measured at up to 1.7 units off the room centre in this project) exactly as it was.
/// </summary>
public static class RoomCameraBoundsUtility
{
    /// <summary>
    /// Extra size added to a pinned axis. The confiner treats a shape that is not strictly
    /// larger than the view as an oversize window, which needs a far more expensive bake,
    /// so the shape is kept marginally larger than the view.
    /// </summary>
    public const float DefaultPadding = 0.1f;

    public const float DefaultAspect = 16f / 9f;

    /// <summary>View size in world units for an orthographic lens.</summary>
    public static Vector2 GetViewSize(float orthographicSize, float aspect)
    {
        float height = Mathf.Max(0f, orthographicSize) * 2f;
        float width = height * Mathf.Max(0.01f, aspect);
        return new Vector2(width, height);
    }

    /// <summary>
    /// Builds the confiner bounding box for one room.
    /// </summary>
    /// <param name="roomBounds">World bounds of the room's area colliders.</param>
    /// <param name="cameraPosition">Authored camera position, used as the centre of pinned axes.</param>
    /// <param name="orthographicSize">The room camera's orthographic size.</param>
    /// <param name="aspect">Viewport aspect ratio (width / height).</param>
    /// <param name="padding">Extra size on pinned axes. Use <see cref="DefaultPadding"/>.</param>
    public static Bounds Build(
        Bounds roomBounds,
        Vector3 cameraPosition,
        float orthographicSize,
        float aspect,
        float padding = DefaultPadding)
    {
        Vector2 view = GetViewSize(orthographicSize, aspect);
        padding = Mathf.Max(0f, padding);

        bool pinnedX = roomBounds.size.x <= view.x;
        bool pinnedY = roomBounds.size.y <= view.y;

        float sizeX = pinnedX ? view.x + padding : roomBounds.size.x;
        float sizeY = pinnedY ? view.y + padding : roomBounds.size.y;

        // A pinned axis resolves to the shape centre, so anchor it on the authored camera
        // position. A following axis must stay aligned with the room it clamps against.
        float centerX = pinnedX ? cameraPosition.x : roomBounds.center.x;
        float centerY = pinnedY ? cameraPosition.y : roomBounds.center.y;

        return new Bounds(
            new Vector3(centerX, centerY, roomBounds.center.z),
            new Vector3(sizeX, sizeY, 0f));
    }

    /// <summary>True when the camera is pinned on both axes, i.e. the room fits on screen.</summary>
    public static bool IsFullyPinned(Bounds roomBounds, float orthographicSize, float aspect)
    {
        Vector2 view = GetViewSize(orthographicSize, aspect);
        return roomBounds.size.x <= view.x && roomBounds.size.y <= view.y;
    }

    /// <summary>
    /// Smallest orthographic size that fits the room on screen, for rooms that are meant to be
    /// shown in one frame. Used by the migration tool to report rooms that currently cannot
    /// contain the player (Col_112 and Col_109 in this project).
    /// </summary>
    public static float GetOrthographicSizeToFit(Bounds roomBounds, float aspect)
    {
        aspect = Mathf.Max(0.01f, aspect);
        return Mathf.Max(roomBounds.extents.y, roomBounds.extents.x / aspect);
    }
}
