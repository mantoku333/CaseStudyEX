using GameName.Enemy;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// ボスエリア侵入で戦闘を開始し、
/// 撃破まで壁とカメラをロックするコントローラー。
/// </summary>
[DisallowMultipleComponent]
public sealed class BossAreaController : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool disableTriggerAfterStart = true;

    [Header("Boss")]
    // StageBoss用エリアではstageBossAttackのみ、LastBoss用エリアではlastBossControllerのみを設定する。
    // 両方入れると同じBossAreaで両方のボスが起動するため、1エリア1ボスの設定にする。
    [SerializeField] private StageBossAttack stageBossAttack;
    [SerializeField] private LastBossController lastBossController;
    [SerializeField] private Transform bossRoot;
    [SerializeField] private string bossDefeatedFlagKey = GameProgressKeys.Boss01Defeated;

    [Header("Walls")]
    [SerializeField] private ShutterWallBlockRise[] wallsCloseOnStart = new ShutterWallBlockRise[0];
    [SerializeField] private ShutterWallBlockRise[] wallsOpenOnStart = new ShutterWallBlockRise[0];

    [Header("Camera")]
    [SerializeField] private CinemachineCamera fixedBossCamera;
    [SerializeField] private int activeCameraPriority = 50;
    [SerializeField] private int inactiveCameraPriority = 0;

    [Header("BGM")]
    [SerializeField] private StageBgmController stageBgm;
    [SerializeField] private bool returnToNormalAfterBoss;

    [Header("Confinement")]
    // エリア内拘束のマスターON/OFF（戦闘中のみ有効）
    [SerializeField] private bool confineInsideArea = true;
    // プレイヤー拘束の有効化
    [SerializeField] private bool confinePlayerInsideArea = true;
    // ボス拘束の有効化
    [SerializeField] private bool confineBossInsideArea = true;
    // 横方向（左右）拘束
    [SerializeField] private bool confineX = true;
    // 縦方向（上下）拘束
    [SerializeField] private bool confineY = true;
    // Dynamic Rigidbody2D に対する縦拘束。false なら床判定を優先して落下挙動を壊しにくい。
    [SerializeField] private bool confineYForDynamicBodies = false;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging;

    
    private static readonly bool enableWallMechanic = false;

    private bool encounterStarted;
    private bool encounterCompleted;
    private bool hasConfinementBounds;
    private Bounds confinementBounds;
    private Transform playerRoot;
    private Rigidbody2D playerRigidbody2D;
    private Rigidbody2D bossRigidbody2D;

    private void Awake()
    {
        // 後でトリガーを無効化しても拘束範囲を使えるよう、起動時に bounds を確定しておく。
        CacheConfinementBounds();

        // どちらか一方だけ設定されていても動くように参照を相互補完する。
        // bossRootだけ設定されている場合でも、StageBoss/LastBossどちらかを自動取得する。
        if (stageBossAttack == null && bossRoot != null)
        {
            stageBossAttack = bossRoot.GetComponent<StageBossAttack>();
        }

        if (lastBossController == null && bossRoot != null)
        {
            lastBossController = bossRoot.GetComponent<LastBossController>();
        }

        if (bossRoot == null && stageBossAttack != null)
        {
            bossRoot = stageBossAttack.transform;
        }

        if (bossRoot == null && lastBossController != null)
        {
            bossRoot = lastBossController.transform;
        }

        if (bossRoot != null)
        {
            bossRigidbody2D = bossRoot.GetComponent<Rigidbody2D>();
        }

        CachePlayerReferences();

        // 開始時は固定カメラを非アクティブ優先度に戻す。
        DeactivateBossCamera();

        // 既に撃破済みフラグが立っている場合、再ロックしないよう完了状態で起動する。
        if (!string.IsNullOrWhiteSpace(bossDefeatedFlagKey) && GameProgressFlags.Get(bossDefeatedFlagKey))
        {
            encounterCompleted = true;

            if (enableWallMechanic)
            {
                UnlockArea();
            }
        }
    }

    private void FixedUpdate()
    {
        if (!encounterStarted || encounterCompleted || !confineInsideArea)
        {
            return;
        }

        // 物理更新タイミングで拘束し、プレイヤー・ボスをエリア外へ出さない。
        ConfineTargetsInsideArea();
    }

    private void Update()
    {
        if (!encounterStarted || encounterCompleted)
        {
            return;
        }

        // Destroy 済みを監視して撃破完了へ遷移する。
        if (!IsBossAlive())
        {
            CompleteEncounter();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag(playerTag))
        {
            TryStartEncounter();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            TryStartEncounter();
        }
    }

    private void TryStartEncounter()
    {
        if (encounterStarted || encounterCompleted)
        {
            return;
        }

        // 戦闘開始直前にプレイヤー参照を再取得しておく。
        CachePlayerReferences();

        encounterStarted = true;

        if (enableWallMechanic)
        {
            LockArea();
        }

        // カメラ切り替え -> ボス起動 の順で開始演出を揃える。
        // カメラ/BGMをボス戦用へ切り替えてから、設定されているボスを起動する。
        ActivateBossCamera();
        stageBgm?.PlayBoss();

        if (stageBossAttack != null)
        {
            stageBossAttack.ActivateEncounter();
        }

        if (lastBossController != null)
        {
            lastBossController.ActivateEncounter();
        }

        if (disableTriggerAfterStart)
        {
            DisableTriggerComponents();
        }

        if (verboseLogging)
        {
            Debug.Log($"[BossAreaController] Encounter started on {gameObject.name}", this);
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

        if (stageBossAttack != null)
        {
            stageBossAttack.DeactivateEncounter();
        }

        if (lastBossController != null)
        {
            lastBossController.DeactivateEncounter();
        }

        if (!string.IsNullOrWhiteSpace(bossDefeatedFlagKey))
        {
            // 再入室時に再戦闘しないよう撃破フラグを永続化する。
            GameProgressFlags.Set(bossDefeatedFlagKey, true);
        }

        if (enableWallMechanic)
        {
            UnlockArea();
        }

        DeactivateBossCamera();

        if (returnToNormalAfterBoss)
        {
            stageBgm?.PlayNormal();
        }

        if (verboseLogging)
        {
            Debug.Log($"[BossAreaController] Encounter completed on {gameObject.name}", this);
        }
    }

    private bool IsBossAlive()
    {
        if (stageBossAttack != null)
        {
            // StageBossAttack 側が残っている限り同一 transform をボス本体として扱う。
            bossRoot = stageBossAttack.transform;

            if (bossRoot != null && bossRigidbody2D == null)
            {
                bossRigidbody2D = bossRoot.GetComponent<Rigidbody2D>();
            }
        }
        else if (lastBossController != null)
        {
            bossRoot = lastBossController.transform;

            if (bossRoot != null && bossRigidbody2D == null)
            {
                bossRigidbody2D = bossRoot.GetComponent<Rigidbody2D>();
            }
        }

        return bossRoot != null;
    }

    private void ConfineTargetsInsideArea()
    {
        if (!hasConfinementBounds)
        {
            return;
        }

        if (confinePlayerInsideArea)
        {
            CachePlayerReferences();
            ConstrainTransform(playerRoot, playerRigidbody2D);
        }

        if (confineBossInsideArea)
        {
            ConstrainTransform(bossRoot, bossRigidbody2D);
        }
    }

    private void ConstrainTransform(Transform target, Rigidbody2D targetRigidbody2D)
    {
        if (target == null)
        {
            return;
        }

        // コライダー外形を加味して、めり込みなく bounds 内に収める。
        Vector2 extents = ResolveColliderExtents(target);
        Vector3 current = target.position;

        float clampedX = current.x;
        float clampedY = current.y;

        if (confineX)
        {
            float minX = confinementBounds.min.x + extents.x;
            float maxX = confinementBounds.max.x - extents.x;
            clampedX = minX <= maxX ? Mathf.Clamp(current.x, minX, maxX) : confinementBounds.center.x;
        }

        bool canConfineY = confineY && ShouldConfineYForTarget(targetRigidbody2D);
        if (canConfineY)
        {
            float minY = confinementBounds.min.y + extents.y;
            float maxY = confinementBounds.max.y - extents.y;
            clampedY = minY <= maxY ? Mathf.Clamp(current.y, minY, maxY) : confinementBounds.center.y;
        }

        if (Mathf.Approximately(clampedX, current.x) && Mathf.Approximately(clampedY, current.y))
        {
            return;
        }

        if (targetRigidbody2D != null)
        {
            // Rigidbody2D がある場合は transform 直接変更を避けて物理座標を書き換える。
            targetRigidbody2D.position = new Vector2(clampedX, clampedY);
            return;
        }

        target.position = new Vector3(clampedX, clampedY, current.z);
    }

    private bool ShouldConfineYForTarget(Rigidbody2D targetRigidbody2D)
    {
        if (targetRigidbody2D == null)
        {
            return true;
        }

        // Dynamic は床判定・重力への干渉が大きいため、必要時のみ縦拘束する。
        if (targetRigidbody2D.bodyType != RigidbodyType2D.Dynamic)
        {
            return true;
        }

        return confineYForDynamicBodies;
    }

    private static Vector2 ResolveColliderExtents(Transform target)
    {
        Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(includeInactive: false);
        if (colliders == null || colliders.Length == 0)
        {
            return Vector2.zero;
        }

        bool hasBounds = false;
        Bounds merged = default;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                merged = collider.bounds;
                hasBounds = true;
            }
            else
            {
                merged.Encapsulate(collider.bounds);
            }
        }

        return hasBounds ? (Vector2)merged.extents : Vector2.zero;
    }

    private void CacheConfinementBounds()
    {
        // 2Dコライダー優先、なければ3Dコライダーを拘束範囲として利用する。
        Collider2D area2D = GetComponent<Collider2D>();
        if (area2D != null)
        {
            hasConfinementBounds = true;
            confinementBounds = area2D.bounds;
            return;
        }

        Collider area3D = GetComponent<Collider>();
        if (area3D != null)
        {
            hasConfinementBounds = true;
            confinementBounds = area3D.bounds;
            return;
        }

        hasConfinementBounds = false;
    }

    private void CachePlayerReferences()
    {
        if (playerRoot != null)
        {
            if (playerRigidbody2D == null)
            {
                playerRigidbody2D = playerRoot.GetComponent<Rigidbody2D>();
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return;
        }

        // タグ検索は必要時のみ実行してキャッシュする。
        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject == null)
        {
            return;
        }

        playerRoot = playerObject.transform;
        playerRigidbody2D = playerObject.GetComponent<Rigidbody2D>();
    }

    private void LockArea()
    {
        ApplyWallCommands(wallsCloseOnStart, open: false);
        ApplyWallCommands(wallsOpenOnStart, open: true);
    }

    private void UnlockArea()
    {
        ApplyWallCommands(wallsCloseOnStart, open: true);
        ApplyWallCommands(wallsOpenOnStart, open: false);
    }

    private static void ApplyWallCommands(ShutterWallBlockRise[] walls, bool open)
    {
        if (walls == null || walls.Length == 0)
        {
            return;
        }

        for (int i = 0; i < walls.Length; i++)
        {
            ShutterWallBlockRise wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            if (open)
            {
                wall.TryOpen();
            }
            else
            {
                wall.TryClose();
            }
        }
    }

    private void ActivateBossCamera()
    {
        if (fixedBossCamera == null)
        {
            return;
        }

        fixedBossCamera.Priority.Value = activeCameraPriority;
        fixedBossCamera.Priority.Enabled = true;
    }

    private void DeactivateBossCamera()
    {
        if (fixedBossCamera == null)
        {
            return;
        }

        fixedBossCamera.Priority.Value = inactiveCameraPriority;
        fixedBossCamera.Priority.Enabled = true;
    }

    private void DisableTriggerComponents()
    {
        Collider2D trigger2D = GetComponent<Collider2D>();
        if (trigger2D != null)
        {
            trigger2D.enabled = false;
        }

        Collider trigger3D = GetComponent<Collider>();
        if (trigger3D != null)
        {
            trigger3D.enabled = false;
        }
    }
}
