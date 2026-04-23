using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MinimapMantokuStoryBootstrap
{
    private const string TargetSceneName = "Story_Mantoku";
    private static readonly Regex TriggerNamePattern = new Regex(@"^Col_(\d+)-(\d+)$", RegexOptions.Compiled);

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

        MantokuStoryHudBootstrap.EnsureHudCanvasExists();

        RoomCameraTrigger[] triggers = Object.FindObjectsByType<RoomCameraTrigger>(FindObjectsSortMode.None);
        if (triggers == null || triggers.Length == 0)
        {
            return;
        }

        Dictionary<string, MinimapRoomDefinition> definitions = CreateDefinitions(triggers);
        if (definitions.Count == 0)
        {
            return;
        }

        MinimapManager manager = MinimapManager.Instance;
        if (manager == null)
        {
            GameObject systemObject = new GameObject("MinimapSystem");
            manager = systemObject.AddComponent<MinimapManager>();
        }

        manager.SetRoomDefinitions(definitions.Values);
        BindExistingRoomTriggers(triggers, definitions);
    }

    private static Dictionary<string, MinimapRoomDefinition> CreateDefinitions(RoomCameraTrigger[] triggers)
    {
        var definitions = new Dictionary<string, MinimapRoomDefinition>();

        for (int i = 0; i < triggers.Length; i++)
        {
            RoomCameraTrigger trigger = triggers[i];
            if (trigger == null)
            {
                continue;
            }

            MinimapRoom room = trigger.GetComponent<MinimapRoom>();
            if (room == null)
            {
                room = trigger.gameObject.AddComponent<MinimapRoom>();
                room.ConfigureAuthoringFields(
                    trigger.gameObject.name,
                    trigger.gameObject.name,
                    GuessInitialMapPosition(trigger.gameObject.name),
                    Vector2Int.one);
            }

            definitions[room.RoomId] = room.Definition;
        }

        return definitions;
    }

    private static void BindExistingRoomTriggers(
        RoomCameraTrigger[] triggers,
        IReadOnlyDictionary<string, MinimapRoomDefinition> definitions)
    {
        for (int i = 0; i < triggers.Length; i++)
        {
            RoomCameraTrigger trigger = triggers[i];
            if (trigger == null)
            {
                continue;
            }

            MinimapRoom room = trigger.GetComponent<MinimapRoom>();
            if (room == null || !definitions.TryGetValue(room.RoomId, out MinimapRoomDefinition definition))
            {
                continue;
            }

            room.Configure(definition);
        }
    }

    private static Vector2Int GuessInitialMapPosition(string triggerObjectName)
    {
        if (string.IsNullOrWhiteSpace(triggerObjectName))
        {
            return Vector2Int.zero;
        }

        Match match = TriggerNamePattern.Match(triggerObjectName);
        if (!match.Success)
        {
            return Vector2Int.zero;
        }

        int row = int.Parse(match.Groups[1].Value);
        int column = int.Parse(match.Groups[2].Value);
        return new Vector2Int(column - 1, -(row - 1));
    }
}
