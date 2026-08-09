using NUnit.Framework;

public sealed class ElegantActionChainTests
{
    [Test]
    public void NonAttackAction_WhenUnarmed_IsIgnored()
    {
        var chain = new ElegantActionChain();

        chain.RegisterActionStart(ElegantActionType.Glide);
        chain.RegisterActionStart(ElegantActionType.Dodge);
        chain.RegisterActionStart(ElegantActionType.RecoilMove);

        Assert.That(chain.IsArmed, Is.False);
        Assert.That(chain.ActionCount, Is.Zero);
    }

    [Test]
    public void SuccessfulAttack_ArmsOnFirstHit_AndRemainsPendingUntilEnd()
    {
        var chain = new ElegantActionChain();

        chain.RegisterAttackStart(ElegantActionType.NormalAttack);

        Assert.That(chain.IsArmed, Is.False);
        Assert.That(chain.IsAttackInProgress, Is.True);
        Assert.That(chain.Advance(5f, false), Is.Zero);

        chain.RegisterAttackHit(ElegantActionType.NormalAttack);

        Assert.That(chain.IsArmed, Is.True);
        Assert.That(chain.ActionCount, Is.EqualTo(1));
        Assert.That(chain.Advance(5f, false), Is.Zero);

        chain.RegisterAttackEnd(ElegantActionType.NormalAttack, true);

        Assert.That(chain.IsAttackInProgress, Is.False);
        Assert.That(chain.Advance(1f, false), Is.Zero);
        Assert.That(chain.IsArmed, Is.False);
    }

    [Test]
    public void AttackHit_WhenReportedMoreThanOnce_CountsOnlyOnce()
    {
        var chain = new ElegantActionChain();

        chain.RegisterAttackStart(ElegantActionType.DiveAttack);
        chain.RegisterAttackHit(ElegantActionType.DiveAttack);
        chain.RegisterAttackHit(ElegantActionType.DiveAttack);
        chain.RegisterAttackEnd(ElegantActionType.DiveAttack, true);

        Assert.That(chain.ActionCount, Is.EqualTo(1));
    }

    [Test]
    public void NonAttackStartedAfterPendingAttackHit_IsNotDropped()
    {
        var chain = new ElegantActionChain();

        chain.RegisterAttackStart(ElegantActionType.NormalAttack);
        chain.RegisterAttackHit(ElegantActionType.NormalAttack);
        chain.RegisterActionStart(ElegantActionType.Dodge);

        Assert.That(chain.IsAttackInProgress, Is.True);
        Assert.That(chain.ActionCount, Is.EqualTo(2));
        Assert.That(chain.LastAction, Is.EqualTo(ElegantActionType.Dodge));
    }

    [Test]
    public void NonAttackStartedBeforePendingAttackHit_IsDeferredForAnArmedChain()
    {
        var chain = new ElegantActionChain();
        CompleteSuccessfulAttack(chain, ElegantActionType.NormalAttack);

        chain.RegisterAttackStart(ElegantActionType.DiveAttack);
        chain.RegisterActionStart(ElegantActionType.Dodge);

        Assert.That(chain.ActionCount, Is.EqualTo(1));
        chain.RegisterAttackHit(ElegantActionType.DiveAttack);

        Assert.That(chain.ActionCount, Is.EqualTo(3));
        Assert.That(chain.LastAction, Is.EqualTo(ElegantActionType.Dodge));
    }

    [Test]
    public void NonAttackDuringFirstPendingAttack_RemainsIgnoredBeforeArmingHit()
    {
        var chain = new ElegantActionChain();

        chain.RegisterAttackStart(ElegantActionType.NormalAttack);
        chain.RegisterActionStart(ElegantActionType.Dodge);
        chain.RegisterAttackHit(ElegantActionType.NormalAttack);

        Assert.That(chain.ActionCount, Is.EqualTo(1));
        Assert.That(chain.LastAction, Is.EqualTo(ElegantActionType.NormalAttack));
    }

    [Test]
    public void DeferredNonAttack_IsDiscardedWhenPendingAttackMisses()
    {
        ElegantActionChain chain = CreateTwoActionChain();

        chain.RegisterAttackStart(ElegantActionType.DiveAttack);
        chain.RegisterActionStart(ElegantActionType.Dodge);
        int reward = chain.RegisterAttackEnd(ElegantActionType.DiveAttack, false);

        Assert.That(reward, Is.EqualTo(10));
        Assert.That(chain.IsArmed, Is.False);
        Assert.That(chain.ActionCount, Is.Zero);
    }

    [Test]
    public void ReusedDeferredNonAttack_SettlesAfterPendingAttackHits()
    {
        ElegantActionChain chain = CreateTwoActionChain();

        chain.RegisterAttackStart(ElegantActionType.DiveAttack);
        chain.RegisterActionStart(ElegantActionType.Glide);
        int reward = chain.RegisterAttackHit(ElegantActionType.DiveAttack);

        Assert.That(reward, Is.EqualTo(20));
        Assert.That(chain.IsArmed, Is.False);
        Assert.That(chain.ActionCount, Is.Zero);
        Assert.That(chain.IsAttackInProgress, Is.True);
    }

    [Test]
    public void Timeout_AtExactlyOneScaledSecond_SettlesOnce()
    {
        ElegantActionChain chain = CreateTwoActionChain();

        Assert.That(chain.Advance(0.5f, false), Is.Zero);
        Assert.That(chain.Advance(0.5f, false), Is.EqualTo(10));
        Assert.That(chain.IsArmed, Is.False);
        Assert.That(chain.Advance(1f, false), Is.Zero);
    }

