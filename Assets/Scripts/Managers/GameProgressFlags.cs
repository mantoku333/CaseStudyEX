using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameProgressFlags
{
    private const string SectionKey = "game_progress_flags_v1";
    private static readonly Dictionary<string, bool> flags = new Dictionary<string, bool>(StringComparer.Ordinal);
    private static readonly GameProgressFlagsSaveModule module = new GameProgressFlagsSaveModule();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SaveManager.RegisterModule(module);
    }

    public static int Count => flags.Count;

    public static bool IsSet(string flagKey)
    {
        return Get(flagKey);
    }

    public static bool Get(string flagKey, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(flagKey))
        {
            return defaultValue;
        }

        return flags.TryGetValue(flagKey, out var value) ? value : defaultValue;
    }

    public static void Set(string flagKey, bool value = true)
    {
        if (string.IsNullOrWhiteSpace(flagKey))
        {
            return;
        }

        flags[flagKey] = value;
    }

    public static void Remove(string flagKey)
    {
        if (string.IsNullOrWhiteSpace(flagKey))
        {
            return;
        }

        flags.Remove(flagKey);
    }

    public static void ClearAll()
    {
        flags.Clear();
    }

    private static void ApplyPayload(GameProgressFlagsPayload payload)
    {
        flags.Clear();

        if (payload == null || payload.entries == null)
        {
            return;
        }

        for (int i = 0; i < payload.entries.Count; i++)
        {
            GameProgressFlagEntry entry = payload.entries[i];
            if (string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            flags[entry.key] = entry.value;
        }
    }

    private static void ApplyLegacyFlags(List<SaveBoolEntry> legacyFlags)
    {
        flags.Clear();

        if (legacyFlags == null)
        {
            return;
        }

        for (int i = 0; i < legacyFlags.Count; i++)
        {
            SaveBoolEntry entry = legacyFlags[i];
            if (string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            flags[entry.key] = entry.value;
        }
    }

    private static GameProgressFlagsPayload CreatePayload()
    {
        var payload = new GameProgressFlagsPayload
        {
            entries = new List<GameProgressFlagEntry>(flags.Count)
        };

        foreach (var pair in flags)
        {
            payload.entries.Add(new GameProgressFlagEntry
            {
                key = pair.Key,
                value = pair.Value
            });
        }

        return payload;
    }

    [Serializable]
    private sealed class GameProgressFlagsPayload
    {
        public List<GameProgressFlagEntry> entries = new List<GameProgressFlagEntry>();
    }

    [Serializable]
    private struct GameProgressFlagEntry
    {
        public string key;
        public bool value;
    }

    private sealed class GameProgressFlagsSaveModule : ISaveDataModule
    {
        public int Priority => 200;

        public void Capture(SaveGameData saveData)
        {
            if (saveData == null)
            {
                return;
            }

            GameProgressFlagsPayload payload = CreatePayload();
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
                // Backward compatibility: read legacy flags from SaveGameData.flags.
                ApplyLegacyFlags(saveData.flags);
                return;
            }

            try
            {
                var payload = JsonUtility.FromJson<GameProgressFlagsPayload>(json);
                ApplyPayload(payload);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GameProgressFlags] Failed to parse saved flags. {exception}");
                ApplyLegacyFlags(saveData.flags);
            }
        }
    }
}
