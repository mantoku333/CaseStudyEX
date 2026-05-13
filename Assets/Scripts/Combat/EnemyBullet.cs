using Metroidvania.Player;
using Player;
using System.Collections.Generic;
using UnityEngine;

namespace Metroidvania.Enemy
{
    /// <summary>
    /// 敵が発射する弾の挙動を管理するクラス。
    /// 通常の直進弾と、遠距離敵用の地形回避追尾弾の両方を扱う。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EnemyBullet : MonoBehaviour
    {
        [Header("Lifetime")]
        [SerializeField, Min(0.1f)] private float lifeTime = 10f;

        [Header("Collision")]
        [SerializeField] private LayerMask destroyOnHitLayers;
        [SerializeField, Min(1)] private int damage = 10;

        [Header("Homing")]
        [SerializeField, Min(0.05f)] private float pathRefreshInterval = 0.15f;
        [SerializeField, Min(0.1f)] private float pathNodeSpacing = 0.4f;
        [SerializeField, Min(0.01f)] private float obstacleClearanceRadius = 0.55f;
        [SerializeField, Min(0.05f)] private float waypointReachDistance = 0.12f;

        private static readonly Vector2Int[] NeighborOffsets =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1)
        };

        private readonly List<Vector2> currentPath = new List<Vector2>();

        private Rigidbody2D rb2D;
        // 弾自身の大きさを見て、壁からどれくらい離れて経路探索するかを決める。
        private Collider2D bulletCollider;
        // プレイヤーの Transform 位置ではなく、当たり判定の中心を狙うために使う。
        private Collider2D targetCollider;
        private bool initialized;
        private float bulletSpeed;
        private Transform target;
        private Transform owner;
        private float ownerDetectionRadius;
        private bool useTerrainAvoidance;
        private LayerMask obstacleMask;
        private float nextPathRefreshTime;
        private int currentPathIndex;

        private sealed class PathNode
        {
            public Vector2Int Grid;
            public Vector2Int Parent;
            public bool HasParent;
            public float G;
            public float H;
            public float F => G + H;
        }

        private void Awake()
        {
            rb2D = GetComponent<Rigidbody2D>();
            bulletCollider = GetComponent<Collider2D>();

            if (destroyOnHitLayers.value == 0)
            {
                destroyOnHitLayers = BuildDefaultObstacleMask();
            }

            obstacleMask = destroyOnHitLayers;
        }

        private void OnValidate()
        {
            lifeTime = Mathf.Max(0.1f, lifeTime);
            damage = Mathf.Max(1, damage);
            pathRefreshInterval = Mathf.Max(0.05f, pathRefreshInterval);
            pathNodeSpacing = Mathf.Max(0.1f, pathNodeSpacing);
            obstacleClearanceRadius = Mathf.Max(0.01f, obstacleClearanceRadius);
            waypointReachDistance = Mathf.Max(0.05f, waypointReachDistance);

            if (destroyOnHitLayers.value == 0)
            {
                destroyOnHitLayers = BuildDefaultObstacleMask();
            }
        }

        private void Start()
        {
            Destroy(gameObject, lifeTime);
        }

        private void FixedUpdate()
        {
            if (!initialized || target == null)
            {
                return;
            }

            if (owner == null)
            {
                Destroy(gameObject);
                return;
            }

            // プレイヤーの足元や pivot ではなく、実際に狙う中心位置を基準に範囲外判定を行う。
            Vector2 targetPosition = GetTargetAimPosition();
            if (IsOutsideOwnerRadius(targetPosition) || IsOutsideOwnerRadius(transform.position))
            {
                Destroy(gameObject);
                return;
            }

            if (useTerrainAvoidance && Time.time >= nextPathRefreshTime)
            {
                RefreshTerrainPath();
            }

            Vector2 direction = ResolveHomingDirection();
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            SetVelocity(direction);
        }

        /// <summary>
        /// 発射方向と速度を設定。
        /// </summary>
        public void Initialize(Vector2 direction, float speed)
        {
            target = null;
            targetCollider = null;
            owner = null;
            ownerDetectionRadius = 0f;
            useTerrainAvoidance = false;
            currentPath.Clear();
            currentPathIndex = 0;
            bulletSpeed = Mathf.Max(0.1f, speed);
            initialized = true;

            SetVelocity(direction.sqrMagnitude > 0.0001f ? direction : Vector2.left);
        }

        /// <summary>
        /// 追尾弾として初期化する。
        /// </summary>
        public void Initialize(
            Transform target,
            Transform owner,
            float ownerDetectionRadius,
            float speed,
            int damage,
            float lifetime,
            LayerMask obstacleMask,
            bool useTerrainAvoidance)
        {
            this.target = target;
            // 追尾先をプレイヤー本体の当たり判定中心にして、弾が足元へ吸われるのを防ぐ。
            targetCollider = FindBestTargetCollider(target);
            this.owner = owner;
            this.ownerDetectionRadius = Mathf.Max(0f, ownerDetectionRadius);
            this.bulletSpeed = Mathf.Max(0.1f, speed);
            this.damage = Mathf.Max(1, damage);
            this.lifeTime = Mathf.Max(0.1f, lifetime);
            this.useTerrainAvoidance = useTerrainAvoidance;
            this.obstacleMask = obstacleMask.value != 0 ? obstacleMask : BuildDefaultObstacleMask();
            destroyOnHitLayers = this.obstacleMask;
            initialized = true;

            RefreshTerrainPath();
            SetVelocity(ResolveHomingDirection());
        }

        public void DestroyByParry()
        {
            Destroy(gameObject);
        }

        private void SetVelocity(Vector2 direction)
        {
            if (rb2D == null || direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 normalizedDirection = direction.normalized;
            rb2D.linearVelocity = normalizedDirection * Mathf.Max(0.1f, bulletSpeed);
            transform.right = normalizedDirection;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (TryApplyPlayerHit(other.gameObject))
            {
                return;
            }

            if (IsInLayerMask(other.gameObject.layer, destroyOnHitLayers))
            {
                Destroy(gameObject);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (TryApplyPlayerHit(collision.gameObject))
            {
                return;
            }

            if (IsInLayerMask(collision.gameObject.layer, destroyOnHitLayers))
            {
                Destroy(gameObject);
            }
        }

        private bool TryApplyPlayerHit(GameObject hitObject)
        {
            if (hitObject == null)
            {
                return false;
            }

            PlayerDamageFlash damageFlash = hitObject.GetComponent<PlayerDamageFlash>();
            if (damageFlash == null)
            {
                damageFlash = hitObject.GetComponentInParent<PlayerDamageFlash>();
            }

            PlayerHealth playerHealth = hitObject.GetComponent<PlayerHealth>();
            if (playerHealth == null)
            {
                playerHealth = hitObject.GetComponentInParent<PlayerHealth>();
            }

            if (damageFlash == null && playerHealth == null && !hitObject.CompareTag("Player"))
            {
                return false;
            }

            if (damageFlash != null)
            {
                damageFlash.PlayFlash();
            }

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
            }

            Destroy(gameObject);
            return true;
        }

        private Vector2 ResolveHomingDirection()
        {
            Vector2 currentPosition = transform.position;

            if (currentPath.Count > 0)
            {
                while (currentPathIndex < currentPath.Count &&
                       Vector2.Distance(currentPosition, currentPath[currentPathIndex]) <= waypointReachDistance)
                {
                    currentPathIndex++;
                }

                if (currentPathIndex < currentPath.Count)
                {
                    return currentPath[currentPathIndex] - currentPosition;
                }
            }

            // 経路が使えない場合の直接追尾でも、Transform 位置ではなくプレイヤー中心へ向かう。
            return target != null
                ? GetTargetAimPosition() - currentPosition
                : rb2D.linearVelocity;
        }

        private void RefreshTerrainPath()
        {
            nextPathRefreshTime = Time.time + pathRefreshInterval;
            currentPath.Clear();
            currentPathIndex = 0;

            if (!useTerrainAvoidance || target == null || owner == null)
            {
                return;
            }

            // 経路探索のゴールもプレイヤー中心にすることで、壁際の足元を狙い続ける動きを避ける。
            TryBuildTerrainPath(transform.position, GetTargetAimPosition(), currentPath);
        }

        private bool TryBuildTerrainPath(Vector2 start, Vector2 goal, List<Vector2> path)
        {
            path.Clear();

            Vector2 center = owner.position;
            float radius = Mathf.Max(0.1f, ownerDetectionRadius);
            if (!IsInsideOwnerRadius(start, center, radius) || !IsInsideOwnerRadius(goal, center, radius))
            {
                return false;
            }

            if (!IsMovementSegmentBlocked(start, goal))
            {
                path.Add(goal);
                return true;
            }

            float spacing = Mathf.Max(0.1f, pathNodeSpacing);
            Vector2Int startCell = WorldToPathCell(start, center, spacing);
            Vector2Int goalCell = WorldToPathCell(goal, center, spacing);
            int extent = Mathf.CeilToInt(radius / spacing);
            int maxIterations = Mathf.Min(5000, (extent * 2 + 1) * (extent * 2 + 1));

            List<PathNode> open = new List<PathNode>();
            Dictionary<Vector2Int, PathNode> nodes = new Dictionary<Vector2Int, PathNode>();
            HashSet<Vector2Int> closed = new HashSet<Vector2Int>();

            PathNode startNode = new PathNode
            {
                Grid = startCell,
                G = 0f,
                H = Vector2Int.Distance(startCell, goalCell)
            };
            open.Add(startNode);
            nodes[startCell] = startNode;

            int iterations = 0;
            while (open.Count > 0 && iterations < maxIterations)
            {
                iterations++;
                PathNode current = PopBestNode(open);

                if (current.Grid == goalCell)
                {
                    ReconstructPath(current, nodes, center, spacing, goal, path);
                    return path.Count > 0;
                }

                closed.Add(current.Grid);

                for (int i = 0; i < NeighborOffsets.Length; i++)
                {
                    Vector2Int nextCell = current.Grid + NeighborOffsets[i];
                    if (closed.Contains(nextCell) ||
                        Mathf.Abs(nextCell.x) > extent ||
                        Mathf.Abs(nextCell.y) > extent)
                    {
                        continue;
                    }

                    Vector2 nextWorld = PathCellToWorld(nextCell, center, spacing);
                    if (!IsInsideOwnerRadius(nextWorld, center, radius))
                    {
                        continue;
                    }

                    bool isGoalCell = nextCell == goalCell;
                    if (!isGoalCell && IsObstacleAt(nextWorld))
                    {
                        continue;
                    }

                    Vector2 currentWorld = PathCellToWorld(current.Grid, center, spacing);
                    if (IsMovementSegmentBlocked(currentWorld, nextWorld))
                    {
                        continue;
                    }

                    float nextCost = current.G + Vector2.Distance(currentWorld, nextWorld);

                    if (!nodes.TryGetValue(nextCell, out PathNode nextNode))
                    {
                        nextNode = new PathNode
                        {
                            Grid = nextCell,
                            H = Vector2Int.Distance(nextCell, goalCell)
                        };
                        nodes[nextCell] = nextNode;
                        open.Add(nextNode);
                    }
                    else if (open.IndexOf(nextNode) < 0)
                    {
                        open.Add(nextNode);
                    }

                    if (nextNode.HasParent && nextCost >= nextNode.G)
                    {
                        continue;
                    }

                    nextNode.G = nextCost;
                    nextNode.Parent = current.Grid;
                    nextNode.HasParent = true;
                }
            }

            path.Clear();
            return false;
        }

        private PathNode PopBestNode(List<PathNode> open)
        {
            int bestIndex = 0;
            for (int i = 1; i < open.Count; i++)
            {
                if (open[i].F < open[bestIndex].F ||
                    (Mathf.Approximately(open[i].F, open[bestIndex].F) && open[i].H < open[bestIndex].H))
                {
                    bestIndex = i;
                }
            }

            PathNode bestNode = open[bestIndex];
            open.RemoveAt(bestIndex);
            return bestNode;
        }

        private void ReconstructPath(
            PathNode endNode,
            Dictionary<Vector2Int, PathNode> nodes,
            Vector2 center,
            float spacing,
            Vector2 goal,
            List<Vector2> path)
        {
            List<Vector2> reversed = new List<Vector2>();
            PathNode current = endNode;

            while (current != null)
            {
                reversed.Add(PathCellToWorld(current.Grid, center, spacing));

                if (!current.HasParent || !nodes.TryGetValue(current.Parent, out current))
                {
                    break;
                }
            }

            for (int i = reversed.Count - 2; i >= 0; i--)
            {
                path.Add(reversed[i]);
            }

            if (path.Count == 0 || Vector2.Distance(path[path.Count - 1], goal) > waypointReachDistance)
            {
                path.Add(goal);
            }
        }

        private Vector2Int WorldToPathCell(Vector2 world, Vector2 center, float spacing)
        {
            Vector2 local = (world - center) / spacing;
            return new Vector2Int(Mathf.RoundToInt(local.x), Mathf.RoundToInt(local.y));
        }

        private Vector2 PathCellToWorld(Vector2Int cell, Vector2 center, float spacing)
        {
            return center + new Vector2(cell.x * spacing, cell.y * spacing);
        }

        private bool IsObstacleAt(Vector2 point)
        {
            // 弾の実サイズぶんの余白を含めて、壁に近すぎる経路ノードを除外する。
            return obstacleMask.value != 0 &&
                   Physics2D.OverlapCircle(point, GetEffectiveObstacleClearanceRadius(), obstacleMask) != null;
        }

        private bool IsMovementSegmentBlocked(Vector2 from, Vector2 to)
        {
            if (obstacleMask.value == 0)
            {
                return false;
            }

            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= 0.001f)
            {
                return false;
            }

            // 線ではなく弾の太さを持った CircleCast で、角をかすめる経路を弾く。
            RaycastHit2D hit = Physics2D.CircleCast(
                from,
                GetEffectiveObstacleClearanceRadius(),
                delta / distance,
                distance,
                obstacleMask);

            return hit.collider != null;
        }

        /// <summary>
        /// 追尾先の基準点を返す。プレイヤーの足元ではなく、当たり判定の中心を優先する。
        /// </summary>
        private Vector2 GetTargetAimPosition()
        {
            if (targetCollider != null && targetCollider.enabled)
            {
                return targetCollider.bounds.center;
            }

            return target != null ? target.position : transform.position;
        }

        /// <summary>
        /// 設定値と弾自身の当たり判定サイズから、壁回避に使う安全半径を決める。
        /// </summary>
        private float GetEffectiveObstacleClearanceRadius()
        {
            float configuredRadius = Mathf.Max(0.01f, obstacleClearanceRadius);
            if (bulletCollider == null || !bulletCollider.enabled)
            {
                return configuredRadius;
            }

            Bounds bounds = bulletCollider.bounds;
            float colliderRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);
            return Mathf.Max(configuredRadius, colliderRadius + 0.05f);
        }

        /// <summary>
        /// プレイヤー配下の非 Trigger コライダーから、狙う基準にしやすい一番大きなものを探す。
        /// </summary>
        private static Collider2D FindBestTargetCollider(Transform targetRoot)
        {
            if (targetRoot == null)
            {
                return null;
            }

            Collider2D[] colliders = targetRoot.GetComponentsInChildren<Collider2D>();
            Collider2D bestCollider = null;
            float bestArea = -1f;

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D candidate = colliders[i];
                if (candidate == null || !candidate.enabled || candidate.isTrigger)
                {
                    continue;
                }

                float area = GetColliderBoundsArea(candidate);
                if (area <= bestArea)
                {
                    continue;
                }

                bestArea = area;
                bestCollider = candidate;
            }

            if (bestCollider != null)
            {
                return bestCollider;
            }

            return targetRoot.GetComponentInChildren<Collider2D>();
        }

        /// <summary>
        /// コライダーの Bounds 面積を返す。追尾先として一番大きい当たり判定を選ぶために使う。
        /// </summary>
        private static float GetColliderBoundsArea(Collider2D collider)
        {
            Bounds bounds = collider.bounds;
            return Mathf.Max(0f, bounds.size.x) * Mathf.Max(0f, bounds.size.y);
        }

        private bool IsOutsideOwnerRadius(Vector2 position)
        {
            if (owner == null || ownerDetectionRadius <= 0f)
            {
                return false;
            }

            return !IsInsideOwnerRadius(position, owner.position, ownerDetectionRadius);
        }

        private static bool IsInsideOwnerRadius(Vector2 position, Vector2 center, float radius)
        {
            return (position - center).sqrMagnitude <= radius * radius;
        }

        private static bool IsInLayerMask(int layer, LayerMask layerMask)
        {
            return (layerMask.value & (1 << layer)) != 0;
        }

        private static LayerMask BuildDefaultObstacleMask()
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

            return mask;
        }
    }
}
