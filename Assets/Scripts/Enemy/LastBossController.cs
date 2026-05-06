using System.Collections;
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
    public sealed class LastBossController : MonoBehaviour, IAttackReceiver
    {
        [Header("Activation")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField, Min(0f)] private float initialActionDelay = 2f;
        [SerializeField] private bool startActiveOnPlay;

        [Header("Stats")]
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField, Min(0f)] private float moveSpeed = 8f;
        [SerializeField, Range(0.01f, 1f)] private float enrageHealthRate = 0.4f;
        [SerializeField, Min(1f)] private float enragedAttackMultiplier = 1.1f;
        [SerializeField, Min(1)] private int playerAttackDamage = 1;

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
        [SerializeField, Min(0.01f)] private float normalAttackVisibleTime = 0.16f;
        [SerializeField, Min(0f)] private float normalAttackRecovery = 0.45f;

        [Header("Horizontal Range Attack")]
        [SerializeField, Min(1)] private int horizontalAttackDamage = 15;
        [SerializeField] private Vector2 horizontalAttackSize = new Vector2(10f, 4f);
        [SerializeField, Min(0f)] private float horizontalAttackCooldown = 10f;
        [SerializeField, Min(0f)] private float horizontalTelegraphTime = 2f;
        [SerializeField, Min(0.01f)] private float horizontalAttackVisibleTime = 0.22f;
        [SerializeField, Min(0f)] private float horizontalAttackRecovery = 0.6f;

        [Header("Vertical Range Attack")]
        [SerializeField, Min(1)] private int verticalAttackDamage = 20;
        [SerializeField, Min(0.1f)] private float verticalAttackWidth = 2f;
        [SerializeField, Min(0f)] private float verticalAttackStartHeight = 6f;
        [SerializeField, Min(0f)] private float verticalAttackCooldown = 18f;
        [SerializeField, Min(0f)] private float verticalTelegraphTime = 2f;
        [SerializeField, Min(0.01f)] private float verticalAttackVisibleTime = 0.24f;
        [SerializeField, Min(0f)] private float verticalAttackRecovery = 0.7f;

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

        private readonly Collider2D[] playerHits = new Collider2D[16];

        private Rigidbody2D rb2D;
        private Collider2D bodyCollider;
        private SpriteRenderer spriteRenderer;
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
        private int currentHealth;
        private int facingDirection = 1;
        private int normalChainCount;
        private int downCount;
        private bool encounterActive;
        private bool enraged;
        private bool downRoutineRunning;
        private bool hitStopTimeScaleActive;
        private float stateTimer;
        private float horizontalReadyTime;
        private float verticalReadyTime;
        private float hitStopRestoreTimeScale = 1f;
        // 予兆中に成立したジャストパリィを、攻撃発生まで短時間だけ保持する。
        private float justParryValidUntil = -1f;
        private BossAction justParryBufferedAction = BossAction.None;
        private Color defaultSpriteColor = Color.white;

        public bool IsEncounterActive => encounterActive;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;

        private void Awake()
        {
            rb2D = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            currentHealth = Mathf.Max(1, maxHealth);

            if (spriteRenderer != null)
            {
                defaultSpriteColor = spriteRenderer.color;
            }

            if (playerDetectionMask.value == 0)
            {
                playerDetectionMask = Physics2D.DefaultRaycastLayers;
            }

            BuildPlayerContactFilter();
            ConfigureRigidbody();
            EnsureVisualObjects();
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
            normalAttackSize.x = Mathf.Max(0.1f, normalAttackSize.x);
            normalAttackSize.y = Mathf.Max(0.1f, normalAttackSize.y);
            horizontalAttackSize.x = Mathf.Max(0.1f, horizontalAttackSize.x);
            horizontalAttackSize.y = Mathf.Max(0.1f, horizontalAttackSize.y);
            verticalAttackWidth = Mathf.Max(0.1f, verticalAttackWidth);
            justParryEffectDuration = Mathf.Max(0f, justParryEffectDuration);
            BuildPlayerContactFilter();
        }

        private void OnDisable()
        {
            StopMotion();
            HideAttackVisual();
            RestoreHitStopTimeScale();
            encounterActive = false;
            state = BossState.Inactive;
        }

        private void OnDestroy()
        {
            RestoreHitStopTimeScale();

            if (telegraphObject != null)
            {
                Destroy(telegraphObject);
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

            CachePlayerReferences();
            encounterActive = true;
            pendingAction = BossAction.None;
            // BossAreaに入ってすぐ攻撃せず、調整可能な待ち時間後に初回行動を始める。
            stateTimer = initialActionDelay;
            state = BossState.InitialDelay;
            StopMotion();
            HideAttackVisual();
        }

        public void DeactivateEncounter()
        {
            encounterActive = false;
            state = BossState.Inactive;
            pendingAction = BossAction.None;
            visibleAction = BossAction.None;
            ClearJustParryBuffer();
            StopMotion();
            HideAttackVisual();
        }

        public void OnAttacked(AttackHitbox attacker, Collider2D hitCollider)
        {
            // プレイヤー通常攻撃から呼ばれる被弾口。背後ヒットならダウンカウントを多めに加算する。
            if (state == BossState.Dead || currentHealth <= 0)
            {
                return;
            }

            currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(1, playerAttackDamage));
            TryEnterEnraged();

            if (currentHealth <= 0)
            {
                Die();
                return;
            }

            AddDownCount(IsBackAttack(attacker) ? 2 : 1);
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
            stateTimer -= Time.deltaTime;

            if (stateTimer > 0f)
            {
                return;
            }

            HideAttackVisual();
            stateTimer = GetRecoveryTime(visibleAction);
            state = BossState.Recovery;
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
                ShowAttackVisual(activeAttackBox, telegraphColor);
                return;
            }

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

        private void ApplyDamageToPlayersInBox(BossAction action, AttackBox attackBox)
        {
            // ダメージ判定は攻撃発生時に1回だけ行う。見た目の赤範囲表示時間とは別。
            int hitCount = Physics2D.OverlapBox(attackBox.Center, attackBox.Size, attackBox.Angle, playerContactFilter, playerHits);
            PlayerHealth damagedHealth = null;

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = playerHits[i];
                if (!IsPlayerCollider(hit))
                {
                    continue;
                }

                PlayerHealth targetHealth = hit.GetComponentInParent<PlayerHealth>();
                if (targetHealth == null || targetHealth == damagedHealth)
                {
                    continue;
                }

                damagedHealth = targetHealth;
                targetHealth.TakeDamage(GetAttackDamage(action));
                hit.GetComponentInParent<PlayerDamageFlash>()?.PlayFlash();
            }
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

            return IsPlayerCurrentlyParryingInBox(attackBox);
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

            justParryBufferedAction = action;
            justParryValidUntil = Time.time + justParryEffectDuration;
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
            Vector2 velocity = rb2D.linearVelocity;
            velocity.x = facingDirection * moveSpeed;
            rb2D.linearVelocity = velocity;
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
            float centerX = bounds.center.x + facingDirection * (bounds.extents.x + size.x * 0.5f);
            return new AttackBox(new Vector2(centerX, bounds.center.y), size, 0f);
        }

        private bool IsPlayerCollider(Collider2D hit)
        {
            if (hit == null)
            {
                return false;
            }

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(playerTag) && hit.CompareTag(playerTag))
            {
                return true;
            }

            return hit.GetComponentInParent<PlayerHealth>() != null;
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

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = facingDirection < 0;
            }
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

            float attackDeltaX = attacker.transform.position.x - transform.position.x;
            if (Mathf.Abs(attackDeltaX) <= 0.05f)
            {
                return false;
            }

            int attackerSide = attackDeltaX >= 0f ? 1 : -1;
            return attackerSide != facingDirection;
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
            // ヒットストップ前にDownedへ入れて、同フレーム以降の攻撃更新を止める。
            state = BossState.Downed;
            StopMotion();
            HideAttackVisual();
            pendingAction = BossAction.None;
            visibleAction = BossAction.None;
            ClearJustParryBuffer();

            float previousTimeScale = Time.timeScale;
            if (useGlobalHitStop && hitStopDuration > 0f)
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

            yield return new WaitForSeconds(downDuration);

            if (state != BossState.Dead)
            {
                downCount = 0;
                downRoutineRunning = false;
                state = BossState.Recovery;
                stateTimer = 0f;
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

        private void UpdateEnragedVisual()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (!enraged)
            {
                spriteRenderer.color = defaultSpriteColor;
                return;
            }

            float pulse = enragedPulseSpeed <= 0f ? 1f : (Mathf.Sin(Time.time * enragedPulseSpeed) + 1f) * 0.5f;
            spriteRenderer.color = Color.Lerp(defaultSpriteColor, enragedColor, 0.45f + pulse * 0.35f);
        }

        private void Die()
        {
            state = BossState.Dead;
            encounterActive = false;
            StopMotion();
            HideAttackVisual();
            Destroy(gameObject);
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
            playerContactFilter.SetLayerMask(playerDetectionMask.value == 0 ? Physics2D.DefaultRaycastLayers : playerDetectionMask);
        }

        private void EnsureVisualObjects()
        {
            if (telegraphRenderer != null)
            {
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

            telegraphCollider.isTrigger = true;
            telegraphCollider.size = Vector2.one;
            telegraphCollider.enabled = false;
            telegraphRenderer.sprite = runtimeBoxSprite;
            telegraphRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 1;
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

    public sealed class LastBossAttackParryTarget : MonoBehaviour
    {
        // LastBossの範囲攻撃予兆をParryHitboxへ知らせるためのマーカーコンポーネント。
    }
}
