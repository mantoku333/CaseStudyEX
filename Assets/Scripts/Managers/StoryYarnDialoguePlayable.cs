using Metroidvania.Managers;
using UnityEngine;
using UnityEngine.Playables;

public sealed class StoryYarnDialoguePlayable : PlayableBehaviour
{
    public string nodeName = "Start";
    public bool useControllerDefaultStyle = true;
    public DialogueStyle dialogueStyle = DialogueStyle.Bubble;
    public bool pauseTimelineUntilComplete = true;
    public string bubbleActorKey = string.Empty;
    public string clipId = string.Empty;

    private bool started;

    public override void OnGraphStart(Playable playable)
    {
        started = false;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (!Application.isPlaying || started)
        {
            return;
        }

        StoryEventController controller = ResolveStoryEventController(playable, playerData);
        if (controller == null)
        {
            return;
        }

        controller.TryStartDialogueFromTimeline(
            clipId,
            nodeName,
            useControllerDefaultStyle,
            dialogueStyle,
            pauseTimelineUntilComplete,
            bubbleActorKey);

        started = true;
    }

    private static StoryEventController ResolveStoryEventController(Playable playable, object playerData)
    {
        if (playerData is StoryEventController boundController)
        {
            return boundController;
        }

        PlayableDirector director = playable.GetGraph().GetResolver() as PlayableDirector;
        return director != null ? director.GetComponent<StoryEventController>() : null;
    }
}
