using UnityEngine;

public sealed class FlagBasedUITrigger : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject targetUI;
    [SerializeField] private bool hideTargetOnAwake = true;

    [Header("Trigger Flag")]
    [SerializeField] private string triggerFlagKey = GameProgressKeys.PrologueCompleted;
    [SerializeField] private bool expectedFlagValue = true;

    [Header("One-Time Display")]
    [SerializeField] private bool showOnlyOnce = true;
    [SerializeField] private string shownStateFlagKey = string.Empty;

    [Header("Behavior")]
    [SerializeField] private bool disableAfterHandled = true;

    private bool hasHandled;

    private void Awake()
    {
        if (hideTargetOnAwake && targetUI != null)
        {
            targetUI.SetActive(false);
        }
    }

    private void OnEnable()
    {
        GameProgressFlags.FlagChanged += OnProgressFlagChanged;
        EvaluateAndApply();
    }

    private void OnDisable()
    {
        GameProgressFlags.FlagChanged -= OnProgressFlagChanged;
    }

    private void OnProgressFlagChanged(string flagKey, bool value)
    {
        string resolvedShownKey = ResolveShownStateFlagKey();
        if (!string.Equals(flagKey, triggerFlagKey, System.StringComparison.Ordinal) &&
            !string.Equals(flagKey, resolvedShownKey, System.StringComparison.Ordinal))
        {
            return;
        }

        EvaluateAndApply();
    }

    public void EvaluateAndApply()
    {
        if (hasHandled)
        {
            return;
        }

        if (targetUI == null || string.IsNullOrWhiteSpace(triggerFlagKey))
        {
            MarkHandledIfNeeded();
            return;
        }

        string resolvedShownKey = ResolveShownStateFlagKey();
        if (showOnlyOnce &&
            !string.IsNullOrWhiteSpace(resolvedShownKey) &&
            GameProgressFlags.Get(resolvedShownKey))
        {
            MarkHandledIfNeeded();
            return;
        }

        bool currentValue = GameProgressFlags.Get(triggerFlagKey);
        if (currentValue != expectedFlagValue)
        {
            return;
        }

        targetUI.SetActive(true);

        if (showOnlyOnce && !string.IsNullOrWhiteSpace(resolvedShownKey))
        {
            GameProgressFlags.Set(resolvedShownKey, true);
        }

        MarkHandledIfNeeded();
    }

    private string ResolveShownStateFlagKey()
    {
        if (!string.IsNullOrWhiteSpace(shownStateFlagKey))
        {
            return shownStateFlagKey.Trim();
        }

        if (string.IsNullOrWhiteSpace(triggerFlagKey))
        {
            return string.Empty;
        }

        return triggerFlagKey.Trim() + "_ui_shown";
    }

    private void MarkHandledIfNeeded()
    {
        hasHandled = true;

        if (disableAfterHandled)
        {
            enabled = false;
        }
    }
}
