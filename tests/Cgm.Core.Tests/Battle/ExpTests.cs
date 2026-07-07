using Cgm.Core.Battle;

namespace Cgm.Core.Tests.Battle;

public sealed class ExpCurveTests
{
    [Theory]
    [InlineData("fast")]
    [InlineData("medium-fast")]
    [InlineData("medium-slow")]
    [InlineData("slow")]
    [InlineData("erratic")]
    [InlineData("fluctuating")]
    public void Level1_IsZero_ForEveryCurve(string curve)
    {
        Assert.Equal(0, ExpCurve.TotalExp(curve, 1));
    }

    [Theory]
    [InlineData("fast", 800_000)]
    [InlineData("medium-fast", 1_000_000)]
    [InlineData("medium-slow", 1_059_860)]
    [InlineData("slow", 1_250_000)]
    [InlineData("erratic", 600_000)]
    [InlineData("fluctuating", 1_640_000)]
    public void Level100_MatchesKnownTotals(string curve, long expected)
    {
        Assert.Equal(expected, ExpCurve.TotalExp(curve, 100));
    }

    [Theory]
    [InlineData(2, 8)]
    [InlineData(3, 27)]
    [InlineData(5, 125)]
    public void MediumFast_IsLevelCubed(int level, long expected)
    {
        Assert.Equal(expected, ExpCurve.TotalExp("medium-fast", level));
    }

    [Theory]
    [InlineData(2, 9)]
    [InlineData(3, 57)]
    public void MediumSlow_MatchesTable(int level, long expected)
    {
        Assert.Equal(expected, ExpCurve.TotalExp("medium-slow", level));
    }

    [Fact]
    public void UnknownCurve_DefaultsToMediumFast()
    {
        Assert.Equal(ExpCurve.TotalExp("medium-fast", 40), ExpCurve.TotalExp("turbo", 40));
    }

    [Fact]
    public void TotalExp_IsMonotonic()
    {
        for (int lvl = 2; lvl <= 100; lvl++)
            Assert.True(ExpCurve.TotalExp("medium-slow", lvl) > ExpCurve.TotalExp("medium-slow", lvl - 1));
    }

    [Fact]
    public void LevelForExp_FindsCorrectLevel()
    {
        // medium-fast: level 5 = 125, level 6 = 216.
        Assert.Equal(5, ExpCurve.LevelForExp("medium-fast", 125)); // exact threshold
        Assert.Equal(5, ExpCurve.LevelForExp("medium-fast", 200)); // between 5 and 6
        Assert.Equal(6, ExpCurve.LevelForExp("medium-fast", 216));
        Assert.Equal(1, ExpCurve.LevelForExp("medium-fast", 0));
        Assert.Equal(100, ExpCurve.LevelForExp("medium-fast", 999_999_999));
    }
}

public sealed class ExpCalcTests
{
    [Fact]
    public void Yield_WildSingleParticipant()
    {
        // floor(64 * 20 / 7) = floor(182.8) = 182.
        Assert.Equal(182, ExpCalc.Yield(64, 20, trainer: false, participants: 1));
    }

    [Fact]
    public void Yield_TrainerBonus_IsOneAndHalf()
    {
        // floor(64*20/7 * 1.5) = floor(274.28) = 274.
        Assert.Equal(274, ExpCalc.Yield(64, 20, trainer: true, participants: 1));
    }

    [Fact]
    public void Yield_SplitAmongParticipants()
    {
        Assert.Equal(91, ExpCalc.Yield(64, 20, trainer: false, participants: 2));
    }

    [Fact]
    public void Yield_ZeroParticipants_TreatedAsOne()
    {
        Assert.Equal(182, ExpCalc.Yield(64, 20, trainer: false, participants: 0));
    }
}
