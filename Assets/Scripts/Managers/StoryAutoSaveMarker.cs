using System.ComponentModel;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
[DisplayName("Story/Auto Save")]
public sealed class StoryAutoSaveMarker : Marker, INotification, INotificationOptionProvider
{
    [SerializeField] private bool applyCompleteMutationsBeforeSave;

    public bool ApplyCompleteMutationsBeforeSave => applyCompleteMutationsBeforeSave;
    public PropertyName id => new PropertyName(nameof(StoryAutoSaveMarker));
    public NotificationFlags flags => NotificationFlags.TriggerOnce;
}
