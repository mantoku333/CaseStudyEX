using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[AddComponentMenu("Environment/Room Fog Reveal Manager")]
public sealed class RoomFogRevealManager : MonoBehaviour, ISaveDataModule
{
    private const string ShaderName = "CaseStudy/RoomFogOverlay";
    private const float PlayerRoomRefreshInterval = 0.2f;
    private const float PreviewRevealGraceDuration = 0.25f;

    private static RoomFogRevealManager instance;

    [Header("Mask")]
    [SerializeField] private bool fogEnabled = true;
    [SerializeField, Min(64)] private int textureResolution = 2048;
    [SerializeField] private bool useColliderShape = true;
    [SerializeField, Min(0f)] private float worldPadding = 6f;
    [SerializeField, Min(0f)] private float revealPaddingX = 2f;
    [SerializeField, Min(0f)] private float revealPaddingY = 2f;

    [Header("Fog Look")]
    [SerializeField] private Color fogColor = new Color(0f, 0f, 0f, 0.92f);
    [SerializeField, Range(0f, 1f)] private float fogAlpha = 1f;
    [SerializeField, Range(0.01f, 1f)] private float edgeSoftness = 0.22f;
    [SerializeField, Range(0f, 1f)] private float noiseStrength = 0.18f;
    [SerializeField, Min(0.1f)] private float noiseScale = 0.32f;
    [SerializeField] private int sortingOrder = 30000;
    [SerializeField] private float overlayZ = -1f;

    [Header("Reveal Animation")]
    [SerializeField, Min(0.01f)] private float revealDuration = 2.55f;
    [SerializeField, Range(0f, 0.5f)] private float revealNoiseStrength = 0.18f;

    [Header("Portal Entrance Reveal")]
    [SerializeField] private bool revealPortalEntrances = true;
    [SerializeField, Min(0f)] private float portalEntranceDepth = 2.5f;
    [SerializeField, Min(0f)] private float portalEntranceWidthPadding = 0.35f;
    [SerializeField, Min(0f)] private float portalEntranceEdgeNoise = 0.15f;
    [SerializeField, Min(0.01f)] private float portalEntranceNoiseScale = 1.2f;

    private readonly List<RoomCameraTrigger> rooms = new List<RoomCameraTrigger>();
    private readonly Dictionary<RoomCameraTrigger, string> roomIds = new Dictionary<RoomCameraTrigger, string>();
    private readonly HashSet<string> visibleRoomIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> pendingRoomIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<PendingReveal> pendingReveals = new List<PendingReveal>();
    private readonly Dictionary<string, float> previewRoomExpirations = new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly List<PortalOpening> portalOpenings = new List<PortalOpening>();
    private PortalOpening retainedArrivalOpening;
    private RoomCameraTrigger retainedArrivalSourceRoom;
    private RoomCameraTrigger retainedArrivalRoom;
    private bool hasRetainedArrivalOpening;
    private RoomCameraTrigger currentRoom;

    private Scene managedScene;
    private Bounds worldBounds;
    private Texture2D maskTexture;
    private Color32[] maskPixels;
    private Texture2D entranceMaskTexture;
    private Color32[] entranceMaskPixels;
    private Material fogMaterial;
    private GameObject overlayObject;
    private Mesh overlayMesh;
    private MeshRenderer overlayRenderer;
    private MeshFilter overlayFilter;
    private float nextPlayerRoomRefreshTime;
    private bool hasRooms;
    private bool shaderWarningLogged;

    public int Priority => 260;

    public static bool FogEnabled => instance != null && instance.fogEnabled;

    private struct PendingReveal
    {
        public RoomCameraTrigger Room;
        public string RoomId;
        public bool Revealing;
        public Bounds OriginalBounds;
        public Bounds RevealBounds;
        public Vector2 Center;
        public float Radius;
        public float StartedAt;
    }

    private struct PortalOpening
    {
        public GameObject PortalObject;
        public RoomCameraTrigger RoomA;
        public RoomCameraTrigger RoomB;
        public PortalOpeningAxis Axis;
        public bool AllowAToB;
        public bool AllowBToA;
    }

    private enum PortalOpeningAxis
    {
        Horizontal,
        Vertical
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RuntimeInitialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        RefreshExistingManagerForCurrentScene(true);
    }

    public static bool RevealRoom(RoomCameraTrigger room)
    {
        if (room == null)
        {
            return false;
        }

        return TryGetInstance(out RoomFogRevealManager manager) &&
            manager.Reveal(room);
    }

    public static void SetFogEnabled(bool enabled)
    {
        if (!TryGetInstance(out RoomFogRevealManager manager))
        {
            Debug.LogWarning("[RoomFogRevealManager] No manager exists in the active scene.");
            return;
        }

        manager.fogEnabled = enabled;
        manager.SetOverlayVisible(manager.hasRooms);
    }

    private static bool TryGetInstance(out RoomFogRevealManager manager)
    {
        if (instance != null)
        {
            manager = instance;
            return true;
        }

        RoomFogRevealManager existing = FindFirstObjectByType<RoomFogRevealManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            manager = instance;
            return true;
        }

