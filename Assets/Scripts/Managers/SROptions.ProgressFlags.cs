using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using UnityEngine;

public partial class SROptions
{
    private const string ProgressFlagsEditCategory = "Flags - Edit";
    private const string ProgressFlagsInspectCategory = "Flags - Inspect";
    private const string ProgressFlagsRuntimeCategory = "Flags - Runtime List";
    private const string ProgressFlagsSavedCategory = "Flags - Saved List";
    private string progressFlagKey = GameProgressKeys.Boss01Defeated;
    private bool progressFlagValue = true;

    [Category(ProgressFlagsEditCategory)]
    [DisplayName("フラグ名")]
    [Sort(-30)]
    public string ProgressFlagKey
    {
        get => progressFlagKey;
        set => progressFlagKey = value;
    }

    [Category(ProgressFlagsEditCategory)]
    [DisplayName("設定する値")]
    [Sort(-29)]
    public bool ProgressFlagValue
    {
        get => progressFlagValue;
        set => progressFlagValue = value;
    }

    [Category(ProgressFlagsEditCategory)]
    [DisplayName("現在の値")]
    [Sort(-28)]
    public bool CurrentProgressFlagState => GameProgressFlags.Get(progressFlagKey);

    [Category(ProgressFlagsInspectCategory)]
    [DisplayName("現在のフラグ数")]
    [Sort(-27)]
    public int ProgressFlagCount => GameProgressFlags.Count;

    [Category(ProgressFlagsInspectCategory)]
    [DisplayName("セーブ内フラグ数")]
    [Sort(-26)]
    public int SelectedSlotSavedProgressFlagCount => GetSavedProgressFlagCount(selectedSaveSlot);

    [Category(ProgressFlagsInspectCategory)]
    [DisplayName("セーブ内フラグ状態")]
    [Sort(-25)]
    public string SelectedSlotSavedProgressFlagStatus => GetSavedProgressFlagStatus(selectedSaveSlot);

    [Category(ProgressFlagsRuntimeCategory)]
    [DisplayName("現在のフラグ")]
    [Sort(-30)]
    public string RuntimeProgressFlagsDump => BuildRuntimeProgressFlagsDump();

    [Category(ProgressFlagsSavedCategory)]
    [DisplayName("選択スロットの保存フラグ")]
    [Sort(-30)]
    public string SelectedSlotSavedProgressFlagsDump => BuildSavedProgressFlagsDump(selectedSaveSlot);

    [Browsable(false)]
    [Category(ProgressFlagsInspectCategory)]
    [DisplayName("選択スロットのカスタム保存データ")]
    [Sort(-21)]
    public string SelectedSlotCustomSectionsDump => BuildCustomSectionsDump(selectedSaveSlot);

    [Category(ProgressFlagsEditCategory)]
    [DisplayName("フラグを設定 / 更新")]
    [Sort(-26)]
    public void SetProgressFlag()
    {
        if (string.IsNullOrWhiteSpace(progressFlagKey))
        {
            Debug.LogWarning("[SROptions] Progress flag key is empty.");
            return;
        }

        GameProgressFlags.Set(progressFlagKey, progressFlagValue);
    }

    [Category(ProgressFlagsEditCategory)]
    [DisplayName("フラグを削除")]
    [Sort(-25)]
    public void RemoveProgressFlag()
    {
        if (string.IsNullOrWhiteSpace(progressFlagKey))
        {
            Debug.LogWarning("[SROptions] Progress flag key is empty.");
            return;
        }

        GameProgressFlags.Remove(progressFlagKey);
    }

    [Category(ProgressFlagsEditCategory)]
    [DisplayName("全フラグをクリア")]
    [Sort(-24)]
    public void ClearAllProgressFlags()
    {
        GameProgressFlags.ClearAll();
    }

    [Category(ProgressFlagsInspectCategory)]
    [DisplayName("フラグ表示を更新")]
    [Sort(-21)]
    public void ReloadProgressFlagView()
    {
        // SRDebugger refreshes option controls after action buttons are invoked.
    }

    [Category(ProgressFlagsInspectCategory)]
    [DisplayName("現在のフラグをログ出力")]
    [Sort(-20)]
    public void LogRuntimeProgressFlags()
    {
        Debug.Log(BuildRuntimeProgressFlagsDump());
    }

    [Category(ProgressFlagsInspectCategory)]
    [DisplayName("セーブ内フラグをログ出力")]
    [Sort(-19)]
    public void LogSelectedSlotSavedProgressFlags()
    {
        Debug.Log(BuildSavedProgressFlagsDump(selectedSaveSlot));
    }

