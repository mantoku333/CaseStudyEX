using System;
using UnityEngine;

[Serializable]
public sealed class MinimapRoomDefinition
{
    [SerializeField] private string roomId;
    [SerializeField] private string displayName;
    [SerializeField] private Vector2Int mapPosition;
    [SerializeField] private Vector2Int mapSize = Vector2Int.one;
    [SerializeField] private MinimapConnection connections;
    [SerializeField] private bool usesFreeformLayout;
    [SerializeField] private Vector2 areaPosition;
    [SerializeField] private Vector2 areaSize = Vector2.one;

    public string RoomId => roomId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? roomId : displayName;
    public Vector2Int MapPosition => mapPosition;
    public Vector2Int MapSize => new Vector2Int(Mathf.Max(1, mapSize.x), Mathf.Max(1, mapSize.y));
    public MinimapConnection Connections => connections;
    public bool UsesFreeformLayout => usesFreeformLayout;
    public Vector2 AreaPosition => usesFreeformLayout ? areaPosition : LegacyGridToBoardPosition(mapPosition, MapSize);
    public Vector2 AreaSize => usesFreeformLayout
        ? new Vector2(Mathf.Max(0.1f, areaSize.x), Mathf.Max(0.1f, areaSize.y))
        : new Vector2(MapSize.x, MapSize.y);
    public Vector2 AreaCenter => AreaPosition + (AreaSize * 0.5f);

    public MinimapRoomDefinition()
    {
        roomId = string.Empty;
        displayName = string.Empty;
        mapPosition = Vector2Int.zero;
        mapSize = Vector2Int.one;
        connections = MinimapConnection.None;
        usesFreeformLayout = false;
        areaPosition = Vector2.zero;
        areaSize = Vector2.one;
    }

    public MinimapRoomDefinition(
        string roomId,
        string displayName,
        Vector2Int mapPosition,
        Vector2Int mapSize,
        MinimapConnection connections)
    {
        this.roomId = roomId;
        this.displayName = displayName;
        this.mapPosition = mapPosition;
        this.mapSize = new Vector2Int(Mathf.Max(1, mapSize.x), Mathf.Max(1, mapSize.y));
        this.connections = connections;
        usesFreeformLayout = false;
        areaPosition = new Vector2(mapPosition.x, mapPosition.y);
        areaSize = new Vector2(this.mapSize.x, this.mapSize.y);
    }

    public MinimapRoomDefinition(
        string roomId,
        string displayName,
        Vector2Int mapPosition,
        Vector2Int mapSize,
        MinimapConnection connections,
        Vector2 areaPosition,
        Vector2 areaSize,
        bool usesFreeformLayout)
    {
        this.roomId = roomId;
        this.displayName = displayName;
        this.mapPosition = mapPosition;
        this.mapSize = new Vector2Int(Mathf.Max(1, mapSize.x), Mathf.Max(1, mapSize.y));
        this.connections = connections;
        this.usesFreeformLayout = usesFreeformLayout;
        this.areaPosition = areaPosition;
        this.areaSize = new Vector2(Mathf.Max(0.1f, areaSize.x), Mathf.Max(0.1f, areaSize.y));
    }

    public MinimapRoomDefinition WithConnections(MinimapConnection nextConnections)
    {
        return new MinimapRoomDefinition(
            roomId,
            displayName,
            mapPosition,
            mapSize,
            nextConnections,
            AreaPosition,
            AreaSize,
            usesFreeformLayout);
    }

    private static Vector2 LegacyGridToBoardPosition(Vector2Int legacyPosition, Vector2Int legacySize)
    {
        return new Vector2(
            legacyPosition.x,
            -(legacyPosition.y + legacySize.y));
    }
}
