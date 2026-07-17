using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>An IRng returning scripted values, for deterministic roll tests.</summary>
internal sealed class FakeRng : IRng
{
    private readonly Queue<int> _ints;
    private readonly Queue<double> _doubles;

    public FakeRng(int[]? ints = null, double[]? doubles = null)
    {
        _ints = new Queue<int>(ints ?? []);
        _doubles = new Queue<double>(doubles ?? []);
    }

    public int Next(int maxExclusive) => _ints.Dequeue();
    public int Next(int minInclusive, int maxExclusive) => _ints.Dequeue();
    public double NextDouble() => _doubles.Dequeue();
}

public sealed class BattleRollsTests
{
    [Fact]
    public void Hits_NullAccuracy_AlwaysHits_NoDraw()
    {
        Assert.True(BattleRolls.Hits(null, new FakeRng())); // empty rng → must not draw
    }

    [Theory]
    [InlineData(100, 99, true)]  // d100=99 < 100 → hit
    [InlineData(50, 49, true)]
    [InlineData(50, 50, false)]
    [InlineData(0, 0, false)]    // 0 accuracy never hits
    public void Hits_RollsAgainstAccuracy(int accuracy, int roll, bool expected)
    {
        Assert.Equal(expected, BattleRolls.Hits(accuracy, new FakeRng(ints: [roll])));
    }

    [Theory]
    [InlineData(50, 6, 0, 99, true)]
    [InlineData(100, 0, 6, 50, false)]
    [InlineData(100, -6, 6, 34, false)]
    public void Hits_AppliesAccuracyAndEvasionStages(int accuracy, int accuracyStage, int evasionStage, int roll, bool expected)
    {
        Assert.Equal(expected, BattleRolls.Hits(accuracy, accuracyStage, evasionStage, new FakeRng(ints: [roll])));
    }

    [Theory]
    [InlineData(0, 1, 16)]
    [InlineData(-3, 1, 16)] // clamped low
    [InlineData(1, 1, 8)]
    [InlineData(2, 1, 4)]
    [InlineData(3, 1, 3)]
    [InlineData(4, 1, 2)]
    [InlineData(9, 1, 2)] // clamped high
    public void CritChance_ByStage(int stage, int numerator, int denominator)
    {
        Assert.Equal(new BattleQueryValue(numerator, denominator), BattleRolls.CritChanceValue(stage));
        Assert.Equal((double)numerator / denominator, BattleRolls.CritChance(stage), precision: 10);
    }

    [Fact]
    public void IsCrit_ComparesDrawToChance()
    {
        Assert.True(BattleRolls.IsCrit(0, new FakeRng(doubles: [0.05])));  // < 1/16
        Assert.False(BattleRolls.IsCrit(0, new FakeRng(doubles: [0.07]))); // > 1/16
    }

    [Theory]
    [InlineData(0, 85)]
    [InlineData(15, 100)]
    public void DamageRoll_Maps0To15Onto85To100(int draw, int expected)
    {
        Assert.Equal(expected, BattleRolls.DamageRoll(new FakeRng(ints: [draw])));
    }

    // --- Statistical (seeded real RNG) ---

    [Fact]
    public void Accuracy80_HitsAboutEightyPercent()
    {
        var rng = new Rng(42);
        int hits = Enumerable.Range(0, 10_000).Count(_ => BattleRolls.Hits(80, rng));
        Assert.InRange(hits, 7600, 8400); // ~8000 ± tolerance
    }

    [Fact]
    public void CritStage0_AboutOneInSixteen()
    {
        var rng = new Rng(7);
        int crits = Enumerable.Range(0, 10_000).Count(_ => BattleRolls.IsCrit(0, rng));
        Assert.InRange(crits, 475, 775); // ~625 ± tolerance
    }

    [Fact]
    public void DamageRoll_AlwaysInRange_AndSpansEnds()
    {
        var rng = new Rng(9);
        var rolls = Enumerable.Range(0, 5000).Select(_ => BattleRolls.DamageRoll(rng)).ToList();
        Assert.All(rolls, r => Assert.InRange(r, 85, 100));
        Assert.Contains(85, rolls);
        Assert.Contains(100, rolls);
    }
}
