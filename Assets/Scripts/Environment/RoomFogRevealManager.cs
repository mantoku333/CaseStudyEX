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

    private readonly List<RoomCameraTrigger> rooms = new List<RoomCameraTrigger>();
    private readonly Dictionary<RoomCameraTrigger, string> roomIds = new Dictionary<RoomCameraTrigger, string>();
    private readonly HashSet<string> visibleRoomIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> pendingRoomIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<PendingReveal> pendingReveals = new List<PendingReveal>();
    private RoomCameraTrigger currentRoom;

    private Scene managedScene;
    private Bounds worldBounds;
    private Texture2D maskTexture;
    private Color32[] maskPixels;
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
        currentRoom = null;
        ClearMaskPixels();

        ApplyMaskTexture();
        SetOverlayVisible(true);

        if (revealCurrentRoom)
        {
            RevealCurrentRoomFromRuntimeState();
        }
    }

    private void HandleActiveRoomChanged(RoomCameraTrigger activeRoom)
    {
        if (activeRoom != null)
        {
            SetCurrentRoom(activeRoom);
        }
        else
        {
            ClearCurrentRoom();
        }
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

        if (!ShouldRoomBeCurrent(room))
        {
            return false;
        }

        SetCurrentRoom(room);
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

        if (currentRoom == room &&
            (visibleRoomIds.Contains(roomId) || pendingRoomIds.Contains(roomId)))
        {
            return;
        }

        RoomCameraTrigger previousRoom = currentRoom;
        currentRoom = room;

        if (previousRoom != null &&
            previousRoom != room &&
            previousRoom.gameObject.scene == managedScene &&
            roomIds.TryGetValue(previousRoom, out string previousRoomId))
        {
            BeginTransition(previousRoom, previousRoomId, false);
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
        if (previousRoom.gameObject.scene == managedScene &&
            roomIds.TryGetValue(previousRoom, out string previousRoomId))
        {
            BeginTransition(previousRoom, previousRoomId, false);
        }
    }

    private bool ShouldRoomBeCurrent(RoomCameraTrigger room)
    {
        RoomCameraTrigger activeRoom = RoomCameraTrigger.ActiveRoom;
        if (activeRoom != null)
        {
            return activeRoom == room;
        }

        return TryGetPlayerPosition(out Vector3 playerPosition) &&
            room.ContainsPoint(playerPosition);
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
        if (activeRoom != null && activeRoom.gameObject.scene == managedScene)
        {
            SetCurrentRoom(activeRoom);
            return;
        }

        if (!TryGetPlayerPosition(out Vector3 playerPosition))
        {
            return;
        }

        RoomCameraTrigger containingRoom = ResolveSmallestRoomContaining(playerPosition);
        if (containingRoom != null)
        {
            SetCurrentRoom(containingRoom);
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
            maskTexture.height == resolution)
        {
            return;
        }

        if (maskTexture != null)
        {
            Destroy(maskTexture);
        }

        maskTexture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
        {
            name = "RoomFogRevealMask",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        maskPixels = new Color32[resolution * resolution];
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

                maskPixels[row + x] = hidden;
            }
        }
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

            ConfigureTrigger(portal.gameObject, ReadRoomField(portal, "roomA"), ReadRoomField(portal, "roomB"));
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

            ConfigureTrigger(portal.gameObject, ReadRoomField(portal, "upperRoom"), ReadRoomField(portal, "lowerRoom"));
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

            ConfigureTrigger(portal.gameObject, ReadRoomField(portal, "upperRoom"), ReadRoomField(portal, "lowerRoom"));
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
