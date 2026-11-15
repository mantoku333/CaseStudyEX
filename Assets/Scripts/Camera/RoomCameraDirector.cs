using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// The single authority for which room camera is active.
///
/// It replaces three competing mechanisms that used to fight each other:
///   1. <c>RoomCameraTrigger</c>'s static active-room state and per-frame re-validation
///   2. the portal hold that existed only to silence (1)
///   3. the manual pose blending inside the portal classes
///
/// This class only raises and lowers Cinemachine priorities. It never writes a camera
/// transform or lens, so all motion between rooms is produced by CinemachineBrain's blend
/// and each camera's own Confiner2D.
/// </summary>
[AddComponentMenu("Cinemachine/Room Camera Director")]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class RoomCameraDirector : MonoBehaviour
{
    private const float BakeBudgetSeconds = 1f / 120f;

    private static RoomCameraDirector instance;

    [Header("Detection")]
    [SerializeField, Tooltip("Tag used to find the player. The camera reference point is the player's \"CameraCol\" child.")]
    private string playerTag = "Player";

    [Header("Room Resolution")]
    [SerializeField, Tooltip(
        "World-unit margin the player must travel past the current room's edge before the room is " +
        "allowed to change. Stops the camera flipping back and forth inside the overlap bands " +
        "between neighbouring room colliders.")]
    [Min(0f)]
    private float exitMargin = 0.5f;

    [SerializeField, Tooltip(
        "How far inside a new room the player must be before that room takes the camera. " +
        "Together with Exit Margin this is what stops the camera flipping when the player walks " +
        "back and forth over a boundary. Scaled down automatically for rooms too thin to reach it.")]
    [Min(0f)]
    private float enterDepth = 3f;

    [SerializeField, Tooltip(
        "How long a return to the room just left is resisted. A deadband alone cannot stop a " +
        "determined player from oscillating across a boundary. Moving on to a different room is " +
        "never delayed by this, so fast falls are not affected.")]
    [Min(0f)]
    private float minHoldSeconds = 0.3f;

    [SerializeField, Tooltip(
        "Keep the last room active while the player is inside no room at all. Room colliders in this " +
        "project do not tile perfectly, and falling back to the follow camera in those gaps produced " +
        "a visible snap out and back.")]
    private bool keepLastRoomInGaps = true;

    [Header("Fallback")]
    [SerializeField, Tooltip("Used only before the first room is resolved, or when no room exists.")]
    private CinemachineCamera fallbackCamera;

    [SerializeField]
    private int fallbackActivePriority = 10;

    [SerializeField]
    private int fallbackInactivePriority = 0;

    [Header("Debug")]
    [SerializeField, Tooltip("Logs every room change.")]
    private bool logRoomChanges;

    private RoomCameraVolume activeRoom;
    private float lastChangeTime = float.NegativeInfinity;
    private RoomCameraVolume lastLeftRoom;
    private Transform cachedPlayer;
    private MonoBehaviour overrideSource;
    private RoomCameraVolume overrideRoom;

    public static RoomCameraDirector Instance => instance;
    public RoomCameraVolume ActiveRoom => activeRoom;

    /// <summary>Raised when the active room changes. Argument may be null.</summary>
    public static event System.Action<RoomCameraVolume> ActiveRoomChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        ActiveRoomChanged = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning(
                $"[RoomCameraDirector] A director already exists ({instance.name}). Destroying the duplicate on {name}.",
                this);
            Destroy(this);
            return;
        }

        instance = this;

        // Take the camera off RoomCameraTrigger, but leave the component in place: fog,
        // enemy activity and story events all use it as the room identity.
        RoomCameraTrigger.ExternalCameraAuthority = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            RoomCameraTrigger.ExternalCameraAuthority = false;
        }
    }

    private void Start()
    {
        // Resolve immediately so the first rendered frame already uses the correct room,
        // and force its confiner bake so the camera is confined on frame one.
        UpdateActiveRoom(forceBake: true);
    }

    private void LateUpdate()
    {
        UpdateActiveRoom(forceBake: false);
    }

    private void UpdateActiveRoom(bool forceBake)
    {
        // A boss encounter takes the brain over through CinemachineBrain.SetCameraOverride.
        // Room priorities are meaningless while that is in effect, so stay out of the way.
        if (EncounterCameraOwnership.IsActive)
        {
            return;
        }

        bool isOverride = TryGetOverrideRoom(out _);
        RoomCameraVolume next = ResolveRoom();
        if (next != activeRoom)
        {
            // An explicit request (early-switch trigger, teleport) is intentional, so it is
            // never throttled. Only geometric resolution is.
            //
            // Rooms that share one camera also bypass the throttle: the view does not change
            // when crossing between them, so there is nothing to rate-limit, and delaying the
            // bookkeeping would only make ActiveRoom lag behind the player.
            bool sameCamera = SharesCameraWithActive(next);

            // Only a return to the room just left is throttled. Blanket rate limiting would also
            // delay legitimate fast transits - falling runs at gravity scale 2, about 28
            // units/second against 7 while running, so a 0.3s hold would leave the camera a
            // third of the way into the room behind the player. Oscillation always means going
            // back where you came from, so that is the only case to resist.
            bool returningToPrevious = next != null && next == lastLeftRoom;
            if (isOverride || activeRoom == null || sameCamera || !returningToPrevious || HasHeldLongEnough())
            {
                lastLeftRoom = activeRoom;
                Commit(next);

                // Only an actual change of view restarts the throttle. Walking through a run of
                // rooms that share a camera would otherwise keep pushing the timer forward and
                // delay the one transition that does matter - leaving the run.
                if (!sameCamera)
                {
                    lastChangeTime = Time.unscaledTime;
                }

                forceBake = true;
            }
        }

        if (forceBake)
        {
            AdvanceConfinerBake(activeRoom);
        }
        else
        {
            // Baking is spread across frames by Cinemachine and the confiner has no effect
            // until it finishes, so keep feeding it until the active room is ready.
            AdvanceConfinerBakeIfPending(activeRoom);
        }
    }

    private RoomCameraVolume ResolveRoom()
    {
        if (TryGetOverrideRoom(out RoomCameraVolume requested))
        {
            return requested;
        }

        if (!TryGetPlayerPoint(out Vector3 point))
        {
            return activeRoom;
        }

        return Resolve(
            RoomCameraVolume.Registered,
            activeRoom,
            point,
            exitMargin,
            enterDepth,
            keepLastRoomInGaps);
    }

    /// <summary>
    /// Picks the room for a point. Pure, so the behaviour that actually matters is testable.
    ///
    /// Two rules replace <c>RoomCameraTrigger</c>'s "smallest room containing the point wins":
    ///
    ///   Hysteresis - the current room is kept until the player is <paramref name="exitMargin"/>
    ///   past its edge. The 58 room colliders in this scene overlap in 65 places, 8 of them with
    ///   under 5% area difference, so the old rule could hand the camera to a room the player had
    ///   not entered. Holding the current room makes those overlaps harmless.
    ///
    ///   Deepest inside wins - when the player is genuinely in several rooms at once, the one
    ///   whose edge is furthest away is chosen. Area was a poor tie-break here: Col_212 and
    ///   Col_213 differ by one square unit, so the winner was effectively arbitrary.
    ///
    /// <paramref name="enterDepth"/> adds the other half of the deadband. Exit margin alone left
    /// a flip band only twice its width: a player walking back and forth over a boundary crossed
    /// it every 0.14s at 7 units/second, and each crossing moved the view 38 units. Requiring the
    /// player to be properly inside a room before it takes over widens that band to
    /// exitMargin + enterDepth on each side, so wandering around a seam no longer changes the view.
    /// </summary>
    public static RoomCameraVolume Resolve(
        IReadOnlyList<RoomCameraVolume> volumes,
        RoomCameraVolume current,
        Vector3 point,
        float exitMargin,
        float enterDepth,
        bool keepLastRoomInGaps)
    {
        bool hasCurrent = current != null && current.isActiveAndEnabled && current.IsUsable;
        if (hasCurrent)
        {
            if (current.ContainsPoint(point) || current.GetDepthInside(point) > -Mathf.Max(0f, exitMargin))
            {
                return current;
            }
        }

        RoomCameraVolume best = null;
        float bestDepth = float.NegativeInfinity;
        if (volumes != null)
        {
            for (int i = 0; i < volumes.Count; i++)
            {
                RoomCameraVolume volume = volumes[i];
                if (volume == null || !volume.isActiveAndEnabled || !volume.IsUsable)
                {
                    continue;
                }

                if (!volume.ContainsPoint(point))
                {
                    continue;
                }

                float depth = volume.GetDepthInside(point);

                // With no room yet (scene start, or after a teleport) commit to whatever
                // contains the player; waiting for depth would leave the fallback camera up.
                if (hasCurrent)
                {
                    // A room thinner than 2 * enterDepth can never reach the fixed threshold,
                    // so scale the requirement to what the room can actually offer.
                    float required = Mathf.Min(Mathf.Max(0f, enterDepth), volume.GetMaxDepth() * 0.5f);
                    if (depth < required)
                    {
                        continue;
                    }
                }

                if (depth > bestDepth)
                {
                    bestDepth = depth;
                    best = volume;
                }
            }
        }

        if (best != null)
        {
            return best;
        }

        return keepLastRoomInGaps ? current : null;
    }

    /// <summary>
    /// True when the incoming room is framed by the same camera as the current one.
    ///
    /// Assigning one camera and one confiner shape to a whole run of adjoining rooms is the way
    /// to remove a seam entirely rather than merely damp it: the camera stays active across the
    /// crossing, so it keeps following the player and the view never jumps, no matter how often
    /// the player walks back and forth over the boundary.
    /// </summary>
    private bool SharesCameraWithActive(RoomCameraVolume next)
    {
        return next != null &&
            activeRoom != null &&
            next.RoomCamera != null &&
            next.RoomCamera == activeRoom.RoomCamera;
    }

    /// <summary>
    /// Unscaled time, so a room change still completes while dialogue has gameplay paused.
    /// </summary>
    private bool HasHeldLongEnough()
    {
        return Time.unscaledTime - lastChangeTime >= Mathf.Max(0f, minHoldSeconds);
    }

    private bool TryGetOverrideRoom(out RoomCameraVolume room)
    {
        bool usable =
            overrideSource != null &&
            overrideSource.isActiveAndEnabled &&
            overrideRoom != null &&
            overrideRoom.isActiveAndEnabled &&
            overrideRoom.IsUsable;

        if (!usable)
        {
            overrideSource = null;
            overrideRoom = null;
            room = null;
            return false;
        }

        room = overrideRoom;
        return true;
    }

    private void Commit(RoomCameraVolume next)
    {
        RoomCameraVolume previous = activeRoom;
        activeRoom = next;

        IReadOnlyList<RoomCameraVolume> volumes = RoomCameraVolume.Registered;
        for (int i = 0; i < volumes.Count; i++)
        {
            RoomCameraVolume volume = volumes[i];
            if (volume == null || volume == next)
            {
                continue;
            }

            // Several rooms may legitimately share one camera; lower everything first,
            // then raise the winner, so a shared camera ends up active rather than idle.
            volume.Deactivate();
        }

        if (next != null)
        {
            next.Activate();
            SetFallbackActive(false);
        }
        else
        {
            SetFallbackActive(true);
        }

        // Republish through RoomCameraTrigger so RoomFogRevealManager,
        // RoomEnemyActivityManager and StoryEventController keep receiving room changes
        // without any change on their side.
        RoomCameraTrigger.SetActiveRoomExternally(
            next != null ? next.GetComponent<RoomCameraTrigger>() : null);

        if (logRoomChanges)
        {
            string from = previous != null ? previous.name : "(none)";
            string to = next != null ? next.name : "(fallback)";
            Debug.Log($"[RoomCameraDirector] {from} -> {to}");
        }

        ActiveRoomChanged?.Invoke(next);
    }

    private void SetFallbackActive(bool active)
    {
        if (fallbackCamera == null)
        {
            return;
        }

        fallbackCamera.Priority.Value = active ? fallbackActivePriority : fallbackInactivePriority;
        fallbackCamera.Priority.Enabled = true;
    }

    private static void AdvanceConfinerBake(RoomCameraVolume room)
    {
        if (room == null || room.RoomCamera == null)
        {
            return;
        }

        CinemachineConfiner2D confiner = room.RoomCamera.GetComponent<CinemachineConfiner2D>();
        if (confiner == null)
        {
            return;
        }

        confiner.BakeBoundingShape(room.RoomCamera, BakeBudgetSeconds);
    }

    private static void AdvanceConfinerBakeIfPending(RoomCameraVolume room)
    {
        if (room == null || room.RoomCamera == null)
        {
            return;
        }

        CinemachineConfiner2D confiner = room.RoomCamera.GetComponent<CinemachineConfiner2D>();
        if (confiner == null || confiner.BoundingShapeIsBaked)
        {
            return;
        }

        confiner.BakeBoundingShape(room.RoomCamera, BakeBudgetSeconds);
    }

    private bool TryGetPlayerPoint(out Vector3 point)
    {
        if (PlayerCameraColliderUtility.TryGetCameraPointFromPlayerTag(playerTag, out point))
        {
            return true;
        }

        // Fall back to the player transform when the "CameraCol" child is missing.
        if (cachedPlayer == null)
        {
            cachedPlayer = FindPlayer();
        }

        if (cachedPlayer == null)
        {
            point = default;
            return false;
        }

        point = cachedPlayer.position;
        return true;
    }

    private Transform FindPlayer()
    {
        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return null;
        }

        try
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            return player != null ? player.transform : null;
        }
        catch (UnityException)
        {
            return null;
        }
    }

    /// <summary>
    /// Forces a specific room to stay active while <paramref name="source"/> is enabled.
    /// Used by <see cref="RoomCameraEarlySwitch"/> to switch before the player crosses a seam.
    /// The hold is dropped automatically when the source is disabled or destroyed.
    /// </summary>
    public static bool RequestRoom(MonoBehaviour source, RoomCameraVolume room)
    {
        if (instance == null || source == null || room == null || !room.IsUsable)
        {
            return false;
        }

        instance.overrideSource = source;
        instance.overrideRoom = room;
        return true;
    }

    public static void ReleaseRoom(MonoBehaviour source)
    {
        if (instance == null || source == null || instance.overrideSource != source)
        {
            return;
        }

        instance.overrideSource = null;
        instance.overrideRoom = null;
    }

#if UNITY_EDITOR
    // Public for the migration tool, which lives in the editor assembly.
    public void EditorAssignFallback(CinemachineCamera camera)
    {
        fallbackCamera = camera;
    }
#endif
}
