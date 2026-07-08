using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>Statuses are data-defined conditions (EFFECT_TYPES_CATALOG §7.1): each declares its hook
/// parameters in one registry row, and <see cref="StatusEffects"/> reads from it.</summary>
public sealed class StatusConditionsTests
{
    [Fact]
    public void Burn_DeclaresResidualDamageCutAndImmunity()
    {
        StatusConditionDef burn = StatusConditions.For(PersistentStatus.Burn);
        Assert.Equal(8, burn.ResidualDenominator);          // on_turn_end 1/8
        Assert.Equal(0.5, burn.PhysicalDamageMultiplier);   // on_damage_query
        Assert.Contains("fire", burn.ImmuneTypes);
        Assert.Equal(1.5, burn.CaptureBonus);
    }

    [Fact]
    public void Toxic_RampsResidual()
    {
        StatusConditionDef toxic = StatusConditions.For(PersistentStatus.Toxic);
        Assert.True(toxic.ResidualRamps);
        Assert.Equal(16, toxic.ResidualDenominator);
    }

    [Fact]
    public void Paralysis_And_Freeze_DeclareBlockAndThawChances()
    {
        Assert.Equal(0.25, StatusConditions.For(PersistentStatus.Paralysis).FullBlockChance);
        Assert.Equal(0.25, StatusConditions.For(PersistentStatus.Paralysis).SpeedMultiplier);
        Assert.Equal(0.20, StatusConditions.For(PersistentStatus.Freeze).ThawChance);
        Assert.Contains("ice", StatusConditions.For(PersistentStatus.Freeze).ImmuneTypes);
    }

    // The reader (StatusEffects) must produce the same results as the registry data — anchors the refactor.
    [Theory]
    [InlineData(PersistentStatus.Burn, 80, 1, 10)]   // 1/8 of 80
    [InlineData(PersistentStatus.Poison, 80, 1, 10)]
    [InlineData(PersistentStatus.Toxic, 160, 3, 30)] // 3/16 of 160
    [InlineData(PersistentStatus.Sleep, 80, 1, 0)]   // no residual
    public void ResidualDamage_MatchesRegistry(PersistentStatus status, int maxHp, int counter, int expected)
    {
        Assert.Equal(expected, StatusEffects.ResidualDamage(status, maxHp, counter));
    }

    [Fact]
    public void EveryStatus_HasARegistryEntry()
    {
        foreach (PersistentStatus s in Enum.GetValues<PersistentStatus>())
            Assert.Equal(s, StatusConditions.For(s).Status); // no missing rows → no KeyNotFoundException
    }
}
