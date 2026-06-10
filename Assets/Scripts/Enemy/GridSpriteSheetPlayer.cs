using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameName.Enemy
{
    public struct GridSpriteSheetClip
    {
        public Texture2D SpriteSheet;
        public int Columns;
        public int Rows;
        public int FrameCount;
        public float FramesPerSecond;
        public float PixelsPerUnit;
        public Vector2 Pivot;
        public int CenteredCropInsetPixels;
        public bool UseFrameCrop;
        public RectInt FrameCropPixels;
        public Vector2Int FrameCropReferencePixels;

        public bool IsValid =>
            SpriteSheet != null &&
            Columns > 0 &&
            Rows > 0 &&
            FrameCount > 0 &&
            FramesPerSecond > 0f &&
            PixelsPerUnit > 0f;

        public float FrameSeconds => 1f / Mathf.Max(0.01f, FramesPerSecond);
        public float DurationSeconds => Mathf.Max(0, FrameCount) * FrameSeconds;
    }

    public static class GridSpriteSheetUtility
    {
        public static Sprite[] BuildFrames(GridSpriteSheetClip clip, ICollection<Sprite> generatedSprites = null)
        {
            if (!clip.IsValid)
            {
                return Array.Empty<Sprite>();
            }

            int frameWidth = clip.SpriteSheet.width / clip.Columns;
            int frameHeight = clip.SpriteSheet.height / clip.Rows;
            if (frameWidth <= 0 || frameHeight <= 0)
            {
                return Array.Empty<Sprite>();
            }

            RectInt crop = ResolveFrameCrop(clip, frameWidth, frameHeight);
            int maxFrameCount = Mathf.Min(clip.FrameCount, clip.Columns * clip.Rows);
            Sprite[] frames = new Sprite[maxFrameCount];
            int index = 0;

            for (int row = 0; row < clip.Rows && index < maxFrameCount; row++)
            {
                int y = clip.SpriteSheet.height - ((row + 1) * frameHeight);

                for (int column = 0; column < clip.Columns && index < maxFrameCount; column++)
                {
                    Rect rect = new Rect(
                        column * frameWidth + crop.x,
                        y + crop.y,
                        crop.width,
                        crop.height);
                    Sprite sprite = Sprite.Create(
                        clip.SpriteSheet,
                        rect,
                        clip.Pivot,
                        clip.PixelsPerUnit,
                        0,
                        SpriteMeshType.FullRect);

                    frames[index++] = sprite;
                    generatedSprites?.Add(sprite);
                }
            }

            return frames;
        }

        private static RectInt ResolveFrameCrop(GridSpriteSheetClip clip, int frameWidth, int frameHeight)
        {
            if (clip.UseFrameCrop)
            {
                RectInt frameCrop = ScaleFrameCrop(clip.FrameCropPixels, clip.FrameCropReferencePixels, frameWidth, frameHeight);
                int cropX = Mathf.Clamp(frameCrop.x, 0, Mathf.Max(0, frameWidth - 1));
                int cropY = Mathf.Clamp(frameCrop.y, 0, Mathf.Max(0, frameHeight - 1));
                int cropWidth = Mathf.Clamp(frameCrop.width, 1, frameWidth - cropX);
                int cropHeight = Mathf.Clamp(frameCrop.height, 1, frameHeight - cropY);
                return new RectInt(cropX, cropY, cropWidth, cropHeight);
            }

            int maxInset = Mathf.Max(0, (Mathf.Min(frameWidth, frameHeight) - 1) / 2);
            int inset = Mathf.Clamp(clip.CenteredCropInsetPixels, 0, maxInset);
            return new RectInt(
                inset,
                inset,
                frameWidth - (inset * 2),
                frameHeight - (inset * 2));
        }

        private static RectInt ScaleFrameCrop(
            RectInt crop,
            Vector2Int referenceSize,
            int frameWidth,
            int frameHeight)
        {
            if (referenceSize.x <= 0 || referenceSize.y <= 0)
            {
                return crop;
            }

            float scaleX = frameWidth / (float)referenceSize.x;
            float scaleY = frameHeight / (float)referenceSize.y;
            return new RectInt(
                Mathf.RoundToInt(crop.x * scaleX),
                Mathf.RoundToInt(crop.y * scaleY),
                Mathf.Max(1, Mathf.RoundToInt(crop.width * scaleX)),
                Mathf.Max(1, Mathf.RoundToInt(crop.height * scaleY)));
        }

        public static void DestroyGeneratedSprites(IList<Sprite> generatedSprites)
        {
            if (generatedSprites == null)
            {
                return;
            }

            for (int i = 0; i < generatedSprites.Count; i++)
            {
                Sprite sprite = generatedSprites[i];
                if (sprite == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(sprite);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(sprite);
                }
            }

            generatedSprites.Clear();
        }
    }

    [DisallowMultipleComponent]
    public sealed class GridSpriteSheetPlayer : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;

        private readonly List<Sprite> generatedSprites = new();
        private Coroutine playRoutine;
        private Coroutine fadeRoutine;
        private Sprite[] frames = Array.Empty<Sprite>();
        private bool useTargetWorldSize;
        private Vector2 targetWorldSize;
#if UNITY_EDITOR
        private bool editorPlaybackActive;
        private bool editorLoop;
        private bool editorHoldLast;
        private bool editorHideOnComplete;
        private float editorFrameSeconds;
        private double editorLastUpdateTime;
        private double editorFrameTimer;
        private int editorNextFrameIndex;
        private Action<int> editorFrameChanged;
        private Action editorCompleted;
#endif

        public SpriteRenderer Renderer => targetRenderer;
        public int CurrentFrameIndex { get; private set; } = -1;
        public bool IsPlaying =>
            playRoutine != null
#if UNITY_EDITOR
            || editorPlaybackActive
#endif
            ;

        private void Awake()
        {
            CacheRenderer();
        }

        public void ConfigureRenderer(SpriteRenderer renderer)
        {
            targetRenderer = renderer;
            CacheRenderer();
        }

        public void SetTargetWorldSize(Vector2 worldSize)
        {
            targetWorldSize = new Vector2(Mathf.Abs(worldSize.x), Mathf.Abs(worldSize.y));
            useTargetWorldSize = targetWorldSize.x > 0.001f && targetWorldSize.y > 0.001f;
        }

        public void ClearTargetWorldSize()
        {
            useTargetWorldSize = false;
        }

        public bool Play(
            GridSpriteSheetClip clip,
            bool loop,
            bool holdLast,
            bool hideOnComplete,
            Action<int> frameChanged = null,
            Action completed = null)
        {
            CacheRenderer();
            StopPlayback(clearSprite: true, hideRenderer: false);
            ClearGeneratedSprites();

            frames = GridSpriteSheetUtility.BuildFrames(clip, generatedSprites);
            if (targetRenderer == null || frames.Length == 0)
            {
                completed?.Invoke();
                return false;
            }

            targetRenderer.enabled = true;
            if (!Application.isPlaying)
            {
#if UNITY_EDITOR
                StartEditorPlayback(clip.FrameSeconds, loop, holdLast, hideOnComplete, frameChanged, completed);
                return true;
#endif
            }

            playRoutine = StartCoroutine(PlayRoutine(clip.FrameSeconds, loop, holdLast, hideOnComplete, frameChanged, completed));
            return true;
        }

        public void StopPlayback(bool clearSprite = true, bool hideRenderer = true)
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

#if UNITY_EDITOR
            StopEditorPlayback();
#endif

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            CurrentFrameIndex = -1;

            if (targetRenderer == null)
            {
                return;
            }

            if (clearSprite)
            {
                targetRenderer.sprite = null;
            }

            if (hideRenderer)
            {
                targetRenderer.enabled = false;
            }
        }

        public void SetAlpha(float alpha)
        {
            CacheRenderer();
            if (targetRenderer == null)
            {
                return;
            }

            Color color = targetRenderer.color;
            color.a = Mathf.Clamp01(alpha);
            targetRenderer.color = color;
        }

        public void FadeToAlpha(float targetAlpha, float duration, bool destroyOnComplete = false, Action completed = null)
        {
            CacheRenderer();
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            if (targetRenderer == null)
            {
                completed?.Invoke();
                if (destroyOnComplete)
                {
                    DestroyPlayerObject();
                }

                return;
            }

            fadeRoutine = StartCoroutine(FadeRoutine(Mathf.Clamp01(targetAlpha), Mathf.Max(0f, duration), destroyOnComplete, completed));
        }

        public void ApplyRendererSettings(SpriteRenderer sourceRenderer, int sortingOrderOffset, bool copyMaterial)
        {
            CacheRenderer();
            if (targetRenderer == null || sourceRenderer == null)
            {
                return;
            }

            targetRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            targetRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;

            if (copyMaterial && sourceRenderer.sharedMaterial != null)
            {
                targetRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            }
        }

        private IEnumerator PlayRoutine(
            float frameSeconds,
            bool loop,
            bool holdLast,
            bool hideOnComplete,
            Action<int> frameChanged,
            Action completed)
        {
            do
            {
                for (int i = 0; i < frames.Length; i++)
                {
                    ApplyFrame(i);
                    frameChanged?.Invoke(i);
                    yield return new WaitForSeconds(frameSeconds);
                }
            }
            while (loop && targetRenderer != null);

            playRoutine = null;

            if (targetRenderer != null && !holdLast)
            {
                if (hideOnComplete)
                {
                    targetRenderer.enabled = false;
                }

                targetRenderer.sprite = null;
            }

            completed?.Invoke();
        }

