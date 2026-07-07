using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class CaptureCalcTests
{
    [Fact]
    public void CatchValue_FullHp_IsRateBallStatusOverThree()
    {
        // full HP: (3max-2max)=max → a = rate*ball*status/3. 45/3 = 15.
        Assert.Equal(15, CaptureCalc.CatchValue(100, 100, 45, 1.0, 1.0));
    }

    [Fact]
    public void CatchValue_LowHp_IsHigherThanFullHp()
    {
        int full = CaptureCalc.CatchValue(100, 100, 45, 1.0, 1.0);
        int low = CaptureCalc.CatchValue(100, 1, 45, 1.0, 1.0);
        Assert.True(low > full);
        Assert.Equal(44, low); // (298*45)/300 = 44.7 → 44
    }

    [Fact]
    public void CatchValue_BallAndStatus_ScaleUp()
    {
        int baseVal = CaptureCalc.CatchValue(100, 100, 45, 1.0, 1.0);   // 15
        Assert.Equal(30, CaptureCalc.CatchValue(100, 100, 45, 2.0, 1.0)); // great ball ×2
        Assert.True(CaptureCalc.CatchValue(100, 100, 45, 1.0, 2.0) > baseVal); // sleep ×2
    }

    [Fact]
    public void HighValue_IsGuaranteed()
    {
        // Master-ball-like: huge bonus → a ≥ 255 → always caught.
        var result = CaptureCalc.Attempt(100, 1, 255, 255.0, 1.0, new FakeRng());
        Assert.True(result.Caught);
        Assert.Equal(4, result.Shakes);
        Assert.True(CaptureCalc.GuaranteedAt(CaptureCalc.CatchValue(100, 1, 255, 255.0, 1.0)));
    }

    [Theory]
    [InlineData(0, 0.0)]
    [InlineData(255, 1.0)]
    [InlineData(300, 1.0)]
    public void ShakeProbability_Bounds(int a, double expected)
    {
        Assert.Equal(expected, CaptureCalc.ShakeProbability(a));
    }

    [Fact]
    public void ShakeProbability_MidValue_MatchesFormula()
    {
        // a=44 → b = 65536 / (255/44)^0.25 ; p = b/65536 ≈ 0.6445.
        Assert.InRange(CaptureCalc.ShakeProbability(44), 0.643, 0.646);
    }

    [Fact]
    public void Attempt_StopsShakingOnFirstFailure()
    {
        // Draws: 0.1 (pass), 0.99 (fail) → 1 shake, not caught. p(44)≈0.644.
        var rng = new FakeRng(doubles: [0.1, 0.99]);
        var result = CaptureCalc.Attempt(100, 1, 45, 1.0, 1.0, rng);
        Assert.Equal(1, result.Shakes);
        Assert.False(result.Caught);
    }

    [Fact]
    public void Attempt_AllFourPass_IsCaught()
    {
        var rng = new FakeRng(doubles: [0.1, 0.1, 0.1, 0.1]);
        Assert.True(CaptureCalc.Attempt(100, 1, 45, 1.0, 1.0, rng).Caught);
    }

    [Fact]
    public void CatchRate_DistributionMatchesTheory()
    {
        var rng = new Rng(2026);
        double p = CaptureCalc.ShakeProbability(CaptureCalc.CatchValue(100, 1, 45, 1.0, 1.0));
        double expectedRate = Math.Pow(p, 4);

        int caught = Enumerable.Range(0, 20_000).Count(_ =>
            CaptureCalc.Attempt(100, 1, 45, 1.0, 1.0, rng).Caught);

        double actual = caught / 20_000.0;
        Assert.InRange(actual, expectedRate - 0.03, expectedRate + 0.03);
    }
}
