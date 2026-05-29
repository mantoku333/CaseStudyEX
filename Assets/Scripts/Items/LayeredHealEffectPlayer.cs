using System.Collections;
using System.Collections.Generic;
using Player;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LayeredHealEffectPlayer : MonoBehaviour
{
    private readonly List<Sprite> generatedSprites = new();

    private SpriteRenderer backRenderer;
    private SpriteRenderer frontRenderer;
    private Sprite[] backFrames;
    private Sprite[] frontFrames;
    private float frameSeconds;

    private readonly struct RendererSortSettings
    {
        public RendererSortSettings(int sortingLayerId, int sortingOrder, Material sharedMaterial)
        {
            SortingLayerId = sortingLayerId;
            SortingOrder = sortingOrder;
            SharedMaterial = sharedMaterial;
        }

        public int SortingLayerId { get; }
        public int SortingOrder { get; }
        public Material SharedMaterial { get; }
    }

    public static bool Play(PlayerHealth targetHealth, ItemEffectSettings settings)
    {
        if (targetHealth == null || settings == null || !settings.playHealEffectOnPlayer)
        {
            return false;
        }

        if (settings.healBackSpriteSheet == null && settings.healFrontSpriteSheet == null)
        {
            return false;
        }

        GameObject effectObject = new GameObject("HealPickupLayeredEffect");
        effectObject.transform.position = ResolveSpawnPosition(targetHealth, settings.healVisualOffset);
        effectObject.transform.SetParent(targetHealth.transform, true);
        effectObject.transform.localRotation = Quaternion.identity;
        effectObject.transform.localScale = settings.healVisualScale;

        LayeredHealEffectPlayer player = effectObject.AddComponent<LayeredHealEffectPlayer>();
        player.Initialize(targetHealth, settings);
        return true;
    }

    private static Vector3 ResolveSpawnPosition(PlayerHealth targetHealth, Vector3 offset)
    {
        Collider2D collider2d = targetHealth.GetComponent<Collider2D>();
        if (collider2d == null)
        {
            collider2d = targetHealth.GetComponentInChildren<Collider2D>();
        }

        Vector3 center = collider2d != null ? collider2d.bounds.center : targetHealth.transform.position;
        return center + offset;
    }

    private void Initialize(PlayerHealth targetHealth, ItemEffectSettings settings)
    {
        frameSeconds = Mathf.Max(0.01f, settings.healFrameSeconds);

        ResolvePlayerSorting(
            targetHealth,
            out RendererSortSettings backSorting,
            out RendererSortSettings frontSorting);

        backRenderer = CreateRenderer("HealBack", backSorting, settings.healBackSortingOrderOffset);
        frontRenderer = CreateRenderer("HealFront", frontSorting, settings.healFrontSortingOrderOffset);

        backFrames = BuildFrames(settings.healBackSpriteSheet, settings);
        frontFrames = BuildFrames(settings.healFrontSpriteSheet, settings);

        StartCoroutine(PlayRoutine());
    }

    private static void ResolvePlayerSorting(
        PlayerHealth targetHealth,
        out RendererSortSettings backSorting,
        out RendererSortSettings frontSorting)
    {
        SpriteRenderer[] renderers = targetHealth.GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer frontSource = null;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (frontSource == null || CompareRendererSort(renderer, frontSource) > 0)
            {
                frontSource = renderer;
            }
        }

        backSorting = CreateSortSettings(frontSource, 0);
        frontSorting = CreateSortSettings(frontSource, 0);
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

    private static RendererSortSettings CreateSortSettings(SpriteRenderer sourceRenderer, int fallbackSortingOrder)
    {
        if (sourceRenderer == null)
        {
            return new RendererSortSettings(0, fallbackSortingOrder, null);
        }

        return new RendererSortSettings(
            sourceRenderer.sortingLayerID,
            sourceRenderer.sortingOrder,
            sourceRenderer.sharedMaterial);
    }

    private SpriteRenderer CreateRenderer(
        string objectName,
        RendererSortSettings sortSettings,
        int sortingOrderOffset)
    {
        GameObject rendererObject = new GameObject(objectName);
        rendererObject.transform.SetParent(transform, false);

        SpriteRenderer renderer = rendererObject.AddComponent<SpriteRenderer>();
        renderer.sortingLayerID = sortSettings.SortingLayerId;
        renderer.sortingOrder = sortSettings.SortingOrder + sortingOrderOffset;

        if (sortSettings.SharedMaterial != null)
        {
            renderer.sharedMaterial = sortSettings.SharedMaterial;
        }

        renderer.enabled = false;
        return renderer;
    }

    private Sprite[] BuildFrames(Texture2D spriteSheet, ItemEffectSettings settings)
    {
        if (spriteSheet == null)
        {
            return System.Array.Empty<Sprite>();
        }

        int columns = Mathf.Max(1, settings.healColumns);
        int rows = Mathf.Max(1, settings.healRows);
        int frameWidth = spriteSheet.width / columns;
        int frameHeight = spriteSheet.height / rows;
        if (frameWidth <= 0 || frameHeight <= 0)
        {
            return System.Array.Empty<Sprite>();
        }

        int maxFrameCount = Mathf.Min(Mathf.Max(1, settings.healFrameCount), columns * rows);
        Sprite[] frames = new Sprite[maxFrameCount];
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        float pixelsPerUnit = Mathf.Max(1f, settings.healPixelsPerUnit);
        int index = 0;

        for (int row = 0; row < rows && index < maxFrameCount; row++)
        {
            int y = spriteSheet.height - ((row + 1) * frameHeight);

            for (int column = 0; column < columns && index < maxFrameCount; column++)
            {
                Rect rect = new Rect(column * frameWidth, y, frameWidth, frameHeight);
                Sprite sprite = Sprite.Create(
                    spriteSheet,
                    rect,
                    pivot,
                    pixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);

                frames[index++] = sprite;
                generatedSprites.Add(sprite);
            }
        }

        return frames;
    }

    private IEnumerator PlayRoutine()
    {
        int frameCount = Mathf.Max(backFrames.Length, frontFrames.Length);
        if (frameCount <= 0)
        {
            Destroy(gameObject);
            yield break;
        }

        SetRendererEnabled(backRenderer, backFrames.Length > 0);
        SetRendererEnabled(frontRenderer, frontFrames.Length > 0);

        for (int i = 0; i < frameCount; i++)
        {
            ApplyFrame(backRenderer, backFrames, i);
            ApplyFrame(frontRenderer, frontFrames, i);
            yield return new WaitForSeconds(frameSeconds);
        }

        Destroy(gameObject);
    }

    private static void ApplyFrame(SpriteRenderer renderer, Sprite[] frames, int index)
    {
        if (renderer == null || frames == null || index < 0 || index >= frames.Length)
        {
            return;
        }

        renderer.sprite = frames[index];
    }

    private static void SetRendererEnabled(SpriteRenderer renderer, bool enabled)
    {
        if (renderer != null)
        {
            renderer.enabled = enabled;
        }
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
