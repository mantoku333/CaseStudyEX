using System;
using System.ComponentModel;
using System.IO;
using Metroidvania.Player;
using Player;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class SROptions
{
    private const string DebugCategory = "Debug";
    private const string SaveCategory = "Save";
    private static readonly bool EnableSaveLoadTrace = false;
    private int selectedSaveSlot = SaveManager.DefaultSlotIndex;
    private int selectedDebugTeleportPointIndex;
    private int selectedDebugEventAreaIndex;
    private string debugTeleportStageId = string.Empty;
    private SaveSlotMeta SelectedSlotMeta => SaveManager.GetSlotMeta(selectedSaveSlot);

    [Category(DebugCategory)]
    [DisplayName("プレイヤー位置を原点に戻す")]
    [Sort(-101)]
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
    [DisplayName("全回復")]
    [Sort(-100)]
    public void RestorePlayerFullHealth()
    {
        PlayerHealth playerHealth = ResolvePlayerHealth();
        if (playerHealth == null)
        {
            Debug.LogWarning("[SROptions] PlayerHealth not found.");
            return;
        }

        playerHealth.RestoreFullHealth();
    }

    [Category(DebugCategory)]
    [DisplayName("主人公 無敵")]
    [Sort(-99)]
    public bool IsPlayerDebugInvincible
    {
        get
        {
            PlayerHealth playerHealth = ResolvePlayerHealth();
            return playerHealth != null && playerHealth.DebugInvincible;
        }
        set
        {
            PlayerHealth playerHealth = ResolvePlayerHealth();
            if (playerHealth == null)
            {
                Debug.LogWarning("[SROptions] PlayerHealth not found.");
                return;
            }

            playerHealth.DebugInvincible = value;
        }
    }

    [Category(DebugCategory)]
    [DisplayName("ガチ死亡")]
    [Sort(-98)]
    public void ForcePlayerDeath()
    {
        PlayerHealth playerHealth = ResolvePlayerHealth();
        if (playerHealth == null)
        {
            Debug.LogWarning("[SROptions] PlayerHealth not found.");
            return;
        }

        playerHealth.ForceDeath();
    }

    [Category(DebugCategory)]
    [DisplayName("チートモード")]
    [Sort(-97)]
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

    [Category(DebugCategory)]
    [DisplayName("テレポート先ステージID")]
    [Sort(-95)]
    public string DebugTeleportStageId
    {
        get => debugTeleportStageId;
        set => debugTeleportStageId = value ?? string.Empty;
    }

    [Category(DebugCategory)]
    [DisplayName("テレポート先番号")]
    [Sort(-94)]
    [Increment(1)]
    public int DebugTeleportPointIndex
    {
        get => selectedDebugTeleportPointIndex;
        set => selectedDebugTeleportPointIndex = Mathf.Max(0, value);
    }

    [Category(DebugCategory)]
    [DisplayName("テレポート先一覧")]
    [Sort(-93)]
    public string DebugTeleportPointList
    {
        get
        {
            DebugTeleportPoint2D[] points = CollectDebugTeleportPoints();
            if (points.Length == 0)
            {
                return "(none)";
            }

            string result = string.Empty;
            for (int i = 0; i < points.Length; i++)
            {
                DebugTeleportPoint2D point = points[i];
                Vector3 position = point.TeleportPosition;
                string line = $"{i}: {point.Label} ({position.x:0.##}, {position.y:0.##})";
                result = string.IsNullOrEmpty(result) ? line : $"{result}\n{line}";
            }

            return result;
        }
    }

    [Category(DebugCategory)]
    [DisplayName("次のテレポート先")]
    [Sort(-92)]
    public void SelectNextDebugTeleportPoint()
    {
        DebugTeleportPoint2D[] points = CollectDebugTeleportPoints();
        if (points.Length == 0)
        {
            selectedDebugTeleportPointIndex = 0;
            Debug.LogWarning("[SROptions] DebugTeleportPoint2D not found in the active scene.");
            return;
        }

        selectedDebugTeleportPointIndex = (selectedDebugTeleportPointIndex + 1) % points.Length;
    }

    [Category(DebugCategory)]
    [DisplayName("選択先へテレポート")]
    [Sort(-91)]
    public void TeleportPlayerToSelectedDebugPoint()
    {
        DebugTeleportPoint2D[] points = CollectDebugTeleportPoints();
        if (points.Length == 0)
        {
            Debug.LogWarning("[SROptions] DebugTeleportPoint2D not found in the active scene.");
            return;
        }

        selectedDebugTeleportPointIndex = Mathf.Clamp(selectedDebugTeleportPointIndex, 0, points.Length - 1);
        TeleportPlayer(points[selectedDebugTeleportPointIndex]);
    }

    [Category(DebugCategory)]
    [DisplayName("イベントエリア番号")]
    [Sort(-89)]
    [Increment(1)]
    public int DebugEventAreaIndex
    {
        get => selectedDebugEventAreaIndex;
        set => selectedDebugEventAreaIndex = Mathf.Max(0, value);
    }

    [Category(DebugCategory)]
    [DisplayName("イベントエリア一覧")]
    [Sort(-88)]
    public string DebugEventAreaList
    {
        get
        {
            StoryEventTrigger2D[] triggers = CollectStoryEventTriggers();
            if (triggers.Length == 0)
            {
                return "(none)";
            }

            string result = string.Empty;
            for (int i = 0; i < triggers.Length; i++)
            {
                StoryEventTrigger2D trigger = triggers[i];
                Vector3 position = ResolveEventAreaTeleportPosition(trigger);
                string label = BuildEventAreaLabel(trigger);
                string line = $"{i}: {label} ({position.x:0.##}, {position.y:0.##})";
                result = string.IsNullOrEmpty(result) ? line : $"{result}\n{line}";
            }

            return result;
        }
    }

    [Category(DebugCategory)]
    [DisplayName("次のイベントエリア")]
    [Sort(-87)]
    public void SelectNextDebugEventArea()
    {
        StoryEventTrigger2D[] triggers = CollectStoryEventTriggers();
        if (triggers.Length == 0)
        {
            selectedDebugEventAreaIndex = 0;
            Debug.LogWarning("[SROptions] StoryEventTrigger2D not found in the active scene.");
            return;
        }

        selectedDebugEventAreaIndex = (selectedDebugEventAreaIndex + 1) % triggers.Length;
    }

    [Category(DebugCategory)]
    [DisplayName("選択イベントエリアへテレポート")]
    [Sort(-86)]
    public void TeleportPlayerToSelectedDebugEventArea()
    {
        StoryEventTrigger2D[] triggers = CollectStoryEventTriggers();
        if (triggers.Length == 0)
        {
            Debug.LogWarning("[SROptions] StoryEventTrigger2D not found in the active scene.");
            return;
        }

        selectedDebugEventAreaIndex = Mathf.Clamp(selectedDebugEventAreaIndex, 0, triggers.Length - 1);
        StoryEventTrigger2D trigger = triggers[selectedDebugEventAreaIndex];
        TeleportPlayer(ResolveEventAreaTeleportPosition(trigger), BuildEventAreaLabel(trigger));
    }

    [Category(SaveCategory)]
    [DisplayName("セーブスロット")]
    [Sort(-99)]
    public int SaveSlot
    {
        get => selectedSaveSlot;
        set => selectedSaveSlot = Mathf.Clamp(value, SaveManager.MinSlotIndex, SaveManager.MaxSlotIndex);
    }

    [Category(SaveCategory)]
    [DisplayName("スロットにセーブあり")]
    [Sort(-97)]
    public bool HasSaveInSelectedSlot => SelectedSlotMeta.HasSave;

    [Category(SaveCategory)]
    [DisplayName("スロット破損")]
    [Sort(-96)]
    public bool IsSelectedSlotCorrupted => SelectedSlotMeta.IsCorrupted;

    [Category(SaveCategory)]
    [DisplayName("保存シーン")]
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
    [DisplayName("保存日時")]
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
    [DisplayName("現在の状態をセーブ")]
    [Sort(-93)]
    public void SaveCurrentGame()
    {
        string source = DetectSaveLoadInvokeSource();
        if (EnableSaveLoadTrace)
        {
            Debug.Log($"[SROptions] SaveCurrentGame invoked. source={source}, scene='{SceneManager.GetActiveScene().name}', frame={Time.frameCount}");
        }

        bool saved = SaveManager.TrySaveCurrentGame(selectedSaveSlot);
        if (!saved)
        {
            Debug.LogWarning("[SROptions] Save failed.");
            ShowSaveLoadOverlay($"Save failed  Slot {selectedSaveSlot}");
            return;
        }

        ShowSaveLoadOverlay(
            $"Saved  Slot {selectedSaveSlot}\nSaved At: {GetSavedAtTextForSlot(selectedSaveSlot)}");
    }

    [Category(SaveCategory)]
    [DisplayName("セーブをロード")]
    [Sort(-92)]
    public void LoadSavedGame()
    {
        string source = DetectSaveLoadInvokeSource();
        if (EnableSaveLoadTrace)
        {
            Debug.Log($"[SROptions] LoadSavedGame invoked. source={source}, scene='{SceneManager.GetActiveScene().name}', frame={Time.frameCount}");
        }

        bool loaded = SaveManager.TryLoadGame(selectedSaveSlot, SceneManager.GetActiveScene().name);
        if (!loaded)
        {
            Debug.LogWarning("[SROptions] Load failed. Fallback scene was loaded.");
            ShowSaveLoadOverlay($"Load failed  Slot {selectedSaveSlot}");
            return;
        }

        ShowSaveLoadOverlay(
            $"Loaded  Slot {selectedSaveSlot}\nSaved At: {GetSavedAtTextForSlot(selectedSaveSlot)}");
    }

    [Category(SaveCategory)]
    [DisplayName("セーブデータを削除")]
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
    [DisplayName("セーブファイルを開く")]
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

    private static string DetectSaveLoadInvokeSource()
    {
        string stack = System.Environment.StackTrace;
        if (stack.Contains("SRDebugger.Editor.SROptionsWindow"))
        {
            return "SRDebuggerEditorWindow";
        }

        if (stack.Contains("SRDebugger.UI.Controls.Data.ActionControl"))
        {
            return "SRDebuggerInGameUI";
        }

        return "Unknown";
    }

    private static void ShowSaveLoadOverlay(string message)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        FullscreenDebugMessageOverlay.Show(message);
    }

    private static PlayerHealth ResolvePlayerHealth()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            PlayerHealth playerHealth = playerObject.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                return playerHealth;
            }

            playerHealth = playerObject.GetComponentInChildren<PlayerHealth>(true);
            if (playerHealth != null)
            {
                return playerHealth;
            }
        }

        return UnityEngine.Object.FindFirstObjectByType<PlayerHealth>();
    }

    private DebugTeleportPoint2D[] CollectDebugTeleportPoints()
    {
        DebugTeleportPoint2D[] allPoints = UnityEngine.Object.FindObjectsByType<DebugTeleportPoint2D>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        string stageFilter = debugTeleportStageId != null ? debugTeleportStageId.Trim() : string.Empty;
        Scene activeScene = SceneManager.GetActiveScene();

        int count = 0;
        for (int i = 0; i < allPoints.Length; i++)
        {
            DebugTeleportPoint2D point = allPoints[i];
            if (point == null || point.gameObject.scene != activeScene)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(stageFilter) &&
                !string.Equals(point.StageId, stageFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            count++;
        }

        if (count == 0)
        {
            return Array.Empty<DebugTeleportPoint2D>();
        }

        DebugTeleportPoint2D[] filteredPoints = new DebugTeleportPoint2D[count];
        int writeIndex = 0;
        for (int i = 0; i < allPoints.Length; i++)
        {
            DebugTeleportPoint2D point = allPoints[i];
            if (point == null || point.gameObject.scene != activeScene)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(stageFilter) &&
                !string.Equals(point.StageId, stageFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            filteredPoints[writeIndex] = point;
            writeIndex++;
        }

        Array.Sort(
            filteredPoints,
            (left, right) => string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase));

        return filteredPoints;
    }

    private static StoryEventTrigger2D[] CollectStoryEventTriggers()
    {
        StoryEventTrigger2D[] allTriggers = UnityEngine.Object.FindObjectsByType<StoryEventTrigger2D>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        Scene activeScene = SceneManager.GetActiveScene();

        int count = 0;
        for (int i = 0; i < allTriggers.Length; i++)
        {
            StoryEventTrigger2D trigger = allTriggers[i];
            if (trigger != null && trigger.gameObject.scene == activeScene)
            {
                count++;
            }
        }

        if (count == 0)
        {
            return Array.Empty<StoryEventTrigger2D>();
        }

        StoryEventTrigger2D[] filteredTriggers = new StoryEventTrigger2D[count];
        int writeIndex = 0;
        for (int i = 0; i < allTriggers.Length; i++)
        {
            StoryEventTrigger2D trigger = allTriggers[i];
            if (trigger == null || trigger.gameObject.scene != activeScene)
            {
                continue;
            }

            filteredTriggers[writeIndex] = trigger;
            writeIndex++;
        }

        Array.Sort(
            filteredTriggers,
            (left, right) => string.Compare(BuildEventAreaLabel(left), BuildEventAreaLabel(right), StringComparison.OrdinalIgnoreCase));

        return filteredTriggers;
    }

    private static string BuildEventAreaLabel(StoryEventTrigger2D trigger)
    {
        if (trigger == null)
        {
            return "(null)";
        }

        string eventId = string.IsNullOrWhiteSpace(trigger.EventId) ? "(no event)" : trigger.EventId;
        return $"{eventId}/{trigger.name}";
    }

    private static Vector3 ResolveEventAreaTeleportPosition(StoryEventTrigger2D trigger)
    {
        if (trigger == null)
        {
            return Vector3.zero;
        }

        Collider2D triggerCollider = trigger.GetComponent<Collider2D>();
        if (triggerCollider == null)
        {
            return trigger.transform.position;
        }

        if (triggerCollider.enabled && trigger.gameObject.activeInHierarchy)
        {
            return triggerCollider.bounds.center;
        }

        return trigger.transform.TransformPoint(triggerCollider.offset);
    }

    private static void TeleportPlayer(DebugTeleportPoint2D point)
    {
        if (point == null)
        {
            return;
        }

        TeleportPlayer(point.TeleportPosition, point.Label);
    }

    private static void TeleportPlayer(Vector3 destination, string label)
    {
        global::PlayerController player = ResolvePlayerController();
        if (player == null)
        {
            Debug.LogWarning("[SROptions] PlayerController not found.");
            return;
        }

        player.ClearExternalMovementDirection();

        Rigidbody2D rigidbody2D = player.GetComponent<Rigidbody2D>();
        if (rigidbody2D != null)
        {
            rigidbody2D.position = new Vector2(destination.x, destination.y);
            rigidbody2D.linearVelocity = Vector2.zero;
            rigidbody2D.angularVelocity = 0f;
            rigidbody2D.Sleep();
        }

        player.transform.position = destination;
        Physics2D.SyncTransforms();

        Debug.Log($"[SROptions] Teleported player to '{label}' at {destination}.");
    }

    private static global::PlayerController ResolvePlayerController()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            global::PlayerController player = playerObject.GetComponent<global::PlayerController>();
            if (player != null)
            {
                return player;
            }

            player = playerObject.GetComponentInChildren<global::PlayerController>(true);
            if (player != null)
            {
                return player;
            }
        }

        return UnityEngine.Object.FindFirstObjectByType<global::PlayerController>();
    }

    private static string GetSavedAtTextForSlot(int slotIndex)
    {
        SaveSlotMeta slotMeta = SaveManager.GetSlotMeta(slotIndex);
        if (!slotMeta.HasSave || slotMeta.IsCorrupted || string.IsNullOrEmpty(slotMeta.SavedAtUtc))
        {
            return "(unknown)";
        }

        if (DateTime.TryParse(slotMeta.SavedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var utcTime))
        {
            return utcTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        }

        return slotMeta.SavedAtUtc;
    }
}
