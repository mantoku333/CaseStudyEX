using NUnit.Framework;

public sealed class ElegantActionChainTests
{
    [TestCase(ElegantActionType.Glide)]
    [TestCase(ElegantActionType.Dodge)]
    [TestCase(ElegantActionType.DodgeProjectile)]
    [TestCase(ElegantActionType.RecoilMove)]
    [TestCase(ElegantActionType.RecoilJump)]
    public void AnySuccessfulMovement_CanStartAtLevelOne(ElegantActionType action)
    {
        var chain = new ElegantActionChain();
        chain.RegisterSuccess(action);
        Assert.That(chain.ActionCount, Is.EqualTo(1));
        // Level one is flourish only: a single glide must not feed the gauge.
        Assert.That(chain.Settle(), Is.Zero);
        Assert.That(chain.IsArmed, Is.False);
    }

    [Test]
    public void LevelOnePaysNothing_AndLevelTwoPaysTheWholeChain()
    {
        var chain = new ElegantActionChain();
        chain.RegisterSuccess(ElegantActionType.Glide);
        Assert.That(chain.Advance(1f, false), Is.Zero);
        chain.RegisterSuccess(ElegantActionType.Glide);
        Assert.That(chain.Advance(1f, false), Is.Zero);
        chain.RegisterSuccess(ElegantActionType.Glide);
        chain.RegisterSuccess(ElegantActionType.Dodge);
        Assert.That(chain.Advance(1f, false), Is.EqualTo(20));
        Assert.That(ElegantActionChain.CalculateReward(1), Is.Zero);
        Assert.That(ElegantActionChain.CalculateReward(2), Is.EqualTo(20));
    }

    [Test]
    public void RequestedSequence_AdvancesOneLevelPerSuccess()
    {
        var chain = new ElegantActionChain();
        chain.RegisterSuccess(ElegantActionType.Glide);
        Assert.That(chain.ActionCount, Is.EqualTo(1));
        chain.RegisterAttackStart(ElegantActionType.DiveAttack);
        Assert.That(chain.ActionCount, Is.EqualTo(1));
        chain.RegisterAttackHit(ElegantActionType.DiveAttack);
        Assert.That(chain.ActionCount, Is.EqualTo(2));
        chain.RegisterAttackEnd(ElegantActionType.DiveAttack, true);
        chain.RegisterSuccess(ElegantActionType.Dodge);
        Assert.That(chain.ActionCount, Is.EqualTo(3));
        chain.RegisterSuccess(ElegantActionType.RecoilMove);
        Assert.That(chain.ActionCount, Is.EqualTo(4));
        Assert.That(chain.Advance(1f, false), Is.EqualTo(40));
        Assert.That(chain.ActionCount, Is.Zero);
    }

    [Test]
    public void OrdinaryNormalHit_DoesNotStartOrAdvanceChain()
    {
        var chain = new ElegantActionChain();
        chain.RegisterAttackStart(ElegantActionType.NormalAttack);
        chain.RegisterAttackHit(ElegantActionType.NormalAttack);
        chain.RegisterAttackEnd(ElegantActionType.NormalAttack, true);
        chain.RegisterSuccess(ElegantActionType.NormalAttack);
        Assert.That(chain.ActionCount, Is.Zero);
        chain.RegisterSuccess(ElegantActionType.Glide);
        chain.RegisterAttackStart(ElegantActionType.NormalAttack);
        chain.RegisterAttackEnd(ElegantActionType.NormalAttack, true);
        Assert.That(chain.ActionCount, Is.EqualTo(1));
    }

    [Test]
    public void OverheadBackKill_CountsOncePerNormalAttack()
    {
        var chain = new ElegantActionChain();
        chain.RegisterAttackStart(ElegantActionType.NormalAttack);
        chain.RegisterAttackSuccess(ElegantActionType.NormalAttack, ElegantActionType.OverheadBackKill);
        chain.RegisterAttackSuccess(ElegantActionType.NormalAttack, ElegantActionType.OverheadBackKill);
        chain.RegisterAttackHit(ElegantActionType.NormalAttack);
        chain.RegisterAttackEnd(ElegantActionType.NormalAttack, true);
        Assert.That(chain.ActionCount, Is.EqualTo(1));
        Assert.That(chain.LastAction, Is.EqualTo(ElegantActionType.OverheadBackKill));
    }

