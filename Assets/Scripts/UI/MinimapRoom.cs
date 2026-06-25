using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("UI/Minimap Room")]
[DisallowMultipleComponent]
public sealed class MinimapRoom : MonoBehaviour
{
    private static readonly Vector2 DefaultFreeformRoomSize = new Vector2(1.5f, 1f);
    private static readonly List<MinimapRoom> OccupiedRooms = new List<MinimapRoom>();
    private static readonly List<MinimapRoom> ActiveRooms = new List<MinimapRoom>();

    [Header("Room Identity")]
    [SerializeField, Tooltip("Unique room id for the minimap. Uses the GameObject name when empty.")]
    private string roomId;

    [SerializeField, Tooltip("Display name shown by tools or debug UI. Uses Room ID when empty.")]
    private string displayName;

    [SerializeField, InspectorName("ボスエリア"), Tooltip("有効にすると、このエリアを下のボスエリア色で表示します。")]
    private bool isBossRoom;

    [SerializeField, InspectorName("ボスエリアの色"), Tooltip("ミニマップで使用するボスエリアの色です。")]
    private Color bossRoomColor = new Color(1f, 0.12f, 0.12f, 1f);

    [Header("Map Layout")]
    [SerializeField, Tooltip("Manual map position. X moves left/right, Y moves up/down.")]
    private Vector2Int mapPosition;

    [SerializeField, Tooltip("Manual map size in layout cells. For simple planner workflow, leave this at 1x1 and only adjust X / Y.")]
    private Vector2Int mapSize = Vector2Int.one;

    [SerializeField, Tooltip("Use freeform board coordinates instead of the legacy grid layout.")]
    private bool usesFreeformLayout;

    [SerializeField, Tooltip("Top-left board position on the minimap editor board.")]
    private Vector2 areaPosition;

    [SerializeField, Tooltip("Freeform room size on the minimap editor board.")]
    private Vector2 areaSize = new Vector2(1.5f, 1f);

    [HideInInspector, SerializeField]
    private MinimapConnection connections;

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";

    private readonly List<Collider2D> roomCollider2DBuffer = new List<Collider2D>();
    private readonly List<Collider> roomColliderBuffer = new List<Collider>();
    private int overlapCount;

    public string RoomId => roomId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? roomId : displayName;
    public Vector2Int MapPosition => mapPosition;
    public Vector2Int MapSize => new Vector2Int(Mathf.Max(1, mapSize.x), Mathf.Max(1, mapSize.y));
    public MinimapConnection Connections => connections;
    public static IReadOnlyList<MinimapRoom> RegisteredRooms => ActiveRooms;
    public bool UsesFreeformLayout => usesFreeformLayout;
    public Vector2 AreaPosition => usesFreeformLayout ? areaPosition : LegacyGridToBoardPosition(mapPosition, MapSize);
    public Vector2 AreaSize => usesFreeformLayout
        ? new Vector2(Mathf.Max(0.1f, areaSize.x), Mathf.Max(0.1f, areaSize.y))
        : new Vector2(MapSize.x, MapSize.y);
    public bool IsBossRoom => isBossRoom;
    public Color BossRoomColor => bossRoomColor;

    public MinimapRoomDefinition Definition => new MinimapRoomDefinition(
        RoomId,
        DisplayName,
        MapPosition,
        MapSize,
        Connections,
        AreaPosition,
        AreaSize,
        usesFreeformLayout,
        isBossRoom,
        bossRoomColor);

