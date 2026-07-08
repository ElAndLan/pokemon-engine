using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>Volatile battle effects (Battle v4): confusion and flinch. Cleared on switch/end.
/// Target: Gen III/IV (confusion 1–4 turns, 50% self-hit).</summary>
public static class VolatileEffects
{
    public static int ConfusionDuration(IRng rng) => rng.Next(1, 5); // 1–4 turns

    public static bool HitsSelfInConfusion(IRng rng) => rng.NextDouble() < 0.50;

    /// <summary>Confusion self-hit: a typeless 40-power physical hit using the user's own Atk/Def,
    /// no crit, STAB, or damage roll.</summary>
    public static int ConfusionSelfDamage(int level, int atk, int def) =>
        DamageCalc.Compute(level, 40, atk, def, effectiveness: 1.0, stab: 1.0, crit: false, roll: 100, burn: false);

    public static bool Flinches(int flinchChance, IRng rng) => flinchChance > 0 && rng.Next(100) < flinchChance;
}
