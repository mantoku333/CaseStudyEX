using Player;
using UnityEngine;

/// <summary>
/// Bridges player action lifecycles into the Elegant Point chain state machine.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class PlayerElegantPointController : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float chainTimeoutSeconds = ElegantActionChain.DefaultTimeoutSeconds;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private UmbrellaAttackController normalAttackController;
    [SerializeField] private PlayerDiveAttackController diveAttackController;
    [SerializeField] private UmbrellaController glideController;
    [SerializeField] private GunController recoilController;
    [SerializeField] private PlayerHealth playerHealth;

    private ElegantActionChain chain;

    private UmbrellaAttackController subscribedNormalAttackController;
    private PlayerDiveAttackController subscribedDiveAttackController;
    private UmbrellaController subscribedGlideController;
    private GunController subscribedRecoilController;
    private PlayerHealth subscribedPlayerHealth;

    private bool previousGlideActionActive;
    private bool previousDodging;
    private bool previousRecoilActionActive;
    private bool hasCapturedActionState;
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
        CaptureActionState();
    }

    private void Update()
    {
        ResolveReferences();
        RefreshSubscriptions();

        if (!hasCapturedActionState)
        {
            CaptureActionState();
        }

        bool glideActionActive = glideController != null && glideController.IsGlideActionActive;
        bool dodging = playerController != null && playerController.IsDodging;
        bool recoilActionActive = recoilController != null && recoilController.IsAirborneRecoilActive;
        bool eligibleStateEndedThisFrame =
            (!glideActionActive && previousGlideActionActive) ||
            (!dodging && previousDodging) ||
            (!recoilActionActive && previousRecoilActionActive);

        if (dodging && !previousDodging)
        {
            NotifyActionStarted(ElegantActionType.Dodge);
        }

        previousGlideActionActive = glideActionActive;
        previousDodging = dodging;
        previousRecoilActionActive = recoilActionActive;

        bool eligibleActionHeld =
            glideActionActive ||
            dodging ||
            recoilActionActive ||
            (playerController != null && playerController.IsDiveAttackLanding) ||
            (normalAttackController != null && normalAttackController.IsAttacking()) ||
            (diveAttackController != null && diveAttackController.IsDiveAttacking) ||
            eligibleStateEndedThisFrame ||
            actionActivitySinceLastUpdate;

        actionActivitySinceLastUpdate = false;
        Award(chain.Advance(Time.deltaTime, eligibleActionHeld));
    }

    private void OnDisable()
    {
        UnsubscribeAll();
        hasCapturedActionState = false;
        actionActivitySinceLastUpdate = false;
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
    }

    /// <summary>
    /// Public integration entry point for valid action-start edges.
    /// Attack actions remain pending until NotifyAttackHit/Ended is called.
    /// </summary>
    public void NotifyActionStarted(ElegantActionType action)
    {
        EnsureChain();
        actionActivitySinceLastUpdate = true;

        int reward = action == ElegantActionType.NormalAttack || action == ElegantActionType.DiveAttack
            ? chain.RegisterAttackStart(action)
            : chain.RegisterActionStart(action);
        Award(reward);
    }

    public void NotifyAttackHit(ElegantActionType action)
    {
        EnsureChain();
        actionActivitySinceLastUpdate = true;
        Award(chain.RegisterAttackHit(action));
    }

    public void NotifyAttackEnded(ElegantActionType action, bool hitEnemy)
    {
        EnsureChain();
        actionActivitySinceLastUpdate = true;
        Award(chain.RegisterAttackEnd(action, hitEnemy));
    }

    public int SettleChain()
    {
        EnsureChain();
        int reward = chain.Settle();
        Award(reward);
        return reward;
    }

    private void EnsureChain()
    {
        if (chain == null)
        {
            chain = new ElegantActionChain(chainTimeoutSeconds);
        }
    }

    private void ResolveReferences()
    {
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

    private void CaptureActionState()
    {
        previousGlideActionActive = glideController != null && glideController.IsGlideActionActive;
        previousDodging = playerController != null && playerController.IsDodging;
        previousRecoilActionActive = recoilController != null && recoilController.IsAirborneRecoilActive;
        hasCapturedActionState = playerController != null;
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
        NotifyActionStarted(ElegantActionType.Glide);
    }

    private void HandleAirborneRecoilStarted()
    {
        NotifyActionStarted(ElegantActionType.RecoilMove);
    }

    private void HandlePlayerDied()
    {
        SettleChain();
    }

    private static void Award(int reward)
    {
        if (reward > 0)
        {
            ElegantPointWallet.Add(reward);
        }
    }
}
