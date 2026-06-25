using System;
using System.Collections.Generic;
using UnityEngine;

public static class GameProgressFlags
{
    public const string SectionKey = "game_progress_flags_v1";
    private static readonly Dictionary<string, bool> flags = new Dictionary<string, bool>(StringComparer.Ordinal);
    private static readonly GameProgressFlagsSaveModule module = new GameProgressFlagsSaveModule();

    public static event Action<string, bool> FlagChanged;

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

        bool changed = !flags.TryGetValue(flagKey, out bool previousValue) || previousValue != value;
        flags[flagKey] = value;

        if (changed)
        {
            FlagChanged?.Invoke(flagKey, value);
        }
    }

    public static void Remove(string flagKey)
    {
        if (string.IsNullOrWhiteSpace(flagKey))
        {
            return;
        }

        if (flags.Remove(flagKey))
        {
            FlagChanged?.Invoke(flagKey, false);
        }
    }

    public static void ClearAll()
    {
        if (flags.Count == 0)
        {
            return;
        }

        List<string> removedKeys = new List<string>(flags.Keys);
        flags.Clear();

        for (int i = 0; i < removedKeys.Count; i++)
        {
            FlagChanged?.Invoke(removedKeys[i], false);
        }
    }

    public static List<GameProgressFlagSnapshot> GetSnapshot()
    {
        var snapshot = new List<GameProgressFlagSnapshot>(flags.Count);
        foreach (var pair in flags)
        {
            snapshot.Add(new GameProgressFlagSnapshot(pair.Key, pair.Value));
        }

        snapshot.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
        return snapshot;
    }

    public static void RestoreFromSaveData(SaveGameData saveData)
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
            var payload = JsonUtility.FromJson<GameProgressFlagsPayload>(json);
            ApplyPayload(payload);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[GameProgressFlags] Failed to parse saved flags. {exception}");
            ApplyPayload(null);
        }
    }

    private static void ApplyPayload(GameProgressFlagsPayload payload)
    {
        Dictionary<string, bool> previousFlags = new Dictionary<string, bool>(flags, StringComparer.Ordinal);
        flags.Clear();

        if (payload != null && payload.entries != null)
        {
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

        NotifyChangedFlags(previousFlags);
    }

    private static void NotifyChangedFlags(Dictionary<string, bool> previousFlags)
    {
        foreach (var pair in flags)
        {
            if (!previousFlags.TryGetValue(pair.Key, out bool previousValue) || previousValue != pair.Value)
            {
                FlagChanged?.Invoke(pair.Key, pair.Value);
            }
        }

        foreach (var pair in previousFlags)
        {
            if (!flags.ContainsKey(pair.Key))
            {
                FlagChanged?.Invoke(pair.Key, false);
            }
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
    public sealed class GameProgressFlagsPayload
    {
        public List<GameProgressFlagEntry> entries = new List<GameProgressFlagEntry>();
    }

    [Serializable]
    public struct GameProgressFlagEntry
    {
        public string key;
        public bool value;
    }

    public readonly struct GameProgressFlagSnapshot
    {
        public string Key { get; }
        public bool Value { get; }

        public GameProgressFlagSnapshot(string key, bool value)
        {
            Key = key ?? string.Empty;
            Value = value;
        }
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
            RestoreFromSaveData(saveData);
        }
    }
}
