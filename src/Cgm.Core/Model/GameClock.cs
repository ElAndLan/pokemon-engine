namespace Cgm.Core.Model;

/// <summary>An in-game wall time: 0–23 hour, 0–59 minute, and its day/night phase.</summary>
public readonly record struct GameTime(int Hour, int Minute, TimeOfDay Phase);

/// <summary>
/// The day/night sim clock (Phase 13). Pure: game time is a function of accumulated in-game minutes,
/// which the save persists as an offset (ADDENDUM §6 "clock offset"). The runtime feeds elapsed play
/// time (in-game mode) or the real clock's minute-of-day (real-time mode) — no wall-clock read lives
/// in Core, so replays stay deterministic. Two phases: Day 06:00–17:59, Night 18:00–05:59, which drive
/// time-of-day encounter slots and evolutions.
/// </summary>
public static class GameClock
{
    public const int MinutesPerDay = 24 * 60;
    public const int DayStartMinute = 6 * 60;   // 06:00
    public const int NightStartMinute = 18 * 60; // 18:00

    /// <summary>Minute-of-day (0–1439) for any total, wrapping and handling negatives.</summary>
    public static int MinuteOfDay(long totalGameMinutes) =>
        (int)(((totalGameMinutes % MinutesPerDay) + MinutesPerDay) % MinutesPerDay);

    public static TimeOfDay PhaseAtMinute(int minuteOfDay) =>
        minuteOfDay >= DayStartMinute && minuteOfDay < NightStartMinute ? TimeOfDay.Day : TimeOfDay.Night;

    public static GameTime TimeAt(long totalGameMinutes)
    {
        int m = MinuteOfDay(totalGameMinutes);
        return new GameTime(m / 60, m % 60, PhaseAtMinute(m));
    }

    /// <summary>In-game minutes elapsed over a span of real play seconds, for an in-game cycle of
    /// <paramref name="cycleMinutes"/> real minutes per full day. Add this to the saved offset.</summary>
    public static long ElapsedGameMinutes(double realSecondsPlayed, int cycleMinutes)
    {
        if (cycleMinutes <= 0)
            throw new ArgumentOutOfRangeException(nameof(cycleMinutes), "Cycle length must be positive.");
        if (realSecondsPlayed < 0)
            throw new ArgumentOutOfRangeException(nameof(realSecondsPlayed), "Elapsed time cannot be negative.");

        return (long)(realSecondsPlayed / 60.0 * (MinutesPerDay / (double)cycleMinutes));
    }
}
