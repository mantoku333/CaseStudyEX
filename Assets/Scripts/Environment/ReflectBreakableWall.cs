using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class ReflectBreakableWall : MonoBehaviour
{
    [Header("壁の大きさ")]
    [SerializeField] private Vector2 size = Vector2.one;
    [SerializeField] private bool syncSpriteSize = true;

    [Header("Break")]
    [SerializeField] private GameObject breakEffectPrefab;
    [SerializeField] private bool destroyBulletOnBreak = true;

    private BoxCollider2D boxCollider;
    private SpriteRenderer spriteRenderer;
    private bool isBroken;

    public bool DestroyBulletOnBreak => destroyBulletOnBreak;

    private void Awake()
    {
        CacheComponents();
        ApplySize();
    }

    public void Break()
    {
        if (isBroken)
        {
            return;
        }

        isBroken = true;

        if (breakEffectPrefab != null)
        {
            Instantiate(breakEffectPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }

    private void CacheComponents()
    {
        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider2D>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void ApplySize()
    {
        CacheComponents();

        size = new Vector2(
            Mathf.Max(0.01f, size.x),
            Mathf.Max(0.01f, size.y));

        if (boxCollider != null)
        {
            boxCollider.isTrigger = false;
            boxCollider.size = size;
        }

        if (syncSpriteSize && spriteRenderer != null)
        {
            spriteRenderer.drawMode = SpriteDrawMode.Tiled;
            spriteRenderer.size = size;
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheComponents();
        ApplySize();
    }

    private void OnValidate()
    {
        ApplySize();
    }
#endif
}
