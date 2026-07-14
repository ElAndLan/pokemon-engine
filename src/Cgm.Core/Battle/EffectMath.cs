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
        => HitCount(rng, min, max, out _);

    public static int HitCount(IRng rng, int min, int max, out int? draw)
    {
        draw = null;
        if (min > max)
            throw new ArgumentException($"min {min} exceeds max {max}.");
        if (min == max)
            return min;
        if (min == 2 && max == 5)
        {
            int r = rng.Next(8); // 0–2 → 2, 3–5 → 3, 6 → 4, 7 → 5
            draw = r;
            return r < 3 ? 2 : r < 6 ? 3 : r < 7 ? 4 : 5;
        }
        draw = rng.Next(max - min + 1);
        return min + draw.Value;
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

    public static int HpFractionAmount(int currentHp, int maxHp, HpFractionBasis basis, Fraction fraction)
    {
        if (fraction.Num <= 0 || fraction.Den <= 0)
            throw new ArgumentOutOfRangeException(nameof(fraction), "HP fraction must be positive.");

        int source = basis == HpFractionBasis.MaxHp ? maxHp : currentHp;
        return Math.Max(1, source * fraction.Num / fraction.Den);
    }

    public static int TargetHpThresholdPower(
        int basePower,
        int currentHp,
        int maxHp,
        int thresholdNum,
        int thresholdDen,
        int multiplierNum,
        int multiplierDen)
    {
        if (maxHp <= 0)
            return basePower;
        return currentHp * thresholdDen <= maxHp * thresholdNum
            ? Math.Max(1, basePower * multiplierNum / multiplierDen)
            : basePower;
    }

    /// <summary>Spikes-style entry-hazard damage on switch-in, scaling with layers: 1→1/8, 2→1/6,
    /// 3→1/4 of max HP (Gen III/IV). ≥1; 0 layers → no damage.</summary>
    public static int HpRatioPower(int basePower, int currentHp, int maxHp) =>
        maxHp <= 0 ? basePower : Math.Max(1, basePower * currentHp / maxHp);

    /// <summary>Applies a status-conditioned base-power multiplier when its condition is met.</summary>
    public static int StatusPower(int basePower, bool conditionMet, Fraction multiplier) =>
        !conditionMet ? basePower : Math.Max(1, basePower * multiplier.Num / multiplier.Den);

    public static int HazardDamage(int maxHp, int layers) => layers switch
    {
        1 => Math.Max(1, maxHp / 8),
        2 => Math.Max(1, maxHp / 6),
        >= 3 => Math.Max(1, maxHp / 4),
        _ => 0,
    };

    /// <summary>Stealth-Rock-style entry hazard: 1/8 max HP scaled by the hazard type's effectiveness
    /// against the switch-in (¼×–4×), so a 4×-weak creature loses ½ HP. ≥1.</summary>
    public static int TypeScaledHazardDamage(int maxHp, double effectiveness) =>
        Math.Max(1, (int)(maxHp / 8.0 * effectiveness));
}
