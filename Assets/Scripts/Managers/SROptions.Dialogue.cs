using System.ComponentModel;
using Yarn.Unity;
using UnityEngine;
using SRDebugger;
using SRF.Service;

/// <summary>
/// SRDebuggerを用いてデバッグメニューからダイアログを呼び出すための機能拡張
/// </summary>
public partial class SROptions
    {
        private const string SampleDialogueNode = "SampleNPC";

        [Category("Dialogue")]
        [DisplayName("Play Sample Dialogue")]
        [Sort(1)]
        public void PlaySampleDialogue()
        {
            var manager = Object.FindFirstObjectByType<Metroidvania.Managers.DialogueManager>();
            if (manager != null)
            {
                if (!manager.Runner.Dialogue.NodeExists(SampleDialogueNode))
                {
                    Debug.LogError($"[SRDebugger] Yarnプロジェクトにノード '{SampleDialogueNode}' が見つかりません。ノード名を確認してください。");
                    return;
                }
                manager.StartConversation(SampleDialogueNode, Metroidvania.Managers.DialogueStyle.ADV);
            }
            else
            {
                Debug.LogError("[SRDebugger] シーン内にDialogueManagerが見つかりません。");
            }
        }

        [Category("Dialogue")]
        [DisplayName("Play Bubble Dialogue (On Player)")]
        [Sort(2)]
        public void PlayBubbleDialogue()
        {
            var manager = Object.FindFirstObjectByType<Metroidvania.Managers.DialogueManager>();
            var player = Object.FindFirstObjectByType<Metroidvania.Player.PlayerPlatformerMockController>();

            if (manager != null)
            {
                if (!manager.Runner.Dialogue.NodeExists(SampleDialogueNode))
                {
                    Debug.LogError($"[SRDebugger] Yarnプロジェクトにノード '{SampleDialogueNode}' が見つかりません。ノード名を確認してください。");
                    return;
                }
                // SRDebuggerから直呼び出しテスト用に、プレイヤーの頭上に吹き出しを出す
                Transform target = player != null ? player.transform : null;
                manager.StartConversation(SampleDialogueNode, Metroidvania.Managers.DialogueStyle.Bubble, target);
            }
            else
            {
                Debug.LogError("[SRDebugger] シーン内にDialogueManagerが見つかりません。");
            }
        }
        
        [Category("Dialogue")]
        [DisplayName("Stop All Dialogues")]
        [Sort(2)]
        public void StopDialogue()
        {
            var runner = Object.FindFirstObjectByType<DialogueRunner>();
            if (runner != null && runner.IsDialogueRunning)
            {
                runner.Stop();
            }
        }
    }
