using System.Collections;
using System.Collections.Generic;
using Metroidvania.Player;
using Player;
using UnityEngine;

namespace GameName.Enemy
{
    /// <summary>
    /// LastBoss専用の戦闘制御。
    /// BossAreaControllerから起動され、移動、攻撃選択、範囲攻撃、ジャストパリィ、ダウンをまとめて扱う。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class LastBossController : MonoBehaviour, IAttackReceiver, IBossHealthSource
    {
        [Header("Activation")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField, Min(0f)] private float initialActionDelay = 2f;
        [SerializeField] private bool startActiveOnPlay;

        [Header("Stats")]
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField, Min(1f)] private float backAttackDamageMultiplier = 2f;
        [SerializeField, Min(0f)] private float moveSpeed = 8f;
        [SerializeField, Range(0.01f, 1f)] private float enrageHealthRate = 0.4f;
        [SerializeField, Min(1f)] private float enragedAttackMultiplier = 1.1f;
        [Header("Action Selection")]
        [SerializeField, Min(0f)] private float closeRangeDistance = 12f;
        [SerializeField, Min(0f)] private float farRangeDistance = 13f;
        [SerializeField, Range(0f, 1f)] private float thirdNormalChanceAfterSecondNormal = 0.6f;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float normalApproachStartDistance = 5f;
        [SerializeField, Min(0f)] private float normalStopDistance = 3f;
        [SerializeField, Min(0f)] private float longDistanceMoveStart = 16f;
        [SerializeField, Min(0f)] private float minimumPlayerDistance = 3f;

        [Header("Normal Attack")]
        [SerializeField, Min(1)] private int normalAttackDamage = 10;
        [SerializeField] private Vector2 normalAttackSize = new Vector2(4f, 4f);
        [SerializeField, Min(0f)] private float normalAttackForwardInset = 2f;
        [SerializeField, Min(0.01f)] private float normalAttackVisibleTime = 0.16f;
        [SerializeField, Min(0f)] private float normalAttackRecovery = 0.45f;

        [Header("Horizontal Range Attack")]
        [SerializeField, Min(1)] private int horizontalAttackDamage = 15;
        [SerializeField] private Vector2 horizontalAttackSize = new Vector2(10f, 4f);
        [SerializeField, Min(0f)] private float horizontalAttackCooldown = 10f;
        [SerializeField, Min(0f)] private float horizontalTelegraphTime = 2f;
        [SerializeField, Min(0.01f)] private float horizontalAttackVisibleTime = 0.22f;
        [SerializeField, Min(0f)] private float horizontalAttackRecovery = 0.6f;
        // 横範囲攻撃は即時判定ではなく、GroundBlade prefabをボス側から順に生成して見せる。
        [SerializeField] private GameObject groundBladePrefab;
        [SerializeField, Min(0.1f)] private float horizontalGroundBladeSweepSpeed = 5f;

        [Header("Vertical Range Attack")]
        [SerializeField, Min(1)] private int verticalAttackDamage = 20;
        [SerializeField, Min(0.1f)] private float verticalAttackWidth = 2f;
        [SerializeField, Min(0f)] private float verticalAttackStartHeight = 6f;
        [SerializeField, Min(0f)] private float verticalAttackCooldown = 18f;
        [SerializeField, Min(0f)] private float verticalTelegraphTime = 2f;
        [SerializeField, Min(0f)] private float verticalTargetLockBeforeAttack = 0.5f;
        [SerializeField, Min(0.01f)] private float verticalAttackVisibleTime = 0.24f;
        [SerializeField, Min(0f)] private float verticalAttackRecovery = 0.7f;
        // 縦範囲攻撃は予兆中にRainBladeを上端へ待機させ、攻撃開始後に継続して降らせる。
        [SerializeField] private GameObject rainBladePrefab;
        [SerializeField, Min(1)] private int verticalRainBladeCount = 3;
        [SerializeField, Min(0f)] private float verticalRainPreviewSpawnInterval = 0.06f;
        [SerializeField, Min(0.01f)] private float verticalRainDuration = 2f;
        [SerializeField, Min(0.01f)] private float verticalRainBladeInterval = 0.4f;
        [SerializeField, Min(0.01f)] private float rainBladeFallSpeed = 8f;
        [SerializeField, Min(0f)] private float rainBladeGroundDestroyDelay = 0.3f;
        // パリィで中断された時だけ、この時間を使って発生中のブレードをフェードアウトする。
        [SerializeField, Min(0f)] private float bladeParryFadeDuration = 0.2f;

        [Header("Down")]
        [SerializeField, Min(1)] private int downCountThreshold = 20;
        [SerializeField, Min(0f)] private float downDuration = 10f;
        [SerializeField, Min(0f)] private float hitStopDuration = 0.08f;
        [SerializeField] private bool useGlobalHitStop = true;

        [Header("Just Parry")]
        [SerializeField, Min(0f)] private float justParryEffectDuration = 0.5f;

        [Header("Detection")]
        [SerializeField] private LayerMask playerDetectionMask;

        [Header("Visuals")]
        [SerializeField] private Color telegraphColor = new Color(1f, 0f, 0f, 0.35f);
        [SerializeField] private Color attackColor = new Color(1f, 0.15f, 0.05f, 0.55f);
        [SerializeField] private Color enragedColor = new Color(0.6f, 0f, 0f, 1f);
        [SerializeField, Min(0f)] private float enragedPulseSpeed = 8f;
        [SerializeField] private Color hitFlashColor = Color.red;
        [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.14f;
        [SerializeField, Min(1)] private int hitFlashRepeatCount = 2;
        [SerializeField, Min(0f)] private float hitFlashNormalDuration = 0.05f;
        [SerializeField] private LastBossSpriteAnimator spriteView;
        [SerializeField] private bool drawDebugGizmos = true;

        // Update内の状態遷移を明示するための簡易ステート。
        // ダウン中は攻撃更新を止め、赤い攻撃範囲も必ず非表示にする。
        private enum BossState
        {
            Inactive,
            InitialDelay,
            MovingToRange,
            Telegraphing,
            AttackVisible,
            Recovery,
            Downed,
            Dead
        }

        // 次に実行する攻撃種別。Noneは予約攻撃がない状態を表す。
        private enum BossAction
        {
            None,
            Normal,
            Horizontal,
            Vertical
        }

        private const float BackAttackMinHorizontalDelta = 0.05f;

        private readonly Collider2D[] playerHits = new Collider2D[16];
        // 発生中のブレードを、パリィ・死亡・非アクティブ化でまとめて止めるために保持する。
        private readonly List<LastBossBladeAttack> activeBladeAttacks = new List<LastBossBladeAttack>();
        // 予兆中に上端で待機しているRainBlade。攻撃開始後はここから順番に落とす。
        private readonly List<LastBossBladeAttack> preparedRainBlades = new List<LastBossBladeAttack>();

        private Rigidbody2D rb2D;
        private Collider2D bodyCollider;
        private SpriteRenderer spriteRenderer;
        private LastBossEffectController effectController;
        private Transform playerTransform;
        private UmbrellaParryController playerParryController;
        private ParryHitbox playerParryHitbox;
        private ContactFilter2D playerContactFilter;
        private Sprite runtimeBoxSprite;
        private GameObject telegraphObject;
        private SpriteRenderer telegraphRenderer;
        private BoxCollider2D telegraphCollider;

        private BossState state = BossState.Inactive;
        private BossAction pendingAction = BossAction.None;
        private BossAction previousAction = BossAction.None;
        private BossAction visibleAction = BossAction.None;
        private BossAction lastDebugAction = BossAction.None;
        private AttackBox activeAttackBox;
        private Coroutine activeBladeAttackRoutine;
        private Coroutine verticalRainPreviewRoutine;
        private int currentHealth;
        private int facingDirection = 1;
        private int normalChainCount;
        private int downCount;
        private bool encounterActive;
        private bool enraged;
        private bool downRoutineRunning;
        private bool hitStopTimeScaleActive;
        private bool prefabAttackRunning;
        private bool horizontalBladeDamageDealt;
        private bool verticalBladeDamageDealt;
        private bool rangeParryProxyActive;
        private bool deathRoutineRunning;
        private float stateTimer;
        private float horizontalReadyTime;
        private float verticalReadyTime;
        private float hitStopRestoreTimeScale = 1f;
        private float hitFlashStartTime = -1f;
        private float hitFlashEndTime = -1f;
        // 予兆中に成立したジャストパリィを、攻撃発生まで短時間だけ保持する。
        private float justParryValidUntil = -1f;
        private BossAction justParryBufferedAction = BossAction.None;
        private Color defaultSpriteColor = Color.white;

        public bool IsEncounterActive => encounterActive;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => Mathf.Max(1, maxHealth);
        private static bool UseLegacyBossHitStop => false;
        /// <summary>
        /// LastBossがDestroyされる直前に通知する。専用死亡SEの再生に使う。
        /// System.Actionを直接書き、UnityEngine.Randomとの名前衝突を避ける。
        /// </summary>
        public event System.Action Died;
        public event System.Action<int, int> HealthChanged;

        private void Awake()
        {
            rb2D = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            ResolveSpriteView();
            spriteRenderer = GetComponent<SpriteRenderer>();
            effectController = GetComponent<LastBossEffectController>();
            InitializeFacingDirectionFromSprite();
            effectController?.SetFacingDirection(facingDirection);
            currentHealth = MaxHealth;

            SpriteRenderer mainRenderer = GetMainSpriteRenderer();
            if (mainRenderer != null)
            {
                defaultSpriteColor = mainRenderer.color;
            }

            if (playerDetectionMask.value == 0)
            {
                playerDetectionMask = PlayerBodyColliderUtility.GetPlayerBodyLayerMask();
            }

            BuildPlayerContactFilter();
            ConfigureRigidbody();
            EnsureVisualObjects();
        }

        private void InitializeFacingDirectionFromSprite()
        {
            if (spriteRenderer != null)
            {
                facingDirection = spriteRenderer.flipX ? 1 : -1;
            }
        }

        private void Start()
        {
            CachePlayerReferences();

            if (startActiveOnPlay)
            {
                ActivateEncounter();
            }
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            backAttackDamageMultiplier = Mathf.Max(1f, backAttackDamageMultiplier);
            normalAttackSize.x = Mathf.Max(0.1f, normalAttackSize.x);
            normalAttackSize.y = Mathf.Max(0.1f, normalAttackSize.y);
            normalAttackForwardInset = Mathf.Max(0f, normalAttackForwardInset);
            horizontalAttackSize.x = Mathf.Max(0.1f, horizontalAttackSize.x);
            horizontalAttackSize.y = Mathf.Max(0.1f, horizontalAttackSize.y);
            horizontalGroundBladeSweepSpeed = Mathf.Max(0.1f, horizontalGroundBladeSweepSpeed);
            verticalAttackWidth = Mathf.Max(0.1f, verticalAttackWidth);
            verticalTargetLockBeforeAttack = Mathf.Max(0f, verticalTargetLockBeforeAttack);
            verticalRainBladeCount = Mathf.Max(1, verticalRainBladeCount);
            verticalRainPreviewSpawnInterval = Mathf.Max(0f, verticalRainPreviewSpawnInterval);
            verticalRainDuration = Mathf.Max(0.01f, verticalRainDuration);
            verticalRainBladeInterval = Mathf.Max(0.01f, verticalRainBladeInterval);
            rainBladeFallSpeed = Mathf.Max(0.01f, rainBladeFallSpeed);
            rainBladeGroundDestroyDelay = Mathf.Max(0f, rainBladeGroundDestroyDelay);
            bladeParryFadeDuration = Mathf.Max(0f, bladeParryFadeDuration);
            justParryEffectDuration = Mathf.Max(0f, justParryEffectDuration);
            hitFlashDuration = Mathf.Max(0.01f, hitFlashDuration);
            hitFlashRepeatCount = Mathf.Max(1, hitFlashRepeatCount);
            hitFlashNormalDuration = Mathf.Max(0f, hitFlashNormalDuration);
            BuildPlayerContactFilter();
        }

        private void OnDisable()
        {
            CancelActiveBladeAttack();
            effectController?.HandleEncounterStopped();
            StopMotion();
            HideAttackVisual();
            encounterActive = false;
            state = BossState.Inactive;
        }

        private void OnDestroy()
        {
            CancelActiveBladeAttack();

            if (telegraphObject != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(telegraphObject);
                }
                else
                {
                    DestroyImmediate(telegraphObject);
                }
            }
        }

