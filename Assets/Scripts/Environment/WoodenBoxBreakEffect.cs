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

    [Header("Launch")]
    [SerializeField, Min(0f)] private float launchDistance = 0.45f;
    [SerializeField, Min(0f)] private float launchRise = 0.18f;

    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private Coroutine playRoutine;
    private Coroutine launchRoutine;

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

        Vector2 frameWorldSize = WoodenBoxSpriteSheetFrames.GetSpriteContentWorldSize(frames[0]);
        if (frameWorldSize.x <= 0f || frameWorldSize.y <= 0f)
        {
            return;
        }

        transform.localScale = new Vector3(
            targetWorldSize.x / frameWorldSize.x,
            targetWorldSize.y / frameWorldSize.y,
            1f);
    }

    public void Launch(Vector2 direction)
    {
        if (launchRoutine != null)
        {
            StopCoroutine(launchRoutine);
            launchRoutine = null;
        }

        if (direction.sqrMagnitude <= 0.0001f || launchDistance <= 0f)
        {
            return;
        }

        CacheRenderer();
        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = direction.x < -0.0001f;
        }

        float launchMultiplier = Mathf.Max(1f, direction.magnitude);
        launchRoutine = StartCoroutine(LaunchRoutine(direction.normalized * launchMultiplier));
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

    private IEnumerator LaunchRoutine(Vector2 direction)
    {
        Vector3 startPosition = transform.position;
        float duration = Mathf.Max(frameSeconds * Mathf.Max(1, frameColumns * frameRows), 0.01f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedOut = 1f - ((1f - t) * (1f - t));
            float rise = Mathf.Sin(t * Mathf.PI) * launchRise;
            Vector3 offset = (Vector3)(direction * (launchDistance * easedOut)) + (Vector3.up * rise);
            transform.position = startPosition + offset;
            yield return null;
        }

        transform.position = startPosition + (Vector3)(direction * launchDistance);
        launchRoutine = null;
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

        frames = WoodenBoxSpriteSheetFrames.CreateFrames(
            spriteSheetTexture,
            frameColumns,
            frameRows,
            pixelsPerUnit);
    }

    private void OnDestroy()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        if (launchRoutine != null)
        {
            StopCoroutine(launchRoutine);
        }

        WoodenBoxSpriteSheetFrames.DestroyFrames(frames);
    }
}

internal static class WoodenBoxSpriteSheetFrames
{
    public static Sprite[] CreateFrames(Texture2D spriteSheetTexture, int frameColumns, int frameRows, float pixelsPerUnit)
    {
        if (spriteSheetTexture == null || frameColumns <= 0 || frameRows <= 0)
        {
            return System.Array.Empty<Sprite>();
        }

        int frameCount = frameColumns * frameRows;
        Sprite[] frames = new Sprite[frameCount];

        for (int i = 0; i < frameCount; i++)
        {
            frames[i] = CreateFrame(spriteSheetTexture, frameColumns, frameRows, i, pixelsPerUnit);
        }

        return frames;
    }

    public static Sprite CreateFrame(
        Texture2D spriteSheetTexture,
        int frameColumns,
        int frameRows,
        int frameIndex,
        float pixelsPerUnit)
    {
        if (spriteSheetTexture == null || frameColumns <= 0 || frameRows <= 0)
        {
            return null;
        }

        int frameWidth = spriteSheetTexture.width / frameColumns;
        int frameHeight = spriteSheetTexture.height / frameRows;
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            return null;
        }

        int clampedFrameIndex = Mathf.Clamp(frameIndex, 0, (frameColumns * frameRows) - 1);
        int visualRow = clampedFrameIndex / frameColumns;
        int xIndex = clampedFrameIndex % frameColumns;
        int x = xIndex * frameWidth;
        int y = spriteSheetTexture.height - ((visualRow + 1) * frameHeight);

        return Sprite.Create(
            spriteSheetTexture,
            new Rect(x, y, frameWidth, frameHeight),
            new Vector2(0.5f, 0.5f),
            Mathf.Max(1f, pixelsPerUnit),
            0,
            SpriteMeshType.Tight);
    }

    public static Vector2 GetSpriteContentWorldSize(Sprite sprite)
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

    public static void DestroyFrames(Sprite[] frames)
    {
        if (frames == null)
        {
            return;
        }

        for (int i = 0; i < frames.Length; i++)
        {
            DestroyFrame(frames[i]);
        }
    }

    public static void DestroyFrame(Sprite frame)
    {
        if (frame != null)
        {
            Object.Destroy(frame);
        }
    }
}
