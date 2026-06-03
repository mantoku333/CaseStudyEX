using System;
using GameName.Enemy;
using Metroidvania.Player;
using Player;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// ボスエリア侵入で戦闘を開始し、
/// 撃破まで壁とカメラをロックするコントローラー。
/// </summary>
[DisallowMultipleComponent]
public sealed class BossAreaController : MonoBehaviour, ISaveDataModule
{
    public static event Action<BossAreaController> EncounterStarted;
    public static event Action<BossAreaController> EncounterCompleted;
    public static event Action<BossAreaController> EncounterReset;

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool disableTriggerAfterStart = true;

    [Header("Boss")]
    [SerializeField] private string bossDisplayName = "Boss";
    [SerializeField] private Transform bossRoot;
    [HideInInspector, SerializeField] private StageBossAttack stageBossAttack;
    [HideInInspector, SerializeField] private LastBossController lastBossController;
    [SerializeField] private string bossDefeatedFlagKey = GameProgressKeys.Boss01Defeated;
    [SerializeField] private bool hideBossWhenDefeated = true;

    [Header("Walls")]
    [SerializeField] private ShutterWallBlockRise[] wallsCloseOnStart = new ShutterWallBlockRise[0];
    [SerializeField] private ShutterWallBlockRise[] wallsOpenOnStart = new ShutterWallBlockRise[0];

    [Header("Camera")]
    [SerializeField] private CinemachineCamera fixedBossCamera;
    [SerializeField] private int activeCameraPriority = 50;
    [SerializeField] private int inactiveCameraPriority = 0;

    [Header("BGM")]
    [SerializeField] private StageBgmController stageBgm;
    [SerializeField] private AudioClip bossBgm;
    [SerializeField, Range(0f, 1f)] private float bossBgmVolume = 0.2f;
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
    // プレイヤー拘束は攻撃・パリィなどの子トリガーではなく、本体コライダーだけを見る。
    private Collider2D playerBodyCollider;
    // 回避移動がエリア拘束と競合しないよう、DodgeController に境界を渡すための参照。
    private DodgeController playerDodgeController;
    private Rigidbody2D bossRigidbody2D;

    public int Priority => 240;
    public string BossDisplayName => ResolveBossDisplayName();
    public Transform BossRoot => bossRoot;
    public StageBossAttack StageBossAttack => stageBossAttack;
    public LastBossController LastBossController => lastBossController;
    public IBossHealthSource BossHealthSource => ResolveBossHealthSource();

    private void Awake()
    {
        // 後でトリガーを無効化しても拘束範囲を使えるよう、起動時に bounds を確定しておく。
        CacheConfinementBounds();

        ResolveBossReferences(logIssues: false);
        ResolveStageBgm();

        CachePlayerReferences();

        // 開始時は固定カメラを非アクティブ優先度に戻す。
        DeactivateBossCamera();

        ApplyDefeatedStateIfSaved();
    }

    private void OnEnable()
    {
        SaveManager.RegisterModule(this);
    }

    private void OnDisable()
    {
        ClearActiveDodgeBounds();
        SaveManager.UnregisterModule(this);
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

        if (!ResolveBossReferences(logIssues: true))
        {
            return;
        }

        ResolveStageBgm();

        // 戦闘開始直前にプレイヤー参照を再取得しておく。
        CachePlayerReferences();

        encounterStarted = true;
        RegisterActiveDodgeBounds();

        if (enableWallMechanic)
        {
            LockArea();
        }

        // カメラ切り替え -> ボス起動 の順で開始演出を揃える。
        // カメラ/BGMをボス戦用へ切り替えてから、設定されているボスを起動する。
        ActivateBossCamera();
        stageBgm?.PlayBoss(bossBgm, bossBgmVolume);

        if (stageBossAttack != null)
        {
            stageBossAttack.ActivateEncounter();
        }
        else if (lastBossController != null)
        {
            lastBossController.ActivateEncounter();
        }

        EncounterStarted?.Invoke(this);

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
        ClearActiveDodgeBounds();

        if (stageBossAttack != null)
        {
            stageBossAttack.DeactivateEncounter();
        }
        else if (lastBossController != null)
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

        EncounterCompleted?.Invoke(this);

        if (verboseLogging)
        {
            Debug.Log($"[BossAreaController] Encounter completed on {gameObject.name}", this);
        }
    }

