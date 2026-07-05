using UnityEngine;

[AddComponentMenu("CaseStudy/UI/Floating Guide Message Trigger")]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class FloatingGuideMessageTrigger : MonoBehaviour
{
    private enum TriggerAction
    {
        Show = 0,
        Hide = 1
    }

    [Header("Guide Message")]
    [SerializeField] private FloatingGuideMessageView messageView;
    [SerializeField] private TriggerAction action = TriggerAction.Show;
    [TextArea(1, 4)]
    [SerializeField] private string messageOverride = string.Empty;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool triggerOnce;
    [SerializeField] private bool disableColliderAfterTriggered;

    private Collider2D triggerCollider;
    private bool triggered;

    private void Reset()
    {
        ResolveReferences();
        ConfigureCollider();
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureCollider();
    }

    private void OnValidate()
    {
        ResolveReferences();
        ConfigureCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggerOnce && triggered)
        {
            return;
        }

        if (!IsPlayerCollider(other))
        {
            return;
        }

        if (messageView == null)
        {
            Debug.LogWarning($"[FloatingGuideMessageTrigger] Message View is missing. trigger='{name}'", this);
            return;
        }

        triggered = true;

        if (action == TriggerAction.Show)
        {
            if (!string.IsNullOrWhiteSpace(messageOverride))
            {
                messageView.ShowMessage(messageOverride);
            }
            else
            {
                messageView.Show();
            }
        }
        else
        {
            messageView.Hide();
        }

        if (disableColliderAfterTriggered && triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }
    }

    public void ResetTriggeredState()
    {
        triggered = false;
        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
        }
    }

    private void ResolveReferences()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider2D>();
        }

        if (messageView == null)
        {
            messageView = FindFirstObjectByType<FloatingGuideMessageView>(FindObjectsInactive.Include);
        }
    }

    private void ConfigureCollider()
    {
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null || IsPlayerActionHitbox(other))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return true;
        }

        string trimmedPlayerTag = playerTag.Trim();
        if (other.CompareTag(trimmedPlayerTag))
        {
            return true;
        }

        Transform current = other.transform;
        while (current != null)
        {
            if (current.CompareTag(trimmedPlayerTag))
            {
                return true;
            }

            current = current.parent;
        }

        PlayerController playerController = other.GetComponentInParent<PlayerController>();
        return playerController != null && playerController.CompareTag(trimmedPlayerTag);
    }

    private static bool IsPlayerActionHitbox(Collider2D other)
    {
        return other.GetComponent<AttackHitbox>() != null ||
               other.GetComponentInParent<AttackHitbox>() != null ||
               other.GetComponent<ParryHitbox>() != null ||
               other.GetComponentInParent<ParryHitbox>() != null;
    }
}