    [Test]
    public void HeldAction_ResetsInactivityAndProvidesFullWindowAfterRelease()
    {
        ElegantActionChain chain = CreateTwoActionChain();

        Assert.That(chain.Advance(0.75f, false), Is.Zero);
        Assert.That(chain.Advance(0.5f, true), Is.Zero);
        Assert.That(chain.Advance(0.75f, false), Is.Zero);
        Assert.That(chain.Advance(0.25f, false), Is.EqualTo(10));
    }

    [Test]
    public void ZeroScaledDeltaTime_DoesNotAdvanceTimeout()
    {
        ElegantActionChain chain = CreateTwoActionChain();

        Assert.That(chain.Advance(0.75f, false), Is.Zero);
        Assert.That(chain.Advance(0f, false), Is.Zero);
        Assert.That(chain.Advance(0.25f, false), Is.EqualTo(10));
    }

    [Test]
    public void ReusedNonAttack_SettlesAndLeavesChainDisarmed()
    {
        var chain = new ElegantActionChain();
        CompleteSuccessfulAttack(chain, ElegantActionType.NormalAttack);
        chain.RegisterActionStart(ElegantActionType.Glide);
        chain.RegisterActionStart(ElegantActionType.Dodge);

        int reward = chain.RegisterActionStart(ElegantActionType.Glide);

        Assert.That(reward, Is.EqualTo(20));
        Assert.That(chain.IsArmed, Is.False);
        Assert.That(chain.ActionCount, Is.Zero);
    }

    [Test]
    public void ReusedSuccessfulAttack_SettlesThenRearmsAtStepOne()
    {
        var chain = new ElegantActionChain();
        CompleteSuccessfulAttack(chain, ElegantActionType.NormalAttack);
        chain.RegisterActionStart(ElegantActionType.Dodge);

        int reward = chain.RegisterAttackStart(ElegantActionType.NormalAttack);

        Assert.That(reward, Is.EqualTo(10));
        Assert.That(chain.IsArmed, Is.False);
        Assert.That(chain.IsAttackInProgress, Is.True);

        chain.RegisterAttackHit(ElegantActionType.NormalAttack);
        chain.RegisterAttackEnd(ElegantActionType.NormalAttack, true);

        Assert.That(chain.IsArmed, Is.True);
        Assert.That(chain.ActionCount, Is.EqualTo(1));
        Assert.That(chain.LastAction, Is.EqualTo(ElegantActionType.NormalAttack));
    }

    [Test]
    public void MissedAttack_SettlesExistingChainAndDisarms()
    {
        ElegantActionChain chain = CreateTwoActionChain();

        chain.RegisterAttackStart(ElegantActionType.DiveAttack);
        int reward = chain.RegisterAttackEnd(ElegantActionType.DiveAttack, false);

        Assert.That(reward, Is.EqualTo(10));
        Assert.That(chain.IsArmed, Is.False);
        Assert.That(chain.IsAttackInProgress, Is.False);
    }

    [Test]
    public void AlternatingAttackAndGlide_CannotGrowOneFarmedChain()
    {
        var chain = new ElegantActionChain();
        CompleteSuccessfulAttack(chain, ElegantActionType.NormalAttack);
        chain.RegisterActionStart(ElegantActionType.Glide);

        int firstReward = chain.RegisterAttackStart(ElegantActionType.NormalAttack);
        chain.RegisterAttackHit(ElegantActionType.NormalAttack);
        chain.RegisterAttackEnd(ElegantActionType.NormalAttack, true);
        chain.RegisterActionStart(ElegantActionType.Glide);

        int secondReward = chain.RegisterAttackStart(ElegantActionType.NormalAttack);

        Assert.That(firstReward, Is.EqualTo(10));
        Assert.That(secondReward, Is.EqualTo(10));
        Assert.That(chain.IsArmed, Is.False);
        Assert.That(chain.ActionCount, Is.Zero);
        Assert.That(chain.IsAttackInProgress, Is.True);
    }

    [Test]
    public void AllFiveEligibleActionTypes_AreMaximumBeforeReuseSettles()
    {
        var chain = new ElegantActionChain();
        CompleteSuccessfulAttack(chain, ElegantActionType.NormalAttack);
        chain.RegisterActionStart(ElegantActionType.Glide);
        chain.RegisterActionStart(ElegantActionType.Dodge);
        CompleteSuccessfulAttack(chain, ElegantActionType.DiveAttack);
        chain.RegisterActionStart(ElegantActionType.RecoilMove);

        Assert.That(chain.ActionCount, Is.EqualTo(5));
        Assert.That(chain.RegisterActionStart(ElegantActionType.Glide), Is.EqualTo(50));
        Assert.That(chain.IsArmed, Is.False);
        Assert.That(chain.ActionCount, Is.Zero);
    }

    [TestCase(0, 0)]
    [TestCase(1, 0)]
    [TestCase(2, 10)]
    [TestCase(3, 20)]
    [TestCase(4, 40)]
    [TestCase(7, 70)]
    public void CalculateReward_UsesFinalChainLength(int actionCount, int expectedReward)
    {
        Assert.That(ElegantActionChain.CalculateReward(actionCount), Is.EqualTo(expectedReward));
    }

    private static ElegantActionChain CreateTwoActionChain()
    {
        var chain = new ElegantActionChain();
        CompleteSuccessfulAttack(chain, ElegantActionType.NormalAttack);
        chain.RegisterActionStart(ElegantActionType.Glide);
        return chain;
    }

    private static void CompleteSuccessfulAttack(
        ElegantActionChain chain,
        ElegantActionType action)
    {
        chain.RegisterAttackStart(action);
        chain.RegisterAttackHit(action);
        chain.RegisterAttackEnd(action, true);
    }
}
