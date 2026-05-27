using Player;
using Metroidvania.Player;
using System;
using UnityEngine;

namespace GameName.Enemy
{
    /// <summary>
    /// シンプルな敵の巡回移動と接触ダメージを管理するクラス
    /// </summary>
    public class EnemyController : MonoBehaviour, IAttackReceiver, IBossHealthSource
    {
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float patrolDistance = 2f;
        [SerializeField] private int damageToPlayer = 1;
        [SerializeField, Min(1)] private int maxHealth = 1;

        [Header("Turn Check")]
        [SerializeField, Min(0.01f)] private float wallCheckDistance = 0.15f;
        [SerializeField, Min(0.01f)] private float edgeCheckDistance = 0.35f;
        [SerializeField, Min(0f)] private float edgeCheckForwardOffset = 0.1f;
        [SerializeField] private LayerMask stageLayerMask;
        // シャッター壁は Default レイヤーに置かれることがあるため、通常の床レイヤーとは別に判定する。
        [SerializeField] private bool treatShutterWallsAsWalls = true;
        [SerializeField] private bool flipSpriteOnTurn = true;

        [Header("Enemy Collision")]
        [SerializeField, Min(0f)] private float enemyCollisionTurnCooldown = 0.15f;

        private const float EnemyCollisionSideNormalThreshold = 0.35f;
        private Vector3 startPosition;
        private int moveDirection = 1;
        private bool movementPaused;
        private Rigidbody2D rigidbody2D;
        private Collider2D bodyCollider;
        private SpriteRenderer spriteRenderer;
        private EnemyDamageFlash damageFlash;
        private int currentHealth;
        private float nextEnemyCollisionTurnTime;
        // シャッター壁の子ブロックを検出するための一時バッファ。
        private readonly RaycastHit2D[] wallCheckHits = new RaycastHit2D[8];
        private ContactFilter2D shutterWallContactFilter;

        public event Action EnemyCollisionTurned;
        /// <summary>
        /// 敵がDestroyされる直前に通知する。死亡SEなど、破棄前に必要な処理で使う。
        /// </summary>
        public event Action Died;
        public event Action<int, int> HealthChanged;

        /// <summary>
        /// 現在の向き。右が 1、左が -1。
        /// </summary>
        public int FacingDirection => moveDirection;

        /// <summary>
        /// 現在の X 座標（Rigidbody2D がある場合は物理座標）。
        /// </summary>
        public float CurrentX => rigidbody2D != null ? rigidbody2D.position.x : transform.position.x;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => Mathf.Max(1, maxHealth);

        private float ignoreContactDamageUntilTime;  //接触ダメージを無効にする時間

        /// <summary>
        /// 必要コンポーネントの取得と、未設定レイヤーマスクの補完を行う。
        /// </summary>
        private void Awake()
        {
            rigidbody2D = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            damageFlash = GetComponentInChildren<EnemyDamageFlash>(true);
            currentHealth = MaxHealth;

            if (stageLayerMask.value == 0)
            {
                stageLayerMask = BuildDefaultStageMask();
            }

            BuildShutterWallContactFilter();
        }

        /// <summary>
        /// 初期位置を保存
        /// </summary>
        private void Start()
        {
            startPosition = transform.position;
            ApplyFacing();
        }

        /// <summary>
        /// 左右の簡易巡回を行う
        /// </summary>
        private void FixedUpdate()
        {
            if (movementPaused)
            {
                // 自動巡回のみ停止し、速度制御は攻撃側スクリプトに委譲する
                return;
            }

            bool turnedByEnvironment = false;

            if (ShouldTurnAround())
            {
                TurnAround();
                turnedByEnvironment = true;
            }

            Move(moveSpeed);

            // 開始位置から一定距離離れたら反転する
            if (!turnedByEnvironment && patrolDistance > 0f)
            {
                float distanceFromStart = transform.position.x - startPosition.x;
                bool reachedRightLimit = moveDirection > 0 && distanceFromStart >= patrolDistance;
                bool reachedLeftLimit = moveDirection < 0 && distanceFromStart <= -patrolDistance;
                if (reachedRightLimit || reachedLeftLimit)
                {
                    TurnAround();
                }
            }
        }

