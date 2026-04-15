using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SaveGameData
{
    public int version;
    public string sceneName;
    public SerializableVector3 playerPosition;
    public string savedAtUtc;
    public List<SaveBoolEntry> flags = new List<SaveBoolEntry>();
    public List<SaveItemStackEntry> items = new List<SaveItemStackEntry>();
    public List<SaveCustomSectionEntry> customSections = new List<SaveCustomSectionEntry>();

    public bool TryGetFlag(string key, out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        EnsureCollections();

        for (int i = 0; i < flags.Count; i++)
        {
            if (!string.Equals(flags[i].key, key, StringComparison.Ordinal))
            {
                continue;
            }

            value = flags[i].value;
            return true;
        }

        return false;
    }

    public void SetFlag(string key, bool value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        EnsureCollections();

        for (int i = 0; i < flags.Count; i++)
        {
            if (!string.Equals(flags[i].key, key, StringComparison.Ordinal))
            {
                continue;
            }

            flags[i] = new SaveBoolEntry { key = key, value = value };
            return;
        }

        flags.Add(new SaveBoolEntry { key = key, value = value });
    }

    public void RemoveFlag(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        EnsureCollections();

        for (int i = flags.Count - 1; i >= 0; i--)
        {
            if (string.Equals(flags[i].key, key, StringComparison.Ordinal))
            {
                flags.RemoveAt(i);
            }
        }
    }

    public int GetItemCount(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        EnsureCollections();

        for (int i = 0; i < items.Count; i++)
        {
            if (string.Equals(items[i].itemId, itemId, StringComparison.Ordinal))
            {
                return Mathf.Max(0, items[i].count);
            }
        }

        return 0;
    }

    public void SetItemCount(string itemId, int count)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return;
        }

        EnsureCollections();
        int normalized = Mathf.Max(0, count);

        for (int i = 0; i < items.Count; i++)
        {
            if (!string.Equals(items[i].itemId, itemId, StringComparison.Ordinal))
            {
                continue;
            }

            if (normalized <= 0)
            {
                items.RemoveAt(i);
                return;
            }

            items[i] = new SaveItemStackEntry { itemId = itemId, count = normalized };
            return;
        }

        if (normalized > 0)
        {
            items.Add(new SaveItemStackEntry { itemId = itemId, count = normalized });
        }
    }

    public string GetCustomSectionJson(string sectionKey)
    {
        if (string.IsNullOrWhiteSpace(sectionKey))
        {
            return string.Empty;
        }

        EnsureCollections();

        for (int i = 0; i < customSections.Count; i++)
        {
            if (string.Equals(customSections[i].sectionKey, sectionKey, StringComparison.Ordinal))
            {
                return customSections[i].json ?? string.Empty;
            }
        }

        return string.Empty;
    }

    public void SetCustomSectionJson(string sectionKey, string json)
    {
        if (string.IsNullOrWhiteSpace(sectionKey))
        {
            return;
        }

        EnsureCollections();

        for (int i = 0; i < customSections.Count; i++)
        {
            if (!string.Equals(customSections[i].sectionKey, sectionKey, StringComparison.Ordinal))
            {
                continue;
            }

            customSections[i] = new SaveCustomSectionEntry
            {
                sectionKey = sectionKey,
                json = json ?? string.Empty
            };
            return;
        }

        customSections.Add(new SaveCustomSectionEntry
        {
            sectionKey = sectionKey,
            json = json ?? string.Empty
        });
    }

    private void EnsureCollections()
    {
        if (flags == null)
        {
            flags = new List<SaveBoolEntry>();
        }

        if (items == null)
        {
            items = new List<SaveItemStackEntry>();
        }

        if (customSections == null)
        {
            customSections = new List<SaveCustomSectionEntry>();
        }
    }
}

[Serializable]
public struct SaveBoolEntry
{
    public string key;
    public bool value;
}

[Serializable]
public struct SaveItemStackEntry
{
    public string itemId;
    public int count;
}

[Serializable]
public struct SaveCustomSectionEntry
{
    public string sectionKey;
    public string json;
}

public readonly struct SaveSlotMeta
{
    public int SlotIndex { get; }
    public bool HasSave { get; }
    public bool IsCorrupted { get; }
    public string SceneName { get; }
    public string SavedAtUtc { get; }

    public SaveSlotMeta(
        int slotIndex,
        bool hasSave,
        bool isCorrupted,
        string sceneName,
        string savedAtUtc)
    {
        SlotIndex = slotIndex;
        HasSave = hasSave;
        IsCorrupted = isCorrupted;
        SceneName = sceneName ?? string.Empty;
        SavedAtUtc = savedAtUtc ?? string.Empty;
    }
}

[Serializable]
public struct SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }

    public static SerializableVector3 FromVector3(Vector3 value)
    {
        return new SerializableVector3
        {
            x = value.x,
            y = value.y,
            z = value.z
        };
    }
}
