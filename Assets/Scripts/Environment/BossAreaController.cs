using System;
using System.Collections;
using System.Collections.Generic;
using GameName.Enemy;
using Metroidvania.Enemy;
using Metroidvania.Managers;
using Metroidvania.Player;
using Player;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

    [Header("StageBoss Intro")]
    // StageBoss 専用の登場演出。LastBoss は従来通り即時開始させる。
    [SerializeField] private bool playStageBossIntro = true;
    [SerializeField] private bool hideStageBossUntilIntro = true;
    // 入室直後は部屋とカメラだけ先に固定し、少し間を置いてからプレイヤーを止める。
    [SerializeField, Min(0f)] private float stageBossEntryDelaySeconds = 2f;
    [SerializeField, Min(0f)] private float stageBossNormalBgmFadeOutSeconds = 1f;
    // 上から入室・ジャンプ中に空中で固まらないよう、接地後にロックする。
    [SerializeField] private bool waitForStageBossPlayerGroundedBeforeLock = true;
    [SerializeField, Min(0f)] private float stageBossRevealDuration = 3f;
    [SerializeField, Min(0f)] private float stageBossHpLeadInSeconds = 1f;
    [SerializeField] private bool lockPlayerFacingStageBoss = true;
    [SerializeField, Min(0f)] private float stageBossMirageAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float stageBossMirageFrequency = 8f;

    [Header("Walls")]
    [SerializeField] private ShutterWallBlockRise[] wallsCloseOnStart = new ShutterWallBlockRise[0];
    [SerializeField] private ShutterWallBlockRise[] wallsOpenOnStart = new ShutterWallBlockRise[0];

    [Header("Camera")]
    [SerializeField] private CinemachineCamera fixedBossCamera;
    [SerializeField] private DualTargetCameraTarget dualTargetCameraTarget;
    [SerializeField] private int activeCameraPriority = 50;
    [SerializeField] private int inactiveCameraPriority = 0;
    [SerializeField] private bool restoreRoomCameraAfterBoss = true;

    [Header("BGM")]
    [SerializeField] private StageBgmController stageBgm;
    [SerializeField] private AudioClip bossBgm;
    [SerializeField, Range(0f, 1f)] private float bossBgmVolume = 0.2f;
    [SerializeField] private bool returnToNormalAfterBoss;

    [Header("Story Events")]
    [SerializeField] private string preEncounterStoryEventId = string.Empty;
    [SerializeField] private bool waitForPreEncounterStoryEvent = true;
    [SerializeField] private string lastBossHealthThresholdStoryEventId = string.Empty;
    [SerializeField, Range(0.01f, 1f)] private float lastBossHealthThresholdRate = 0.4f;
    [SerializeField] private bool waitForLastBossHealthThresholdStoryEvent = true;
    [SerializeField] private string postDefeatStoryEventId = string.Empty;
    [SerializeField] private bool waitForPostDefeatStoryEvent = true;
    [SerializeField] private bool logMissingStoryEvents = true;

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
    private static readonly List<BossAreaController> StageBossIntroWindSuppressors = new List<BossAreaController>();
#if UNITY_EDITOR
    private static Func<bool> activeDialogueRunningOverride;
