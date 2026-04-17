using System.ComponentModel;
using UnityEngine;

public partial class SROptions
{
    private const string StoryEventsCategory = "Story Events";
    private const string PrologueEventId = "prologue";

    [Category(StoryEventsCategory)]
    [DisplayName("Play Prologue (Ignore Flags)")]
    [Sort(-10)]
    public void PlayPrologueIgnoringFlags()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SROptions] This action is available only in Play Mode.");
            return;
        }

        if (StoryEventRuntimeService.TryPlayEventFromDebugger(PrologueEventId, ignoreFlags: true))
        {
            return;
        }

        Debug.LogWarning(
            "[SROptions] Failed to queue story event 'prologue'. Verify build scene registration and StoryEvent catalog.");
    }
}
