using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class WindRiseEffectPlayer : MonoBehaviour
{
    [Header("Sprite Sheet")]
    [SerializeField] private Texture2D spriteSheetTexture;
    [SerializeField, Min(1)] private int frameColumns = 5;
    [SerializeField, Min(1)] private int frameRows = 6;
    [SerializeField, Min(0.01f)] private float frameSeconds = 0.04f;
    [SerializeField, Min(1f)] private float pixelsPerUnit = 512f;

    [Header("Layout")]
    [SerializeField] private bool matchParentCollider = true;
    [SerializeField] private Vector2 sizeMultiplier = Vector2.one;
    [SerializeField] private Vector3 localOffset = Vector3.zero;

    [Header("Renderer")]
    [SerializeField] private int sortingOrder = 1;

    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private Coroutine playRoutine;

    private void Awake()
    {
        CacheRenderer();
        ApplyRendererSettings();
        BuildFramesIfNeeded();
        MatchParentColliderIfNeeded();
    }

    private void OnEnable()
    {
        Play();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        frameColumns = Mathf.Max(1, frameColumns);
        frameRows = Mathf.Max(1, frameRows);
        frameSeconds = Mathf.Max(0.01f, frameSeconds);
        pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
        sizeMultiplier.x = Mathf.Max(0.01f, sizeMultiplier.x);
        sizeMultiplier.y = Mathf.Max(0.01f, sizeMultiplier.y);

        CacheRenderer();
        ApplyRendererSettings();
    }
#endif

    public void Play()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        playRoutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        BuildFramesIfNeeded();
        MatchParentColliderIfNeeded();

        if (spriteRenderer == null || frames == null || frames.Length == 0)
        {
            yield break;
        }

        spriteRenderer.enabled = true;

        int frameIndex = 0;
        while (true)
        {
            Sprite frame = frames[frameIndex];
            if (frame != null)
            {
                spriteRenderer.sprite = frame;
            }

            frameIndex = (frameIndex + 1) % frames.Length;
            yield return new WaitForSeconds(frameSeconds);
        }
    }

    private void CacheRenderer()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void ApplyRendererSettings()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.sortingOrder = sortingOrder;
    }

    private void BuildFramesIfNeeded()
    {
        if (frames != null && frames.Length > 0)
        {
            return;
        }

        if (spriteSheetTexture == null || frameColumns <= 0 || frameRows <= 0)
        {
            frames = System.Array.Empty<Sprite>();
            return;
        }

        int frameWidth = spriteSheetTexture.width / frameColumns;
        int frameHeight = spriteSheetTexture.height / frameRows;
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            frames = System.Array.Empty<Sprite>();
            return;
        }

        frames = new Sprite[frameColumns * frameRows];
        int frameIndex = 0;
        Vector2 pivot = new Vector2(0.5f, 0.5f);

        for (int visualRow = 0; visualRow < frameRows; visualRow++)
        {
            int y = spriteSheetTexture.height - ((visualRow + 1) * frameHeight);

            for (int xIndex = 0; xIndex < frameColumns; xIndex++)
            {
                int x = xIndex * frameWidth;
                Rect rect = new Rect(x, y, frameWidth, frameHeight);
                frames[frameIndex++] = Sprite.Create(
                    spriteSheetTexture,
                    rect,
                    pivot,
                    pixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);
            }
        }
    }

    private void MatchParentColliderIfNeeded()
    {
        if (!matchParentCollider || frames == null || frames.Length == 0 || frames[0] == null)
        {
            transform.localPosition = localOffset;
            return;
        }

        Collider2D parentCollider = GetComponentInParent<Collider2D>();
        if (parentCollider == null)
        {
            transform.localPosition = localOffset;
            return;
        }

        Vector2 spriteSize = frames[0].bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            transform.localPosition = localOffset;
            return;
        }

        Transform parentTransform = transform.parent;
        if (parentTransform == null)
        {
            transform.localPosition = localOffset;
            return;
        }

        float parentScaleX = Mathf.Max(0.0001f, Mathf.Abs(parentTransform.lossyScale.x));
        float parentScaleY = Mathf.Max(0.0001f, Mathf.Abs(parentTransform.lossyScale.y));

        transform.localPosition = (Vector3)parentCollider.offset + localOffset;
        transform.localScale = new Vector3(
            (parentCollider.bounds.size.x / parentScaleX) / spriteSize.x * sizeMultiplier.x,
            (parentCollider.bounds.size.y / parentScaleY) / spriteSize.y * sizeMultiplier.y,
            1f);
    }

    private void OnDisable()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
            spriteRenderer.sprite = null;
        }
    }

    private void OnDestroy()
    {
        if (frames == null)
        {
            return;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null)
            {
                Destroy(frames[i]);
            }
        }
    }
}
