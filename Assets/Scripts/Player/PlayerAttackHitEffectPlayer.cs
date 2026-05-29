using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AttackHitbox))]
[RequireComponent(typeof(Collider2D))]
public sealed class PlayerAttackHitEffectPlayer : MonoBehaviour
{
    [Header("Sprite Sheet")]
    [SerializeField] private Texture2D hitSpriteSheet;
    [SerializeField, Min(1)] private int frameColumns = 5;
    [SerializeField, Min(1)] private int frameRows = 3;
    [SerializeField, Min(1)] private int frameCount = 15;
    [SerializeField, Min(0.01f)] private float frameSeconds = 0.033f;
    [SerializeField, Min(1f)] private float pixelsPerUnit = 100f;

    [Header("Placement")]
    [SerializeField] private Vector3 worldOffset = Vector3.zero;
    [SerializeField] private Vector3 effectScale = Vector3.one;
    [SerializeField] private bool playOnlyOnEnemies = true;

    [Header("Renderer")]
    [SerializeField] private int sortingOrderOffset = 4;
    [SerializeField] private int minimumSortingOrder = 20;
    [SerializeField] private bool copyHitTargetMaterial = true;

    private readonly List<Sprite> generatedSprites = new();
    private AttackHitbox attackHitbox;
    private Collider2D attackCollider;
    private Sprite[] frames;

    private void Awake()
    {
        CacheComponents();
        BuildFramesIfNeeded();
    }

    private void OnEnable()
    {
        CacheComponents();
        if (attackHitbox != null)
        {
            attackHitbox.OnHit -= HandleAttackHit;
            attackHitbox.OnHit += HandleAttackHit;
        }
    }

    private void OnDisable()
    {
        if (attackHitbox != null)
        {
            attackHitbox.OnHit -= HandleAttackHit;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        frameColumns = Mathf.Max(1, frameColumns);
        frameRows = Mathf.Max(1, frameRows);
        frameCount = Mathf.Max(1, frameCount);
        frameSeconds = Mathf.Max(0.01f, frameSeconds);
        pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
    }
#endif

    private void CacheComponents()
    {
        if (attackHitbox == null)
        {
            attackHitbox = GetComponent<AttackHitbox>();
        }

        if (attackCollider == null)
        {
            attackCollider = GetComponent<Collider2D>();
        }
    }

    private void HandleAttackHit(Collider2D hitCollider)
    {
        if (hitCollider == null)
        {
            return;
        }

        if (playOnlyOnEnemies && !IsEnemyHit(hitCollider))
        {
            return;
        }

        BuildFramesIfNeeded();
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        PlayEffect(hitCollider);
    }

    private static bool IsEnemyHit(Collider2D hitCollider)
    {
        return hitCollider.GetComponentInParent<GameName.Enemy.EnemyController>() != null ||
               hitCollider.GetComponentInParent<GameName.Enemy.LastBossController>() != null;
    }

    private void PlayEffect(Collider2D hitCollider)
    {
        GameObject effectObject = new GameObject("PlayerAttackHitEffect");
        effectObject.transform.position = ResolveEffectPosition(hitCollider);
        effectObject.transform.localRotation = Quaternion.identity;
        effectObject.transform.localScale = effectScale;

        SpriteRenderer renderer = effectObject.AddComponent<SpriteRenderer>();
        ApplyRendererSettings(renderer, hitCollider);
        StartCoroutine(PlayEffectRoutine(effectObject, renderer));
    }

    private Vector3 ResolveEffectPosition(Collider2D hitCollider)
    {
        if (attackCollider == null)
        {
            return hitCollider.bounds.center + worldOffset;
        }

        Bounds attackBounds = attackCollider.bounds;
        Bounds hitBounds = hitCollider.bounds;
        float minX = Mathf.Max(attackBounds.min.x, hitBounds.min.x);
        float maxX = Mathf.Min(attackBounds.max.x, hitBounds.max.x);
        float minY = Mathf.Max(attackBounds.min.y, hitBounds.min.y);
        float maxY = Mathf.Min(attackBounds.max.y, hitBounds.max.y);

        if (minX <= maxX && minY <= maxY)
        {
            Vector3 overlapCenter = new Vector3(
                (minX + maxX) * 0.5f,
                (minY + maxY) * 0.5f,
                transform.position.z);

            return overlapCenter + worldOffset;
        }

        Vector2 attackPoint = attackCollider.ClosestPoint(hitBounds.center);
        Vector2 hitPoint = hitCollider.ClosestPoint(attackBounds.center);
        return ((Vector3)((attackPoint + hitPoint) * 0.5f)) + worldOffset;
    }

    private void ApplyRendererSettings(SpriteRenderer renderer, Collider2D hitCollider)
    {
        SpriteRenderer sourceRenderer = ResolveFrontmostRenderer(hitCollider);
        if (sourceRenderer == null)
        {
            renderer.sortingOrder = minimumSortingOrder;
            return;
        }

        renderer.sortingLayerID = sourceRenderer.sortingLayerID;
        renderer.sortingOrder = Mathf.Max(
            sourceRenderer.sortingOrder + sortingOrderOffset,
            minimumSortingOrder);

        if (copyHitTargetMaterial && sourceRenderer.sharedMaterial != null)
        {
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
        }
    }

    private static SpriteRenderer ResolveFrontmostRenderer(Collider2D hitCollider)
    {
        Transform targetRoot = ResolveHitTargetRoot(hitCollider);

        SpriteRenderer[] renderers = targetRoot.GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer frontmost = null;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (frontmost == null || CompareRendererSort(renderer, frontmost) > 0)
            {
                frontmost = renderer;
            }
        }

        return frontmost;
    }

