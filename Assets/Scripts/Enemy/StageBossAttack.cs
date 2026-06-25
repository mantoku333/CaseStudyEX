using System;
using Metroidvania.Player;
using Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameName.Enemy
{
    /// <summary>
    /// ステージボス専用の突進攻撃ロジック。
    /// ボスエリア開始時に外部から有効化されると、
    /// 振動→突進→クールダウンをループする。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageBossAttack : MonoBehaviour, IParryableAttack
    {
        [Header("Activation")]
        [SerializeField] private string playerTag = "Player";
        // true ならエリア起動を待たずに開始時から攻撃ループに入る。
        [SerializeField] private bool startActiveOnPlay = false;

        [Header("Wind Up")]
        [SerializeField, Min(0.1f)] private float vibrationDuration = 2f;
        [SerializeField, Min(0f)] private float vibrationAmplitude = 0.05f;
        [SerializeField, Min(1f)] private float vibrationFrequency = 35f;

        [Header("Charge")]
        [SerializeField, Min(0.1f)] private float chargeSpeed = 8f;
        [SerializeField, Min(0.05f)] private float chargeDistance = 3.6f;
        [SerializeField, Min(0f)] private float chargeCooldown = 0.4f;
        [SerializeField] private bool stopChargeOnWall = true;
        // 長距離部屋用の安全弁。false なら壁/詰まり判定のみで停止する。
        [SerializeField] private bool stopChargeByDistance = false;
        [SerializeField] private bool stopChargeWhenBlocked = true;
        [SerializeField, Min(0.001f)] private float blockedMoveThreshold = 0.01f;
        [SerializeField, Min(0.02f)] private float blockedStopDelay = 0.1f;
        [SerializeField, Min(0f)] private float chargeEndingLeadDistance = 1.2f;

        [Header("Camera Shake")]
        [SerializeField, FormerlySerializedAs("playShakeOnChargeStart")] private bool playShakeOnChargeImpact = true;
        [SerializeField, FormerlySerializedAs("chargeStartShakeForce"), Min(0f)] private float chargeImpactShakeForce = 1f;
        [SerializeField, FormerlySerializedAs("chargeStartShakeCount"), Min(1)] private int chargeImpactShakeCount = 2;

        [Header("Debug")]
        [SerializeField] private bool drawDebugGizmo = true;

        [Header("Parry")]//(中江)
        [SerializeField, Min(0f)] private float parryKnockbackDistance = 0.5f;
        [SerializeField, Min(0f)] private float parryContactDamageIgnoreDuration = 0.3f;

        private enum AttackState
        {
            Idle,
            Vibration,
            Charging,
            Cooldown
        }

        private EnemyController enemyController;
        private Collider2D bodyCollider;
        private Transform playerTransform;
        private readonly Collider2D[] bodyHitResults = new Collider2D[8];
        private ContactFilter2D playerContactFilter;
        private BossAreaController activeBossArea;

        private AttackState attackState = AttackState.Idle;
        private bool encounterActive;
        private bool playedPlayerImpactShakeThisCharge;
        private float stateTimer;
        private int chargeDirection;
        private float vibrationBaseX;
        private float vibrationElapsed;
        private float chargeStartX;
        private float previousChargeX;
        private float blockedTimer;
        private bool chargeEndingNotified;

        /// <summary>
        /// StageBossが実際に突進状態へ入った瞬間に通知する。
        /// Enemy_Tackleと同じ突進開始SEを鳴らすために使う。
        /// </summary>
        public event Action ChargeStarted;

        /// <summary>
        /// 実際の突進状態が終わる少し前、または即時終了の直前に通知する。
        /// Enemy_Tackle と同じ攻撃エフェクト再生タイミングで使う。
        /// </summary>
        public event Action ChargeEnding;

        public bool IsEncounterActive => encounterActive;
        public bool IsWindingUp => attackState == AttackState.Vibration;
        public bool IsCharging => attackState == AttackState.Charging;
        public bool IsCoolingDown => attackState == AttackState.Cooldown;

        private const float BossAreaEdgeImpactTolerance = 0.02f;

        private void Awake()
        {
            enemyController = GetComponent<EnemyController>();
            bodyCollider = GetComponent<Collider2D>();
            if (enemyController == null)
            {
                Debug.LogWarning("StageBossAttack requires EnemyController on the same GameObject.", this);
                enabled = false;
                return;
            }

            BuildPlayerContactFilter();
            encounterActive = startActiveOnPlay;
        }

        private void OnValidate()
        {
            BuildPlayerContactFilter();
        }

        private void OnEnable()
        {
            BossAreaController.EncounterStarted -= HandleBossAreaEncounterStarted;
            BossAreaController.EncounterStarted += HandleBossAreaEncounterStarted;
            BossAreaController.EncounterCompleted -= HandleBossAreaEncounterEnded;
            BossAreaController.EncounterCompleted += HandleBossAreaEncounterEnded;
            BossAreaController.EncounterReset -= HandleBossAreaEncounterEnded;
            BossAreaController.EncounterReset += HandleBossAreaEncounterEnded;
        }

        private void OnDisable()
        {
            BossAreaController.EncounterStarted -= HandleBossAreaEncounterStarted;
            BossAreaController.EncounterCompleted -= HandleBossAreaEncounterEnded;
            BossAreaController.EncounterReset -= HandleBossAreaEncounterEnded;

            if (enemyController == null)
            {
                return;
            }

            encounterActive = false;
            activeBossArea = null;
            attackState = AttackState.Idle;
            stateTimer = 0f;
            chargeEndingNotified = false;
            enemyController.PauseMovement(false);
            enemyController.StopHorizontalMotion();
        }

        public void ActivateEncounter()
        {
            if (enemyController == null)
            {
                return;
            }

            encounterActive = true;
            if (attackState == AttackState.Idle)
            {
                EnterVibrationState();
            }
        }

        public void DeactivateEncounter()
        {
            if (enemyController == null)
            {
                return;
            }

            encounterActive = false;
            attackState = AttackState.Idle;
            stateTimer = 0f;
            blockedTimer = 0f;
            chargeEndingNotified = false;
            playedPlayerImpactShakeThisCharge = false;
            enemyController.StopHorizontalMotion();
            enemyController.PauseMovement(false);
            enemyController.ResetPatrolOrigin();
        }

        private void FixedUpdate()
        {
            if (!encounterActive)
            {
                return;
            }

            if (EnemyGameplayPause.IsPaused())
            {
                enemyController?.StopHorizontalMotion();
                return;
            }

            // ボスの攻撃状態マシン。
            switch (attackState)
            {
                case AttackState.Idle:
                    EnterVibrationState();
                    break;
                case AttackState.Vibration:
                    UpdateVibrationState();
                    break;
                case AttackState.Charging:
                    UpdateChargingState();
                    break;
                case AttackState.Cooldown:
                    UpdateCooldownState();
                    break;
            }
        }

        private void EnterVibrationState()
        {
            // 予備動作中は通常巡回を止め、向きはプレイヤー位置から確定する。
            enemyController.PauseMovement(true);
            enemyController.StopHorizontalMotion();

            attackState = AttackState.Vibration;
            stateTimer = vibrationDuration;
            vibrationElapsed = 0f;
            chargeDirection = ResolveChargeDirectionTowardsPlayer();
            enemyController.FaceDirection(chargeDirection);
            vibrationBaseX = enemyController.CurrentX;
        }

        private void UpdateVibrationState()
        {
            stateTimer -= Time.fixedDeltaTime;
            vibrationElapsed += Time.fixedDeltaTime;

            float offset = Mathf.Sin(vibrationElapsed * vibrationFrequency * Mathf.PI * 2f) * vibrationAmplitude;
            enemyController.FaceDirection(chargeDirection);
            enemyController.SetHorizontalPosition(vibrationBaseX + offset);
            enemyController.StopHorizontalMotion();

            if (stateTimer <= 0f)
            {
                BeginCharge();
            }
        }

        private void BeginCharge()
        {
            // 振動でずれた位置を戻してから突進開始。
            enemyController.SetHorizontalPosition(vibrationBaseX);
            enemyController.FaceDirection(chargeDirection);

            chargeStartX = enemyController.CurrentX;
            previousChargeX = chargeStartX;
            blockedTimer = 0f;
            playedPlayerImpactShakeThisCharge = false;
            chargeEndingNotified = false;
            attackState = AttackState.Charging;
            // StageBossもEnemy_Tackleと同じSEを、この突進開始時に1回だけ鳴らす。
            ChargeStarted?.Invoke();
        }

        private void PlayChargeImpactShake()
        {
            if (!playShakeOnChargeImpact || chargeImpactShakeForce <= 0f)
            {
                return;
            }

            CameraManager cameraManager = CameraManager.Instance;
            if (cameraManager == null)
            {
                cameraManager = FindFirstObjectByType<CameraManager>(FindObjectsInactive.Include);
            }

            if (cameraManager == null)
            {
                return;
            }

            Vector3 shakeDirection = Vector3.right * chargeDirection;
            int shakeCount = Mathf.Max(1, chargeImpactShakeCount);
            cameraManager.PlayShakePulses(chargeImpactShakeForce, shakeDirection, shakeCount);
        }

        private void UpdateChargingState()
        {
            // 仕様: プレイヤー接触では停止しない。
            enemyController.FaceDirection(chargeDirection);

            TryPlayPlayerImpactShake();

            if (stopChargeOnWall && enemyController.IsWallAhead())
            {
                PlayChargeImpactShake();
                EnterCooldownState();
                return;
            }

            if (HasReachedBossAreaEdge())
            {
                PlayChargeImpactShake();
                EnterCooldownState();
                return;
            }

            enemyController.SetHorizontalVelocity(chargeSpeed);

            if (stopChargeWhenBlocked && IsChargeBlockedThisFrame())
            {
                EnterCooldownState();
                return;
            }

            NotifyChargeEndingIfCloseToDistanceLimit();

            if (stopChargeByDistance && HasReachedChargeDistance())
            {
                EnterCooldownState();
            }
        }

        private void TryPlayPlayerImpactShake()
        {
            if (playedPlayerImpactShakeThisCharge || !IsPlayerTouchingBody())
            {
                return;
            }

            playedPlayerImpactShakeThisCharge = true;
            PlayChargeImpactShake();
        }

        private bool IsPlayerTouchingBody()
        {
            if (bodyCollider == null)
            {
                return false;
            }

            Bounds bounds = bodyCollider.bounds;
            Vector2 overlapSize = new Vector2(
                Mathf.Max(0.01f, bounds.size.x * 0.95f),
                Mathf.Max(0.01f, bounds.size.y * 0.95f));

            int hitCount = Physics2D.OverlapBox(bounds.center, overlapSize, 0f, playerContactFilter, bodyHitResults);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = bodyHitResults[i];
                if (hit != null && !hit.isTrigger && IsPlayerCollider(hit))
                {
                    return true;
                }
            }

            return false;
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

            if (!PlayerBodyColliderUtility.TryGetPlayerBodyFromCollider(hit, out PlayerHealth playerHealth, out _))
            {
                return false;
            }

            return string.IsNullOrEmpty(playerTag) ||
                   hit.CompareTag(playerTag) ||
                   playerHealth.CompareTag(playerTag) ||
                   (playerHealth.transform.root != null && playerHealth.transform.root.CompareTag(playerTag));
        }

        private void BuildPlayerContactFilter()
        {
            ContactFilter2D filter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = true
            };
            filter.SetLayerMask(PlayerBodyColliderUtility.GetPlayerBodyLayerMask());
            playerContactFilter = filter;
        }

        private bool HasReachedBossAreaEdge()
        {
            if (activeBossArea == null || bodyCollider == null)
            {
                return false;
            }

            if (!activeBossArea.TryGetActiveBossHorizontalConfinementBounds(out Bounds bounds))
            {
                return false;
            }

            Bounds bodyBounds = bodyCollider.bounds;
            if (chargeDirection >= 0)
            {
                return bodyBounds.max.x >= bounds.max.x - BossAreaEdgeImpactTolerance;
            }

            return bodyBounds.min.x <= bounds.min.x + BossAreaEdgeImpactTolerance;
        }

        private bool IsChargeBlockedThisFrame()
        {
            // 移動量が閾値以下のフレームが続く場合は詰まりとみなして停止する。
            float currentX = enemyController.CurrentX;
            float movedDistance = Mathf.Abs(currentX - previousChargeX);

            if (movedDistance <= blockedMoveThreshold)
            {
                blockedTimer += Time.fixedDeltaTime;
            }
            else
            {
                blockedTimer = 0f;
            }

            previousChargeX = currentX;
            return blockedTimer >= blockedStopDelay;
        }

        private bool HasReachedChargeDistance()
        {
            float targetDistance = Mathf.Max(0f, chargeDistance);
            if (targetDistance <= 0f)
            {
                return true;
            }

            float traveledDistance = Mathf.Abs(enemyController.CurrentX - chargeStartX);
            return traveledDistance >= targetDistance;
        }

        private void NotifyChargeEndingIfCloseToDistanceLimit()
        {
            if (chargeEndingNotified || !stopChargeByDistance)
            {
                return;
            }

            float leadDistance = Mathf.Max(0f, chargeEndingLeadDistance);
            if (leadDistance <= 0f)
            {
                return;
            }

            float targetDistance = Mathf.Max(0f, chargeDistance);
            if (targetDistance <= leadDistance)
            {
                return;
            }

            float traveledDistance = Mathf.Abs(enemyController.CurrentX - chargeStartX);
            if (traveledDistance >= targetDistance - leadDistance)
            {
                NotifyChargeEnding();
            }
        }

        private void NotifyChargeEnding()
        {
            if (chargeEndingNotified)
            {
                return;
            }

            chargeEndingNotified = true;
            ChargeEnding?.Invoke();
        }

        private void EnterCooldownState()
        {
            bool wasCharging = attackState == AttackState.Charging;
            if (wasCharging)
            {
                NotifyChargeEnding();
            }

            attackState = AttackState.Cooldown;
            stateTimer = chargeCooldown;
            enemyController.StopHorizontalMotion();
        }

        private void UpdateCooldownState()
        {
            stateTimer -= Time.fixedDeltaTime;
            enemyController.StopHorizontalMotion();

            if (stateTimer > 0f)
            {
                return;
            }

            attackState = AttackState.Idle;
        }

        private int ResolveChargeDirectionTowardsPlayer()
        {
            // プレイヤーが見つからない場合は現在の向きを維持する。
            int fallbackDirection = enemyController.FacingDirection >= 0 ? 1 : -1;
            if (!TryGetPlayerTransform(out Transform player))
            {
                return fallbackDirection;
            }

            float deltaX = player.position.x - transform.position.x;
            if (Mathf.Abs(deltaX) <= 0.05f)
            {
                return fallbackDirection;
            }

            return deltaX >= 0f ? 1 : -1;
        }

        private bool TryGetPlayerTransform(out Transform player)
        {
            if (playerTransform != null)
            {
                player = playerTransform;
                return true;
            }

            player = null;

            if (string.IsNullOrWhiteSpace(playerTag))
            {
                return false;
            }

            GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
            if (playerObject == null)
            {
                return false;
            }

            // 一度見つけた参照をキャッシュして毎フレーム検索を避ける。
            playerTransform = playerObject.transform;
            player = playerTransform;
            return true;
        }

        private void HandleBossAreaEncounterStarted(BossAreaController bossArea)
        {
            if (bossArea == null || bossArea.StageBossAttack != this)
            {
                return;
            }

            activeBossArea = bossArea;
        }

        private void HandleBossAreaEncounterEnded(BossAreaController bossArea)
        {
            if (bossArea == activeBossArea)
            {
                activeBossArea = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmo)
            {
                return;
            }

            int direction = chargeDirection != 0 ? chargeDirection : 1;
            float previewDistance = Mathf.Max(0.2f, chargeDistance);

            Vector3 start = transform.position;
            Vector3 end = start + (Vector3.right * direction * previewDistance);

            Gizmos.color = new Color(1f, 0.3f, 0.15f, 0.9f);
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(end, 0.12f);
        }

        public bool IsParryable => attackState == AttackState.Charging && !EnemyGameplayPause.IsPaused();

        /// <summary>
        /// パリィされたときの処理。
        /// 攻撃状態に関わらず呼び出される可能性があるが、突進中以外は無視する。  
        /// </summary>
        public void StopByParry()
        {
            if (attackState != AttackState.Charging)
            {
                return;
            }

            Debug.Log("StageBossの突進をパリィしました");

            if (enemyController != null)
            {
                enemyController.IgnoreContactDamage(0.3f);
                enemyController.StopHorizontalMotion();
            }

            EnterCooldownState();
        }
    }
}
