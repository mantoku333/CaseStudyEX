using UnityEngine;

[AddComponentMenu("UI/Minimap Room")]
[DisallowMultipleComponent]
public sealed class MinimapRoom : MonoBehaviour
{
    [Header("Room Identity")]
    [SerializeField, Tooltip("Unique room id for the minimap. Uses the GameObject name when empty.")]
    private string roomId;

    [SerializeField, Tooltip("Display name shown by tools or debug UI. Uses Room ID when empty.")]
    private string displayName;

    [Header("Map Layout")]
    [SerializeField, Tooltip("Manual map position. X moves left/right, Y moves up/down.")]
    private Vector2Int mapPosition;

    [SerializeField, Tooltip("Manual map size in layout cells. For simple planner workflow, leave this at 1x1 and only adjust X / Y.")]
    private Vector2Int mapSize = Vector2Int.one;

    [HideInInspector, SerializeField]
    private MinimapConnection connections;

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";

    public string RoomId => roomId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? roomId : displayName;
    public Vector2Int MapPosition => mapPosition;
    public Vector2Int MapSize => new Vector2Int(Mathf.Max(1, mapSize.x), Mathf.Max(1, mapSize.y));
    public MinimapConnection Connections => connections;

    public MinimapRoomDefinition Definition => new MinimapRoomDefinition(
        RoomId,
        DisplayName,
        MapPosition,
        MapSize,
        Connections);

    private void OnEnable()
    {
        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.RegisterRoom(this);
        }
    }

    private void OnDisable()
    {
        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.UnregisterRoom(this);
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
        connections = MinimapConnection.None;
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
        connections = MinimapConnection.None;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(playerTag))
        {
            EnterRoom();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            EnterRoom();
        }
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
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject == null)
        {
            return;
        }

        Vector3 playerPosition = playerObject.transform.position;
        Vector2 playerPosition2D = new Vector2(playerPosition.x, playerPosition.y);
        Collider2D[] colliders2D = GetComponents<Collider2D>();
        for (int i = 0; i < colliders2D.Length; i++)
        {
            Collider2D roomCollider = colliders2D[i];
            if (roomCollider != null && roomCollider.enabled && roomCollider.OverlapPoint(playerPosition2D))
            {
                EnterRoom();
                return;
            }
        }

        Collider[] colliders = GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider roomCollider = colliders[i];
            if (roomCollider != null && roomCollider.enabled && roomCollider.bounds.Contains(playerPosition))
            {
                EnterRoom();
                return;
            }
        }
    }
}
