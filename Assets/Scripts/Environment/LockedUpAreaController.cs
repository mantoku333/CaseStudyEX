using System;
using System.Collections;
using System.Collections.Generic;
using GameName.Enemy;
using Metroidvania.Player;
using Player;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 指定された敵を全滅させるまで、プレイヤーをエリア内に閉じ込める制御クラス。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class LockedUpAreaController : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";

    [Header("Enemies")]
    [SerializeField] private EnemyController[] enemiesToDefeat = new EnemyController[0];

    [Header("Shutters")]
    [SerializeField] private ShutterWallBlockRise[] shutterWalls = new ShutterWallBlockRise[0];
    [SerializeField, Min(0.01f)] private float fastCloseDurationPerBlock = 0.03f;
    [SerializeField, Min(0f)] private float fastCloseIntervalBetweenBlocks = 0.005f;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera fixedCamera;
    [SerializeField] private int activeCameraPriority = 50;
    [SerializeField] private int inactiveCameraPriority = 0;

    [Header("Confinement")]
    [SerializeField] private bool confinePlayerInsideArea = true;
    [SerializeField] private bool confineX = true;
    [SerializeField] private bool confineY = true;
    [SerializeField] private bool confineYForDynamicBodies = false;
    [SerializeField] private Vector2 confinementInset = new Vector2(0.35f, 0f);

    [Header("Debug")]
    [SerializeField] private bool verboseLogging;

    private readonly List<TrackedEnemy> trackedEnemies = new List<TrackedEnemy>();

    private bool encounterStarted;
    private bool encounterCompleted;
    private bool hasConfinementBounds;
    private int remainingEnemyCount;
    private Bounds confinementBounds;
    private Transform playerRoot;
    private Rigidbody2D playerRigidbody2D;
    private Collider2D playerBodyCollider;
    private DodgeController playerDodgeController;

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        CacheConfinementBounds();
        CachePlayerReferences();
        DeactivateFixedCamera();
    }

    private void OnDisable()
    {
        UnsubscribeTrackedEnemies();
        ClearActiveDodgeBounds();
        DeactivateFixedCamera();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        fastCloseDurationPerBlock = Mathf.Max(0.01f, fastCloseDurationPerBlock);
        fastCloseIntervalBetweenBlocks = Mathf.Max(0f, fastCloseIntervalBetweenBlocks);
        confinementInset = new Vector2(
            Mathf.Max(0f, confinementInset.x),
            Mathf.Max(0f, confinementInset.y));

        EnsureTriggerCollider();
    }
