using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public readonly record struct CaptureResult(int Shakes, bool Caught);

/// <summary>
/// Gen III/IV capture math (MASTER_PLAN §8): modified catch value <c>a</c>, then up to four shake
/// checks against a threshold derived from <c>a</c>. Lower HP, higher catch rate, better balls, and
/// status all raise the odds. Pure; the four shakes draw from the injected <see cref="IRng"/>.
/// </summary>
public static class CaptureCalc
{
    /// <summary>a = ((3·maxHP − 2·curHP)·rate·ball / (3·maxHP))·status, floored.</summary>
    public static int CatchValue(int maxHp, int curHp, int catchRate, double ballBonus, double statusBonus)
    {
        double a = (3.0 * maxHp - 2 * curHp) * catchRate * ballBonus / (3.0 * maxHp) * statusBonus;
        return (int)a;
    }

    public static bool GuaranteedAt(int catchValue) => catchValue >= 255;

    /// <summary>Per-shake success probability for a catch value (1.0 at ≥255).</summary>
    public static double ShakeProbability(int catchValue)
    {
        if (catchValue <= 0) return 0.0;
        if (catchValue >= 255) return 1.0;
        double b = 65536.0 / Math.Pow(255.0 / catchValue, 0.25);
        return Math.Min(1.0, b / 65536.0);
    }

    public static CaptureResult Attempt(int maxHp, int curHp, int catchRate,
        double ballBonus, double statusBonus, IRng rng)
    {
        int a = CatchValue(maxHp, curHp, catchRate, ballBonus, statusBonus);
        if (a >= 255)
            return new CaptureResult(4, true);

        double p = ShakeProbability(a);
        int shakes = 0;
        for (int i = 0; i < 4; i++)
        {
            if (rng.NextDouble() < p)
                shakes++;
            else
                break;
        }
        return new CaptureResult(shakes, shakes == 4);
    }
}
