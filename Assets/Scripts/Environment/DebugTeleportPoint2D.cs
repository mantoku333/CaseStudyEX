using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[AddComponentMenu("Environment/Debug Teleport Point 2D")]
public sealed class DebugTeleportPoint2D : MonoBehaviour
{
    [SerializeField] private string stageId = string.Empty;
    [SerializeField] private string pointId = string.Empty;
    [SerializeField] private Transform targetPoint;
    [SerializeField] private bool useColliderCenter = true;
    [SerializeField] private Vector2 offset;
    [SerializeField] private bool forceTriggerCollider = true;

    private Collider2D cachedCollider;

    public string StageId => string.IsNullOrWhiteSpace(stageId) ? gameObject.scene.name : stageId.Trim();
    public string PointId => string.IsNullOrWhiteSpace(pointId) ? name : pointId.Trim();
    public string Label => $"{StageId}/{PointId}";

    public Vector3 TeleportPosition
    {
        get
        {
            Vector3 position;
            if (targetPoint != null)
            {
                position = targetPoint.position;
            }
            else if (useColliderCenter && TryGetCollider(out Collider2D markerCollider))
            {
                position = markerCollider.enabled && gameObject.activeInHierarchy
                    ? markerCollider.bounds.center
                    : transform.TransformPoint(markerCollider.offset);
            }
            else
            {
                position = transform.position;
            }

            return position + new Vector3(offset.x, offset.y, 0f);
        }
    }

    private void Reset()
    {
        EnsureCollider();
    }

    private void OnValidate()
    {
        EnsureCollider();
    }

    private void Awake()
    {
        EnsureCollider();
    }

    private bool TryGetCollider(out Collider2D markerCollider)
    {
        if (cachedCollider == null)
        {
            cachedCollider = GetComponent<Collider2D>();
        }

        markerCollider = cachedCollider;
        return markerCollider != null;
    }

    private void EnsureCollider()
    {
        if (!TryGetCollider(out Collider2D markerCollider))
        {
            return;
        }

        if (forceTriggerCollider)
        {
            markerCollider.isTrigger = true;
        }
    }
}
