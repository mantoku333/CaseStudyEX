using Player;
using UnityEngine;

namespace GameName.Enemy
{
    /// <summary>
    /// シンプルな敵の巡回移動と接触ダメージを管理するクラス
    /// </summary>
    public class EnemyController : MonoBehaviour, IAttackReceiver
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
        [SerializeField] private bool flipSpriteOnTurn = true;

        private Vector3 startPosition;
        private int moveDirection = 1;
        private bool movementPaused;
        private Rigidbody2D rigidbody2D;
        private Collider2D bodyCollider;
        private SpriteRenderer spriteRenderer;
        private int currentHealth;

        /// <summary>
        /// 現在の向き。右が 1、左が -1。
        /// </summary>
        public int FacingDirection => moveDirection;

        /// <summary>
        /// 現在の X 座標（Rigidbody2D がある場合は物理座標）。
        /// </summary>
        public float CurrentX => rigidbody2D != null ? rigidbody2D.position.x : transform.position.x;

        /// <summary>
        /// 必要コンポーネントの取得と、未設定レイヤーマスクの補完を行う。
        /// </summary>
        private void Awake()
        {
            rigidbody2D = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            currentHealth = Mathf.Max(1, maxHealth);

            if (stageLayerMask.value == 0)
            {
                stageLayerMask = BuildDefaultStageMask();
            }
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
                if (Mathf.Abs(distanceFromStart) >= patrolDistance)
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
            return hit.collider != null;
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

        /// <summary>
        /// 向きを反転して見た目の向きも更新する。
        /// </summary>
        private void TurnAround()
        {
            moveDirection *= -1;
            ApplyFacing();
        }

        /// <summary>
        /// 現在の向きに合わせてスプライトの左右反転を適用する。
        /// </summary>
        private void ApplyFacing()
        {
            if (spriteRenderer != null && flipSpriteOnTurn)
            {
                spriteRenderer.flipX = moveDirection < 0;
            }
        }

        /// <summary>
        /// プレイヤーに接触した際にダメージを与える
        /// </summary>
        /// <param name="collision">衝突情報</param>
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.gameObject.TryGetComponent<PlayerHealth>(out PlayerHealth playerHealth))
            {
                playerHealth.TakeDamage(damageToPlayer);
            }
        }

        public void OnAttacked(AttackHitbox attacker, Collider2D hitCollider)
        {
            currentHealth -= 1;

            if (currentHealth <= 0)
            {
                Debug.Log("敵に当たりました");
                Destroy(gameObject);
            }
        }
    }
}
