using TMPro;
using UnityEngine;

[CreateAssetMenu(fileName = "AutoSaveNotificationSettings", menuName = "Game/UI/Auto Save Notification Settings")]
public sealed class AutoSaveNotificationSettings : ScriptableObject
{
    public TMP_FontAsset font;
    [Min(0f)] public float displaySeconds = 2.5f;
    [Min(0f)] public float fadeSeconds = 0.5f;
}
