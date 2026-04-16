using System;
using System.ComponentModel;
using System.IO;
using Metroidvania.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class SROptions
{
    private const string DebugCategory = "Debug";
    private const string SaveCategory = "Save";
    private static readonly bool EnableSaveLoadTrace = false;
    private int selectedSaveSlot = SaveManager.DefaultSlotIndex;
    private SaveSlotMeta SelectedSlotMeta => SaveManager.GetSlotMeta(selectedSaveSlot);

    [Category(DebugCategory)]
    [DisplayName("Reset Player Position (0,0,0)")]
    [Sort(-100)]
    public void ResetPlayerPositionToOrigin()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<global::PlayerController>();
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

    [Category(DebugCategory)]
    [DisplayName("Cheat Mode")]
    [Sort(-98)]
    public bool IsCheatMode
    {
        get
        {
            var player = UnityEngine.Object.FindFirstObjectByType<global::PlayerController>();
            if (player != null)
            {
                return player.GetComponent<DebugCheatModeController>() != null;
            }
            return false;
        }
        set
        {
            var player = UnityEngine.Object.FindFirstObjectByType<global::PlayerController>();
            if (player != null)
            {
                var cheatController = player.GetComponent<DebugCheatModeController>();
                if (value && cheatController == null)
                {
                    player.gameObject.AddComponent<DebugCheatModeController>();
                }
                else if (!value && cheatController != null)
                {
                    UnityEngine.Object.Destroy(cheatController);
                }
            }
        }
    }

    [Category(SaveCategory)]
    [DisplayName("Save Slot")]
    [Sort(-99)]
    public int SaveSlot
    {
        get => selectedSaveSlot;
        set => selectedSaveSlot = Mathf.Clamp(value, SaveManager.MinSlotIndex, SaveManager.MaxSlotIndex);
    }

    [Category(SaveCategory)]
    [DisplayName("Has Save In Slot")]
    [Sort(-97)]
    public bool HasSaveInSelectedSlot => SelectedSlotMeta.HasSave;

    [Category(SaveCategory)]
    [DisplayName("Slot Is Corrupted")]
    [Sort(-96)]
    public bool IsSelectedSlotCorrupted => SelectedSlotMeta.IsCorrupted;

    [Category(SaveCategory)]
    [DisplayName("Slot Scene")]
    [Sort(-95)]
    public string SelectedSlotSceneName
    {
        get
        {
            if (!SelectedSlotMeta.HasSave)
            {
                return "(empty)";
            }

            if (SelectedSlotMeta.IsCorrupted)
            {
                return "(corrupted)";
            }

            return string.IsNullOrEmpty(SelectedSlotMeta.SceneName) ? "(unknown)" : SelectedSlotMeta.SceneName;
        }
    }

    [Category(SaveCategory)]
    [DisplayName("Slot Saved At")]
    [Sort(-94)]
    public string SelectedSlotSavedAtLocal
    {
        get
        {
            if (!SelectedSlotMeta.HasSave)
            {
                return "(empty)";
            }

            if (SelectedSlotMeta.IsCorrupted)
            {
                return "(corrupted)";
            }

            if (string.IsNullOrEmpty(SelectedSlotMeta.SavedAtUtc))
            {
                return "(unknown)";
            }

            if (DateTime.TryParse(SelectedSlotMeta.SavedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var utcTime))
            {
                return utcTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            }

            return SelectedSlotMeta.SavedAtUtc;
        }
    }

    [Category(SaveCategory)]
    [DisplayName("Save Current Game")]
    [Sort(-93)]
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

        bool saved = SaveManager.TrySaveCurrentGame(selectedSaveSlot);
        if (!saved)
        {
            Debug.LogWarning("[SROptions] Save failed.");
        }
    }

    [Category(SaveCategory)]
    [DisplayName("Load Saved Game")]
    [Sort(-92)]
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

        bool loaded = SaveManager.TryLoadGame(selectedSaveSlot, SceneManager.GetActiveScene().name);
        if (!loaded)
        {
            Debug.LogWarning("[SROptions] Load failed. Fallback scene was loaded.");
        }
    }

    [Category(SaveCategory)]
    [DisplayName("Delete Save Data")]
    [Sort(-91)]
    public void DeleteSaveData()
    {
        bool deleted = SaveManager.DeleteSave(selectedSaveSlot);
        if (!deleted)
        {
            Debug.LogWarning("[SROptions] Delete save failed.");
        }
    }

    [Category(SaveCategory)]
    [DisplayName("Open Save File")]
    [Sort(-90)]
    public void OpenSelectedSaveFile()
    {
        string savePath = SaveRepository.GetSaveFilePath(selectedSaveSlot);
        if (string.IsNullOrWhiteSpace(savePath))
        {
            Debug.LogWarning("[SROptions] Save path is empty.");
            return;
        }

        if (!File.Exists(savePath))
        {
            Debug.LogWarning($"[SROptions] Save file not found. slot={selectedSaveSlot}, path='{savePath}'");
            return;
        }

        try
        {
            string url = new Uri(savePath).AbsoluteUri;
            Application.OpenURL(url);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SROptions] Failed to open save file: {exception}");
        }
    }
}
