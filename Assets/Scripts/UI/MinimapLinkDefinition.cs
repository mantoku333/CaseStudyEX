using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MinimapLinkDefinition
{
    [SerializeField] private string linkId;
    [SerializeField] private string fromRoomId;
    [SerializeField] private string toRoomId;
    [SerializeField] private List<Vector2> pathPoints = new List<Vector2>();

    public string LinkId => linkId;
    public string FromRoomId => fromRoomId;
    public string ToRoomId => toRoomId;
    public IReadOnlyList<Vector2> PathPoints => pathPoints;

    public MinimapLinkDefinition()
    {
        linkId = string.Empty;
        fromRoomId = string.Empty;
        toRoomId = string.Empty;
    }

    public MinimapLinkDefinition(
        string linkId,
        string fromRoomId,
        string toRoomId,
        IEnumerable<Vector2> pathPoints)
    {
        this.linkId = linkId;
        this.fromRoomId = fromRoomId;
        this.toRoomId = toRoomId;
        this.pathPoints = pathPoints != null ? new List<Vector2>(pathPoints) : new List<Vector2>();
    }
}
