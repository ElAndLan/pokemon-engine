using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>
/// Pure numeric math for the Battle v5 effect ops (BATTLE_SYSTEM_SPEC §Effect-op numeric formulas).
/// Same category as <see cref="DamageCalc"/>/<see cref="CaptureCalc"/>: the interpreter composes these;
/// they own no state. Floor rounding throughout (Gen III/IV integer math); amounts are ≥1 where noted.
/// </summary>
public static class EffectMath
{
    /// <summary>Number of hits for a multi-hit move. Fixed when <paramref name="min"/> == <paramref name="max"/>;
    /// the canonical 2–5 range uses the Gen III/IV weighting (2→3/8, 3→3/8, 4→1/8, 5→1/8); any other range is
    /// uniform.</summary>
    public static int HitCount(IRng rng, int min = 2, int max = 5)
    {
        if (min > max)
            throw new ArgumentException($"min {min} exceeds max {max}.");
        if (min == max)
            return min;
        if (min == 2 && max == 5)
        {
            int r = rng.Next(8); // 0–2 → 2, 3–5 → 3, 6 → 4, 7 → 5
            return r < 3 ? 2 : r < 6 ? 3 : r < 7 ? 4 : 5;
        }
        return min + rng.Next(max - min + 1);
    }

    /// <summary>HP the user drains (heals) from the damage it dealt — half by default, ≥1, 0 if no damage.</summary>
    public static int DrainHeal(int damageDealt, int num = 1, int den = 2) =>
        damageDealt <= 0 ? 0 : Math.Max(1, damageDealt * num / den);

    /// <summary>Recoil the user takes from the damage it dealt (e.g. ¼, ⅓) — ≥1, 0 if no damage.</summary>
    public static int RecoilDamage(int damageDealt, int num, int den) =>
        damageDealt <= 0 ? 0 : Math.Max(1, damageDealt * num / den);

    /// <summary>Crash damage a `crashOnMiss` move deals to its user when it misses (Gen IV: ½ maxHp).</summary>
    public static int CrashDamage(int maxHp, int num = 1, int den = 2) => maxHp * num / den;

    /// <summary>OHKO accuracy: <c>userLevel − targetLevel + 30</c>, or 0 (auto-fail) if the target out-levels
    /// the user.</summary>
    public static int OhkoAccuracy(int userLevel, int targetLevel) =>
        targetLevel > userLevel ? 0 : userLevel - targetLevel + 30;

    /// <summary>HP restored by a fixed-fraction heal (default ½ maxHp), ≥1.</summary>
    public static int HealAmount(int maxHp, int num = 1, int den = 2) => Math.Max(1, maxHp * num / den);
}
