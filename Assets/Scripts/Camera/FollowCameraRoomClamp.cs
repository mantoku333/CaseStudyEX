using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Keeps the follow camera inside the room it is in.
///
/// This is the piece the project was missing. The design here is already two modes, and both
/// are deliberate:
///   - a small room gets its own static camera and shows the whole room
///   - a large room uses the shared follow camera, zoomed out (five rooms override it to 16)
///     and tracking the player
/// The second mode had no bounds at all, which is why large rooms show the darkness past their
/// edges, why the player's place in frame drifts there, and why the long halls needed a separate
/// hand-written follow with its own clamping.
///
/// The rule is per-axis and needs no per-room setup:
///
///   room size on an axis &lt;= view size  -> pin to the room's centre on that axis
///   room size on an axis &gt;  view size  -> track the player, stop at the room's edge
///
/// That single rule reproduces all three behaviours the project hand-assigns today. A hall
/// (163 x 20.3 against a 42.7 x 24 view) pins vertically and tracks horizontally, which is
/// exactly what the hand-written horizontal follow did. A shaft (9 x 20.6) pins horizontally.
/// A room that fits pins on both axes and looks like a static camera.
///
/// The clamp rectangle itself is what blends on a room change. Snapping it instead is the
/// documented way to make a following camera jerk, because the camera is yanked to the new
/// limits in one frame; moving the limits over a short time turns that into the transition.
/// </summary>
[AddComponentMenu("Cinemachine/Follow Camera Room Clamp")]
[DisallowMultipleComponent]
public sealed class FollowCameraRoomClamp : CinemachineExtension
{
    [SerializeField, Min(0f), Tooltip(
        "0 = the limits change with the room in the same frame, so a room change is a cut like " +
        "every other area change. A value above 0 slides the limits instead; measured in play, " +
        "0.25s moved the view about 250px in 0.13s, which reads as a lurch. Leave at 0.")]
    private float boundsBlendSeconds = 0f;

    [SerializeField, Min(0f), Tooltip(
        "Extra margin kept inside the room edge. 0 puts the screen edge exactly on the room edge.")]
    private float edgePadding;

    private const float DefaultSideInset = 0.2f;
    private const float DefaultTopInset = 0.3f;
    private const float DefaultBottomInset = 0.12f;
    private const float DefaultPinnedInset = 0.04f;

    [Header("Player visibility (takes priority over the room edge)")]
    [SerializeField, Range(0f, 0.45f), Tooltip(
        "The player is never allowed closer than this to the left or right screen edge, as a " +
        "fraction of the screen width. If keeping the camera inside the room would break it, the " +
        "camera leaves the room instead.")]
    private float sideInset = DefaultSideInset;

    [SerializeField, Range(0f, 0.45f), Tooltip(
        "Same, for the top of the screen. Larger than the others because the HUD sits there.")]
    private float topInset = DefaultTopInset;

    [SerializeField, Range(0f, 0.45f), Tooltip(
        "Same, for the bottom of the screen. Kept below the composer's own framing, which " +
        "places the player about 20% above the bottom edge.")]
    private float bottomInset = DefaultBottomInset;

    [SerializeField, Range(0f, 0.2f), Tooltip(
        "Inset used on an axis where the room fits on screen. Small, so a room that is shown " +
        "whole stays still and only moves if the player would actually leave the picture.")]
    private float pinnedInset = DefaultPinnedInset;

    [SerializeField, Tooltip("Draw the active clamp rectangle in the scene view.")]
    private bool drawGizmo = true;

    private RoomCameraTrigger room;
    private Rect targetRect;
    private Rect sourceRect;
    private bool hasRect;
    private bool blending;
    private float blendStartedAt;

    protected override void OnEnable()
    {
        base.OnEnable();
        RoomCameraTrigger.ActiveRoomChanged += HandleActiveRoomChanged;
        AdoptRoom(RoomCameraTrigger.ActiveRoom, snap: true);
    }

    // CinemachineExtension declares no virtual OnDisable, so this is a plain Unity callback
    // rather than an override.
    private void OnDisable()
    {
        RoomCameraTrigger.ActiveRoomChanged -= HandleActiveRoomChanged;
    }

    private void HandleActiveRoomChanged(RoomCameraTrigger next)
    {
        // Snap while something else is on screen: the cut is the transition in that case, and
        // blending the limits as well would add a slide on top of it.
        CinemachineVirtualCameraBase owner = ComponentOwner;
        bool live = owner != null && owner.IsLive;
        AdoptRoom(next, snap: !live);
    }

    private void AdoptRoom(RoomCameraTrigger next, bool snap)
    {
        room = next;
        if (room == null || !room.TryGetAreaBounds(out Bounds bounds))
        {
            // No room resolved: leave the camera unconstrained rather than clamping to nothing.
            hasRect = false;
            blending = false;
            return;
        }

        Rect rect = new Rect(
            bounds.min.x, bounds.min.y,
            Mathf.Max(0f, bounds.size.x), Mathf.Max(0f, bounds.size.y));

        if (!hasRect || snap || boundsBlendSeconds <= 0.001f)
        {
            sourceRect = rect;
            targetRect = rect;
            hasRect = true;
            blending = false;
            return;
        }

        sourceRect = CurrentRect();
        targetRect = rect;
        blending = true;
        blendStartedAt = Time.unscaledTime;
    }

