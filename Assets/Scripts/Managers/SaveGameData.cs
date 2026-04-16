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
    public List<SaveCustomSectionEntry> customSections = new List<SaveCustomSectionEntry>();

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
        if (customSections == null)
        {
            customSections = new List<SaveCustomSectionEntry>();
        }
    }
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