    [Test]
    public void DiveHitAndBounce_RefineOneSuccessWithoutDoubleCounting()
    {
        var chain = new ElegantActionChain();
        chain.RegisterAttackStart(ElegantActionType.DiveAttack);
        chain.RegisterAttackHit(ElegantActionType.DiveAttack);
        chain.RegisterAttackHit(ElegantActionType.DiveAttack);
        chain.RegisterAttackEnd(ElegantActionType.DiveAttack, true);
        Assert.That(chain.RefineDiveBounce(), Is.True);
        Assert.That(chain.RefineDiveBounce(), Is.False);
        Assert.That(chain.ActionCount, Is.EqualTo(1));
        Assert.That(chain.LastAction, Is.EqualTo(ElegantActionType.DiveBounce));
    }

    [Test]
    public void MissesAndPendingAttempts_DoNotRefreshTheWindow()
    {
        var chain = new ElegantActionChain();
        chain.RegisterSuccess(ElegantActionType.Glide);
        chain.RegisterSuccess(ElegantActionType.Dodge);
        chain.Advance(0.7f, false);
        chain.RegisterAttackStart(ElegantActionType.DiveAttack);
        chain.RegisterAttackEnd(ElegantActionType.DiveAttack, false);
        chain.RegisterAttackStart(ElegantActionType.NormalAttack);
        Assert.That(chain.Advance(0.3f, false), Is.EqualTo(20));
    }

    [Test]
    public void AttackStillInFlight_CanSucceedAfterPreviousChainExpires()
    {
        var chain = new ElegantActionChain();
        chain.RegisterSuccess(ElegantActionType.Glide);
        chain.RegisterSuccess(ElegantActionType.Dodge);
        chain.RegisterAttackStart(ElegantActionType.DiveAttack);
        Assert.That(chain.Advance(1f, false), Is.EqualTo(20));
        chain.RegisterAttackHit(ElegantActionType.DiveAttack);
        Assert.That(chain.ActionCount, Is.EqualTo(1));
    }

    [Test]
    public void DistinctExecutionsOfSameAction_CanContinueBeyondLevelTwo()
    {
        var chain = new ElegantActionChain();
        for (int i = 0; i < 8; i++) chain.RegisterSuccess(ElegantActionType.Dodge);
        Assert.That(chain.ActionCount, Is.EqualTo(8));
        Assert.That(chain.Settle(), Is.EqualTo(80));
    }

    [Test]
    public void SuccessfulHeldAction_PreservesWindowButPauseDoesNotAdvanceIt()
    {
        var chain = new ElegantActionChain();
        chain.RegisterSuccess(ElegantActionType.Glide);
        chain.RegisterSuccess(ElegantActionType.Dodge);
        chain.Advance(0.7f, false);
        Assert.That(chain.Advance(5f, true), Is.Zero);
        chain.Advance(0.7f, false);
        Assert.That(chain.Advance(0f, false), Is.Zero);
        Assert.That(chain.Advance(0.3f, false), Is.EqualTo(20));
        Assert.That(chain.Advance(1f, false), Is.Zero);
    }

    [Test]
    public void RewardRateAndTimeout_AreConfigurable()
    {
        var chain = new ElegantActionChain(2f, 7);
        chain.RegisterSuccess(ElegantActionType.Glide);
        chain.RegisterSuccess(ElegantActionType.Dodge);
        Assert.That(chain.Advance(1.9f, false), Is.Zero);
        Assert.That(chain.Advance(0.1f, false), Is.EqualTo(14));
    }

    [Test]
    public void DeathOrReset_SettlesOnlyOnceAndDropsPendingAttack()
    {
        var chain = new ElegantActionChain();
        chain.RegisterSuccess(ElegantActionType.Glide);
        chain.RegisterSuccess(ElegantActionType.Dodge);
        chain.RegisterAttackStart(ElegantActionType.DiveAttack);
        Assert.That(chain.Settle(), Is.EqualTo(20));
        Assert.That(chain.IsAttackInProgress, Is.False);
        Assert.That(chain.Settle(), Is.Zero);
    }
}