    private void OnEnable()
    {
        if (!ActiveRooms.Contains(this))
        {
            ActiveRooms.Add(this);
        }

        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.RegisterRoom(this);
        }
    }

    private void OnDisable()
    {
        ActiveRooms.Remove(this);

        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.UnregisterRoom(this);
        }

        if (overlapCount > 0)
        {
            OccupiedRooms.Remove(this);
            overlapCount = 0;
            ActivateBestOccupiedRoom();
        }
    }

    private void Start()
    {
        EnterIfPlayerAlreadyInside();
    }

    private void Reset()
    {
        ApplyEditorFriendlyDefaults();
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            roomId = gameObject.name;
        }

        if (mapSize.x <= 0 || mapSize.y <= 0)
        {
            mapSize = new Vector2Int(Mathf.Max(1, mapSize.x), Mathf.Max(1, mapSize.y));
        }

        if (areaSize.x <= 0f || areaSize.y <= 0f)
        {
            areaSize = new Vector2(Mathf.Max(0.1f, areaSize.x), Mathf.Max(0.1f, areaSize.y));
        }
    }

    public void Configure(MinimapRoomDefinition definition, string playerTagName = "Player")
    {
        if (definition == null)
        {
            return;
        }

        roomId = definition.RoomId;
        displayName = definition.DisplayName;
        mapPosition = definition.MapPosition;
        mapSize = definition.MapSize;
        connections = definition.Connections;
        usesFreeformLayout = definition.UsesFreeformLayout;
        areaPosition = definition.AreaPosition;
        areaSize = definition.AreaSize;
        isBossRoom = definition.IsBossRoom;
        bossRoomColor = definition.BossRoomColor;
        playerTag = string.IsNullOrWhiteSpace(playerTagName) ? "Player" : playerTagName;

        if (isActiveAndEnabled && MinimapManager.Instance != null)
        {
            MinimapManager.Instance.RegisterRoom(this);
        }
    }

    // Friendly defaults for planners: use the trigger name as the id
    // and start with a simple wide room shape.
    public void ApplyEditorFriendlyDefaults()
    {
        roomId = gameObject.name;
        displayName = gameObject.name;
        mapPosition = Vector2Int.zero;
        mapSize = Vector2Int.one;
        usesFreeformLayout = true;
        areaPosition = Vector2.zero;
        areaSize = DefaultFreeformRoomSize;
        connections = MinimapConnection.None;
        isBossRoom = false;
        bossRoomColor = new Color(1f, 0.12f, 0.12f, 1f);
    }

    // Shared entry point used by editor tools and scene bootstrap code.
    // Room connections are computed automatically by MinimapManager.
    public void ConfigureAuthoringFields(
        string nextRoomId,
        string nextDisplayName,
        Vector2Int nextMapPosition,
        Vector2Int nextMapSize)
    {
        roomId = string.IsNullOrWhiteSpace(nextRoomId) ? gameObject.name : nextRoomId.Trim();
        displayName = string.IsNullOrWhiteSpace(nextDisplayName) ? roomId : nextDisplayName.Trim();
        mapPosition = nextMapPosition;
        mapSize = new Vector2Int(Mathf.Max(1, nextMapSize.x), Mathf.Max(1, nextMapSize.y));
        usesFreeformLayout = false;
        areaPosition = new Vector2(mapPosition.x, mapPosition.y);
        areaSize = new Vector2(mapSize.x, mapSize.y);
        connections = MinimapConnection.None;
    }

    public void ConfigureFreeformAuthoringFields(
        string nextRoomId,
        string nextDisplayName,
        Vector2 nextAreaPosition,
        Vector2 nextAreaSize)
    {
        roomId = string.IsNullOrWhiteSpace(nextRoomId) ? gameObject.name : nextRoomId.Trim();
        displayName = string.IsNullOrWhiteSpace(nextDisplayName) ? roomId : nextDisplayName.Trim();
        usesFreeformLayout = true;
        areaPosition = nextAreaPosition;
        areaSize = new Vector2(Mathf.Max(0.1f, nextAreaSize.x), Mathf.Max(0.1f, nextAreaSize.y));
        mapPosition = new Vector2Int(
            Mathf.RoundToInt(areaPosition.x),
            Mathf.RoundToInt(areaPosition.y));
        mapSize = new Vector2Int(
            Mathf.Max(1, Mathf.RoundToInt(areaSize.x)),
            Mathf.Max(1, Mathf.RoundToInt(areaSize.y)));
        connections = MinimapConnection.None;
    }

    private static Vector2 LegacyGridToBoardPosition(Vector2Int legacyPosition, Vector2Int legacySize)
    {
        return new Vector2(
            legacyPosition.x,
            -(legacyPosition.y + legacySize.y));
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(playerTag))
        {
            HandlePlayerEntered();
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag(playerTag))
        {
            HandlePlayerExited();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            HandlePlayerEntered();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            HandlePlayerExited();
        }
    }

    private void HandlePlayerEntered()
    {
        overlapCount++;
        if (overlapCount != 1)
        {
            return;
        }

        OccupiedRooms.Remove(this);
        OccupiedRooms.Add(this);
        EnterRoom();
    }

    private void HandlePlayerExited()
    {
        if (overlapCount <= 0)
        {
            return;
        }

        overlapCount--;
        if (overlapCount != 0)
        {
            return;
        }

        OccupiedRooms.Remove(this);
        ActivateBestOccupiedRoom();
    }

    private void EnterRoom()
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            Debug.LogWarning($"[{name}] MinimapRoom roomId is empty.", this);
            return;
        }

        if (MinimapManager.Instance == null)
        {
            return;
        }

        MinimapManager.Instance.EnterRoom(roomId);
    }

    private void EnterIfPlayerAlreadyInside()
    {
        GameObject playerObject = global::PlayerReferenceCache.GetGameObject(playerTag);
        if (playerObject == null)
        {
            return;
        }

        Vector3 playerPosition = playerObject.transform.position;
        Vector2 playerPosition2D = new Vector2(playerPosition.x, playerPosition.y);
        roomCollider2DBuffer.Clear();
        GetComponents(roomCollider2DBuffer);
        for (int i = 0; i < roomCollider2DBuffer.Count; i++)
        {
            Collider2D roomCollider = roomCollider2DBuffer[i];
            if (roomCollider != null && roomCollider.enabled && roomCollider.OverlapPoint(playerPosition2D))
            {
                HandlePlayerEntered();
                return;
            }
        }

        roomColliderBuffer.Clear();
        GetComponents(roomColliderBuffer);
        for (int i = 0; i < roomColliderBuffer.Count; i++)
        {
            Collider roomCollider = roomColliderBuffer[i];
            if (roomCollider != null && roomCollider.enabled && roomCollider.bounds.Contains(playerPosition))
            {
                HandlePlayerEntered();
                return;
            }
        }
    }

    private static void ActivateBestOccupiedRoom()
    {
        for (int i = OccupiedRooms.Count - 1; i >= 0; i--)
        {
            MinimapRoom room = OccupiedRooms[i];
            if (room == null || !room.isActiveAndEnabled || room.overlapCount <= 0)
            {
                OccupiedRooms.RemoveAt(i);
                continue;
            }

            room.EnterRoom();
            return;
        }

        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.ClearCurrentRoom();
        }
    }
}
