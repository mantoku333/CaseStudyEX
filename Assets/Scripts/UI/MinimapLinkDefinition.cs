using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MinimapLinkDefinition
{
    [SerializeField] private string linkId;
    [SerializeField] private string fromRoomId;
    [SerializeField] private string toRoomId;
    [SerializeField] private MinimapLinkType linkType = MinimapLinkType.Line;
    [SerializeField] private List<Vector2Int> pathPoints = new List<Vector2Int>();

    public string LinkId => string.IsNullOrWhiteSpace(linkId)
        ? BuildFallbackId(fromRoomId, toRoomId)
        : linkId;

    public string FromRoomId => fromRoomId;
    public string ToRoomId => toRoomId;
    public MinimapLinkType LinkType => linkType;
    public IReadOnlyList<Vector2Int> PathPoints => pathPoints;
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(fromRoomId) &&
        !string.IsNullOrWhiteSpace(toRoomId) &&
        !string.Equals(fromRoomId, toRoomId, StringComparison.Ordinal);

    public MinimapLinkDefinition()
    {
        linkId = string.Empty;
        fromRoomId = string.Empty;
        toRoomId = string.Empty;
        linkType = MinimapLinkType.Line;
        pathPoints = new List<Vector2Int>();
    }

    public MinimapLinkDefinition(
        string linkId,
        string fromRoomId,
        string toRoomId,
        MinimapLinkType linkType,
        IEnumerable<Vector2Int> pathPoints)
    {
        this.linkId = string.IsNullOrWhiteSpace(linkId)
            ? BuildFallbackId(fromRoomId, toRoomId)
            : linkId.Trim();
        this.fromRoomId = string.IsNullOrWhiteSpace(fromRoomId) ? string.Empty : fromRoomId.Trim();
        this.toRoomId = string.IsNullOrWhiteSpace(toRoomId) ? string.Empty : toRoomId.Trim();
        this.linkType = linkType;
        this.pathPoints = ClonePathPoints(pathPoints);
    }

    public MinimapLinkDefinition Clone()
    {
        return new MinimapLinkDefinition(linkId, fromRoomId, toRoomId, linkType, pathPoints);
    }

    public MinimapLinkDefinition WithRooms(string nextFromRoomId, string nextToRoomId)
    {
        return new MinimapLinkDefinition(linkId, nextFromRoomId, nextToRoomId, linkType, pathPoints);
    }

    public MinimapLinkDefinition WithType(MinimapLinkType nextLinkType)
    {
        return new MinimapLinkDefinition(linkId, fromRoomId, toRoomId, nextLinkType, pathPoints);
    }

    public MinimapLinkDefinition WithPathPoints(IEnumerable<Vector2Int> nextPathPoints)
    {
        return new MinimapLinkDefinition(linkId, fromRoomId, toRoomId, linkType, nextPathPoints);
    }

    public MinimapLinkDefinition WithLinkId(string nextLinkId)
    {
        return new MinimapLinkDefinition(nextLinkId, fromRoomId, toRoomId, linkType, pathPoints);
    }

    private static List<Vector2Int> ClonePathPoints(IEnumerable<Vector2Int> source)
    {
        var cloned = new List<Vector2Int>();
        if (source == null)
        {
            return cloned;
        }

        foreach (Vector2Int point in source)
        {
            cloned.Add(point);
        }

        return cloned;
    }

    private static string BuildFallbackId(string fromRoomId, string toRoomId)
    {
        string from = string.IsNullOrWhiteSpace(fromRoomId) ? "RoomA" : fromRoomId.Trim();
        string to = string.IsNullOrWhiteSpace(toRoomId) ? "RoomB" : toRoomId.Trim();
        return from + "_to_" + to;
    }
}