    private static Transform ResolveHitTargetRoot(Collider2D hitCollider)
    {
        GameName.Enemy.EnemyController enemyController =
            hitCollider.GetComponentInParent<GameName.Enemy.EnemyController>();
        if (enemyController != null)
        {
            return enemyController.transform;
        }

        GameName.Enemy.LastBossController lastBossController =
            hitCollider.GetComponentInParent<GameName.Enemy.LastBossController>();
        if (lastBossController != null)
        {
            return lastBossController.transform;
        }

        Rigidbody2D attachedRigidbody = hitCollider.attachedRigidbody;
        return attachedRigidbody != null ? attachedRigidbody.transform : hitCollider.transform;
    }

    private static int CompareRendererSort(SpriteRenderer left, SpriteRenderer right)
    {
        int leftLayerValue = SortingLayer.GetLayerValueFromID(left.sortingLayerID);
        int rightLayerValue = SortingLayer.GetLayerValueFromID(right.sortingLayerID);
        if (leftLayerValue != rightLayerValue)
        {
            return leftLayerValue.CompareTo(rightLayerValue);
        }

        return left.sortingOrder.CompareTo(right.sortingOrder);
    }

    private IEnumerator PlayEffectRoutine(GameObject effectObject, SpriteRenderer renderer)
    {
        for (int i = 0; i < frames.Length; i++)
        {
            if (renderer == null)
            {
                yield break;
            }

            renderer.sprite = frames[i];
            yield return new WaitForSeconds(frameSeconds);
        }

        Destroy(effectObject);
    }

    private void BuildFramesIfNeeded()
    {
        if (frames == null || frames.Length == 0)
        {
            frames = BuildFrames(hitSpriteSheet);
        }
    }

    private Sprite[] BuildFrames(Texture2D spriteSheet)
    {
        if (spriteSheet == null)
        {
            return System.Array.Empty<Sprite>();
        }

        int frameWidth = spriteSheet.width / frameColumns;
        int frameHeight = spriteSheet.height / frameRows;
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            return System.Array.Empty<Sprite>();
        }

        int maxFrameCount = Mathf.Min(frameCount, frameColumns * frameRows);
        Sprite[] builtFrames = new Sprite[maxFrameCount];
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        int index = 0;

        for (int row = 0; row < frameRows && index < maxFrameCount; row++)
        {
            int y = spriteSheet.height - ((row + 1) * frameHeight);

            for (int column = 0; column < frameColumns && index < maxFrameCount; column++)
            {
                Rect rect = new Rect(column * frameWidth, y, frameWidth, frameHeight);
                Sprite sprite = Sprite.Create(
                    spriteSheet,
                    rect,
                    pivot,
                    pixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);

                builtFrames[index++] = sprite;
                generatedSprites.Add(sprite);
            }
        }

        return builtFrames;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < generatedSprites.Count; i++)
        {
            if (generatedSprites[i] != null)
            {
                Destroy(generatedSprites[i]);
            }
        }
    }
}