        private void Update()
        {
            UpdateEnragedVisual();

            if (!encounterActive || state == BossState.Dead)
            {
                return;
            }

            if (downRoutineRunning)
            {
                // ヒットストップ中もUpdateは走るため、ダウン開始後は攻撃更新を完全に止める。
                StopMotion();
                HideAttackVisual();
                return;
            }

            CachePlayerReferences();
            if (CanTurnTowardPlayer())
            {
                FacePlayer();
            }

            switch (state)
            {
                case BossState.InitialDelay:
                    UpdateInitialDelay();
                    break;
                case BossState.MovingToRange:
                    UpdateMovingToRange();
                    break;
                case BossState.Telegraphing:
                    UpdateTelegraphing();
                    break;
                case BossState.AttackVisible:
                    UpdateAttackVisible();
                    break;
                case BossState.Recovery:
                    UpdateRecovery();
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (!encounterActive || state == BossState.Dead)
            {
                return;
            }

            if (state == BossState.MovingToRange)
            {
                MoveForPendingAction();
                return;
            }

            StopMotion();
        }

        public void ActivateEncounter()
        {
            if (state == BossState.Dead)
            {
                return;
            }

            CancelActiveBladeAttack();
            CachePlayerReferences();
            encounterActive = true;
            deathRoutineRunning = false;
            pendingAction = BossAction.None;
            // BossAreaに入ってすぐ攻撃せず、調整可能な待ち時間後に初回行動を始める。
            stateTimer = initialActionDelay;
            state = BossState.InitialDelay;
            StopMotion();
            spriteView?.PlayIdle();
            HideAttackVisual();
            SetBossRenderersEnabled(true);
            RestoreCombatBodyAfterReset();
            effectController?.SetFacingDirection(facingDirection);
            effectController?.HandleEncounterStarted();
        }

        public void DeactivateEncounter()
        {
            CancelActiveBladeAttack();
            effectController?.HandleEncounterStopped();
            encounterActive = false;
            state = BossState.Inactive;
            pendingAction = BossAction.None;
            visibleAction = BossAction.None;
            ClearJustParryBuffer();
            StopMotion();
            spriteView?.PlayIdle();
            HideAttackVisual();
        }

        public void OnAttacked(AttackHitbox attacker, Collider2D hitCollider)
        {
            // プレイヤー通常攻撃から呼ばれる被弾口。背後ヒットならダウンカウントを多めに加算する。
            if (state == BossState.Dead || currentHealth <= 0)
            {
                return;
            }

            bool isBackAttack = IsBackAttack(attacker);
            int damage = CalculatePlayerAttackDamage(attacker, isBackAttack);
            if (damage <= 0)
            {
                return;
            }

            bool wasAlive = currentHealth > 0;

            PlayHitFlash();
            currentHealth = Mathf.Max(0, currentHealth - damage);
            NotifyHealthChanged();
            HitStopController.RequestPlayerToEnemy();
            TryEnterEnraged();

            if (currentHealth <= 0)
            {
                if (wasAlive && attacker != null)
                {
                    PlayerEquipmentController equipmentController =
                        attacker.GetComponentInParent<PlayerEquipmentController>();
                    equipmentController?.NotifyEnemyKilledByPlayerAttack();
                }

                Die();
                return;
            }

            AddDownCount(isBackAttack ? 2 : 1);
        }

        private void UpdateInitialDelay()
        {
            StopMotion();
            stateTimer -= Time.deltaTime;

            if (stateTimer <= 0f)
            {
                StartNextAction();
            }
        }

        private void UpdateMovingToRange()
        {
            if (pendingAction == BossAction.None)
            {
                StartNextAction();
                return;
            }

            if (IsPlayerInRangeForAction(pendingAction))
            {
                BeginAction(pendingAction);
            }
        }

        private void UpdateTelegraphing()
        {
            StopMotion();
            if (downRoutineRunning || pendingAction == BossAction.None)
            {
                HideAttackVisual();
                return;
            }

            if (pendingAction == BossAction.Horizontal || pendingAction == BossAction.Vertical)
            {
                // Prefab range attacks are parried by the blade colliders only.
                HideAttackVisual();
                if (pendingAction == BossAction.Vertical)
                {
                    UpdateVerticalAttackTracking();
                }

                HideRangeParryProxy();

                stateTimer -= Time.deltaTime;
                if (stateTimer > 0f)
                {
                    return;
                }

                BeginPrefabRangeAttack(pendingAction, activeAttackBox);
                return;
            }

            if (pendingAction != BossAction.Vertical)
            {
                // 横範囲は予兆中もプレイヤーXへ追従する。縦範囲はBeginAction時点の位置で固定する。
                activeAttackBox = BuildAttackBox(pendingAction);
            }

            ShowAttackVisual(activeAttackBox, telegraphColor);
            // 予兆中のパリィを短時間記録し、攻撃発生の1フレームだけに依存しないようにする。
            UpdateJustParryBuffer(pendingAction, activeAttackBox);

            stateTimer -= Time.deltaTime;
            if (stateTimer > 0f)
            {
                return;
            }

            bool downStarted = ResolveAttack(pendingAction, activeAttackBox);
            if (downStarted)
            {
                HideAttackVisual();
                return;
            }

            visibleAction = pendingAction;
            stateTimer = GetAttackVisibleTime(pendingAction);
            state = BossState.AttackVisible;
            ShowAttackVisual(activeAttackBox, attackColor);
        }

        private void UpdateAttackVisible()
        {
            StopMotion();
            if (prefabAttackRunning)
            {
                return;
            }

            stateTimer -= Time.deltaTime;

            if (stateTimer > 0f)
            {
                return;
            }

            HideAttackVisual();
            stateTimer = GetRecoveryTime(visibleAction);
            state = BossState.Recovery;
            spriteView?.PlayIdle();
        }

        private void UpdateRecovery()
        {
            StopMotion();
            stateTimer -= Time.deltaTime;

            if (stateTimer <= 0f)
            {
                StartNextAction();
            }
        }

        private void StartNextAction()
        {
            if (!encounterActive || state == BossState.Downed || state == BossState.Dead)
            {
                return;
            }

            pendingAction = ChooseNextAction();
            lastDebugAction = pendingAction;

            if (!IsPlayerInRangeForAction(pendingAction))
            {
                state = BossState.MovingToRange;
                return;
            }

            BeginAction(pendingAction);
        }

        private BossAction ChooseNextAction()
        {
            // 範囲攻撃の直後は通常攻撃に戻し、通常攻撃2回後から距離に応じて範囲攻撃を選ぶ。
            if (previousAction == BossAction.Horizontal || previousAction == BossAction.Vertical)
            {
                return BossAction.Normal;
            }

            if (previousAction != BossAction.Normal || normalChainCount <= 0)
            {
                return BossAction.Normal;
            }

            if (normalChainCount == 1)
            {
                return BossAction.Normal;
            }

            if (normalChainCount == 2 && Random.value < thirdNormalChanceAfterSecondNormal)
            {
                return BossAction.Normal;
            }

            return ChooseRangeActionByDistance();
        }

        private BossAction ChooseRangeActionByDistance()
        {
            float distance = GetHorizontalPlayerDistance();
            BossAction preferred = distance <= closeRangeDistance ? BossAction.Horizontal : BossAction.Vertical;
            if (distance >= farRangeDistance || distance >= longDistanceMoveStart)
            {
                preferred = BossAction.Vertical;
            }

            if (IsActionReady(preferred))
            {
                return preferred;
            }

            BossAction fallback = preferred == BossAction.Horizontal ? BossAction.Vertical : BossAction.Horizontal;
            return IsActionReady(fallback) ? fallback : BossAction.Normal;
        }

        private bool IsActionReady(BossAction action)
        {
            return action switch
            {
                BossAction.Horizontal => Time.time >= horizontalReadyTime,
                BossAction.Vertical => Time.time >= verticalReadyTime,
                _ => true
            };
        }

        private void BeginAction(BossAction action)
        {
            StopMotion();
            // 攻撃開始時点で向きと攻撃範囲を確定する。攻撃中はプレイヤーが回り込んでも振り向かない。
            FacePlayer();
            ClearJustParryBuffer();
            activeAttackBox = BuildAttackBox(action);

            if (action == BossAction.Horizontal || action == BossAction.Vertical)
            {
                stateTimer = action == BossAction.Horizontal ? horizontalTelegraphTime : verticalTelegraphTime;
                state = BossState.Telegraphing;
                HideAttackVisual();
                PlayRangeTelegraphAnimation(action);

                if (action == BossAction.Horizontal)
                {
                    BeginHorizontalRangeCharge(activeAttackBox);
                }
                else if (action == BossAction.Vertical)
                {
                    StartVerticalRainPreview();
                    BeginVerticalMagicCircle(activeAttackBox);
                }

                return;
            }

            spriteView?.PlayNormalAttack();

            bool downStarted = ResolveAttack(action, activeAttackBox);
            if (downStarted)
            {
                HideAttackVisual();
                return;
            }

            visibleAction = action;
            stateTimer = GetAttackVisibleTime(action);
            state = BossState.AttackVisible;
            ShowAttackVisual(activeAttackBox, attackColor);
            effectController?.PlayNormalSlash(
                activeAttackBox.Center,
                activeAttackBox.Size,
                activeAttackBox.Angle,
                facingDirection);
        }

        // 戻り値は「この攻撃解決でダウンが開始したか」。trueなら攻撃表示へ進めない。
        private bool ResolveAttack(BossAction action, AttackBox attackBox)
        {
            if (action == BossAction.Horizontal)
            {
                horizontalReadyTime = Time.time + horizontalAttackCooldown;
            }
            else if (action == BossAction.Vertical)
            {
                verticalReadyTime = Time.time + verticalAttackCooldown;
            }

            bool parried = IsRangeAttackParried(action, attackBox);
            if (parried)
            {
                if (AddDownCount(action == BossAction.Horizontal ? 7 : 15))
                {
                    normalChainCount = 0;
                    previousAction = action;
                    ClearJustParryBuffer();
                    return true;
                }
            }
            else
            {
                ApplyDamageToPlayersInBox(action, attackBox);
            }

            if (action == BossAction.Normal)
            {
                normalChainCount++;
            }
            else
            {
                normalChainCount = 0;
            }

            previousAction = action;
            ClearJustParryBuffer();
            return false;
        }

        private void BeginPrefabRangeAttack(BossAction action, AttackBox attackBox)
        {
            if (action != BossAction.Horizontal && action != BossAction.Vertical)
            {
                return;
            }

            // 発生から終了までをブレード用コルーチンに任せるため、通常の赤箱表示フローから切り離す。
            HideAttackVisual();
            SetRangeActionCooldown(action);
            MarkActionResolved(action);
            visibleAction = action;
            state = BossState.AttackVisible;
            prefabAttackRunning = true;
            HideRangeParryProxy();
            PlayAttackReleaseAnimation(action);

            if (activeBladeAttackRoutine != null)
            {
                StopCoroutine(activeBladeAttackRoutine);
                activeBladeAttackRoutine = null;
            }

            if (action == BossAction.Horizontal)
            {
                horizontalBladeDamageDealt = false;
                activeBladeAttackRoutine = StartCoroutine(HorizontalGroundBladeAttackRoutine(attackBox));
                return;
            }

            verticalBladeDamageDealt = false;
            activeBladeAttackRoutine = StartCoroutine(VerticalRainBladeAttackRoutine());
        }

        private void UpdateVerticalAttackTracking()
        {
            if (stateTimer <= Mathf.Max(0f, verticalTargetLockBeforeAttack))
            {
                return;
            }

            // 縦範囲の狙いは攻撃開始直前までプレイヤーを追い、指定秒数前に固定する。
            activeAttackBox = BuildAttackBox(BossAction.Vertical);
            UpdateQueuedRainBladePreviews(activeAttackBox);
            UpdateVerticalMagicCircle(activeAttackBox);
        }

        private IEnumerator HorizontalGroundBladeAttackRoutine(AttackBox attackBox)
        {
            if (groundBladePrefab == null)
            {
                Debug.LogWarning("LastBoss horizontal attack needs a GroundBlade prefab.", this);
                FinishPrefabRangeAttack(BossAction.Horizontal);
                yield break;
            }

            float spacing = ResolveGroundBladePrefabWidth();
            float sweepSpeed = Mathf.Max(0.1f, horizontalGroundBladeSweepSpeed);
            int bladeCount = Mathf.Max(1, Mathf.FloorToInt(attackBox.Size.x / spacing));
            float slotWidth = attackBox.Size.x / bladeCount;
            float spawnInterval = slotWidth / sweepSpeed;
            float nearEdgeX = attackBox.Center.x - facingDirection * (attackBox.Size.x * 0.5f);
            float groundY = attackBox.Center.y - attackBox.Size.y * 0.5f;
            GridSpriteSheetClip groundVisualClip = effectController != null
                ? effectController.GroundBladeClip
                : default;
            bool hasGroundVisual = groundVisualClip.IsValid;
            float groundVisualDuration = hasGroundVisual ? groundVisualClip.DurationSeconds : 0f;

            // GroundBladeのscaleは触らず、prefabの実幅を使ってボス側から順に敷き詰める。
            for (int i = 0; i < bladeCount; i++)
            {
                float distance = slotWidth * (i + 0.5f);
                Vector2 spawnPosition = new Vector2(
                    nearEdgeX + facingDirection * distance,
                    groundY);

                LastBossBladeAttack blade = SpawnBlade(groundBladePrefab, spawnPosition, Quaternion.identity);
                if (blade != null)
                {
                    if (hasGroundVisual)
                    {
                        blade.ConfigureGroundVisual(
                            groundVisualClip,
                            effectController.GroundBladeUprightFrameIndex,
                            i,
                            effectController.GroundBladeVisualFrameSizeMultiplier);
                    }

                    blade.InitializeGround(
                        this,
                        GetAttackDamage(BossAction.Horizontal),
                        groundY,
                        horizontalAttackVisibleTime);
                }

                if (i < bladeCount - 1)
                {
                    yield return new WaitForSeconds(spawnInterval);
                }
            }

            float cleanupDelay = Mathf.Max(Mathf.Max(0.01f, horizontalAttackVisibleTime * 2f), groundVisualDuration);
            yield return new WaitForSeconds(cleanupDelay);

            ClearBladesOfKind(LastBossBladeAttack.BladeKind.Ground);
            FinishPrefabRangeAttack(BossAction.Horizontal);
        }

        private IEnumerator VerticalRainBladeAttackRoutine()
        {
            if (verticalRainPreviewRoutine != null)
            {
                StopCoroutine(verticalRainPreviewRoutine);
                verticalRainPreviewRoutine = null;
            }

            EnsurePreparedRainBladeQueue(activeAttackBox);

            if (preparedRainBlades.Count == 0)
            {
                Debug.LogWarning("LastBoss vertical attack has no prepared RainBlades.", this);
                FinishPrefabRangeAttack(BossAction.Vertical);
                yield break;
            }

            float endTime = Time.time + Mathf.Max(0.01f, verticalRainDuration);
            float releaseInterval = Mathf.Max(0.01f, verticalRainBladeInterval);
            int nextSpawnSlot = preparedRainBlades.Count;

            // 待機ブレードを1本落とすたびに上端へ補充し、duration中だけ雨を継続する。
            while (Time.time < endTime)
            {
                if (preparedRainBlades.Count <= 0)
                {
                    LastBossBladeAttack replacement = SpawnRainPreviewBlade(activeAttackBox, nextSpawnSlot);
                    nextSpawnSlot++;
                    if (replacement != null)
                    {
                        preparedRainBlades.Add(replacement);
                    }
                }

                LastBossBladeAttack blade = preparedRainBlades[0];
                preparedRainBlades.RemoveAt(0);

                if (blade != null)
                {
                    blade.ReleaseRainBlade();
                }

                if (Time.time < endTime)
                {
                    LastBossBladeAttack replacement = SpawnRainPreviewBlade(activeAttackBox, nextSpawnSlot);
                    nextSpawnSlot++;
                    if (replacement != null)
                    {
                        preparedRainBlades.Add(replacement);
                    }
                }

                yield return new WaitForSeconds(releaseInterval);
            }

            ClearQueuedRainBlades();

            while (HasLiveBlade(LastBossBladeAttack.BladeKind.Rain))
            {
                yield return null;
            }

            preparedRainBlades.Clear();
            FinishPrefabRangeAttack(BossAction.Vertical);
        }

        private void StartVerticalRainPreview()
        {
            if (verticalRainPreviewRoutine != null)
            {
                StopCoroutine(verticalRainPreviewRoutine);
                verticalRainPreviewRoutine = null;
            }

            ClearBladesOfKind(LastBossBladeAttack.BladeKind.Rain);
            preparedRainBlades.Clear();

            if (rainBladePrefab == null)
            {
                Debug.LogWarning("LastBoss vertical attack needs a RainBlade prefab.", this);
                return;
            }

            // 予兆開始時に上端へ見せブレードを素早く並べ、攻撃開始まで待機させる。
            verticalRainPreviewRoutine = StartCoroutine(VerticalRainPreviewRoutine());
        }

        private IEnumerator VerticalRainPreviewRoutine()
        {
            int bladeCount = Mathf.Max(1, verticalRainBladeCount);
            float spawnInterval = ResolveVerticalRainPreviewSpawnInterval(bladeCount);

            for (int i = 0; i < bladeCount; i++)
            {
                LastBossBladeAttack blade = SpawnRainPreviewBlade(activeAttackBox, i);
                if (blade != null)
                {
                    preparedRainBlades.Add(blade);
                }

                if (i < bladeCount - 1 && spawnInterval > 0f)
                {
                    yield return new WaitForSeconds(spawnInterval);
                }
            }

            verticalRainPreviewRoutine = null;
        }

        private float ResolveVerticalRainPreviewSpawnInterval(int bladeCount)
        {
            if (bladeCount <= 1)
            {
                return 0f;
            }

            float configuredInterval = Mathf.Max(0f, verticalRainPreviewSpawnInterval);
            float maxInterval = Mathf.Max(0f, verticalTelegraphTime - 0.01f) / (bladeCount - 1);
            return Mathf.Min(configuredInterval, maxInterval);
        }

        private LastBossBladeAttack SpawnRainPreviewBlade(AttackBox attackBox, int slotIndex)
        {
            if (rainBladePrefab == null)
            {
                return null;
            }

            GetRainBladeSlotPoints(
                attackBox,
                slotIndex,
                out Vector2 spawnPosition,
                out Vector2 targetPoint,
                out Vector2 previewAimPoint);
            LastBossBladeAttack blade = SpawnBlade(rainBladePrefab, spawnPosition, Quaternion.identity);
            if (blade != null)
            {
                if (effectController != null)
                {
                    GridSpriteSheetClip rainInClip = effectController.RainBladeInClip;
                    GridSpriteSheetClip rainOutClip = effectController.RainBladeOutClip;
                    if (rainInClip.IsValid && rainOutClip.IsValid)
                    {
                        blade.ConfigureRainVisual(
                            rainInClip,
                            rainOutClip,
                            effectController.RainBladeVisualFrameSizeMultiplier);
                    }
                }

                blade.InitializeRainPreview(
                    this,
                    GetAttackDamage(BossAction.Vertical),
                    targetPoint,
                    previewAimPoint,
                    rainBladeFallSpeed,
                    rainBladeGroundDestroyDelay);
            }

            return blade;
        }

        private void EnsurePreparedRainBladeQueue(AttackBox attackBox)
        {
            for (int i = preparedRainBlades.Count - 1; i >= 0; i--)
            {
                if (preparedRainBlades[i] == null)
                {
                    preparedRainBlades.RemoveAt(i);
                }
            }

            int bladeCount = Mathf.Max(1, verticalRainBladeCount);
            for (int i = preparedRainBlades.Count; i < bladeCount; i++)
            {
                LastBossBladeAttack blade = SpawnRainPreviewBlade(attackBox, i);
                if (blade != null)
                {
                    preparedRainBlades.Add(blade);
                }
            }

            UpdateQueuedRainBladePreviews(attackBox);
        }

        private void UpdateQueuedRainBladePreviews(AttackBox attackBox)
        {
            for (int i = 0; i < preparedRainBlades.Count; i++)
            {
                LastBossBladeAttack blade = preparedRainBlades[i];
                if (blade == null)
                {
                    continue;
                }

                GetRainBladeSlotPoints(
                    attackBox,
                    i,
                    out Vector2 spawnPosition,
                    out Vector2 targetPoint,
                    out Vector2 previewAimPoint);
                blade.UpdateRainPreview(spawnPosition, targetPoint, previewAimPoint);
            }
        }

        private void GetRainBladeSlotPoints(
            AttackBox attackBox,
            int slotIndex,
            out Vector2 spawnPosition,
            out Vector2 targetPoint,
            out Vector2 previewAimPoint)
        {
            int bladeCount = Mathf.Max(1, verticalRainBladeCount);
            Vector2 direction = GetAttackBoxDirection(attackBox);
            Vector2 side = GetAttackBoxSide(attackBox);
            Vector2 topCenter = attackBox.Center - direction * (attackBox.Size.y * 0.5f);
            Vector2 bottomCenter = attackBox.Center + direction * (attackBox.Size.y * 0.5f);
            int normalizedSlot = bladeCount <= 0 ? 0 : Mathf.Abs(slotIndex) % bladeCount;
            float t = bladeCount == 1 ? 0.5f : normalizedSlot / (bladeCount - 1f);
            float sideOffset = Mathf.Lerp(-attackBox.Size.x * 0.5f, attackBox.Size.x * 0.5f, t);

            // 同じスロットの上端と下端を使うので、中央一点へ集まらず赤箱内を平行に落ちる。
            spawnPosition = topCenter + side * sideOffset;
            targetPoint = bottomCenter + side * sideOffset;
            previewAimPoint = bottomCenter;
        }

        private void BeginHorizontalRangeCharge(AttackBox attackBox)
        {
            if (effectController == null)
            {
                return;
            }

            List<Vector2> footPositions = BuildGroundBladeFootPositions(
                attackBox,
                out _,
                out _);
            effectController.BeginHorizontalRangeCharge(footPositions, ResolveGroundBladePrefabWidth());
        }

        private List<Vector2> BuildGroundBladeFootPositions(
            AttackBox attackBox,
            out float groundY,
            out float spawnInterval)
        {
            float spacing = ResolveGroundBladePrefabWidth();
            float sweepSpeed = Mathf.Max(0.1f, horizontalGroundBladeSweepSpeed);
            int bladeCount = Mathf.Max(1, Mathf.FloorToInt(attackBox.Size.x / spacing));
            float slotWidth = attackBox.Size.x / bladeCount;
            spawnInterval = slotWidth / sweepSpeed;
            float nearEdgeX = attackBox.Center.x - facingDirection * (attackBox.Size.x * 0.5f);
            groundY = attackBox.Center.y - attackBox.Size.y * 0.5f;

            List<Vector2> footPositions = new List<Vector2>(bladeCount);
            for (int i = 0; i < bladeCount; i++)
            {
                float distance = slotWidth * (i + 0.5f);
                footPositions.Add(new Vector2(
                    nearEdgeX + facingDirection * distance,
                    groundY));
            }

            return footPositions;
        }

        private void BeginVerticalMagicCircle(AttackBox attackBox)
        {
            if (effectController == null)
            {
                return;
            }

            GetRainBladeSlotPoints(
                attackBox,
                0,
                out Vector2 spawnPosition,
                out Vector2 targetPoint,
                out _);
            effectController.BeginVerticalRangeCharge(targetPoint, spawnPosition);
        }

        private void UpdateVerticalMagicCircle(AttackBox attackBox)
        {
            if (effectController == null)
            {
                return;
            }

            GetRainBladeSlotPoints(
                attackBox,
                0,
                out Vector2 spawnPosition,
                out Vector2 targetPoint,
                out _);
            effectController.UpdateVerticalRangeCharge(targetPoint, spawnPosition);
        }

        private float ResolveGroundBladePrefabWidth()
        {
            if (groundBladePrefab == null)
            {
                return 1f;
            }

            // 一時ブロックのX scale調整が、見た目だけでなく生成数と間隔にも反映されるようにする。
            Collider2D bladeCollider = groundBladePrefab.GetComponentInChildren<Collider2D>(true);
            float colliderWidth = ResolveColliderPrefabWidth(bladeCollider);
            if (colliderWidth > 0.001f)
            {
                return Mathf.Max(0.1f, colliderWidth);
            }

            SpriteRenderer bladeRenderer = groundBladePrefab.GetComponentInChildren<SpriteRenderer>(true);
            if (bladeRenderer != null && bladeRenderer.sprite != null)
            {
                float scaleX = Mathf.Abs(bladeRenderer.transform.lossyScale.x);
                float rendererWidth = bladeRenderer.sprite.bounds.size.x * Mathf.Max(0.001f, scaleX);
                if (rendererWidth > 0.001f)
                {
                    return Mathf.Max(0.1f, rendererWidth);
                }
            }

            return 1f;
        }

        private static float ResolveColliderPrefabWidth(Collider2D bladeCollider)
        {
            if (bladeCollider == null)
            {
                return 0f;
            }

            float scaleX = Mathf.Max(0.001f, Mathf.Abs(bladeCollider.transform.lossyScale.x));
            if (bladeCollider is BoxCollider2D boxCollider)
            {
                return Mathf.Abs(boxCollider.size.x) * scaleX;
            }

            if (bladeCollider is CapsuleCollider2D capsuleCollider)
            {
                return Mathf.Abs(capsuleCollider.size.x) * scaleX;
            }

            if (bladeCollider is CircleCollider2D circleCollider)
            {
                return Mathf.Abs(circleCollider.radius) * 2f * scaleX;
            }

            return bladeCollider.bounds.size.x;
        }

        private LastBossBladeAttack SpawnBlade(GameObject prefab, Vector2 position, Quaternion rotation)
        {
            GameObject bladeObject = Instantiate(
                prefab,
                new Vector3(position.x, position.y, transform.position.z),
                rotation);

            Collider2D bladeCollider = bladeObject.GetComponent<Collider2D>();
            if (bladeCollider != null)
            {
                bladeCollider.isTrigger = true;
            }

            LastBossBladeAttack blade = bladeObject.GetComponent<LastBossBladeAttack>();
            if (blade == null)
            {
                blade = bladeObject.AddComponent<LastBossBladeAttack>();
            }

            if (!activeBladeAttacks.Contains(blade))
            {
                activeBladeAttacks.Add(blade);
            }

            return blade;
        }

        private bool HasLiveBlade(LastBossBladeAttack.BladeKind kindToCheck)
        {
            for (int i = activeBladeAttacks.Count - 1; i >= 0; i--)
            {
                LastBossBladeAttack blade = activeBladeAttacks[i];
                if (blade == null)
                {
                    activeBladeAttacks.RemoveAt(i);
                    continue;
                }

                if (blade.Kind == kindToCheck)
                {
                    return true;
                }
            }

            return false;
        }

        private void FinishPrefabRangeAttack(BossAction action)
        {
            if (state == BossState.Dead || state == BossState.Downed)
            {
                return;
            }

            if (action == BossAction.Horizontal)
            {
                effectController?.StopHorizontalRangeEffects();
            }
            else if (action == BossAction.Vertical)
            {
                effectController?.EndVerticalRangeCharge();
            }

            HideRangeParryProxy();
            activeBladeAttackRoutine = null;
            prefabAttackRunning = false;
            visibleAction = action;
            stateTimer = GetRecoveryTime(action);
            state = BossState.Recovery;
            spriteView?.PlayIdle();
        }

        private void SetRangeActionCooldown(BossAction action)
        {
            if (action == BossAction.Horizontal)
            {
                horizontalReadyTime = Time.time + horizontalAttackCooldown;
            }
            else if (action == BossAction.Vertical)
            {
                verticalReadyTime = Time.time + verticalAttackCooldown;
            }
        }

        private void MarkActionResolved(BossAction action)
        {
            if (action == BossAction.Normal)
            {
                normalChainCount++;
            }
            else
            {
                normalChainCount = 0;
            }

            previousAction = action;
            ClearJustParryBuffer();
        }

        private void ApplyDamageToPlayersInBox(BossAction action, AttackBox attackBox)
        {
            // ダメージ判定は攻撃発生時に1回だけ行う。見た目の赤範囲表示時間とは別。
            int hitCount = Physics2D.OverlapBox(attackBox.Center, attackBox.Size, attackBox.Angle, playerContactFilter, playerHits);
            PlayerHealth damagedHealth = null;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = playerHits[i];
                // ボス攻撃も、傘やパリィ判定ではなくプレイヤー本体コライダーだけを被弾対象にする。
                if (!PlayerBodyColliderUtility.TryGetPlayerBodyFromCollider(
                        hit,
                        out PlayerHealth targetHealth,
                        out _))
                {
                    continue;
                }

                if (targetHealth == null || targetHealth == damagedHealth)
                {
                    continue;
                }

                damagedHealth = targetHealth;
                if (targetHealth.TryTakeDamage(GetAttackDamage(action)))
                {
                    HitStopController.RequestEnemyToPlayer();

                    // HP クールダウンを通過した実ダメージだけ、被弾フラッシュを強制再生する。
                    PlayerDamageFlash damageFlash = targetHealth.GetComponent<PlayerDamageFlash>();
                    if (damageFlash == null)
                    {
                        damageFlash = targetHealth.GetComponentInChildren<PlayerDamageFlash>(true);
                    }

                    damageFlash?.PlayFlashForced();
                }
            }
        }