    public void Capture(SaveGameData saveData)
    {
        if (!encounterCompleted || string.IsNullOrWhiteSpace(bossDefeatedFlagKey))
        {
            return;
        }

        GameProgressFlags.Set(bossDefeatedFlagKey, true);
    }

    public void Restore(SaveGameData saveData)
    {
        if (IsBossDefeatedInSavedProgress())
        {
            ApplyDefeatedStateIfSaved();
            return;
        }

        ResetUnfinishedEncounterAfterLoad();
    }

    private void ApplyDefeatedStateIfSaved()
    {
        if (!IsBossDefeatedInSavedProgress())
        {
            return;
        }

        encounterCompleted = true;
        encounterStarted = false;
        ClearActiveDodgeBounds();

        if (stageBossAttack != null)
        {
            stageBossAttack.DeactivateEncounter();
        }
        else if (lastBossController != null)
        {
            lastBossController.DeactivateEncounter();
        }

        if (enableWallMechanic)
        {
            UnlockArea();
        }

        DeactivateBossCamera();
        DisableTriggerComponents();

        if (hideBossWhenDefeated && bossRoot != null)
        {
            bossRoot.gameObject.SetActive(false);
        }
    }

    private bool IsBossDefeatedInSavedProgress()
    {
        return !string.IsNullOrWhiteSpace(bossDefeatedFlagKey) && GameProgressFlags.Get(bossDefeatedFlagKey);
    }

    private void ResetUnfinishedEncounterAfterLoad()
    {
        encounterCompleted = false;
        encounterStarted = false;
        ClearActiveDodgeBounds();

        ResolveBossReferences(logIssues: false);

        if (bossRoot != null)
        {
            bossRoot.gameObject.SetActive(true);
        }

        if (stageBossAttack != null)
        {
            stageBossAttack.DeactivateEncounter();
        }
        else if (lastBossController != null)
        {
            lastBossController.DeactivateEncounter();
        }

        BossHealthSource?.ResetHealthToFull();

        if (enableWallMechanic)
        {
            UnlockArea();
        }

        DeactivateBossCamera();
        EnableTriggerComponents();
        EncounterReset?.Invoke(this);
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

    private bool ResolveBossReferences(bool logIssues)
    {
        StageBossAttack resolvedStageBoss = null;
        LastBossController resolvedLastBoss = null;

        if (bossRoot != null)
        {
            resolvedStageBoss = bossRoot.GetComponent<StageBossAttack>();
            resolvedLastBoss = bossRoot.GetComponent<LastBossController>();

            if (resolvedStageBoss == null && resolvedLastBoss == null)
            {
                if (logIssues)
                {
                    Debug.LogWarning($"[BossAreaController] Boss Root '{bossRoot.name}' has no StageBossAttack or LastBossController.", this);
                }

                stageBossAttack = null;
                lastBossController = null;
                bossRigidbody2D = null;
                return false;
            }
        }
        else
        {
            resolvedStageBoss = stageBossAttack;
            resolvedLastBoss = lastBossController;

            if (resolvedStageBoss != null)
            {
                bossRoot = resolvedStageBoss.transform;
            }
            else if (resolvedLastBoss != null)
            {
                bossRoot = resolvedLastBoss.transform;
            }
        }

        if (resolvedStageBoss != null && resolvedLastBoss != null)
        {
            if (logIssues)
            {
                Debug.LogError("[BossAreaController] Boss Root has both StageBossAttack and LastBossController. Assign exactly one boss type.", this);
            }

            stageBossAttack = null;
            lastBossController = null;
            bossRigidbody2D = null;
            return false;
        }

        stageBossAttack = resolvedStageBoss;
        lastBossController = resolvedLastBoss;
        bossRigidbody2D = bossRoot != null ? bossRoot.GetComponent<Rigidbody2D>() : null;

        if (stageBossAttack != null || lastBossController != null)
        {
            return true;
        }

        if (logIssues)
        {
            Debug.LogWarning("[BossAreaController] No boss is assigned.", this);
        }

        return false;
    }

    private string ResolveBossDisplayName()
    {
        string trimmedName = bossDisplayName != null ? bossDisplayName.Trim() : string.Empty;
        if (!string.IsNullOrEmpty(trimmedName))
        {
            return trimmedName;
        }

        if (bossRoot != null)
        {
            return bossRoot.name;
        }

        if (stageBossAttack != null)
        {
            return stageBossAttack.name;
        }

        if (lastBossController != null)
        {
            return lastBossController.name;
        }

        return "Boss";
    }

    private IBossHealthSource ResolveBossHealthSource()
    {
        if (lastBossController != null)
        {
            return lastBossController;
        }

        if (stageBossAttack != null)
        {
            return stageBossAttack.GetComponent<EnemyController>();
        }

        if (bossRoot == null)
        {
            return null;
        }

        LastBossController resolvedLastBoss = bossRoot.GetComponent<LastBossController>();
        if (resolvedLastBoss != null)
        {
            return resolvedLastBoss;
        }

        return bossRoot.GetComponent<EnemyController>();
    }

    private void ResolveStageBgm()
    {
        if (stageBgm != null)
        {
            return;
        }

        stageBgm = FindFirstObjectByType<StageBgmController>();
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
            ConstrainPlayerBodyInsideArea();
        }

        if (confineBossInsideArea)
        {
            ConstrainTransform(bossRoot, bossRigidbody2D);
        }
    }

