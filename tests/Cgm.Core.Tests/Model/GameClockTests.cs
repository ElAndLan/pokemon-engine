using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

public sealed class GameClockTests
{
    [Theory]
    [InlineData(6 * 60, TimeOfDay.Day)]       // 06:00 → day begins
    [InlineData(12 * 60, TimeOfDay.Day)]      // noon
    [InlineData(17 * 60 + 59, TimeOfDay.Day)] // 17:59 → last day minute
    [InlineData(18 * 60, TimeOfDay.Night)]    // 18:00 → night begins
    [InlineData(23 * 60, TimeOfDay.Night)]
    [InlineData(0, TimeOfDay.Night)]          // midnight
    [InlineData(5 * 60 + 59, TimeOfDay.Night)]// 05:59 → last night minute
    public void PhaseAtMinute_DayNightBoundaries(int minute, TimeOfDay expected)
    {
        Assert.Equal(expected, GameClock.PhaseAtMinute(minute));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(90, 1, 30)]                  // 01:30
    [InlineData(GameClock.MinutesPerDay, 0, 0)]        // exactly one day → wraps to 00:00
    [InlineData(GameClock.MinutesPerDay + 90, 1, 30)]  // day + 90 → 01:30 again
    public void TimeAt_WrapsAcrossDays(long total, int hour, int minute)
    {
        GameTime t = GameClock.TimeAt(total);
        Assert.Equal(hour, t.Hour);
        Assert.Equal(minute, t.Minute);
    }

    [Fact]
    public void MinuteOfDay_HandlesNegativeTotals()
    {
        Assert.Equal(GameClock.MinutesPerDay - 1, GameClock.MinuteOfDay(-1)); // -1 → 23:59
        Assert.Equal(0, GameClock.MinuteOfDay(-GameClock.MinutesPerDay));
    }

    [Fact]
    public void OffsetPersistence_ContinuesFromSavedOffset()
    {
        // Save at 17:00, play 2 more in-game hours → 19:00 (night). Proves offset+delta composition.
        long savedOffset = 17 * 60;
        long afterPlay = savedOffset + 2 * 60;
        Assert.Equal(TimeOfDay.Night, GameClock.TimeAt(afterPlay).Phase);
        Assert.Equal(19, GameClock.TimeAt(afterPlay).Hour);
    }

    [Theory]
    // cycle = 60 real min per day → 24 game-min per real-min; 60 real sec → 24 game min.
    [InlineData(60, 60, 24)]
    // cycle = 24 real min per day → 60 game-min per real-min; 60 real sec → 60 game min.
    [InlineData(60, 24, 60)]
    // Half the cycle of real time → half a game day.
    [InlineData(30 * 60, 60, GameClock.MinutesPerDay / 2)]
    public void ElapsedGameMinutes_ScalesByCycleLength(double realSeconds, int cycleMinutes, long expected)
    {
        Assert.Equal(expected, GameClock.ElapsedGameMinutes(realSeconds, cycleMinutes));
    }

    [Fact]
    public void ElapsedGameMinutes_RejectsBadInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameClock.ElapsedGameMinutes(60, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameClock.ElapsedGameMinutes(-1, 60));
    }
}
