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

    public string RoomId => roomId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? roomId : displayName;
    public Vector2Int MapPosition => mapPosition;
    public Vector2Int MapSize => new Vector2Int(Mathf.Max(1, mapSize.x), Mathf.Max(1, mapSize.y));
    public MinimapConnection Connections => connections;

    public MinimapRoomDefinition()
    {
        roomId = string.Empty;
        displayName = string.Empty;
        mapPosition = Vector2Int.zero;
        mapSize = Vector2Int.one;
        connections = MinimapConnection.None;
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
    }
}