    [Category(ProgressFlagsInspectCategory)]
    [DisplayName("カスタム保存データをログ出力")]
    [Sort(-18)]
    public void LogSelectedSlotCustomSections()
    {
        Debug.Log(BuildCustomSectionsDump(selectedSaveSlot));
    }

    private static string BuildRuntimeProgressFlagsDump()
    {
        List<GameProgressFlags.GameProgressFlagSnapshot> snapshot = GameProgressFlags.GetSnapshot();
        if (snapshot.Count == 0)
        {
            return "(no runtime flags)";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Runtime Flags ({snapshot.Count})");
        for (int i = 0; i < snapshot.Count; i++)
        {
            builder.Append(snapshot[i].Key);
            builder.Append(" = ");
            builder.AppendLine(snapshot[i].Value ? "true" : "false");
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildSavedProgressFlagsDump(int slotIndex)
    {
        if (!SaveRepository.TryRead(slotIndex, out SaveGameData saveData))
        {
            return $"Slot {slotIndex}: (no readable save)";
        }

        string json = saveData.GetCustomSectionJson(GameProgressFlags.SectionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return $"Slot {slotIndex}: (no saved progress flags section)";
        }

        try
        {
            GameProgressFlags.GameProgressFlagsPayload payload =
                JsonUtility.FromJson<GameProgressFlags.GameProgressFlagsPayload>(json);

            if (payload == null || payload.entries == null || payload.entries.Count == 0)
            {
                return $"Slot {slotIndex}: (saved progress flags empty)";
            }

            List<GameProgressFlags.GameProgressFlagEntry> entries =
                new List<GameProgressFlags.GameProgressFlagEntry>(payload.entries);
            entries.Sort((left, right) => string.CompareOrdinal(left.key, right.key));

            var builder = new StringBuilder();
            builder.AppendLine($"Slot {slotIndex} Saved Flags ({entries.Count})");
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(entries[i].key))
                {
                    continue;
                }

                builder.Append(entries[i].key);
                builder.Append(" = ");
                builder.AppendLine(entries[i].value ? "true" : "false");
            }

            return builder.ToString().TrimEnd();
        }
        catch (Exception exception)
        {
            return $"Slot {slotIndex}: failed to parse saved flags. {exception.Message}";
        }
    }

    private static string BuildCustomSectionsDump(int slotIndex)
    {
        if (!SaveRepository.TryRead(slotIndex, out SaveGameData saveData))
        {
            return $"Slot {slotIndex}: (no readable save)";
        }

        if (saveData.customSections == null || saveData.customSections.Count == 0)
        {
            return $"Slot {slotIndex}: (no custom sections)";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Slot {slotIndex} Custom Sections ({saveData.customSections.Count})");
        for (int i = 0; i < saveData.customSections.Count; i++)
        {
            SaveCustomSectionEntry section = saveData.customSections[i];
            builder.AppendLine($"[{section.sectionKey}]");
            builder.AppendLine(string.IsNullOrWhiteSpace(section.json) ? "(empty)" : section.json);
        }

        return builder.ToString().TrimEnd();
    }

    private static int GetSavedProgressFlagCount(int slotIndex)
    {
        if (!TryReadSavedProgressFlagsPayload(slotIndex, out var payload))
        {
            return 0;
        }

        if (payload.entries == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < payload.entries.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(payload.entries[i].key))
            {
                count++;
            }
        }

        return count;
    }

    private static string GetSavedProgressFlagStatus(int slotIndex)
    {
        if (!SaveRepository.TryRead(slotIndex, out SaveGameData saveData))
        {
            return "No readable save";
        }

        string json = saveData.GetCustomSectionJson(GameProgressFlags.SectionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return "No flags section";
        }

        return $"OK ({GetSavedProgressFlagCount(slotIndex)})";
    }

    private static bool TryReadSavedProgressFlagsPayload(
        int slotIndex,
        out GameProgressFlags.GameProgressFlagsPayload payload)
    {
        payload = null;

        if (!SaveRepository.TryRead(slotIndex, out SaveGameData saveData))
        {
            return false;
        }

        string json = saveData.GetCustomSectionJson(GameProgressFlags.SectionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            payload = JsonUtility.FromJson<GameProgressFlags.GameProgressFlagsPayload>(json);
            return payload != null;
        }
        catch
        {
            payload = null;
            return false;
        }
    }
}
