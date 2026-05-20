using System.ComponentModel;
using Metroidvania.Managers;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[System.Serializable]
[DisplayName("Story/Yarn Dialogue")]
public sealed class StoryYarnDialogueMarker : Marker, INotification, INotificationOptionProvider
{
    [SerializeField] private string nodeName = "Start";
    [SerializeField] private bool useControllerDefaultStyle = true;
    [SerializeField] private DialogueStyle dialogueStyle = DialogueStyle.Bubble;
    [SerializeField] private bool pauseTimelineUntilComplete = true;
    [SerializeField] private string bubbleActorKey = string.Empty;

    public string NodeName => string.IsNullOrWhiteSpace(nodeName) ? "Start" : nodeName.Trim();
    public bool UseControllerDefaultStyle => useControllerDefaultStyle;
    public DialogueStyle DialogueStyle => dialogueStyle;
    public bool PauseTimelineUntilComplete => pauseTimelineUntilComplete;
    public string BubbleActorKey => string.IsNullOrWhiteSpace(bubbleActorKey) ? string.Empty : bubbleActorKey.Trim();

    public PropertyName id => new PropertyName(nameof(StoryYarnDialogueMarker));
    public NotificationFlags flags => NotificationFlags.TriggerOnce;
}
