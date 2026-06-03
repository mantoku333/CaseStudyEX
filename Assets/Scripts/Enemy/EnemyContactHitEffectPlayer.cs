using System.Collections;
using System.Collections.Generic;
using Metroidvania.Enemy;
using Player;
using UnityEngine;

namespace GameName.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyContact))]
    public sealed class EnemyContactHitEffectPlayer : MonoBehaviour
    {
        [Header("Sprite Sheets")]
        [SerializeField] private Texture2D normalHitSpriteSheet;
        [SerializeField] private Texture2D fatalHitSpriteSheet;
        [SerializeField, Min(1)] private int frameColumns = 5;
        [SerializeField, Min(1)] private int frameRows = 3;
        [SerializeField, Min(1)] private int frameCount = 15;
        [SerializeField, Min(0.01f)] private float frameSeconds = 0.033f;
        [SerializeField, Min(1f)] private float pixelsPerUnit = 100f;

        [Header("Placement")]
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.2f, 0f);
        [SerializeField] private Vector3 effectScale = Vector3.one;
        [SerializeField] private bool parentToPlayer = true;

        [Header("Renderer")]
        [SerializeField] private int sortingOrderOffset = 3;
        [SerializeField] private bool copyPlayerMaterial = true;

        [Header("Fatal Hit")]
        [SerializeField, Min(0f)] private float chargingGraceSeconds = 0.15f;

        private readonly List<Sprite> generatedSprites = new();
        private EnemyContact enemyContact;
        private EnemyTackleAttack tackleAttack;
        private Sprite[] normalFrames;
        private Sprite[] fatalFrames;
        private float lastChargingTime = -999f;

        private void Awake()
        {
            CacheComponents();
            BuildFramesIfNeeded();
        }

        private void OnEnable()
        {
            CacheComponents();
            if (enemyContact != null)
            {
                enemyContact.ContactDamageApplied -= HandleContactDamageApplied;
                enemyContact.ContactDamageApplied += HandleContactDamageApplied;
            }
        }

        private void OnDisable()
        {
            if (enemyContact != null)
            {
                enemyContact.ContactDamageApplied -= HandleContactDamageApplied;
            }
        }

        private void Update()
        {
            if (tackleAttack != null && tackleAttack.IsCharging)
            {
                lastChargingTime = Time.time;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            frameColumns = Mathf.Max(1, frameColumns);
            frameRows = Mathf.Max(1, frameRows);
            frameCount = Mathf.Max(1, frameCount);
            frameSeconds = Mathf.Max(0.01f, frameSeconds);
            pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
        }
#endif

        private void CacheComponents()
        {
            if (enemyContact == null)
            {
                enemyContact = GetComponent<EnemyContact>();
            }

            if (tackleAttack == null)
            {
                tackleAttack = GetComponent<EnemyTackleAttack>();
            }
        }

        private void HandleContactDamageApplied(PlayerHealth playerHealth, Collider2D playerBodyCollider)
        {
            if (playerHealth == null)
            {
                return;
            }

            BuildFramesIfNeeded();

            bool isFatalTackleHit =
                IsTackleHitActive() &&
                playerHealth.CurrentHealth <= 0;

            Sprite[] frames = isFatalTackleHit ? fatalFrames : normalFrames;
            if (frames == null || frames.Length == 0)
            {
                return;
            }

            PlayEffect(playerHealth, playerBodyCollider, frames);
        }

        private bool IsTackleHitActive()
        {
            if (tackleAttack == null)
            {
                return false;
            }

            return tackleAttack.IsCharging ||
                   Time.time <= lastChargingTime + chargingGraceSeconds;
        }

        private void PlayEffect(PlayerHealth playerHealth, Collider2D playerBodyCollider, Sprite[] frames)
        {
            GameObject effectObject = new GameObject("EnemyContactHitEffect");
            effectObject.transform.position = ResolveEffectPosition(playerHealth, playerBodyCollider);
            effectObject.transform.localScale = effectScale;

            if (parentToPlayer)
            {
                effectObject.transform.SetParent(playerHealth.transform, true);
            }

            SpriteRenderer renderer = effectObject.AddComponent<SpriteRenderer>();
            ApplyRendererSettings(renderer, playerHealth);
            StartCoroutine(PlayEffectRoutine(effectObject, renderer, frames));
        }

        private Vector3 ResolveEffectPosition(PlayerHealth playerHealth, Collider2D playerBodyCollider)
        {
            if (playerBodyCollider != null)
            {
                return playerBodyCollider.bounds.center + worldOffset;
            }

            Collider2D fallbackCollider = playerHealth.GetComponent<Collider2D>();
            if (fallbackCollider != null)
            {
                return fallbackCollider.bounds.center + worldOffset;
            }

            return playerHealth.transform.position + worldOffset;
        }

        private void ApplyRendererSettings(SpriteRenderer renderer, PlayerHealth playerHealth)
        {
            SpriteRenderer sourceRenderer = ResolveFrontmostPlayerRenderer(playerHealth);
            if (sourceRenderer == null)
            {
                renderer.sortingOrder = sortingOrderOffset;
                return;
            }

            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;

            if (copyPlayerMaterial && sourceRenderer.sharedMaterial != null)
            {
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            }
        }

        private static SpriteRenderer ResolveFrontmostPlayerRenderer(PlayerHealth playerHealth)
        {
            SpriteRenderer[] renderers = playerHealth.GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer frontmost = null;

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
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

        private IEnumerator PlayEffectRoutine(GameObject effectObject, SpriteRenderer renderer, Sprite[] frames)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                if (renderer == null)
                {
                    yield break;
                }

                renderer.sprite = frames[i];
                yield return new WaitForSeconds(frameSeconds);
            }

            Destroy(effectObject);
        }

        private void BuildFramesIfNeeded()
        {
            if (normalFrames == null || normalFrames.Length == 0)
            {
                normalFrames = BuildFrames(normalHitSpriteSheet);
            }

            if (fatalFrames == null || fatalFrames.Length == 0)
            {
                fatalFrames = BuildFrames(fatalHitSpriteSheet);
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
            Sprite[] frames = new Sprite[maxFrameCount];
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

                    frames[index++] = sprite;
                    generatedSprites.Add(sprite);
                }
            }

            return frames;
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
