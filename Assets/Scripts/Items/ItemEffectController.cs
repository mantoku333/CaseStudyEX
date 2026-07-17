using System.Collections;
using System.Collections.Generic;
using Player;
using UnityEngine;

[DisallowMultipleComponent]
public class ItemEffectController : MonoBehaviour
{
    [SerializeField] private ItemEffectSettings settings;
    [SerializeField] private Transform effectTransform;
    [SerializeField] private SpriteRenderer effectRenderer;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField, Min(0f)] private float pickupVisualScaleMultiplier = 1f;

    private Coroutine playbackRoutine;
    private bool isPickupPlaying;
    private Sprite[] loopFrames;
    private Vector3[] loopFrameOffsets;
    private Sprite[] pickupFrames;
    private Vector3[] pickupFrameOffsets;
    private Vector3 pickupStartPosition;
    private bool isPickupEffectDetached;
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

    public bool PlayPickupEffectAndDestroy(Transform pickupTarget = null)
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

        if (settings != null && settings.playPickupEffectAtPlayer && pickupTarget != null)
        {
            PreparePickupEffectAtPlayer(pickupTarget);
        }
        else
        {
            PreparePickupEffectAtItem();
        }

        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
        }

        playbackRoutine = StartCoroutine(PlayPickupAndDestroyRoutine());
        return true;
    }

    public bool PlayHealEffectOnPlayer(PlayerHealth playerHealth)
    {
        return LayeredHealEffectPlayer.Play(playerHealth, settings);
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

        if (isPickupEffectDetached && effectTransform != null)
        {
            Destroy(effectTransform.gameObject);
        }

        Destroy(gameObject);
    }

    private void PreparePickupEffectAtItem()
    {
        isPickupEffectDetached = false;
        ApplyPickupVisualScale(compensateRootScale: true);
        pickupStartPosition = effectTransform != null
            ? effectTransform.localPosition
            : (settings != null ? settings.visualOffset : Vector3.zero);

        if (settings != null)
        {
            pickupStartPosition += settings.pickupVisualOffset;
        }
    }

    private void PreparePickupEffectAtPlayer(Transform pickupTarget)
    {
        isPickupEffectDetached = false;
        if (effectTransform == null || settings == null || pickupTarget == null)
        {
            PreparePickupEffectAtItem();
            return;
        }

        effectTransform.SetParent(pickupTarget, false);
        effectTransform.localRotation = Quaternion.identity;
        ApplyPickupVisualScale(compensateRootScale: false);
        pickupStartPosition = settings.playerPickupVisualOffset + settings.pickupVisualOffset;
        effectTransform.localPosition = pickupStartPosition;
        isPickupEffectDetached = true;
    }

    private void ApplyPickupVisualScale(bool compensateRootScale)
    {
        if (effectTransform == null)
        {
            return;
        }

        Vector3 targetScale = settings != null ? settings.pickupVisualScale : Vector3.one;
        targetScale *= pickupVisualScaleMultiplier;
        if (!compensateRootScale)
        {
            effectTransform.localScale = targetScale;
            return;
        }

        Vector3 rootScale = transform.localScale;
        effectTransform.localScale = new Vector3(
            SafeDivide(targetScale.x, rootScale.x),
            SafeDivide(targetScale.y, rootScale.y),
            SafeDivide(targetScale.z, rootScale.z));
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
                out pickupFrameOffsets);
        }
    }

    private Sprite[] BuildFrames(
        Texture2D spriteSheet,
        int columns,
        int rows,
        int frameCount,
        Sprite[] fallbackFrames,
        out Vector3[] frameOffsets)
    {
        Vector3 baseOffset = settings != null ? settings.visualOffset : Vector3.zero;
        if (spriteSheet == null || columns <= 0 || rows <= 0 || frameCount <= 0)
        {
            frameOffsets = BuildLockedOffsets(fallbackFrames, baseOffset);
            return fallbackFrames;
        }

        int frameWidth = spriteSheet.width / columns;
        int frameHeight = spriteSheet.height / rows;

        if (frameWidth <= 0 || frameHeight <= 0)
        {
            frameOffsets = BuildLockedOffsets(fallbackFrames, baseOffset);
            return fallbackFrames;
        }

        int maxFrameCount = Mathf.Min(frameCount, columns * rows);
        Sprite[] frames = new Sprite[maxFrameCount];
        frameOffsets = new Vector3[maxFrameCount];
        int index = 0;
        float sizeCompensatedPixelsPerUnit =
            SpriteSheetResolutionUtility.GetSizeCompensatedPixelsPerUnit(
                spriteSheet,
                settings.pixelsPerUnit);

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
                    sizeCompensatedPixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);

                frames[index++] = sprite;
                generatedSprites.Add(sprite);
                // The sprite sheet already preserves the authored position inside each
                // fixed grid cell. Keep one anchor for every frame, like LastBoss effects,
                // instead of following the changing center of the visible pixels.
                frameOffsets[index - 1] = baseOffset;
            }
        }

        return frames;
    }

    private static Vector3[] BuildLockedOffsets(Sprite[] frames, Vector3 lockedOffset)
    {
        if (frames == null)
        {
            return null;
        }

        Vector3[] offsets = new Vector3[frames.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            offsets[i] = lockedOffset;
        }

        return offsets;
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
        // Frame offsets used to follow each frame's visible-pixel center. That moves the
        // renderer even though every generated sprite uses the same fixed grid. Lock the
        // transform instead, matching LastBoss effect playback.
        effectTransform.localPosition = anchoredBasePosition ?? baseOffset;
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
