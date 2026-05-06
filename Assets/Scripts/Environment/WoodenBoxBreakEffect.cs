using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class WoodenBoxBreakEffect : MonoBehaviour
{
    [Header("Sprite Sheet")]
    [SerializeField] private Texture2D spriteSheetTexture;
    [SerializeField, Min(1)] private int frameColumns = 5;
    [SerializeField, Min(1)] private int frameRows = 3;
    [SerializeField, Min(0.01f)] private float frameSeconds = 0.04f;
    [SerializeField, Min(1f)] private float pixelsPerUnit = 100f;

    [Header("Renderer")]
    [SerializeField] private int sortingOrder = 2;

    [Header("Lifecycle")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool destroyOnComplete = true;

    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private Coroutine playRoutine;

    private void Awake()
    {
        CacheRenderer();
        ApplyRendererSettings();
    }

    private void OnEnable()
    {
        if (!playOnEnable)
        {
            return;
        }

        Play();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        frameColumns = Mathf.Max(1, frameColumns);
        frameRows = Mathf.Max(1, frameRows);
        frameSeconds = Mathf.Max(0.01f, frameSeconds);
        pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);

        CacheRenderer();
        ApplyRendererSettings();
    }
#endif

    public void Play()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        playRoutine = StartCoroutine(PlayRoutine());
    }

    public void MatchWorldSize(Vector2 targetWorldSize)
    {
        BuildFramesIfNeeded();

        if (frames == null || frames.Length == 0 || frames[0] == null)
        {
            return;
        }

        Vector2 frameWorldSize = GetSpriteContentWorldSize(frames[0]);
        if (frameWorldSize.x <= 0f || frameWorldSize.y <= 0f)
        {
            return;
        }

        transform.localScale = new Vector3(
            targetWorldSize.x / frameWorldSize.x,
            targetWorldSize.y / frameWorldSize.y,
            1f);
    }

    private IEnumerator PlayRoutine()
    {
        BuildFramesIfNeeded();

        if (spriteRenderer == null || frames == null || frames.Length == 0)
        {
            if (destroyOnComplete)
            {
                Destroy(gameObject);
            }

            yield break;
        }

        spriteRenderer.enabled = true;

        for (int i = 0; i < frames.Length; i++)
        {
            spriteRenderer.sprite = frames[i];
            yield return new WaitForSeconds(frameSeconds);
        }

        spriteRenderer.enabled = false;
        spriteRenderer.sprite = null;
        playRoutine = null;

        if (destroyOnComplete)
        {
            Destroy(gameObject);
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
                    SpriteMeshType.Tight);
            }
        }
    }

    private static Vector2 GetSpriteContentWorldSize(Sprite sprite)
    {
        if (sprite == null)
        {
            return Vector2.zero;
        }

        Vector2[] vertices = sprite.vertices;
        if (vertices != null && vertices.Length > 0)
        {
            Vector2 min = vertices[0];
            Vector2 max = vertices[0];

            for (int i = 1; i < vertices.Length; i++)
            {
                min = Vector2.Min(min, vertices[i]);
                max = Vector2.Max(max, vertices[i]);
            }

            Vector2 size = max - min;
            if (size.x > 0f && size.y > 0f)
            {
                return size;
            }
        }

        return sprite.bounds.size;
    }

    private void OnDestroy()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

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
