using Player;
using UnityEngine;

/// <summary>
/// Bridges player action lifecycles into the Elegant Point chain state machine.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ElegantActionSuccessSensor))]
[DefaultExecutionOrder(100)]
public sealed class PlayerElegantPointController : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float chainTimeoutSeconds = ElegantActionChain.DefaultTimeoutSeconds;
    [SerializeField, Min(1)] private int pointsPerSuccess = 10;
    [SerializeField, Min(0.1f)] private float minimumRecoilDistance = 2f;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private UmbrellaAttackController normalAttackController;
    [SerializeField] private PlayerDiveAttackController diveAttackController;
    [SerializeField] private UmbrellaController glideController;
    [SerializeField] private GunController recoilController;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerEquipmentController equipmentController;

    private ElegantActionChain chain;
    private float pendingElegantPointFraction;
    private ElegantActionSuccessSensor successSensor;
    private Vector2 recoilOrigin;
    private Vector2 previousRecoilPosition;
    private bool recoilPending;
    private bool recoilCounted;
    private int recoilSequence;
    private int publishedLevel;

    public event System.Action<int> LevelChanged;
    public event System.Action<ElegantActionType, int> ActionSucceeded;
    public event System.Action<ElegantActionType, int> ActionRefined;

    private UmbrellaAttackController subscribedNormalAttackController;
    private PlayerDiveAttackController subscribedDiveAttackController;
    private UmbrellaController subscribedGlideController;
    private GunController subscribedRecoilController;
    private PlayerHealth subscribedPlayerHealth;

    private bool actionActivitySinceLastUpdate;

    public bool IsChainArmed => chain != null && chain.IsArmed;
    public int CurrentChainLength => chain != null ? chain.ActionCount : 0;
    public ElegantActionType? LastAction => chain?.LastAction;
    public bool IsAttackPending => chain != null && chain.IsAttackInProgress;

    private void Awake()
    {
        EnsureChain();
        ResolveReferences();
    }

    private void OnEnable()
    {
        EnsureChain();
        ResolveReferences();
        RefreshSubscriptions();
        successSensor.Succeeded += NotifySuccessfulAction;
    }

    private void Update()
    {
        AdvanceActions(Time.deltaTime);
    }

    private void AdvanceActions(float deltaTime)
    {
        ResolveReferences();
        RefreshSubscriptions();
        if (playerHealth != null && playerHealth.CurrentHealth <= 0) return;

        bool glideActionActive = glideController != null && glideController.IsGlideMotionActive;
        bool dodging = playerController != null && playerController.IsDodging;
        bool recoilActionActive = recoilController != null && recoilController.IsAirborneRecoilActive;
        UpdateRecoilSuccess();
        // A glide session may outlive a chain (for example a long recoil interruption).
        // Resuming actual glide after settlement starts a new level-one chain.
        if (!chain.IsArmed && glideActionActive) NotifySuccessfulAction(ElegantActionType.Glide);

        bool eligibleActionHeld = IsSuccessfulActionHeld(glideActionActive, dodging, recoilActionActive) ||
            actionActivitySinceLastUpdate;

        actionActivitySinceLastUpdate = false;
        Award(chain.Advance(deltaTime, eligibleActionHeld));
        PublishLevel();
    }

    private void OnDisable()
    {
        if (successSensor != null) successSensor.Succeeded -= NotifySuccessfulAction;
        UnsubscribeAll();
        actionActivitySinceLastUpdate = false;
        if (Application.isPlaying && Time.timeScale > 0f) SettleChain();
    }

    private void OnDestroy()
    {
        if (chain == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Award(chain.Settle());
        }
        else
        {
            chain.ResetWithoutReward();
        }
    }

    private void OnValidate()
    {
        chainTimeoutSeconds = Mathf.Max(0.01f, chainTimeoutSeconds);
        pointsPerSuccess = Mathf.Max(1, pointsPerSuccess);
        minimumRecoilDistance = Mathf.Max(0.1f, minimumRecoilDistance);
    }

    /// <summary>
    /// Public integration entry point for valid action-start edges.
    /// Attack actions remain pending until NotifyAttackHit/Ended is called.
    /// </summary>
    public void NotifyActionStarted(ElegantActionType action)
    {
        EnsureChain();
        int reward = action == ElegantActionType.NormalAttack || action == ElegantActionType.DiveAttack
            ? chain.RegisterAttackStart(action)
            : 0;
        Award(reward);
    }

    public void NotifyAttackHit(ElegantActionType action)
    {
        EnsureChain();
        int before = chain.ActionCount;
        Award(chain.RegisterAttackHit(action));
        PublishSuccess(before);
    }

    public void NotifyAttackEnded(ElegantActionType action, bool hitEnemy)
    {
        EnsureChain();
        int before = chain.ActionCount;
        Award(chain.RegisterAttackEnd(action, hitEnemy));
        PublishSuccess(before);
    }

    public int SettleChain()
    {
        EnsureChain();
        int reward = chain.Settle();
        Award(reward);
        PublishLevel();
        return reward;
    }

    private void EnsureChain()
    {
        if (chain == null)
        {
            chain = new ElegantActionChain(chainTimeoutSeconds, pointsPerSuccess);
        }
    }

    private void ResolveReferences()
    {
        if (successSensor == null)
        {
            successSensor = GetComponent<ElegantActionSuccessSensor>();
            if (successSensor == null) successSensor = gameObject.AddComponent<ElegantActionSuccessSensor>();
        }
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
            if (playerController == null)
            {
                playerController = GetComponentInParent<PlayerController>();
            }
        }

        if (normalAttackController == null)
        {
            normalAttackController = GetComponentInChildren<UmbrellaAttackController>(true);
            if (normalAttackController == null)
            {
                normalAttackController = GetComponentInParent<UmbrellaAttackController>();
            }
        }

        if (diveAttackController == null)
        {
            diveAttackController = GetComponent<PlayerDiveAttackController>();
            if (diveAttackController == null)
            {
                diveAttackController = GetComponentInParent<PlayerDiveAttackController>();
            }
        }

        if (glideController == null)
        {
            glideController = GetComponentInChildren<UmbrellaController>(true);
            if (glideController == null)
            {
                glideController = GetComponentInParent<UmbrellaController>();
            }
        }

        if (recoilController == null)
        {
            recoilController = GetComponentInChildren<GunController>(true);
            if (recoilController == null)
            {
                recoilController = GetComponentInParent<GunController>();
            }
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
            if (playerHealth == null)
            {
                playerHealth = GetComponentInParent<PlayerHealth>();
            }
        }

        if (equipmentController == null)
        {
            equipmentController = GetComponent<PlayerEquipmentController>();
            if (equipmentController == null)
            {
                equipmentController = GetComponentInParent<PlayerEquipmentController>();
            }
        }
    }

    private void RefreshSubscriptions()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (subscribedNormalAttackController != normalAttackController)
        {
            UnsubscribeNormalAttack();
            subscribedNormalAttackController = normalAttackController;
            if (subscribedNormalAttackController != null)
            {
                subscribedNormalAttackController.ActionStarted += HandleNormalAttackStarted;
                subscribedNormalAttackController.FirstEnemyHit += HandleNormalAttackHit;
                subscribedNormalAttackController.ActionEnded += HandleNormalAttackEnded;
                subscribedNormalAttackController.EnemyHitResolved += HandleNormalAttackResult;
            }
        }

        if (subscribedDiveAttackController != diveAttackController)
        {
            UnsubscribeDiveAttack();
            subscribedDiveAttackController = diveAttackController;
            if (subscribedDiveAttackController != null)
            {
                subscribedDiveAttackController.ActionStarted += HandleDiveAttackStarted;
                subscribedDiveAttackController.FirstEnemyHit += HandleDiveAttackHit;
                subscribedDiveAttackController.ActionEnded += HandleDiveAttackEnded;
                subscribedDiveAttackController.BouncedOnEnemy += HandleDiveBounce;
            }
        }

        if (subscribedGlideController != glideController)
        {
            UnsubscribeGlide();
            subscribedGlideController = glideController;
            if (subscribedGlideController != null)
            {
                subscribedGlideController.GlideStarted += HandleGlideStarted;
            }
        }

        if (subscribedRecoilController != recoilController)
        {
            UnsubscribeRecoil();
            subscribedRecoilController = recoilController;
            if (subscribedRecoilController != null)
            {
                subscribedRecoilController.AirborneRecoilStarted += HandleAirborneRecoilStarted;
            }
        }

        if (subscribedPlayerHealth != playerHealth)
        {
            UnsubscribePlayerHealth();
            subscribedPlayerHealth = playerHealth;
            if (subscribedPlayerHealth != null)
            {
                subscribedPlayerHealth.Died += HandlePlayerDied;
            }
        }
    }

    private void UnsubscribeAll()
    {
        UnsubscribeNormalAttack();
        UnsubscribeDiveAttack();
        UnsubscribeGlide();
        UnsubscribeRecoil();
        UnsubscribePlayerHealth();
    }

    private void UnsubscribeNormalAttack()
    {
        if (subscribedNormalAttackController == null)
        {
            return;
        }

        subscribedNormalAttackController.ActionStarted -= HandleNormalAttackStarted;
        subscribedNormalAttackController.FirstEnemyHit -= HandleNormalAttackHit;
        subscribedNormalAttackController.ActionEnded -= HandleNormalAttackEnded;
        subscribedNormalAttackController.EnemyHitResolved -= HandleNormalAttackResult;
        subscribedNormalAttackController = null;
    }

    private void UnsubscribeDiveAttack()
    {
        if (subscribedDiveAttackController == null)
        {
            return;
        }

        subscribedDiveAttackController.ActionStarted -= HandleDiveAttackStarted;
        subscribedDiveAttackController.FirstEnemyHit -= HandleDiveAttackHit;
        subscribedDiveAttackController.ActionEnded -= HandleDiveAttackEnded;
        subscribedDiveAttackController.BouncedOnEnemy -= HandleDiveBounce;
        subscribedDiveAttackController = null;
    }

    private void UnsubscribeGlide()
    {
        if (subscribedGlideController == null)
        {
            return;
        }

        subscribedGlideController.GlideStarted -= HandleGlideStarted;
        subscribedGlideController = null;
    }

    private void UnsubscribeRecoil()
    {
        if (subscribedRecoilController == null)
        {
            return;
        }

        subscribedRecoilController.AirborneRecoilStarted -= HandleAirborneRecoilStarted;
        subscribedRecoilController = null;
    }

    private void UnsubscribePlayerHealth()
    {
        if (subscribedPlayerHealth == null)
        {
            return;
        }

        subscribedPlayerHealth.Died -= HandlePlayerDied;
        subscribedPlayerHealth = null;
    }

    private void HandleNormalAttackStarted()
    {
        NotifyActionStarted(ElegantActionType.NormalAttack);
    }

    private void HandleNormalAttackHit()
    {
        NotifyAttackHit(ElegantActionType.NormalAttack);
    }

    private void HandleNormalAttackEnded(bool hitEnemy)
    {
        NotifyAttackEnded(ElegantActionType.NormalAttack, hitEnemy);
    }

    private void HandleDiveAttackStarted()
    {
        NotifyActionStarted(ElegantActionType.DiveAttack);
    }

    private void HandleDiveAttackHit()
    {
        NotifyAttackHit(ElegantActionType.DiveAttack);
    }

    private void HandleDiveAttackEnded(bool hitEnemy)
    {
        NotifyAttackEnded(ElegantActionType.DiveAttack, hitEnemy);
    }

    private void HandleGlideStarted()
    {
        NotifySuccessfulAction(ElegantActionType.Glide);
    }

    private void HandleAirborneRecoilStarted()
    {
        recoilOrigin = recoilController.RecoilStartPosition;
        previousRecoilPosition = transform.position;
        recoilSequence = recoilController.RecoilSequence;
        recoilPending = true;
        recoilCounted = false;
    }

    public void NotifySuccessfulAction(ElegantActionType action)
    {
        if (!isActiveAndEnabled) return;
        EnsureChain();
        int before = chain.ActionCount;
        chain.RegisterSuccess(action);
        PublishSuccess(before);
    }

    private void PublishSuccess(int before)
    {
        if (chain.ActionCount <= before) return;
        actionActivitySinceLastUpdate = true;
        PublishLevel();
        ActionSucceeded?.Invoke(chain.LastAction.Value, chain.ActionCount);
    }

    private void PublishLevel()
    {
        if (publishedLevel == CurrentChainLength) return;
        publishedLevel = CurrentChainLength;
        LevelChanged?.Invoke(publishedLevel);
    }

    private void HandleNormalAttackResult(Component enemy, bool killed, bool fromBehind)
    {
        if (!killed || !fromBehind || !successSensor.ConsumeOverheadCrossing(enemy)) return;
        int before = chain.ActionCount;
        chain.RegisterAttackSuccess(ElegantActionType.NormalAttack, ElegantActionType.OverheadBackKill);
        PublishSuccess(before);
    }

    private void HandleDiveBounce()
    {
        if (chain.RefineDiveBounce())
            ActionRefined?.Invoke(ElegantActionType.DiveBounce, chain.ActionCount);
    }

    private void UpdateRecoilSuccess()
    {
        if (!recoilPending || recoilController == null) return;
        Vector2 position = transform.position;
        bool teleported = Vector2.Distance(position, previousRecoilPosition) > 4f;
        previousRecoilPosition = position;
        if (teleported || recoilSequence != recoilController.RecoilSequence)
        {
            recoilPending = false;
            return;
        }
        if (!recoilCounted &&
            Vector2.Distance(recoilOrigin, position) >= minimumRecoilDistance)
        {
            recoilCounted = true;
            NotifySuccessfulAction(recoilController.CurrentRecoilIsJump ? ElegantActionType.RecoilJump : ElegantActionType.RecoilMove);
        }
        if (!recoilController.IsAirborneRecoilActive) recoilPending = false;
    }

    private bool IsSuccessfulActionHeld(bool glide, bool dodge, bool recoil)
    {
        // A sustained action that is still happening holds the window open even when it is no
        // longer the latest success. Dodging or recoiling mid-glide must not settle the chain
        // while the glide itself continues.
        if (glide || (dodge && successSensor.CurrentDodgeSucceeded) || (recoil && recoilCounted)) return true;
        switch (chain.LastAction)
        {
            case ElegantActionType.DiveAttack:
            case ElegantActionType.DiveBounce:
                return chain.LatestSuccessBelongsToCurrentAttack &&
                    ((diveAttackController != null && diveAttackController.IsDiveAttacking) ||
                    (playerController != null && playerController.IsDiveAttackLanding));
            case ElegantActionType.OverheadBackKill:
                return chain.LatestSuccessBelongsToCurrentAttack && normalAttackController != null && normalAttackController.IsAttacking();
            default: return false;
        }
    }

    private void HandlePlayerDied()
    {
        SettleChain();
    }

    private void Award(int reward)
    {
        if (reward <= 0)
        {
            return;
        }

        ResolveReferences();
        float multiplier = equipmentController != null
            ? Mathf.Max(0f, equipmentController.ElegantPointGainMultiplier)
            : 1f;
        float scaledReward = reward * multiplier + pendingElegantPointFraction;
        int amount = Mathf.FloorToInt(scaledReward);
        pendingElegantPointFraction = scaledReward - amount;

        if (amount > 0)
        {
            int amountAdded = ElegantPointWallet.Add(amount);
            if (amountAdded > 0)
            {
                ElegantPointGainEvents.Raise(amountAdded, ElegantPointWallet.Balance, transform.position);
            }
        }
    }
}
