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
        private SpriteRenderer[] spriteRenderers = System.Array.Empty<SpriteRenderer>();
        private Color[] spriteRendererStartColors = System.Array.Empty<Color>();
        private ContactFilter2D playerOverlapFilter;
        private BladeKind kind;
        private Vector2 rainTargetPoint;
        private Vector3 originalScale;
        private int damage = 1;
        private float rainFallSpeed = 8f;
        private float rainGroundDestroyDelay = 0.3f;
        private bool initialized;
        private bool canDamage;
        private bool rainReleased;
        private bool rainLanded;
        private bool parried;
        private bool destroying;

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
            canDamage = true;
            rainReleased = false;
            rainLanded = false;
            parried = false;
            destroying = false;
            transform.localScale = originalScale;

            if (originalScale == Vector3.zero)
            {
                originalScale = transform.localScale;
            }

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

            float angle = Vector2.SignedAngle(Vector2.up, direction);
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
            Destroy(gameObject);
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
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (owner != null)
            {
                owner.NotifyBladeDestroyed(this);
            }
        }
    }
}
