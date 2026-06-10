using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameName.Enemy
{
    [DisallowMultipleComponent]
    public sealed class LastBossEffectController : MonoBehaviour
    {
        private static readonly Vector2Int LargeFrameReferencePixels = new Vector2Int(1024, 1024);
        private static readonly Vector2Int RangeFrameReferencePixels = new Vector2Int(512, 512);
        private static readonly RectInt RangeFrameCropPixels = new RectInt(98, 0, 316, 214);
        private static readonly RectInt GroundBladeFrameCropPixels = new RectInt(418, 0, 187, 1009);
        private static readonly RectInt RainBladeFrameCropPixels = new RectInt(405, 20, 217, 995);
        private static readonly RectInt ShieldInFrameCropPixels = new RectInt(97, 97, 830, 830);
        private static readonly RectInt ShieldLoopFrameCropPixels = new RectInt(94, 94, 836, 836);
        private static readonly RectInt DeathFrameCropPixels = new RectInt(20, 20, 984, 984);

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
        [SerializeField, Min(0f)] private float auraFacingPush = 0.35f;
        [SerializeField, Min(0.01f)] private float shieldSizeMultiplier = 1.35f;
        [SerializeField, Min(0.01f)] private float shieldBreakSizeMultiplier = 1.45f;
        [SerializeField, Min(0.01f)] private float deathSizeMultiplier = 1.8f;

        [Header("Attack Placement")]
        [SerializeField] private Vector3 slashOffset;
        [SerializeField, Min(0.01f)] private float slashSizeMultiplier = 1.1f;
        [SerializeField, Min(0.01f)] private float rangeEffectSizeMultiplier = 1f;
        [SerializeField] private Vector2 rangeEffectFrameSizeMultiplier = Vector2.one;
        [SerializeField] private Vector3 rangeOffset;
        [SerializeField, Min(0.01f)] private float magicCircleBeyondSpawnDistance = 1.5f;
        [SerializeField] private Vector2 magicCircleWorldSize = new Vector2(4f, 4f);
        [SerializeField] private Vector2 groundBladeVisualFrameSizeMultiplier = Vector2.one;
        [SerializeField] private Vector2 rainBladeVisualFrameSizeMultiplier = Vector2.one;

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
        private int facingDirection = 1;
        private int magicCircleFacingDirection = 1;
        private float horizontalRangeBladeWorldWidth = 1f;
        private bool shieldBrokenThisDown;
        private bool deathHideNotified;

        public int FacingDirection => facingDirection;
        public int GroundBladeUprightFrameIndex => Mathf.Max(0, groundBladeUprightFrameIndex);
        public Vector2 GroundBladeVisualFrameSizeMultiplier => SanitizeVectorMultiplier(groundBladeVisualFrameSizeMultiplier);
        public Vector2 RainBladeVisualFrameSizeMultiplier => SanitizeVectorMultiplier(rainBladeVisualFrameSizeMultiplier);
        public float RangeEffectSizeMultiplier => Mathf.Max(0.01f, rangeEffectSizeMultiplier);
        public float GroundBladeClipDuration => GroundBladeClip.DurationSeconds;

        public GridSpriteSheetClip GroundBladeClip => CreateClip(underAttackSpriteSheet, 5, 4, 20, new Vector2(0.5f, 0.5f), GroundBladeFrameCropPixels, LargeFrameReferencePixels);
        public GridSpriteSheetClip RainBladeInClip => CreateClip(topAttackInSpriteSheet, 5, 5, 23, new Vector2(0.5f, 0.5f), RainBladeFrameCropPixels, LargeFrameReferencePixels);
        public GridSpriteSheetClip RainBladeOutClip => CreateClip(topAttackOutSpriteSheet, 5, 4, 20, new Vector2(0.5f, 0.5f), RainBladeFrameCropPixels, LargeFrameReferencePixels);

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
            FollowBoss(auraObject, ResolveAuraOffset());
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
            rangeEffectSizeMultiplier = Mathf.Max(0.01f, rangeEffectSizeMultiplier);
            rangeEffectFrameSizeMultiplier = SanitizeVectorMultiplier(rangeEffectFrameSizeMultiplier);
            magicCircleBeyondSpawnDistance = Mathf.Max(0.01f, magicCircleBeyondSpawnDistance);
            groundBladeVisualFrameSizeMultiplier = SanitizeVectorMultiplier(groundBladeVisualFrameSizeMultiplier);
            rainBladeVisualFrameSizeMultiplier = SanitizeVectorMultiplier(rainBladeVisualFrameSizeMultiplier);
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

        public void SetFacingDirection(int direction)
        {
            facingDirection = direction < 0 ? -1 : 1;
            FollowBoss(auraObject, ResolveAuraOffset());
            FollowBoss(shieldObject, shieldOffset);
            FollowBoss(shieldBreakObject, shieldBreakOffset);
            FollowBoss(deathObject, deathOffset);
            ApplyAuraFacing(auraObject);
            ApplyMagicCircleFacing(magicCirclePlayer);
        }

        public void PlayNormalSlash(Vector2 center, Vector2 size, float angle, int facingDirection)
        {
            GridSpriteSheetClip clip = CreateClip(slashSpriteSheet, 5, 6, 30, new Vector2(0.5f, 0.5f));
            if (!clip.IsValid)
            {
                return;
            }

            int slashFacingDirection = facingDirection < 0 ? -1 : 1;
            Vector3 position = new Vector3(center.x, center.y, transform.position.z) +
                               ResolveFacingOffset(slashOffset, slashFacingDirection);
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
                player.Renderer.flipX = slashFacingDirection < 0;
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
            BeginHorizontalRangeCharge(footPositions, 1f);
        }

        public void BeginHorizontalRangeCharge(IReadOnlyList<Vector2> footPositions, float groundBladeWorldWidth)
        {
            StopHorizontalRangeEffects();

            if (footPositions == null || footPositions.Count == 0)
            {
                return;
            }

            horizontalRangeBladeWorldWidth = Mathf.Max(0.1f, groundBladeWorldWidth);
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
            UpdateMagicCircleFacing(groundLockPoint, rainSpawnPosition);
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
            UpdateMagicCircleFacing(groundLockPoint, rainSpawnPosition);
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
            ApplyMagicCircleFacing(outPlayer);
            outPlayer.Play(
                outClip,
                loop: false,
                holdLast: false,
                hideOnComplete: true,
                completed: () => DestroyEffectObject(outObject));
        }

        public bool PlayDeath(Action hideBossVisuals, Action completed)
        {
            GridSpriteSheetClip clip = CreateClip(deathSpriteSheet, 5, 9, 45, new Vector2(0.5f, 0.5f), DeathFrameCropPixels, LargeFrameReferencePixels);
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
                ResolveBossCenter() + ResolveFacingOffset(deathOffset),
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
            GridSpriteSheetClip clip = CreateClip(rangeSpriteSheet, 10, 9, 90, new Vector2(0.5f, 0f), RangeFrameCropPixels, RangeFrameReferencePixels);
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

                Vector3 position = new Vector3(footPositions[i].x, footPositions[i].y, transform.position.z) +
                                   ResolveFacingOffset(rangeOffset);
                GridSpriteSheetPlayer player = CreateEffectPlayer(
                    "LastBossRangeIndicator",
                    position,
                    ResolveRangeIndicatorWorldSize(horizontalRangeBladeWorldWidth),
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
                ResolveBossCenter() + ResolveFacingOffset(ResolveAuraOffset()),
                ResolveBossSquareSize(auraSizeMultiplier),
                auraSortingOrderOffset);
            if (player == null)
            {
                return;
            }

            auraObject = player.gameObject;
            ApplyAuraFacing(auraObject);
            player.Play(clip, loop: true, holdLast: false, hideOnComplete: false);
        }

        private void StopAura()
        {
            DestroyEffectObject(auraObject);
            auraObject = null;
        }

        private void PlayShieldInThenLoop()
        {
            GridSpriteSheetClip inClip = CreateClip(shieldInSpriteSheet, 5, 2, 10, new Vector2(0.5f, 0.5f), ShieldInFrameCropPixels, LargeFrameReferencePixels);
            GridSpriteSheetClip loopClip = CreateClip(
                shieldLoopSpriteSheet,
                5,
                12,
                60,
                new Vector2(0.5f, 0.5f),
                ShieldLoopFrameCropPixels,
                LargeFrameReferencePixels);
            if (!inClip.IsValid || !loopClip.IsValid)
            {
                return;
            }

            StopShield();
            GridSpriteSheetPlayer player = CreateEffectPlayer(
                "LastBossShieldEffect",
                ResolveBossCenter() + ResolveFacingOffset(shieldOffset),
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
                        player.SetTargetWorldSize(ResolveBossSquareSize(shieldSizeMultiplier));
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
                ResolveBossCenter() + ResolveFacingOffset(shieldBreakOffset),
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

        private GridSpriteSheetClip CreateClip(
            Texture2D spriteSheet,
            int columns,
            int rows,
            int frameCount,
            Vector2 pivot,
            int centeredCropInsetPixels = 0)
        {
            return new GridSpriteSheetClip
            {
                SpriteSheet = spriteSheet,
                Columns = columns,
                Rows = rows,
                FrameCount = frameCount,
                FramesPerSecond = framesPerSecond,
                PixelsPerUnit = pixelsPerUnit,
                Pivot = pivot,
                CenteredCropInsetPixels = Mathf.Max(0, centeredCropInsetPixels)
            };
        }

        private GridSpriteSheetClip CreateClip(
            Texture2D spriteSheet,
            int columns,
            int rows,
            int frameCount,
            Vector2 pivot,
            RectInt frameCropPixels,
            Vector2Int frameCropReferencePixels)
        {
            GridSpriteSheetClip clip = CreateClip(spriteSheet, columns, rows, frameCount, pivot, 0);
            clip.UseFrameCrop = true;
            clip.FrameCropPixels = frameCropPixels;
            clip.FrameCropReferencePixels = frameCropReferencePixels;
            return clip;
        }

        private void FollowBoss(GameObject target, Vector3 offset)
        {
            if (target == null)
            {
                return;
            }

            target.transform.position = ResolveBossCenter() + ResolveFacingOffset(offset);
        }

        public Vector3 ResolveFacingOffset(Vector3 localOffset)
        {
            return ResolveFacingOffset(localOffset, facingDirection);
        }

        public Vector2 ResolveRangeIndicatorWorldSize(float groundBladeWorldWidth)
        {
            float width = Mathf.Max(0.1f, groundBladeWorldWidth) * RangeEffectSizeMultiplier;
            Vector2 frameMultiplier = SanitizeVectorMultiplier(rangeEffectFrameSizeMultiplier);
            float croppedAspect = (float)RangeFrameCropPixels.height / Mathf.Max(1, RangeFrameCropPixels.width);
            return new Vector2(width * frameMultiplier.x, width * croppedAspect * frameMultiplier.y);
        }

        private Vector3 ResolveAuraOffset()
        {
            return auraOffset + new Vector3(Mathf.Max(0f, auraFacingPush), 0f, 0f);
        }

        private static Vector3 ResolveFacingOffset(Vector3 localOffset, int direction)
        {
            int normalizedDirection = direction < 0 ? -1 : 1;
            return new Vector3(localOffset.x * normalizedDirection, localOffset.y, localOffset.z);
        }

        private static Vector2 SanitizeVectorMultiplier(Vector2 multiplier)
        {
            return new Vector2(
                Mathf.Max(0.01f, multiplier.x),
                Mathf.Max(0.01f, multiplier.y));
        }

        private void ApplyAuraFacing(GameObject target)
        {
            GridSpriteSheetPlayer player = target != null ? target.GetComponent<GridSpriteSheetPlayer>() : null;
            if (player?.Renderer != null)
            {
                player.Renderer.flipX = facingDirection < 0;
            }
        }

        private void ApplyMagicCircleFacing(GridSpriteSheetPlayer player)
        {
            if (player?.Renderer != null)
            {
                player.Renderer.flipX = magicCircleFacingDirection > 0;
            }
        }

        private void UpdateMagicCircleFacing(Vector2 groundLockPoint, Vector2 rainSpawnPosition)
        {
            float horizontalDirection = groundLockPoint.x - rainSpawnPosition.x;
            magicCircleFacingDirection = Mathf.Abs(horizontalDirection) > 0.001f
                ? (horizontalDirection > 0f ? 1 : -1)
                : facingDirection;
            ApplyMagicCircleFacing(magicCirclePlayer);
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
            if (TryGetUsableBounds(bodyCollider, out Bounds colliderBounds))
            {
                return colliderBounds;
            }

            if (TryGetUsableBounds(bossRenderer, out Bounds rendererBounds))
            {
                return rendererBounds;
            }

            return new Bounds(transform.position, Vector3.one);
        }

        private static bool TryGetUsableBounds(Collider2D collider, out Bounds bounds)
        {
            bounds = default;
            if (collider == null || !collider.enabled)
            {
                return false;
            }

            bounds = collider.bounds;
            return HasUsableSize(bounds);
        }

        private static bool TryGetUsableBounds(Renderer renderer, out Bounds bounds)
        {
            bounds = default;
            if (renderer == null)
            {
                return false;
            }

            bounds = renderer.bounds;
            return HasUsableSize(bounds);
        }

        private static bool HasUsableSize(Bounds bounds)
        {
            Vector3 size = bounds.size;
            return size.x > 0.001f || size.y > 0.001f || size.z > 0.001f;
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
