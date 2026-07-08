using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class EffectMathTests
{
    // --- multi-hit ---------------------------------------------------------------

    [Theory]
    [InlineData(0, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(5, 3)]
    [InlineData(6, 4)]
    [InlineData(7, 5)]
    public void HitCount_Canonical2To5_UsesGenWeighting(int roll, int expected)
    {
        Assert.Equal(expected, EffectMath.HitCount(new FakeRng(ints: [roll]), 2, 5));
    }

    [Fact]
    public void HitCount_FixedN_DrawsNothing()
    {
        Assert.Equal(3, EffectMath.HitCount(new FakeRng(), 3, 3)); // empty rng → must not draw
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 3)]
    public void HitCount_OtherRange_IsUniform(int roll, int expected)
    {
        Assert.Equal(expected, EffectMath.HitCount(new FakeRng(ints: [roll]), 2, 3));
    }

    [Fact]
    public void HitCount_MinGreaterThanMax_Throws()
    {
        Assert.Throws<ArgumentException>(() => EffectMath.HitCount(new FakeRng(), 5, 2));
    }

    [Fact]
    public void HitCount_Distribution_MatchesGenWeights()
    {
        var rng = new Rng(12345);
        var counts = new int[6];
        const int n = 40000;
        for (int i = 0; i < n; i++)
            counts[EffectMath.HitCount(rng)]++;

        // Expected: 2 and 3 at 3/8 (.375), 4 and 5 at 1/8 (.125).
        Assert.InRange(counts[2] / (double)n, 0.355, 0.395);
        Assert.InRange(counts[3] / (double)n, 0.355, 0.395);
        Assert.InRange(counts[4] / (double)n, 0.110, 0.140);
        Assert.InRange(counts[5] / (double)n, 0.110, 0.140);
    }

    // --- drain / recoil / crash --------------------------------------------------

    [Theory]
    [InlineData(100, 1, 2, 50)]
    [InlineData(101, 1, 2, 50)]   // floor
    [InlineData(1, 1, 2, 1)]      // rounds to 0 → clamped to min 1
    [InlineData(0, 1, 2, 0)]      // no damage → no heal
    public void DrainHeal_HalfByDefault(int dmg, int num, int den, int expected)
    {
        Assert.Equal(expected, EffectMath.DrainHeal(dmg, num, den));
    }

    [Theory]
    [InlineData(120, 1, 3, 40)]
    [InlineData(120, 1, 4, 30)]
    [InlineData(2, 1, 3, 1)]   // floor(0) → min 1
    [InlineData(0, 1, 3, 0)]   // no damage → no recoil
    public void RecoilDamage_Fractions(int dmg, int num, int den, int expected)
    {
        Assert.Equal(expected, EffectMath.RecoilDamage(dmg, num, den));
    }

    [Fact]
    public void CrashDamage_HalfMaxHp_Floored()
    {
        Assert.Equal(50, EffectMath.CrashDamage(100));
        Assert.Equal(50, EffectMath.CrashDamage(101)); // floor
    }

    // --- ohko --------------------------------------------------------------------

    [Theory]
    [InlineData(50, 50, 30)]  // equal levels → 30%
    [InlineData(60, 50, 40)]  // +10 levels → 40%
    [InlineData(50, 60, 0)]   // target out-levels user → auto-fail
    [InlineData(100, 1, 129)] // huge gap
    public void OhkoAccuracy_LevelScaled(int userLevel, int targetLevel, int expected)
    {
        Assert.Equal(expected, EffectMath.OhkoAccuracy(userLevel, targetLevel));
    }

    // --- heal fraction -----------------------------------------------------------

    [Theory]
    [InlineData(100, 1, 2, 50)]
    [InlineData(101, 1, 2, 50)]
    [InlineData(1, 1, 2, 1)]   // floor(0) → min 1
    public void HealAmount_FractionOfMax(int maxHp, int num, int den, int expected)
    {
        Assert.Equal(expected, EffectMath.HealAmount(maxHp, num, den));
    }
}
