using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class StatCalcTests
{
    private static Stats All(int v) => new(v, v, v, v, v, v);

    [Fact]
    public void Hp_KnownValue_Base100_L100_MaxIvEv()
    {
        // (2*100 + 31 + 252/4)*100/100 + 100 + 10 = 294 + 110 = 404.
        Stats s = StatCalc.Compute(All(100), All(31), All(252), "serious", 100);
        Assert.Equal(404, s.Hp);
    }

    [Fact]
    public void OtherStat_Neutral_Base100_L100_MaxIvEv()
    {
        // ((2*100 + 31 + 63)*100/100 + 5) * 1.0 = 299.
        Stats s = StatCalc.Compute(All(100), All(31), All(252), "serious", 100);
        Assert.Equal(299, s.Atk);
    }

    [Fact]
    public void Nature_BoostsAndHinders_ByTenPercent_Floored()
    {
        // Adamant: +Atk (299*1.1 = 328.9 → 328), -Spa (299*0.9 = 269.1 → 269). Others neutral.
        Stats s = StatCalc.Compute(All(100), All(31), All(252), "adamant", 100);
        Assert.Equal(328, s.Atk);
        Assert.Equal(269, s.Spa);
        Assert.Equal(299, s.Def); // untouched by nature
        Assert.Equal(404, s.Hp);  // HP never affected
    }

    [Fact]
    public void MinIvEv_Level1()
    {
        // HP L1 base45: (2*45 + 0 + 0)*1/100 + 1 + 10 = 0 + 11 = 11.
        Stats s = StatCalc.Compute(All(45), All(0), All(0), "serious", 1);
        Assert.Equal(11, s.Hp);
        // Other L1 base45: ((90)*1/100 + 5)*1 = (0 + 5) = 5.
        Assert.Equal(5, s.Atk);
    }

    [Fact]
    public void NeutralNatures_HaveNoEffect()
    {
        Stats hardy = StatCalc.Compute(All(80), All(20), All(100), "hardy", 50);
        Stats serious = StatCalc.Compute(All(80), All(20), All(100), "serious", 50);
        Assert.Equal(serious, hardy); // both neutral → identical
    }

    [Theory]
    [InlineData("adamant", StatKind.Atk, 1.1)]
    [InlineData("adamant", StatKind.Spa, 0.9)]
    [InlineData("adamant", StatKind.Def, 1.0)]
    [InlineData("adamant", StatKind.Hp, 1.0)]   // HP never
    [InlineData("hardy", StatKind.Atk, 1.0)]     // neutral
    [InlineData("unknown", StatKind.Atk, 1.0)]   // unknown → neutral
    public void NatureMultiplier(string nature, StatKind stat, double expected)
    {
        Assert.Equal(expected, Natures.Multiplier(nature, stat));
    }

    [Fact]
    public void Natures_HasAll25_AndValidity()
    {
        Assert.Equal(25, Natures.All.Count);
        Assert.True(Natures.IsValid("jolly"));
        Assert.False(Natures.IsValid("turbo"));
    }
}
