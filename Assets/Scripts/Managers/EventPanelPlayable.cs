using UnityEngine;
using UnityEngine.Playables;

public sealed class EventPanelPlayable : PlayableBehaviour
{
    public string clipId = string.Empty;
    public EventPanelContent content;
    public bool pauseTimelineUntilClosed = true;
    public float autoCloseSecondsWhenNoButton = 3f;

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

        controller.TryShowPanelFromTimeline(
            clipId,
            content,
            pauseTimelineUntilClosed,
            autoCloseSecondsWhenNoButton);

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
