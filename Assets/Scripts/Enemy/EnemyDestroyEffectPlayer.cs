using System.Collections;
using UnityEngine;

namespace GameName.Enemy
{
    [DisallowMultipleComponent]
    public sealed class EnemyDestroyEffectPlayer : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Texture2D spriteSheetTexture;
        [SerializeField, Min(1)] private int frameColumns = 5;
        [SerializeField, Min(1)] private int frameRows = 5;
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(0.01f)] private float frameSeconds = 0.04f;
        [SerializeField, Min(1f)] private float pixelsPerUnit = 100f;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool destroyOnComplete = true;
        [SerializeField] private bool centerFramesOnOrigin = true;

        private Coroutine playRoutine;
        private Sprite[] generatedFrames;

        private void Awake()
        {
            CacheRenderer();
        }

        private void OnEnable()
        {
            if (playOnEnable)
            {
                Play();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            frameColumns = Mathf.Max(1, frameColumns);
            frameRows = Mathf.Max(1, frameRows);
            frameSeconds = Mathf.Max(0.01f, frameSeconds);
            pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
            CacheRenderer();
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

        private IEnumerator PlayRoutine()
        {
            CacheRenderer();
            Sprite[] playbackFrames = GetPlaybackFrames();

            if (targetRenderer == null || playbackFrames == null || playbackFrames.Length == 0)
            {
                DestroyIfNeeded();
                yield break;
            }

            targetRenderer.enabled = true;

            for (int i = 0; i < playbackFrames.Length; i++)
            {
                Sprite frame = playbackFrames[i];
                if (frame == null)
                {
                    continue;
                }

                targetRenderer.sprite = frame;
                if (centerFramesOnOrigin)
                {
                    targetRenderer.transform.localPosition = -frame.bounds.center;
                }

                yield return new WaitForSeconds(frameSeconds);
            }

            targetRenderer.enabled = false;
            targetRenderer.sprite = null;
            playRoutine = null;
            DestroyIfNeeded();
        }

        private void CacheRenderer()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        private Sprite[] GetPlaybackFrames()
        {
            BuildGeneratedFramesIfNeeded();

            if (generatedFrames != null && generatedFrames.Length > 0)
            {
                return generatedFrames;
            }

            return frames;
        }

        private void BuildGeneratedFramesIfNeeded()
        {
            if (generatedFrames != null && generatedFrames.Length > 0)
            {
                return;
            }

            if (spriteSheetTexture == null || frameColumns <= 0 || frameRows <= 0)
            {
                return;
            }

            int frameWidth = spriteSheetTexture.width / frameColumns;
            int frameHeight = spriteSheetTexture.height / frameRows;
            if (frameWidth <= 0 || frameHeight <= 0)
            {
                return;
            }

            generatedFrames = new Sprite[frameColumns * frameRows];
            int frameIndex = 0;
            Vector2 pivot = new Vector2(0.5f, 0.5f);

            for (int visualRow = 0; visualRow < frameRows; visualRow++)
            {
                int y = spriteSheetTexture.height - ((visualRow + 1) * frameHeight);

                for (int xIndex = 0; xIndex < frameColumns; xIndex++)
                {
                    int x = xIndex * frameWidth;
                    Rect rect = new Rect(x, y, frameWidth, frameHeight);
                    generatedFrames[frameIndex++] = Sprite.Create(
                        spriteSheetTexture,
                        rect,
                        pivot,
                        pixelsPerUnit,
                        0,
                        SpriteMeshType.FullRect);
                }
            }
        }

        private void DestroyIfNeeded()
        {
            if (destroyOnComplete)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
            }

            if (generatedFrames == null)
            {
                return;
            }

            for (int i = 0; i < generatedFrames.Length; i++)
            {
                if (generatedFrames[i] != null)
                {
                    Destroy(generatedFrames[i]);
                }
            }
        }
    }
}
