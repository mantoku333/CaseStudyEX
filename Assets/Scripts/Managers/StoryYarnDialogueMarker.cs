using System.ComponentModel;
using System.Globalization;
using Metroidvania.Managers;
using UnityEngine;
using UnityEngine.Timeline;

[System.Serializable]
[HideInMenu]
[DisplayName("Story/Yarn Dialogue Point")]
public sealed class StoryYarnDialogueMarker : Marker
{
    [SerializeField, HideInInspector] private string markerId = string.Empty;
    [SerializeField] private string nodeName = "Start";
    [SerializeField] private bool useControllerDefaultStyle = true;
    [SerializeField] private DialogueStyle dialogueStyle = DialogueStyle.Bubble;
    [SerializeField] private bool pauseTimelineUntilComplete = true;
    [SerializeField] private string bubbleActorKey = string.Empty;

    public string TriggerKey =>
        string.Join(
            "|",
            string.IsNullOrWhiteSpace(markerId) ? nameof(StoryYarnDialogueMarker) : markerId.Trim(),
            NodeName,
            time.ToString("0.######", CultureInfo.InvariantCulture));

    public string NodeName => string.IsNullOrWhiteSpace(nodeName) ? "Start" : nodeName.Trim();
    public bool UseControllerDefaultStyle => useControllerDefaultStyle;
    public DialogueStyle DialogueStyle => dialogueStyle;
    public bool PauseTimelineUntilComplete => pauseTimelineUntilComplete;
    public string BubbleActorKey => string.IsNullOrWhiteSpace(bubbleActorKey) ? string.Empty : bubbleActorKey.Trim();

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(markerId))
        {
            markerId = System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
