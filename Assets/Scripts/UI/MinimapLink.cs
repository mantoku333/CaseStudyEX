using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("UI/Minimap Link")]
[DisallowMultipleComponent]
public sealed class MinimapLink : MonoBehaviour
{
    [SerializeField] private string linkId;
    [SerializeField] private MinimapRoom fromRoom;
    [SerializeField] private MinimapRoom toRoom;
    [SerializeField] private List<Vector2> pathPoints = new List<Vector2>();

    public string LinkId => string.IsNullOrWhiteSpace(linkId) ? gameObject.name : linkId;
    public MinimapRoom FromRoom => fromRoom;
    public MinimapRoom ToRoom => toRoom;
    public IReadOnlyList<Vector2> PathPoints => pathPoints;

    public bool IsValid
    {
        get
        {
            return fromRoom != null &&
                toRoom != null &&
                !string.IsNullOrWhiteSpace(fromRoom.RoomId) &&
                !string.IsNullOrWhiteSpace(toRoom.RoomId);
        }
    }

    public MinimapLinkDefinition Definition
    {
        get
        {
            if (!IsValid)
            {
                return null;
            }

            return new MinimapLinkDefinition(
                LinkId,
                fromRoom.RoomId,
                toRoom.RoomId,
                pathPoints);
        }
    }

    private void Reset()
    {
        if (string.IsNullOrWhiteSpace(linkId))
        {
            linkId = gameObject.name;
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(linkId))
        {
            linkId = gameObject.name;
        }
    }

    private void OnEnable()
    {
        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.RegisterLink(this);
        }
    }

    private void OnDisable()
    {
        if (MinimapManager.Instance != null)
        {
            MinimapManager.Instance.UnregisterLink(this);
        }
    }

    public void Configure(MinimapRoom nextFromRoom, MinimapRoom nextToRoom, IList<Vector2> nextPathPoints)
    {
        fromRoom = nextFromRoom;
        toRoom = nextToRoom;
        pathPoints = nextPathPoints != null ? new List<Vector2>(nextPathPoints) : new List<Vector2>();

        if (isActiveAndEnabled && MinimapManager.Instance != null)
        {
            MinimapManager.Instance.RegisterLink(this);
        }
    }

    public void SetPathPoints(IList<Vector2> nextPathPoints)
    {
        pathPoints = nextPathPoints != null ? new List<Vector2>(nextPathPoints) : new List<Vector2>();

        if (isActiveAndEnabled && MinimapManager.Instance != null)
        {
            MinimapManager.Instance.RegisterLink(this);
        }
    }
}
