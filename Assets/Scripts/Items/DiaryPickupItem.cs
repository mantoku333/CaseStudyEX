using UnityEngine;

[DisallowMultipleComponent]
public class DiaryPickupItem : MonoBehaviour, ISaveDataModule
{
    [SerializeField] private DiaryEntryData diaryEntryData;

    private bool isPickedUp;

    public int Priority => 253;

    private void OnEnable()
    {
        GameProgressFlags.FlagChanged += OnProgressFlagChanged;
        SaveManager.RegisterModule(this);
    }

    private void OnDisable()
    {
        GameProgressFlags.FlagChanged -= OnProgressFlagChanged;
        SaveManager.UnregisterModule(this);
    }

    private void Start()
    {
        RemoveIfAlreadyCollected();
    }

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();

        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isPickedUp || other == null || !other.CompareTag("Player"))
        {
            return;
        }

        CompletePickup();
    }

    private void CompletePickup()
    {
        isPickedUp = true;

        ItemEffectController effectController = GetComponent<ItemEffectController>();
        if (effectController != null && effectController.PlayPickupEffectAndDestroy())
        {
            return;
        }

        Destroy(gameObject);
    }

    private void OnProgressFlagChanged(string flagKey, bool value)
    {
        if (!value || isPickedUp || !MatchesProgressFlag(flagKey))
        {
            return;
        }

        RemoveCollectedItem();
    }

    private void RemoveIfAlreadyCollected()
    {
        string flagKey = ResolveProgressFlagKey();
        if (!string.IsNullOrWhiteSpace(flagKey) && GameProgressFlags.Get(flagKey))
        {
            RemoveCollectedItem();
        }
    }

    private bool MatchesProgressFlag(string flagKey)
    {
        string progressFlagKey = ResolveProgressFlagKey();
        return !string.IsNullOrWhiteSpace(progressFlagKey) &&
               string.Equals(flagKey, progressFlagKey, System.StringComparison.Ordinal);
    }

    private string ResolveProgressFlagKey()
    {
        return diaryEntryData != null ? diaryEntryData.GetProgressFlagKey() : string.Empty;
    }

    private void RemoveCollectedItem()
    {
        isPickedUp = true;
        Destroy(gameObject);
    }

    public void Capture(SaveGameData saveData)
    {
    }

    public void Restore(SaveGameData saveData)
    {
        RemoveIfAlreadyCollected();
    }
}