#endif

    private void FixedUpdate()
    {
        if (!encounterStarted || encounterCompleted || !confinePlayerInsideArea)
        {
            return;
        }

        ConfinePlayerInsideArea();
    }

    private void Update()
    {
        if (!encounterStarted || encounterCompleted)
        {
            return;
        }

        PruneMissingEnemies();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryStartEncounter(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryStartEncounter(other);
    }

    private void TryStartEncounter(Collider2D playerCollider)
    {
        if (encounterStarted || encounterCompleted)
        {
            return;
        }

        if (!TryResolvePlayerBodyCollider(playerCollider, out Collider2D bodyCollider))
        {
            return;
        }

        if (!IsPlayerBodyFullyInsideArea(bodyCollider))
        {
            return;
        }

        CachePlayerReferences(bodyCollider);
        BuildTrackedEnemyList();

        if (remainingEnemyCount <= 0)
        {
            encounterCompleted = true;
            if (verboseLogging)
            {
                Debug.Log($"[LockedUpAreaController] No alive enemies assigned on {gameObject.name}. Encounter skipped.", this);
            }

            return;
        }

        encounterStarted = true;
        ActivateFixedCamera();
        RegisterActiveDodgeBounds();

        // シャッター演出より先に拘束し、閉まり切る前に外へ抜けられないようにする。
        ConfinePlayerInsideArea();
        LockArea();

        if (verboseLogging)
        {
            Debug.Log($"[LockedUpAreaController] Encounter started on {gameObject.name}. Enemies: {remainingEnemyCount}", this);
        }
    }

    private void CompleteEncounter()
    {
        if (encounterCompleted)
        {
            return;
        }

        encounterCompleted = true;
        encounterStarted = false;
        remainingEnemyCount = 0;

        UnsubscribeTrackedEnemies();
        ClearActiveDodgeBounds();
        UnlockArea();
        DeactivateFixedCamera();

        if (verboseLogging)
        {
            Debug.Log($"[LockedUpAreaController] Encounter completed on {gameObject.name}", this);
        }
    }

    private void BuildTrackedEnemyList()
    {
        UnsubscribeTrackedEnemies();

        if (enemiesToDefeat == null || enemiesToDefeat.Length == 0)
        {
            remainingEnemyCount = 0;
            return;
        }

        for (int i = 0; i < enemiesToDefeat.Length; i++)
        {
            EnemyController enemy = enemiesToDefeat[i];
            if (enemy == null || !enemy.gameObject.activeInHierarchy || IsAlreadyTracked(enemy))
            {
                continue;
            }

            TrackedEnemy entry = new TrackedEnemy
            {
                Enemy = enemy
            };

            entry.DeathHandler = () => HandleTrackedEnemyDied(entry);
            enemy.Died += entry.DeathHandler;
            trackedEnemies.Add(entry);
        }

        remainingEnemyCount = trackedEnemies.Count;
    }

    private bool IsAlreadyTracked(EnemyController enemy)
    {
        for (int i = 0; i < trackedEnemies.Count; i++)
        {
            if (trackedEnemies[i].Enemy == enemy)
            {
                return true;
            }
        }

        return false;
    }

    private void HandleTrackedEnemyDied(TrackedEnemy entry)
    {
        MarkTrackedEnemyDefeated(entry);
    }

    private void PruneMissingEnemies()
    {
        for (int i = trackedEnemies.Count - 1; i >= 0; i--)
        {
            if (!encounterStarted || encounterCompleted)
            {
                return;
            }

            TrackedEnemy entry = trackedEnemies[i];
            if (entry == null || entry.CountedAsDefeated)
            {
                continue;
            }

            EnemyController enemy = entry.Enemy;
            if (enemy == null || !enemy.gameObject.activeInHierarchy)
            {
                MarkTrackedEnemyDefeated(entry);
            }
        }
    }

    private void MarkTrackedEnemyDefeated(TrackedEnemy entry)
    {
        if (entry == null || entry.CountedAsDefeated)
        {
            return;
        }

        entry.CountedAsDefeated = true;
        if (entry.Enemy != null && entry.DeathHandler != null)
        {
            entry.Enemy.Died -= entry.DeathHandler;
        }

        remainingEnemyCount = Mathf.Max(0, remainingEnemyCount - 1);
        if (remainingEnemyCount <= 0)
        {
            CompleteEncounter();
        }
    }

    private void UnsubscribeTrackedEnemies()
    {
        for (int i = 0; i < trackedEnemies.Count; i++)
        {
            TrackedEnemy entry = trackedEnemies[i];
            if (entry == null || entry.Enemy == null || entry.DeathHandler == null)
            {
                continue;
            }

            entry.Enemy.Died -= entry.DeathHandler;
        }

        trackedEnemies.Clear();
    }

    private void LockArea()
    {
        if (shutterWalls == null)
        {
            return;
        }

        for (int i = 0; i < shutterWalls.Length; i++)
        {
            ShutterWallBlockRise wall = shutterWalls[i];
            if (wall == null)
            {
                continue;
            }

            wall.TryClose(fastCloseDurationPerBlock, fastCloseIntervalBetweenBlocks);
        }
    }

    private void UnlockArea()
    {
        if (shutterWalls == null)
        {
            return;
        }

        for (int i = 0; i < shutterWalls.Length; i++)
        {
            ShutterWallBlockRise wall = shutterWalls[i];
            if (wall == null)
            {
                continue;
            }

            if (wall.IsTransitioning)
            {
                StartCoroutine(OpenWallWhenReady(wall));
            }
            else
            {
                wall.TryOpen();
            }
        }
    }

    private IEnumerator OpenWallWhenReady(ShutterWallBlockRise wall)
    {
        while (wall != null && wall.IsTransitioning)
        {
            yield return null;
        }

        if (wall != null)
        {
            wall.TryOpen();
        }
    }

    private void ActivateFixedCamera()
    {
        if (fixedCamera == null)
        {
            return;
        }

        fixedCamera.Priority.Value = activeCameraPriority;
        fixedCamera.Priority.Enabled = true;
    }

    private void DeactivateFixedCamera()
    {
        if (fixedCamera == null)
        {
            return;
        }

        fixedCamera.Priority.Value = inactiveCameraPriority;
        fixedCamera.Priority.Enabled = true;
    }

    private void ConfinePlayerInsideArea()
    {
        if (!hasConfinementBounds)
        {
            return;
        }

        CachePlayerReferences();
        ConstrainPlayerBodyInsideArea();
    }

    private void ConstrainPlayerBodyInsideArea()
    {
        if (playerRoot == null || playerBodyCollider == null)
        {
            return;
        }

        Bounds bodyBounds = playerBodyCollider.bounds;
        Vector3 current = playerRoot.position;
        float offsetX = 0f;
        float offsetY = 0f;

        if (confineX)
        {
            float minX = confinementBounds.min.x + confinementInset.x;
            float maxX = confinementBounds.max.x - confinementInset.x;
            if (minX <= maxX)
            {
                if (bodyBounds.min.x < minX)
                {
                    offsetX = minX - bodyBounds.min.x;
                }
                else if (bodyBounds.max.x > maxX)
                {
                    offsetX = maxX - bodyBounds.max.x;
                }
            }
            else
            {
                offsetX = confinementBounds.center.x - bodyBounds.center.x;
            }
        }

        bool canConfineY = confineY && ShouldConfineYForTarget(playerRigidbody2D);
        if (canConfineY)
        {
            float minY = confinementBounds.min.y + confinementInset.y;
            float maxY = confinementBounds.max.y - confinementInset.y;
            if (minY <= maxY)
            {
                if (bodyBounds.min.y < minY)
                {
                    offsetY = minY - bodyBounds.min.y;
                }
                else if (bodyBounds.max.y > maxY)
                {
                    offsetY = maxY - bodyBounds.max.y;
                }
            }
            else
            {
                offsetY = confinementBounds.center.y - bodyBounds.center.y;
            }
        }

        if (Mathf.Approximately(offsetX, 0f) && Mathf.Approximately(offsetY, 0f))
        {
            return;
        }

        Vector2 nextPosition = new Vector2(current.x + offsetX, current.y + offsetY);
        if (playerRigidbody2D != null)
        {
            playerRigidbody2D.position = nextPosition;
            return;
        }

        playerRoot.position = new Vector3(nextPosition.x, nextPosition.y, current.z);
    }

    private void RegisterActiveDodgeBounds()
    {
        // 回避移動の目標地点も拘束範囲内に切り詰め、回避と拘束の押し戻し競合を防ぐ。
        if (!confinePlayerInsideArea || !hasConfinementBounds)
        {
            return;
        }

        CachePlayerReferences();
        if (playerDodgeController == null || playerBodyCollider == null)
        {
            return;
        }

        playerDodgeController.SetAreaDodgeBounds(
            this,
            confinementBounds,
            confinementInset,
            confineX,
            confineY,
            confineYForDynamicBodies,
            playerBodyCollider);
    }

    private void ClearActiveDodgeBounds()
    {
        // このエリアが設定した回避制限だけを解除する。
        if (playerDodgeController != null)
        {
            playerDodgeController.ClearAreaDodgeBounds(this);
        }
    }

    private bool ShouldConfineYForTarget(Rigidbody2D targetRigidbody2D)
    {
        if (targetRigidbody2D == null)
        {
            return true;
        }

        if (targetRigidbody2D.bodyType != RigidbodyType2D.Dynamic)
        {
            return true;
        }

        return confineYForDynamicBodies;
    }

    private void CacheConfinementBounds()
    {
        Collider2D area2D = GetComponent<Collider2D>();
        if (area2D == null)
        {
            hasConfinementBounds = false;
            return;
        }

        hasConfinementBounds = true;
        confinementBounds = area2D.bounds;
    }

    private void CachePlayerReferences(Collider2D playerCollider = null)
    {
        if (playerRoot != null && playerBodyCollider != null)
        {
            if (playerRigidbody2D == null)
            {
                playerRigidbody2D = playerRoot.GetComponent<Rigidbody2D>();
            }

            if (playerDodgeController == null)
            {
                playerDodgeController = playerRoot.GetComponent<DodgeController>();
            }

            return;
        }

        if (playerCollider != null && TryResolvePlayerBodyCollider(playerCollider, out Collider2D bodyCollider))
        {
            CachePlayerReferencesFromBodyCollider(bodyCollider);
            return;
        }

        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return;
        }

        GameObject playerObject = global::PlayerReferenceCache.GetGameObject(playerTag);
        if (playerObject == null)
        {
            return;
        }

        PlayerHealth playerHealth = playerObject.GetComponent<PlayerHealth>();
        if (PlayerBodyColliderUtility.TryGetBodyCollider(playerHealth, out bodyCollider))
        {
            CachePlayerReferencesFromBodyCollider(bodyCollider);
            return;
        }

        playerRoot = playerObject.transform;
        playerRigidbody2D = playerObject.GetComponent<Rigidbody2D>();
        playerBodyCollider = playerObject.GetComponent<Collider2D>();
        playerDodgeController = playerObject.GetComponent<DodgeController>();
    }

    private void CachePlayerReferencesFromBodyCollider(Collider2D bodyCollider)
    {
        if (bodyCollider == null)
        {
            return;
        }

        playerBodyCollider = bodyCollider;

        Rigidbody2D attachedRigidbody = bodyCollider.attachedRigidbody;
        if (attachedRigidbody != null)
        {
            playerRoot = attachedRigidbody.transform;
            playerRigidbody2D = attachedRigidbody;
            playerDodgeController = attachedRigidbody.GetComponent<DodgeController>();
            return;
        }

        playerRoot = bodyCollider.transform;
        playerRigidbody2D = bodyCollider.GetComponent<Rigidbody2D>();
        playerDodgeController = bodyCollider.GetComponent<DodgeController>();
    }

    private bool TryResolvePlayerBodyCollider(Collider2D sourceCollider, out Collider2D bodyCollider)
    {
        // 傘・攻撃・パリィなどの子トリガーではなく、プレイヤー本体コライダーを必ず使う。
        bodyCollider = null;
        if (sourceCollider == null)
        {
            return false;
        }

        if (PlayerBodyColliderUtility.TryGetPlayerBodyFromCollider(
                sourceCollider,
                out PlayerHealth playerHealth,
                out bodyCollider) &&
            IsPlayerHealthObject(playerHealth))
        {
            return true;
        }

        if (bodyCollider != null && IsPlayerHealthObject(playerHealth))
        {
            return true;
        }

        playerHealth = ResolvePlayerHealth(sourceCollider);
        if (!IsPlayerHealthObject(playerHealth))
        {
            return false;
        }

        return PlayerBodyColliderUtility.TryGetBodyCollider(playerHealth, out bodyCollider);
    }

    private static PlayerHealth ResolvePlayerHealth(Collider2D sourceCollider)
    {
        if (sourceCollider == null)
        {
            return null;
        }

        PlayerHealth playerHealth = sourceCollider.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            return playerHealth;
        }

        playerHealth = sourceCollider.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            return playerHealth;
        }

        Rigidbody2D attachedRigidbody = sourceCollider.attachedRigidbody;
        if (attachedRigidbody == null)
        {
            return null;
        }

        playerHealth = attachedRigidbody.GetComponent<PlayerHealth>();
        return playerHealth != null ? playerHealth : attachedRigidbody.GetComponentInParent<PlayerHealth>();
    }

    private bool IsPlayerBodyFullyInsideArea(Collider2D bodyCollider)
    {
        // 拘束開始時に即座の位置補正が起きないよう、本体全体が有効範囲内に入るまで待つ。
        if (bodyCollider == null)
        {
            return false;
        }

        if (!hasConfinementBounds)
        {
            return true;
        }

        Bounds bodyBounds = bodyCollider.bounds;
        const float tolerance = 0.001f;

        if (confineX)
        {
            float minX = confinementBounds.min.x + confinementInset.x;
            float maxX = confinementBounds.max.x - confinementInset.x;
            if (bodyBounds.min.x < minX - tolerance || bodyBounds.max.x > maxX + tolerance)
            {
                return false;
            }
        }

        Rigidbody2D bodyRigidbody = bodyCollider.attachedRigidbody != null
            ? bodyCollider.attachedRigidbody
            : bodyCollider.GetComponent<Rigidbody2D>();
        bool canConfineY = confineY && ShouldConfineYForTarget(bodyRigidbody);
        if (canConfineY)
        {
            float minY = confinementBounds.min.y + confinementInset.y;
            float maxY = confinementBounds.max.y - confinementInset.y;
            if (bodyBounds.min.y < minY - tolerance || bodyBounds.max.y > maxY + tolerance)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsPlayerHealthObject(PlayerHealth playerHealth)
    {
        if (playerHealth == null)
        {
            return false;
        }

        return IsPlayerObject(playerHealth.gameObject) ||
               (playerHealth.transform.root != null && IsPlayerObject(playerHealth.transform.root.gameObject));
    }

    private bool IsPlayerObject(GameObject target)
    {
        return target != null &&
               (string.IsNullOrWhiteSpace(playerTag) || target.CompareTag(playerTag));
    }

    private void EnsureTriggerCollider()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private sealed class TrackedEnemy
    {
        public EnemyController Enemy;
        public Action DeathHandler;
        public bool CountedAsDefeated;
    }
}
