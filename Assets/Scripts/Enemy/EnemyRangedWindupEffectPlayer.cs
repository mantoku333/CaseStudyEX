using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameName.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyRangedAttack))]
    public sealed class EnemyRangedWindupEffectPlayer : MonoBehaviour
    {
        [Header("Charging Sprite Sheet")]
        [SerializeField] private Texture2D inSpriteSheet;
        [SerializeField, Min(1)] private int frameColumns = 5;
        [SerializeField, Min(1)] private int frameRows = 12;
        [SerializeField, Min(1)] private int frameCount = 60;
        [SerializeField, Min(1f)] private float pixelsPerUnit = 100f;

        [Header("Attack Timing Sprite Sheet")]
        [SerializeField] private Texture2D attackTimingSpriteSheet;
        [SerializeField, Min(1)] private int timingFrameColumns = 5;
        [SerializeField, Min(1)] private int timingFrameRows = 6;
        [SerializeField, Min(1)] private int timingFrameCount = 28;
        [SerializeField, Min(1f)] private float timingPixelsPerUnit = 100f;

        [Header("Charging Placement")]
        [SerializeField] private Vector3 localOffset = Vector3.zero;
        [SerializeField, Min(0f)] private float forwardOffset = 0.5f;
        [SerializeField] private Vector3 effectScale = Vector3.one;
        [SerializeField] private bool parentToFirePoint = true;

        [Header("Attack Timing Placement")]
        [SerializeField] private Vector3 timingLocalOffset = Vector3.zero;
        [SerializeField] private float timingForwardOffset;
        [SerializeField, Min(0.01f)] private float timingFitScaleMultiplier = 1f;

        [Header("Renderer")]
        [SerializeField] private int sortingOrderOffset = 3;
        [SerializeField] private bool copyEnemyMaterial = true;

        private readonly List<Sprite> generatedSprites = new();
        private EnemyRangedAttack rangedAttack;
        private EnemyController enemyController;
        private Sprite[] frames;
        private Sprite[] timingFrames;
        private GameObject effectObject;
        private SpriteRenderer effectRenderer;
        private GameObject timingEffectObject;
        private SpriteRenderer timingEffectRenderer;
        private Coroutine playRoutine;
        private float playbackElapsed;
        private float playbackDuration;

        private void Awake()
        {
            CacheComponents();
            BuildFramesIfNeeded();
        }

        private void OnEnable()
        {
            CacheComponents();
            if (rangedAttack != null)
            {
                rangedAttack.WindupStarted -= HandleWindupStarted;
                rangedAttack.WindupEnded -= HandleWindupEnded;
                rangedAttack.WindupStarted += HandleWindupStarted;
                rangedAttack.WindupEnded += HandleWindupEnded;
            }
        }

        private void OnDisable()
        {
            if (rangedAttack != null)
            {
                rangedAttack.WindupStarted -= HandleWindupStarted;
                rangedAttack.WindupEnded -= HandleWindupEnded;
            }

            StopAndDestroyEffects();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            frameColumns = Mathf.Max(1, frameColumns);
            frameRows = Mathf.Max(1, frameRows);
            frameCount = Mathf.Max(1, frameCount);
            pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
            timingFrameColumns = Mathf.Max(1, timingFrameColumns);
            timingFrameRows = Mathf.Max(1, timingFrameRows);
            timingFrameCount = Mathf.Max(1, timingFrameCount);
            timingPixelsPerUnit = Mathf.Max(1f, timingPixelsPerUnit);
            timingFitScaleMultiplier = Mathf.Max(0.01f, timingFitScaleMultiplier);
        }
#endif

        private void CacheComponents()
        {
            if (rangedAttack == null)
            {
                rangedAttack = GetComponent<EnemyRangedAttack>();
            }

            if (enemyController == null)
            {
                enemyController = GetComponent<EnemyController>();
            }
        }

        private void HandleWindupStarted(Transform firePoint, float duration)
        {
            BuildFramesIfNeeded();
            if (frames == null || frames.Length == 0)
            {
                return;
            }

            StopAndDestroyEffects();

            SpriteRenderer sourceRenderer = ResolveFrontmostEnemyRenderer();
            CreateChargingEffect(firePoint, sourceRenderer);
            CreateTimingEffect(sourceRenderer);

            playbackElapsed = 0f;
            playbackDuration = Mathf.Max(0.01f, duration);
            ApplyVisualFrames();
            playRoutine = StartCoroutine(PlayRoutine());
        }

        private void CreateChargingEffect(Transform firePoint, SpriteRenderer sourceRenderer)
        {
            Transform anchor = firePoint != null ? firePoint : transform;
            effectObject = new GameObject("LongRangeAttackInEffect");
            effectObject.transform.position = ResolveEffectWorldPosition(anchor);
            effectObject.transform.rotation = anchor.rotation;
            effectObject.transform.localScale = effectScale;

            if (parentToFirePoint)
            {
                effectObject.transform.SetParent(anchor, true);
            }

            effectRenderer = effectObject.AddComponent<SpriteRenderer>();
            ApplyRendererSettings(effectRenderer, sourceRenderer, sortingOrderOffset);
        }

        private void CreateTimingEffect(SpriteRenderer sourceRenderer)
        {
            if (timingFrames == null || timingFrames.Length == 0 || sourceRenderer == null)
            {
                return;
            }

            timingEffectObject = new GameObject("RangedAttackTimingEffect");
            timingEffectObject.transform.position = ResolveTimingWorldPosition(sourceRenderer);
            timingEffectObject.transform.rotation = transform.rotation;

            float spriteWidth = Mathf.Max(0.0001f, timingFrames[0].bounds.size.x);
            float targetWidth = Mathf.Max(0.0001f, sourceRenderer.bounds.size.x) * timingFitScaleMultiplier;
            float fittedScale = targetWidth / spriteWidth;
            timingEffectObject.transform.localScale = Vector3.one * fittedScale;
            timingEffectObject.transform.SetParent(transform, true);

            timingEffectRenderer = timingEffectObject.AddComponent<SpriteRenderer>();
            timingEffectRenderer.sprite = timingFrames[0];
            timingEffectRenderer.enabled = false;
            ApplyRendererSettings(timingEffectRenderer, sourceRenderer, sortingOrderOffset + 1);
        }

        public Vector3 ResolveEffectWorldPosition(Transform firePoint)
        {
            CacheComponents();
            Transform anchor = firePoint != null ? firePoint : transform;
            return anchor.TransformPoint(ResolveLocalOffset());
        }

        private Vector3 ResolveLocalOffset()
        {
            int facingDirection = enemyController != null ? enemyController.FacingDirection : 1;
            return localOffset + (Vector3.right * Mathf.Sign(facingDirection) * forwardOffset);
        }

        private Vector3 ResolveTimingWorldPosition(SpriteRenderer sourceRenderer)
        {
            Vector3 center = sourceRenderer != null ? sourceRenderer.bounds.center : transform.position;
            int facingDirection = enemyController != null ? enemyController.FacingDirection : 1;
            Vector3 facingOffset = Vector3.right * Mathf.Sign(facingDirection) * timingForwardOffset;
            return center + transform.TransformVector(timingLocalOffset + facingOffset);
        }

        private void HandleWindupEnded()
        {
            StopAndDestroyEffects();
        }

        private IEnumerator PlayRoutine()
        {
            while (effectRenderer != null)
            {
                yield return null;
                AdvancePlayback(Time.deltaTime, EnemyGameplayPause.IsPaused());
            }
        }

        private void AdvancePlayback(float deltaTime, bool isPaused)
        {
            if (isPaused || effectRenderer == null)
            {
                return;
            }

            playbackElapsed = Mathf.Min(playbackDuration, playbackElapsed + Mathf.Max(0f, deltaTime));
            ApplyVisualFrames();
        }

        private void ApplyVisualFrames()
        {
            if (effectRenderer == null || frames == null || frames.Length == 0)
            {
                return;
            }

            float frameSeconds = playbackDuration / frames.Length;
            int chargeFrameIndex = Mathf.Min(
                frames.Length - 1,
                Mathf.FloorToInt(playbackElapsed / Mathf.Max(0.0001f, frameSeconds)));
            effectRenderer.sprite = frames[chargeFrameIndex];

            if (timingEffectRenderer == null || timingFrames == null || timingFrames.Length == 0)
            {
                return;
            }

            int timingStartChargeFrame = Mathf.Max(0, frames.Length - timingFrames.Length);
            if (chargeFrameIndex < timingStartChargeFrame)
            {
                timingEffectRenderer.enabled = false;
                return;
            }

            int timingFrameIndex = Mathf.Min(
                timingFrames.Length - 1,
                chargeFrameIndex - timingStartChargeFrame);
            timingEffectRenderer.sprite = timingFrames[timingFrameIndex];
            timingEffectRenderer.enabled = true;
        }

        private void StopAndDestroyEffects()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            DestroyEffectObject(ref effectObject, ref effectRenderer);
            DestroyEffectObject(ref timingEffectObject, ref timingEffectRenderer);
            playbackElapsed = 0f;
            playbackDuration = 0f;
        }

        private static void DestroyEffectObject(ref GameObject target, ref SpriteRenderer renderer)
        {
            if (target != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(target);
                }
                else
                {
                    DestroyImmediate(target);
                }
            }

            target = null;
            renderer = null;
        }

        private void ApplyRendererSettings(SpriteRenderer renderer, SpriteRenderer sourceRenderer, int orderOffset)
        {
            if (sourceRenderer == null)
            {
                renderer.sortingOrder = orderOffset;
                return;
            }

            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = sourceRenderer.sortingOrder + orderOffset;

            if (copyEnemyMaterial && sourceRenderer.sharedMaterial != null)
            {
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            }
        }

        private SpriteRenderer ResolveFrontmostEnemyRenderer()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer frontmost = null;

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || renderer == effectRenderer || renderer == timingEffectRenderer)
                {
                    continue;
                }

                if (frontmost == null || CompareRendererSort(renderer, frontmost) > 0)
                {
                    frontmost = renderer;
                }
            }

            return frontmost;
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

        private void BuildFramesIfNeeded()
        {
            if (frames == null || frames.Length == 0)
            {
                frames = BuildFrames(inSpriteSheet, frameColumns, frameRows, frameCount, pixelsPerUnit);
            }

            if (timingFrames == null || timingFrames.Length == 0)
            {
                timingFrames = BuildFrames(
                    attackTimingSpriteSheet,
                    timingFrameColumns,
                    timingFrameRows,
                    timingFrameCount,
                    timingPixelsPerUnit);
            }
        }

        private Sprite[] BuildFrames(
            Texture2D spriteSheet,
            int columns,
            int rows,
            int requestedFrameCount,
            float spritePixelsPerUnit)
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

            int maxFrameCount = Mathf.Min(requestedFrameCount, columns * rows);
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
                        spritePixelsPerUnit,
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
            for (int i = 0; i < generatedSprites.Count; i++)
            {
                if (generatedSprites[i] != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(generatedSprites[i]);
                    }
                    else
                    {
                        DestroyImmediate(generatedSprites[i]);
                    }
                }
            }
        }
    }
}