        public bool TryApplyBladeDamage(LastBossBladeAttack blade, PlayerHealth targetHealth, int damage)
        {
            if (blade == null || targetHealth == null || state == BossState.Dead || !encounterActive)
            {
                return false;
            }

            // ブレードは複数本出るため、横/縦それぞれ攻撃1回につきダメージは1回だけに制限する。
            if (blade.Kind == LastBossBladeAttack.BladeKind.Ground)
            {
                if (horizontalBladeDamageDealt)
                {
                    return false;
                }
            }
            else if (verticalBladeDamageDealt)
            {
                return false;
            }

            if (!targetHealth.TryTakeDamage(Mathf.Max(1, damage)))
            {
                return false;
            }

            if (blade.Kind == LastBossBladeAttack.BladeKind.Ground)
            {
                horizontalBladeDamageDealt = true;
            }
            else
            {
                verticalBladeDamageDealt = true;
            }

            PlayPlayerDamageFlash(targetHealth);
            HitStopController.RequestEnemyToPlayer();
            return true;
        }

        public void NotifyBladeParried(LastBossBladeAttack blade)
        {
            if (blade == null || state == BossState.Dead)
            {
                return;
            }

            BossAction action = blade.Kind == LastBossBladeAttack.BladeKind.Ground
                ? BossAction.Horizontal
                : BossAction.Vertical;

            if (!prefabAttackRunning)
            {
                SetRangeActionCooldown(action);
                MarkActionResolved(action);
            }
            else
            {
                ClearJustParryBuffer();
            }

            // パリィ成立時は通常キャンセルと違い、ブレードを指定時間でフェードアウトさせる。
            CancelActiveBladeAttack(bladeParryFadeDuration);
            if (action == BossAction.Vertical)
            {
                effectController?.EndVerticalRangeCharge();
            }

            bool downStarted = AddDownCount(action == BossAction.Horizontal ? 7 : 15);
            if (downStarted)
            {
                HideAttackVisual();
                return;
            }

            if (state != BossState.Dead && state != BossState.Downed)
            {
                visibleAction = action;
                stateTimer = GetRecoveryTime(action);
                state = BossState.Recovery;
                spriteView?.PlayIdle();
            }
        }

