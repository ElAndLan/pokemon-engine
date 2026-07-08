using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class VolatileEffectsTests
{
    [Fact]
    public void ConfusionDuration_IsOneToFour()
    {
        var rng = new Rng(1);
        var durations = Enumerable.Range(0, 2000).Select(_ => VolatileEffects.ConfusionDuration(rng)).ToList();
        Assert.All(durations, d => Assert.InRange(d, 1, 4));
        Assert.Contains(1, durations);
        Assert.Contains(4, durations);
    }

    [Fact]
    public void HitsSelfInConfusion_AboutHalf()
    {
        var rng = new Rng(2);
        int self = Enumerable.Range(0, 10_000).Count(_ => VolatileEffects.HitsSelfInConfusion(rng));
        Assert.InRange(self, 4700, 5300);
    }

    [Fact]
    public void ConfusionSelfDamage_MatchesFormula_AndScalesWithAttack()
    {
        int expected = DamageCalc.Compute(50, 40, 100, 100, 1.0, 1.0, false, 100, false);
        Assert.Equal(expected, VolatileEffects.ConfusionSelfDamage(50, 100, 100));
        Assert.True(VolatileEffects.ConfusionSelfDamage(50, 200, 100) > VolatileEffects.ConfusionSelfDamage(50, 100, 100));
    }

    [Theory]
    [InlineData(0, 50, false)]   // no flinch chance → never
    [InlineData(100, 50, true)]  // 100% → always (50 < 100)
    [InlineData(30, 29, true)]
    [InlineData(30, 30, false)]
    public void Flinches_RollsAgainstChance(int chance, int roll, bool expected)
    {
        Assert.Equal(expected, VolatileEffects.Flinches(chance, new FakeRng(ints: [roll])));
    }

    [Fact]
    public void Flinches_StatisticalRate()
    {
        var rng = new Rng(6);
        int flinch = Enumerable.Range(0, 10_000).Count(_ => VolatileEffects.Flinches(30, rng));
        Assert.InRange(flinch, 2700, 3300); // ~30%
    }
}
