using System.ComponentModel;
using Metroidvania.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class SROptions
{
    private const bool EnableSaveLoadTrace = false;

    [Category("Debug")]
    [DisplayName("Reset Player Position (0,0,0)")]
    [Sort(-100)]
    public void ResetPlayerPositionToOrigin()
    {
        var player = Object.FindFirstObjectByType<global::PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("[SROptions] PlayerController not found.");
            return;
        }

        player.transform.position = Vector3.zero;

        var rigidbody2D = player.GetComponent<Rigidbody2D>();
        if (rigidbody2D != null)
        {
            rigidbody2D.linearVelocity = Vector2.zero;
            rigidbody2D.angularVelocity = 0f;
        }
    }

    [Category("Debug")]
    [DisplayName("Cheat Mode")]
    [Sort(-99)]
    public bool IsCheatMode
    {
        get
        {
            var player = Object.FindFirstObjectByType<global::PlayerController>();
            if (player != null)
            {
                return player.GetComponent<DebugCheatModeController>() != null;
            }
            return false;
        }
        set
        {
            var player = Object.FindFirstObjectByType<global::PlayerController>();
            if (player != null)
            {
                var cheatController = player.GetComponent<DebugCheatModeController>();
                if (value && cheatController == null)
                {
                    player.gameObject.AddComponent<DebugCheatModeController>();
                }
                else if (!value && cheatController != null)
                {
                    Object.Destroy(cheatController);
                }
            }
        }
    }

    [Category("Debug")]
    [DisplayName("Save Current Game")]
    [Sort(-98)]
    public void SaveCurrentGame()
    {
        if (EnableSaveLoadTrace)
        {
            string stack = System.Environment.StackTrace;
            string source = stack.Contains("SRDebugger.Editor.SROptionsWindow")
                ? "SRDebuggerEditorWindow"
                : stack.Contains("SRDebugger.UI.Controls.Data.ActionControl")
                    ? "SRDebuggerInGameUI"
                    : "Unknown";
            Debug.Log($"[SROptions] SaveCurrentGame invoked. source={source}, scene='{SceneManager.GetActiveScene().name}', frame={Time.frameCount}");
        }

        bool saved = SaveManager.TrySaveCurrentGame();
        if (!saved)
        {
            Debug.LogWarning("[SROptions] Save failed.");
        }
    }

    [Category("Debug")]
    [DisplayName("Load Saved Game")]
    [Sort(-97)]
    public void LoadSavedGame()
    {
        if (EnableSaveLoadTrace)
        {
            string stack = System.Environment.StackTrace;
            string source = stack.Contains("SRDebugger.Editor.SROptionsWindow")
                ? "SRDebuggerEditorWindow"
                : stack.Contains("SRDebugger.UI.Controls.Data.ActionControl")
                    ? "SRDebuggerInGameUI"
                    : "Unknown";
            Debug.Log($"[SROptions] LoadSavedGame invoked. source={source}, scene='{SceneManager.GetActiveScene().name}', frame={Time.frameCount}");
        }

        bool loaded = SaveManager.TryLoadGame(SceneManager.GetActiveScene().name);
        if (!loaded)
        {
            Debug.LogWarning("[SROptions] Load failed. Fallback scene was loaded.");
        }
    }

    [Category("Debug")]
    [DisplayName("Delete Save Data")]
    [Sort(-96)]
    public void DeleteSaveData()
    {
        bool deleted = SaveManager.DeleteSave();
        if (!deleted)
        {
            Debug.LogWarning("[SROptions] Delete save failed.");
        }
    }
}