        public bool IsRangeParryProxyActive()
        {
            return rangeParryProxyActive
                   && state != BossState.Dead
                   && state != BossState.Downed
                   && (pendingAction == BossAction.Horizontal
                       || pendingAction == BossAction.Vertical
                       || visibleAction == BossAction.Horizontal
                       || visibleAction == BossAction.Vertical);
        }

        public void NotifyRangeParryProxyParried()
        {
            if (!IsRangeParryProxyActive())
            {
                return;
            }

            BossAction action = pendingAction == BossAction.Horizontal || pendingAction == BossAction.Vertical
                ? pendingAction
                : visibleAction;

            if (action != BossAction.Horizontal && action != BossAction.Vertical)
            {
                return;
            }

            if (!prefabAttackRunning)
            {
                SetRangeActionCooldown(action);
                MarkActionResolved(action);
            }
            else
            {
                ClearJustParryBuffer();
            }

            // 透明プロキシ経由のパリィでも、実体ブレード側と同じ中断処理を使う。
            CancelActiveBladeAttack(bladeParryFadeDuration);
            if (action == BossAction.Vertical)
            {
                effectController?.EndVerticalRangeCharge();
            }

            bool downStarted = AddDownCount(action == BossAction.Horizontal ? 7 : 15);
            if (downStarted)
            {
                HideAttackVisual();
                return;
            }

            if (state != BossState.Dead && state != BossState.Downed)
            {
                visibleAction = action;
                stateTimer = GetRecoveryTime(action);
                state = BossState.Recovery;
                spriteView?.PlayIdle();
            }
        }

