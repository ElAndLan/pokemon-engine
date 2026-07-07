namespace Cgm.Core.Battle;

/// <summary>The six experience curves (DATA_SCHEMA §4.15): total exp to reach a level, and the
/// inverse. Level 1 is 0 for every curve (formulas can go negative at low levels).</summary>
public static class ExpCurve
{
    public static long TotalExp(string growthRate, int level)
    {
        if (level <= 1) return 0;
        double n = level;
        double v = growthRate switch
        {
            "fast" => 4.0 * n * n * n / 5,
            "medium-fast" => n * n * n,
            "medium-slow" => 6.0 / 5 * n * n * n - 15 * n * n + 100 * n - 140,
            "slow" => 5.0 * n * n * n / 4,
            "erratic" => Erratic(level),
            "fluctuating" => Fluctuating(level),
            _ => n * n * n, // default: medium-fast
        };
        return (long)Math.Floor(v);
    }

    /// <summary>The level reached with a given total exp (1–100).</summary>
    public static int LevelForExp(string growthRate, long exp)
    {
        int level = 1;
        while (level < 100 && TotalExp(growthRate, level + 1) <= exp)
            level++;
        return level;
    }

    private static double Erratic(int n)
    {
        double c = (double)n * n * n;
        return n < 50 ? c * (100 - n) / 50
            : n < 68 ? c * (150 - n) / 100
            : n < 98 ? c * ((1911 - 10 * n) / 3) / 500 // integer floor inside
            : c * (160 - n) / 100;
    }

    private static double Fluctuating(int n)
    {
        double c = (double)n * n * n;
        return n < 15 ? c * ((n + 1) / 3 + 24) / 50
            : n < 36 ? c * (n + 14) / 50
            : c * (n / 2 + 32) / 50;
    }
}

/// <summary>Experience awarded for defeating a creature (MASTER_PLAN §8): baseExp·level/7, split
/// among participants, ×1.5 vs trainers.</summary>
public static class ExpCalc
{
    public static int Yield(int baseExp, int defeatedLevel, bool trainer, int participants)
    {
        double a = (double)baseExp * defeatedLevel / 7 / Math.Max(1, participants);
        if (trainer) a *= 1.5;
        return (int)a;
    }
}
