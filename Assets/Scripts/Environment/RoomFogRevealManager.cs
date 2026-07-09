using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Environment/Room Fog Reveal Manager")]
public sealed class RoomFogRevealManager : MonoBehaviour, ISaveDataModule
{
    private const string ShaderName = "CaseStudy/RoomFogOverlay";
    private const string OverlayObjectName = "FOG";
    private const float RoomRefreshInterval = 0.15f;

    private static RoomFogRevealManager instance;

    [SerializeField] private bool fogEnabled = true;
    [SerializeField, Min(64)] private int textureResolution = 1024;
    [SerializeField, Min(0f)] private float worldPadding = 6f;
    [SerializeField] private Shader fogShader;

    [SerializeField] private Color fogColor = new Color(0f, 0f, 0f, 0.92f);
    [SerializeField, Range(0f, 1f)] private float fogAlpha = 1f;
    [SerializeField, Range(0.01f, 1f)] private float edgeSoftness = 0.22f;
    [SerializeField, Range(0f, 1f)] private float noiseStrength = 0.18f;
    [SerializeField, Min(0.1f)] private float noiseScale = 0.32f;
    [SerializeField] private int sortingOrder = 30000;
    [SerializeField] private float overlayZ = -1f;

    [SerializeField, Min(0.01f)] private float revealDuration = 2.55f;
    [SerializeField, Min(0.01f)] private float concealDuration = 2.55f;
    [SerializeField, Range(0f, 0.5f)] private float revealNoiseStrength = 0.18f;

    [SerializeField] private bool revealPortalEntrances = true;
    [SerializeField, Min(0f)] private float portalEntranceDepth = 2.5f;
    [SerializeField, Min(0.05f)] private float portalEntranceRadius = 1.2f;
    [SerializeField, Range(0.01f, 1f)] private float portalEntranceSoftness = 0.35f;
    [SerializeField, Range(0f, 0.5f)] private float portalEntranceEdgeNoise = 0.12f;

    private readonly List<RoomCameraTrigger> rooms = new List<RoomCameraTrigger>();
    private readonly List<PortalDent> portalDents = new List<PortalDent>();
    private RoomCameraTrigger currentRoom;
    private RoomCameraTrigger revealingRoom;
    private RoomCameraTrigger concealingRoom;
    private Bounds revealingBounds;
    private Bounds revealingPaintBounds;
    private Bounds concealingBounds;
    private Bounds concealingPaintBounds;
    private Vector2 revealCenter;
    private Vector2 concealCenter;
    private float revealRadius = 1f;
    private float concealRadius = 1f;
    private float revealStartedAt;
    private float concealStartedAt;
    private bool revealComplete;
    private bool concealComplete = true;

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
    private float nextRoomRefreshTime;
    private bool hasRooms;
    private bool shaderWarningLogged;

    public int Priority => 260;
    public static bool FogEnabled => instance != null && instance.fogEnabled;

    private struct PortalDent
    {
        public GameObject PortalObject;
        public RoomCameraTrigger RoomA;
        public RoomCameraTrigger RoomB;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RuntimeInitialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        RefreshExistingManager();
    }

    public static bool RevealRoom(RoomCameraTrigger room)
    {
        if (room == null || !TryGetInstance(out RoomFogRevealManager manager))
        {
            return false;
        }

        if (!manager.IsRoomCurrentByRuntimeState(room))
        {
            return false;
        }

        manager.SetCurrentRoom(room, true);
        return true;
    }

    public static void SetFogEnabled(bool enabled)
    {
        if (!TryGetInstance(out RoomFogRevealManager manager))
        {
            Debug.LogWarning("[RoomFogRevealManager] No manager exists in the active scene.");
            return;
        }

        manager.fogEnabled = enabled;
        manager.RefreshForCurrentScene(true);
    }

    private static bool TryGetInstance(out RoomFogRevealManager manager)
    {
        if (instance != null && instance.isActiveAndEnabled)
        {
            manager = instance;
            return true;
        }

        instance = FindFirstObjectByType<RoomFogRevealManager>(FindObjectsInactive.Exclude);
        manager = instance;
        return manager != null && manager.isActiveAndEnabled;
    }

    private static void RefreshExistingManager()
    {
        if (TryGetInstance(out RoomFogRevealManager manager))
        {
            manager.RefreshForCurrentScene(true);
        }
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshExistingManager();
    }