        public void NotifyBladeLanded(LastBossBladeAttack blade)
        {
        }

        public void NotifyGroundBladeUpright(int slotIndex)
        {
            effectController?.FadeHorizontalRangeSlot(slotIndex);
        }

        public void NotifyBladeDestroyed(LastBossBladeAttack blade)
        {
            if (blade == null)
            {
                return;
            }

            activeBladeAttacks.Remove(blade);
            preparedRainBlades.Remove(blade);
        }

        private void PlayPlayerDamageFlash(PlayerHealth targetHealth)
        {
            if (targetHealth == null)
            {
                return;
            }

            PlayerDamageFlash damageFlash = targetHealth.GetComponent<PlayerDamageFlash>();
            if (damageFlash == null)
            {
                damageFlash = targetHealth.GetComponentInChildren<PlayerDamageFlash>(true);
            }

            damageFlash?.PlayFlashForced();
        }

        private bool IsRangeAttackParried(BossAction action, AttackBox attackBox)
        {
            // 通常攻撃はジャストパリィ対象外。横/縦範囲攻撃だけダウンカウントを加算する。
            if (action != BossAction.Horizontal && action != BossAction.Vertical)
            {
                return false;
            }

            if (IsBufferedJustParryValid(action))
            {
                return true;
            }

            if (!IsPlayerCurrentlyParryingInBox(attackBox))
            {
                return false;
            }

            HitStopController.RequestParry();
            return true;
        }

