using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class StatusEffectsTests
{
    [Theory]
    [InlineData(PersistentStatus.Burn, 80, 1, 10)]    // 80/8
    [InlineData(PersistentStatus.Poison, 80, 1, 10)]
    [InlineData(PersistentStatus.Burn, 4, 1, 1)]       // min 1
    [InlineData(PersistentStatus.Toxic, 160, 1, 10)]   // 160*1/16
    [InlineData(PersistentStatus.Toxic, 160, 3, 30)]   // 160*3/16
    [InlineData(PersistentStatus.Toxic, 160, 99, 150)] // clamped at 15/16
    [InlineData(PersistentStatus.Sleep, 80, 1, 0)]     // no residual
    [InlineData(PersistentStatus.Paralysis, 80, 1, 0)]
    public void ResidualDamage(PersistentStatus status, int maxHp, int toxic, int expected)
    {
        Assert.Equal(expected, StatusEffects.ResidualDamage(status, maxHp, toxic));
    }

    [Fact]
    public void SpeedAndAttackModifiers()
    {
        Assert.Equal(0.25, StatusEffects.SpeedMultiplier(PersistentStatus.Paralysis));
        Assert.Equal(1.0, StatusEffects.SpeedMultiplier(null));
        Assert.Equal(0.5, StatusEffects.BurnAttackMultiplier(PersistentStatus.Burn));
        Assert.Equal(1.0, StatusEffects.BurnAttackMultiplier(PersistentStatus.Poison));
    }

    [Fact]
    public void FullyParalyzed_AboutOneInFour()
    {
        var rng = new Rng(11);
        int para = Enumerable.Range(0, 10_000).Count(_ => StatusEffects.FullyParalyzed(rng));
        Assert.InRange(para, 2250, 2750);
    }

    [Fact]
    public void Thaws_AboutOneInFive()
    {
        var rng = new Rng(13);
        int thaw = Enumerable.Range(0, 10_000).Count(_ => StatusEffects.Thaws(rng));
        Assert.InRange(thaw, 1750, 2250);
    }

    [Fact]
    public void CanApplyStatus_OnlyWhenNoneActive()
    {
        Assert.True(StatusEffects.CanApplyStatus(null));
        Assert.False(StatusEffects.CanApplyStatus(PersistentStatus.Burn));
    }

    [Theory]
    [InlineData(PersistentStatus.Burn, "fire", true)]
    [InlineData(PersistentStatus.Burn, "water", false)]
    [InlineData(PersistentStatus.Freeze, "ice", true)]
    [InlineData(PersistentStatus.Poison, "poison", true)]
    [InlineData(PersistentStatus.Poison, "steel", true)]
    [InlineData(PersistentStatus.Toxic, "steel", true)]
    [InlineData(PersistentStatus.Paralysis, "electric", false)] // not immune in Gen III/IV
    public void TypeImmuneToStatus(PersistentStatus status, string type, bool expected)
    {
        Assert.Equal(expected, StatusEffects.TypeImmuneToStatus(status, [EntityId.Parse($"type:{type}")]));
    }

    [Theory]
    [InlineData(PersistentStatus.Sleep, 2.0)]
    [InlineData(PersistentStatus.Freeze, 2.0)]
    [InlineData(PersistentStatus.Burn, 1.5)]
    [InlineData(PersistentStatus.Paralysis, 1.5)]
    public void CaptureStatusBonus(PersistentStatus status, double expected)
    {
        Assert.Equal(expected, StatusEffects.CaptureStatusBonus(status));
    }

    [Fact]
    public void CaptureStatusBonus_NoneIsOne()
    {
        Assert.Equal(1.0, StatusEffects.CaptureStatusBonus(null));
    }
}