#if UNITY_EDITOR
        private void StartEditorPlayback(
            float frameSeconds,
            bool loop,
            bool holdLast,
            bool hideOnComplete,
            Action<int> frameChanged,
            Action completed)
        {
            StopEditorPlayback();
            editorLoop = loop;
            editorHoldLast = holdLast;
            editorHideOnComplete = hideOnComplete;
            editorFrameSeconds = Mathf.Max(0.0001f, frameSeconds);
            editorLastUpdateTime = EditorApplication.timeSinceStartup;
            editorFrameTimer = 0d;
            editorFrameChanged = frameChanged;
            editorCompleted = completed;
            editorPlaybackActive = true;

            ApplyFrame(0);
            editorFrameChanged?.Invoke(0);
            editorNextFrameIndex = 1;
            EditorApplication.update += EditorPlaybackUpdate;
        }

        private void StopEditorPlayback()
        {
            if (!editorPlaybackActive)
            {
                return;
            }

            EditorApplication.update -= EditorPlaybackUpdate;
            editorPlaybackActive = false;
            editorFrameChanged = null;
            editorCompleted = null;
            editorNextFrameIndex = 0;
            editorFrameTimer = 0d;
        }

        private void EditorPlaybackUpdate()
        {
            if (!editorPlaybackActive || frames == null || frames.Length == 0)
            {
                CompleteEditorPlayback();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            editorFrameTimer += Math.Max(0d, now - editorLastUpdateTime);
            editorLastUpdateTime = now;

            while (editorPlaybackActive && editorFrameTimer >= editorFrameSeconds)
            {
                editorFrameTimer -= editorFrameSeconds;
                if (editorNextFrameIndex >= frames.Length)
                {
                    if (editorLoop)
                    {
                        editorNextFrameIndex = 0;
                    }
                    else
                    {
                        CompleteEditorPlayback();
                        return;
                    }
                }

                ApplyFrame(editorNextFrameIndex);
                editorFrameChanged?.Invoke(editorNextFrameIndex);
                editorNextFrameIndex++;
            }
        }

        private void CompleteEditorPlayback()
        {
            if (!editorPlaybackActive)
            {
                return;
            }

            EditorApplication.update -= EditorPlaybackUpdate;
            editorPlaybackActive = false;

            if (targetRenderer != null && !editorHoldLast)
            {
                if (editorHideOnComplete)
                {
                    targetRenderer.enabled = false;
                }

                targetRenderer.sprite = null;
            }

            Action completed = editorCompleted;
            editorFrameChanged = null;
            editorCompleted = null;
            completed?.Invoke();
        }
#endif

        private IEnumerator FadeRoutine(float targetAlpha, float duration, bool destroyOnComplete, Action completed)
        {
            float startAlpha = targetRenderer != null ? targetRenderer.color.a : 0f;

            if (duration <= 0f)
            {
                SetAlpha(targetAlpha);
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
                    yield return null;
                }

                SetAlpha(targetAlpha);
            }

            fadeRoutine = null;
            completed?.Invoke();

            if (destroyOnComplete)
            {
                DestroyPlayerObject();
            }
        }

        private void ApplyFrame(int frameIndex)
        {
            if (targetRenderer == null || frameIndex < 0 || frameIndex >= frames.Length)
            {
                return;
            }

            Sprite frame = frames[frameIndex];
            if (frame == null)
            {
                return;
            }

            targetRenderer.sprite = frame;
            targetRenderer.enabled = true;
            CurrentFrameIndex = frameIndex;

            if (!useTargetWorldSize)
            {
                return;
            }

            Vector2 spriteSize = frame.bounds.size;
            if (spriteSize.x <= 0.001f || spriteSize.y <= 0.001f)
            {
                return;
            }

            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float parentScaleX = Mathf.Max(0.001f, Mathf.Abs(parentScale.x));
            float parentScaleY = Mathf.Max(0.001f, Mathf.Abs(parentScale.y));

            transform.localScale = new Vector3(
                targetWorldSize.x / (spriteSize.x * parentScaleX),
                targetWorldSize.y / (spriteSize.y * parentScaleY),
                transform.localScale.z == 0f ? 1f : transform.localScale.z);
        }

        private void CacheRenderer()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<SpriteRenderer>();
            }

            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        private void ClearGeneratedSprites()
        {
            GridSpriteSheetUtility.DestroyGeneratedSprites(generatedSprites);
            frames = Array.Empty<Sprite>();
        }

        private void OnDestroy()
        {
            StopPlayback(clearSprite: false, hideRenderer: false);
            ClearGeneratedSprites();
        }

        private void DestroyPlayerObject()
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}
