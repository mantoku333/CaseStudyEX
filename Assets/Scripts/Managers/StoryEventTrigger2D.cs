using Metroidvania.Player;
using Player;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[AddComponentMenu("CaseStudy/Story/Story Event Trigger 2D")]
public sealed class StoryEventTrigger2D : MonoBehaviour
{
    [Header("Event")]
    [SerializeField] private string eventId = string.Empty;
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private bool skipWhenStoryEventRunning;

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool fireOnEnter = true;
    [SerializeField] private bool fireOnStay;
    [SerializeField] private bool disableColliderAfterTriggered;

    [Header("Debug")]
    [SerializeField] private bool logIfEventNotFound = true;

    private Collider2D triggerCollider;
    private bool triggered;

    private void Reset()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnValidate()
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

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (fireOnEnter)
        {
            TryTrigger(other);
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (fireOnStay)
        {
            TryTrigger(other);
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

    private void TryTrigger(Collider2D other)
    {
        if (triggerOnce && triggered)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(eventId))
        {
            return;
        }

        if (skipWhenStoryEventRunning && StoryEventRuntimeService.HasPendingEvents)
        {
            return;
        }

        if (!IsPlayerCollider(other))
        {
            return;
        }

        string trimmedEventId = eventId.Trim();
        bool enqueued = StoryEventRuntimeService.TryPlayEvent(trimmedEventId);
        if (!enqueued)
        {
            if (logIfEventNotFound)
            {
                Debug.LogWarning(
                    $"[StoryEventTrigger2D] Story event was not found or could not start. eventId='{trimmedEventId}', trigger='{name}'",
                    this);
            }

            return;
        }

        triggered = true;

        if (disableColliderAfterTriggered && triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (PlayerBodyColliderUtility.TryGetPlayerBodyFromCollider(
                other,
                out PlayerHealth playerHealth,
                out Collider2D bodyCollider))
        {
            return MatchesPlayerTag(bodyCollider != null ? bodyCollider.gameObject : null, playerHealth);
        }

        if (MatchesPlayerTag(other.gameObject, null))
        {
            return true;
        }

        global::PlayerController playerController = other.GetComponentInParent<global::PlayerController>();
        return playerController != null && MatchesPlayerTag(playerController.gameObject, null);
    }

    private bool MatchesPlayerTag(GameObject hitObject, PlayerHealth playerHealth)
    {
        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return true;
        }

        string trimmedPlayerTag = playerTag.Trim();
        if (hitObject != null && hitObject.CompareTag(trimmedPlayerTag))
        {
            return true;
        }

        if (playerHealth != null)
        {
            if (playerHealth.CompareTag(trimmedPlayerTag))
            {
                return true;
            }

            Transform root = playerHealth.transform.root;
            return root != null && root.CompareTag(trimmedPlayerTag);
        }

        return false;
    }
}