        /// <summary>
        /// 外部スクリプトから移動を一時停止／再開する。
        /// </summary>
        /// <param name="paused">true で停止、false で再開。</param>
        public void PauseMovement(bool paused)
        {
            movementPaused = paused;
            if (paused)
            {
                StopHorizontalMotion();
            }
        }

        /// <summary>
        /// 向きを強制設定する。
        /// </summary>
        /// <param name="direction">0 以上で右、負値で左。</param>
        public void FaceDirection(int direction)
        {
            moveDirection = direction >= 0 ? 1 : -1;
            ApplyFacing();
        }

        /// <summary>
        /// 巡回距離判定の基準位置を現在位置に更新する。
        /// </summary>
        public void ResetPatrolOrigin()
        {
            startPosition = transform.position;
        }

        /// <summary>
        /// 障害物接触時に向きを反転し、巡回基準位置を現在地に戻す。
        /// </summary>
        public void TurnAroundFromObstacle()
        {
            FaceDirection(-moveDirection);
            StopHorizontalMotion();
            ResetPatrolOrigin();
        }

        /// <summary>
        /// 障害物から離れる向きへ方向転換し、巡回基準位置を現在地に戻す。
        /// </summary>
        /// <param name="direction">右: 1 / 左: -1。</param>
        public void TurnAwayFromObstacle(int direction)
        {
            FaceDirection(direction);
            StopHorizontalMotion();
            ResetPatrolOrigin();
        }

        /// <summary>
        /// 現在の向きに対して水平速度を設定する。
        /// </summary>
        /// <param name="speed">移動速度（絶対値で使用）。</param>
        public void SetHorizontalVelocity(float speed)
        {
            Move(Mathf.Abs(speed));
        }

        /// <summary>
        /// 水平方向の移動を即座に停止する。
        /// </summary>
        public void StopHorizontalMotion()
        {
            if (rigidbody2D == null)
            {
                return;
            }

            Vector2 velocity = rigidbody2D.linearVelocity;
            velocity.x = 0f;
            rigidbody2D.linearVelocity = velocity;
        }

        /// <summary>
        /// X 座標のみを指定値へ移動する。
        /// </summary>
        /// <param name="x">移動先 X 座標。</param>
        public void SetHorizontalPosition(float x)
        {
            if (rigidbody2D != null)
            {
                Vector2 rigidbodyPosition = rigidbody2D.position;
                rigidbodyPosition.x = x;
                rigidbody2D.MovePosition(rigidbodyPosition);
                return;
            }

            Vector3 worldPosition = transform.position;
            worldPosition.x = x;
            transform.position = worldPosition;
        }

        /// <summary>
        /// 進行方向の正面に壁があるかを判定する。
        /// </summary>
        /// <returns>壁がある場合は true。</returns>
        public bool IsWallAhead()
        {
            if (bodyCollider == null)
            {
                return false;
            }

            Bounds bounds = bodyCollider.bounds;
            float originX = moveDirection > 0f
                ? bounds.max.x + 0.02f
                : bounds.min.x - 0.02f;
            Vector2 origin = new Vector2(originX, bounds.center.y);

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.right * moveDirection, wallCheckDistance, stageLayerMask);
            if (hit.collider != null)
            {
                return true;
            }

            // StageBoss と Enemy_Tackle はこの共通判定を使うため、ここでシャッター壁も壁扱いにする。
            return treatShutterWallsAsWalls && IsShutterWallAhead(origin);
        }

        /// <summary>
        /// 向き方向へ移動する内部処理。
        /// </summary>
        /// <param name="speed">移動速度。</param>
        private void Move(float speed)
        {
            if (rigidbody2D != null)
            {
                Vector2 velocity = rigidbody2D.linearVelocity;
                velocity.x = moveDirection * speed;
                rigidbody2D.linearVelocity = velocity;
                return;
            }

            transform.Translate(Vector3.right * moveDirection * speed * Time.fixedDeltaTime);
        }

        /// <summary>
        /// 壁または足場端で折り返すべきか判定する。
        /// </summary>
        /// <returns>折り返しが必要な場合は true。</returns>
        private bool ShouldTurnAround()
        {
            if (bodyCollider == null)
            {
                return false;
            }

            return IsWallAhead() || IsEdgeAhead();
        }

