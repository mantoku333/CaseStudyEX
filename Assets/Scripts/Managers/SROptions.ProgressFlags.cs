using System.ComponentModel;
using UnityEngine;

public partial class SROptions
{
    private const string ProgressFlagsCategory = "Progress Flags";
    private string progressFlagKey = GameProgressKeys.Boss01Defeated;
    private bool progressFlagValue = true;

    [Category(ProgressFlagsCategory)]
    [DisplayName("Flag Key")]
    [Sort(-30)]
    public string ProgressFlagKey
    {
        get => progressFlagKey;
        set => progressFlagKey = value;
    }

    [Category(ProgressFlagsCategory)]
    [DisplayName("Flag Value")]
    [Sort(-29)]
    public bool ProgressFlagValue
    {
        get => progressFlagValue;
        set => progressFlagValue = value;
    }

    [Category(ProgressFlagsCategory)]
    [DisplayName("Current Flag State")]
    [Sort(-28)]
    public bool CurrentProgressFlagState => GameProgressFlags.Get(progressFlagKey);

    [Category(ProgressFlagsCategory)]
    [DisplayName("Registered Flag Count")]
    [Sort(-27)]
    public int ProgressFlagCount => GameProgressFlags.Count;

    [Category(ProgressFlagsCategory)]
    [DisplayName("Set / Update Flag")]
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

    [Category(ProgressFlagsCategory)]
    [DisplayName("Remove Flag")]
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

    [Category(ProgressFlagsCategory)]
    [DisplayName("Clear All Flags")]
    [Sort(-24)]
    public void ClearAllProgressFlags()
    {
        GameProgressFlags.ClearAll();
    }
}