    private void ConstrainPlayerBodyInsideArea()
    {
        // プレイヤーだけは本体コライダー基準で補正し、傘や攻撃判定で拘束範囲が広がらないようにする。
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
            float minX = confinementBounds.min.x;
            float maxX = confinementBounds.max.x;
            if (bodyBounds.min.x < minX)
            {
                offsetX = minX - bodyBounds.min.x;
            }
            else if (bodyBounds.max.x > maxX)
            {
                offsetX = maxX - bodyBounds.max.x;
            }
        }

        bool canConfineY = confineY && ShouldConfineYForTarget(playerRigidbody2D);
        if (canConfineY)
        {
            float minY = confinementBounds.min.y;
            float maxY = confinementBounds.max.y;
            if (bodyBounds.min.y < minY)
            {
                offsetY = minY - bodyBounds.min.y;
            }
            else if (bodyBounds.max.y > maxY)
            {
                offsetY = maxY - bodyBounds.max.y;
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
        // 回避開始時点で目標地点をエリア内に収め、FixedUpdate の拘束処理との振動を防ぐ。
        if (!confineInsideArea || !confinePlayerInsideArea || !hasConfinementBounds)
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
            Vector2.zero,
            confineX,
            confineY,
            confineYForDynamicBodies,
            playerBodyCollider);
    }

    private void ClearActiveDodgeBounds()
    {
        // このボスエリアが設定した回避制限だけを解除する。
        if (playerDodgeController != null)
        {
            playerDodgeController.ClearAreaDodgeBounds(this);
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

        PlayerHealth playerHealth = playerObject.GetComponent<PlayerHealth>();
        if (PlayerBodyColliderUtility.TryGetBodyCollider(playerHealth, out Collider2D bodyCollider))
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
        // PlayerHealth と同じルートにある非トリガー本体コライダーを基準に参照をそろえる。
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

    private void EnableTriggerComponents()
    {
        Collider2D trigger2D = GetComponent<Collider2D>();
        if (trigger2D != null)
        {
            trigger2D.enabled = true;
        }

        Collider trigger3D = GetComponent<Collider>();
        if (trigger3D != null)
        {
            trigger3D.enabled = true;
        }
    }
}
