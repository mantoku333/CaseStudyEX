using System.Collections;
using Metroidvania.Player;
using Player;
using UnityEngine;

namespace GameName.Enemy
{
    // GroundBlade/RainBlade共通の攻撃判定。物理で押さず、trigger重なりでダメージとパリィを処理する。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class LastBossBladeAttack : MonoBehaviour, IParryableAttack
    {
        public enum BladeKind
        {
            Ground,
            Rain
        }

        private readonly Collider2D[] overlapResults = new Collider2D[8];

        private LastBossController owner;
        private Collider2D bladeCollider;
        private LastBossBladeVisual bladeVisual;
        private SpriteRenderer[] spriteRenderers = System.Array.Empty<SpriteRenderer>();
        private Color[] spriteRendererStartColors = System.Array.Empty<Color>();
        private ContactFilter2D playerOverlapFilter;
        private BladeKind kind;
        private Vector2 rainTargetPoint;
        private Vector3 originalScale;
        private GridSpriteSheetClip groundVisualClip;
        private GridSpriteSheetClip rainInVisualClip;
        private GridSpriteSheetClip rainOutVisualClip;
        private Vector2 groundVisualWorldSize;
        private Vector2 rainVisualWorldSize;
        private int damage = 1;
        private int groundVisualUprightFrameIndex;
        private int groundVisualSlotIndex = -1;
        private float rainFallSpeed = 8f;
        private float rainGroundDestroyDelay = 0.3f;
        private Vector2 groundVisualFrameSizeMultiplier = Vector2.one;
        private Vector2 rainVisualFrameSizeMultiplier = Vector2.one;
        [SerializeField] private float rainAimRotationOffsetDegrees = 180f;
        private bool initialized;
        private bool canDamage;
        private bool rainReleased;
        private bool rainLanded;
        private bool parried;
        private bool destroying;
        private bool useGroundVisual;
        private bool useRainVisual;

        public BladeKind Kind => kind;
        public bool IsParryable => initialized && canDamage && !parried && !destroying;

        private void Awake()
        {
            bladeCollider = GetComponent<Collider2D>();
            CacheSpriteRenderers();
            originalScale = transform.localScale;

            playerOverlapFilter = new ContactFilter2D
            {
                useLayerMask = false,
                useTriggers = false
            };

            if (bladeCollider != null)
            {
                bladeCollider.isTrigger = true;
            }
        }

        private void OnValidate()
        {
            Collider2D validatedCollider = GetComponent<Collider2D>();
            if (validatedCollider != null)
            {
                validatedCollider.isTrigger = true;
            }
        }

        private void Update()
        {
            if (!initialized || destroying)
            {
                return;
            }

            if (kind == BladeKind.Rain && rainReleased && !rainLanded)
            {
                MoveRainBlade();
            }

            if (canDamage)
            {
                DamageOverlappingPlayer();
            }
        }

        public void ConfigureGroundVisual(
            GridSpriteSheetClip clip,
            int uprightFrameIndex,
            int slotIndex,
            Vector2 frameSizeMultiplier)
        {
            groundVisualClip = clip;
            groundVisualUprightFrameIndex = Mathf.Max(0, uprightFrameIndex);
            groundVisualSlotIndex = slotIndex;
            groundVisualFrameSizeMultiplier = SanitizeFrameSizeMultiplier(frameSizeMultiplier);
            useGroundVisual = clip.IsValid;
            groundVisualWorldSize = Vector2.Scale(ResolveBladeWorldSize(), groundVisualFrameSizeMultiplier);

            if (useGroundVisual)
            {
                EnsureBladeVisual();
            }
        }

        public void ConfigureRainVisual(
            GridSpriteSheetClip inClip,
            GridSpriteSheetClip outClip,
            Vector2 frameSizeMultiplier)
        {
            rainInVisualClip = inClip;
            rainOutVisualClip = outClip;
            rainVisualFrameSizeMultiplier = SanitizeFrameSizeMultiplier(frameSizeMultiplier);
            useRainVisual = inClip.IsValid;
            rainVisualWorldSize = Vector2.Scale(ResolveBladeWorldSize(), rainVisualFrameSizeMultiplier);

            if (useRainVisual)
            {
                EnsureBladeVisual();
            }
        }

        public void InitializeGround(
            LastBossController attackOwner,
            int attackDamage,
            float groundY,
            float riseDuration)
        {
            owner = attackOwner;
            kind = BladeKind.Ground;
            damage = Mathf.Max(1, attackDamage);
            initialized = true;
            canDamage = !useGroundVisual;
            rainReleased = false;
            rainLanded = false;
            parried = false;
            destroying = false;
            transform.localScale = originalScale;

            if (originalScale == Vector3.zero)
            {
                originalScale = transform.localScale;
            }

            if (bladeCollider != null)
            {
                bladeCollider.enabled = !useGroundVisual;
            }

            if (useGroundVisual && PlayGroundVisual(groundY))
            {
                return;
            }

            EnableBladeDamage();
            StartCoroutine(GroundBladeRiseRoutine(groundY, Mathf.Max(0f, riseDuration)));
        }

        public void InitializeRainPreview(
            LastBossController attackOwner,
            int attackDamage,
            Vector2 targetPoint,
            Vector2 previewAimPoint,
            float fallSpeed,
            float groundDestroyDelay)
        {
            owner = attackOwner;
            kind = BladeKind.Rain;
            damage = Mathf.Max(1, attackDamage);
            rainTargetPoint = targetPoint;
            rainFallSpeed = Mathf.Max(0.01f, fallSpeed);
            rainGroundDestroyDelay = Mathf.Max(0f, groundDestroyDelay);
            initialized = true;
            canDamage = false;
            rainReleased = false;
            rainLanded = false;
            parried = false;
            destroying = false;
            transform.localScale = originalScale;

            RotateToward(previewAimPoint);
            if (useRainVisual)
            {
                EnsureBladeVisual();
                bladeVisual.PlayRainIn(rainInVisualClip, ResolveCachedVisualWorldSize(rainVisualWorldSize));
            }
        }

        public void UpdateRainPreview(Vector2 previewPosition, Vector2 targetPoint, Vector2 previewAimPoint)
        {
            if (kind != BladeKind.Rain || rainReleased || destroying)
            {
                return;
            }

            rainTargetPoint = targetPoint;
            transform.position = new Vector3(previewPosition.x, previewPosition.y, transform.position.z);
            RotateToward(previewAimPoint);
        }

        public void ReleaseRainBlade()
        {
            if (kind != BladeKind.Rain || destroying)
            {
                return;
            }

            rainReleased = true;
            canDamage = true;
            if (bladeCollider != null)
            {
                bladeCollider.enabled = true;
            }

            RotateToward(rainTargetPoint);
        }

        public void ForceDestroy()
        {
            DestroySelf();
        }

        public void ForceFadeOut(float duration)
        {
            if (destroying)
            {
                return;
            }

            if (duration <= 0f || spriteRenderers.Length == 0)
            {
                DestroySelf();
                return;
            }

            // パリィ中断時だけ使う演出。攻撃判定を止めてから、現在の色を基準に薄くする。
            destroying = true;
            canDamage = false;
            StopAllCoroutines();
            CaptureCurrentSpriteColors();

            if (bladeCollider != null)
            {
                bladeCollider.enabled = false;
            }

            StartCoroutine(FadeOutRoutine(duration));
        }

        public void StopByParry()
        {
            if (!IsParryable)
            {
                return;
            }

            parried = true;
            canDamage = false;

            if (owner != null)
            {
                owner.NotifyBladeParried(this);
            }
            else
            {
                DestroySelf();
            }
        }

        private IEnumerator GroundBladeRiseRoutine(float groundY, float riseDuration)
        {
            float bladeHeight = ResolveBladeWorldHeight();
            Vector3 finalPosition = transform.position;
            finalPosition.y = groundY + bladeHeight * 0.5f;

            // scaleを変えると中央から開く見た目になるため、位置だけ動かして地面から出す。
            Vector3 startPosition = finalPosition - Vector3.up * bladeHeight;
            transform.position = startPosition;

            if (riseDuration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < riseDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / riseDuration);
                    transform.position = Vector3.Lerp(startPosition, finalPosition, t);
                    yield return null;
                }
            }

