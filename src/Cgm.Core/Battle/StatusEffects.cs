using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>Persistent-status mechanics (Battle v4, BATTLE_SYSTEM_SPEC): end-of-turn residuals,
/// stat modifiers, action-blocking rolls, application rules, and the capture status bonus. Target:
/// Gen III/IV.</summary>
public static class StatusEffects
{
    /// <summary>End-of-turn HP loss: burn/poison 1/8 of max, toxic ramps n/16 (n = 1..15). Min 1.</summary>
    public static int ResidualDamage(PersistentStatus status, int maxHp, int toxicCounter) => status switch
    {
        PersistentStatus.Burn or PersistentStatus.Poison => Math.Max(1, maxHp / 8),
        PersistentStatus.Toxic => Math.Max(1, maxHp * Math.Clamp(toxicCounter, 1, 15) / 16),
        _ => 0,
    };

    public static double SpeedMultiplier(PersistentStatus? status) =>
        status == PersistentStatus.Paralysis ? 0.25 : 1.0;

    public static double BurnAttackMultiplier(PersistentStatus? status) =>
        status == PersistentStatus.Burn ? 0.5 : 1.0;

    public static bool FullyParalyzed(IRng rng) => rng.NextDouble() < 0.25;
    public static bool Thaws(IRng rng) => rng.NextDouble() < 0.20;

    /// <summary>Only one persistent status at a time.</summary>
    public static bool CanApplyStatus(PersistentStatus? current) => current is null;

    /// <summary>Type immunity to a status (Gen III/IV): fire↛burn, ice↛freeze, poison/steel↛poison.</summary>
    // ponytail: keyed on standard type slugs; fine for PokeAPI-derived and typical original types.
    public static bool TypeImmuneToStatus(PersistentStatus status, IReadOnlyList<EntityId> defenderTypes)
    {
        var slugs = defenderTypes.Select(t => t.Slug).ToHashSet();
        return status switch
        {
            PersistentStatus.Burn => slugs.Contains("fire"),
            PersistentStatus.Freeze => slugs.Contains("ice"),
            PersistentStatus.Poison or PersistentStatus.Toxic => slugs.Contains("poison") || slugs.Contains("steel"),
            _ => false,
        };
    }

    /// <summary>Capture rate bonus by status (sleep/freeze ×2, others ×1.5).</summary>
    public static double CaptureStatusBonus(PersistentStatus? status) => status switch
    {
        PersistentStatus.Sleep or PersistentStatus.Freeze => 2.0,
        PersistentStatus.Burn or PersistentStatus.Poison or PersistentStatus.Toxic or PersistentStatus.Paralysis => 1.5,
        _ => 1.0,
    };
}
