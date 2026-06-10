using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class MinimapManager : MonoBehaviour
{
    [SerializeField] private List<MinimapRoomDefinition> roomDefinitions = new List<MinimapRoomDefinition>();
    [SerializeField] private List<MinimapLinkDefinition> linkDefinitions = new List<MinimapLinkDefinition>();
    [SerializeField] private bool showFullMapOnStart;
#if ENABLE_LEGACY_INPUT_MANAGER
    [SerializeField] private KeyCode fullMapKey = KeyCode.M;
#endif

    private readonly Dictionary<string, MinimapRoomDefinition> roomsById = new Dictionary<string, MinimapRoomDefinition>(StringComparer.Ordinal);
    private readonly HashSet<string> visitedRoomIds = new HashSet<string>(StringComparer.Ordinal);
    private bool hasExplicitLinks;
    private MinimapView view;
    private string currentRoomId;

    public static MinimapManager Instance { get; private set; }

    public static System.Action MapKeyRequested;

    public event Action Changed;

    public IReadOnlyList<MinimapRoomDefinition> RoomDefinitions => roomDefinitions;
    public IReadOnlyList<MinimapLinkDefinition> LinkDefinitions => linkDefinitions;
    public string CurrentRoomId => currentRoomId;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RebuildRoomLookup();
        EnsureView();
    }

    private void Start()
    {
        view.SetFullMapVisible(showFullMapOnStart);
        NotifyChanged();
    }

    private void Update()
    {
        if (WasFullMapKeyPressed())
        {
            if (MapKeyRequested != null)
                MapKeyRequested.Invoke();
            else
                view.ToggleFullMap();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void SetRoomDefinitions(IEnumerable<MinimapRoomDefinition> definitions)
    {
        roomDefinitions.Clear();

        if (definitions != null)
        {
            foreach (MinimapRoomDefinition definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.RoomId))
                {
                    continue;
                }

                roomDefinitions.Add(definition);
            }
        }

        NormalizeRoomDefinitions();
        RebuildRoomLookup();
        RebuildLinksIfNeeded();
        NotifyChanged();
    }

    public void SetLinkDefinitions(IEnumerable<MinimapLinkDefinition> definitions)
    {
        linkDefinitions.Clear();
        hasExplicitLinks = false;

        if (definitions != null)
        {
            foreach (MinimapLinkDefinition definition in definitions)
            {
                if (!IsValidLink(definition))
                {
                    continue;
                }

                linkDefinitions.Add(definition);
                hasExplicitLinks = true;
            }
        }

        if (!hasExplicitLinks)
        {
            RebuildAutoLinks();
        }

        NotifyChanged();
    }

    public void RegisterRoom(MinimapRoom room)
    {
        if (room == null || string.IsNullOrWhiteSpace(room.RoomId))
        {
            return;
        }

        roomsById[room.RoomId] = room.Definition;
        bool existsInList = false;

        for (int i = 0; i < roomDefinitions.Count; i++)
        {
            if (!string.Equals(roomDefinitions[i].RoomId, room.RoomId, StringComparison.Ordinal))
            {
                continue;
            }

            roomDefinitions[i] = room.Definition;
            existsInList = true;
            break;
        }

        if (!existsInList)
        {
            roomDefinitions.Add(room.Definition);
        }

        NormalizeRoomDefinitions();
        RebuildRoomLookup();
        RebuildLinksIfNeeded();
        NotifyChanged();
    }

    public void UnregisterRoom(MinimapRoom room)
    {
        if (room == null || string.IsNullOrWhiteSpace(room.RoomId))
        {
            return;
        }

        roomDefinitions.RemoveAll(definition =>
            definition != null &&
            string.Equals(definition.RoomId, room.RoomId, StringComparison.Ordinal));

        NormalizeRoomDefinitions();
        RebuildRoomLookup();
        RebuildLinksIfNeeded();
        NotifyChanged();
    }

    public void RegisterLink(MinimapLink link)
    {
        if (link == null || !link.IsValid)
        {
            return;
        }

        MinimapLinkDefinition nextDefinition = link.Definition;
        if (nextDefinition == null)
        {
            return;
        }

        hasExplicitLinks = true;
        bool existsInList = false;
        for (int i = 0; i < linkDefinitions.Count; i++)
        {
            MinimapLinkDefinition existing = linkDefinitions[i];
            if (existing == null || !string.Equals(existing.LinkId, nextDefinition.LinkId, StringComparison.Ordinal))
            {
                continue;
            }

            linkDefinitions[i] = nextDefinition;
            existsInList = true;
            break;
        }

        if (!existsInList)
        {
            linkDefinitions.Add(nextDefinition);
        }

        NotifyChanged();
    }

    public void UnregisterLink(MinimapLink link)
    {
        if (link == null)
        {
            return;
        }

        linkDefinitions.RemoveAll(definition =>
            definition != null &&
            string.Equals(definition.LinkId, link.LinkId, StringComparison.Ordinal));

        hasExplicitLinks = linkDefinitions.Count > 0;
        RebuildLinksIfNeeded();
        NotifyChanged();
    }

    public void EnterRoom(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            return;
        }

        currentRoomId = roomId;
        visitedRoomIds.Add(roomId);
        NotifyChanged();
    }

    public void ClearCurrentRoom()
    {
        if (string.IsNullOrWhiteSpace(currentRoomId))
        {
            return;
        }

        currentRoomId = string.Empty;
        NotifyChanged();
    }

    public bool IsVisited(string roomId)
    {
        return !string.IsNullOrWhiteSpace(roomId) && visitedRoomIds.Contains(roomId);
    }

    public bool IsCurrent(string roomId)
    {
        return !string.IsNullOrWhiteSpace(roomId) && string.Equals(currentRoomId, roomId, StringComparison.Ordinal);
    }

    public bool TryGetRoom(string roomId, out MinimapRoomDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            definition = null;
            return false;
        }

        return roomsById.TryGetValue(roomId, out definition);
    }

    private void RebuildRoomLookup()
    {
        roomsById.Clear();

        for (int i = 0; i < roomDefinitions.Count; i++)
        {
            MinimapRoomDefinition definition = roomDefinitions[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.RoomId))
            {
                continue;
            }

            roomsById[definition.RoomId] = definition;
        }
    }

    private void RebuildLinksIfNeeded()
    {
        if (!hasExplicitLinks)
        {
            RebuildAutoLinks();
            return;
        }

        linkDefinitions.RemoveAll(definition => !IsValidLink(definition));
    }

    // Build room connections from adjacency so scene authors only need to place
    // rooms with X / Y / Width / Height values.
    private void NormalizeRoomDefinitions()
    {
        var normalized = new List<MinimapRoomDefinition>(roomDefinitions.Count);

        for (int i = 0; i < roomDefinitions.Count; i++)
        {
            MinimapRoomDefinition room = roomDefinitions[i];
            if (room == null)
            {
                continue;
            }

            MinimapConnection connections = MinimapConnection.None;

            for (int j = 0; j < roomDefinitions.Count; j++)
            {
                if (i == j || roomDefinitions[j] == null)
                {
                    continue;
                }

                MinimapRoomDefinition candidate = roomDefinitions[j];
                RectInt a = new RectInt(room.MapPosition, room.MapSize);
                RectInt b = new RectInt(candidate.MapPosition, candidate.MapSize);

                if (a.xMax == b.xMin && RangesOverlap(a.yMin, a.yMax, b.yMin, b.yMax))
                {
                    connections |= MinimapConnection.Right;
                }

                if (a.xMin == b.xMax && RangesOverlap(a.yMin, a.yMax, b.yMin, b.yMax))
                {
                    connections |= MinimapConnection.Left;
                }

                if (a.yMax == b.yMin && RangesOverlap(a.xMin, a.xMax, b.xMin, b.xMax))
                {
                    connections |= MinimapConnection.Up;
                }

                if (a.yMin == b.yMax && RangesOverlap(a.xMin, a.xMax, b.xMin, b.xMax))
                {
                    connections |= MinimapConnection.Down;
                }
            }

            normalized.Add(room.WithConnections(connections));
        }

        roomDefinitions.Clear();
        roomDefinitions.AddRange(normalized);
    }

    private static bool RangesOverlap(int aMin, int aMax, int bMin, int bMax)
    {
        return aMin < bMax && bMin < aMax;
    }

    private void RebuildAutoLinks()
    {
        linkDefinitions.Clear();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < roomDefinitions.Count; i++)
        {
            MinimapRoomDefinition room = roomDefinitions[i];
            if (room == null)
            {
                continue;
            }

            AddAutoLinkForDirection(room, MinimapConnection.Right, seenKeys);
            AddAutoLinkForDirection(room, MinimapConnection.Left, seenKeys);
            AddAutoLinkForDirection(room, MinimapConnection.Up, seenKeys);
            AddAutoLinkForDirection(room, MinimapConnection.Down, seenKeys);
        }
    }

    private void AddAutoLinkForDirection(
        MinimapRoomDefinition room,
        MinimapConnection direction,
        ISet<string> seenKeys)
    {
        if ((room.Connections & direction) == 0)
        {
            return;
        }

        MinimapRoomDefinition neighbor = FindNeighbor(room, direction);
        if (neighbor == null)
        {
            return;
        }

        string first = room.RoomId;
        string second = neighbor.RoomId;
        if (string.CompareOrdinal(first, second) > 0)
        {
            string swap = first;
            first = second;
            second = swap;
        }

        string key = first + "->" + second;
        if (!seenKeys.Add(key))
        {
            return;
        }

        linkDefinitions.Add(new MinimapLinkDefinition("auto_" + key, first, second, null));
    }

    private MinimapRoomDefinition FindNeighbor(MinimapRoomDefinition room, MinimapConnection direction)
    {
        RectInt a = new RectInt(room.MapPosition, room.MapSize);

        for (int i = 0; i < roomDefinitions.Count; i++)
        {
            MinimapRoomDefinition candidate = roomDefinitions[i];
            if (candidate == null || ReferenceEquals(candidate, room))
            {
                continue;
            }

            RectInt b = new RectInt(candidate.MapPosition, candidate.MapSize);

            if (direction == MinimapConnection.Right && a.xMax == b.xMin && RangesOverlap(a.yMin, a.yMax, b.yMin, b.yMax))
            {
                return candidate;
            }

            if (direction == MinimapConnection.Left && a.xMin == b.xMax && RangesOverlap(a.yMin, a.yMax, b.yMin, b.yMax))
            {
                return candidate;
            }

            if (direction == MinimapConnection.Up && a.yMax == b.yMin && RangesOverlap(a.xMin, a.xMax, b.xMin, b.xMax))
            {
                return candidate;
            }

            if (direction == MinimapConnection.Down && a.yMin == b.yMax && RangesOverlap(a.xMin, a.xMax, b.xMin, b.xMax))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsValidLink(MinimapLinkDefinition definition)
    {
        return definition != null &&
            !string.IsNullOrWhiteSpace(definition.FromRoomId) &&
            !string.IsNullOrWhiteSpace(definition.ToRoomId);
    }

    private void EnsureView()
    {
        view = GetComponent<MinimapView>();
        if (view == null)
        {
            Debug.LogError("[MinimapManager] MinimapView component is missing. Please attach MinimapView to the same GameObject.", this);
            return;
        }

        view.Initialize(this);
    }

    private bool WasFullMapKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(fullMapKey))
        {
            return true;
        }
#endif

        return false;
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }
}
