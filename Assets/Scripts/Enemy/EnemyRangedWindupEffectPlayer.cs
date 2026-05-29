using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameName.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyRangedAttack))]
    public sealed class EnemyRangedWindupEffectPlayer : MonoBehaviour
    {
        [Header("Sprite Sheet")]
        [SerializeField] private Texture2D inSpriteSheet;
        [SerializeField, Min(1)] private int frameColumns = 5;
        [SerializeField, Min(1)] private int frameRows = 12;
        [SerializeField, Min(1)] private int frameCount = 60;
        [SerializeField, Min(1f)] private float pixelsPerUnit = 100f;

        [Header("Placement")]
        [SerializeField] private Vector3 localOffset = Vector3.zero;
        [SerializeField, Min(0f)] private float forwardOffset = 0.5f;
        [SerializeField] private Vector3 effectScale = Vector3.one;
        [SerializeField] private bool parentToFirePoint = true;

        [Header("Renderer")]
        [SerializeField] private int sortingOrderOffset = 3;
        [SerializeField] private bool copyEnemyMaterial = true;

        private readonly List<Sprite> generatedSprites = new();
        private EnemyRangedAttack rangedAttack;
        private EnemyController enemyController;
        private Sprite[] frames;
        private GameObject effectObject;
        private SpriteRenderer effectRenderer;
        private Coroutine playRoutine;

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

            StopAndDestroyEffect();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            frameColumns = Mathf.Max(1, frameColumns);
            frameRows = Mathf.Max(1, frameRows);
            frameCount = Mathf.Max(1, frameCount);
            pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
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

            StopAndDestroyEffect();

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
            ApplyRendererSettings(effectRenderer);
            playRoutine = StartCoroutine(PlayRoutine(Mathf.Max(0.01f, duration)));
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

        private void HandleWindupEnded()
        {
            StopAndDestroyEffect();
        }

        private IEnumerator PlayRoutine(float duration)
        {
            float frameSeconds = Mathf.Max(0.01f, duration / frames.Length);
            for (int i = 0; i < frames.Length; i++)
            {
                if (effectRenderer == null)
                {
                    yield break;
                }

                effectRenderer.sprite = frames[i];
                yield return new WaitForSeconds(frameSeconds);
            }

            StopAndDestroyEffect();
        }

        private void StopAndDestroyEffect()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            if (effectObject != null)
            {
                Destroy(effectObject);
                effectObject = null;
                effectRenderer = null;
            }
        }

        private void ApplyRendererSettings(SpriteRenderer renderer)
        {
            SpriteRenderer sourceRenderer = ResolveFrontmostEnemyRenderer();
            if (sourceRenderer == null)
            {
                renderer.sortingOrder = sortingOrderOffset;
                return;
            }

            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;

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
                if (renderer == null || renderer == effectRenderer)
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
                frames = BuildFrames(inSpriteSheet);
            }
        }

        private Sprite[] BuildFrames(Texture2D spriteSheet)
        {
            if (spriteSheet == null)
            {
                return System.Array.Empty<Sprite>();
            }

            int frameWidth = spriteSheet.width / frameColumns;
            int frameHeight = spriteSheet.height / frameRows;
            if (frameWidth <= 0 || frameHeight <= 0)
            {
                return System.Array.Empty<Sprite>();
            }

            int maxFrameCount = Mathf.Min(frameCount, frameColumns * frameRows);
            Sprite[] builtFrames = new Sprite[maxFrameCount];
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            int index = 0;

            for (int row = 0; row < frameRows && index < maxFrameCount; row++)
            {
                int y = spriteSheet.height - ((row + 1) * frameHeight);

                for (int column = 0; column < frameColumns && index < maxFrameCount; column++)
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
