using Player;
using UnityEngine;

namespace GameName.Enemy
{
    /// <summary>
    /// タックル敵専用の突進攻撃ロジック。
    /// EnemyController と分離しているため、
    /// アタッチ／デタッチだけで攻撃挙動を差し替えられる。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyTackleAttack : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private LayerMask playerDetectionMask;
        [SerializeField] private Vector2 viewHitboxSize = new Vector2(3f, 1.25f);
        [SerializeField] private float viewForwardOffset = -0.6f;
        [SerializeField] private float viewVerticalOffset = 0f;

        [Header("Wind Up")]
        [SerializeField, Min(0.1f)] private float vibrationDuration = 2f;
        [SerializeField, Min(0f)] private float vibrationAmplitude = 0.05f;
        [SerializeField, Min(1f)] private float vibrationFrequency = 35f;

        [Header("Charge")]
        [SerializeField, Min(0.1f)] private float chargeSpeed = 8f;
        [SerializeField, Min(0.05f)] private float chargeDistance = 3.6f;
        [SerializeField, Min(0f)] private float chargeCooldown = 0.4f;
        [SerializeField] private bool stopChargeOnWall = true;
        [SerializeField] private bool stopChargeOnPlayerHit = true;
        [SerializeField] private bool stopChargeWhenBlocked = true;
        [SerializeField, Min(0.001f)] private float blockedMoveThreshold = 0.01f;
        [SerializeField, Min(0.02f)] private float blockedStopDelay = 0.1f;

        [Header("Debug")]
        [SerializeField] private bool drawViewGizmo = true;

        private enum AttackState
        {
            Idle,
            Vibration,
            Charging,
            Cooldown
        }

        private EnemyController enemyController;
        private Collider2D bodyCollider;
        private readonly Collider2D[] viewHitResults = new Collider2D[8];
        private readonly Collider2D[] bodyHitResults = new Collider2D[8];
        private ContactFilter2D playerContactFilter;

        private AttackState attackState = AttackState.Idle;
        private float stateTimer;
        private int chargeDirection;
        private float vibrationBaseX;
        private float vibrationElapsed;
        private float chargeStartX;
        private float previousChargeX;
        private float blockedTimer;

        /// <summary>
        /// 依存コンポーネントを取得し、検知マスクの初期化を行う。
        /// </summary>
        private void Awake()
        {
            enemyController = GetComponent<EnemyController>();
            bodyCollider = GetComponent<Collider2D>();

            if (enemyController == null)
            {
                Debug.LogWarning("EnemyTackleAttack requires EnemyController on the same GameObject.", this);
                enabled = false;
                return;
            }

            if (playerDetectionMask.value == 0)
            {
                playerDetectionMask = Physics2D.DefaultRaycastLayers;
            }

            BuildPlayerContactFilter();
        }

        /// <summary>
        /// インスペクター値変更時にプレイヤー判定フィルターを再構築する。
        /// </summary>
        private void OnValidate()
        {
            BuildPlayerContactFilter();
        }

        /// <summary>
        /// 無効化時に移動停止状態を解除し、内部状態をリセットする。
        /// </summary>
        private void OnDisable()
        {
            if (enemyController == null)
            {
                return;
            }

            enemyController.PauseMovement(false);
            enemyController.StopHorizontalMotion();
            attackState = AttackState.Idle;
            stateTimer = 0f;
        }

