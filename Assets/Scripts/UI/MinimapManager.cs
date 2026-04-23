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
    [SerializeField] private bool showFullMapOnStart;
#if ENABLE_LEGACY_INPUT_MANAGER
    [SerializeField] private KeyCode fullMapKey = KeyCode.M;
#endif

    private readonly Dictionary<string, MinimapRoomDefinition> roomsById = new Dictionary<string, MinimapRoomDefinition>(StringComparer.Ordinal);
    private readonly HashSet<string> visitedRoomIds = new HashSet<string>(StringComparer.Ordinal);
    private MinimapView view;
    private string currentRoomId;

    public static MinimapManager Instance { get; private set; }

    public event Action Changed;

    public IReadOnlyList<MinimapRoomDefinition> RoomDefinitions => roomDefinitions;
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

        RebuildRoomLookup();
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

        NotifyChanged();
    }

    public void UnregisterRoom(MinimapRoom room)
    {
        if (room == null || string.IsNullOrWhiteSpace(room.RoomId))
        {
            return;
        }

        RebuildRoomLookup();
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

    private void EnsureView()
    {
        view = GetComponent<MinimapView>();
        if (view == null)
        {
            view = gameObject.AddComponent<MinimapView>();
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
