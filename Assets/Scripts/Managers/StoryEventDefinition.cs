using System;
using System.Collections.Generic;
using Metroidvania.Managers;

[Serializable]
public sealed class StoryEventDefinition
{
    public string eventId = "event_id";
    public string sceneName = string.Empty;
    public string dialogueNodeName = "Start";
    public DialogueStyle dialogueStyle = DialogueStyle.ADV;
    public string runOnceFlagKey = string.Empty;
    public StoryFlagConditionSet conditions = new StoryFlagConditionSet();
    public StoryFlagMutationSet onStartMutations = new StoryFlagMutationSet();
    public StoryFlagMutationSet onCompleteMutations = new StoryFlagMutationSet();
    public List<StoryEventActionDefinition> preActions = new List<StoryEventActionDefinition>();
    public List<StoryEventActionDefinition> postActions = new List<StoryEventActionDefinition>();
    public StoryPausePolicy pausePolicy = StoryPausePolicy.UseDialogueDefault;
    public bool autoSaveOnComplete = true;
    public bool skipWhenDialogueRunning = true;

    public bool MatchesScene(string currentSceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return true;
        }

        return string.Equals(sceneName.Trim(), currentSceneName, StringComparison.Ordinal);
    }

    public bool CanRunByFlags()
    {
        if (!string.IsNullOrWhiteSpace(runOnceFlagKey) && GameProgressFlags.Get(runOnceFlagKey.Trim()))
        {
            return false;
        }

        return conditions == null || conditions.IsSatisfied();
    }
}
