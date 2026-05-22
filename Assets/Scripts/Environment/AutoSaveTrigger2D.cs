using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[AddComponentMenu("Environment/Auto Save Trigger 2D")]
public sealed class AutoSaveTrigger2D : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private int slotIndex = SaveManager.DefaultSlotIndex;
    [SerializeField] private bool saveOnlyOnce = true;
    [SerializeField, Min(0f)] private float cooldownSeconds = 1f;
    [SerializeField] private bool logResult = true;

    private Collider2D triggerCollider;
    private int playerOverlapCount;
    private bool hasSaved;
    private float nextSaveAllowedTime;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        slotIndex = Mathf.Clamp(slotIndex, SaveManager.MinSlotIndex, SaveManager.MaxSlotIndex);
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void OnDisable()
    {
        playerOverlapCount = 0;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        playerOverlapCount++;
        if (playerOverlapCount > 1)
        {
            return;
        }

        TryAutoSave();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        playerOverlapCount = Mathf.Max(0, playerOverlapCount - 1);
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return other.GetComponentInParent<global::PlayerController>() != null;
        }

        if (other.CompareTag(playerTag))
        {
            return true;
        }

        var player = other.GetComponentInParent<global::PlayerController>();
        return player != null && player.CompareTag(playerTag);
    }

    private void TryAutoSave()
    {
        if (saveOnlyOnce && hasSaved)
        {
            return;
        }

        if (Time.unscaledTime < nextSaveAllowedTime)
        {
            return;
        }

        nextSaveAllowedTime = Time.unscaledTime + cooldownSeconds;
        int targetSlotIndex = Mathf.Clamp(slotIndex, SaveManager.MinSlotIndex, SaveManager.MaxSlotIndex);
        bool saved = SaveManager.TrySaveCurrentGame(targetSlotIndex);
        if (saved)
        {
            hasSaved = true;
        }

        if (logResult)
        {
            Debug.Log(
                saved
                    ? $"[AutoSaveTrigger2D] Saved current game. slot={targetSlotIndex}, trigger='{name}'"
                    : $"[AutoSaveTrigger2D] Failed to save current game. slot={targetSlotIndex}, trigger='{name}'",
                this);
        }
    }

    private void EnsureTriggerCollider()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider2D>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }
}
