using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>Build-safe references for audio selected in the dialogue editor.</summary>
public sealed class StoryDialogueAudioCatalog : ScriptableObject
{
    public const string ResourcePath = "Story/StoryDialogueAudioCatalog";
    [Serializable]
    public sealed class Entry
    {
        public string key;
        public AudioClip clip;
    }

    public List<Entry> entries = new List<Entry>();

    public AudioClip FindClip(string key)
    {
        Entry entry = entries.Find(item => item != null && item.key == key);
        return entry != null ? entry.clip : null;
    }

    public static void ApplyLineMetadata(string[] metadata)
    {
        string bgm = ReadValue(metadata, "bgm:");
        string se = ReadValue(metadata, "se:");
        if (string.IsNullOrEmpty(bgm) && string.IsNullOrEmpty(se)) return;

        var catalog = Resources.Load<StoryDialogueAudioCatalog>(ResourcePath);
        float fade = ReadNumber(metadata, "bgmfade:", 0.5f, float.MaxValue);
        if (bgm == "stop")
            StoryTimelineRuntime.Instance.StopBgm(fade);
        else if (!string.IsNullOrEmpty(bgm))
        {
            AudioClip clip = catalog != null ? catalog.FindClip(bgm) : null;
            if (clip != null)
                StoryTimelineRuntime.Instance.PlayBgm(clip, ReadNumber(metadata, "bgmvolume:", 1f, 1f), true, fade);
            else Debug.LogWarning($"[Story Audio] BGM reference is missing: {bgm}");
        }

        if (!string.IsNullOrEmpty(se))
        {
            AudioClip clip = catalog != null ? catalog.FindClip(se) : null;
            if (clip != null)
                StoryTimelineRuntime.Instance.PlaySe(clip, ReadNumber(metadata, "sevolume:", 1f, 1f));
            else Debug.LogWarning($"[Story Audio] SE reference is missing: {se}");
        }
    }

    private static string ReadValue(string[] metadata, string prefix)
    {
        if (metadata == null) return string.Empty;
        foreach (string raw in metadata)
        {
            string tag = (raw ?? string.Empty).Trim().TrimStart('#');
            if (tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return tag.Substring(prefix.Length);
        }
        return string.Empty;
    }

    private static float ReadNumber(string[] metadata, string prefix, float fallback, float maximum)
    {
        return float.TryParse(ReadValue(metadata, prefix), NumberStyles.Float,
            CultureInfo.InvariantCulture, out float value) && !float.IsNaN(value) && !float.IsInfinity(value)
            ? Mathf.Clamp(value, 0f, maximum) : fallback;
    }
}