    private Rect CurrentRect()
    {
        if (!blending)
        {
            return targetRect;
        }

        float t = Mathf.Clamp01((Time.unscaledTime - blendStartedAt) / boundsBlendSeconds);
        float k = Mathf.SmoothStep(0f, 1f, t);
        if (t >= 1f)
        {
            blending = false;
            return targetRect;
        }

        return new Rect(
            Mathf.Lerp(sourceRect.xMin, targetRect.xMin, k),
            Mathf.Lerp(sourceRect.yMin, targetRect.yMin, k),
            Mathf.Lerp(sourceRect.width, targetRect.width, k),
            Mathf.Lerp(sourceRect.height, targetRect.height, k));
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || !hasRect)
        {
            return;
        }

        float halfHeight = state.Lens.OrthographicSize;
        if (halfHeight <= 0f)
        {
            return;
        }

        float aspect = state.Lens.Aspect > 0.01f ? state.Lens.Aspect : RoomCameraBoundsUtility.DefaultAspect;
        float halfWidth = halfHeight * aspect;

        Rect rect = CurrentRect();
        Vector3 position = state.GetCorrectedPosition();

        float x = Resolve(position.x, rect.xMin, rect.xMax, rect.width, halfWidth + edgePadding);
        float y = Resolve(position.y, rect.yMin, rect.yMax, rect.height, halfHeight + edgePadding);

        // Seeing the player matters more than staying inside the room. With the room edge as a
        // hard limit, a player standing near the ceiling or a wall of a large area ended up at
        // the very edge of the screen, under the HUD, for as long as they stayed there - and a
        // player who stepped just outside the rectangle could not be followed at all. Whatever
        // lies past the room edge is fogged anyway, so showing a little of it costs nothing.
        if (TryGetPlayerPosition(vcam, out Vector3 player))
        {
            bool pinnedX = rect.width <= (halfWidth + edgePadding) * 2f;
            bool pinnedY = rect.height <= (halfHeight + edgePadding) * 2f;
            float insetX = pinnedX ? pinnedInset : sideInset;
            float insetTop = pinnedY ? pinnedInset : topInset;
            float insetBottom = pinnedY ? pinnedInset : bottomInset;

            x = KeepInView(x, player.x, halfWidth, insetX, insetX);
            y = KeepInView(y, player.y, halfHeight, insetBottom, insetTop);
        }

        state.PositionCorrection += new Vector3(x - position.x, y - position.y, 0f);
    }

    /// <summary>
    /// Moves the camera only as far as needed for the player to sit inside the insets.
    /// Insets are fractions of the full screen size on that axis.
    /// </summary>
    private static float KeepInView(float camera, float player, float halfView, float insetLow, float insetHigh)
    {
        float min = player - halfView * (1f - 2f * insetHigh);
        float max = player + halfView * (1f - 2f * insetLow);
        return min <= max ? Mathf.Clamp(camera, min, max) : player;
    }

    private static bool TryGetPlayerPosition(CinemachineVirtualCameraBase vcam, out Vector3 position)
    {
        Transform target = ResolvePlayer(vcam != null ? vcam.Follow : null);
        if (target == null)
        {
            position = default;
            return false;
        }

        position = target.position;
        return true;
    }

    /// <summary>
    /// One axis of the rule: pin to the centre when the room cannot fill the screen, otherwise
    /// follow and stop at the edge.
    /// </summary>
    private static float Resolve(float value, float min, float max, float size, float halfView)
    {
        if (size <= halfView * 2f)
        {
            return (min + max) * 0.5f;
        }

        return Mathf.Clamp(value, min + halfView, max - halfView);
    }

    /// <summary>
    /// The same rule as the live clamp, for code that has to know in advance where the follow
    /// camera will sit in a room - the story event camera hands the view back to it, and has to
    /// aim at the clamped pose or the view jumps a second time at the hand-off.
    /// </summary>
    public static Vector3 ClampToRoom(
        Vector3 position,
        Bounds room,
        float orthographicSize,
        float aspect,
        Transform trackingTarget = null)
    {
        float halfHeight = Mathf.Max(0f, orthographicSize);
        float halfWidth = halfHeight * Mathf.Max(0.01f, aspect);
        position.x = Resolve(position.x, room.min.x, room.max.x, room.size.x, halfWidth);
        position.y = Resolve(position.y, room.min.y, room.max.y, room.size.y, halfHeight);

        Transform player = ResolvePlayer(trackingTarget);
        if (player != null)
        {
            bool pinnedX = room.size.x <= halfWidth * 2f;
            bool pinnedY = room.size.y <= halfHeight * 2f;
            float insetX = pinnedX ? DefaultPinnedInset : DefaultSideInset;
            position.x = KeepInView(position.x, player.position.x, halfWidth, insetX, insetX);
            position.y = KeepInView(
                position.y,
                player.position.y,
                halfHeight,
                pinnedY ? DefaultPinnedInset : DefaultBottomInset,
                pinnedY ? DefaultPinnedInset : DefaultTopInset);
        }

        return position;
    }

    /// <summary>
    /// FollowCameraFacingBias points the camera at a look-ahead proxy parented to the real
    /// target and slides it below the player while falling. The player is the parent.
    /// </summary>
    private static Transform ResolvePlayer(Transform trackingTarget)
    {
        if (trackingTarget != null &&
            trackingTarget.parent != null &&
            trackingTarget.name == "FollowCameraDownwardLookAheadTarget")
        {
            return trackingTarget.parent;
        }

        return trackingTarget;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmo || !hasRect)
        {
            return;
        }

        Rect rect = CurrentRect();
        Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.9f);
        Gizmos.DrawWireCube(
            new Vector3(rect.center.x, rect.center.y, transform.position.z),
            new Vector3(rect.width, rect.height, 0f));
    }
#endif
}
