using UnityEngine;

[DisallowMultipleComponent]
public sealed class MinimapRoom : MonoBehaviour
{
    [SerializeField] private string roomId;
    [SerializeField] private string displayName;
    [SerializeField] private Vector2Int mapPosition;
    [SerializeField] private Vector2Int mapSize = Vector2Int.one;
    [SerializeField] private MinimapConnection connections;
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
