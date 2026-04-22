using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
// Crate-specific setup. Break logic is inherited from AttackDestructible.
public class BreakableCrate : AttackDestructible
{
    [Header("Collision Settings")]
    [SerializeField] private bool forceColliderAsSolid = true;
    [SerializeField] private bool forceStaticBody = true;

    [Header("Gravity Settings")]
    [SerializeField] private bool useGravity = false;
    [SerializeField, Min(0f)] private float gravityScale = 1f;
    [SerializeField] private bool freezeHorizontalMovement = true;
    [SerializeField] private bool freezeRotation = true;

    private Collider2D cachedCollider;
    private Rigidbody2D cachedRigidbody;

    private void Awake()
    {
        CacheComponents();
        ApplyCollisionSettings();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        CacheComponents();
        ApplyCollisionSettings();
    }

    private void Reset()
    {
        CacheComponents();
        ApplyCollisionSettings();
    }
#endif

    private void CacheComponents()
    {
        if (cachedCollider == null)
        {
            cachedCollider = GetComponent<Collider2D>();
        }

        if (cachedRigidbody == null)
        {
            cachedRigidbody = GetComponent<Rigidbody2D>();
        }
    }

    private void ApplyCollisionSettings()
    {
        if (forceColliderAsSolid && cachedCollider != null)
        {
            cachedCollider.isTrigger = false;
        }

        if (cachedRigidbody == null)
        {
            return;
        }

        if (useGravity)
        {
            cachedRigidbody.bodyType = RigidbodyType2D.Dynamic;
            cachedRigidbody.gravityScale = Mathf.Max(0f, gravityScale);

            RigidbodyConstraints2D constraints = RigidbodyConstraints2D.None;
            if (freezeHorizontalMovement)
            {
                constraints |= RigidbodyConstraints2D.FreezePositionX;
            }

            if (freezeRotation)
            {
                constraints |= RigidbodyConstraints2D.FreezeRotation;
            }

            cachedRigidbody.constraints = constraints;
            return;
        }

        if (forceStaticBody)
        {
            cachedRigidbody.bodyType = RigidbodyType2D.Static;
            cachedRigidbody.gravityScale = 0f;
            cachedRigidbody.constraints = RigidbodyConstraints2D.None;
            cachedRigidbody.linearVelocity = Vector2.zero;
            cachedRigidbody.angularVelocity = 0f;
        }
    }
}
