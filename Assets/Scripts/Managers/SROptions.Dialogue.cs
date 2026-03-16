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
        [Category("Dialogue")]
        [DisplayName("Play Sample Dialogue")]
        [Sort(1)]
        public void PlaySampleDialogue()
        {
            var runner = Object.FindFirstObjectByType<DialogueRunner>();
            if (runner != null)
            {
                if (runner.IsDialogueRunning)
                {
                    runner.Stop();
                }

                // SampleDialogue.yarn にある 'SampleNPC' ノードを指定して再生
                if (runner.Dialogue != null && runner.Dialogue.NodeExists("SampleNPC"))
                {
                    runner.StartDialogue("SampleNPC");
                }
                else
                {
                    Debug.LogError("[SRDebugger] 'SampleNPC' というノードが見つかりません。Yarn ProjectにSampleDialogue.yarnが登録されているか確認してください。");
                }
            }
            else
            {
                Debug.LogError("[SRDebugger] シーン内にDialogueRunnerが見つかりません。");
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
