using Cgm.Core.Battle;

namespace Cgm.Core.Tests.Battle;

public sealed class DamageCalcTargetsTests
{
    [Theory]
    [InlineData(101, 1, 101)]
    [InlineData(101, 2, 75)]
    [InlineData(101, 3, 75)]
    [InlineData(1, 2, 0)]
    public void ApplyTargetsModifier_UsesSnapshottedLiveTargetCountAndFloors(int damage, int targets, int expected) =>
        Assert.Equal(expected, DamageCalc.ApplyTargetsModifier(damage, targets));
}