        private void UpdateJustParryBuffer(BossAction action, AttackBox attackBox)
        {
            // 予兆中に範囲内でパリィできていれば、justParryEffectDuration秒だけ成功扱いを保持する。
            if (action != BossAction.Horizontal && action != BossAction.Vertical)
            {
                return;
            }

            if (!IsPlayerCurrentlyParryingInBox(attackBox))
            {
                return;
            }

            bool alreadyBuffered = IsBufferedJustParryValid(action);
            justParryBufferedAction = action;
            justParryValidUntil = Time.time + justParryEffectDuration;
            if (!alreadyBuffered)
            {
                HitStopController.RequestParry();
            }
        }

        private bool IsBufferedJustParryValid(BossAction action)
        {
            return justParryBufferedAction == action && Time.time <= justParryValidUntil;
        }

        private void ClearJustParryBuffer()
        {
            justParryBufferedAction = BossAction.None;
            justParryValidUntil = -1f;
        }

        private bool IsPlayerCurrentlyParryingInBox(AttackBox attackBox)
        {
            if (playerParryController == null || !playerParryController.IsParrying() || playerParryHitbox == null)
            {
                return false;
            }

            Collider2D parryCollider = playerParryHitbox.GetComponent<Collider2D>();
            if (parryCollider == null || !parryCollider.enabled)
            {
                return false;
            }

            int hitCount = Physics2D.OverlapBox(attackBox.Center, attackBox.Size, attackBox.Angle, playerContactFilter, playerHits);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = playerHits[i];
                if (hit == parryCollider || hit.transform.IsChildOf(parryCollider.transform))
                {
                    return true;
                }
            }

