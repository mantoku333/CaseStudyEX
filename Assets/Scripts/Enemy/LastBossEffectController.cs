using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameName.Enemy
{
    [DisallowMultipleComponent]
    public sealed class LastBossEffectController : MonoBehaviour
    {
        [Header("Sprite Sheets")]
        [SerializeField] private Texture2D shieldInSpriteSheet;
        [SerializeField] private Texture2D shieldLoopSpriteSheet;
        [SerializeField] private Texture2D shieldBreakSpriteSheet;
        [SerializeField] private Texture2D auraSpriteSheet;
        [SerializeField] private Texture2D deathSpriteSheet;
        [SerializeField] private Texture2D slashSpriteSheet;
        [SerializeField] private Texture2D underAttackSpriteSheet;
        [SerializeField] private Texture2D rangeSpriteSheet;
        [SerializeField] private Texture2D magicCircleInSpriteSheet;
        [SerializeField] private Texture2D magicCircleOutSpriteSheet;
        [SerializeField] private Texture2D topAttackInSpriteSheet;
        [SerializeField] private Texture2D topAttackOutSpriteSheet;

        [Header("Playback")]
        [SerializeField, Min(1f)] private float framesPerSecond = 30f;
        [SerializeField, Min(1f)] private float pixelsPerUnit = 100f;
        [SerializeField, Min(0f)] private float rangeSpawnInterval = 0.2f;
        [SerializeField, Min(0f)] private float rangeFadeInSeconds = 0.3f;
        [SerializeField, Min(0f)] private float rangeFadeOutSeconds = 0.5f;
        [SerializeField, Min(0)] private int bossHideFrameIndex = 4;
        [SerializeField, Min(0)] private int groundBladeUprightFrameIndex = 3;

        [Header("Boss Placement")]
        [SerializeField] private Vector3 auraOffset;
        [SerializeField] private Vector3 shieldOffset;
        [SerializeField] private Vector3 shieldBreakOffset;
        [SerializeField] private Vector3 deathOffset;
        [SerializeField, Min(0.01f)] private float auraSizeMultiplier = 1.35f;
        [SerializeField, Min(0.01f)] private float shieldSizeMultiplier = 1.35f;
        [SerializeField, Min(0.01f)] private float shieldBreakSizeMultiplier = 1.45f;
        [SerializeField, Min(0.01f)] private float deathSizeMultiplier = 1.8f;

        [Header("Attack Placement")]
        [SerializeField] private Vector3 slashOffset;
        [SerializeField, Min(0.01f)] private float slashSizeMultiplier = 1.1f;
        [SerializeField] private Vector2 rangeEffectWorldSize = new Vector2(1.6f, 1.1f);
        [SerializeField] private Vector3 rangeOffset;
        [SerializeField, Min(0.01f)] private float magicCircleBeyondSpawnDistance = 1.5f;
        [SerializeField] private Vector2 magicCircleWorldSize = new Vector2(4f, 4f);
        [SerializeField, Min(0.01f)] private float groundBladeVisualSizeMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float rainBladeVisualSizeMultiplier = 1f;

        [Header("Sorting")]
        [SerializeField] private int auraSortingOrderOffset = -2;
        [SerializeField] private int shieldSortingOrderOffset = 2;
        [SerializeField] private int shieldBreakSortingOrderOffset = 3;
        [SerializeField] private int deathSortingOrderOffset = 5;
        [SerializeField] private int slashSortingOrderOffset = 4;
        [SerializeField] private int rangeSortingOrderOffset = 1;
        [SerializeField] private int magicCircleSortingOrderOffset = 3;
        [SerializeField] private bool copyBossMaterial = true;

        private readonly List<RangeIndicator> rangeIndicators = new();
        private readonly HashSet<int> pendingRangeFadeSlots = new();

        private SpriteRenderer bossRenderer;
        private Collider2D bodyCollider;
        private GameObject auraObject;
        private GameObject shieldObject;
        private GameObject shieldBreakObject;
        private GameObject magicCircleObject;
        private GameObject deathObject;
        private GridSpriteSheetPlayer shieldPlayer;
        private GridSpriteSheetPlayer magicCirclePlayer;
        private Coroutine rangeSpawnRoutine;
        private bool shieldBrokenThisDown;
        private bool deathHideNotified;

        public int GroundBladeUprightFrameIndex => Mathf.Max(0, groundBladeUprightFrameIndex);
        public float GroundBladeVisualSizeMultiplier => Mathf.Max(0.01f, groundBladeVisualSizeMultiplier);
        public float RainBladeVisualSizeMultiplier => Mathf.Max(0.01f, rainBladeVisualSizeMultiplier);
        public float GroundBladeClipDuration => GroundBladeClip.DurationSeconds;

        public GridSpriteSheetClip GroundBladeClip => CreateClip(underAttackSpriteSheet, 5, 4, 20, new Vector2(0.5f, 0.5f));
        public GridSpriteSheetClip RainBladeInClip => CreateClip(topAttackInSpriteSheet, 5, 5, 25, new Vector2(0.5f, 0.5f));
        public GridSpriteSheetClip RainBladeOutClip => CreateClip(topAttackOutSpriteSheet, 5, 4, 20, new Vector2(0.5f, 0.5f));

        private void Awake()
        {
            CacheComponents();
        }

        private void OnEnable()
        {
            CacheComponents();
            PlayAuraLoop();
        }

        private void OnDisable()
        {
            StopShield();
            StopShieldBreak();
            StopHorizontalRangeEffects();
            StopMagicCircleImmediate();
        }

        private void LateUpdate()
        {
            FollowBoss(auraObject, auraOffset);
            FollowBoss(shieldObject, shieldOffset);
            FollowBoss(shieldBreakObject, shieldBreakOffset);
            FollowBoss(deathObject, deathOffset);
        }

        private void OnValidate()
        {
            framesPerSecond = Mathf.Max(1f, framesPerSecond);
            pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
            rangeSpawnInterval = Mathf.Max(0f, rangeSpawnInterval);
            rangeFadeInSeconds = Mathf.Max(0f, rangeFadeInSeconds);
            rangeFadeOutSeconds = Mathf.Max(0f, rangeFadeOutSeconds);
            bossHideFrameIndex = Mathf.Max(0, bossHideFrameIndex);
            groundBladeUprightFrameIndex = Mathf.Max(0, groundBladeUprightFrameIndex);
            auraSizeMultiplier = Mathf.Max(0.01f, auraSizeMultiplier);
            shieldSizeMultiplier = Mathf.Max(0.01f, shieldSizeMultiplier);
            shieldBreakSizeMultiplier = Mathf.Max(0.01f, shieldBreakSizeMultiplier);
            deathSizeMultiplier = Mathf.Max(0.01f, deathSizeMultiplier);
            slashSizeMultiplier = Mathf.Max(0.01f, slashSizeMultiplier);
            magicCircleBeyondSpawnDistance = Mathf.Max(0.01f, magicCircleBeyondSpawnDistance);
            groundBladeVisualSizeMultiplier = Mathf.Max(0.01f, groundBladeVisualSizeMultiplier);
            rainBladeVisualSizeMultiplier = Mathf.Max(0.01f, rainBladeVisualSizeMultiplier);
        }

        public void HandleEncounterStarted()
        {
            PlayAuraLoop();
            shieldBrokenThisDown = false;
            PlayShieldInThenLoop();
        }

        public void HandleEncounterStopped()
        {
            StopShield();
            StopShieldBreak();
            StopHorizontalRangeEffects();
            StopMagicCircleImmediate();
            shieldBrokenThisDown = false;
        }

        public void HandleDownStarted()
        {
            StopShield();
            StopHorizontalRangeEffects();
            EndVerticalRangeCharge();

            if (shieldBrokenThisDown)
            {
                return;
            }

            shieldBrokenThisDown = true;
            PlayShieldBreak();
        }

        public void HandleDownEnded()
        {
            shieldBrokenThisDown = false;
            PlayShieldInThenLoop();
        }

        public void HandleResetToFull()
        {
            deathHideNotified = false;
            StopShield();
            StopHorizontalRangeEffects();
            StopMagicCircleImmediate();
            StopShieldBreak();
            StopDeathImmediate();
            StopAura();
            PlayAuraLoop();
        }

        public void PlayNormalSlash(Vector2 center, Vector2 size, float angle, int facingDirection)
        {
            GridSpriteSheetClip clip = CreateClip(slashSpriteSheet, 5, 6, 30, new Vector2(0.5f, 0.5f));
            if (!clip.IsValid)
            {
                return;
            }

            Vector3 position = new Vector3(center.x, center.y, transform.position.z) + slashOffset;
            GridSpriteSheetPlayer player = CreateEffectPlayer(
                "LastBossSlashEffect",
                position,
                size * slashSizeMultiplier,
                slashSortingOrderOffset);
            if (player == null)
            {
                return;
            }

            player.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            if (player.Renderer != null)
            {
                player.Renderer.flipX = facingDirection < 0;
            }

            GameObject effectObject = player.gameObject;
            player.Play(
                clip,
                loop: false,
                holdLast: false,
                hideOnComplete: true,
                completed: () => DestroyEffectObject(effectObject));
        }

        public void BeginHorizontalRangeCharge(IReadOnlyList<Vector2> footPositions)
        {
            StopHorizontalRangeEffects();

            if (footPositions == null || footPositions.Count == 0)
            {
                return;
            }

            rangeSpawnRoutine = StartCoroutine(SpawnHorizontalRangeIndicators(footPositions));
        }

        public void FadeHorizontalRangeSlot(int slotIndex)
        {
            if (slotIndex < 0)
            {
                return;
            }

            RangeIndicator indicator = slotIndex < rangeIndicators.Count ? rangeIndicators[slotIndex] : null;
            if (indicator == null || indicator.EffectObject == null)
            {
                pendingRangeFadeSlots.Add(slotIndex);
                return;
            }

            FadeRangeIndicator(indicator);
        }

        public void StopHorizontalRangeEffects()
        {
            if (rangeSpawnRoutine != null)
            {
                StopCoroutine(rangeSpawnRoutine);
                rangeSpawnRoutine = null;
            }

            for (int i = 0; i < rangeIndicators.Count; i++)
            {
                RangeIndicator indicator = rangeIndicators[i];
                if (indicator?.EffectObject != null)
                {
                    DestroyEffectObject(indicator.EffectObject);
                }
            }

            rangeIndicators.Clear();
            pendingRangeFadeSlots.Clear();
        }

        public void BeginVerticalRangeCharge(Vector2 groundLockPoint, Vector2 rainSpawnPosition)
        {
            GridSpriteSheetClip clip = CreateClip(magicCircleInSpriteSheet, 5, 4, 19, new Vector2(0.5f, 0.5f));
            if (!clip.IsValid)
            {
                return;
            }

            StopMagicCircleImmediate();

            Vector3 position = ResolveMagicCirclePosition(groundLockPoint, rainSpawnPosition);
            magicCirclePlayer = CreateEffectPlayer(
                "LastBossMagicCircleIn",
                position,
                magicCircleWorldSize,
                magicCircleSortingOrderOffset);

            if (magicCirclePlayer == null)
            {
                return;
            }

            magicCircleObject = magicCirclePlayer.gameObject;
            magicCircleObject.transform.rotation = Quaternion.identity;
            magicCirclePlayer.Play(
                clip,
                loop: false,
                holdLast: true,
                hideOnComplete: false);
        }

        public void UpdateVerticalRangeCharge(Vector2 groundLockPoint, Vector2 rainSpawnPosition)
        {
            if (magicCircleObject == null)
            {
                return;
            }

            magicCircleObject.transform.position = ResolveMagicCirclePosition(groundLockPoint, rainSpawnPosition);
            magicCircleObject.transform.rotation = Quaternion.identity;
        }

        public void EndVerticalRangeCharge()
        {
            if (magicCircleObject == null)
            {
                return;
            }

            Vector3 position = magicCircleObject.transform.position;
            StopMagicCircleImmediate();

            GridSpriteSheetClip outClip = CreateClip(magicCircleOutSpriteSheet, 5, 2, 9, new Vector2(0.5f, 0.5f));
            if (!outClip.IsValid)
            {
                return;
            }

            GridSpriteSheetPlayer outPlayer = CreateEffectPlayer(
                "LastBossMagicCircleOut",
                position,
                magicCircleWorldSize,
                magicCircleSortingOrderOffset);
            if (outPlayer == null)
            {
                return;
            }

            GameObject outObject = outPlayer.gameObject;
            outObject.transform.rotation = Quaternion.identity;
            outPlayer.Play(
                outClip,
                loop: false,
                holdLast: false,
                hideOnComplete: true,
                completed: () => DestroyEffectObject(outObject));
        }

        public bool PlayDeath(Action hideBossVisuals, Action completed)
        {
            GridSpriteSheetClip clip = CreateClip(deathSpriteSheet, 5, 9, 45, new Vector2(0.5f, 0.5f));
            if (!clip.IsValid)
            {
                return false;
            }

            StopShield();
            StopShieldBreak();
            StopHorizontalRangeEffects();
            StopMagicCircleImmediate();
            StopAura();
            StopDeathImmediate();
            deathHideNotified = false;

            GridSpriteSheetPlayer deathPlayer = CreateEffectPlayer(
                "LastBossDestroyEffect",
                ResolveBossCenter() + deathOffset,
                ResolveBossSquareSize(deathSizeMultiplier),
                deathSortingOrderOffset);
            if (deathPlayer == null)
            {
                return false;
            }

            deathObject = deathPlayer.gameObject;
            deathPlayer.Play(
                clip,
                loop: false,
                holdLast: false,
                hideOnComplete: true,
                frameChanged: frameIndex =>
                {
                    if (deathHideNotified || frameIndex < bossHideFrameIndex)
                    {
                        return;
                    }

                    deathHideNotified = true;
                    hideBossVisuals?.Invoke();
                },
                completed: () =>
                {
                    if (!deathHideNotified)
                    {
                        deathHideNotified = true;
                        hideBossVisuals?.Invoke();
                    }

                    DestroyEffectObject(deathObject);
                    deathObject = null;
                    completed?.Invoke();
                });
            return true;
        }

        public Vector3 ResolveMagicCirclePosition(Vector2 groundLockPoint, Vector2 rainSpawnPosition)
        {
            Vector2 awayFromGround = rainSpawnPosition - groundLockPoint;
            if (awayFromGround.sqrMagnitude <= 0.0001f)
            {
                awayFromGround = Vector2.up;
            }

            Vector2 position = rainSpawnPosition + awayFromGround.normalized * magicCircleBeyondSpawnDistance;
            return new Vector3(position.x, position.y, transform.position.z);
        }

        private IEnumerator SpawnHorizontalRangeIndicators(IReadOnlyList<Vector2> footPositions)
        {
            GridSpriteSheetClip clip = CreateClip(rangeSpriteSheet, 10, 9, 90, new Vector2(0.5f, 0f));
            if (!clip.IsValid)
            {
                rangeSpawnRoutine = null;
                yield break;
            }

            for (int i = 0; i < footPositions.Count; i++)
            {
                while (rangeIndicators.Count <= i)
                {
                    rangeIndicators.Add(null);
                }

                Vector3 position = new Vector3(footPositions[i].x, footPositions[i].y, transform.position.z) + rangeOffset;
                GridSpriteSheetPlayer player = CreateEffectPlayer(
                    "LastBossRangeIndicator",
                    position,
                    rangeEffectWorldSize,
                    rangeSortingOrderOffset);

                if (player != null)
                {
                    player.SetAlpha(0f);
                    GameObject effectObject = player.gameObject;
                    RangeIndicator indicator = new RangeIndicator(i, effectObject, player);
                    rangeIndicators[i] = indicator;
                    player.Play(
                        clip,
                        loop: true,
                        holdLast: false,
                        hideOnComplete: false);
                    player.FadeToAlpha(1f, rangeFadeInSeconds);

                    if (pendingRangeFadeSlots.Remove(i))
                    {
                        FadeRangeIndicator(indicator);
                    }
                }

                if (i < footPositions.Count - 1 && rangeSpawnInterval > 0f)
                {
                    yield return new WaitForSeconds(rangeSpawnInterval);
                }
            }

            rangeSpawnRoutine = null;
        }

        private void FadeRangeIndicator(RangeIndicator indicator)
        {
            if (indicator == null || indicator.FadingOut || indicator.Player == null)
            {
                return;
            }

            indicator.FadingOut = true;
            indicator.Player.FadeToAlpha(
                0f,
                rangeFadeOutSeconds,
                destroyOnComplete: true,
                completed: () =>
                {
                    if (indicator.SlotIndex >= 0 && indicator.SlotIndex < rangeIndicators.Count)
                    {
                        rangeIndicators[indicator.SlotIndex] = null;
                    }
                });
        }

        private void PlayAuraLoop()
        {
            if (auraObject != null)
            {
                return;
            }

            GridSpriteSheetClip clip = CreateClip(auraSpriteSheet, 5, 6, 30, new Vector2(0.5f, 0.5f));
            if (!clip.IsValid)
            {
                return;
            }

            GridSpriteSheetPlayer player = CreateEffectPlayer(
                "LastBossAuraEffect",
                ResolveBossCenter() + auraOffset,
                ResolveBossSquareSize(auraSizeMultiplier),
                auraSortingOrderOffset);
            if (player == null)
            {
                return;
            }

            auraObject = player.gameObject;
            player.Play(clip, loop: true, holdLast: false, hideOnComplete: false);
        }

        private void StopAura()
        {
            DestroyEffectObject(auraObject);
            auraObject = null;
        }

        private void PlayShieldInThenLoop()
        {
            GridSpriteSheetClip inClip = CreateClip(shieldInSpriteSheet, 5, 2, 10, new Vector2(0.5f, 0.5f));
            GridSpriteSheetClip loopClip = CreateClip(shieldLoopSpriteSheet, 5, 12, 60, new Vector2(0.5f, 0.5f));
            if (!inClip.IsValid || !loopClip.IsValid)
            {
                return;
            }

            StopShield();
            GridSpriteSheetPlayer player = CreateEffectPlayer(
                "LastBossShieldEffect",
                ResolveBossCenter() + shieldOffset,
                ResolveBossSquareSize(shieldSizeMultiplier),
                shieldSortingOrderOffset);
            if (player == null)
            {
                return;
            }

            shieldObject = player.gameObject;
            shieldPlayer = player;
            player.Play(
                inClip,
                loop: false,
                holdLast: false,
                hideOnComplete: false,
                completed: () =>
                {
                    if (shieldPlayer == player && shieldObject != null)
                    {
                        player.Play(loopClip, loop: true, holdLast: false, hideOnComplete: false);
                    }
                });
        }

        private void StopShield()
        {
            DestroyEffectObject(shieldObject);
            shieldObject = null;
            shieldPlayer = null;
        }

        private void PlayShieldBreak()
        {
            GridSpriteSheetClip clip = CreateClip(shieldBreakSpriteSheet, 3, 10, 30, new Vector2(0.5f, 0.5f));
            if (!clip.IsValid)
            {
                return;
            }

            StopShieldBreak();
            GridSpriteSheetPlayer player = CreateEffectPlayer(
                "LastBossShieldBreakEffect",
                ResolveBossCenter() + shieldBreakOffset,
                ResolveBossSquareSize(shieldBreakSizeMultiplier),
                shieldBreakSortingOrderOffset);
            if (player == null)
            {
                return;
            }

            shieldBreakObject = player.gameObject;
            player.Play(
                clip,
                loop: false,
                holdLast: false,
                hideOnComplete: true,
                completed: () =>
                {
                    DestroyEffectObject(shieldBreakObject);
                    shieldBreakObject = null;
                });
        }

        private void StopShieldBreak()
        {
            DestroyEffectObject(shieldBreakObject);
            shieldBreakObject = null;
        }

        private void StopMagicCircleImmediate()
        {
            DestroyEffectObject(magicCircleObject);
            magicCircleObject = null;
            magicCirclePlayer = null;
        }

        private void StopDeathImmediate()
        {
            DestroyEffectObject(deathObject);
            deathObject = null;
        }

        private GridSpriteSheetPlayer CreateEffectPlayer(
            string objectName,
            Vector3 position,
            Vector2 targetWorldSize,
            int sortingOrderOffset)
        {
            CacheComponents();

            GameObject effectObject = new GameObject(objectName);
            effectObject.transform.position = position;

            SpriteRenderer renderer = effectObject.AddComponent<SpriteRenderer>();
            GridSpriteSheetPlayer player = effectObject.AddComponent<GridSpriteSheetPlayer>();
            player.ConfigureRenderer(renderer);
            player.SetTargetWorldSize(targetWorldSize);
            player.ApplyRendererSettings(bossRenderer, sortingOrderOffset, copyBossMaterial);
            return player;
        }

        private GridSpriteSheetClip CreateClip(Texture2D spriteSheet, int columns, int rows, int frameCount, Vector2 pivot)
        {
            return new GridSpriteSheetClip
            {
                SpriteSheet = spriteSheet,
                Columns = columns,
                Rows = rows,
                FrameCount = frameCount,
                FramesPerSecond = framesPerSecond,
                PixelsPerUnit = pixelsPerUnit,
                Pivot = pivot
            };
        }

        private void FollowBoss(GameObject target, Vector3 offset)
        {
            if (target == null)
            {
                return;
            }

            target.transform.position = ResolveBossCenter() + offset;
        }

        private Vector3 ResolveBossCenter()
        {
            Bounds bounds = ResolveBossBounds();
            return new Vector3(bounds.center.x, bounds.center.y, transform.position.z);
        }

        private Vector2 ResolveBossSquareSize(float multiplier)
        {
            Bounds bounds = ResolveBossBounds();
            float size = Mathf.Max(0.1f, Mathf.Max(bounds.size.x, bounds.size.y));
            return Vector2.one * (size * Mathf.Max(0.01f, multiplier));
        }

        private Bounds ResolveBossBounds()
        {
            CacheComponents();
            if (bodyCollider != null)
            {
                return bodyCollider.bounds;
            }

            if (bossRenderer != null)
            {
                return bossRenderer.bounds;
            }

            return new Bounds(transform.position, Vector3.one);
        }

        private void CacheComponents()
        {
            if (bossRenderer == null)
            {
                bossRenderer = GetComponent<SpriteRenderer>();
            }

            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }
        }

        private static void DestroyEffectObject(GameObject effectObject)
        {
            if (effectObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(effectObject);
            }
            else
            {
                DestroyImmediate(effectObject);
            }
        }

        private sealed class RangeIndicator
        {
            public RangeIndicator(int slotIndex, GameObject effectObject, GridSpriteSheetPlayer player)
            {
                SlotIndex = slotIndex;
                EffectObject = effectObject;
                Player = player;
            }

            public int SlotIndex { get; }
            public GameObject EffectObject { get; }
            public GridSpriteSheetPlayer Player { get; }
            public bool FadingOut { get; set; }
        }
    }
}
