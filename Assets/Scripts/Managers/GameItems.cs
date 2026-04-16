using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameItems
{
    private const string SectionKey = "game_items_v1";
    private static readonly Dictionary<string, int> items = new Dictionary<string, int>(StringComparer.Ordinal);
    private static readonly GameItemsSaveModule module = new GameItemsSaveModule();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SaveManager.RegisterModule(module);
    }

    public static int Count => items.Count;

    public static int GetCount(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        return items.TryGetValue(itemId, out var count) ? Mathf.Max(0, count) : 0;
    }

    public static void SetCount(string itemId, int count)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        int normalized = Mathf.Max(0, count);
        if (normalized <= 0)
        {
            items.Remove(itemId);
            return;
        }

        items[itemId] = normalized;
    }

    public static void AddCount(string itemId, int delta)
    {
        if (string.IsNullOrWhiteSpace(itemId) || delta == 0)
        {
            return;
        }

        int current = GetCount(itemId);
        SetCount(itemId, current + delta);
    }

    public static void Remove(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        items.Remove(itemId);
    }

    public static void ClearAll()
    {
        items.Clear();
    }

    private static void ApplyPayload(GameItemsPayload payload)
    {
        items.Clear();

        if (payload == null || payload.entries == null)
        {
            return;
        }

        for (int i = 0; i < payload.entries.Count; i++)
        {
            GameItemEntry entry = payload.entries[i];
            if (string.IsNullOrWhiteSpace(entry.itemId))
            {
                continue;
            }

            int count = Mathf.Max(0, entry.count);
            if (count <= 0)
            {
                continue;
            }

            items[entry.itemId] = count;
        }
    }

    private static GameItemsPayload CreatePayload()
    {
        var payload = new GameItemsPayload
        {
            entries = new List<GameItemEntry>(items.Count)
        };

        foreach (var pair in items)
        {
            payload.entries.Add(new GameItemEntry
            {
                itemId = pair.Key,
                count = pair.Value
            });
        }

        return payload;
    }

    [Serializable]
    private sealed class GameItemsPayload
    {
        public List<GameItemEntry> entries = new List<GameItemEntry>();
    }

    [Serializable]
    private struct GameItemEntry
    {
        public string itemId;
        public int count;
    }

    private sealed class GameItemsSaveModule : ISaveDataModule
    {
        public int Priority => 210;

        public void Capture(SaveGameData saveData)
        {
            if (saveData == null)
            {
                return;
            }

            GameItemsPayload payload = CreatePayload();
            string json = JsonUtility.ToJson(payload);
            saveData.SetCustomSectionJson(SectionKey, json);
        }

        public void Restore(SaveGameData saveData)
        {
            if (saveData == null)
            {
                ApplyPayload(null);
                return;
            }

            string json = saveData.GetCustomSectionJson(SectionKey);
            if (string.IsNullOrWhiteSpace(json))
            {
                ApplyPayload(null);
                return;
            }

            try
            {
                var payload = JsonUtility.FromJson<GameItemsPayload>(json);
                ApplyPayload(payload);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GameItems] Failed to parse saved items. {exception}");
                ApplyPayload(null);
            }
        }
    }
}