            return parryCollider.bounds.Intersects(new Bounds(attackBox.Center, attackBox.Size));
        }

        private int GetAttackDamage(BossAction action)
        {
            int baseDamage = action switch
            {
                BossAction.Horizontal => horizontalAttackDamage,
                BossAction.Vertical => verticalAttackDamage,
                _ => normalAttackDamage
            };

            return Mathf.Max(1, Mathf.CeilToInt(baseDamage * (enraged ? enragedAttackMultiplier : 1f)));
        }

        private float GetAttackVisibleTime(BossAction action)
        {
            return action switch
            {
                BossAction.Horizontal => horizontalAttackVisibleTime,
                BossAction.Vertical => verticalAttackVisibleTime,
                _ => normalAttackVisibleTime
            };
        }

        private float GetRecoveryTime(BossAction action)
        {
            return action switch
            {
                BossAction.Horizontal => horizontalAttackRecovery,
                BossAction.Vertical => verticalAttackRecovery,
                _ => normalAttackRecovery
            };
        }

        private void MoveForPendingAction()
        {
            if (!IsPlayerAvailable())
            {
                StopMotion();
                return;
            }

            float signedDistance = playerTransform.position.x - transform.position.x;
            float absDistance = Mathf.Abs(signedDistance);
            float stopDistance = GetStopDistanceForAction(pendingAction);

            if (absDistance <= stopDistance || absDistance <= minimumPlayerDistance)
            {
                StopMotion();
                return;
            }

            facingDirection = signedDistance >= 0f ? 1 : -1;
            effectController?.SetFacingDirection(facingDirection);
            Vector2 velocity = rb2D.linearVelocity;
            velocity.x = facingDirection * moveSpeed;
            rb2D.linearVelocity = velocity;
            ApplyFacingVisual();
            spriteView?.PlayMove();
        }

        private float GetStopDistanceForAction(BossAction action)
        {
            return action switch
            {
                BossAction.Normal => normalStopDistance,
                BossAction.Horizontal => Mathf.Max(minimumPlayerDistance, horizontalAttackSize.x * 0.8f),
                _ => minimumPlayerDistance
            };
        }

        private bool IsPlayerInRangeForAction(BossAction action)
        {
            if (!IsPlayerAvailable())
            {
                return false;
            }

            float distance = GetHorizontalPlayerDistance();
            return action switch
            {
                BossAction.Normal => distance < normalApproachStartDistance,
                BossAction.Horizontal => distance <= horizontalAttackSize.x,
                BossAction.Vertical => true,
                _ => false
            };
        }

        private AttackBox BuildAttackBox(BossAction action)
        {
            Bounds bounds = bodyCollider != null ? bodyCollider.bounds : new Bounds(transform.position, Vector3.one);

            if (action == BossAction.Vertical && IsPlayerAvailable())
            {
                // 縦範囲は「ボス頭上+指定高さ」から「プレイヤーX、ボス足元Y」へ斜めに伸ばす。
                // この関数をBeginAction時にだけ呼ぶことで、予兆中にプレイヤーへ追従しない。
                Vector2 start = new Vector2(bounds.center.x, bounds.max.y + verticalAttackStartHeight);
                Vector2 end = new Vector2(playerTransform.position.x, bounds.min.y);
                Vector2 direction = end - start;
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    direction = Vector2.down;
                }

                float length = Mathf.Max(0.1f, direction.magnitude);
                Vector2 center = start + direction.normalized * (length * 0.5f);
                float angle = Vector2.SignedAngle(Vector2.up, direction);
                return new AttackBox(center, new Vector2(verticalAttackWidth, length), angle);
            }

            Vector2 size = action == BossAction.Horizontal ? horizontalAttackSize : normalAttackSize;
            float forwardInset = action == BossAction.Normal
                ? Mathf.Min(
                    Mathf.Max(0f, normalAttackForwardInset),
                    Mathf.Max(0f, bounds.extents.x + size.x * 0.5f - 0.05f))
                : 0f;
            float centerX = bounds.center.x + facingDirection * (bounds.extents.x + size.x * 0.5f - forwardInset);
            return new AttackBox(new Vector2(centerX, bounds.center.y), size, 0f);
        }

        private bool IsPlayerAvailable()
        {
            if (playerTransform != null)
            {
                return true;
            }

            CachePlayerReferences();
            return playerTransform != null;
        }

        private void CachePlayerReferences()
        {
            if (playerTransform == null && !string.IsNullOrWhiteSpace(playerTag))
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
                if (playerObject != null)
                {
                    playerTransform = playerObject.transform;
                }
            }

            if (playerTransform == null)
            {
                return;
            }

            if (playerParryController == null)
            {
                playerParryController = playerTransform.GetComponentInChildren<UmbrellaParryController>(true);
            }

            if (playerParryHitbox == null)
            {
                playerParryHitbox = playerTransform.GetComponentInChildren<ParryHitbox>(true);
            }
        }

        private void FacePlayer()
        {
            if (!IsPlayerAvailable())
            {
                return;
            }

            float deltaX = playerTransform.position.x - transform.position.x;
            if (Mathf.Abs(deltaX) <= 0.05f)
            {
                return;
            }

            facingDirection = deltaX >= 0f ? 1 : -1;
            ApplyFacingVisual();

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = facingDirection > 0;
            }

            effectController?.SetFacingDirection(facingDirection);
        }

        private bool CanTurnTowardPlayer()
        {
            return state == BossState.InitialDelay || state == BossState.MovingToRange;
        }

        private float GetHorizontalPlayerDistance()
        {
            if (!IsPlayerAvailable())
            {
                return float.PositiveInfinity;
            }

            return Mathf.Abs(playerTransform.position.x - transform.position.x);
        }

        private bool IsBackAttack(AttackHitbox attacker)
        {
            if (attacker == null)
            {
                return false;
            }

            float attackDeltaX = attacker.AttackOriginPosition.x - transform.position.x;
            if (Mathf.Abs(attackDeltaX) <= BackAttackMinHorizontalDelta)
            {
                return false;
            }

            int attackerSide = attackDeltaX >= 0f ? 1 : -1;
            return attackerSide != facingDirection;
        }

        private int CalculatePlayerAttackDamage(AttackHitbox attacker, bool isBackAttack)
        {
            if (attacker == null)
            {
                return 0;
            }

            int baseDamage = attacker.PlayerAttackDamage;
            if (baseDamage <= 0 || !isBackAttack)
            {
                return baseDamage;
            }

            return Mathf.CeilToInt(baseDamage * Mathf.Max(1f, backAttackDamageMultiplier));
        }

        private bool AddDownCount(int amount)
        {
            if (amount <= 0 || state == BossState.Downed || downRoutineRunning || state == BossState.Dead)
            {
                return false;
            }

            downCount += amount;
            if (downCount >= downCountThreshold)
            {
                StartCoroutine(EnterDownRoutine());
                return true;
            }

            return false;
        }

        private IEnumerator EnterDownRoutine()
        {
            downRoutineRunning = true;
            CancelActiveBladeAttack();
            // ヒットストップ前にDownedへ入れて、同フレーム以降の攻撃更新を止める。
            state = BossState.Downed;
            StopMotion();
            HideAttackVisual();
            spriteView?.PlayDownStart();
            pendingAction = BossAction.None;
            visibleAction = BossAction.None;
            ClearJustParryBuffer();
            effectController?.HandleDownStarted();

            HitStopController.Request(hitStopDuration);
            float previousTimeScale = Time.timeScale;
            if (UseLegacyBossHitStop && useGlobalHitStop && hitStopDuration > 0f)
            {
                // 全体停止のヒットストップ。終了時は必ず元のtimeScaleへ戻す。
                hitStopRestoreTimeScale = previousTimeScale;
                hitStopTimeScaleActive = true;
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(hitStopDuration);

                RestoreHitStopTimeScale();
            }
            else if (hitStopDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(hitStopDuration);
            }

            float downHoldSeconds = Mathf.Max(0f, downDuration);
            float downStartDuration = spriteView != null ? spriteView.DownStartDuration : 0f;
            float downEndDuration = spriteView != null ? spriteView.DownEndDuration : 0f;
            if (downEndDuration > 0f)
            {
                downHoldSeconds = Mathf.Max(0f, downHoldSeconds - downEndDuration);
            }

            if (downStartDuration > 0f && downHoldSeconds > 0f)
            {
                float downStartWait = Mathf.Min(downStartDuration, downHoldSeconds);
                yield return new WaitForSeconds(downStartWait);
                downHoldSeconds = Mathf.Max(0f, downHoldSeconds - downStartWait);
            }

            spriteView?.PlayDownHold();
            if (downHoldSeconds > 0f)
            {
                yield return new WaitForSeconds(downHoldSeconds);
            }

            spriteView?.PlayDownEnd();
            if (downEndDuration > 0f)
            {
                yield return new WaitForSeconds(downEndDuration);
            }

            if (state != BossState.Dead)
            {
                downCount = 0;
                downRoutineRunning = false;
                state = BossState.Recovery;
                stateTimer = 0f;
                spriteView?.PlayIdle();
                effectController?.HandleDownEnded();
            }
        }

        private void RestoreHitStopTimeScale()
        {
            if (!hitStopTimeScaleActive)
            {
                return;
            }

            if (Mathf.Approximately(Time.timeScale, 0f))
            {
                Time.timeScale = hitStopRestoreTimeScale;
            }

            hitStopTimeScaleActive = false;
        }

        private void TryEnterEnraged()
        {
            if (enraged || currentHealth > maxHealth * enrageHealthRate)
            {
                return;
            }

            enraged = true;
        }

        public void ResetHealthToFull()
        {
            StopAllCoroutines();
            CancelActiveBladeAttack();
            RestoreHitStopTimeScale();
            downRoutineRunning = false;
            deathRoutineRunning = false;
            enraged = false;
            downCount = 0;
            state = BossState.Inactive;
            encounterActive = false;
            pendingAction = BossAction.None;
            visibleAction = BossAction.None;
            ClearJustParryBuffer();
            StopMotion();
            spriteView?.PlayIdle();
            HideAttackVisual();
            RestoreCombatBodyAfterReset();
            SetBossRenderersEnabled(true);
            effectController?.SetFacingDirection(facingDirection);
            effectController?.HandleResetToFull();
            currentHealth = MaxHealth;
            NotifyHealthChanged();
        }

        private void NotifyHealthChanged()
        {
            HealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        private void PlayHitFlash()
        {
            hitFlashStartTime = Time.time;
            int repeatCount = Mathf.Max(1, hitFlashRepeatCount);
            hitFlashEndTime = hitFlashStartTime +
                repeatCount * Mathf.Max(0.01f, hitFlashDuration) +
                (repeatCount - 1) * Mathf.Max(0f, hitFlashNormalDuration);
        }

        private void UpdateEnragedVisual()
        {
            SpriteRenderer mainRenderer = GetMainSpriteRenderer();
            if (mainRenderer == null)
            {
                return;
            }

            if (IsHitFlashActive())
            {
                mainRenderer.color = hitFlashColor;
                return;
            }

            if (!enraged)
            {
                mainRenderer.color = defaultSpriteColor;
                return;
            }

            float pulse = enragedPulseSpeed <= 0f ? 1f : (Mathf.Sin(Time.time * enragedPulseSpeed) + 1f) * 0.5f;
            mainRenderer.color = Color.Lerp(defaultSpriteColor, enragedColor, 0.45f + pulse * 0.35f);
        }

        private bool IsHitFlashActive()
        {
            if (Time.time < hitFlashStartTime || Time.time >= hitFlashEndTime)
            {
                return false;
            }

            float cycleDuration = Mathf.Max(0.01f, hitFlashDuration) + Mathf.Max(0f, hitFlashNormalDuration);
            float elapsed = Time.time - hitFlashStartTime;
            float cyclePosition = Mathf.Repeat(elapsed, cycleDuration);
            return cyclePosition < hitFlashDuration;
        }

        private void Die()
        {
            if (deathRoutineRunning)
            {
                return;
            }

            state = BossState.Dead;
            encounterActive = false;
            deathRoutineRunning = true;
            CancelActiveBladeAttack();
            StopMotion();
            HideAttackVisual();
            spriteView?.PlayDead();
            DisableCombatBodyForDeath();
            // Destroy前に通知して、ボスの表示状態を参照できるようにする。
            Died?.Invoke();
            bool deathEffectStarted = effectController != null &&
                                      effectController.PlayDeath(HideBossVisualsForDeath, CompleteDeathDestroy);
            if (!deathEffectStarted)
            {
                HideBossVisualsForDeath();
                CompleteDeathDestroy();
            }
        }

        private void DisableCombatBodyForDeath()
        {
            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            if (rb2D != null)
            {
                rb2D.linearVelocity = Vector2.zero;
                rb2D.simulated = false;
            }
        }

        private void RestoreCombatBodyAfterReset()
        {
            if (bodyCollider != null)
            {
                bodyCollider.enabled = true;
            }

            if (rb2D != null)
            {
                rb2D.simulated = true;
                ConfigureRigidbody();
            }
        }

        private void HideBossVisualsForDeath()
        {
            SetBossRenderersEnabled(false);
        }

        private void SetBossRenderersEnabled(bool enabled)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i] != telegraphRenderer)
                {
                    renderers[i].enabled = enabled;
                }
            }
        }

        private void CompleteDeathDestroy()
        {
            if (!deathRoutineRunning || state != BossState.Dead)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }

        private void CancelActiveBladeAttack(float bladeFadeDuration = 0f)
        {
            // bladeFadeDurationはパリィ演出用。0なら死亡・非アクティブ化など従来通り即破棄する。
            if (activeBladeAttackRoutine != null)
            {
                StopCoroutine(activeBladeAttackRoutine);
                activeBladeAttackRoutine = null;
            }

            if (verticalRainPreviewRoutine != null)
            {
                StopCoroutine(verticalRainPreviewRoutine);
                verticalRainPreviewRoutine = null;
            }

            prefabAttackRunning = false;
            horizontalBladeDamageDealt = false;
            verticalBladeDamageDealt = false;
            HideRangeParryProxy();
            effectController?.StopHorizontalRangeEffects();

            for (int i = activeBladeAttacks.Count - 1; i >= 0; i--)
            {
                LastBossBladeAttack blade = activeBladeAttacks[i];
                if (blade == null)
                {
                    continue;
                }

                if (bladeFadeDuration > 0f)
                {
                    blade.ForceFadeOut(bladeFadeDuration);
                }
                else
                {
                    blade.ForceDestroy();
                }
            }

            activeBladeAttacks.Clear();
            preparedRainBlades.Clear();
        }

        private void ClearBladesOfKind(LastBossBladeAttack.BladeKind bladeKind)
        {
            for (int i = activeBladeAttacks.Count - 1; i >= 0; i--)
            {
                LastBossBladeAttack blade = activeBladeAttacks[i];
                if (blade == null)
                {
                    activeBladeAttacks.RemoveAt(i);
                    continue;
                }

                if (blade.Kind != bladeKind)
                {
                    continue;
                }

                blade.ForceDestroy();

                activeBladeAttacks.RemoveAt(i);
            }

            if (bladeKind == LastBossBladeAttack.BladeKind.Rain)
            {
                preparedRainBlades.Clear();
            }
        }

        private void ClearQueuedRainBlades()
        {
            for (int i = preparedRainBlades.Count - 1; i >= 0; i--)
            {
                LastBossBladeAttack blade = preparedRainBlades[i];
                if (blade != null)
                {
                    blade.ForceDestroy();
                }
            }

            preparedRainBlades.Clear();
        }

        private void ShowRangeParryProxy(AttackBox attackBox, BossAction action)
        {
            if (action != BossAction.Horizontal && action != BossAction.Vertical)
            {
                return;
            }

            // ブレード本体に触れていないタイミングでも範囲攻撃全体をパリィできるようにする透明判定。
            EnsureVisualObjects();

            if (telegraphObject == null || telegraphCollider == null)
            {
                return;
            }

            rangeParryProxyActive = true;
            telegraphObject.transform.SetParent(null, true);
            telegraphObject.transform.position = new Vector3(attackBox.Center.x, attackBox.Center.y, transform.position.z);
            telegraphObject.transform.rotation = Quaternion.Euler(0f, 0f, attackBox.Angle);
            telegraphObject.transform.localScale = new Vector3(attackBox.Size.x, attackBox.Size.y, 1f);
            telegraphCollider.enabled = true;

            if (telegraphRenderer != null)
            {
                telegraphRenderer.enabled = false;
            }
        }

        private void HideRangeParryProxy()
        {
            rangeParryProxyActive = false;

            if (telegraphRenderer != null && telegraphRenderer.enabled)
            {
                return;
            }

            if (telegraphCollider != null)
            {
                telegraphCollider.enabled = false;
            }
        }

        private static Vector2 GetAttackBoxDirection(AttackBox attackBox)
        {
            Quaternion rotation = Quaternion.Euler(0f, 0f, attackBox.Angle);
            Vector2 direction = rotation * Vector2.up;
            return direction.sqrMagnitude <= 0.0001f ? Vector2.down : direction.normalized;
        }

        private static Vector2 GetAttackBoxSide(AttackBox attackBox)
        {
            Quaternion rotation = Quaternion.Euler(0f, 0f, attackBox.Angle);
            Vector2 side = rotation * Vector2.right;
            return side.sqrMagnitude <= 0.0001f ? Vector2.right : side.normalized;
        }

        private void ResolveSpriteView()
        {
            if (spriteView == null)
            {
                spriteView = GetComponentInChildren<LastBossSpriteAnimator>(true);
            }

            spriteRenderer = spriteView != null && spriteView.MainRenderer != null
                ? spriteView.MainRenderer
                : GetComponent<SpriteRenderer>();
        }

        private SpriteRenderer GetMainSpriteRenderer()
        {
            if (spriteView != null && spriteView.MainRenderer != null)
            {
                return spriteView.MainRenderer;
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            return spriteRenderer;
        }

        private void ApplyFacingVisual()
        {
            if (spriteView != null)
            {
                spriteView.SetFacing(facingDirection);
                return;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = facingDirection > 0;
            }
        }

        private void PlayRangeTelegraphAnimation(BossAction action)
        {
            if (spriteView == null)
            {
                return;
            }

            if (action == BossAction.Horizontal)
            {
                spriteView.PlayHorizontalStart();
            }
            else if (action == BossAction.Vertical)
            {
                spriteView.PlayVerticalStart();
            }
        }

        private void PlayAttackReleaseAnimation(BossAction action)
        {
            if (spriteView == null)
            {
                return;
            }

            if (action == BossAction.Horizontal)
            {
                spriteView.PlayHorizontalEnd();
            }
            else if (action == BossAction.Vertical)
            {
                spriteView.PlayVerticalEnd();
            }
        }

        private void ConfigureRigidbody()
        {
            if (rb2D == null)
            {
                return;
            }

            rb2D.gravityScale = 0f;
            rb2D.constraints |= RigidbodyConstraints2D.FreezeRotation;
        }

        private void StopMotion()
        {
            if (rb2D == null)
            {
                return;
            }

            Vector2 velocity = rb2D.linearVelocity;
            velocity.x = 0f;
            rb2D.linearVelocity = velocity;
        }

        private void BuildPlayerContactFilter()
        {
            playerContactFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = true
            };
            playerContactFilter.SetLayerMask(playerDetectionMask.value == 0 ? PlayerBodyColliderUtility.GetPlayerBodyLayerMask() : playerDetectionMask);
        }

        private void EnsureVisualObjects()
        {
            if (telegraphRenderer != null)
            {
                LastBossAttackParryTarget existingParryTarget =
                    telegraphObject != null ? telegraphObject.GetComponent<LastBossAttackParryTarget>() : null;
                if (existingParryTarget != null)
                {
                    existingParryTarget.Initialize(this);
                }

                return;
            }

            if (runtimeBoxSprite == null)
            {
                runtimeBoxSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            }

            Transform existing = transform.Find("LastBossAttackPreview");
            if (existing == null)
            {
                telegraphObject = new GameObject("LastBossAttackPreview");
                existing = telegraphObject.transform;
                existing.SetParent(transform, false);
            }
            else
            {
                telegraphObject = existing.gameObject;
            }

            telegraphRenderer = telegraphObject.GetComponent<SpriteRenderer>();
            if (telegraphRenderer == null)
            {
                telegraphRenderer = telegraphObject.AddComponent<SpriteRenderer>();
            }

            telegraphCollider = telegraphObject.GetComponent<BoxCollider2D>();
            if (telegraphCollider == null)
            {
                telegraphCollider = telegraphObject.AddComponent<BoxCollider2D>();
            }

            if (telegraphObject.GetComponent<LastBossAttackParryTarget>() == null)
            {
                // ParryHitboxがLastBossの範囲攻撃を「敵攻撃」として検知するための目印。
                telegraphObject.AddComponent<LastBossAttackParryTarget>();
            }

            LastBossAttackParryTarget parryTarget = telegraphObject.GetComponent<LastBossAttackParryTarget>();
            if (parryTarget == null)
            {
                parryTarget = telegraphObject.AddComponent<LastBossAttackParryTarget>();
            }

            parryTarget.Initialize(this);
            telegraphCollider.isTrigger = true;
            telegraphCollider.size = Vector2.one;
            telegraphCollider.enabled = false;
            telegraphRenderer.sprite = runtimeBoxSprite;
            SpriteRenderer mainRenderer = GetMainSpriteRenderer();
            telegraphRenderer.sortingOrder = mainRenderer != null ? mainRenderer.sortingOrder + 1 : 1;
            telegraphRenderer.enabled = false;
        }

        private void ShowAttackVisual(AttackBox attackBox, Color color)
        {
            EnsureVisualObjects();

            if (telegraphRenderer == null)
            {
                return;
            }

            // 予兆オブジェクトはワールド座標で固定表示し、親の反転や移動の影響を受けにくくする。
            telegraphObject.transform.SetParent(null, true);
            telegraphObject.transform.position = new Vector3(attackBox.Center.x, attackBox.Center.y, transform.position.z);
            telegraphObject.transform.rotation = Quaternion.Euler(0f, 0f, attackBox.Angle);
            telegraphObject.transform.localScale = new Vector3(attackBox.Size.x, attackBox.Size.y, 1f);
            telegraphRenderer.color = color;
            telegraphRenderer.enabled = true;

            if (telegraphCollider != null)
            {
                telegraphCollider.enabled = true;
            }
        }

        private void HideAttackVisual()
        {
            if (telegraphRenderer == null)
            {
                return;
            }

            if (telegraphCollider != null)
            {
                telegraphCollider.enabled = false;
            }

            telegraphRenderer.enabled = false;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos)
            {
                return;
            }

            AttackBox preview = BuildAttackBox(lastDebugAction == BossAction.None ? BossAction.Normal : lastDebugAction);
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(preview.Center, Quaternion.Euler(0f, 0f, preview.Angle), Vector3.one);
            Gizmos.color = new Color(1f, 0f, 0f, 0.75f);
            Gizmos.DrawWireCube(Vector3.zero, preview.Size);
            Gizmos.matrix = previousMatrix;
        }

        private readonly struct AttackBox
        {
            public AttackBox(Vector2 center, Vector2 size, float angle)
            {
                Center = center;
                Size = size;
                Angle = angle;
            }

            public Vector2 Center { get; }
            public Vector2 Size { get; }
            public float Angle { get; }
        }
    }

    public sealed class LastBossAttackParryTarget : MonoBehaviour, IParryableAttack
    {
        private LastBossController owner;

        // 赤箱は非表示でも、このプロキシが範囲攻撃のパリィ受付状態をParryHitboxへ伝える。
        public bool IsParryable => owner != null && owner.IsRangeParryProxyActive();

        public void Initialize(LastBossController targetOwner)
        {
            owner = targetOwner;
        }

        public void StopByParry()
        {
            if (!IsParryable)
            {
                return;
            }

            owner.NotifyRangeParryProxyParried();
        }

        // LastBossの範囲攻撃予兆をParryHitboxへ知らせるためのマーカーコンポーネント。
    }
}
