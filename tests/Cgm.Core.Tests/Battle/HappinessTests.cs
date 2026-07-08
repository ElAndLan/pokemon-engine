using Cgm.Core.Battle;

namespace Cgm.Core.Tests.Battle;

public sealed class HappinessTests
{
    [Theory]
    // Low bracket [0,99]: biggest gains.
    [InlineData(HappinessEvent.LevelUp, 50, 5)]
    [InlineData(HappinessEvent.Walk, 50, 2)]
    [InlineData(HappinessEvent.Vitamin, 50, 5)]
    // Mid bracket [100,199].
    [InlineData(HappinessEvent.LevelUp, 150, 4)]
    [InlineData(HappinessEvent.Vitamin, 150, 3)]
    // High bracket [200,255]: slowest gains.
    [InlineData(HappinessEvent.LevelUp, 220, 3)]
    [InlineData(HappinessEvent.Walk, 220, 1)]
    [InlineData(HappinessEvent.Vitamin, 220, 2)]
    // Faint loses regardless of bracket.
    [InlineData(HappinessEvent.Faint, 50, -1)]
    [InlineData(HappinessEvent.Faint, 220, -1)]
    public void Delta_MatchesBracketTable(HappinessEvent evt, int current, int expected)
    {
        Assert.Equal(expected, Happiness.Delta(evt, current));
    }

    [Theory]
    [InlineData(99, 0)]   // last of low bracket
    [InlineData(100, 1)]  // first of mid
    [InlineData(199, 1)]  // last of mid
    [InlineData(200, 2)]  // first of high
    public void BracketBoundaries_UseCorrectDelta(int current, int expectedBracketIndex)
    {
        // LevelUp deltas are [5,4,3]; verify the boundary lands in the intended bracket.
        int[] levelUp = [5, 4, 3];
        Assert.Equal(levelUp[expectedBracketIndex], Happiness.Delta(HappinessEvent.LevelUp, current));
    }

    [Fact]
    public void Apply_ClampsToMax()
    {
        Assert.Equal(255, Happiness.Apply(254, HappinessEvent.LevelUp)); // 254 + 3 → clamp 255
        Assert.Equal(255, Happiness.Apply(255, HappinessEvent.LevelUp));
    }

    [Fact]
    public void Apply_ClampsToZero()
    {
        Assert.Equal(0, Happiness.Apply(0, HappinessEvent.Faint));
    }

    [Fact]
    public void Apply_AccumulatesTowardMax()
    {
        int h = 70;
        for (int i = 0; i < 100; i++)
            h = Happiness.Apply(h, HappinessEvent.LevelUp);
        Assert.Equal(255, h); // saturates, never overflows
    }
}
