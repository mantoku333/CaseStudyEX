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
    private static readonly Vector4[] WoodenBoxBreakEffectContentRects =
    {
        new Vector4(423f, 423f, 154f, 154f),
        new Vector4(431f, 384f, 205f, 224f),
        new Vector4(436f, 358f, 229f, 276f),
        new Vector4(440f, 339f, 246f, 326f),
        new Vector4(443f, 327f, 275f, 362f),
        new Vector4(446f, 321f, 284f, 375f),
        new Vector4(447f, 319f, 302f, 390f),
        new Vector4(43f, 269f, 723f, 455f),
        new Vector4(264f, 165f, 517f, 645f),
        new Vector4(451f, 315f, 344f, 447f),
        new Vector4(450f, 316f, 371f, 462f),
        new Vector4(433f, 319f, 396f, 474f),
        new Vector4(454f, 322f, 384f, 484f),
        new Vector4(193f, 326f, 649f, 492f),
        new Vector4(523f, 380f, 305f, 449f),
    };

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
        Rect frameRect = ResolveFrameRect(
            spriteSheetTexture,
            frameColumns,
            frameRows,
            frameWidth,
            frameHeight,
            clampedFrameIndex,
            x,
            y);
        Vector2 framePivot = ResolveFramePivot(frameRect, frameWidth, frameHeight, x, y);

        return Sprite.Create(
            spriteSheetTexture,
            frameRect,
            framePivot,
            Mathf.Max(1f, pixelsPerUnit),
            0,
            SpriteMeshType.Tight);
    }

    private static Vector2 ResolveFramePivot(Rect frameRect, int frameWidth, int frameHeight, int cellX, int cellY)
    {
        if (frameRect.width <= 0f || frameRect.height <= 0f)
        {
            return new Vector2(0.5f, 0.5f);
        }

        Vector2 cellCenter = new Vector2(
            cellX + (frameWidth * 0.5f),
            cellY + (frameHeight * 0.5f));

        return new Vector2(
            (cellCenter.x - frameRect.x) / frameRect.width,
            (cellCenter.y - frameRect.y) / frameRect.height);
    }

    private static Rect ResolveFrameRect(
        Texture2D spriteSheetTexture,
        int frameColumns,
        int frameRows,
        int frameWidth,
        int frameHeight,
        int frameIndex,
        int cellX,
        int cellY)
    {
        if (frameColumns == 5 &&
            frameRows == 3 &&
            frameIndex >= 0 &&
            frameIndex < WoodenBoxBreakEffectContentRects.Length)
        {
            Vector4 contentRect = WoodenBoxBreakEffectContentRects[frameIndex];
            if (contentRect.z > 0f && contentRect.w > 0f)
            {
                float scaleX = frameWidth / 1000f;
                float scaleY = frameHeight / 1000f;
                return new Rect(
                    cellX + (contentRect.x * scaleX),
                    cellY + frameHeight - ((contentRect.y + contentRect.w) * scaleY),
                    contentRect.z * scaleX,
                    contentRect.w * scaleY);
            }
        }

        return new Rect(cellX, cellY, frameWidth, frameHeight);
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
