using System.Collections;
using System.Collections.Generic;
using Metroidvania.Managers;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Yarn.Unity;

public sealed class StoryEventRunner : MonoBehaviour
{
    [SerializeField] private DialogueManager dialogueManager = null!;

    private readonly Queue<StoryEventDefinition> queuedEvents = new Queue<StoryEventDefinition>();
    private StoryEventDefinition activeEvent;
    private DialogueRunner activeDialogueRunner;
    private Coroutine activeRoutine;
    private bool waitingDialogueCompletion;

    public bool HasPendingEvents => activeEvent != null || queuedEvents.Count > 0;

    public void Enqueue(StoryEventDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        queuedEvents.Enqueue(definition);
        TryStartNextEvent();
    }

    public void ClearQueue()
    {
        queuedEvents.Clear();
    }

    private void OnDisable()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        waitingDialogueCompletion = false;
        UnsubscribeFromDialogueComplete();
        StoryPauseRuntime.ClearOverride();

        activeEvent = null;
        queuedEvents.Clear();
    }

    private void TryStartNextEvent()
    {
        if (activeEvent != null || activeRoutine != null)
        {
            return;
        }

        while (queuedEvents.Count > 0)
        {
            StoryEventDefinition nextEvent = queuedEvents.Dequeue();
            if (TryStartEvent(nextEvent))
            {
                return;
            }
        }
    }

    private bool TryStartEvent(StoryEventDefinition definition)
    {
        if (definition == null)
        {
            return false;
        }

        string activeSceneName = SceneManager.GetActiveScene().name;
        if (!definition.MatchesScene(activeSceneName))
        {
            return false;
        }

        if (!definition.CanRunByFlags())
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.dialogueNodeName))
        {
            Debug.LogWarning("[StoryEventRunner] dialogueNodeName is empty.");
            return false;
        }

        if (dialogueManager == null)
        {
            dialogueManager = FindFirstObjectByType<DialogueManager>();
        }

        if (dialogueManager == null || dialogueManager.Runner == null)
        {
            Debug.LogWarning($"[StoryEventRunner] DialogueManager not found. eventId='{definition.eventId}'");
            return false;
        }

        DialogueRunner runner = dialogueManager.Runner;
        if (runner.Dialogue == null || !runner.Dialogue.NodeExists(definition.dialogueNodeName))
        {
            Debug.LogWarning(
                $"[StoryEventRunner] Dialogue node not found. eventId='{definition.eventId}', node='{definition.dialogueNodeName}'");
            return false;
        }

        if (runner.IsDialogueRunning && definition.skipWhenDialogueRunning)
        {
            Debug.LogWarning(
                $"[StoryEventRunner] Dialogue is already running. eventId='{definition.eventId}' was skipped.");
            return false;
        }

        if (runner.IsDialogueRunning && !definition.skipWhenDialogueRunning)
        {
            runner.Stop();
        }

        activeEvent = definition;
        activeRoutine = StartCoroutine(RunEventSequence(definition, runner));
        return true;
    }

    private IEnumerator RunEventSequence(StoryEventDefinition definition, DialogueRunner runner)
    {
        StoryPauseRuntime.SetOverride(definition.pausePolicy);

        if (definition.preActions != null && definition.preActions.Count > 0)
        {
            yield return RunActions(definition.preActions);
        }

        activeDialogueRunner = runner;
        waitingDialogueCompletion = true;
        activeDialogueRunner.onDialogueComplete?.AddListener(OnDialogueComplete);

        definition.onStartMutations?.Apply();
        dialogueManager.StartConversation(definition.dialogueNodeName, definition.dialogueStyle);

        while (waitingDialogueCompletion)
        {
            yield return null;
        }

        UnsubscribeFromDialogueComplete();

        if (definition.postActions != null && definition.postActions.Count > 0)
        {
            yield return RunActions(definition.postActions);
        }

        definition.onCompleteMutations?.Apply();

        if (!string.IsNullOrWhiteSpace(definition.runOnceFlagKey))
        {
            GameProgressFlags.Set(definition.runOnceFlagKey.Trim(), true);
        }

        if (definition.autoSaveOnComplete)
        {
            SaveManager.TrySaveCurrentGame();
        }

        StoryPauseRuntime.ClearOverride();
        activeEvent = null;
        activeRoutine = null;

        TryStartNextEvent();
    }

    private IEnumerator RunActions(List<StoryEventActionDefinition> actions)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            StoryEventActionDefinition action = actions[i];
            if (action == null)
            {
                continue;
            }

            yield return ExecuteAction(action);
        }
    }

    private IEnumerator ExecuteAction(StoryEventActionDefinition action)
    {
        switch (action.actionType)
        {
            case StoryEventActionType.DelayRealtime:
                yield return WaitForRealtime(action.seconds);
                yield break;

            case StoryEventActionType.FadeOverlay:
                yield return StoryOverlayFader.Instance.FadeTo(
                    action.targetAlpha,
                    action.durationSeconds,
                    action.overlayColor);
                yield break;

            case StoryEventActionType.SwitchCameraPriority:
                ApplyCameraPriority(action);
                yield break;

            case StoryEventActionType.PlayTimeline:
                if (action.waitForCompletion)
                {
                    yield return PlayTimelineAndWait(action);
                }
                else
                {
                    PlayTimeline(action);
                }
                yield break;
        }
    }

    private static void ApplyCameraPriority(StoryEventActionDefinition action)
    {
        if (string.IsNullOrWhiteSpace(action.targetName))
        {
            return;
        }

        CinemachineCamera[] cameras = Object.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] == null)
            {
                continue;
            }

            if (cameras[i].name == action.targetName.Trim())
            {
                cameras[i].Priority = action.priority;
            }

            if (!string.IsNullOrWhiteSpace(action.secondaryTargetName) &&
                cameras[i].name == action.secondaryTargetName.Trim())
            {
                cameras[i].Priority = action.secondaryPriority;
            }
        }
    }

    private static void PlayTimeline(StoryEventActionDefinition action)
    {
        PlayableDirector director = FindDirectorByName(action.targetName);
        if (director == null)
        {
            return;
        }

        if (action.forceUnscaledTime)
        {
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        }

        if (action.stopBeforePlay)
        {
            director.Stop();
        }

        director.Play();
    }

    private static IEnumerator PlayTimelineAndWait(StoryEventActionDefinition action)
    {
        PlayableDirector director = FindDirectorByName(action.targetName);
        if (director == null)
        {
            yield break;
        }

        if (action.forceUnscaledTime)
        {
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        }

        if (action.stopBeforePlay)
        {
            director.Stop();
        }

        director.Play();
        while (director.state == PlayState.Playing)
        {
            yield return null;
        }
    }

    private static PlayableDirector FindDirectorByName(string directorName)
    {
        if (string.IsNullOrWhiteSpace(directorName))
        {
            return null;
        }

        string trimmedName = directorName.Trim();
        PlayableDirector[] directors = Object.FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None);
        for (int i = 0; i < directors.Length; i++)
        {
            if (directors[i] != null && directors[i].name == trimmedName)
            {
                return directors[i];
            }
        }

        return null;
    }

    private static IEnumerator WaitForRealtime(float seconds)
    {
        float duration = Mathf.Max(0f, seconds);
        if (duration <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void OnDialogueComplete()
    {
        waitingDialogueCompletion = false;
    }

    private void UnsubscribeFromDialogueComplete()
    {
        if (activeDialogueRunner != null)
        {
            activeDialogueRunner.onDialogueComplete?.RemoveListener(OnDialogueComplete);
            activeDialogueRunner = null;
        }
    }
}
