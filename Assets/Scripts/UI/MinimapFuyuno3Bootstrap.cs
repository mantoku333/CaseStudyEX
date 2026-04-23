using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MinimapFuyuno3Bootstrap
{
    private const string TargetSceneName = "Test_Fuyuno3";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TrySetup(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrySetup(scene);
    }

    private static void TrySetup(Scene scene)
    {
        if (!scene.IsValid() || scene.name != TargetSceneName)
        {
            return;
        }

        MinimapManager manager = MinimapManager.Instance;
        if (manager == null)
        {
            GameObject systemObject = new GameObject("MinimapSystem");
            manager = systemObject.AddComponent<MinimapManager>();
        }

        Dictionary<string, MinimapRoomDefinition> definitions = CreateFuyuno3Definitions();
        manager.SetRoomDefinitions(definitions.Values);
        BindExistingRoomTriggers(definitions);
    }

    private static Dictionary<string, MinimapRoomDefinition> CreateFuyuno3Definitions()
    {
        var definitions = new Dictionary<string, MinimapRoomDefinition>
        {
            {
                "IrisRoom",
                new MinimapRoomDefinition(
                    "IrisRoom",
                    "Iris Room",
                    new Vector2Int(0, 2),
                    new Vector2Int(2, 1),
                    MinimapConnection.Right)
            },
            {
                "Corridor01",
                new MinimapRoomDefinition(
                    "Corridor01",
                    "Corridor 1",
                    new Vector2Int(2, 2),
                    new Vector2Int(3, 1),
                    MinimapConnection.Left | MinimapConnection.Right | MinimapConnection.Down)
            },
            {
                "SisterRoom",
                new MinimapRoomDefinition(
                    "SisterRoom",
                    "Sister Room",
                    new Vector2Int(5, 2),
                    new Vector2Int(2, 1),
                    MinimapConnection.Left)
            },
            {
                "BookshelfRoom",
                new MinimapRoomDefinition(
                    "BookshelfRoom",
                    "Bookshelf Room",
                    new Vector2Int(0, 1),
                    new Vector2Int(2, 1),
                    MinimapConnection.Right | MinimapConnection.Down)
            },
            {
                "Corridor02",
                new MinimapRoomDefinition(
                    "Corridor02",
                    "Corridor 2",
                    new Vector2Int(2, 1),
                    new Vector2Int(2, 1),
                    MinimapConnection.Left | MinimapConnection.Right | MinimapConnection.Up)
            },
            {
                "PartsRoom",
                new MinimapRoomDefinition(
                    "PartsRoom",
                    "Parts Room",
                    new Vector2Int(4, 1),
                    new Vector2Int(2, 1),
                    MinimapConnection.Left)
            },
            {
                "Stairs",
                new MinimapRoomDefinition(
                    "Stairs",
                    "Stairs",
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 1),
                    MinimapConnection.Up | MinimapConnection.Right)
            },
            {
                "Corridor03",
                new MinimapRoomDefinition(
                    "Corridor03",
                    "Corridor 3",
                    new Vector2Int(1, 0),
                    new Vector2Int(2, 1),
                    MinimapConnection.Left | MinimapConnection.Right)
            },
            {
                "SaveRoom",
                new MinimapRoomDefinition(
                    "SaveRoom",
                    "Save Room",
                    new Vector2Int(3, 0),
                    new Vector2Int(2, 1),
                    MinimapConnection.Left | MinimapConnection.Right)
            },
            {
                "BossRoom",
                new MinimapRoomDefinition(
                    "BossRoom",
                    "Boss Room",
                    new Vector2Int(5, 0),
                    new Vector2Int(3, 1),
                    MinimapConnection.Left)
            }
        };

        return definitions;
    }

    private static void BindExistingRoomTriggers(IReadOnlyDictionary<string, MinimapRoomDefinition> definitions)
    {
        ConfigureRoom("Col_1-1", "IrisRoom", definitions);
        ConfigureRoom("Col_1-2", "Corridor01", definitions);
        ConfigureRoom("Col_1-3", "SisterRoom", definitions);
        ConfigureRoom("Col_1-4", "BookshelfRoom", definitions);
    }

    private static void ConfigureRoom(string triggerObjectName, string roomId, IReadOnlyDictionary<string, MinimapRoomDefinition> definitions)
    {
        if (!definitions.TryGetValue(roomId, out MinimapRoomDefinition definition))
        {
            return;
        }

        GameObject triggerObject = GameObject.Find(triggerObjectName);
        if (triggerObject == null)
        {
            return;
        }

        MinimapRoom room = triggerObject.GetComponent<MinimapRoom>();
        if (room == null)
        {
            room = triggerObject.AddComponent<MinimapRoom>();
        }

        room.Configure(definition);
    }
}
