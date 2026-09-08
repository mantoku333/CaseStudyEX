using System.Collections.Generic;
using System.Reflection;
using Unity.Profiling;
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
    private const float RevealFrontStart = -0.12f;
    private const float RevealFrontEnd = 1.12f;
    private const float RevealFieldMin = -0.25f;
    private const float RevealFieldMax = 1.25f;
    private const int RevealFieldMaxByte = 254;
    private const int MinimumOverlaySortingOrder = 100;

    private static RoomFogRevealManager instance;
    private static readonly int RevealFrontPropertyId = Shader.PropertyToID("_RevealFront");
    private static readonly int ConcealFrontPropertyId = Shader.PropertyToID("_ConcealFront");
    private static readonly int PreviousActivePropertyId = Shader.PropertyToID("_PreviousActive");
    private static readonly int PreviousPortalStrengthPropertyId =
        Shader.PropertyToID("_PreviousPortalStrength");
    private static readonly ProfilerMarker RoomMaskUpdateMarker =
        new ProfilerMarker("CaseStudy.RoomFog.UpdateRoomMask");
    private static readonly ProfilerMarker EntranceMaskUpdateMarker =
        new ProfilerMarker("CaseStudy.RoomFog.UpdateEntranceMask");
    private static readonly Vector2[] OverlayUvs =
    {
        new Vector2(0f, 0f),
        new Vector2(1f, 0f),
        new Vector2(1f, 1f),
        new Vector2(0f, 1f)
    };
    private static readonly int[] OverlayTriangles = { 0, 2, 1, 0, 3, 2 };

    [SerializeField] private bool fogEnabled = true;
    [SerializeField, Min(64)] private int textureResolution = 1024;
    [SerializeField, Min(0.01f)] private float targetWorldUnitsPerPixel = 0.08f;
    [SerializeField, Min(64)] private int maximumTextureResolution = 4096;
    [SerializeField, Min(0f)] private float worldPadding = 6f;
    [SerializeField] private Shader fogShader;

    [SerializeField] private Color fogColor = Color.black;
    [SerializeField, Range(0f, 1f)] private float fogAlpha = 1f;
    [SerializeField, Range(0.01f, 1f)] private float edgeSoftness = 0.22f;
    [SerializeField, Range(0f, 1f)] private float noiseStrength = 0.18f;
    [SerializeField, Min(0.1f)] private float noiseScale = 0.32f;
    [SerializeField, Min(MinimumOverlaySortingOrder)] private int sortingOrder = 30000;
    [SerializeField] private float overlayZ = -1f;

    [SerializeField, Min(0.01f)] private float revealDuration = 1.6f;
    [SerializeField, Min(0.01f)] private float concealDuration = 1.8f;
    [SerializeField, Range(0f, 0.5f)] private float revealNoiseStrength = 0.18f;

    [SerializeField] private bool revealPortalEntrances = true;

    private readonly List<RoomCameraTrigger> rooms = new List<RoomCameraTrigger>();
    private readonly List<PortalDent> portalDents = new List<PortalDent>();
    private readonly Vector3[] overlayVertices = new Vector3[4];
    private RoomCameraTrigger currentRoom;
    private RoomCameraTrigger entranceSourceRoom;
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
    private bool refreshRequested = true;
    private bool roomMaskDirty = true;
    private bool entranceMaskDirty = true;
    private float appliedRevealFront = float.NaN;
    private float appliedConcealFront = float.NaN;
    private float appliedPreviousActive = float.NaN;
    private float appliedPreviousPortalStrength = float.NaN;
    private Transform cachedPlayerTransform;

#if UNITY_EDITOR
    private bool editorRefreshEventsSubscribed;
