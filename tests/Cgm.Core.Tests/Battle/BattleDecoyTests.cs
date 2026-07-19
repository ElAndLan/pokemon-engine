using Cgm.Core.Battle;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleDecoyTests
{
    private static readonly Fraction Quarter = new(1, 4);

    [Fact]
    public void CreateBuildsADecoyAtTheExactCostWhenHpAllows()
    {
        BattleDecoyCreation result = BattleDecoy.Create(currentHp: 100, maxHp: 100, Quarter, decoyPresent: false);

        Assert.True(result.Succeeded);
        Assert.Equal(25, result.Cost);
        Assert.Equal(new BattleDecoyState(25, 25), result.Decoy);
    }

    [Fact]
    public void CreateFloorsTheCostAndRequiresStrictlyMoreHpThanTheCost()
    {
        // maxHp 26 -> floor(26/4) = 6. currentHp exactly 6 is not enough (owner would faint at cost>=hp).
        Assert.Equal(BattleDecoyFailure.InsufficientHp,
            BattleDecoy.Create(currentHp: 6, maxHp: 26, Quarter, decoyPresent: false).Failure);
        // 7 HP with a cost of 6 succeeds.
        BattleDecoyCreation ok = BattleDecoy.Create(currentHp: 7, maxHp: 26, Quarter, decoyPresent: false);
        Assert.True(ok.Succeeded);
        Assert.Equal(6, ok.Cost);
    }

    [Fact]
    public void CreateFailsWhenADecoyIsAlreadyPresentWithoutComputingCost()
    {
        BattleDecoyCreation result = BattleDecoy.Create(currentHp: 100, maxHp: 100, Quarter, decoyPresent: true);

        Assert.Equal(BattleDecoyFailure.AlreadyPresent, result.Failure);
        Assert.Null(result.Decoy);
        Assert.Equal(0, result.Cost);
    }

    [Fact]
    public void CreateClampsTinyCostToAtLeastOne()
    {
        // maxHp 3 -> floor(3/4) = 0 -> clamped to 1.
        BattleDecoyCreation result = BattleDecoy.Create(currentHp: 3, maxHp: 3, Quarter, decoyPresent: false);
        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Cost);
    }

    [Fact]
    public void InterceptAbsorbsPartialDamageAndKeepsTheDecoy()
    {
        BattleDecoyInterception hit = BattleDecoy.Intercept(new BattleDecoyState(25, 25), incomingDamage: 10);

        Assert.False(hit.Broke);
        Assert.Equal(10, hit.Absorbed);
        Assert.Equal(new BattleDecoyState(15, 25), hit.Remaining);
    }

    [Theory]
    [InlineData(25)] // exact
    [InlineData(999)] // overkill — excess is discarded, no overflow to the owner
    public void InterceptBreaksTheDecoyWithoutOverflow(int incomingDamage)
    {
        BattleDecoyInterception hit = BattleDecoy.Intercept(new BattleDecoyState(25, 25), incomingDamage);

        Assert.True(hit.Broke);
        Assert.Null(hit.Remaining);
        Assert.Equal(25, hit.Absorbed); // never more than the decoy had
    }

    [Fact]
    public void InvalidInputsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => BattleDecoy.Create(100, 0, Quarter, false));
        Assert.Throws<ArgumentException>(() => BattleDecoy.Create(100, 100, new Fraction(0, 4), false));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BattleDecoy.Intercept(new BattleDecoyState(10, 10), -1));
    }
}
