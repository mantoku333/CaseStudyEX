using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure state machine for an Elegant Point action chain. It does not read
/// Unity object state or mutate the wallet; callers award the returned value.
/// </summary>
public sealed class ElegantActionChain
{
    public const float DefaultTimeoutSeconds = 1f;

    private readonly float timeoutSeconds;

    private bool isArmed;
    private int actionCount;
    private ElegantActionType? lastAction;
    private readonly HashSet<ElegantActionType> actionsInChain =
        new HashSet<ElegantActionType>();
    private float inactiveSeconds;

    private bool attackInProgress;
    private bool activeAttackHitEnemy;
    private ElegantActionType activeAttack;
    private readonly List<ElegantActionType> deferredNonAttackActions = new List<ElegantActionType>();

    public ElegantActionChain(float timeoutSeconds = DefaultTimeoutSeconds)
    {
        this.timeoutSeconds = Mathf.Max(0.01f, timeoutSeconds);
    }

    public bool IsArmed => isArmed;
    public int ActionCount => actionCount;
    public ElegantActionType? LastAction => lastAction;
    public float InactiveSeconds => inactiveSeconds;
    public bool IsAttackInProgress => attackInProgress;

    /// <summary>
    /// Registers a glide, dodge, or airborne recoil start edge.
    /// Returns a reward when the action repeats any action already used by
    /// the current chain.
    /// </summary>
    public int RegisterActionStart(ElegantActionType action)
    {
        if (IsAttack(action) || !isArmed)
        {
            return 0;
        }

        // A valid action edge can overlap an unresolved attack. Preserve it
        // only when a chain was already armed; an unarmed chain must still be
        // started by the attack's first enemy hit.
        if (attackInProgress && !activeAttackHitEnemy)
        {
            deferredNonAttackActions.Add(action);
            return 0;
        }

        if (actionsInChain.Contains(action))
        {
            return SettleCurrentChain();
        }

        AppendAction(action);
        return 0;
    }

    /// <summary>
    /// Starts a normal or dive attack. The attack remains pending until its
    /// first enemy hit or its completion reports a miss.
    /// </summary>
    public int RegisterAttackStart(ElegantActionType action)
    {
        if (!IsAttack(action) || attackInProgress)
        {
            return 0;
        }

        int reward = 0;
        if (isArmed && actionsInChain.Contains(action))
        {
            reward = SettleCurrentChain();
        }

        attackInProgress = true;
        activeAttackHitEnemy = false;
        activeAttack = action;
        deferredNonAttackActions.Clear();
        inactiveSeconds = 0f;
        return reward;
    }

    /// <summary>
    /// Resolves the pending attack as successful on its first enemy/boss hit.
    /// Additional hits from the same attack are ignored.
    /// </summary>
    public int RegisterAttackHit(ElegantActionType action)
    {
        if (!attackInProgress || activeAttack != action || activeAttackHitEnemy)
        {
            return 0;
        }

        activeAttackHitEnemy = true;
        AppendAction(action);

        int reward = 0;
        for (int i = 0; i < deferredNonAttackActions.Count; i++)
        {
            reward += RegisterActionStart(deferredNonAttackActions[i]);
        }

        deferredNonAttackActions.Clear();
        return reward;
    }

    /// <summary>
    /// Completes the pending attack. A miss settles the chain that existed
    /// before the attack and leaves the state disarmed.
    /// </summary>
    public int RegisterAttackEnd(ElegantActionType action, bool hitEnemy)
    {
        if (!attackInProgress || activeAttack != action)
        {
            return 0;
        }

        int reward = 0;
        if (hitEnemy && !activeAttackHitEnemy)
        {
            reward += RegisterAttackHit(action);
        }

        if (!activeAttackHitEnemy)
        {
            reward += SettleCurrentChain();
        }

        attackInProgress = false;
        activeAttackHitEnemy = false;
        deferredNonAttackActions.Clear();
        inactiveSeconds = 0f;
        return reward;
    }

    /// <summary>
    /// Advances the one-second inactivity window with scaled delta time.
    /// Passing zero while paused freezes the chain. Any held eligible action,
    /// including a pending attack, keeps a full timeout available after it ends.
    /// </summary>
    public int Advance(float scaledDeltaTime, bool eligibleActionHeld)
    {
        if (!isArmed)
        {
            return 0;
        }

        if (attackInProgress || eligibleActionHeld)
        {
            inactiveSeconds = 0f;
            return 0;
        }

        inactiveSeconds += Mathf.Max(0f, scaledDeltaTime);
        return inactiveSeconds >= timeoutSeconds
            ? SettleCurrentChain()
            : 0;
    }

    /// <summary>
    /// Settles the current chain and cancels any unresolved attack.
    /// Used for death and scene teardown.
    /// </summary>
    public int Settle()
    {
        int reward = SettleCurrentChain();
        attackInProgress = false;
        activeAttackHitEnemy = false;
        deferredNonAttackActions.Clear();
        inactiveSeconds = 0f;
        return reward;
    }

    public void ResetWithoutReward()
    {
        isArmed = false;
        actionCount = 0;
        lastAction = null;
        actionsInChain.Clear();
        inactiveSeconds = 0f;
        attackInProgress = false;
        activeAttackHitEnemy = false;
        deferredNonAttackActions.Clear();
    }

    public static int CalculateReward(int finalActionCount)
    {
        if (finalActionCount < 2)
        {
            return 0;
        }

        if (finalActionCount == 2)
        {
            return 10;
        }

        if (finalActionCount == 3)
        {
            return 20;
        }

        return finalActionCount * 10;
    }

    private void AppendAction(ElegantActionType action)
    {
        if (!isArmed)
        {
            isArmed = true;
            actionCount = 1;
        }
        else
        {
            actionCount++;
        }

        lastAction = action;
        actionsInChain.Add(action);
        inactiveSeconds = 0f;
    }

    private int SettleCurrentChain()
    {
        int reward = CalculateReward(actionCount);
        isArmed = false;
        actionCount = 0;
        lastAction = null;
        actionsInChain.Clear();
        inactiveSeconds = 0f;
        return reward;
    }

    private static bool IsAttack(ElegantActionType action)
    {
        return action == ElegantActionType.NormalAttack ||
               action == ElegantActionType.DiveAttack;
    }
}
