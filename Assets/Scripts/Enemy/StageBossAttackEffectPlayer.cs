using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameName.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StageBossAttack))]
    public sealed class StageBossAttackEffectPlayer : MonoBehaviour
    {
        [Header("Frames")]
        [SerializeField] private Texture2D attackSpriteSheet;
        [SerializeField, Min(1)] private int frameColumns = 3;
        [SerializeField, Min(1)] private int frameRows = 2;
        [SerializeField, Min(1)] private int frameCount = 6;
        [SerializeField, Min(0.01f)] private float frameSeconds = 0.033f;
        [SerializeField, Min(1f)] private float pixelsPerUnit = 100f;
        [SerializeField] private Vector2 spritePivot = new Vector2(0.25f, 0.5f);

        [Header("Placement")]
        [SerializeField] private Vector3 worldOffset = new Vector3(2f, -0.75f, 0f);
        [SerializeField] private Vector3 effectScale = new Vector3(1.4f, 1.4f, 1.4f);
        [SerializeField, Min(0f)] private float downwardAngleDegrees = 20f;
        [SerializeField] private bool parentToEnemy = true;

        [Header("Renderer")]
        [SerializeField] private int sortingOrderOffset = 4;
        [SerializeField] private bool copyEnemyMaterial = true;

        private StageBossAttack stageBossAttack;
        private EnemyController enemyController;
        private Collider2D bodyCollider;
        private GameObject effectObject;
        private SpriteRenderer effectRenderer;
        private Coroutine playRoutine;
        private readonly List<Sprite> generatedSprites = new();
        private Sprite[] frames;

        private void Awake()
        {
            CacheComponents();
            BuildFramesIfNeeded();
        }

        private void OnEnable()
        {
            CacheComponents();
            if (stageBossAttack != null)
            {
                stageBossAttack.ChargeEnding -= HandleChargeEnding;
                stageBossAttack.ChargeEnding += HandleChargeEnding;
            }
        }

        private void OnDisable()
        {
            if (stageBossAttack != null)
            {
                stageBossAttack.ChargeEnding -= HandleChargeEnding;
            }

            StopAndDestroyEffect();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            frameColumns = Mathf.Max(1, frameColumns);
            frameRows = Mathf.Max(1, frameRows);
            frameCount = Mathf.Max(1, frameCount);
            frameSeconds = Mathf.Max(0.01f, frameSeconds);
            pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
            spritePivot = new Vector2(
                Mathf.Clamp01(spritePivot.x),
                Mathf.Clamp01(spritePivot.y));
        }
#endif

        private void CacheComponents()
        {
            if (stageBossAttack == null)
            {
                stageBossAttack = GetComponent<StageBossAttack>();
            }

            if (enemyController == null)
            {
                enemyController = GetComponent<EnemyController>();
            }

            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }
        }

        private void HandleChargeEnding()
        {
            BuildFramesIfNeeded();
            if (frames == null || frames.Length == 0)
            {
                return;
            }

            StopAndDestroyEffect();

            Vector3 enemyCenter = ResolveEnemyCenter();
            Vector3 effectPosition = ResolveEffectPosition(enemyCenter);
            bool isFacingLeft = ResolveFacingDirection() < 0;

            effectObject = new GameObject("StageBossAttackEffect");
            effectObject.transform.position = effectPosition;
            effectObject.transform.rotation = ResolveEffectRotation(enemyCenter, effectPosition, isFacingLeft);
            effectObject.transform.localScale = effectScale;

            if (parentToEnemy)
            {
                effectObject.transform.SetParent(transform, true);
            }

            effectRenderer = effectObject.AddComponent<SpriteRenderer>();
            effectRenderer.flipX = isFacingLeft;
            ApplyRendererSettings(effectRenderer);
            playRoutine = StartCoroutine(PlayRoutine());
        }

        private Vector3 ResolveEnemyCenter()
        {
            if (bodyCollider != null)
            {
                return bodyCollider.bounds.center;
            }

            return transform.position;
        }

        private Vector3 ResolveEffectPosition(Vector3 enemyCenter)
        {
            Vector3 offset = worldOffset;
            offset.x = Mathf.Abs(offset.x) * ResolveFacingDirection();
            return enemyCenter + offset;
        }

        private Quaternion ResolveEffectRotation(Vector3 enemyCenter, Vector3 effectPosition, bool isFacingLeft)
        {
            Vector2 direction = effectPosition - enemyCenter;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                float fallbackAngle = ResolveFacingDirection() < 0
                    ? 180f + downwardAngleDegrees
                    : -downwardAngleDegrees;
                direction = Quaternion.Euler(0f, 0f, fallbackAngle) * Vector2.right;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float spriteDirectionOffset = isFacingLeft ? 0f : 180f;
            return Quaternion.Euler(0f, 0f, angle + spriteDirectionOffset);
        }

        private int ResolveFacingDirection()
        {
            int facing = enemyController != null ? enemyController.FacingDirection : 1;
            return facing < 0 ? -1 : 1;
        }

        private IEnumerator PlayRoutine()
        {
            for (int i = 0; i < frames.Length; i++)
            {
                if (effectRenderer == null)
                {
                    yield break;
                }

                Sprite frame = frames[i];
                if (frame != null)
                {
                    effectRenderer.sprite = frame;
                }

                yield return new WaitForSeconds(frameSeconds);
            }

            StopAndDestroyEffect();
        }

        private void BuildFramesIfNeeded()
        {
            if (frames == null || frames.Length == 0)
            {
                frames = BuildFrames(attackSpriteSheet);
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
                        spritePivot,
                        pixelsPerUnit,
                        0,
                        SpriteMeshType.FullRect);

                    builtFrames[index++] = sprite;
                    generatedSprites.Add(sprite);
                }
            }

            return builtFrames;
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