    private void Awake()
    {
        if (Application.isPlaying && instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnEnable()
    {
        instance = this;
        if (Application.isPlaying)
        {
            SaveManager.RegisterModule(this);
            RoomCameraTrigger.ActiveRoomChanged += HandleActiveRoomChanged;
        }

        RefreshForCurrentScene(true);
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            SaveManager.UnregisterModule(this);
            RoomCameraTrigger.ActiveRoomChanged -= HandleActiveRoomChanged;
        }

        if (instance == this)
        {
            instance = null;
        }

        DestroyOverlayResources();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        DestroyOverlayResources();
    }

    private void OnValidate()
    {
        textureResolution = Mathf.Max(64, textureResolution);
        revealDuration = Mathf.Max(0.01f, revealDuration);
        concealDuration = Mathf.Max(0.01f, concealDuration);
        edgeSoftness = Mathf.Max(0.01f, edgeSoftness);
        portalEntranceRadius = Mathf.Max(0.05f, portalEntranceRadius);
        portalEntranceSoftness = Mathf.Max(0.01f, portalEntranceSoftness);
        ResolveDefaultShader();
    }

    private void Update()
    {
        if (!managedScene.IsValid() || managedScene != gameObject.scene)
        {
            RefreshForCurrentScene(true);
        }

        if (!fogEnabled)
        {
            SetOverlayVisible(false);
            return;
        }

        if (!Application.isPlaying)
        {
            RefreshForCurrentScene(false);
            PaintAndApplyMasks(1f, 1f);
            return;
        }

        if (Time.unscaledTime >= nextRoomRefreshTime)
        {
            nextRoomRefreshTime = Time.unscaledTime + RoomRefreshInterval;
            RevealCurrentRoomFromRuntimeState();
        }

        float revealProgress = ResolveRevealProgress();
        float concealProgress = ResolveConcealProgress();
        PaintAndApplyMasks(revealProgress, concealProgress);
    }

    public void Capture(SaveGameData saveData)
    {
        // Fog is transient: it follows only the room the player is currently in.
    }

    public void Restore(SaveGameData saveData)
    {
        RefreshForCurrentScene(true);
    }

    private void RefreshForCurrentScene(bool revealCurrentRoom)
    {
        managedScene = gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene();
        ResolveDefaultShader();
        CollectRooms();
        CollectPortalDents();

        if (!hasRooms)
        {
            SetOverlayVisible(false);
            return;
        }

        EnsureTextures();
        EnsureOverlay();
        ApplyMaterialProperties();
        SetOverlayVisible(fogEnabled);

        if (revealCurrentRoom)
        {
            RevealCurrentRoomFromRuntimeState();
        }

        PaintAndApplyMasks(ResolveRevealProgress(), ResolveConcealProgress());
    }

    private void CollectRooms()
    {
        rooms.Clear();
        worldBounds = default;
        hasRooms = false;

        RoomCameraTrigger[] allRooms = FindObjectsByType<RoomCameraTrigger>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

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
            if (!hasRooms)
            {
                worldBounds = roomBounds;
                hasRooms = true;
            }
            else
            {
                worldBounds.Encapsulate(roomBounds);
            }
        }

        if (hasRooms)
        {
            worldBounds.Expand(worldPadding * 2f);
        }
    }

    private void CollectPortalDents()
    {
        portalDents.Clear();
        RegisterPortalDents<RoomCameraPortal>("roomA", "roomB");
        RegisterPortalDents<VerticalRoomCameraPortal>("upperRoom", "lowerRoom");
        RegisterPortalDents<FallRoomCameraPortal>("upperRoom", "lowerRoom");
    }