#endif

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
    private StageBossIntroVisualState stageBossIntroVisualState;
    private StageBossIntroPlayerLockState stageBossIntroPlayerLockState;
    private Coroutine stageBossIntroRoutine;
    private bool suppressWindRiseDuringStageBossIntro;
    private Coroutine encounterStartRoutine;
    private Coroutine encounterCompleteRoutine;
    private Coroutine lastBossHealthThresholdStoryRoutine;
    private LastBossController subscribedLastBossController;
    private bool lastBossHealthThresholdStoryEventPlayed;

    public int Priority => 240;
    public string BossDisplayName => ResolveBossDisplayName();
    public Transform BossRoot => bossRoot;
    public StageBossAttack StageBossAttack => stageBossAttack;
    public LastBossController LastBossController => lastBossController;
    public IBossHealthSource BossHealthSource => ResolveBossHealthSource();
    public bool IsEncounterCompleted =>
        encounterCompleted || IsBossDefeatedInSavedProgress();

    public bool TryGetActiveBossHorizontalConfinementBounds(out Bounds bounds)
    {
        bounds = confinementBounds;
        return encounterStarted &&
               !encounterCompleted &&
               confineInsideArea &&
               confineBossInsideArea &&
               confineX &&
               hasConfinementBounds;
    }

    public static bool ShouldSuppressWindRiseAt(Vector3 worldPosition)
    {
        for (int i = StageBossIntroWindSuppressors.Count - 1; i >= 0; i--)
        {
            BossAreaController bossArea = StageBossIntroWindSuppressors[i];
            if (bossArea == null || !bossArea.suppressWindRiseDuringStageBossIntro)
            {
                StageBossIntroWindSuppressors.RemoveAt(i);
                continue;
            }

            if (bossArea.IsSuppressingWindRiseAt(worldPosition))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ShouldSuppressWindRise(Collider2D windCollider)
    {
        if (windCollider == null)
        {
            return false;
        }

        Bounds windBounds = windCollider.bounds;
        for (int i = StageBossIntroWindSuppressors.Count - 1; i >= 0; i--)
        {
            BossAreaController bossArea = StageBossIntroWindSuppressors[i];
            if (bossArea == null || !bossArea.suppressWindRiseDuringStageBossIntro)
            {
                StageBossIntroWindSuppressors.RemoveAt(i);
                continue;
            }

            if (bossArea.IsSuppressingWindRise(windBounds))
            {
                return true;
            }
        }

        return false;
    }

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
        ApplyInitialStageBossIntroVisibility();
    }

    private void OnEnable()
    {
        SaveManager.RegisterModule(this);
    }

    private void OnDisable()
    {
        StopEncounterStoryRoutines();
        UnsubscribeLastBossHealthChanged();
        StopStageBossIntroRoutine();
        RestoreStageBossIntroPlayerLock();
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
        TryStartEncounterFrom2DTrigger(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // 端に触れた直後は開始せず、歩いて完全に入ったタイミングで開始できるよう Stay でも確認する。
        TryStartEncounterFrom2DTrigger(collision);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            TryStartEncounter();
        }
    }

    private void TryStartEncounterFrom2DTrigger(Collider2D sourceCollider)
    {
        if (encounterStarted || encounterCompleted)
        {
            return;
        }

        // 攻撃・傘・パリィなどの子トリガーではなく、プレイヤー本体コライダーだけを入口判定に使う。
        if (!TryResolvePlayerBodyCollider(sourceCollider, out Collider2D bodyCollider))
        {
            return;
        }

        // 本体が半分だけ触れた状態で拘束を始めると、FixedUpdate の補正で部屋内へ押し込まれて見える。
        // そのため、拘束範囲へ完全に入るまではボス戦を開始しない。
        if (!IsPlayerBodyFullyInsideArea(bodyCollider))
        {
            return;
        }

        CachePlayerReferencesFromBodyCollider(bodyCollider);
        TryStartEncounter();
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
        lastBossHealthThresholdStoryEventPlayed = false;
        SubscribeLastBossHealthChanged();

        encounterStarted = true;
        RegisterActiveDodgeBounds();

        if (enableWallMechanic)
        {
            LockArea();
        }

        ActivateBossCamera();

        if (disableTriggerAfterStart)
        {
            DisableTriggerComponents();
        }

        if (ShouldPlayStageBossIntro())
        {
            BeginStageBossIntroWindSuppression();
        }

        if (TryPlayConfiguredStoryEvent(
                preEncounterStoryEventId,
                waitForPreEncounterStoryEvent,
                waitForExternalTrigger: true,
                out IEnumerator preEncounterStoryRoutine))
        {
            if (waitForPreEncounterStoryEvent)
            {
                encounterStartRoutine = StartCoroutine(StartEncounterAfterStoryRoutine(preEncounterStoryRoutine));
            }
            else
            {
                BeginBossCombatSequence();
            }
        }
        else
        {
            BeginBossCombatSequence();
        }

        if (verboseLogging)
        {
            Debug.Log($"[BossAreaController] Encounter started on {gameObject.name}", this);
        }
    }

    private IEnumerator StartEncounterAfterStoryRoutine(IEnumerator storyRoutine)
    {
        yield return storyRoutine;
        yield return WaitForActivePreEncounterDialogueRoutine();

        encounterStartRoutine = null;
        if (encounterCompleted || !encounterStarted)
        {
            yield break;
        }

        BeginBossCombatSequence();
    }

    private IEnumerator WaitForActivePreEncounterDialogueRoutine()
    {
        while (encounterStarted && !encounterCompleted && IsActiveDialogueRunning())
        {
            yield return null;
        }
    }

    private static bool IsActiveDialogueRunning()
    {
#if UNITY_EDITOR
        if (activeDialogueRunningOverride != null)
        {
            return activeDialogueRunningOverride();
        }
#endif

        DialogueManager dialogueManager = FindFirstObjectByType<DialogueManager>();
        return dialogueManager != null &&
               dialogueManager.Runner != null &&
               dialogueManager.Runner.IsDialogueRunning;
    }

    private void BeginBossCombatSequence()
    {
        if (ShouldPlayStageBossIntro())
        {
            // StageBoss は通常BGMを先に薄くして、HP表示完了後にボスBGMへ切り替える。
            stageBgm?.FadeOutCurrent(stageBossNormalBgmFadeOutSeconds);
            BeginStageBossIntroWindSuppression();
            stageBossIntroRoutine = StartCoroutine(PlayStageBossIntroRoutine());
        }
        else
        {
            // LastBoss など StageBoss 以外はこれまで通り即時に戦闘開始する。
            stageBgm?.PlayBoss(bossBgm, bossBgmVolume);
            ActivateAssignedBoss();
            EncounterStarted?.Invoke(this);
        }
    }

    private bool ShouldPlayStageBossIntro()
    {
        return playStageBossIntro && stageBossAttack != null && lastBossController == null;
    }

    private void ActivateAssignedBoss()
    {
        if (stageBossAttack != null)
        {
            RestoreStageBossForCombat();
            stageBossAttack.ActivateEncounter();
        }
        else if (lastBossController != null)
        {
            lastBossController.ActivateEncounter();
        }
    }

    private IEnumerator PlayStageBossIntroRoutine()
    {
        CaptureStageBossIntroVisualState();

        // 入室してすぐ固めず、プレイヤーが部屋に入ったことを見せるための待ち時間。
        float entryDelaySeconds = Mathf.Max(0f, stageBossEntryDelaySeconds);
        if (entryDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(entryDelaySeconds);
        }

        CachePlayerReferences();
        // 空中で停止すると不自然なので、接地を待ってから操作ロックへ進む。
        yield return WaitForStageBossPlayerGroundedBeforeLock();

        CachePlayerReferences();
        stageBossIntroPlayerLockState = StageBossIntroPlayerLockState.Capture(
            playerRoot,
            bossRoot,
            lockPlayerFacingStageBoss);
        stageBossIntroPlayerLockState?.ApplyLock();
        // 登場時点でボスもプレイヤーの方を向かせ、突進開始時の向きズレを防ぐ。
        FaceStageBossTowardPlayer();

        if (stageBossIntroVisualState != null)
        {
            yield return stageBossIntroVisualState.PlayReveal(
                this,
                stageBossRevealDuration,
                stageBossMirageAmplitude,
                stageBossMirageFrequency);
        }

        EncounterStarted?.Invoke(this);

        float leadInSeconds = Mathf.Max(0f, stageBossHpLeadInSeconds);
        if (leadInSeconds > 0f)
        {
            yield return new WaitForSeconds(leadInSeconds);
        }

        // HPバーのフェードインが終わる想定タイミングでボスBGMとAIを開始する。
        stageBgm?.PlayBoss(bossBgm, bossBgmVolume);
        RestoreStageBossForCombat();
        stageBossAttack?.ActivateEncounter();
        RestoreStageBossIntroPlayerLock();
        EndStageBossIntroWindSuppression();
        stageBossIntroRoutine = null;
    }

    private IEnumerator WaitForStageBossPlayerGroundedBeforeLock()
    {
        if (!waitForStageBossPlayerGroundedBeforeLock)
        {
            yield break;
        }

        // GroundCheck がある場合は実際の接地判定を毎フレーム確認する。
        while (!IsPlayerGroundedForStageBossIntro())
        {
            yield return null;
        }
    }

    private bool IsPlayerGroundedForStageBossIntro()
    {
        if (playerRoot == null)
        {
            return true;
        }

        GroundCheck groundCheck = playerRoot.GetComponentInChildren<GroundCheck>(true);
        if (groundCheck != null)
        {
            return groundCheck.IsGround();
        }

        PlayerController playerController = playerRoot.GetComponent<PlayerController>();
        if (playerController == null)
        {
            playerController = playerRoot.GetComponentInChildren<PlayerController>(true);
        }

        // テスト用・特殊プレイヤーなど接地情報が無い場合は、演出が詰まらないよう待たない。
        return playerController == null || !playerController.enabled || playerController.IsGrounded;
    }

    private void FaceStageBossTowardPlayer()
    {
        if (bossRoot == null)
        {
            return;
        }

        if (playerRoot == null)
        {
            CachePlayerReferences();
        }

        if (playerRoot == null)
        {
            return;
        }

        EnemyController enemyController = bossRoot.GetComponent<EnemyController>();
        if (enemyController == null)
        {
            enemyController = bossRoot.GetComponentInChildren<EnemyController>(true);
        }

        if (enemyController == null)
        {
            return;
        }

        int direction = playerRoot.position.x >= bossRoot.position.x ? 1 : -1;
        enemyController.FaceDirection(direction);
    }

    private void StopStageBossIntroRoutine()
    {
        if (stageBossIntroRoutine == null)
        {
            EndStageBossIntroWindSuppression();
            return;
        }

        StopCoroutine(stageBossIntroRoutine);
        stageBossIntroRoutine = null;
        EndStageBossIntroWindSuppression();
    }

    private void RestoreStageBossIntroPlayerLock()
    {
        stageBossIntroPlayerLockState?.Restore();
        stageBossIntroPlayerLockState = null;
    }

    private void BeginStageBossIntroWindSuppression()
    {
        if (!hasConfinementBounds)
        {
            CacheConfinementBounds();
        }

        suppressWindRiseDuringStageBossIntro = true;
        if (!StageBossIntroWindSuppressors.Contains(this))
        {
            StageBossIntroWindSuppressors.Add(this);
        }
    }

    private void EndStageBossIntroWindSuppression()
    {
        suppressWindRiseDuringStageBossIntro = false;
        StageBossIntroWindSuppressors.Remove(this);
    }

    private bool IsSuppressingWindRiseAt(Vector3 worldPosition)
    {
        return suppressWindRiseDuringStageBossIntro &&
               encounterStarted &&
               !encounterCompleted &&
               ShouldPlayStageBossIntro() &&
               hasConfinementBounds &&
               confinementBounds.Contains(worldPosition);
    }

    private bool IsSuppressingWindRise(Bounds windBounds)
    {
        return suppressWindRiseDuringStageBossIntro &&
               encounterStarted &&
               !encounterCompleted &&
               ShouldPlayStageBossIntro() &&
               hasConfinementBounds &&
               confinementBounds.Intersects(windBounds);
    }

    private void StopEncounterStoryRoutines()
    {
        if (encounterStartRoutine != null)
        {
            StopCoroutine(encounterStartRoutine);
            encounterStartRoutine = null;
        }

        if (encounterCompleteRoutine != null)
        {
            StopCoroutine(encounterCompleteRoutine);
            encounterCompleteRoutine = null;
        }

        if (lastBossHealthThresholdStoryRoutine != null)
        {
            StopCoroutine(lastBossHealthThresholdStoryRoutine);
            lastBossHealthThresholdStoryRoutine = null;
        }
    }

    private void SubscribeLastBossHealthChanged()
    {
        if (ReferenceEquals(subscribedLastBossController, lastBossController))
        {
            return;
        }

        UnsubscribeLastBossHealthChanged();

        if (ReferenceEquals(lastBossController, null))
        {
            return;
        }

        subscribedLastBossController = lastBossController;
        subscribedLastBossController.HealthChanged += HandleLastBossHealthChanged;
    }

    private void UnsubscribeLastBossHealthChanged()
    {
        if (ReferenceEquals(subscribedLastBossController, null))
        {
            return;
        }

        subscribedLastBossController.HealthChanged -= HandleLastBossHealthChanged;
        subscribedLastBossController = null;
    }

    private void HandleLastBossHealthChanged(int currentHealth, int maxHealth)
    {
        if (!encounterStarted ||
            encounterCompleted ||
            lastBossHealthThresholdStoryEventPlayed ||
            string.IsNullOrWhiteSpace(lastBossHealthThresholdStoryEventId) ||
            currentHealth <= 0 ||
            maxHealth <= 0)
        {
            return;
        }

        float healthRate = (float)currentHealth / maxHealth;
        float thresholdRate = Mathf.Clamp(lastBossHealthThresholdRate, 0.01f, 1f);
        if (healthRate > thresholdRate)
        {
            return;
        }

        lastBossHealthThresholdStoryEventPlayed = true;
        if (!TryPlayConfiguredStoryEvent(
                lastBossHealthThresholdStoryEventId,
                waitForLastBossHealthThresholdStoryEvent,
                waitForExternalTrigger: false,
                out IEnumerator storyRoutine))
        {
            return;
        }

        if (waitForLastBossHealthThresholdStoryEvent && storyRoutine != null)
        {
            if (lastBossHealthThresholdStoryRoutine != null)
            {
                StopCoroutine(lastBossHealthThresholdStoryRoutine);
            }

            lastBossHealthThresholdStoryRoutine =
                StartCoroutine(WaitForLastBossHealthThresholdStoryRoutine(storyRoutine));
        }
    }

    private IEnumerator WaitForLastBossHealthThresholdStoryRoutine(IEnumerator storyRoutine)
    {
        yield return storyRoutine;
        lastBossHealthThresholdStoryRoutine = null;
    }

    private bool TryPlayConfiguredStoryEvent(
        string storyEventId,
        bool waitForCompletion,
        bool waitForExternalTrigger,
        out IEnumerator storyRoutine)
    {
        storyRoutine = null;

        if (string.IsNullOrWhiteSpace(storyEventId))
        {
            return false;
        }

        string trimmedEventId = storyEventId.Trim();
        if (waitForCompletion)
        {
            storyRoutine = waitForExternalTrigger
                ? WaitForConfiguredStoryEventTriggerAndCompletion(trimmedEventId)
                : PlayConfiguredStoryEventAndWait(trimmedEventId);
            return true;
        }

        bool started = StoryEventRuntimeService.TryPlayEvent(trimmedEventId);
        if (!started)
        {
            LogMissingStoryEvent(trimmedEventId);
        }

        return started;
    }

    private IEnumerator PlayConfiguredStoryEventAndWait(string storyEventId)
    {
        bool started = false;
        yield return StoryEventRuntimeService.PlayEventAndWait(storyEventId, result => started = result);

        if (!started)
        {
            LogMissingStoryEvent(storyEventId);
        }
    }

    private IEnumerator WaitForConfiguredStoryEventTriggerAndCompletion(string storyEventId)
    {
        bool observedEventRunning = false;
        bool loggedMissingEvent = false;

        while (encounterStarted && !encounterCompleted)
        {
            if (TryFindSceneStoryEventController(storyEventId, out StoryEventController controller) &&
                controller != null)
            {
                if (controller.HasCompleted)
                {
                    yield break;
                }

                if (controller.IsPlaying)
                {
                    observedEventRunning = true;
                }
            }
            else if (!loggedMissingEvent)
            {
                LogMissingStoryEvent(storyEventId);
                loggedMissingEvent = true;
            }

            bool hasRuntimeStoryActivity = StoryEventRuntimeService.HasPendingEvents || IsActiveDialogueRunning();
            if (hasRuntimeStoryActivity)
            {
                observedEventRunning = true;
            }

            if (observedEventRunning && !hasRuntimeStoryActivity)
            {
                yield break;
            }

            yield return null;
        }
    }

    private static bool TryFindSceneStoryEventController(
        string storyEventId,
        out StoryEventController matchedController)
    {
        matchedController = null;

        if (string.IsNullOrWhiteSpace(storyEventId))
        {
            return false;
        }

        string trimmedEventId = storyEventId.Trim();
        string activeSceneName = SceneManager.GetActiveScene().name;
        StoryEventController fallbackController = null;

        StoryEventController[] controllers =
            FindObjectsByType<StoryEventController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            StoryEventController controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            bool matchesId =
                string.Equals(controller.EventId, trimmedEventId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(controller.name, trimmedEventId, StringComparison.OrdinalIgnoreCase);
            if (!matchesId)
            {
                continue;
            }

            if (controller.gameObject.scene.IsValid() &&
                string.Equals(controller.gameObject.scene.name, activeSceneName, StringComparison.Ordinal))
            {
                matchedController = controller;
                return true;
            }

            fallbackController ??= controller;
        }

        matchedController = fallbackController;
        return matchedController != null;
    }

    private void LogMissingStoryEvent(string storyEventId)
    {
        if (!logMissingStoryEvents)
        {
            return;
        }

        Debug.LogWarning(
            $"[BossAreaController] Story event was not found or could not start. eventId='{storyEventId}', bossArea='{name}'",
            this);
    }

    private void ApplyInitialStageBossIntroVisibility()
    {
        if (encounterCompleted || !hideStageBossUntilIntro || !ShouldPlayStageBossIntro())
        {
            return;
        }

        // シーン配置済みのStageBossだけを、入室前は見えず当たらない状態にしておく。
        CaptureStageBossIntroVisualState();
        stageBossIntroVisualState?.ApplyHidden();
    }

    private void CaptureStageBossIntroVisualState()
    {
        if (bossRoot == null || stageBossAttack == null)
        {
            return;
        }

        if (stageBossIntroVisualState == null ||
            !stageBossIntroVisualState.IsForRoot(bossRoot))
        {
            stageBossIntroVisualState = StageBossIntroVisualState.Capture(bossRoot);
        }
    }

    private void RestoreStageBossForCombat()
    {
        if (stageBossAttack == null)
        {
            return;
        }

        // 演出用に無効化した表示・当たり判定・Rigidbodyを戦闘用へ戻す。
        CaptureStageBossIntroVisualState();
        stageBossIntroVisualState?.RestoreForCombat();
        ReapplyStageBossPassThroughCollision();

        if (bossRoot != null && bossRigidbody2D == null)
        {
            bossRigidbody2D = bossRoot.GetComponent<Rigidbody2D>();
        }
    }

    private void ReapplyStageBossPassThroughCollision()
    {
        if (bossRoot == null)
        {
            return;
        }

        EnemyContact enemyContact = bossRoot.GetComponent<EnemyContact>();
        if (enemyContact == null)
        {
            enemyContact = bossRoot.GetComponentInChildren<EnemyContact>(true);
        }

        enemyContact?.ReapplyPassThroughPlayerCollision();
    }

    private void CompleteEncounter()
    {
        if (encounterCompleted)
        {
            return;
        }

        UnsubscribeLastBossHealthChanged();
        StopStageBossIntroRoutine();
        RestoreStageBossIntroPlayerLock();

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

        EncounterCompleted?.Invoke(this);

        if (TryPlayConfiguredStoryEvent(
                postDefeatStoryEventId,
                waitForPostDefeatStoryEvent,
                waitForExternalTrigger: false,
                out IEnumerator postDefeatStoryRoutine))
        {
            if (waitForPostDefeatStoryEvent)
            {
                encounterCompleteRoutine = StartCoroutine(FinishEncounterAfterStoryRoutine(postDefeatStoryRoutine));
            }
            else
            {
                FinishEncounterCompletion();
            }
        }
        else
        {
            FinishEncounterCompletion();
        }

        if (verboseLogging)
        {
            Debug.Log($"[BossAreaController] Encounter completed on {gameObject.name}", this);
        }
    }

    private IEnumerator FinishEncounterAfterStoryRoutine(IEnumerator storyRoutine)
    {
        yield return storyRoutine;
        encounterCompleteRoutine = null;
        FinishEncounterCompletion();
    }

    private void FinishEncounterCompletion()
    {
        if (enableWallMechanic)
        {
            UnlockArea();
        }

        DeactivateBossCamera();

        if (restoreRoomCameraAfterBoss)
        {
            RestoreRoomCameraAfterBoss();
        }

        if (returnToNormalAfterBoss)
        {
            stageBgm?.PlayNormal();
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

        UnsubscribeLastBossHealthChanged();
        StopStageBossIntroRoutine();
        RestoreStageBossIntroPlayerLock();

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
        UnsubscribeLastBossHealthChanged();
        lastBossHealthThresholdStoryEventPlayed = false;
        StopStageBossIntroRoutine();
        RestoreStageBossIntroPlayerLock();

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
        ApplyInitialStageBossIntroVisibility();
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

    private bool TryResolvePlayerBodyCollider(Collider2D sourceCollider, out Collider2D bodyCollider)
    {
        bodyCollider = null;
        if (sourceCollider == null)
        {
            return false;
        }

        // 直接当たったのが本体コライダーなら、そのまま採用する。
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

        // 子オブジェクト側のトリガーが触れた場合でも、PlayerHealth から本体コライダーへ戻して判定する。
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

        // 戦闘開始後に拘束する軸だけを、開始前の「完全に入っている」条件にも使う。
        if (confineX &&
            (bodyBounds.min.x < confinementBounds.min.x - tolerance ||
             bodyBounds.max.x > confinementBounds.max.x + tolerance))
        {
            return false;
        }

        Rigidbody2D bodyRigidbody = bodyCollider.attachedRigidbody != null
            ? bodyCollider.attachedRigidbody
            : bodyCollider.GetComponent<Rigidbody2D>();
        bool canConfineY = confineY && ShouldConfineYForTarget(bodyRigidbody);
        if (canConfineY &&
            (bodyBounds.min.y < confinementBounds.min.y - tolerance ||
             bodyBounds.max.y > confinementBounds.max.y + tolerance))
        {
            return false;
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

        ConfigureDualTargetCamera();
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

    private void RestoreRoomCameraAfterBoss()
    {
        RoomCameraTrigger roomTrigger = GetComponent<RoomCameraTrigger>();
        if (roomTrigger != null)
        {
            AlignDefaultFollowCameraBeforeRoomRestore(roomTrigger);
            roomTrigger.ActivateCamera();
            return;
        }

        Transform player = playerRoot != null ? playerRoot : ResolvePlayerRoot();
        if (player != null)
        {
            RoomCameraTrigger.TryActivateRoomAtPosition(player.position, out _);
        }
    }

    private void AlignDefaultFollowCameraBeforeRoomRestore(RoomCameraTrigger roomTrigger)
    {
        if (roomTrigger == null || !roomTrigger.UsesDefaultCameraWhenEntered || fixedBossCamera == null)
        {
            return;
        }

        CameraManager.Instance?.TrySetFollowCameraPose(
            fixedBossCamera.transform.position,
            fixedBossCamera.Lens.OrthographicSize);
    }

    private void ConfigureDualTargetCamera()
    {
        if (dualTargetCameraTarget == null)
        {
            dualTargetCameraTarget = GetComponentInChildren<DualTargetCameraTarget>(true);
        }

        if (dualTargetCameraTarget == null && fixedBossCamera != null)
        {
            GameObject targetObject = new GameObject("BossCameraTarget");
            targetObject.transform.SetParent(transform, false);
            targetObject.transform.position = fixedBossCamera.transform.position;
            dualTargetCameraTarget = targetObject.AddComponent<DualTargetCameraTarget>();
        }

        Transform primary = playerRoot != null ? playerRoot : ResolvePlayerRoot();
        Transform secondary = bossRoot;
        dualTargetCameraTarget.Configure(primary, secondary, fixedBossCamera, GetComponent<Collider2D>());
    }

    private Transform ResolvePlayerRoot()
    {
        CachePlayerReferences();
        return playerRoot;
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

    private sealed class StageBossIntroVisualState
    {
        // SpriteRenderer以外のRendererでも色フェードできるよう、代表的な色プロパティを探す。
        private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
        private static readonly Color MirageWarm = new Color(1f, 0.78f, 0.38f, 1f);
        private static readonly Color MirageCool = new Color(0.45f, 0.95f, 1f, 1f);

        private readonly Transform root;
        private readonly Vector3 originalLocalPosition;
        private readonly RendererState[] rendererStates;
        private readonly SpriteRendererState[] spriteRendererStates;
        private readonly Collider2DState[] collider2DStates;
        private readonly ColliderState[] colliderStates;
        private readonly Rigidbody2D rigidbody2D;
        private readonly bool rigidbodySimulated;
        private readonly List<SpriteGhostLayer> ghostLayers = new List<SpriteGhostLayer>();

        private StageBossIntroVisualState(
            Transform root,
            RendererState[] rendererStates,
            SpriteRendererState[] spriteRendererStates,
            Collider2DState[] collider2DStates,
            ColliderState[] colliderStates,
            Rigidbody2D rigidbody2D)
        {
            this.root = root;
            originalLocalPosition = root != null ? root.localPosition : Vector3.zero;
            this.rendererStates = rendererStates ?? Array.Empty<RendererState>();
            this.spriteRendererStates = spriteRendererStates ?? Array.Empty<SpriteRendererState>();
            this.collider2DStates = collider2DStates ?? Array.Empty<Collider2DState>();
            this.colliderStates = colliderStates ?? Array.Empty<ColliderState>();
            this.rigidbody2D = rigidbody2D;
            rigidbodySimulated = rigidbody2D == null || rigidbody2D.simulated;
        }

        public static StageBossIntroVisualState Capture(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            // 元の表示・当たり判定状態を保存し、演出後にPrefab/シーン設定へ戻せるようにする。
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            RendererState[] rendererStates = new RendererState[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                rendererStates[i] = new RendererState(renderers[i]);
            }

            SpriteRenderer[] spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRendererState[] spriteRendererStates = new SpriteRendererState[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                spriteRendererStates[i] = new SpriteRendererState(spriteRenderers[i]);
            }

            Collider2D[] colliders2D = root.GetComponentsInChildren<Collider2D>(true);
            Collider2DState[] collider2DStates = new Collider2DState[colliders2D.Length];
            for (int i = 0; i < colliders2D.Length; i++)
            {
                collider2DStates[i] = new Collider2DState(colliders2D[i]);
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            ColliderState[] colliderStates = new ColliderState[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                colliderStates[i] = new ColliderState(colliders[i]);
            }

            return new StageBossIntroVisualState(
                root,
                rendererStates,
                spriteRendererStates,
                collider2DStates,
                colliderStates,
                root.GetComponent<Rigidbody2D>());
        }

        public bool IsForRoot(Transform targetRoot)
        {
            return root == targetRoot;
        }

        public void ApplyHidden()
        {
            if (root == null)
            {
                return;
            }

            root.gameObject.SetActive(true);
            DestroyGhostLayers();
            // 非表示中もGameObject自体は生かし、割り当て参照や撃破保存処理を壊さない。
            SetRendererStates(enabled: false);
            SetSpriteReveal(0f, 0f, 0f, 0f);
            SetColliderStates(enabled: false);
            SetRigidbodySimulated(false);
        }

        public IEnumerator PlayReveal(
            MonoBehaviour owner,
            float duration,
            float amplitude,
            float frequency)
        {
            if (root == null || owner == null)
            {
                yield break;
            }

            root.gameObject.SetActive(true);
            RestoreRendererStates();
            // 本体の薄いフェードだけだと弱いため、色ズレした複製スプライトで蜃気楼感を足す。
            CreateGhostLayers();
            SetColliderStates(enabled: false);
            SetRigidbodySimulated(false);

            duration = Mathf.Max(0f, duration);
            if (duration <= 0f)
            {
                SetSpriteReveal(1f, 0f, amplitude, frequency);
                RestoreVisuals();
                yield break;
            }

            float elapsed = 0f;
            SetSpriteReveal(0f, 0f, amplitude, frequency);

            while (elapsed < duration)
            {
                // EditModeテストや一時停止中でも手動実行が進むよう、deltaTimeが0なら固定値を使う。
                float deltaTime = Time.deltaTime > 0f ? Time.deltaTime : Time.unscaledDeltaTime;
                if (deltaTime <= 0f)
                {
                    deltaTime = 1f / 60f;
                }

                elapsed += deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float eased = normalized * normalized * (3f - 2f * normalized);
                SetSpriteReveal(eased, elapsed, amplitude, frequency);
                yield return null;
            }

            RestoreVisuals();
        }

        public void RestoreForCombat()
        {
            RestoreVisuals();
            RestoreColliderStates();
            RestoreRigidbody();
        }

        private void RestoreVisuals()
        {
            if (root != null)
            {
                root.localPosition = originalLocalPosition;
            }

            // 演出で触った色・位置・ゴーストを完全に戻し、戦闘中の見た目へ影響を残さない。
            RestoreRendererStates();
            RestoreSpriteColors();
            DestroyGhostLayers();
        }

        private void SetSpriteReveal(float reveal, float elapsed, float amplitude, float frequency)
        {
            reveal = Mathf.Clamp01(reveal);

            if (root != null)
            {
                // 本体を小さく横揺れさせ、遠景の蜃気楼のような揺らぎにする。
                float wave = Mathf.Sin(elapsed * Mathf.Max(0f, frequency) * Mathf.PI * 2f);
                float offset = wave * Mathf.Max(0f, amplitude) * (1f - reveal);
                root.localPosition = originalLocalPosition + new Vector3(offset, 0f, 0f);
            }

            float pulse = Mathf.Sin(elapsed * Mathf.Max(0f, frequency) * Mathf.PI * 2f) * 0.5f + 0.5f;
            Color tint = Color.Lerp(MirageWarm, MirageCool, pulse);
            float tintWeight = Mathf.Lerp(0.4f, 0f, reveal);

            for (int i = 0; i < spriteRendererStates.Length; i++)
            {
                SpriteRendererState state = spriteRendererStates[i];
                if (state.Renderer == null)
                {
                    continue;
                }

                Color color = Color.Lerp(state.Color, tint, tintWeight);
                color.a = state.Color.a * reveal;
                state.Renderer.color = color;
            }

            SetRendererReveal(reveal, tint, tintWeight);
            SetGhostReveal(reveal, elapsed, amplitude, frequency);
        }

        private void SetRendererReveal(float reveal, Color tint, float tintWeight)
        {
            // SpriteRenderer以外の見た目にも、MaterialPropertyBlockで可能な範囲の色フェードをかける。
            for (int i = 0; i < rendererStates.Length; i++)
            {
                rendererStates[i].ApplyReveal(reveal, tint, tintWeight);
            }
        }

        private void CreateGhostLayers()
        {
            DestroyGhostLayers();

            // 各スプライトの前後に暖色/寒色のゴーストを作り、色収差っぽく見せる。
            for (int i = 0; i < spriteRendererStates.Length; i++)
            {
                SpriteRendererState state = spriteRendererStates[i];
                if (!state.CanCreateGhost)
                {
                    continue;
                }

                ghostLayers.Add(SpriteGhostLayer.Create(state.Renderer, MirageWarm, -1, -1));
                ghostLayers.Add(SpriteGhostLayer.Create(state.Renderer, MirageCool, 1, 1));
            }
        }

        private void SetGhostReveal(float reveal, float elapsed, float amplitude, float frequency)
        {
            float safeFrequency = Mathf.Max(0f, frequency);
            float wave = Mathf.Sin(elapsed * safeFrequency * Mathf.PI * 2f);
            float pulse = wave * 0.5f + 0.5f;
            float split = Mathf.Max(0.02f, amplitude) * (1f - reveal) * Mathf.Lerp(2.5f, 4f, pulse);
            float alpha = Mathf.Lerp(0.42f, 0f, reveal) * Mathf.Lerp(0.65f, 1f, pulse);

            // 本体がはっきりするにつれてゴーストを薄くし、最後は完全に消す。
            for (int i = 0; i < ghostLayers.Count; i++)
            {
                ghostLayers[i].Apply(split, alpha);
            }
        }

        private void DestroyGhostLayers()
        {
            for (int i = 0; i < ghostLayers.Count; i++)
            {
                ghostLayers[i].Destroy();
            }

            ghostLayers.Clear();
        }

        private void SetRendererStates(bool enabled)
        {
            for (int i = 0; i < rendererStates.Length; i++)
            {
                if (rendererStates[i].Renderer != null)
                {
                    rendererStates[i].Renderer.enabled = enabled;
                }
            }
        }

        private void RestoreRendererStates()
        {
            for (int i = 0; i < rendererStates.Length; i++)
            {
                rendererStates[i].Restore();
            }
        }

        private void RestoreSpriteColors()
        {
            for (int i = 0; i < spriteRendererStates.Length; i++)
            {
                spriteRendererStates[i].Restore();
            }
        }

        private void SetColliderStates(bool enabled)
        {
            for (int i = 0; i < collider2DStates.Length; i++)
            {
                if (collider2DStates[i].Collider != null)
                {
                    collider2DStates[i].Collider.enabled = enabled;
                }
            }

            for (int i = 0; i < colliderStates.Length; i++)
            {
                if (colliderStates[i].Collider != null)
                {
                    colliderStates[i].Collider.enabled = enabled;
                }
            }
        }

        private void RestoreColliderStates()
        {
            for (int i = 0; i < collider2DStates.Length; i++)
            {
                collider2DStates[i].Restore();
            }

            for (int i = 0; i < colliderStates.Length; i++)
            {
                colliderStates[i].Restore();
            }
        }

        private void SetRigidbodySimulated(bool simulated)
        {
            if (rigidbody2D == null)
            {
                return;
            }

            rigidbody2D.linearVelocity = Vector2.zero;
            rigidbody2D.angularVelocity = 0f;
            rigidbody2D.simulated = simulated;
        }

        private void RestoreRigidbody()
        {
            if (rigidbody2D == null)
            {
                return;
            }

            rigidbody2D.simulated = rigidbodySimulated;
            rigidbody2D.linearVelocity = Vector2.zero;
            rigidbody2D.angularVelocity = 0f;
        }

        private readonly struct RendererState
        {
            private readonly MaterialPropertyBlock originalPropertyBlock;
            private readonly int colorPropertyId;
            private readonly Color color;

            public RendererState(Renderer renderer)
            {
                Renderer = renderer;
                Enabled = renderer != null && renderer.enabled;
                colorPropertyId = ResolveColorPropertyId(renderer);
                color = ResolveColor(renderer, colorPropertyId);
                originalPropertyBlock = new MaterialPropertyBlock();
                if (renderer != null)
                {
                    renderer.GetPropertyBlock(originalPropertyBlock);
                }
            }

            public Renderer Renderer { get; }
            private bool Enabled { get; }

            public void ApplyReveal(float reveal, Color tint, float tintWeight)
            {
                if (Renderer == null || Renderer is SpriteRenderer || colorPropertyId == 0)
                {
                    return;
                }

                // sharedMaterialを書き換えず、Renderer単位の一時色だけを上書きする。
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                Renderer.GetPropertyBlock(block);

                Color revealColor = Color.Lerp(color, tint, tintWeight);
                revealColor.a = color.a * reveal;
                block.SetColor(colorPropertyId, revealColor);
                Renderer.SetPropertyBlock(block);
            }

            public void Restore()
            {
                if (Renderer != null)
                {
                    Renderer.enabled = Enabled;
                    Renderer.SetPropertyBlock(originalPropertyBlock);
                }
            }

            private static int ResolveColorPropertyId(Renderer renderer)
            {
                Material material = renderer != null ? renderer.sharedMaterial : null;
                if (material == null)
                {
                    return 0;
                }

                if (material.HasProperty(BaseColorPropertyId))
                {
                    return BaseColorPropertyId;
                }

                return material.HasProperty(ColorPropertyId) ? ColorPropertyId : 0;
            }

            private static Color ResolveColor(Renderer renderer, int propertyId)
            {
                Material material = renderer != null ? renderer.sharedMaterial : null;
                if (material == null || propertyId == 0)
                {
                    return Color.white;
                }

                return material.GetColor(propertyId);
            }
        }

        private readonly struct SpriteRendererState
        {
            public SpriteRendererState(SpriteRenderer renderer)
            {
                Renderer = renderer;
                Color = renderer != null ? renderer.color : Color.white;
                Enabled = renderer != null && renderer.enabled;
            }

            public SpriteRenderer Renderer { get; }
            public Color Color { get; }
            public bool Enabled { get; }
            public bool CanCreateGhost => Renderer != null && Enabled && Renderer.sprite != null;

            public void Restore()
            {
                if (Renderer != null)
                {
                    Renderer.color = Color;
                }
            }
        }

        private sealed class SpriteGhostLayer
        {
            private readonly SpriteRenderer source;
            private readonly SpriteRenderer ghost;
            private readonly Color tint;
            private readonly int direction;

            private SpriteGhostLayer(
                SpriteRenderer source,
                SpriteRenderer ghost,
                Color tint,
                int direction)
            {
                this.source = source;
                this.ghost = ghost;
                this.tint = tint;
                this.direction = direction;
            }

            public static SpriteGhostLayer Create(
                SpriteRenderer source,
                Color tint,
                int direction,
                int sortingOffset)
            {
                // DontSaveにして、実行中だけ存在する演出用オブジェクトとして扱う。
                GameObject ghostObject = new GameObject($"{source.name}_MirageGhost");
                ghostObject.hideFlags = HideFlags.DontSave;
                ghostObject.transform.SetParent(source.transform, false);
                ghostObject.transform.localPosition = Vector3.zero;
                ghostObject.transform.localRotation = Quaternion.identity;
                ghostObject.transform.localScale = Vector3.one;

                SpriteRenderer ghost = ghostObject.AddComponent<SpriteRenderer>();
                CopySpriteState(source, ghost, sortingOffset);
                ghost.color = new Color(tint.r, tint.g, tint.b, 0f);

                return new SpriteGhostLayer(source, ghost, tint, direction);
            }

            public void Apply(float split, float alpha)
            {
                if (source == null || ghost == null)
                {
                    return;
                }

                // 元スプライトの反転・ソートなどを毎フレーム追従し、アニメ中の見た目ズレを避ける。
                CopySpriteState(source, ghost, direction);
                ghost.transform.localPosition = Vector3.right * direction * split;

                Color sourceColor = source.color;
                ghost.color = new Color(
                    tint.r,
                    tint.g,
                    tint.b,
                    Mathf.Clamp01(alpha * sourceColor.a));
                ghost.enabled = source.enabled;
            }

            public void Destroy()
            {
                if (ghost == null)
                {
                    return;
                }

                GameObject ghostObject = ghost.gameObject;
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(ghostObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(ghostObject);
                }
            }

            private static void CopySpriteState(
                SpriteRenderer source,
                SpriteRenderer target,
                int sortingOffset)
            {
                if (source == null || target == null)
                {
                    return;
                }

                target.sprite = source.sprite;
                target.drawMode = source.drawMode;
                target.size = source.size;
                target.tileMode = source.tileMode;
                target.adaptiveModeThreshold = source.adaptiveModeThreshold;
                target.flipX = source.flipX;
                target.flipY = source.flipY;
                target.maskInteraction = source.maskInteraction;
                target.spriteSortPoint = source.spriteSortPoint;
                target.sortingLayerID = source.sortingLayerID;
                target.sortingOrder = source.sortingOrder + sortingOffset;
                target.sharedMaterial = source.sharedMaterial;
            }
        }

        private readonly struct Collider2DState
        {
            public Collider2DState(Collider2D collider)
            {
                Collider = collider;
                Enabled = collider != null && collider.enabled;
            }

            public Collider2D Collider { get; }
            private bool Enabled { get; }

            public void Restore()
            {
                if (Collider != null)
                {
                    Collider.enabled = Enabled;
                }
            }
        }

        private readonly struct ColliderState
        {
            public ColliderState(Collider collider)
            {
                Collider = collider;
                Enabled = collider != null && collider.enabled;
            }

            public Collider Collider { get; }
            private bool Enabled { get; }

            public void Restore()
            {
                if (Collider != null)
                {
                    Collider.enabled = Enabled;
                }
            }
        }
    }

    private sealed class StageBossIntroPlayerLockState
    {
        // 入力だけでなく攻撃・回避などの能動アクションも一時停止する対象。
        private static readonly string[] PlayerActionBehaviourNames =
        {
            "DodgeController",
            "PlayerShooter",
            "GunController",
            "UmbrellaController",
            "UmbrellaAttackController",
            "UmbrellaParryController",
            "FallThroughController"
        };

        private readonly Transform playerRoot;
        private readonly Transform bossRoot;
        private readonly PlayerController playerController;
        private readonly bool controlLocked;
        private readonly bool facingLocked;
        private readonly bool facingRight;
        private readonly bool lockFacingBoss;
        private readonly PlayerInput playerInput;
        private readonly bool playerInputEnabled;
        private readonly Rigidbody2D rigidbody2D;
        private readonly RigidbodyConstraints2D rigidbodyConstraints;
        private readonly List<BehaviourState> behaviourStates;
        private bool restored;

        private StageBossIntroPlayerLockState(
            Transform playerRoot,
            Transform bossRoot,
            bool lockFacingBoss,
            PlayerController playerController,
            PlayerInput playerInput,
            Rigidbody2D rigidbody2D,
            List<BehaviourState> behaviourStates)
        {
            this.playerRoot = playerRoot;
            this.bossRoot = bossRoot;
            this.lockFacingBoss = lockFacingBoss;
            this.playerController = playerController;
            this.playerInput = playerInput;
            this.rigidbody2D = rigidbody2D;
            this.behaviourStates = behaviourStates ?? new List<BehaviourState>();

            controlLocked = playerController != null && playerController.IsExternalControlLocked;
            facingLocked = playerController != null && playerController.IsExternalFacingLocked;
            facingRight = playerController == null || playerController.IsFacingRight;
            playerInputEnabled = playerInput != null && playerInput.enabled;
            rigidbodyConstraints = rigidbody2D != null ? rigidbody2D.constraints : RigidbodyConstraints2D.None;
        }

        public static StageBossIntroPlayerLockState Capture(
            Transform playerRoot,
            Transform bossRoot,
            bool lockFacingBoss)
        {
            if (playerRoot == null)
            {
                return null;
            }

            PlayerController playerController = playerRoot.GetComponent<PlayerController>();
            if (playerController == null)
            {
                playerController = playerRoot.GetComponentInChildren<PlayerController>(true);
            }

            PlayerInput playerInput = playerRoot.GetComponent<PlayerInput>();
            if (playerInput == null)
            {
                playerInput = playerRoot.GetComponentInChildren<PlayerInput>(true);
            }

            Rigidbody2D rigidbody2D = playerRoot.GetComponent<Rigidbody2D>();
            List<BehaviourState> behaviourStates = new List<BehaviourState>();
            // 復帰時に元のenabled状態へ戻せるよう、ロック対象の状態を先に保存する。
            MonoBehaviour[] behaviours = playerRoot.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour == playerController || !IsPlayerActionBehaviour(behaviour))
                {
                    continue;
                }

                behaviourStates.Add(new BehaviourState(behaviour));
            }

            return new StageBossIntroPlayerLockState(
                playerRoot,
                bossRoot,
                lockFacingBoss,
                playerController,
                playerInput,
                rigidbody2D,
                behaviourStates);
        }

        public void ApplyLock()
        {
            if (restored)
            {
                return;
            }

            DodgeController dodgeController = playerRoot != null ? playerRoot.GetComponent<DodgeController>() : null;
            dodgeController?.CancelCurrentDodgeMovement();

            // PlayerControllerの外部ロックを使い、入力処理側にも「操作不可」を伝える。
            if (playerController != null)
            {
                playerController.SetExternalControlLocked(true);

                if (lockFacingBoss)
                {
                    playerController.SetExternalFacingLocked(true, ShouldFaceRight());
                }
            }

            if (playerInput != null)
            {
                playerInput.enabled = false;
            }

            for (int i = 0; i < behaviourStates.Count; i++)
            {
                behaviourStates[i].ApplyLock();
            }

            if (rigidbody2D != null)
            {
                // 速度と物理拘束を同時に止め、ロック開始直後の滑りや落下を防ぐ。
                rigidbody2D.linearVelocity = Vector2.zero;
                rigidbody2D.angularVelocity = 0f;
                rigidbody2D.constraints = RigidbodyConstraints2D.FreezeAll;
                rigidbody2D.Sleep();
            }
        }

        public void Restore()
        {
            if (restored)
            {
                return;
            }

            restored = true;

            if (rigidbody2D != null)
            {
                rigidbody2D.constraints = rigidbodyConstraints;
                rigidbody2D.linearVelocity = Vector2.zero;
                rigidbody2D.angularVelocity = 0f;
                rigidbody2D.WakeUp();
            }

            for (int i = 0; i < behaviourStates.Count; i++)
            {
                behaviourStates[i].Restore();
            }

            if (playerInput != null)
            {
                playerInput.enabled = playerInputEnabled;
            }

            if (playerController != null)
            {
                // 一瞬だけ元の向きを戻してからロック状態を復元し、表示向きの取り残しを避ける。
                playerController.SetExternalFacingLocked(true, facingRight);
                playerController.SetExternalControlLocked(controlLocked);
                playerController.SetExternalFacingLocked(facingLocked, facingRight);
            }
        }

        private bool ShouldFaceRight()
        {
            if (playerRoot == null || bossRoot == null)
            {
                return facingRight;
            }

            return bossRoot.position.x >= playerRoot.position.x;
        }

        private static bool IsPlayerActionBehaviour(MonoBehaviour behaviour)
        {
            string typeName = behaviour.GetType().Name;
            for (int i = 0; i < PlayerActionBehaviourNames.Length; i++)
            {
                if (PlayerActionBehaviourNames[i] == typeName)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct BehaviourState
        {
            public BehaviourState(Behaviour behaviour)
            {
                Behaviour = behaviour;
                Enabled = behaviour != null && behaviour.enabled;
            }

            private Behaviour Behaviour { get; }
            private bool Enabled { get; }

            public void ApplyLock()
            {
                if (Behaviour != null)
                {
                    Behaviour.enabled = false;
                }
            }

            public void Restore()
            {
                if (Behaviour != null)
                {
                    Behaviour.enabled = Enabled;
                }
            }
        }
    }
}
