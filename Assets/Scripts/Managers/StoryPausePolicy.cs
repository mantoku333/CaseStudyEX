public enum StoryPausePolicy
{
    UseDialogueDefault = 0,
    None = 1,
    GameplayOnly = 2,
    TimeScaleZero = 3
}

public static class StoryPauseRuntime
{
    private static bool hasOverride;
    private static StoryPausePolicy overridePolicy = StoryPausePolicy.UseDialogueDefault;

    public static StoryPausePolicy DialogueDefaultPolicy { get; set; } = StoryPausePolicy.TimeScaleZero;

    public static StoryPausePolicy EffectivePolicy
    {
        get
        {
            StoryPausePolicy policy = hasOverride ? overridePolicy : DialogueDefaultPolicy;
            return policy == StoryPausePolicy.UseDialogueDefault ? DialogueDefaultPolicy : policy;
        }
    }

    public static void SetOverride(StoryPausePolicy policy)
    {
        hasOverride = true;
        overridePolicy = policy;
    }

    public static void ClearOverride()
    {
        hasOverride = false;
        overridePolicy = StoryPausePolicy.UseDialogueDefault;
    }
}