        /// <summary>
        /// 進行方向の足元に地面がない（崖端）かを判定する。
        /// </summary>
        /// <returns>崖端なら true。</returns>
        private bool IsEdgeAhead()
        {
            Bounds bounds = bodyCollider.bounds;
            float originX = moveDirection > 0f
                ? bounds.max.x + edgeCheckForwardOffset
                : bounds.min.x - edgeCheckForwardOffset;
            Vector2 origin = new Vector2(originX, bounds.min.y + 0.05f);

            RaycastHit2D groundHit = Physics2D.Raycast(origin, Vector2.down, edgeCheckDistance, stageLayerMask);
            return groundHit.collider == null;
        }

        /// <summary>
        /// ステージ判定に使うデフォルトレイヤーマスクを構築する。
        /// </summary>
        /// <returns>Ground / FallThroughFloor を含むマスク。</returns>
        private static LayerMask BuildDefaultStageMask()
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            int fallThroughLayer = LayerMask.NameToLayer("FallThroughFloor");

            int mask = 0;
            if (groundLayer >= 0)
            {
                mask |= 1 << groundLayer;
            }

            if (fallThroughLayer >= 0)
            {
                mask |= 1 << fallThroughLayer;
            }

            return mask == 0 ? Physics2D.DefaultRaycastLayers : mask;
        }

        private void BuildShutterWallContactFilter()
        {
            // レイヤーに依存せず ShutterWallBlockRise 配下の非トリガーコライダーだけを後段で拾う。
            shutterWallContactFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = false
            };
            shutterWallContactFilter.SetLayerMask(Physics2D.DefaultRaycastLayers);
        }

        private bool IsShutterWallAhead(Vector2 origin)
        {
            // シャッター壁の各ブロックは子オブジェクトなので、親に ShutterWallBlockRise があるかで判定する。
            int hitCount = Physics2D.Raycast(
                origin,
                Vector2.right * moveDirection,
                shutterWallContactFilter,
                wallCheckHits,
                wallCheckDistance);

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = wallCheckHits[i].collider;
                if (hitCollider == null || IsOwnCollider(hitCollider))
                {
                    continue;
                }

                if (hitCollider.GetComponentInParent<ShutterWallBlockRise>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsOwnCollider(Collider2D hitCollider)
        {
            // 自分自身や子オブジェクトのコライダーを壁として誤検出しないようにする。
            return hitCollider == bodyCollider ||
                   hitCollider.transform == transform ||
                   hitCollider.transform.IsChildOf(transform);
        }

        /// <summary>
        /// 向きを反転して見た目の向きも更新する。
        /// </summary>
        private void TurnAround()
        {
            moveDirection *= -1;
            ApplyFacing();
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            TryTurnAroundFromEnemyCollision(collision);
        }

        private bool TryTurnAroundFromEnemyCollision(Collision2D collision)
        {
            if (!TryGetEnemyCollisionTurnDirection(collision, out int turnDirection))
            {
                return false;
            }

            if (Time.time < nextEnemyCollisionTurnTime)
            {
                return false;
            }

            nextEnemyCollisionTurnTime = Time.time + enemyCollisionTurnCooldown;
            TurnAwayFromObstacle(turnDirection);
            EnemyCollisionTurned?.Invoke();
            return true;
        }

        private bool TryGetEnemyCollisionTurnDirection(Collision2D collision, out int turnDirection)
        {
            turnDirection = moveDirection;

            if (collision == null || collision.gameObject == null)
            {
                return false;
            }

            EnemyController otherEnemy = collision.gameObject.GetComponentInParent<EnemyController>();
            if (otherEnemy == null || otherEnemy == this)
            {
                return false;
            }

            if (!TryGetSideContactDirection(collision, out turnDirection))
            {
                return false;
            }

            if (TryGetDirectionAwayFromOtherEnemy(otherEnemy, out int awayDirection))
            {
                turnDirection = awayDirection;
            }

            return turnDirection != moveDirection;
        }

        private bool TryGetSideContactDirection(Collision2D collision, out int direction)
        {
            direction = moveDirection;

            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint2D contact = collision.GetContact(i);
                float normalX = contact.normal.x;
                if (Mathf.Abs(normalX) < EnemyCollisionSideNormalThreshold)
                {
                    continue;
                }

                direction = normalX > 0f ? 1 : -1;
                return true;
            }

            return false;
        }

        private bool TryGetDirectionAwayFromOtherEnemy(EnemyController otherEnemy, out int direction)
        {
            direction = moveDirection;

            float deltaX = CurrentX - otherEnemy.CurrentX;
            if (Mathf.Abs(deltaX) <= 0.05f)
            {
                return false;
            }

            direction = deltaX > 0f ? 1 : -1;
            return true;
        }

        /// <summary>
        /// 現在の向きに合わせてスプライトの左右反転を適用する。
        /// </summary>
        private void ApplyFacing()
        {
            if (spriteRenderer != null && flipSpriteOnTurn)
            {
                spriteRenderer.flipX = moveDirection > 0;
            }
        }

        /// <summary>
        /// プレイヤーに接触した際にダメージを与える
        /// </summary>
        /// <param name="collision">衝突情報</param>
        private void OnCollisionEnter2D(Collision2D collision)
        {
           bool shouldIgnoreContactDamage = Time.time < ignoreContactDamageUntilTime;

            if (shouldIgnoreContactDamage)
            {
                Debug.Log("パリィ後なので接触ダメージ無効");
            }
            else
            {
                UmbrellaParryController umbrellaParryController =
                    collision.gameObject.GetComponentInParent<UmbrellaParryController>();

                if (umbrellaParryController != null && umbrellaParryController.IsParrying())
                {
                    Debug.Log("パリィ中なので敵ダメージ無効");
                }
                else if (TryGetPlayerBodyCollision(collision, out PlayerHealth playerHealth) &&
                        playerHealth.TryTakeDamage(damageToPlayer))
                {
                    Debug.Log("敵接触ダメージ");

                    PlayerDamageFlash damageFlash = playerHealth.GetComponent<PlayerDamageFlash>();
                    if (damageFlash == null)
                    {
                        damageFlash = playerHealth.GetComponentInChildren<PlayerDamageFlash>(true);
                    }

                    damageFlash?.PlayFlashForced();
                }
            }

            TryTurnAroundFromEnemyCollision(collision);

        }

        private static bool TryGetPlayerBodyCollision(Collision2D collision, out PlayerHealth playerHealth)
        {
            playerHealth = null;

            if (collision == null)
            {
                return false;
            }

            // Collision2D のどちら側にプレイヤー本体が入っていても拾えるよう、両方の collider を確認する。
            return PlayerBodyColliderUtility.TryGetPlayerBodyFromCollider(
                       collision.collider,
                       out playerHealth,
                       out _) ||
                   PlayerBodyColliderUtility.TryGetPlayerBodyFromCollider(
                       collision.otherCollider,
                       out playerHealth,
                       out _);
        }

        //--------------ダメージ関連------------------

        public void TakeDamage(int damage)
        {
            if (damage <= 0)
            {
                return;
            }

            damageFlash?.PlayFlash();

            currentHealth = Mathf.Max(0, currentHealth - damage);
            NotifyHealthChanged();

            Debug.Log($"敵にダメージ: {damage} / 残りHP: {currentHealth}");

            if (currentHealth > 0)
            {
                return;
            }

            Died?.Invoke();
            Destroy(gameObject);
        }

        public void OnAttacked(AttackHitbox attacker, Collider2D hitCollider)
        {
            int damage = 0;

            if (attacker != null)
            {
                damage = attacker.PlayerAttackDamage;
            }

            TakeDamage(damage);
        }


        /// <summary>
        /// 一定時間、プレイヤーとの接触ダメージを無効化する(中江)
        /// </summary>
        public void IgnoreContactDamage(float duration)
        {
            Debug.Log($"[IgnoreContactDamage] frame={Time.frameCount}, until={ignoreContactDamageUntilTime}");
            ignoreContactDamageUntilTime = Time.time + duration;
        }

        public void ResetHealthToFull()
        {
            currentHealth = MaxHealth;
            NotifyHealthChanged();
        }

        private void NotifyHealthChanged()
        {
            HealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        /// <summary>
        /// 現在、接触ダメージを一時的に無効化しているかを返す。
        /// </summary>
        /// <returns></returns>
        public bool IsContactDamageIgnored()
        {
            return Time.time < ignoreContactDamageUntilTime;
        }
    }
}
