using System.ComponentModel;
using Yarn.Unity;
using UnityEngine;
using SRDebugger;
using SRF.Service;

/// <summary>
/// SRDebugger options for dialogue testing.
/// </summary>
public partial class SROptions
{
    private const string SampleDialogueNode = "SampleNPC";

    [Category("Dialogue")]
    [DisplayName("Play Sample Dialogue (Bubble)")]
    [Sort(1)]
    public void PlaySampleDialogue()
    {
        var manager = Object.FindFirstObjectByType<Metroidvania.Managers.DialogueManager>();
        var player = Object.FindFirstObjectByType<global::PlayerController>();

        if (manager == null)
        {
            Debug.LogError("[SRDebugger] DialogueManager was not found in the scene.");
            return;
        }

        if (!manager.Runner.Dialogue.NodeExists(SampleDialogueNode))
        {
            Debug.LogError($"[SRDebugger] Yarn node '{SampleDialogueNode}' was not found.");
            return;
        }

        Transform target = player != null ? player.transform : null;
        manager.StartConversation(SampleDialogueNode, Metroidvania.Managers.DialogueStyle.Bubble, target);
    }

    [Category("Dialogue")]
    [DisplayName("Play Sample Dialogue (ADV Legacy)")]
    [Sort(2)]
    public void PlayBubbleDialogue()
    {
        var manager = Object.FindFirstObjectByType<Metroidvania.Managers.DialogueManager>();

        if (manager == null)
        {
            Debug.LogError("[SRDebugger] DialogueManager was not found in the scene.");
            return;
        }

        if (!manager.Runner.Dialogue.NodeExists(SampleDialogueNode))
        {
            Debug.LogError($"[SRDebugger] Yarn node '{SampleDialogueNode}' was not found.");
            return;
        }

        manager.StartConversation(SampleDialogueNode, Metroidvania.Managers.DialogueStyle.ADV);
    }

    [Category("Dialogue")]
    [DisplayName("Stop All Dialogues")]
    [Sort(3)]
    public void StopDialogue()
    {
        var runner = Object.FindFirstObjectByType<DialogueRunner>();
        if (runner != null && runner.IsDialogueRunning)
        {
            runner.Stop();
        }
    }
}
