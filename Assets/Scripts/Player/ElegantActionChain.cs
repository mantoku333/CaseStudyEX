using UnityEngine;

/// <summary>Successful actions, not input edges, advance this chain.</summary>
public sealed class ElegantActionChain
{
    public const float DefaultTimeoutSeconds = 1f;
    /// <summary>A single success is flourish only; the gauge accepts level two and above.</summary>
    public const int MinimumRewardedActionCount = 2;
    private readonly float timeoutSeconds;
    private readonly int pointsPerSuccess;
    private float inactiveSeconds;
    private bool attackInProgress;
    private bool attackCounted;
    private ElegantActionType activeAttack;
    private int attackStep;

    public ElegantActionChain(float timeoutSeconds = DefaultTimeoutSeconds, int pointsPerSuccess = 10)
    {
        this.timeoutSeconds = Mathf.Max(0.01f, timeoutSeconds);
        this.pointsPerSuccess = Mathf.Max(1, pointsPerSuccess);
    }

    public bool IsArmed => ActionCount > 0;
    public int ActionCount { get; private set; }
    public ElegantActionType? LastAction { get; private set; }
    public float InactiveSeconds => inactiveSeconds;
    public bool IsAttackInProgress => attackInProgress;
    public bool LatestSuccessBelongsToCurrentAttack => attackCounted && attackStep > 0 && attackStep == ActionCount;

    public int RegisterActionStart(ElegantActionType action)
    {
        if (action == ElegantActionType.NormalAttack || action == ElegantActionType.DiveAttack) return 0;
        return RegisterSuccess(action);
    }

    public int RegisterSuccess(ElegantActionType action)
    {
        if (action == ElegantActionType.NormalAttack) return 0;
        ActionCount++;
        LastAction = action;
        inactiveSeconds = 0f;
        return 0;
    }

    public int RegisterAttackStart(ElegantActionType action)
    {
        if (attackInProgress || (action != ElegantActionType.NormalAttack && action != ElegantActionType.DiveAttack)) return 0;
        activeAttack = action;
        attackInProgress = true;
        attackCounted = false;
        attackStep = 0;
        return 0;
    }

    public int RegisterAttackHit(ElegantActionType action)
    {
        return action == ElegantActionType.DiveAttack
            ? RegisterAttackSuccess(action, ElegantActionType.DiveAttack) : 0;
    }

    public int RegisterAttackSuccess(ElegantActionType attack, ElegantActionType success)
    {
        if (!attackInProgress || activeAttack != attack || attackCounted) return 0;
        bool valid = attack == ElegantActionType.NormalAttack
            ? success == ElegantActionType.OverheadBackKill
            : success == ElegantActionType.DiveAttack || success == ElegantActionType.DiveBounce;
        if (!valid) return 0;
        RegisterSuccess(success);
        attackCounted = true;
        attackStep = ActionCount;
        return 0;
    }

    public bool RefineDiveBounce()
    {
        if (activeAttack != ElegantActionType.DiveAttack || !attackCounted ||
            attackStep != ActionCount || LastAction != ElegantActionType.DiveAttack) return false;
        LastAction = ElegantActionType.DiveBounce;
        return true;
    }

    public int RegisterAttackEnd(ElegantActionType action, bool hitEnemy)
    {
        if (!attackInProgress || activeAttack != action) return 0;
        if (hitEnemy) RegisterAttackHit(action);
        attackInProgress = false;
        return 0;
    }

    // A successful sustained action keeps its window open; an unsuccessful attempt does not.
    public int Advance(float scaledDeltaTime, bool successfulActionHeld)
    {
        if (!IsArmed || scaledDeltaTime <= 0f) return 0;
        if (successfulActionHeld)
        {
            inactiveSeconds = 0f;
            return 0;
        }
        inactiveSeconds += scaledDeltaTime;
        if (inactiveSeconds < timeoutSeconds) return 0;
        int reward = Reward;
        ActionCount = 0;
        LastAction = null;
        inactiveSeconds = 0f;
        // An in-flight attack may start the next chain when it actually succeeds.
        attackStep = 0;
        return reward;
    }

    public int Settle()
    {
        int reward = Reward;
        ResetWithoutReward();
        return reward;
    }

    private int Reward => ActionCount >= MinimumRewardedActionCount ? ActionCount * pointsPerSuccess : 0;

    public void ResetWithoutReward()
    {
        ActionCount = 0;
        LastAction = null;
        inactiveSeconds = 0f;
        attackInProgress = false;
        attackCounted = false;
        attackStep = 0;
    }

    public static int CalculateReward(int count) =>
        count >= MinimumRewardedActionCount ? count * 10 : 0;
}