        manager = null;
        return false;
    }

    private static void RefreshExistingManagerForCurrentScene(bool revealCurrentRoom)
    {
        if (TryGetInstance(out RoomFogRevealManager manager))
        {
            manager.RefreshForCurrentScene(revealCurrentRoom);
        }
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshExistingManagerForCurrentScene(true);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnEnable()
    {
        SaveManager.RegisterModule(this);
        RoomCameraTrigger.ActiveRoomChanged += HandleActiveRoomChanged;
        RefreshForCurrentScene(true);
    }

    private void OnDisable()
    {
        SaveManager.UnregisterModule(this);
        RoomCameraTrigger.ActiveRoomChanged -= HandleActiveRoomChanged;
        SetOverlayVisible(false);
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        instance = null;
        DestroyOverlayResources();
    }

    private void Update()
    {
        if (!managedScene.IsValid() ||
            managedScene != SceneManager.GetActiveScene())
        {
            RefreshForCurrentScene(true);
        }

        if (!hasRooms)
        {
            return;
        }

        ProcessPendingReveals();
        ProcessPreviewExpirations();

        if (Time.unscaledTime < nextPlayerRoomRefreshTime)
        {
            return;
        }

        nextPlayerRoomRefreshTime = Time.unscaledTime + PlayerRoomRefreshInterval;
        RevealCurrentRoomFromRuntimeState();
    }

    public void Capture(SaveGameData saveData)
    {
        // Room fog visibility is transient and follows only the current room.
    }

    public void Restore(SaveGameData saveData)
    {
        RebuildMaskForCurrentRoom(true);
    }

    private void RefreshForCurrentScene(bool revealCurrentRoom)
    {
        managedScene = SceneManager.GetActiveScene();
        rooms.Clear();
        roomIds.Clear();
        visibleRoomIds.Clear();
        pendingRoomIds.Clear();
        pendingReveals.Clear();
        previewRoomExpirations.Clear();
        portalOpenings.Clear();
        hasRetainedArrivalOpening = false;
        retainedArrivalSourceRoom = null;
        retainedArrivalRoom = null;
        currentRoom = null;
        hasRooms = false;

        RoomCameraTrigger[] allRooms = FindObjectsByType<RoomCameraTrigger>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        bool hasBounds = false;
        worldBounds = default;

        for (int i = 0; i < allRooms.Length; i++)
        {
            RoomCameraTrigger room = allRooms[i];
            if (room == null ||
                room.gameObject.scene != managedScene ||
                !room.TryGetAreaBounds(out Bounds roomBounds))
            {
                continue;
            }

            rooms.Add(room);
            roomIds[room] = CreateRoomId(room);

            if (!hasBounds)
            {
                worldBounds = roomBounds;
                hasBounds = true;
            }
            else
            {
                worldBounds.Encapsulate(roomBounds);
            }

        }

        if (!hasBounds || rooms.Count == 0)
        {
            SetOverlayVisible(false);
            return;
        }

        worldBounds.Expand(worldPadding * 2f);
        hasRooms = true;

        EnsureMaskTexture();
        ClearMaskPixels();
        EnsureOverlay();
        ApplyMaterialProperties();
        ConfigurePortalRevealTriggers();
        RebuildMaskForCurrentRoom(revealCurrentRoom);
    }

    private void RebuildMaskForCurrentRoom(bool revealCurrentRoom)
    {
        if (!hasRooms || maskPixels == null)
        {
            return;
        }

        visibleRoomIds.Clear();
        pendingRoomIds.Clear();
        pendingReveals.Clear();
        previewRoomExpirations.Clear();
        currentRoom = null;
        ClearMaskPixels();

        ApplyMaskTexture();
        RefreshPortalEntranceMask();
        SetOverlayVisible(true);

        if (revealCurrentRoom)
        {
            RevealCurrentRoomFromRuntimeState();
        }
    }

    private void HandleActiveRoomChanged(RoomCameraTrigger activeRoom)
    {
        // Camera portals can switch ActiveRoom before the player has physically
        // crossed the boundary. Resolve through the same containment-first path
        // used by polling so the two signals cannot repeatedly reverse a reveal.
        RevealCurrentRoomFromRuntimeState();
    }

    private bool Reveal(RoomCameraTrigger room)
    {
        if (room == null)
        {
            return false;
        }

        if (!hasRooms || room.gameObject.scene != managedScene)
        {
            RefreshForCurrentScene(false);
        }

        if (!roomIds.TryGetValue(room, out string roomId))
        {
            return false;
        }

        if (ShouldRoomBeCurrent(room))
        {
            SetCurrentRoom(room);
            return true;
        }

        RevealPreviewRoom(room, roomId);
        return true;
    }

    private void SetCurrentRoom(RoomCameraTrigger room)
    {
        if (room == null || room.gameObject.scene != managedScene)
        {
            return;
        }

        if (!roomIds.TryGetValue(room, out string roomId))
        {
            return;
        }

        bool targetIsConcealing = HasPendingTransition(roomId, false);
        bool targetAlreadyVisibleOrOpening =
            !targetIsConcealing &&
            (visibleRoomIds.Contains(roomId) ||
             HasPendingTransition(roomId, true));
        bool targetFullyVisible =
            !targetIsConcealing &&
            visibleRoomIds.Contains(roomId);

        if (currentRoom == room && targetAlreadyVisibleOrOpening)
        {
            return;
        }

        RoomCameraTrigger previousRoom = currentRoom;
        RetainArrivalOpening(previousRoom, room, targetFullyVisible);
        currentRoom = room;
        previewRoomExpirations.Remove(roomId);
        RefreshPortalEntranceMask();

        if (previousRoom != null &&
            previousRoom != room &&
            previousRoom.gameObject.scene == managedScene &&
            roomIds.TryGetValue(previousRoom, out string previousRoomId))
        {
            BeginTransition(previousRoom, previousRoomId, false);
        }

        // A preview can expire just before the room becomes current. In that
        // case visibleRoomIds still contains the room while its mask is being
        // concealed, so the conceal must be replaced with a reveal.
        if (targetIsConcealing || !targetAlreadyVisibleOrOpening)
        {
            BeginTransition(room, roomId, true);
        }
    }

    private void RevealPreviewRoom(RoomCameraTrigger room, string roomId)
    {
        previewRoomExpirations[roomId] = Time.unscaledTime + PreviewRevealGraceDuration;

        if (visibleRoomIds.Contains(roomId) || HasPendingTransition(roomId, true))
        {
            return;
        }

        BeginTransition(room, roomId, true);
    }

    private void ClearCurrentRoom()
    {
        if (currentRoom == null)
        {
            return;
        }

        RoomCameraTrigger previousRoom = currentRoom;
        currentRoom = null;
        hasRetainedArrivalOpening = false;
        retainedArrivalSourceRoom = null;
        retainedArrivalRoom = null;
        RefreshPortalEntranceMask();
        if (previousRoom.gameObject.scene == managedScene &&
            roomIds.TryGetValue(previousRoom, out string previousRoomId))
        {
            BeginTransition(previousRoom, previousRoomId, false);
        }
    }

    private bool ShouldRoomBeCurrent(RoomCameraTrigger room)
    {
        RoomCameraTrigger activeRoom = RoomCameraTrigger.ActiveRoom;
        if (TryGetPlayerPosition(out Vector3 playerPosition))
        {
            RoomCameraTrigger containingRoom = ResolveSmallestRoomContaining(playerPosition);
            if (containingRoom != null)
            {
                return containingRoom == room;
            }
        }

        // In a portal gap the player may temporarily belong to neither room.
        return activeRoom == room;
    }

    private void BeginTransition(RoomCameraTrigger room, string roomId, bool revealing)
    {
        if (room == null ||
            string.IsNullOrEmpty(roomId) ||
            !room.TryGetAreaBounds(out Bounds roomBounds))
        {
            return;
        }

        CancelPendingTransition(roomId);

        Vector2 center = new Vector2(roomBounds.center.x, roomBounds.center.y);
        if (TryGetPlayerPosition(out Vector3 playerPosition))
        {
            center = new Vector2(playerPosition.x, playerPosition.y);
        }

        Bounds revealBounds = CreateRevealBounds(roomBounds);
        PendingReveal pendingReveal = new PendingReveal
        {
            Room = room,
            RoomId = roomId,
            Revealing = revealing,
            OriginalBounds = roomBounds,
            RevealBounds = revealBounds,
            Center = center,
            Radius = ResolveRevealRadius(center, revealBounds),
            StartedAt = Time.unscaledTime
        };

        pendingRoomIds.Add(roomId);
        pendingReveals.Add(pendingReveal);
        ProcessPendingReveals();
    }

    private void CancelPendingTransition(string roomId)
    {
        pendingRoomIds.Remove(roomId);
        for (int i = pendingReveals.Count - 1; i >= 0; i--)
        {
            if (string.Equals(pendingReveals[i].RoomId, roomId, StringComparison.Ordinal))
            {
                pendingReveals.RemoveAt(i);
            }
        }
    }

    private bool HasPendingTransition(string roomId, bool revealing)
    {
        for (int i = 0; i < pendingReveals.Count; i++)
        {
            PendingReveal pendingReveal = pendingReveals[i];
            if (pendingReveal.Revealing == revealing &&
                string.Equals(pendingReveal.RoomId, roomId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void ProcessPendingReveals()
    {
        if (pendingReveals.Count == 0 || maskTexture == null || maskPixels == null)
        {
            return;
        }

        bool maskChanged = false;
        for (int i = pendingReveals.Count - 1; i >= 0; i--)
        {
            if (!pendingReveals[i].Revealing)
            {
                maskChanged |= ProcessPendingRevealAt(i);
            }
        }

        for (int i = pendingReveals.Count - 1; i >= 0; i--)
        {
            if (pendingReveals[i].Revealing)
            {
                maskChanged |= ProcessPendingRevealAt(i);
            }
        }

        if (maskChanged)
        {
            ApplyMaskTexture();
        }
    }

    private void ProcessPreviewExpirations()
    {
        if (previewRoomExpirations.Count == 0)
        {
            return;
        }

        float now = Time.unscaledTime;
        RoomCameraTrigger activeRoom = RoomCameraTrigger.ActiveRoom;
        bool hasPlayerPosition = TryGetPlayerPosition(out Vector3 playerPosition);
        RoomCameraTrigger containingRoom = hasPlayerPosition
            ? ResolveSmallestRoomContaining(playerPosition)
            : null;

        for (int i = rooms.Count - 1; i >= 0; i--)
        {
            RoomCameraTrigger room = rooms[i];
            if (room == null ||
                !roomIds.TryGetValue(room, out string roomId) ||
                !previewRoomExpirations.TryGetValue(roomId, out float expiresAt) ||
                expiresAt > now)
            {
                continue;
            }

            previewRoomExpirations.Remove(roomId);

            // Leaving the portal ends preview refresh. If the player has
            // already entered this room, promote it instead of cancelling a
            // reveal that is still in progress.
            if (room == containingRoom)
            {
                SetCurrentRoom(room);
                continue;
            }

            if (room == currentRoom || room == activeRoom)
            {
                continue;
            }

            if (visibleRoomIds.Contains(roomId) || HasPendingTransition(roomId, true))
            {
                BeginTransition(room, roomId, false);
            }
        }
    }

    private bool ProcessPendingRevealAt(int index)
    {
        PendingReveal reveal = pendingReveals[index];
        if (reveal.Room == null)
        {
            pendingRoomIds.Remove(reveal.RoomId);
            pendingReveals.RemoveAt(index);
            return false;
        }

        float rawProgress = Mathf.Clamp01((Time.unscaledTime - reveal.StartedAt) / revealDuration);
        float progress = Mathf.SmoothStep(0f, 1f, rawProgress);

        if (progress >= 1f)
        {
            if (reveal.Revealing)
            {
                PaintRoom(reveal.Room, reveal.OriginalBounds, reveal.RevealBounds);
                visibleRoomIds.Add(reveal.RoomId);
                ReleaseArrivalOpeningWhenCovered(reveal.Room);
            }
            else
            {
                PaintRoomHidden(reveal.Room, reveal.OriginalBounds, reveal.RevealBounds);
                visibleRoomIds.Remove(reveal.RoomId);
            }

            pendingRoomIds.Remove(reveal.RoomId);
            pendingReveals.RemoveAt(index);
            return true;
        }

        if (reveal.Revealing)
        {
            PaintRoomRevealProgress(reveal, progress);
        }
        else
        {
            PaintRoomConcealProgress(reveal, progress);
        }

        return true;
    }

    private void RevealCurrentRoomFromRuntimeState()
    {
        RoomCameraTrigger activeRoom = RoomCameraTrigger.ActiveRoom;
        bool activeRoomIsValid =
            activeRoom != null &&
            activeRoom.gameObject.scene == managedScene;
        bool hasPlayerPosition = TryGetPlayerPosition(out Vector3 playerPosition);

        if (hasPlayerPosition)
        {
            RoomCameraTrigger containingRoom = ResolveSmallestRoomContaining(playerPosition);
            if (containingRoom != null)
            {
                SetCurrentRoom(containingRoom);
                return;
            }
        }

        // Portal gaps may briefly belong to neither room. Preserve the camera
        // room only when containment cannot determine the physical room.
        if (activeRoomIsValid)
        {
            SetCurrentRoom(activeRoom);
            return;
        }

        ClearCurrentRoom();
    }

    private RoomCameraTrigger ResolveSmallestRoomContaining(Vector3 worldPosition)
    {
        RoomCameraTrigger bestRoom = null;
        float bestArea = float.PositiveInfinity;

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomCameraTrigger room = rooms[i];
            if (room == null || !room.ContainsPoint(worldPosition))
            {
                continue;
            }

            if (!room.TryGetAreaBounds(out Bounds bounds))
            {
                return room;
            }

            float area = bounds.size.x * bounds.size.y;
            if (area < bestArea)
            {
                bestArea = area;
                bestRoom = room;
            }
        }

        return bestRoom;
    }

    private void EnsureMaskTexture()
    {
        int resolution = Mathf.Max(64, textureResolution);
        if (maskTexture != null &&
            maskTexture.width == resolution &&
            maskTexture.height == resolution &&
            entranceMaskTexture != null &&
            entranceMaskTexture.width == resolution &&
            entranceMaskTexture.height == resolution)
        {
            return;
        }

        if (maskTexture != null)
        {
            Destroy(maskTexture);
        }

        if (entranceMaskTexture != null)
        {
            Destroy(entranceMaskTexture);
        }

        maskTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
        {
            name = "RoomFogRevealMask",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        maskPixels = new Color32[resolution * resolution];
        entranceMaskTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
        {
            name = "RoomFogPortalEntranceMask",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        entranceMaskPixels = new Color32[resolution * resolution];
    }

    private void ClearMaskPixels()
    {
        if (maskPixels == null)
        {
            return;
        }

        Color32 hidden = new Color32(0, 0, 0, 255);
        for (int i = 0; i < maskPixels.Length; i++)
        {
            maskPixels[i] = hidden;
        }
    }

    private void RefreshPortalEntranceMask()
    {
        if (entranceMaskTexture == null || entranceMaskPixels == null)
        {
            return;
        }

        Color32 hidden = new Color32(0, 0, 0, 255);
        for (int i = 0; i < entranceMaskPixels.Length; i++)
        {
            entranceMaskPixels[i] = hidden;
        }

        if (revealPortalEntrances && currentRoom != null && portalEntranceDepth > 0f)
        {
            if (hasRetainedArrivalOpening && retainedArrivalRoom == currentRoom)
            {
                PaintPortalEntrance(
                    retainedArrivalOpening,
                    retainedArrivalSourceRoom,
                    retainedArrivalRoom);
            }

            for (int i = 0; i < portalOpenings.Count; i++)
            {
                PortalOpening opening = portalOpenings[i];
                if (opening.RoomA == currentRoom && opening.AllowAToB)
                {
                    PaintPortalEntrance(opening, currentRoom, opening.RoomB);
                }
                else if (opening.RoomB == currentRoom && opening.AllowBToA)
                {
                    PaintPortalEntrance(opening, currentRoom, opening.RoomA);
                }
            }
        }

        entranceMaskTexture.SetPixels32(entranceMaskPixels);
        entranceMaskTexture.Apply(false, false);
    }

    private void RetainArrivalOpening(
        RoomCameraTrigger previousRoom,
        RoomCameraTrigger destinationRoom,
        bool destinationAlreadyCovered)
    {
        hasRetainedArrivalOpening = false;
        retainedArrivalSourceRoom = null;
        retainedArrivalRoom = null;

        if (previousRoom == null ||
            destinationRoom == null ||
            previousRoom == destinationRoom ||
            destinationAlreadyCovered)
        {
            return;
        }

        for (int i = 0; i < portalOpenings.Count; i++)
        {
            PortalOpening opening = portalOpenings[i];
            bool connectsForward =
                opening.RoomA == previousRoom &&
                opening.RoomB == destinationRoom &&
                opening.AllowAToB;
            bool connectsBackward =
                opening.RoomB == previousRoom &&
                opening.RoomA == destinationRoom &&
                opening.AllowBToA;
            if (!connectsForward && !connectsBackward)
            {
                continue;
            }

            retainedArrivalOpening = opening;
            retainedArrivalSourceRoom = previousRoom;
            retainedArrivalRoom = destinationRoom;
            hasRetainedArrivalOpening = true;
            return;
        }
    }

    private void ReleaseArrivalOpeningWhenCovered(RoomCameraTrigger revealedRoom)
    {
        if (!hasRetainedArrivalOpening || retainedArrivalRoom != revealedRoom)
        {
            return;
        }

        hasRetainedArrivalOpening = false;
        retainedArrivalSourceRoom = null;
        retainedArrivalRoom = null;
        RefreshPortalEntranceMask();
    }

    private void PaintPortalEntrance(
        PortalOpening opening,
        RoomCameraTrigger sourceRoom,
        RoomCameraTrigger destinationRoom)
    {
        if (opening.PortalObject == null ||
            sourceRoom == null ||
            destinationRoom == null ||
            entranceMaskTexture == null ||
            !TryGetObjectBounds(opening.PortalObject, out Bounds portalBounds) ||
            !sourceRoom.TryGetAreaBounds(out Bounds currentBounds) ||
            !destinationRoom.TryGetAreaBounds(out Bounds destinationBounds))
        {
            return;
        }

        Vector2 forward;
        if (opening.Axis == PortalOpeningAxis.Horizontal)
        {
            float directionX = Mathf.Sign(destinationBounds.center.x - currentBounds.center.x);
            forward = new Vector2(directionX, 0f);
        }
        else
        {
            float directionY = Mathf.Sign(destinationBounds.center.y - currentBounds.center.y);
            forward = new Vector2(0f, directionY);
        }

        if (forward.sqrMagnitude < 0.5f)
        {
            return;
        }

        Vector2 side = new Vector2(-forward.y, forward.x);
        Vector2 center = new Vector2(portalBounds.center.x, portalBounds.center.y);
        Vector2 extents = new Vector2(portalBounds.extents.x, portalBounds.extents.y);
        float halfWidth = Mathf.Abs(side.x) * extents.x + Mathf.Abs(side.y) * extents.y;
        float halfPortalDepth = Mathf.Abs(forward.x) * extents.x + Mathf.Abs(forward.y) * extents.y;
        halfWidth = Mathf.Max(0.05f, halfWidth + portalEntranceWidthPadding);

        float reach = halfPortalDepth + portalEntranceDepth;
        float radius = Mathf.Sqrt(reach * reach + halfWidth * halfWidth) + portalEntranceEdgeNoise;
        Bounds paintBounds = new Bounds(
            new Vector3(center.x, center.y, portalBounds.center.z),
            new Vector3(radius * 2f, radius * 2f, 0f));

        int width = entranceMaskTexture.width;
        int height = entranceMaskTexture.height;
        int minX = Mathf.Clamp(WorldToPixelX(paintBounds.min.x, width) - 1, 0, width - 1);
        int maxX = Mathf.Clamp(WorldToPixelX(paintBounds.max.x, width) + 1, 0, width - 1);
        int minY = Mathf.Clamp(WorldToPixelY(paintBounds.min.y, height) - 1, 0, height - 1);
        int maxY = Mathf.Clamp(WorldToPixelY(paintBounds.max.y, height) + 1, 0, height - 1);
        Color32 revealed = new Color32(255, 255, 255, 255);

        for (int y = minY; y <= maxY; y++)
        {
            float worldY = PixelToWorldY(y, height);
            int row = y * width;
            for (int x = minX; x <= maxX; x++)
            {
                float worldX = PixelToWorldX(x, width);
                Vector2 offset = new Vector2(worldX, worldY) - center;
                float forwardDistance = Vector2.Dot(offset, forward);
                if (forwardDistance < -halfPortalDepth ||
                    forwardDistance > portalEntranceDepth + portalEntranceEdgeNoise)
                {
                    continue;
                }

                float noise = ValueNoise(new Vector2(worldX, worldY) * portalEntranceNoiseScale);
                float edgeOffset = (noise - 0.5f) * 2f * portalEntranceEdgeNoise;
                bool isHorizontalEntrance = opening.Axis == PortalOpeningAxis.Horizontal;

                if (isHorizontalEntrance)
                {
                    // Side entrances keep a flat floor. Only the ceiling curves
                    // down toward the destination, producing a cave-mouth shape.
                    float floorY = portalBounds.min.y - portalEntranceWidthPadding;
                    float ceilingY = portalBounds.max.y + portalEntranceWidthPadding;
                    if (forwardDistance <= 0f)
                    {
                        if (worldY >= floorY && worldY <= ceilingY)
                        {
                            entranceMaskPixels[row + x] = revealed;
                        }

                        continue;
                    }

                    float normalizedDepth = Mathf.Clamp01(forwardDistance / portalEntranceDepth);
                    float arch = Mathf.Sqrt(Mathf.Max(0f, 1f - normalizedDepth * normalizedDepth));
                    float curvedCeilingY = Mathf.Lerp(floorY, ceilingY, arch) + edgeOffset;
                    if (worldY >= floorY && worldY <= curvedCeilingY)
                    {
                        entranceMaskPixels[row + x] = revealed;
                    }

                    continue;
                }

                // Vertical connections use the portal width as the diameter of
                // a half ellipse that closes toward the destination room.
                float effectiveDepth = Mathf.Max(0.001f, portalEntranceDepth + edgeOffset);
                if (forwardDistance > effectiveDepth)
                {
                    continue;
                }

                float allowedVerticalHalfWidth = halfWidth;
                if (forwardDistance > 0f)
                {
                    float normalizedDepth = Mathf.Clamp01(forwardDistance / effectiveDepth);
                    allowedVerticalHalfWidth *= Mathf.Sqrt(
                        Mathf.Max(0f, 1f - normalizedDepth * normalizedDepth));
                }

                if (Mathf.Abs(Vector2.Dot(offset, side)) <= allowedVerticalHalfWidth)
                {
                    entranceMaskPixels[row + x] = revealed;
                }
            }
        }
    }

    private static bool TryGetObjectBounds(GameObject target, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        Collider2D[] colliders2D = target.GetComponents<Collider2D>();
        for (int i = 0; i < colliders2D.Length; i++)
        {
            Collider2D collider = colliders2D[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        Collider[] colliders = target.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }

    private void PaintRoom(RoomCameraTrigger room)
    {
        if (room == null ||
            maskTexture == null ||
            maskPixels == null ||
            !room.TryGetAreaBounds(out Bounds roomBounds))
        {
            return;
        }

        PaintRoom(room, roomBounds, CreateRevealBounds(roomBounds));
    }

    private void PaintRoom(RoomCameraTrigger room, Bounds roomBounds, Bounds revealBounds)
    {
        if (room == null || maskTexture == null || maskPixels == null)
        {
            return;
        }

        int width = maskTexture.width;
        int height = maskTexture.height;
        Color32 revealed = new Color32(255, 255, 255, 255);

        int minX = Mathf.Clamp(WorldToPixelX(revealBounds.min.x, width) - 1, 0, width - 1);
        int maxX = Mathf.Clamp(WorldToPixelX(revealBounds.max.x, width) + 1, 0, width - 1);
        int minY = Mathf.Clamp(WorldToPixelY(revealBounds.min.y, height) - 1, 0, height - 1);
        int maxY = Mathf.Clamp(WorldToPixelY(revealBounds.max.y, height) + 1, 0, height - 1);

        for (int y = minY; y <= maxY; y++)
        {
            float worldY = PixelToWorldY(y, height);
            int row = y * width;

            for (int x = minX; x <= maxX; x++)
            {
                float worldX = PixelToWorldX(x, width);
                if (!IsPointInsideRevealArea(
                        room,
                        new Vector3(worldX, worldY, roomBounds.center.z),
                        roomBounds,
                        revealBounds))
                {
                    continue;
                }

                maskPixels[row + x] = revealed;
            }
        }
    }

    private void PaintRoomRevealProgress(PendingReveal reveal, float progress)
    {
        if (maskTexture == null || maskPixels == null)
        {
            return;
        }

        int width = maskTexture.width;
        int height = maskTexture.height;
        Color32 revealed = new Color32(255, 255, 255, 255);

        int minX = Mathf.Clamp(WorldToPixelX(reveal.RevealBounds.min.x, width) - 1, 0, width - 1);
        int maxX = Mathf.Clamp(WorldToPixelX(reveal.RevealBounds.max.x, width) + 1, 0, width - 1);
        int minY = Mathf.Clamp(WorldToPixelY(reveal.RevealBounds.min.y, height) - 1, 0, height - 1);
        int maxY = Mathf.Clamp(WorldToPixelY(reveal.RevealBounds.max.y, height) + 1, 0, height - 1);
        float revealFront = Mathf.Lerp(-0.12f, 1.12f, progress);

        for (int y = minY; y <= maxY; y++)
        {
            float worldY = PixelToWorldY(y, height);
            int row = y * width;

            for (int x = minX; x <= maxX; x++)
            {
                int pixelIndex = row + x;
                if (maskPixels[pixelIndex].r == 255)
                {
                    continue;
                }

                float worldX = PixelToWorldX(x, width);
                if (!IsPointInsideRevealArea(
                        reveal.Room,
                        new Vector3(worldX, worldY, reveal.OriginalBounds.center.z),
                        reveal.OriginalBounds,
                        reveal.RevealBounds))
                {
                    continue;
                }

                float distance = Vector2.Distance(new Vector2(worldX, worldY), reveal.Center);
                float normalizedDistance = distance / reveal.Radius;
                float noise = ValueNoise(new Vector2(worldX, worldY) * 0.45f);
                float noisyDistance = normalizedDistance + (noise - 0.5f) * revealNoiseStrength;
                if (noisyDistance <= revealFront)
                {
                    maskPixels[pixelIndex] = revealed;
                }
            }
        }
    }

    private void PaintRoomConcealProgress(PendingReveal reveal, float progress)
    {
        if (maskTexture == null || maskPixels == null)
        {
            return;
        }

        int width = maskTexture.width;
        int height = maskTexture.height;
        Color32 hidden = new Color32(0, 0, 0, 255);

        int minX = Mathf.Clamp(WorldToPixelX(reveal.RevealBounds.min.x, width) - 1, 0, width - 1);
        int maxX = Mathf.Clamp(WorldToPixelX(reveal.RevealBounds.max.x, width) + 1, 0, width - 1);
        int minY = Mathf.Clamp(WorldToPixelY(reveal.RevealBounds.min.y, height) - 1, 0, height - 1);
        int maxY = Mathf.Clamp(WorldToPixelY(reveal.RevealBounds.max.y, height) + 1, 0, height - 1);
        float revealFront = Mathf.Lerp(1.12f, -0.12f, progress);
        bool protectsCurrentRoom = TryGetCurrentRoomRevealArea(
            reveal.Room,
            out RoomCameraTrigger protectedRoom,
            out Bounds protectedRoomBounds,
            out Bounds protectedRevealBounds);

        for (int y = minY; y <= maxY; y++)
        {
            float worldY = PixelToWorldY(y, height);
            int row = y * width;

            for (int x = minX; x <= maxX; x++)
            {
                int pixelIndex = row + x;
                if (maskPixels[pixelIndex].r == 0)
                {
                    continue;
                }

                float worldX = PixelToWorldX(x, width);
                Vector3 worldPoint = new Vector3(
                    worldX,
                    worldY,
                    reveal.OriginalBounds.center.z);
                if (!IsPointInsideRevealArea(
                        reveal.Room,
                        worldPoint,
                        reveal.OriginalBounds,
                        reveal.RevealBounds))
                {
                    continue;
                }

                // Adjacent rooms share padded reveal pixels. Never let the
                // outgoing room's conceal overwrite the room the player is in.
                if (protectsCurrentRoom &&
                    IsPointInsideRevealArea(
                        protectedRoom,
                        new Vector3(worldX, worldY, protectedRoomBounds.center.z),
                        protectedRoomBounds,
                        protectedRevealBounds))
                {
                    continue;
                }

                float distance = Vector2.Distance(new Vector2(worldX, worldY), reveal.Center);
                float normalizedDistance = distance / reveal.Radius;
                float noise = ValueNoise(new Vector2(worldX, worldY) * 0.45f);
                float noisyDistance = normalizedDistance + (noise - 0.5f) * revealNoiseStrength;
                if (noisyDistance > revealFront)
                {
                    maskPixels[pixelIndex] = hidden;
                }
            }
        }
    }

    private void PaintRoomHidden(RoomCameraTrigger room, Bounds roomBounds, Bounds revealBounds)
    {
        if (room == null || maskTexture == null || maskPixels == null)
        {
            return;
        }

        int width = maskTexture.width;
        int height = maskTexture.height;
        Color32 hidden = new Color32(0, 0, 0, 255);

        int minX = Mathf.Clamp(WorldToPixelX(revealBounds.min.x, width) - 1, 0, width - 1);
        int maxX = Mathf.Clamp(WorldToPixelX(revealBounds.max.x, width) + 1, 0, width - 1);
        int minY = Mathf.Clamp(WorldToPixelY(revealBounds.min.y, height) - 1, 0, height - 1);
        int maxY = Mathf.Clamp(WorldToPixelY(revealBounds.max.y, height) + 1, 0, height - 1);
        bool protectsCurrentRoom = TryGetCurrentRoomRevealArea(
            room,
            out RoomCameraTrigger protectedRoom,
            out Bounds protectedRoomBounds,
            out Bounds protectedRevealBounds);

        for (int y = minY; y <= maxY; y++)
        {
            float worldY = PixelToWorldY(y, height);
            int row = y * width;

            for (int x = minX; x <= maxX; x++)
            {
                float worldX = PixelToWorldX(x, width);
                Vector3 worldPoint = new Vector3(worldX, worldY, roomBounds.center.z);
                if (!IsPointInsideRevealArea(
                        room,
                        worldPoint,
                        roomBounds,
                        revealBounds))
                {
                    continue;
                }

                if (protectsCurrentRoom &&
                    IsPointInsideRevealArea(
                        protectedRoom,
                        new Vector3(worldX, worldY, protectedRoomBounds.center.z),
                        protectedRoomBounds,
                        protectedRevealBounds))
                {
                    continue;
                }

                maskPixels[row + x] = hidden;
            }
        }
    }

    private bool TryGetCurrentRoomRevealArea(
        RoomCameraTrigger excludedRoom,
        out RoomCameraTrigger room,
        out Bounds roomBounds,
        out Bounds revealBounds)
    {
        room = currentRoom;
        roomBounds = default;
        revealBounds = default;

        if (currentRoom == null ||
            currentRoom == excludedRoom ||
            currentRoom.gameObject.scene != managedScene ||
            !currentRoom.TryGetAreaBounds(out roomBounds))
        {
            return false;
        }

        revealBounds = CreateRevealBounds(roomBounds);
        return true;
    }

    private Bounds CreateRevealBounds(Bounds roomBounds)
    {
        Bounds revealBounds = roomBounds;
        if (revealPaddingX > 0f || revealPaddingY > 0f)
        {
            revealBounds.Expand(new Vector3(revealPaddingX * 2f, revealPaddingY * 2f, 0f));
        }

        return revealBounds;
    }

    private bool IsPointInsideRevealArea(
        RoomCameraTrigger room,
        Vector3 point,
        Bounds originalBounds,
        Bounds revealBounds)
    {
        if ((revealPaddingX > 0f || revealPaddingY > 0f) && revealBounds.Contains(point))
        {
            return true;
        }

        if (!useColliderShape)
        {
            return originalBounds.Contains(point);
        }

        return room != null && room.ContainsPoint(point);
    }

    private void ApplyMaskTexture()
    {
        if (maskTexture == null || maskPixels == null)
        {
            return;
        }

        maskTexture.SetPixels32(maskPixels);
        maskTexture.Apply(false, false);
    }

    private int WorldToPixelX(float worldX, int width)
    {
        float normalized = Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, worldX);
        return Mathf.FloorToInt(normalized * (width - 1));
    }

    private int WorldToPixelY(float worldY, int height)
    {
        float normalized = Mathf.InverseLerp(worldBounds.min.y, worldBounds.max.y, worldY);
        return Mathf.FloorToInt(normalized * (height - 1));
    }

    private float PixelToWorldX(int x, int width)
    {
        float normalized = (x + 0.5f) / width;
        return Mathf.Lerp(worldBounds.min.x, worldBounds.max.x, normalized);
    }

    private float PixelToWorldY(int y, int height)
    {
        float normalized = (y + 0.5f) / height;
        return Mathf.Lerp(worldBounds.min.y, worldBounds.max.y, normalized);
    }

    private float ResolveRevealRadius(Vector2 center, Bounds revealBounds)
    {
        float maxDistance = 0.001f;
        Vector2 min = revealBounds.min;
        Vector2 max = revealBounds.max;
        maxDistance = Mathf.Max(maxDistance, Vector2.Distance(center, new Vector2(min.x, min.y)));
        maxDistance = Mathf.Max(maxDistance, Vector2.Distance(center, new Vector2(max.x, min.y)));
        maxDistance = Mathf.Max(maxDistance, Vector2.Distance(center, new Vector2(max.x, max.y)));
        maxDistance = Mathf.Max(maxDistance, Vector2.Distance(center, new Vector2(min.x, max.y)));
        return maxDistance;
    }

    private float ValueNoise(Vector2 position)
    {
        Vector2 cell = new Vector2(Mathf.Floor(position.x), Mathf.Floor(position.y));
        Vector2 fraction = position - cell;
        fraction = new Vector2(
            fraction.x * fraction.x * (3f - 2f * fraction.x),
            fraction.y * fraction.y * (3f - 2f * fraction.y));

        float a = Hash21(cell);
        float b = Hash21(cell + Vector2.right);
        float c = Hash21(cell + Vector2.up);
        float d = Hash21(cell + Vector2.one);
        return Mathf.Lerp(Mathf.Lerp(a, b, fraction.x), Mathf.Lerp(c, d, fraction.x), fraction.y);
    }

    private float Hash21(Vector2 position)
    {
        float hash = Mathf.Sin(Vector2.Dot(position, new Vector2(127.1f, 311.7f))) * 43758.5453f;
        return hash - Mathf.Floor(hash);
    }

    private void EnsureOverlay()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            if (!shaderWarningLogged)
            {
                Debug.LogWarning($"[RoomFogRevealManager] Shader '{ShaderName}' was not found.");
                shaderWarningLogged = true;
            }

            SetOverlayVisible(false);
            return;
        }

        if (fogMaterial == null)
        {
            fogMaterial = new Material(shader)
            {
                name = "RoomFogOverlayRuntimeMaterial",
                hideFlags = HideFlags.DontSave
            };
        }

        if (overlayFilter == null || overlayRenderer == null)
        {
            overlayObject = new GameObject("[RoomFogOverlay]");
            overlayObject.hideFlags = HideFlags.DontSave;
            overlayFilter = overlayObject.AddComponent<MeshFilter>();
            overlayRenderer = overlayObject.AddComponent<MeshRenderer>();
            overlayRenderer.sharedMaterial = fogMaterial;
        }

        if (overlayMesh == null)
        {
            overlayMesh = new Mesh
            {
                name = "RoomFogOverlayMesh",
                hideFlags = HideFlags.DontSave
            };
        }

        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        min.z = overlayZ;
        max.z = overlayZ;

        overlayMesh.Clear();
        overlayMesh.vertices = new[]
        {
            new Vector3(min.x, min.y, overlayZ),
            new Vector3(max.x, min.y, overlayZ),
            new Vector3(max.x, max.y, overlayZ),
            new Vector3(min.x, max.y, overlayZ)
        };
        overlayMesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        overlayMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        overlayMesh.RecalculateBounds();
        overlayFilter.sharedMesh = overlayMesh;

        overlayRenderer.sortingLayerID = 0;
        overlayRenderer.sortingOrder = sortingOrder;
        overlayRenderer.sharedMaterial = fogMaterial;
    }

    private void ApplyMaterialProperties()
    {
        if (fogMaterial == null || maskTexture == null)
        {
            return;
        }

        Vector3 min = worldBounds.min;
        Vector3 size = worldBounds.size;
        fogMaterial.SetTexture("_MaskTex", maskTexture);
        fogMaterial.SetTexture("_EntranceMaskTex", entranceMaskTexture);
        fogMaterial.SetColor("_FogColor", fogColor);
        fogMaterial.SetFloat("_FogAlpha", fogAlpha);
        fogMaterial.SetFloat("_EdgeSoftness", edgeSoftness);
        fogMaterial.SetFloat("_NoiseStrength", noiseStrength);
        fogMaterial.SetFloat("_NoiseScale", noiseScale);
        fogMaterial.SetVector("_WorldMin", new Vector4(min.x, min.y, 0f, 0f));
        fogMaterial.SetVector("_WorldSize", new Vector4(Mathf.Max(size.x, 0.001f), Mathf.Max(size.y, 0.001f), 0f, 0f));
    }

    private void SetOverlayVisible(bool visible)
    {
        if (overlayRenderer != null)
        {
            overlayRenderer.enabled = fogEnabled && visible;
        }
    }

    private void DestroyOverlayResources()
    {
        if (overlayObject != null)
        {
            Destroy(overlayObject);
            overlayObject = null;
            overlayFilter = null;
            overlayRenderer = null;
        }

        if (overlayMesh != null)
        {
            Destroy(overlayMesh);
            overlayMesh = null;
        }

        if (fogMaterial != null)
        {
            Destroy(fogMaterial);
            fogMaterial = null;
        }

        if (maskTexture != null)
        {
            Destroy(maskTexture);
            maskTexture = null;
            maskPixels = null;
        }

        if (entranceMaskTexture != null)
        {
            Destroy(entranceMaskTexture);
            entranceMaskTexture = null;
            entranceMaskPixels = null;
        }
    }

    private void ConfigurePortalRevealTriggers()
    {
        ConfigureRoomCameraPortals();
        ConfigureVerticalRoomCameraPortals();
        ConfigureFallRoomCameraPortals();
        ConfigureSwitchPortals();
    }

    private void ConfigureRoomCameraPortals()
    {
        RoomCameraPortal[] portals = FindObjectsByType<RoomCameraPortal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < portals.Length; i++)
        {
            RoomCameraPortal portal = portals[i];
            if (portal == null || portal.gameObject.scene != managedScene)
            {
                continue;
            }

            RoomCameraTrigger roomA = ReadRoomField(portal, "roomA");
            RoomCameraTrigger roomB = ReadRoomField(portal, "roomB");
            ConfigureTrigger(portal.gameObject, roomA, roomB);
            RegisterPortalOpening(
                portal.gameObject,
                roomA,
                roomB,
                PortalOpeningAxis.Horizontal,
                true,
                true);
        }
    }

    private void ConfigureVerticalRoomCameraPortals()
    {
        VerticalRoomCameraPortal[] portals = FindObjectsByType<VerticalRoomCameraPortal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < portals.Length; i++)
        {
            VerticalRoomCameraPortal portal = portals[i];
            if (portal == null || portal.gameObject.scene != managedScene)
            {
                continue;
            }

            RoomCameraTrigger upperRoom = ReadRoomField(portal, "upperRoom");
            RoomCameraTrigger lowerRoom = ReadRoomField(portal, "lowerRoom");
            ConfigureTrigger(portal.gameObject, upperRoom, lowerRoom);

            int direction = ReadEnumFieldValue(portal, "direction");
            RegisterPortalOpening(
                portal.gameObject,
                upperRoom,
                lowerRoom,
                PortalOpeningAxis.Vertical,
                direction != 2,
                direction != 1);
        }
    }

    private void ConfigureFallRoomCameraPortals()
    {
        FallRoomCameraPortal[] portals = FindObjectsByType<FallRoomCameraPortal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < portals.Length; i++)
        {
            FallRoomCameraPortal portal = portals[i];
            if (portal == null || portal.gameObject.scene != managedScene)
            {
                continue;
            }

            RoomCameraTrigger upperRoom = ReadRoomField(portal, "upperRoom");
            RoomCameraTrigger lowerRoom = ReadRoomField(portal, "lowerRoom");
            ConfigureTrigger(portal.gameObject, upperRoom, lowerRoom);
            RegisterPortalOpening(
                portal.gameObject,
                upperRoom,
                lowerRoom,
                PortalOpeningAxis.Vertical,
                true,
                false);
        }
    }

    private void ConfigureSwitchPortals()
    {
        RoomCameraSwitchPortal[] portals = FindObjectsByType<RoomCameraSwitchPortal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < portals.Length; i++)
        {
            RoomCameraSwitchPortal portal = portals[i];
            if (portal == null || portal.gameObject.scene != managedScene)
            {
                continue;
            }

            ConfigureTrigger(portal.gameObject, ReadRoomField(portal, "targetRoom"));
        }
    }

    private void RegisterPortalOpening(
        GameObject portalObject,
        RoomCameraTrigger roomA,
        RoomCameraTrigger roomB,
        PortalOpeningAxis axis,
        bool allowAToB,
        bool allowBToA)
    {
        if (portalObject == null || roomA == null || roomB == null)
        {
            return;
        }

        portalOpenings.Add(new PortalOpening
        {
            PortalObject = portalObject,
            RoomA = roomA,
            RoomB = roomB,
            Axis = axis,
            AllowAToB = allowAToB,
            AllowBToA = allowBToA
        });
    }

    private void ConfigureTrigger(GameObject target, params RoomCameraTrigger[] targetRooms)
    {
        if (target == null || targetRooms == null)
        {
            return;
        }

        List<RoomCameraTrigger> validRooms = new List<RoomCameraTrigger>(targetRooms.Length);
        for (int i = 0; i < targetRooms.Length; i++)
        {
            RoomCameraTrigger room = targetRooms[i];
            if (room != null && room.gameObject.scene == managedScene && !validRooms.Contains(room))
            {
                validRooms.Add(room);
            }
        }

        if (validRooms.Count == 0)
        {
            return;
        }

        RoomFogRevealTrigger revealTrigger = target.GetComponent<RoomFogRevealTrigger>();
        if (revealTrigger == null)
        {
            revealTrigger = target.AddComponent<RoomFogRevealTrigger>();
        }

        revealTrigger.Configure(validRooms.ToArray());
    }

    private static RoomCameraTrigger ReadRoomField(object source, string fieldName)
    {
        if (source == null || string.IsNullOrWhiteSpace(fieldName))
        {
            return null;
        }

        Type type = source.GetType();
        System.Reflection.FieldInfo field = type.GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        return field != null ? field.GetValue(source) as RoomCameraTrigger : null;
    }

    private static int ReadEnumFieldValue(object source, string fieldName)
    {
        if (source == null || string.IsNullOrWhiteSpace(fieldName))
        {
            return 0;
        }

        System.Reflection.FieldInfo field = source.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        object value = field != null ? field.GetValue(source) : null;
        return value != null ? Convert.ToInt32(value) : 0;
    }

    private bool TryGetPlayerPosition(out Vector3 position)
    {
        position = Vector3.zero;

        GameObject taggedPlayer = null;
        try
        {
            taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        }
        catch (UnityException)
        {
            taggedPlayer = null;
        }

        if (taggedPlayer != null)
        {
            position = taggedPlayer.transform.position;
            return true;
        }

        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
        if (player == null)
        {
            return false;
        }

        position = player.transform.position;
        return true;
    }

    private string CreateRoomId(RoomCameraTrigger room)
    {
        string roomName = room != null ? room.gameObject.name : "UnknownRoom";
        return $"{managedScene.name}:{roomName}";
    }

}