    private void RegisterPortalDents<T>(string firstRoomField, string secondRoomField)
        where T : Component
    {
        T[] portals = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < portals.Length; i++)
        {
            T portal = portals[i];
            if (portal == null || portal.gameObject.scene != managedScene)
            {
                continue;
            }

            RoomCameraTrigger roomA = ReadRoomField(portal, firstRoomField);
            RoomCameraTrigger roomB = ReadRoomField(portal, secondRoomField);
            if (roomA == null || roomB == null)
            {
                continue;
            }

            portalDents.Add(new PortalDent
            {
                PortalObject = portal.gameObject,
                RoomA = roomA,
                RoomB = roomB
            });
        }
    }

    private void HandleActiveRoomChanged(RoomCameraTrigger activeRoom)
    {
        RevealCurrentRoomFromRuntimeState();
    }

    private void RevealCurrentRoomFromRuntimeState()
    {
        RoomCameraTrigger room = ResolveCurrentRoomFromRuntimeState();
        if (room != null)
        {
            SetCurrentRoom(room, false);
        }
    }

    private RoomCameraTrigger ResolveCurrentRoomFromRuntimeState()
    {
        if (TryGetPlayerPosition(out Vector3 playerPosition))
        {
            RoomCameraTrigger containingRoom = ResolveSmallestRoomContaining(playerPosition);
            if (containingRoom != null)
            {
                return containingRoom;
            }
        }

        RoomCameraTrigger activeRoom = RoomCameraTrigger.ActiveRoom;
        return activeRoom != null && activeRoom.gameObject.scene == managedScene
            ? activeRoom
            : null;
    }

    private bool IsRoomCurrentByRuntimeState(RoomCameraTrigger room)
    {
        return room != null && ResolveCurrentRoomFromRuntimeState() == room;
    }

    private void SetCurrentRoom(RoomCameraTrigger room, bool restartEvenIfSame)
    {
        if (room == null || room.gameObject.scene != managedScene)
        {
            return;
        }

        if (!room.TryGetAreaBounds(out Bounds roomBounds))
        {
            return;
        }

        if (!restartEvenIfSame && currentRoom == room)
        {
            return;
        }

        RoomCameraTrigger previousRoom = currentRoom;
        if (Application.isPlaying &&
            previousRoom != null &&
            previousRoom != room &&
            previousRoom.gameObject.scene == managedScene &&
            previousRoom.TryGetAreaBounds(out Bounds previousBounds))
        {
            concealingRoom = previousRoom;
            concealingBounds = previousBounds;
            concealingPaintBounds = CreateRevealBounds(previousBounds);
            concealCenter = ResolveConcealCenter(previousBounds);
            concealRadius = ResolveRevealRadius(concealCenter, concealingPaintBounds);
            concealStartedAt = Time.unscaledTime;
            concealComplete = concealDuration <= 0.01f;
        }

        currentRoom = room;
        revealingRoom = room;
        revealingBounds = roomBounds;
        revealingPaintBounds = CreateRevealBounds(roomBounds);
        revealCenter = ResolveRevealCenter(roomBounds);
        revealRadius = ResolveRevealRadius(revealCenter, revealingPaintBounds);
        revealStartedAt = Application.isPlaying ? Time.unscaledTime : 0f;
        revealComplete = !Application.isPlaying || revealDuration <= 0.01f;
    }

    private float ResolveRevealProgress()
    {
        if (!Application.isPlaying || revealComplete || revealingRoom == null)
        {
            return 1f;
        }

        float raw = Mathf.Clamp01((Time.unscaledTime - revealStartedAt) / revealDuration);
        float smooth = Mathf.SmoothStep(0f, 1f, raw);
        if (smooth >= 1f)
        {
            revealComplete = true;
        }

        return smooth;
    }

    private float ResolveConcealProgress()
    {
        if (!Application.isPlaying || concealComplete || concealingRoom == null)
        {
            return 1f;
        }

        float raw = Mathf.Clamp01((Time.unscaledTime - concealStartedAt) / concealDuration);
        float smooth = Mathf.SmoothStep(0f, 1f, raw);
        if (smooth >= 1f)
        {
            concealComplete = true;
            concealingRoom = null;
        }

        return smooth;
    }

    private Vector2 ResolveRevealCenter(Bounds roomBounds)
    {
        if (TryGetPlayerPosition(out Vector3 playerPosition))
        {
            return new Vector2(playerPosition.x, playerPosition.y);
        }

        return new Vector2(roomBounds.center.x, roomBounds.center.y);
    }

    private Vector2 ResolveConcealCenter(Bounds roomBounds)
    {
        if (TryGetPlayerPosition(out Vector3 playerPosition))
        {
            return new Vector2(playerPosition.x, playerPosition.y);
        }

        return new Vector2(roomBounds.center.x, roomBounds.center.y);
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
                bestRoom = room;
                bestArea = area;
            }
        }

        return bestRoom;
    }

    private void PaintAndApplyMasks(float revealProgress, float concealProgress)
    {
        if (maskTexture == null ||
            entranceMaskTexture == null ||
            maskPixels == null ||
            entranceMaskPixels == null)
        {
            return;
        }

        ClearPixels(maskPixels);
        ClearPixels(entranceMaskPixels);

        if (fogEnabled)
        {
            if (concealingRoom != null && !concealComplete)
            {
                PaintRoom(
                    concealingRoom,
                    concealingBounds,
                    concealingPaintBounds,
                    concealCenter,
                    concealRadius,
                    Mathf.Lerp(1.12f, -0.12f, concealProgress));
            }

            if (revealingRoom != null)
            {
                PaintRoom(
                    revealingRoom,
                    revealingBounds,
                    revealingPaintBounds,
                    revealCenter,
                    revealRadius,
                    Mathf.Lerp(-0.12f, 1.12f, revealProgress));
            }

            PaintPortalDents(currentRoom, 1f);
            if (concealingRoom != null && !concealComplete)
            {
                PaintPortalDents(concealingRoom, 1f - concealProgress);
            }
        }

        maskTexture.SetPixels32(maskPixels);
        maskTexture.Apply(false, false);
        entranceMaskTexture.SetPixels32(entranceMaskPixels);
        entranceMaskTexture.Apply(false, false);
    }

    private void PaintRoom(
        RoomCameraTrigger room,
        Bounds roomBounds,
        Bounds paintBounds,
        Vector2 center,
        float radius,
        float revealFront)
    {
        int width = maskTexture.width;
        int height = maskTexture.height;
        int minX = Mathf.Clamp(WorldToPixelX(paintBounds.min.x, width) - 1, 0, width - 1);
        int maxX = Mathf.Clamp(WorldToPixelX(paintBounds.max.x, width) + 1, 0, width - 1);
        int minY = Mathf.Clamp(WorldToPixelY(paintBounds.min.y, height) - 1, 0, height - 1);
        int maxY = Mathf.Clamp(WorldToPixelY(paintBounds.max.y, height) + 1, 0, height - 1);

        for (int y = minY; y <= maxY; y++)
        {
            float worldY = PixelToWorldY(y, height);
            int row = y * width;
            for (int x = minX; x <= maxX; x++)
            {
                float worldX = PixelToWorldX(x, width);
                Vector3 worldPoint = new Vector3(worldX, worldY, roomBounds.center.z);
                if (!IsPointInsideRevealArea(room, worldPoint))
                {
                    continue;
                }

                float distance = Vector2.Distance(new Vector2(worldX, worldY), center);
                float normalizedDistance = distance / Mathf.Max(0.001f, radius);
                float noise = ValueNoise(new Vector2(worldX, worldY) * 0.45f);
                float noisyDistance = normalizedDistance + (noise - 0.5f) * revealNoiseStrength;
                if (noisyDistance <= revealFront)
                {
                    maskPixels[row + x] = RevealedPixel(1f);
                }
            }
        }
    }

    private void PaintPortalDents(RoomCameraTrigger sourceRoom, float strengthMultiplier)
    {
        if (!revealPortalEntrances ||
            sourceRoom == null ||
            portalEntranceDepth <= 0f ||
            strengthMultiplier <= 0f)
        {
            return;
        }

        for (int i = 0; i < portalDents.Count; i++)
        {
            PortalDent dent = portalDents[i];
            RoomCameraTrigger destinationRoom = null;
            if (dent.RoomA == sourceRoom)
            {
                destinationRoom = dent.RoomB;
            }
            else if (dent.RoomB == sourceRoom)
            {
                destinationRoom = dent.RoomA;
            }

            if (destinationRoom == null)
            {
                continue;
            }

            PaintPortalDent(dent.PortalObject, sourceRoom, destinationRoom, strengthMultiplier);
        }
    }

    private void PaintPortalDent(
        GameObject portalObject,
        RoomCameraTrigger sourceRoom,
        RoomCameraTrigger destinationRoom,
        float strengthMultiplier)
    {
        if (portalObject == null ||
            !TryGetObjectBounds(portalObject, out Bounds portalBounds) ||
            !sourceRoom.TryGetAreaBounds(out Bounds sourceBounds) ||
            !destinationRoom.TryGetAreaBounds(out Bounds destinationBounds))
        {
            return;
        }

        Vector2 forward = new Vector2(
            destinationBounds.center.x - sourceBounds.center.x,
            destinationBounds.center.y - sourceBounds.center.y);
        if (forward.sqrMagnitude < 0.001f)
        {
            return;
        }

        forward.Normalize();
        Vector2 side = new Vector2(-forward.y, forward.x);
        Vector2 center = new Vector2(portalBounds.center.x, portalBounds.center.y);
        Vector2 extents = new Vector2(portalBounds.extents.x, portalBounds.extents.y);
        float halfPortalDepth = Mathf.Abs(forward.x) * extents.x + Mathf.Abs(forward.y) * extents.y;
        float halfPortalWidth = Mathf.Abs(side.x) * extents.x + Mathf.Abs(side.y) * extents.y;
        float halfWidth = Mathf.Max(0.05f, halfPortalWidth + portalEntranceRadius);
        float halfDepth = Mathf.Max(0.05f, halfPortalDepth + portalEntranceDepth);

        Vector2 dentCenter = center + forward * (portalEntranceDepth * 0.45f);
        float paintRadius = Mathf.Sqrt(halfWidth * halfWidth + halfDepth * halfDepth);
        Bounds paintBounds = new Bounds(
            new Vector3(dentCenter.x, dentCenter.y, portalBounds.center.z),
            new Vector3(paintRadius * 2f, paintRadius * 2f, 0f));

        int width = entranceMaskTexture.width;
        int height = entranceMaskTexture.height;
        int minX = Mathf.Clamp(WorldToPixelX(paintBounds.min.x, width) - 1, 0, width - 1);
        int maxX = Mathf.Clamp(WorldToPixelX(paintBounds.max.x, width) + 1, 0, width - 1);
        int minY = Mathf.Clamp(WorldToPixelY(paintBounds.min.y, height) - 1, 0, height - 1);
        int maxY = Mathf.Clamp(WorldToPixelY(paintBounds.max.y, height) + 1, 0, height - 1);

        for (int y = minY; y <= maxY; y++)
        {
            float worldY = PixelToWorldY(y, height);
            int row = y * width;
            for (int x = minX; x <= maxX; x++)
            {
                float worldX = PixelToWorldX(x, width);
                Vector2 offset = new Vector2(worldX, worldY) - dentCenter;
                float localForward = Vector2.Dot(offset, forward);
                float localSide = Vector2.Dot(offset, side);
                float noise = (ValueNoise(new Vector2(worldX, worldY) * 1.2f) - 0.5f) * portalEntranceEdgeNoise;
                float normalized = Mathf.Sqrt(
                    Mathf.Pow(localSide / halfWidth, 2f) +
                    Mathf.Pow(localForward / halfDepth, 2f)) + noise;
                float strength = (1f - Mathf.SmoothStep(
                    Mathf.Clamp01(1f - portalEntranceSoftness),
                    1f,
                    normalized)) * strengthMultiplier;
                if (strength <= 0f)
                {
                    continue;
                }

                int pixelIndex = row + x;
                float existing = entranceMaskPixels[pixelIndex].r / 255f;
                entranceMaskPixels[pixelIndex] = RevealedPixel(Mathf.Max(existing, strength));
            }
        }
    }

    private bool IsPointInsideRevealArea(RoomCameraTrigger room, Vector3 point)
    {
        return room != null && room.ContainsPoint(point);
    }

    private Bounds CreateRevealBounds(Bounds roomBounds)
    {
        return roomBounds;
    }

    private float ResolveRevealRadius(Vector2 center, Bounds bounds)
    {
        Vector2 min = bounds.min;
        Vector2 max = bounds.max;
        float radius = 0.001f;
        radius = Mathf.Max(radius, Vector2.Distance(center, new Vector2(min.x, min.y)));
        radius = Mathf.Max(radius, Vector2.Distance(center, new Vector2(max.x, min.y)));
        radius = Mathf.Max(radius, Vector2.Distance(center, new Vector2(max.x, max.y)));
        radius = Mathf.Max(radius, Vector2.Distance(center, new Vector2(min.x, max.y)));
        return radius;
    }

    private void EnsureTextures()
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

        DestroyTexture(maskTexture);
        DestroyTexture(entranceMaskTexture);

        maskTexture = CreateMaskTexture("RoomFogRevealMask", resolution, FilterMode.Point);
        entranceMaskTexture = CreateMaskTexture("RoomFogPortalDentMask", resolution, FilterMode.Bilinear);
        maskPixels = new Color32[resolution * resolution];
        entranceMaskPixels = new Color32[resolution * resolution];
    }

    private static Texture2D CreateMaskTexture(string textureName, int resolution, FilterMode filterMode)
    {
        return new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true)
        {
            name = textureName,
            filterMode = filterMode,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
    }

    private void EnsureOverlay()
    {
        if (fogShader == null)
        {
            ResolveDefaultShader();
        }

        if (fogShader == null)
        {
            if (!shaderWarningLogged)
            {
                Debug.LogWarning($"[RoomFogRevealManager] Shader '{ShaderName}' was not found.", this);
                shaderWarningLogged = true;
            }

            SetOverlayVisible(false);
            return;
        }

        if (fogMaterial == null)
        {
            fogMaterial = new Material(fogShader)
            {
                name = "RoomFogOverlayRuntimeMaterial",
                hideFlags = HideFlags.DontSave
            };
        }
        else if (fogMaterial.shader != fogShader)
        {
            fogMaterial.shader = fogShader;
        }

        if (overlayObject == null)
        {
            overlayObject = new GameObject(OverlayObjectName)
            {
                hideFlags = HideFlags.DontSave
            };
            overlayFilter = overlayObject.AddComponent<MeshFilter>();
            overlayRenderer = overlayObject.AddComponent<MeshRenderer>();
        }

        overlayObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        overlayObject.transform.localScale = Vector3.one;

        if (overlayFilter == null)
        {
            overlayFilter = overlayObject.GetComponent<MeshFilter>() ?? overlayObject.AddComponent<MeshFilter>();
        }

        if (overlayRenderer == null)
        {
            overlayRenderer = overlayObject.GetComponent<MeshRenderer>() ?? overlayObject.AddComponent<MeshRenderer>();
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

        overlayRenderer.sharedMaterial = fogMaterial;
        overlayRenderer.sortingLayerID = 0;
        overlayRenderer.sortingOrder = sortingOrder;
    }

    private void ApplyMaterialProperties()
    {
        if (fogMaterial == null || maskTexture == null || entranceMaskTexture == null)
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
            overlayRenderer.enabled = visible && fogEnabled && hasRooms;
        }
    }

    private void DestroyOverlayResources()
    {
        DestroyGameObject(overlayObject);
        overlayObject = null;
        overlayFilter = null;
        overlayRenderer = null;

        DestroyUnityObject(overlayMesh);
        overlayMesh = null;

        DestroyUnityObject(fogMaterial);
        fogMaterial = null;

        DestroyTexture(maskTexture);
        maskTexture = null;
        maskPixels = null;

        DestroyTexture(entranceMaskTexture);
        entranceMaskTexture = null;
        entranceMaskPixels = null;
    }

    private void ResolveDefaultShader()
    {
        if (fogShader == null)
        {
            fogShader = Shader.Find(ShaderName);
        }
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

    private static void ClearPixels(Color32[] pixels)
    {
        Color32 hidden = new Color32(0, 0, 0, 255);
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = hidden;
        }
    }

    private static Color32 RevealedPixel(float strength)
    {
        byte value = (byte)Mathf.Clamp(Mathf.RoundToInt(strength * 255f), 0, 255);
        return new Color32(value, value, value, 255);
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

    private static float Hash21(Vector2 position)
    {
        float hash = Mathf.Sin(Vector2.Dot(position, new Vector2(127.1f, 311.7f))) * 43758.5453f;
        return hash - Mathf.Floor(hash);
    }

    private static RoomCameraTrigger ReadRoomField(object source, string fieldName)
    {
        if (source == null)
        {
            return null;
        }

        FieldInfo field = source.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field != null ? field.GetValue(source) as RoomCameraTrigger : null;
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

            AddBounds(collider.bounds, ref bounds, ref hasBounds);
        }

        Collider[] colliders = target.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            AddBounds(collider.bounds, ref bounds, ref hasBounds);
        }

        return hasBounds;
    }

    private static void AddBounds(Bounds source, ref Bounds target, ref bool hasBounds)
    {
        if (!hasBounds)
        {
            target = source;
            hasBounds = true;
        }
        else
        {
            target.Encapsulate(source);
        }
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

    private static void DestroyTexture(Texture2D texture)
    {
        DestroyUnityObject(texture);
    }

    private static void DestroyGameObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private static void DestroyUnityObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
