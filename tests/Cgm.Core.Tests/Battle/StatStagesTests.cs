using Cgm.Core.Battle;

namespace Cgm.Core.Tests.Battle;

public sealed class StatStagesTests
{
    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 1.5)]
    [InlineData(2, 2.0)]
    [InlineData(3, 2.5)]
    [InlineData(4, 3.0)]
    [InlineData(5, 3.5)]
    [InlineData(6, 4.0)]
    [InlineData(-1, 2.0 / 3)]
    [InlineData(-2, 0.5)]
    [InlineData(-3, 0.4)]
    [InlineData(-4, 2.0 / 6)]
    [InlineData(-5, 2.0 / 7)]
    [InlineData(-6, 0.25)]
    public void Multiplier_OffensiveDefensiveTable(int stage, double expected)
    {
        Assert.Equal(expected, StatStages.Multiplier(stage), precision: 10);
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(1, 4.0 / 3)]
    [InlineData(3, 2.0)]
    [InlineData(6, 3.0)]
    [InlineData(-1, 3.0 / 4)]
    [InlineData(-3, 0.5)]
    [InlineData(-6, 1.0 / 3)]
    public void AccEvaMultiplier_Table(int stage, double expected)
    {
        Assert.Equal(expected, StatStages.AccEvaMultiplier(stage), precision: 10);
    }

    [Fact]
    public void Stages_ClampBeyondRange()
    {
        Assert.Equal(4.0, StatStages.Multiplier(99));   // clamps to +6
        Assert.Equal(0.25, StatStages.Multiplier(-99)); // clamps to −6
    }

    [Fact]
    public void Apply_ClampsToRange()
    {
        Assert.Equal(6, StatStages.Apply(5, 3));   // +5 +3 → clamp +6
        Assert.Equal(-6, StatStages.Apply(-5, -3)); // clamp −6
        Assert.Equal(2, StatStages.Apply(0, 2));
    }
}
