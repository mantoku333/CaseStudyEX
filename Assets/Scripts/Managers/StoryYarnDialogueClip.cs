using System.ComponentModel;
using Metroidvania.Managers;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[DisplayName("Story/Yarn Dialogue")]
public sealed class StoryYarnDialogueClip : PlayableAsset, ITimelineClipAsset
{
    [SerializeField, HideInInspector] private string clipId = string.Empty;
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
    public string ClipId => string.IsNullOrWhiteSpace(clipId) ? NodeName : clipId;
    public ClipCaps clipCaps => ClipCaps.ClipIn;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(clipId))
        {
            clipId = System.Guid.NewGuid().ToString("N");
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<StoryYarnDialoguePlayable> playable =
            ScriptPlayable<StoryYarnDialoguePlayable>.Create(graph);
        StoryYarnDialoguePlayable behaviour = playable.GetBehaviour();
        behaviour.nodeName = NodeName;
        behaviour.useControllerDefaultStyle = UseControllerDefaultStyle;
        behaviour.dialogueStyle = DialogueStyle;
        behaviour.pauseTimelineUntilComplete = PauseTimelineUntilComplete;
        behaviour.bubbleActorKey = BubbleActorKey;
        behaviour.clipId = ClipId;
        return playable;
    }
}