            transform.position = finalPosition;
        }

        private void MoveRainBlade()
        {
            Vector2 currentPosition = transform.position;
            Vector2 nextPosition = Vector2.MoveTowards(
                currentPosition,
                rainTargetPoint,
                rainFallSpeed * Time.deltaTime);

            // RainBladeはControllerが決めたスロット下端へ落ちる。着地後の消滅時間はInspectorで調整する。
            transform.position = new Vector3(nextPosition.x, nextPosition.y, transform.position.z);
            RotateToward(rainTargetPoint);

            if (Vector2.Distance(nextPosition, rainTargetPoint) <= 0.01f)
            {
                rainLanded = true;
                owner?.NotifyBladeLanded(this);
                canDamage = false;

                if (bladeCollider != null)
                {
                    bladeCollider.enabled = false;
                }

                if (useRainVisual && bladeVisual != null)
                {
                    bladeVisual.PlayRainOut(
                        rainOutVisualClip,
                        ResolveCachedVisualWorldSize(rainVisualWorldSize),
                        DestroySelf);
                    return;
                }

                StartCoroutine(DestroyAfterGroundDelayRoutine());
            }
        }

        private IEnumerator DestroyAfterGroundDelayRoutine()
        {
            if (rainGroundDestroyDelay > 0f)
            {
                yield return new WaitForSeconds(rainGroundDestroyDelay);
            }

            DestroySelf();
        }

        private void DamageOverlappingPlayer()
        {
            if (bladeCollider == null || !bladeCollider.enabled)
            {
                return;
            }

            // Player本体Colliderだけを拾い、攻撃1回あたりのダメージ制限はController側で行う。
            int overlapCount = bladeCollider.Overlap(playerOverlapFilter, overlapResults);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider2D hit = overlapResults[i];
                overlapResults[i] = null;

                if (!PlayerBodyColliderUtility.TryGetPlayerBodyFromCollider(
                        hit,
                        out PlayerHealth playerHealth,
                        out _))
                {
                    continue;
                }

                if (owner != null && owner.TryApplyBladeDamage(this, playerHealth, damage))
                {
                    return;
                }
            }
        }

        private void RotateToward(Vector2 targetPoint)
        {
            Vector2 direction = targetPoint - (Vector2)transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float angle = Vector2.SignedAngle(Vector2.up, direction) + rainAimRotationOffsetDegrees;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private float ResolveBladeWorldHeight()
        {
            if (bladeCollider != null)
            {
                float colliderHeight = bladeCollider.bounds.size.y;
                if (colliderHeight > 0.001f)
                {
                    return colliderHeight;
                }
            }

            SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null)
            {
                float rendererHeight = renderer.bounds.size.y;
                if (rendererHeight > 0.001f)
                {
                    return rendererHeight;
                }
            }

            return Mathf.Max(0.1f, Mathf.Abs(transform.lossyScale.y));
        }

        private bool PlayGroundVisual(float groundY)
        {
            EnsureBladeVisual();
            if (bladeVisual == null)
            {
                return false;
            }

            float bladeHeight = ResolveBladeWorldHeight();
            Vector3 finalPosition = transform.position;
            finalPosition.y = groundY + bladeHeight * 0.5f;
            transform.position = finalPosition;

            return bladeVisual.PlayGround(
                groundVisualClip,
                groundVisualUprightFrameIndex,
                ResolveCachedVisualWorldSize(groundVisualWorldSize),
                () =>
                {
                    EnableBladeDamage();
                    owner?.NotifyGroundBladeUpright(groundVisualSlotIndex);
                });
        }

        private void EnableBladeDamage()
        {
            canDamage = true;
            if (bladeCollider != null)
            {
                bladeCollider.enabled = true;
            }
        }

        private Vector2 ResolveBladeWorldSize()
        {
            if (bladeCollider != null)
            {
                Vector2 colliderSize = bladeCollider.bounds.size;
                if (colliderSize.x > 0.001f && colliderSize.y > 0.001f)
                {
                    return colliderSize;
                }
            }

            SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null)
            {
                Vector2 rendererSize = renderer.bounds.size;
                if (rendererSize.x > 0.001f && rendererSize.y > 0.001f)
                {
                    return rendererSize;
                }
            }

            Vector3 scale = transform.lossyScale;
            return new Vector2(
                Mathf.Max(0.1f, Mathf.Abs(scale.x)),
                Mathf.Max(0.1f, Mathf.Abs(scale.y)));
        }

        private Vector2 ResolveCachedVisualWorldSize(Vector2 cachedSize)
        {
            if (cachedSize.x > 0.001f && cachedSize.y > 0.001f)
            {
                return cachedSize;
            }

            return ResolveBladeWorldSize();
        }

        private static Vector2 SanitizeFrameSizeMultiplier(Vector2 multiplier)
        {
            return new Vector2(
                Mathf.Max(0.01f, multiplier.x),
                Mathf.Max(0.01f, multiplier.y));
        }

        private void EnsureBladeVisual()
        {
            if (bladeVisual == null)
            {
                bladeVisual = GetComponent<LastBossBladeVisual>();
            }

            if (bladeVisual == null)
            {
                bladeVisual = gameObject.AddComponent<LastBossBladeVisual>();
            }

            CacheSpriteRenderers();
        }

        private void CacheSpriteRenderers()
        {
            // 後でアニメSpriteへ差し替えてもフェードできるよう、子SpriteRendererもまとめて扱う。
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            CaptureCurrentSpriteColors();
        }

        private void CaptureCurrentSpriteColors()
        {
            spriteRendererStartColors = new Color[spriteRenderers.Length];

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    spriteRendererStartColors[i] = spriteRenderers[i].color;
                }
            }
        }

        private IEnumerator FadeOutRoutine(float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alphaT = 1f - Mathf.Clamp01(elapsed / duration);
                ApplyFadeAlpha(alphaT);
                yield return null;
            }

            ApplyFadeAlpha(0f);
            DestroyBladeObject();
        }

        private void ApplyFadeAlpha(float alphaMultiplier)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer targetRenderer = spriteRenderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                Color color = i < spriteRendererStartColors.Length
                    ? spriteRendererStartColors[i]
                    : targetRenderer.color;
                color.a *= alphaMultiplier;
                targetRenderer.color = color;
            }
        }

        private void DestroySelf()
        {
            if (destroying)
            {
                return;
            }

            destroying = true;
            canDamage = false;
            StopAllCoroutines();
            DestroyBladeObject();
        }

        private void OnDestroy()
        {
            if (owner != null)
            {
                owner.NotifyBladeDestroyed(this);
            }
        }

        private void DestroyBladeObject()
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
