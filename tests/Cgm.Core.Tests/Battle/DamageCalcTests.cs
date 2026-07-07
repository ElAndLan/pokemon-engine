using Cgm.Core.Battle;

namespace Cgm.Core.Tests.Battle;

public sealed class DamageCalcTests
{
    // Reference case: L50, 100 power, 100 atk vs 100 def.
    // i1 = 2*50/5+2 = 22; i2 = 22*100*100/100 = 2200; base = 2200/50 + 2 = 46.
    [Fact]
    public void BaseDamage_KnownValue()
    {
        Assert.Equal(46, DamageCalc.BaseDamage(50, 100, 100, 100));
    }

    private static int Roll100(double eff = 1, double stab = 1, bool crit = false, bool burn = false) =>
        DamageCalc.Compute(50, 100, 100, 100, eff, stab, crit, roll: 100, burn);

    [Fact]
    public void MaxRoll_NoModifiers_EqualsBase()
    {
        Assert.Equal(46, Roll100());
    }

    [Fact]
    public void MinRoll_FloorsCorrectly()
    {
        // floor(46 * 85/100) = floor(39.1) = 39.
        Assert.Equal(39, DamageCalc.Compute(50, 100, 100, 100, 1, 1, false, roll: 85, burn: false));
    }

    [Fact]
    public void Critical_Doubles()
    {
        Assert.Equal(92, Roll100(crit: true));
    }

    [Fact]
    public void Stab_MultipliesByOneAndHalf()
    {
        Assert.Equal(69, Roll100(stab: 1.5)); // floor(46*1.5)
    }

    [Theory]
    [InlineData(2.0, 92)]
    [InlineData(0.5, 23)]
    [InlineData(4.0, 184)]
    [InlineData(0.25, 11)] // floor(46*0.25)=11
    public void Effectiveness_Scales(double eff, int expected)
    {
        Assert.Equal(expected, Roll100(eff: eff));
    }

    [Fact]
    public void Immunity_IsZero_NotClampedToOne()
    {
        Assert.Equal(0, Roll100(eff: 0.0));
    }

    [Fact]
    public void Burn_Halves()
    {
        Assert.Equal(23, Roll100(burn: true)); // floor(46/2)
    }

    [Fact]
    public void ModifiersCompose_InOrder()
    {
        // base 46 → crit ×2 = 92 → roll100 = 92 → stab 1.5 → floor(138) = 138 → eff ×2 = 276.
        Assert.Equal(276, DamageCalc.Compute(50, 100, 100, 100, 2.0, 1.5, crit: true, roll: 100, burn: false));
    }

    [Fact]
    public void NonImmuneDamage_IsAtLeastOne()
    {
        // Tiny base (2) with a resisted quarter multiplier floors to 0 → clamped to 1.
        int dmg = DamageCalc.Compute(1, 1, 1, 255, effectiveness: 0.25, stab: 1, crit: false, roll: 85, burn: false);
        Assert.Equal(1, dmg);
    }

    [Fact]
    public void HigherDefense_ReducesDamage()
    {
        int lowDef = DamageCalc.BaseDamage(50, 100, 100, 50);
        int highDef = DamageCalc.BaseDamage(50, 100, 100, 200);
        Assert.True(lowDef > highDef);
    }
}
