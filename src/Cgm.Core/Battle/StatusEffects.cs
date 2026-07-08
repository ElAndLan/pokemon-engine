using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>Persistent-status mechanics (Battle v4, BATTLE_SYSTEM_SPEC): a thin reader over the
/// data-defined <see cref="StatusConditions"/> registry (EFFECT_TYPES_CATALOG §7.1) — end-of-turn
/// residuals, stat/damage modifiers, action-blocking rolls, application rules, and the capture bonus.
/// Behavior data lives in the registry; this class applies it. Target: Gen III/IV.</summary>
public static class StatusEffects
{
    /// <summary>End-of-turn HP loss: burn/poison 1/8 of max, toxic ramps n/16 (n = 1..15). Min 1.</summary>
    public static int ResidualDamage(PersistentStatus status, int maxHp, int toxicCounter)
    {
        StatusConditionDef def = StatusConditions.For(status);
        if (def.ResidualDenominator == 0)
            return 0;
        int numerator = def.ResidualRamps ? Math.Clamp(toxicCounter, 1, 15) : 1;
        return Math.Max(1, maxHp * numerator / def.ResidualDenominator);
    }

    public static double SpeedMultiplier(PersistentStatus? status) =>
        status is { } s ? StatusConditions.For(s).SpeedMultiplier : 1.0;

    public static double BurnAttackMultiplier(PersistentStatus? status) =>
        status is { } s ? StatusConditions.For(s).PhysicalDamageMultiplier : 1.0;

    public static bool FullyParalyzed(IRng rng) =>
        rng.NextDouble() < StatusConditions.For(PersistentStatus.Paralysis).FullBlockChance;

    public static bool Thaws(IRng rng) =>
        rng.NextDouble() < StatusConditions.For(PersistentStatus.Freeze).ThawChance;

    /// <summary>Only one persistent status at a time.</summary>
    public static bool CanApplyStatus(PersistentStatus? current) => current is null;

    /// <summary>Type immunity to a status (Gen III/IV): fire↛burn, ice↛freeze, poison/steel↛poison.</summary>
    // ponytail: keyed on standard type slugs; fine for PokeAPI-derived and typical original types.
    public static bool TypeImmuneToStatus(PersistentStatus status, IReadOnlyList<EntityId> defenderTypes)
    {
        IReadOnlyList<string> immune = StatusConditions.For(status).ImmuneTypes;
        return immune.Count > 0 && defenderTypes.Any(t => immune.Contains(t.Slug));
    }

    /// <summary>Capture rate bonus by status (sleep/freeze ×2, others ×1.5).</summary>
    public static double CaptureStatusBonus(PersistentStatus? status) =>
        status is { } s ? StatusConditions.For(s).CaptureBonus : 1.0;
}
