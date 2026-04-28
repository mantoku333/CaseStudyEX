using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MinimapSceneBootstrap
{
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
        if (!scene.IsValid())
        {
            return;
        }

        MinimapRoom[] rooms = Object.FindObjectsByType<MinimapRoom>(FindObjectsSortMode.None);
        if (rooms == null || rooms.Length == 0)
        {
            return;
        }

        List<MinimapRoomDefinition> definitions = new List<MinimapRoomDefinition>(rooms.Length);
        for (int i = 0; i < rooms.Length; i++)
        {
            MinimapRoom room = rooms[i];
            if (room == null || string.IsNullOrWhiteSpace(room.RoomId))
            {
                continue;
            }

            definitions.Add(room.Definition);
        }

        if (definitions.Count == 0)
        {
            return;
        }

        MinimapManager manager = MinimapSystemFactory.EnsureInstance();
        if (manager == null)
        {
            return;
        }

        manager.SetRoomDefinitions(definitions);
    }
}
