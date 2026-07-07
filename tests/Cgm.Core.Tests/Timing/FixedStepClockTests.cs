using Cgm.Core.Timing;

namespace Cgm.Core.Tests.Timing;

public sealed class FixedStepClockTests
{
    private const double Interval60 = 1000.0 / 60.0;

    [Fact]
    public void Defaults_Are60HzAnd5TickCap()
    {
        var clock = new FixedStepClock();
        Assert.Equal(60, clock.TickRate);
        Assert.Equal(5, clock.MaxTicksPerAdvance);
        Assert.Equal(Interval60, clock.TickIntervalMs, precision: 10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-16)]
    public void Constructor_RejectsNonPositiveTickRate(int tickRate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepClock(tickRate));
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCap()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepClock(60, 0));
    }

    [Fact]
    public void Advance_ZeroElapsed_ProducesNoTicks()
    {
        var clock = new FixedStepClock();
        Assert.Equal(0, clock.Advance(0));
        Assert.Equal(0.0, clock.AccumulatorMs);
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(-100.0)]
    public void Advance_NegativeElapsed_Throws(double elapsed)
    {
        var clock = new FixedStepClock();
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(elapsed));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Advance_NonFiniteElapsed_Throws(double elapsed)
    {
        var clock = new FixedStepClock();
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(elapsed));
    }

    [Fact]
    public void Advance_LessThanOneInterval_ProducesNoTicksButAccumulates()
    {
        var clock = new FixedStepClock();
        Assert.Equal(0, clock.Advance(10.0));
        Assert.Equal(10.0, clock.AccumulatorMs, precision: 10);
    }

    [Fact]
    public void Advance_ExactlyOneInterval_ProducesOneTick()
    {
        var clock = new FixedStepClock();
        Assert.Equal(1, clock.Advance(Interval60));
        Assert.Equal(0.0, clock.AccumulatorMs, precision: 9);
    }

    [Fact]
    public void Advance_ProducesExactTickCountForKnownElapsed()
    {
        var clock = new FixedStepClock();
        // 10 intervals of time in one shot -> 10 ticks (well under the 5-cap? no: capped).
        // Use 4 intervals to stay under the cap and assert exactness.
        Assert.Equal(4, clock.Advance(Interval60 * 4));
        Assert.Equal(0.0, clock.AccumulatorMs, precision: 8);
    }

    [Fact]
    public void Advance_CarriesRemainderAcrossCalls()
    {
        var clock = new FixedStepClock();
        // 1.5 intervals -> 1 tick, 0.5 remainder
        Assert.Equal(1, clock.Advance(Interval60 * 1.5));
        Assert.Equal(Interval60 * 0.5, clock.AccumulatorMs, precision: 8);
        // another 0.6 intervals -> total 1.1 remainder -> 1 more tick, 0.1 remainder
        Assert.Equal(1, clock.Advance(Interval60 * 0.6));
        Assert.Equal(Interval60 * 0.1, clock.AccumulatorMs, precision: 8);
    }

    [Fact]
    public void Advance_ClampsToMaxTicksAndShedsBacklog()
    {
        var clock = new FixedStepClock(60, maxTicksPerAdvance: 5);
        // Feed a huge stall worth ~60 ticks; must yield exactly the cap.
        Assert.Equal(5, clock.Advance(Interval60 * 60));
        // Backlog shed: alpha must remain in [0,1).
        Assert.InRange(clock.InterpolationAlpha, 0.0, 0.9999999999);
        Assert.True(clock.AccumulatorMs < clock.TickIntervalMs);
    }

    [Fact]
    public void InterpolationAlpha_StaysInRangeAcrossManyRandomFrames()
    {
        var clock = new FixedStepClock();
        var rng = new Random(12345);
        for (int i = 0; i < 100_000; i++)
        {
            clock.Advance(rng.NextDouble() * 40.0); // 0..40ms frames
            Assert.InRange(clock.InterpolationAlpha, 0.0, 0.9999999999);
        }
    }

    [Fact]
    public void Advance_NoDriftOverSimulatedTenMinutes()
    {
        var clock = new FixedStepClock();
        // Feed 10 minutes as steady 60fps frames (one interval per frame).
        // Expect exactly one tick per frame and negligible accumulated drift.
        const int frames = 60 * 600; // 36,000
        long totalTicks = 0;
        for (int i = 0; i < frames; i++)
            totalTicks += clock.Advance(Interval60);

        Assert.Equal(frames, totalTicks);
        Assert.True(clock.AccumulatorMs < clock.TickIntervalMs);
        Assert.Equal(0.0, clock.AccumulatorMs, precision: 3);
    }

    [Fact]
    public void Reset_ClearsAccumulator()
    {
        var clock = new FixedStepClock();
        clock.Advance(10.0);
        Assert.True(clock.AccumulatorMs > 0);
        clock.Reset();
        Assert.Equal(0.0, clock.AccumulatorMs);
    }
}
