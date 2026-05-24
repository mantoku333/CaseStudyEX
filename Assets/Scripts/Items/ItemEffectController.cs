using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ItemEffectController : MonoBehaviour
{
    [SerializeField] private ItemEffectSettings settings;
    [SerializeField] private Transform effectTransform;
    [SerializeField] private SpriteRenderer effectRenderer;
    [SerializeField] private AudioClip pickupSound;

    private Coroutine playbackRoutine;
    private bool isPickupPlaying;
    private Sprite[] loopFrames;
    private Vector3[] loopFrameOffsets;
    private Sprite[] pickupFrames;
    private Vector3[] pickupFrameOffsets;
    private Vector3 pickupStartPosition;
    private readonly List<Sprite> generatedSprites = new();

    private void Awake()
    {
        CacheReferences();
        EnsureFramesBuilt();
        ApplyVisualSetup();
        ShowLoopFirstFrame();
    }

    private void Start()
    {
        if (isPickupPlaying || settings == null || effectRenderer == null)
        {
            return;
        }

        StartLoopPlayback();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheReferences();
        EnsureFramesBuilt();
        ApplyVisualSetup();

        if (!Application.isPlaying)
        {
            ShowLoopFirstFrame();
        }
    }
#endif

    public bool PlayPickupEffectAndDestroy()
    {
        CacheReferences();

        if (isPickupPlaying)
        {
            return true;
        }

        PlayPickupSound();

        if (settings == null || effectRenderer == null)
        {
            return false;
        }

        DisablePickupColliders();
        isPickupPlaying = true;
        pickupStartPosition = effectTransform != null
            ? effectTransform.localPosition
            : (settings != null ? settings.visualOffset : Vector3.zero);

        if (settings != null)
        {
            pickupStartPosition += settings.pickupVisualOffset;
        }

        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
        }

        playbackRoutine = StartCoroutine(PlayPickupAndDestroyRoutine());
        return true;
    }

    private void StartLoopPlayback()
    {
        if (loopFrames == null || loopFrames.Length == 0)
        {
            return;
        }

        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
        }

        playbackRoutine = StartCoroutine(PlayLoopRoutine());
    }

    private IEnumerator PlayLoopRoutine()
    {
        while (!isPickupPlaying)
        {
            yield return PlayFrames(loopFrames, loopFrameOffsets, settings.loopFrameSeconds);
        }
    }

    private IEnumerator PlayPickupAndDestroyRoutine()
    {
        yield return PlayFrames(
            pickupFrames,
            null,
            settings.pickupFrameSeconds,
            pickupStartPosition);
        Destroy(gameObject);
    }

    private IEnumerator PlayFrames(
        Sprite[] frames,
        Vector3[] frameOffsets,
        float frameSeconds,
        Vector3? anchoredBasePosition = null,
        Vector3? anchoredReferenceOffset = null)
    {
        if (effectRenderer == null || frames == null || frames.Length == 0)
        {
            yield break;
        }

        effectRenderer.enabled = true;

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] == null)
            {
                continue;
            }

            effectRenderer.sprite = frames[i];
            ApplyFrameOffset(frameOffsets, i, anchoredBasePosition, anchoredReferenceOffset);
            yield return new WaitForSeconds(frameSeconds);
        }
    }

    private void ApplyVisualSetup()
    {
        if (effectTransform != null)
        {
            Vector3 rootScale = transform.localScale;
            Vector3 targetScale = settings != null ? settings.visualScale : Vector3.one;
            Vector3 targetOffset = settings != null ? settings.visualOffset : Vector3.zero;

            effectTransform.localPosition = targetOffset;
            effectTransform.localRotation = Quaternion.Inverse(transform.localRotation);
            effectTransform.localScale = new Vector3(
                SafeDivide(targetScale.x, rootScale.x),
                SafeDivide(targetScale.y, rootScale.y),
                SafeDivide(targetScale.z, rootScale.z));
        }

        if (effectRenderer != null)
        {
            effectRenderer.color = Color.white;

            if (settings != null)
            {
                effectRenderer.sortingOrder = settings.sortingOrder;
            }
        }
    }

    private void ShowLoopFirstFrame()
    {
        if (settings == null || effectRenderer == null || loopFrames == null || loopFrames.Length == 0)
        {
            return;
        }

        effectRenderer.enabled = true;
        effectRenderer.sprite = loopFrames[0];
        ApplyFrameOffset(loopFrameOffsets, 0);
    }

    private void EnsureFramesBuilt()
    {
        if (settings == null)
        {
            return;
        }

        if (loopFrames == null || loopFrames.Length == 0)
        {
            loopFrames = BuildFrames(
                settings.loopSpriteSheet,
                settings.loopColumns,
                settings.loopRows,
                settings.loopFrameCount,
                settings.loopFrames,
                out loopFrameOffsets);
        }

        if (pickupFrames == null || pickupFrames.Length == 0)
        {
            pickupFrames = BuildFrames(
                settings.pickupSpriteSheet,
                settings.pickupColumns,
                settings.pickupRows,
                settings.pickupFrameCount,
                settings.pickupFrames,
                out pickupFrameOffsets,
                true,
                false);
        }
    }

    private Sprite[] BuildFrames(
        Texture2D spriteSheet,
        int columns,
        int rows,
        int frameCount,
        Sprite[] fallbackFrames,
        out Vector3[] frameOffsets,
        bool stabilizeX = true,
        bool stabilizeY = true)
    {
        if (spriteSheet == null || columns <= 0 || rows <= 0 || frameCount <= 0)
        {
            frameOffsets = BuildZeroOffsets(fallbackFrames);
            return fallbackFrames;
        }

        int frameWidth = spriteSheet.width / columns;
        int frameHeight = spriteSheet.height / rows;

        if (frameWidth <= 0 || frameHeight <= 0)
        {
            frameOffsets = BuildZeroOffsets(fallbackFrames);
            return fallbackFrames;
        }

        int maxFrameCount = Mathf.Min(frameCount, columns * rows);
        Sprite[] frames = new Sprite[maxFrameCount];
        frameOffsets = new Vector3[maxFrameCount];
        int index = 0;
        Color32[] pixels = spriteSheet.GetPixels32();
        int textureWidth = spriteSheet.width;
        Vector3 baseOffset = settings != null ? settings.visualOffset : Vector3.zero;

        for (int row = 0; row < rows && index < maxFrameCount; row++)
        {
            int y = spriteSheet.height - ((row + 1) * frameHeight);

            for (int column = 0; column < columns && index < maxFrameCount; column++)
            {
                Rect rect = new Rect(
                    column * frameWidth,
                    y,
                    frameWidth,
                    frameHeight);
                Sprite sprite = Sprite.Create(
                    spriteSheet,
                    rect,
                    new Vector2(0.5f, 0.5f),
                    settings.pixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);

                frames[index++] = sprite;
                generatedSprites.Add(sprite);
                frameOffsets[index - 1] = baseOffset + CalculateFrameOffset(
                    pixels,
                    textureWidth,
                    column * frameWidth,
                    y,
                    frameWidth,
                    frameHeight,
                    settings.pixelsPerUnit,
                    stabilizeX,
                    stabilizeY);
            }
        }

        return frames;
    }

    private static Vector3[] BuildZeroOffsets(Sprite[] frames)
    {
        return frames == null ? null : new Vector3[frames.Length];
    }

    private static Vector3 CalculateFrameOffset(
        Color32[] pixels,
        int textureWidth,
        int xMin,
        int yMin,
        int frameWidth,
        int frameHeight,
        float pixelsPerUnit,
        bool stabilizeX,
        bool stabilizeY)
    {
        int minX = frameWidth;
        int minY = frameHeight;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < frameHeight; y++)
        {
            int rowIndex = (yMin + y) * textureWidth;

            for (int x = 0; x < frameWidth; x++)
            {
                Color32 pixel = pixels[rowIndex + xMin + x];
                if (pixel.a <= 10)
                {
                    continue;
                }

                if (x < minX) { minX = x; }
                if (y < minY) { minY = y; }
                if (x > maxX) { maxX = x; }
                if (y > maxY) { maxY = y; }
            }
        }

        if (maxX < 0 || maxY < 0 || pixelsPerUnit <= 0f)
        {
            return Vector3.zero;
        }

        float contentCenterX = (minX + maxX) * 0.5f;
        float contentCenterY = (minY + maxY) * 0.5f;
        float frameCenterX = (frameWidth - 1) * 0.5f;
        float frameCenterY = (frameHeight - 1) * 0.5f;

        return new Vector3(
            stabilizeX ? (frameCenterX - contentCenterX) / pixelsPerUnit : 0f,
            stabilizeY ? (frameCenterY - contentCenterY) / pixelsPerUnit : 0f,
            0f);
    }

    private void ApplyFrameOffset(
        Vector3[] frameOffsets,
        int index,
        Vector3? anchoredBasePosition = null,
        Vector3? anchoredReferenceOffset = null)
    {
        if (effectTransform == null)
        {
            return;
        }

        Vector3 baseOffset = settings != null ? settings.visualOffset : Vector3.zero;
        if (frameOffsets == null || index < 0 || index >= frameOffsets.Length)
        {
            effectTransform.localPosition = anchoredBasePosition ?? baseOffset;
            return;
        }

        if (anchoredBasePosition.HasValue && anchoredReferenceOffset.HasValue)
        {
            effectTransform.localPosition = anchoredBasePosition.Value + (frameOffsets[index] - anchoredReferenceOffset.Value);
            return;
        }

        effectTransform.localPosition = frameOffsets[index];
    }
    private void DisablePickupColliders()
    {
        Collider2D[] colliders = GetComponents<Collider2D>();

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private void PlayPickupSound()
    {
        if (pickupSound == null)
        {
            return;
        }

        GameObject audioObject = new GameObject($"{name}_PickupSfx");
        audioObject.transform.position = transform.position;

        AudioSource audioSource = audioObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
        audioSource.clip = pickupSound;
        audioSource.Play();

        Destroy(audioObject, pickupSound.length + 0.1f);
    }

    private void CacheReferences()
    {
        if (effectTransform == null && transform.childCount > 0)
        {
            effectTransform = transform.GetChild(0);
        }

        if (effectRenderer == null && effectTransform != null)
        {
            effectRenderer = effectTransform.GetComponent<SpriteRenderer>();
        }
    }

    private static float SafeDivide(float value, float divisor)
    {
        if (Mathf.Approximately(divisor, 0f))
        {
            return value;
        }

        return value / divisor;
    }

    private void OnDestroy()
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
        }

        for (int i = 0; i < generatedSprites.Count; i++)
        {
            if (generatedSprites[i] != null)
            {
                Destroy(generatedSprites[i]);
            }
        }
    }
}
