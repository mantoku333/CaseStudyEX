using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class LocationAreaTrigger : MonoBehaviour
{
    [SerializeField] private string locationId = string.Empty;
    [SerializeField] private string playerTag = "Player";

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyLocation(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryApplyLocation(other);
    }

    private void TryApplyLocation(Collider2D other)
    {
        if (string.IsNullOrWhiteSpace(locationId))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(playerTag) && !other.CompareTag(playerTag))
        {
            return;
        }

        CurrentLocationService.SetCurrentLocation(locationId);
    }

    private void EnsureTriggerCollider()
    {
        Collider2D target = GetComponent<Collider2D>();
        if (target != null)
        {
            target.isTrigger = true;
        }
    }
}