#endif

    public int Priority => 260;
    public static bool FogEnabled => instance != null && instance.fogEnabled;

    private struct PortalDent
    {
        public GameObject PortalObject;
        public RoomCameraTrigger RoomA;
        public RoomCameraTrigger RoomB;
        public Collider2D[] Colliders2D;
        public Collider[] Colliders;
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
#if UNITY_EDITOR
        else
        {
            SubscribeEditorRefreshEvents();
        }
#endif

        RefreshForCurrentScene(true);
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            SaveManager.UnregisterModule(this);
            RoomCameraTrigger.ActiveRoomChanged -= HandleActiveRoomChanged;
        }
#if UNITY_EDITOR
        else
        {
            UnsubscribeEditorRefreshEvents();
        }
#endif

        if (instance == this)
        {
            instance = null;
        }

        DestroyOverlayResources();
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        UnsubscribeEditorRefreshEvents();
#endif

        if (instance == this)
        {
            instance = null;
        }

        DestroyOverlayResources();
    }

    private void OnValidate()
    {
        textureResolution = Mathf.Max(64, textureResolution);
        targetWorldUnitsPerPixel = Mathf.Max(0.01f, targetWorldUnitsPerPixel);
        maximumTextureResolution = Mathf.Max(textureResolution, maximumTextureResolution);
        revealDuration = Mathf.Max(0.01f, revealDuration);
        concealDuration = Mathf.Max(0.01f, concealDuration);
        edgeSoftness = Mathf.Max(0.01f, edgeSoftness);
        sortingOrder = Mathf.Max(MinimumOverlaySortingOrder, sortingOrder);
        ResolveDefaultShader();
        RequestFullRefresh();
    }

    private void RequestFullRefresh()
    {
        refreshRequested = true;
        MarkAllMasksDirty();
    }

    private void MarkAllMasksDirty()
    {
        roomMaskDirty = true;
        entranceMaskDirty = true;
    }

#if UNITY_EDITOR
    private void SubscribeEditorRefreshEvents()
    {
        if (editorRefreshEventsSubscribed)
        {
            return;
        }

        UnityEditor.EditorApplication.hierarchyChanged += HandleEditorHierarchyChanged;
        UnityEditor.Undo.undoRedoPerformed += HandleEditorUndoRedo;
        UnityEditor.Undo.postprocessModifications += HandleEditorModifications;
        editorRefreshEventsSubscribed = true;
    }

    private void UnsubscribeEditorRefreshEvents()
    {
        if (!editorRefreshEventsSubscribed)
        {
            return;
        }

        UnityEditor.EditorApplication.hierarchyChanged -= HandleEditorHierarchyChanged;
        UnityEditor.Undo.undoRedoPerformed -= HandleEditorUndoRedo;
        UnityEditor.Undo.postprocessModifications -= HandleEditorModifications;
        editorRefreshEventsSubscribed = false;
    }

    private void HandleEditorHierarchyChanged()
    {
        RequestFullRefresh();
    }

    private void HandleEditorUndoRedo()
    {
        RequestFullRefresh();
    }

    private UnityEditor.UndoPropertyModification[] HandleEditorModifications(
        UnityEditor.UndoPropertyModification[] modifications)
    {
        for (int i = 0; i < modifications.Length; i++)
        {
            UnityEngine.Object target = modifications[i].currentValue.target;
            if (IsFogPreviewTarget(target))
            {
                RequestFullRefresh();
                break;
            }
        }

        return modifications;
    }

    private bool IsFogPreviewTarget(UnityEngine.Object target)
    {
        if (target == this || target == gameObject)
        {
            return true;
        }

        Component component = target as Component;
        if (component == null || component.gameObject.scene != gameObject.scene)
        {
            return false;
        }

        if (component is RoomCameraTrigger ||
            component is RoomCameraPortal ||
            component is VerticalRoomCameraPortal ||
            component is FallRoomCameraPortal)
        {
            return true;
        }

        Transform modifiedTransform = component.transform;
        for (int i = 0; i < rooms.Count; i++)
        {
            RoomCameraTrigger room = rooms[i];
            if (room == null)
            {
                continue;
            }

            Transform roomTransform = room.transform;
            if (roomTransform.IsChildOf(modifiedTransform) ||
                modifiedTransform.IsChildOf(roomTransform))
            {
                return true;
            }
        }

        for (int i = 0; i < portalDents.Count; i++)
        {
            GameObject portalObject = portalDents[i].PortalObject;
            if (portalObject == null)
            {
                continue;
            }

            Transform portalTransform = portalObject.transform;
            if (portalTransform.IsChildOf(modifiedTransform) ||
                modifiedTransform.IsChildOf(portalTransform))
            {
                return true;
            }
        }

        if (!(component is Transform))
        {
            return false;
        }

        GameObject targetObject = component.gameObject;
        return targetObject.GetComponent<RoomCameraTrigger>() != null ||
            targetObject.GetComponent<RoomCameraPortal>() != null ||
            targetObject.GetComponent<VerticalRoomCameraPortal>() != null ||
            targetObject.GetComponent<FallRoomCameraPortal>() != null;
    }
