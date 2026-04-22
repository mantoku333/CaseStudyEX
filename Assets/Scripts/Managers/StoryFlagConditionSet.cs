using System;
using UnityEngine;

[Serializable]
public sealed class StoryFlagConditionSet
{
    [Tooltip("すべて true である必要がある進行フラグ")]
    public string[] requiredTrueFlags = Array.Empty<string>();

    [Tooltip("すべて false である必要がある進行フラグ")]
    public string[] requiredFalseFlags = Array.Empty<string>();

    public bool IsSatisfied()
    {
        return AreFlagsInExpectedState(requiredTrueFlags, expectedState: true)
               && AreFlagsInExpectedState(requiredFalseFlags, expectedState: false);
    }

    private static bool AreFlagsInExpectedState(string[] flagKeys, bool expectedState)
    {
        if (flagKeys == null || flagKeys.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < flagKeys.Length; i++)
        {
            string flagKey = flagKeys[i];
            if (string.IsNullOrWhiteSpace(flagKey))
            {
                continue;
            }

            if (GameProgressFlags.Get(flagKey.Trim()) != expectedState)
            {
                return false;
            }
        }

        return true;
    }
}
