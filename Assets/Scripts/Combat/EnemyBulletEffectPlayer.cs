using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Metroidvania.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyBullet))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class EnemyBulletEffectPlayer : MonoBehaviour
    {
        [Header("Loop Sprite Sheet")]
        [SerializeField] private Texture2D loopSpriteSheet;
        [SerializeField, Min(1)] private int loopColumns = 5;
        [SerializeField, Min(1)] private int loopRows = 6;
        [SerializeField, Min(1)] private int loopFrameCount = 30;
        [SerializeField, Min(0.01f)] private float loopFrameSeconds = 0.033f;

        [Header("Out Sprite Sheet")]
        [SerializeField] private Texture2D outSpriteSheet;
        [SerializeField, Min(1)] private int outColumns = 5;
        [SerializeField, Min(1)] private int outRows = 3;
        [SerializeField, Min(1)] private int outFrameCount = 15;
        [SerializeField, Min(0.01f)] private float outFrameSeconds = 0.033f;

        [Header("Impact Sprite Sheet")]
        [SerializeField] private Texture2D impactSpriteSheet;
        [SerializeField, Min(1)] private int impactColumns = 5;
        [SerializeField, Min(1)] private int impactRows = 3;
        [SerializeField, Min(1)] private int impactFrameCount = 15;
        [SerializeField, Min(0.01f)] private float impactFrameSeconds = 0.033f;

        [Header("Renderer")]
        [SerializeField, Min(1f)] private float pixelsPerUnit = 100f;
        [SerializeField] private Vector3 loopScale = Vector3.one;
        [SerializeField] private Vector3 outScale = Vector3.one;
        [SerializeField] private Vector3 impactScale = Vector3.one;
        [SerializeField] private Vector3 oneShotOffset = Vector3.zero;
        [SerializeField] private int sortingOrder = 10;
        [SerializeField] private bool forceWhiteColor = true;

        private readonly List<Sprite> generatedSprites = new();
        private SpriteRenderer spriteRenderer;
        private Sprite[] loopFrames;
        private Sprite[] outFrames;
        private Sprite[] impactFrames;
        private Coroutine loopRoutine;
        private Coroutine oneShotRoutine;

        public bool IsFinishing { get; private set; }

        private void Awake()
        {
            CacheRenderer();
            BuildFramesIfNeeded();
            ApplyLoopRendererSettings();
        }

        private void OnEnable()
        {
            CacheRenderer();
            BuildFramesIfNeeded();
            ApplyLoopRendererSettings();
            StartLoop();
        }

        private void OnDisable()
        {
            StopLoop();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            loopColumns = Mathf.Max(1, loopColumns);
            loopRows = Mathf.Max(1, loopRows);
            loopFrameCount = Mathf.Max(1, loopFrameCount);
            loopFrameSeconds = Mathf.Max(0.01f, loopFrameSeconds);
            outColumns = Mathf.Max(1, outColumns);
            outRows = Mathf.Max(1, outRows);
            outFrameCount = Mathf.Max(1, outFrameCount);
            outFrameSeconds = Mathf.Max(0.01f, outFrameSeconds);
            impactColumns = Mathf.Max(1, impactColumns);
            impactRows = Mathf.Max(1, impactRows);
            impactFrameCount = Mathf.Max(1, impactFrameCount);
            impactFrameSeconds = Mathf.Max(0.01f, impactFrameSeconds);
            pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
        }
#endif

        public bool PlayOutThenDestroy()
        {
            BuildFramesIfNeeded();
            return PlayOneShotThenDestroy(outFrames, outFrameSeconds, outScale, transform.position + oneShotOffset);
        }

        public bool PlayImpactThenDestroy(Collider2D hitCollider)
        {
            BuildFramesIfNeeded();
            return PlayOneShotThenDestroy(
                impactFrames,
                impactFrameSeconds,
                impactScale,
                ResolveImpactPosition(hitCollider) + oneShotOffset);
        }

        private void CacheRenderer()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
        }

        private void ApplyLoopRendererSettings()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.sortingOrder = sortingOrder;
            transform.localScale = loopScale;

            if (forceWhiteColor)
            {
                spriteRenderer.color = Color.white;
            }
        }

        private void StartLoop()
        {
            StopLoop();
            if (loopFrames == null || loopFrames.Length == 0 || spriteRenderer == null)
            {
                return;
            }

            loopRoutine = StartCoroutine(LoopRoutine());
        }

        private void StopLoop()
        {
            if (loopRoutine != null)
            {
                StopCoroutine(loopRoutine);
                loopRoutine = null;
            }
        }

        private IEnumerator LoopRoutine()
        {
            int index = 0;
            while (true)
            {
                if (spriteRenderer == null)
                {
                    yield break;
                }

                spriteRenderer.sprite = loopFrames[index];
                index = (index + 1) % loopFrames.Length;
                yield return new WaitForSeconds(loopFrameSeconds);
            }
        }

        private bool PlayOneShotThenDestroy(
            Sprite[] frames,
            float frameSeconds,
            Vector3 effectScale,
            Vector3 position)
        {
            if (frames == null || frames.Length == 0)
            {
                return false;
            }

            if (spriteRenderer == null)
            {
                return false;
            }

            StopLoop();
            if (oneShotRoutine != null)
            {
                StopCoroutine(oneShotRoutine);
            }

            IsFinishing = true;
            transform.position = position;
            transform.localScale = effectScale;
            spriteRenderer.enabled = true;
            spriteRenderer.sortingOrder = sortingOrder;

            oneShotRoutine = StartCoroutine(PlayOneShotRoutine(frames, frameSeconds));
            return true;
        }

        private Vector3 ResolveImpactPosition(Collider2D hitCollider)
        {
            if (hitCollider == null)
            {
                return transform.position;
            }

            Collider2D bulletCollider = GetComponent<Collider2D>();
            if (bulletCollider == null)
            {
                return hitCollider.bounds.center;
            }

            Vector2 bulletPoint = bulletCollider.ClosestPoint(hitCollider.bounds.center);
            Vector2 hitPoint = hitCollider.ClosestPoint(transform.position);
            return (bulletPoint + hitPoint) * 0.5f;
        }

        private IEnumerator PlayOneShotRoutine(Sprite[] frames, float frameSeconds)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                if (spriteRenderer == null)
                {
                    yield break;
                }

                spriteRenderer.sprite = frames[i];
                yield return new WaitForSeconds(frameSeconds);
            }

            Destroy(gameObject);
        }

        private void BuildFramesIfNeeded()
        {
            if (loopFrames == null || loopFrames.Length == 0)
            {
                loopFrames = BuildFrames(loopSpriteSheet, loopColumns, loopRows, loopFrameCount);
            }

            if (outFrames == null || outFrames.Length == 0)
            {
                outFrames = BuildFrames(outSpriteSheet, outColumns, outRows, outFrameCount);
            }

            if (impactFrames == null || impactFrames.Length == 0)
            {
                impactFrames = BuildFrames(impactSpriteSheet, impactColumns, impactRows, impactFrameCount);
            }
        }

        private Sprite[] BuildFrames(Texture2D spriteSheet, int columns, int rows, int frameCount)
        {
            if (spriteSheet == null)
            {
                return System.Array.Empty<Sprite>();
            }

            int frameWidth = spriteSheet.width / columns;
            int frameHeight = spriteSheet.height / rows;
            if (frameWidth <= 0 || frameHeight <= 0)
            {
                return System.Array.Empty<Sprite>();
            }

            int maxFrameCount = Mathf.Min(frameCount, columns * rows);
            Sprite[] builtFrames = new Sprite[maxFrameCount];
            Vector2 pivot = new Vector2(0.5f, 0.5f);
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

                    builtFrames[index++] = sprite;
                    generatedSprites.Add(sprite);
                }
            }

            return builtFrames;
        }

        private void OnDestroy()
        {
            if (loopRoutine != null)
            {
                StopCoroutine(loopRoutine);
            }

            if (oneShotRoutine != null)
            {
                StopCoroutine(oneShotRoutine);
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
}