#endif

    private void Update()
    {
        if (refreshRequested || !managedScene.IsValid() || managedScene != gameObject.scene)
        {
            RefreshForCurrentScene(Application.isPlaying);
            return;
        }

        if (!fogEnabled || !hasRooms)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            return;
        }

        if (Time.unscaledTime >= nextRoomRefreshTime)
        {
            nextRoomRefreshTime = Time.unscaledTime + RoomRefreshInterval;
            RevealCurrentRoomFromRuntimeState();
        }

        float revealProgress = ResolveRevealProgress();
        float concealProgress = ResolveConcealProgress();
        FlushDirtyMasks(revealProgress, concealProgress);
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
        refreshRequested = false;
        managedScene = gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene();
        cachedPlayerTransform = null;
        entranceSourceRoom = null;
        ResolveDefaultShader();
        CollectRooms();

        if (!hasRooms)
        {
            DestroyOverlayResources();
            return;
        }

        if (revealCurrentRoom)
        {
            RevealCurrentRoomFromRuntimeState();
        }

        RefreshTrackedRoomGeometry();
        MarkAllMasksDirty();

        if (!fogEnabled)
        {
            // Keep the current-room timing above in sync while avoiding the
            // large CPU/GPU mask allocation for an invisible effect.
            portalDents.Clear();
            DestroyOverlayResources();
            return;
        }

        CollectPortalDents();

        EnsureTextures();
        EnsureOverlay();
        ApplyMaterialProperties();
        FlushDirtyMasks(ResolveRevealProgress(), ResolveConcealProgress());
        SetOverlayVisible(true);
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
                RoomB = roomB,
                Colliders2D = portal.GetComponentsInChildren<Collider2D>(true),
                Colliders = portal.GetComponentsInChildren<Collider>(true)
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
            SetEntranceSourceRoom(room);
            SetCurrentRoom(room, false);
        }
    }

    private void SetEntranceSourceRoom(RoomCameraTrigger room)
    {
        if (room == null ||
            room.gameObject.scene != managedScene ||
            entranceSourceRoom == room)
        {
            return;
        }

        entranceSourceRoom = room;
        entranceMaskDirty = true;
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
        MarkAllMasksDirty();
    }

    private void RefreshTrackedRoomGeometry()
    {
        if (revealingRoom != null &&
            revealingRoom.gameObject.scene == managedScene &&
            revealingRoom.TryGetAreaBounds(out Bounds currentBounds))
        {
            revealingBounds = currentBounds;
            revealingPaintBounds = CreateRevealBounds(currentBounds);
            revealRadius = ResolveRevealRadius(revealCenter, revealingPaintBounds);
        }

        if (concealingRoom != null &&
            concealingRoom.gameObject.scene == managedScene &&
            concealingRoom.TryGetAreaBounds(out Bounds previousBounds))
        {
            concealingBounds = previousBounds;
            concealingPaintBounds = CreateRevealBounds(previousBounds);
            concealRadius = ResolveRevealRadius(concealCenter, concealingPaintBounds);
        }
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

    private void FlushDirtyMasks(float revealProgress, float concealProgress)
    {
        ApplyTransitionProgress(revealProgress, concealProgress);

        if (!roomMaskDirty && !entranceMaskDirty)
        {
            return;
        }

        if (roomMaskDirty && maskTexture != null && maskPixels != null)
        {
            using (RoomMaskUpdateMarker.Auto())
            {
                // R stores the current-room reveal threshold, G the previous-room
                // conceal threshold. 255 is reserved for pixels outside a room.
                ClearTransitionPixels(maskPixels);

                if (fogEnabled)
                {
                    if (concealingRoom != null && !concealComplete)
                    {
                        PaintRoomField(
                            concealingRoom,
                            concealingBounds,
                            concealingPaintBounds,
                            concealCenter,
                            concealRadius,
                            false);
                    }

                    if (revealingRoom != null)
                    {
                        PaintRoomField(
                            revealingRoom,
                            revealingBounds,
                            revealingPaintBounds,
                            revealCenter,
                            revealRadius,
                            true);
                    }
                }

                maskTexture.SetPixels32(maskPixels);
                maskTexture.Apply(false, false);
            }

            roomMaskDirty = false;
        }

        if (entranceMaskDirty && entranceMaskTexture != null && entranceMaskPixels != null)
        {
            using (EntranceMaskUpdateMarker.Auto())
            {
                // Portal strengths use R=current and G=previous so their fade can
                // be animated in the shader without uploading another texture.
                ClearPixels(entranceMaskPixels);

                if (fogEnabled)
                {
                    PaintPortalDents(entranceSourceRoom, 1f, true);
                    if (concealingRoom != null && !concealComplete)
                    {
                        PaintPortalDents(concealingRoom, 1f, false);
                    }
                }

                entranceMaskTexture.SetPixels32(entranceMaskPixels);
                entranceMaskTexture.Apply(false, false);
            }

            entranceMaskDirty = false;
        }
    }

    private void ApplyTransitionProgress(float revealProgress, float concealProgress)
    {
        if (fogMaterial == null)
        {
            return;
        }

        float revealFront = Mathf.Lerp(RevealFrontStart, RevealFrontEnd, revealProgress);
        float concealFront = Mathf.Lerp(RevealFrontEnd, RevealFrontStart, concealProgress);
        float previousActive = concealingRoom != null && !concealComplete ? 1f : 0f;
        float previousPortalStrength = previousActive * (1f - concealProgress);
        if (!Mathf.Approximately(revealFront, appliedRevealFront))
        {
            fogMaterial.SetFloat(RevealFrontPropertyId, revealFront);
            appliedRevealFront = revealFront;
        }

        if (!Mathf.Approximately(concealFront, appliedConcealFront))
        {
            fogMaterial.SetFloat(ConcealFrontPropertyId, concealFront);
            appliedConcealFront = concealFront;
        }

        if (!Mathf.Approximately(previousActive, appliedPreviousActive))
        {
            fogMaterial.SetFloat(PreviousActivePropertyId, previousActive);
            appliedPreviousActive = previousActive;
        }

        if (!Mathf.Approximately(previousPortalStrength, appliedPreviousPortalStrength))
        {
            fogMaterial.SetFloat(PreviousPortalStrengthPropertyId, previousPortalStrength);
            appliedPreviousPortalStrength = previousPortalStrength;
        }
    }

    private void PaintRoomField(
        RoomCameraTrigger room,
        Bounds roomBounds,
        Bounds paintBounds,
        Vector2 center,
        float radius,
        bool writeRevealChannel)
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
                byte threshold = EncodeRevealThreshold(noisyDistance);
                int pixelIndex = row + x;
                Color32 pixel = maskPixels[pixelIndex];
                if (writeRevealChannel)
                {
                    pixel.r = threshold;
                }
                else
                {
                    pixel.g = threshold;
                }

                maskPixels[pixelIndex] = pixel;
            }
        }
    }

    private void PaintPortalDents(
        RoomCameraTrigger sourceRoom,
        float strengthMultiplier,
        bool writeCurrentChannel)
    {
        if (!revealPortalEntrances ||
            sourceRoom == null ||
            strengthMultiplier <= 0f)
        {
            return;
        }

        for (int i = 0; i < portalDents.Count; i++)
        {
            PortalDent dent = portalDents[i];
            if (dent.RoomA != sourceRoom && dent.RoomB != sourceRoom)
            {
                continue;
            }

            PaintPortalDent(
                dent,
                strengthMultiplier,
                writeCurrentChannel);
        }
    }

    private void PaintPortalDent(
        PortalDent dent,
        float strengthMultiplier,
        bool writeCurrentChannel)
    {
        if (dent.PortalObject == null ||
            !TryGetObjectBounds(dent.Colliders2D, dent.Colliders, out Bounds portalBounds))
        {
            return;
        }

        int width = entranceMaskTexture.width;
        int height = entranceMaskTexture.height;
        int minX = Mathf.Clamp(WorldToPixelX(portalBounds.min.x, width) - 1, 0, width - 1);
        int maxX = Mathf.Clamp(WorldToPixelX(portalBounds.max.x, width) + 1, 0, width - 1);
        int minY = Mathf.Clamp(WorldToPixelY(portalBounds.min.y, height) - 1, 0, height - 1);
        int maxY = Mathf.Clamp(WorldToPixelY(portalBounds.max.y, height) + 1, 0, height - 1);
        byte value = (byte)Mathf.Clamp(Mathf.RoundToInt(strengthMultiplier * 255f), 0, 255);

        for (int y = minY; y <= maxY; y++)
        {
            float worldY = PixelToWorldY(y, height);
            int row = y * width;
            for (int x = minX; x <= maxX; x++)
            {
                float worldX = PixelToWorldX(x, width);
                Vector3 worldPoint = new Vector3(worldX, worldY, portalBounds.center.z);
                if (!IsPointInsidePortal(dent, worldPoint) ||
                    (!IsPointInsideRevealArea(dent.RoomA, worldPoint) &&
                     !IsPointInsideRevealArea(dent.RoomB, worldPoint)))
                {
                    continue;
                }

                int pixelIndex = row + x;
                Color32 pixel = entranceMaskPixels[pixelIndex];
                if (writeCurrentChannel)
                {
                    if (value > pixel.r)
                    {
                        pixel.r = value;
                    }
                }
                else if (value > pixel.g)
                {
                    pixel.g = value;
                }

                pixel.a = 255;
                entranceMaskPixels[pixelIndex] = pixel;
            }
        }
    }

    private static bool IsPointInsidePortal(PortalDent dent, Vector3 worldPoint)
    {
        if (dent.Colliders2D != null)
        {
            Vector2 point2D = new Vector2(worldPoint.x, worldPoint.y);
            for (int i = 0; i < dent.Colliders2D.Length; i++)
            {
                Collider2D collider = dent.Colliders2D[i];
                if (collider != null && collider.enabled && collider.OverlapPoint(point2D))
                {
                    return true;
                }
            }
        }

        if (dent.Colliders != null)
        {
            for (int i = 0; i < dent.Colliders.Length; i++)
            {
                Collider collider = dent.Colliders[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                Vector3 closestPoint = collider.ClosestPoint(worldPoint);
                if ((closestPoint - worldPoint).sqrMagnitude <= 0.0001f)
                {
                    return true;
                }
            }
        }

        return false;
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
        int resolution = ResolveTextureResolution();
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
        MarkAllMasksDirty();
    }

    private int ResolveTextureResolution()
    {
        int minimumResolution = Mathf.Max(64, textureResolution);
        int maximumResolution = Mathf.Max(minimumResolution, maximumTextureResolution);
        if (!hasRooms || targetWorldUnitsPerPixel <= 0.01f)
        {
            return minimumResolution;
        }

        float longestWorldAxis = Mathf.Max(worldBounds.size.x, worldBounds.size.y);
        int worldResolution = Mathf.CeilToInt(longestWorldAxis / targetWorldUnitsPerPixel);
        return Mathf.Clamp(Mathf.NextPowerOfTwo(worldResolution), minimumResolution, maximumResolution);
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
            ResetAppliedTransitionProperties();
        }
        else if (fogMaterial.shader != fogShader)
        {
            fogMaterial.shader = fogShader;
            ResetAppliedTransitionProperties();
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

        overlayVertices[0] = new Vector3(min.x, min.y, overlayZ);
        overlayVertices[1] = new Vector3(max.x, min.y, overlayZ);
        overlayVertices[2] = new Vector3(max.x, max.y, overlayZ);
        overlayVertices[3] = new Vector3(min.x, max.y, overlayZ);

        overlayMesh.Clear();
        overlayMesh.vertices = overlayVertices;
        overlayMesh.uv = OverlayUvs;
        overlayMesh.triangles = OverlayTriangles;
        overlayMesh.RecalculateBounds();
        overlayFilter.sharedMesh = overlayMesh;

        overlayRenderer.sharedMaterial = fogMaterial;
        overlayRenderer.sortingLayerID = 0;
        overlayRenderer.sortingOrder = Mathf.Max(MinimumOverlaySortingOrder, sortingOrder);
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
        if (overlayRenderer != null)
        {
            overlayRenderer.enabled = false;
        }

        DestroyGameObject(overlayObject);
        overlayObject = null;
        overlayFilter = null;
        overlayRenderer = null;

        DestroyUnityObject(overlayMesh);
        overlayMesh = null;

        DestroyUnityObject(fogMaterial);
        fogMaterial = null;
        ResetAppliedTransitionProperties();

        DestroyTexture(maskTexture);
        maskTexture = null;
        maskPixels = null;

        DestroyTexture(entranceMaskTexture);
        entranceMaskTexture = null;
        entranceMaskPixels = null;
        MarkAllMasksDirty();
    }

    private void ResetAppliedTransitionProperties()
    {
        appliedRevealFront = float.NaN;
        appliedConcealFront = float.NaN;
        appliedPreviousActive = float.NaN;
        appliedPreviousPortalStrength = float.NaN;
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
        return Mathf.FloorToInt(normalized * width);
    }

    private int WorldToPixelY(float worldY, int height)
    {
        float normalized = Mathf.InverseLerp(worldBounds.min.y, worldBounds.max.y, worldY);
        return Mathf.FloorToInt(normalized * height);
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

    private static void ClearTransitionPixels(Color32[] pixels)
    {
        Color32 outsideRoom = new Color32(255, 255, 0, 255);
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = outsideRoom;
        }
    }

    private static byte EncodeRevealThreshold(float noisyDistance)
    {
        float normalized = Mathf.InverseLerp(RevealFieldMin, RevealFieldMax, noisyDistance);
        return (byte)Mathf.Clamp(
            Mathf.RoundToInt(normalized * RevealFieldMaxByte),
            0,
            RevealFieldMaxByte);
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

    private static bool TryGetObjectBounds(
        Collider2D[] colliders2D,
        Collider[] colliders,
        out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (colliders2D != null)
        {
            for (int i = 0; i < colliders2D.Length; i++)
            {
                Collider2D collider = colliders2D[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                AddBounds(collider.bounds, ref bounds, ref hasBounds);
            }
        }

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                AddBounds(collider.bounds, ref bounds, ref hasBounds);
            }
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

        if (cachedPlayerTransform != null && cachedPlayerTransform.gameObject.activeInHierarchy)
        {
            position = cachedPlayerTransform.position;
            return true;
        }

        cachedPlayerTransform = null;

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
            cachedPlayerTransform = taggedPlayer.transform;
            position = cachedPlayerTransform.position;
            return true;
        }

        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Exclude);
        if (player == null)
        {
            return false;
        }

        cachedPlayerTransform = player.transform;
        position = cachedPlayerTransform.position;
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
