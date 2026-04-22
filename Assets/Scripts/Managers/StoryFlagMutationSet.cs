using System;
using UnityEngine;

[Serializable]
public sealed class StoryFlagMutationSet
{
    [Tooltip("true に設定する進行フラグ")]
    public string[] setTrueFlags = Array.Empty<string>();

    [Tooltip("false に設定する進行フラグ")]
    public string[] setFalseFlags = Array.Empty<string>();

    [Tooltip("削除する進行フラグ")]
    public string[] clearFlags = Array.Empty<string>();

    public void Apply()
    {
        SetFlags(setTrueFlags, value: true);
        SetFlags(setFalseFlags, value: false);
        RemoveFlags(clearFlags);
    }

    private static void SetFlags(string[] flagKeys, bool value)
    {
        if (flagKeys == null || flagKeys.Length == 0)
        {
            return;
        }

        for (int i = 0; i < flagKeys.Length; i++)
        {
            string flagKey = flagKeys[i];
            if (string.IsNullOrWhiteSpace(flagKey))
            {
                continue;
            }

            GameProgressFlags.Set(flagKey.Trim(), value);
        }
    }

    private static void RemoveFlags(string[] flagKeys)
    {
        if (flagKeys == null || flagKeys.Length == 0)
        {
            return;
        }

        for (int i = 0; i < flagKeys.Length; i++)
        {
            string flagKey = flagKeys[i];
            if (string.IsNullOrWhiteSpace(flagKey))
            {
                continue;
            }

            GameProgressFlags.Remove(flagKey.Trim());
        }
    }
}