        /// <summary>
        /// 攻撃状態マシンを更新する。
        /// </summary>
        private void FixedUpdate()
        {
            switch (attackState)
            {
                case AttackState.Idle:
                    UpdateIdleState();
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

        /// <summary>
        /// 待機状態。プレイヤーを視界内で検知したら予備動作へ遷移する。
        /// </summary>
        private void UpdateIdleState()
        {
            if (!IsPlayerInView())
            {
                return;
            }

            EnterVibrationState();
        }

        /// <summary>
        /// 予備動作（振動）状態へ遷移し、巡回移動を停止する。
        /// </summary>
        private void EnterVibrationState()
        {
            enemyController.PauseMovement(true);
            enemyController.StopHorizontalMotion();

            attackState = AttackState.Vibration;
            stateTimer = vibrationDuration;
            vibrationElapsed = 0f;
            chargeDirection = GetFacingDirection();
            vibrationBaseX = enemyController.CurrentX;
        }

        /// <summary>
        /// 振動演出を更新し、時間経過で突進状態へ移行する。
        /// </summary>
        private void UpdateVibrationState()
        {
            stateTimer -= Time.fixedDeltaTime;
            vibrationElapsed += Time.fixedDeltaTime;

            float offset = Mathf.Sin(vibrationElapsed * vibrationFrequency * Mathf.PI * 2f) * vibrationAmplitude;
            enemyController.SetHorizontalPosition(vibrationBaseX + offset);
            enemyController.StopHorizontalMotion();

            if (stateTimer <= 0f)
            {
                BeginCharge();
            }
        }

        /// <summary>
        /// 振動位置を基準に向きを固定して突進を開始する。
        /// </summary>
        private void BeginCharge()
        {
            enemyController.SetHorizontalPosition(vibrationBaseX);
            enemyController.FaceDirection(chargeDirection);
            chargeStartX = enemyController.CurrentX;
            previousChargeX = chargeStartX;
            blockedTimer = 0f;

            attackState = AttackState.Charging;
        }

        /// <summary>
        /// 突進中の更新処理。
        /// 壁接触・プレイヤー接触・到達距離のいずれかで終了する。
        /// </summary>
        private void UpdateChargingState()
        {
            if (stopChargeOnPlayerHit && IsPlayerTouchingBody())
            {
                EnterCooldownState();
                return;
            }

            if (stopChargeOnWall && enemyController.IsWallAhead())
            {
                EnterCooldownState();
                return;
            }

            enemyController.SetHorizontalVelocity(chargeSpeed);

            if (stopChargeOnPlayerHit && IsPlayerTouchingBody())
            {
                EnterCooldownState();
                return;
            }

            if (stopChargeWhenBlocked && IsChargeBlockedThisFrame())
            {
                EnterCooldownState();
                return;
            }

            if (HasReachedChargeDistance())
            {
                EnterCooldownState();
            }
        }

        /// <summary>
        /// 突進中にほとんど前進できていない状態が一定時間続いたかを判定する。
        /// </summary>
        /// <returns>進行不能状態なら true。</returns>
        private bool IsChargeBlockedThisFrame()
        {
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

        /// <summary>
        /// 設定した突進距離に到達したかを判定する。
        /// </summary>
        /// <returns>到達済みなら true。</returns>
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

        /// <summary>
        /// クールダウン状態へ遷移し、その場で停止する。
        /// </summary>
        private void EnterCooldownState()
        {
            attackState = AttackState.Cooldown;
            stateTimer = chargeCooldown;
            enemyController.StopHorizontalMotion();
        }

        /// <summary>
        /// クールダウン時間を消化し、終了後に巡回移動を再開する。
        /// </summary>
        private void UpdateCooldownState()
        {
            stateTimer -= Time.fixedDeltaTime;
            enemyController.StopHorizontalMotion();

            if (stateTimer > 0f)
            {
                return;
            }

            attackState = AttackState.Idle;
            enemyController.PauseMovement(false);
            enemyController.ResetPatrolOrigin();
        }

        /// <summary>
        /// 前方視界ヒットボックスでプレイヤーを検知する。
        /// </summary>
        /// <returns>視界内にプレイヤーがいれば true。</returns>
        private bool IsPlayerInView()
        {
            Vector2 size = GetViewHitboxSize();
            Vector2 center = GetViewHitboxCenter(size);

            int hitCount = Physics2D.OverlapBox(center, size, 0f, playerContactFilter, viewHitResults);
            for (int i = 0; i < hitCount; i++)
            {
                if (IsPlayerCollider(viewHitResults[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 敵本体の重なり判定でプレイヤー接触を確認する。
        /// </summary>
        /// <returns>接触中なら true。</returns>
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

        /// <summary>
        /// プレイヤー検知用の ContactFilter2D を構築する。
        /// </summary>
        private void BuildPlayerContactFilter()
        {
            if (playerDetectionMask.value == 0)
            {
                playerDetectionMask = Physics2D.DefaultRaycastLayers;
            }

            playerContactFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = true
            };
            playerContactFilter.SetLayerMask(playerDetectionMask);
        }

        /// <summary>
        /// 与えられた Collider2D がプレイヤー由来かを判定する。
        /// </summary>
        /// <param name="hit">判定対象コライダー。</param>
        /// <returns>プレイヤーなら true。</returns>
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

        /// <summary>
        /// 現在の向きを正規化して返す。
        /// </summary>
        /// <returns>右: 1 / 左: -1。</returns>
        private int GetFacingDirection()
        {
            int facing = enemyController != null ? enemyController.FacingDirection : 1;
            return facing >= 0 ? 1 : -1;
        }

        /// <summary>
        /// 視界ヒットボックスのサイズを最小値付きで返す。
        /// </summary>
        private Vector2 GetViewHitboxSize()
        {
            return new Vector2(
                Mathf.Max(0.1f, viewHitboxSize.x),
                Mathf.Max(0.1f, viewHitboxSize.y));
        }

        /// <summary>
        /// 向き・オフセットを考慮した視界ヒットボックス中心座標を返す。
        /// </summary>
        /// <param name="size">ヒットボックスサイズ。</param>
        private Vector2 GetViewHitboxCenter(Vector2 size)
        {
            float forwardDistance = (size.x * 0.5f) + viewForwardOffset;
            float centerX = transform.position.x + (forwardDistance * GetFacingDirection());
            float centerY = (bodyCollider != null ? bodyCollider.bounds.center.y : transform.position.y) + viewVerticalOffset;

            return new Vector2(centerX, centerY);
        }

        /// <summary>
        /// 選択中に視界ヒットボックスを Gizmo で表示する。
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!drawViewGizmo)
            {
                return;
            }

            Collider2D colliderForPreview = bodyCollider != null ? bodyCollider : GetComponent<Collider2D>();
            int facing = GetFacingDirection();
            Vector2 size = GetViewHitboxSize();

            float forwardDistance = (size.x * 0.5f) + viewForwardOffset;
            float centerY = (colliderForPreview != null ? colliderForPreview.bounds.center.y : transform.position.y) + viewVerticalOffset;
            Vector3 center = new Vector3(
                transform.position.x + (forwardDistance * facing),
                centerY,
                transform.position.z);

            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
