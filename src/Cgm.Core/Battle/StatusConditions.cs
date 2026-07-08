using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>
/// A persistent status as a data-defined condition (EFFECT_TYPES_CATALOG §7.1): its hook parameters in
/// one place — end-of-turn residual, damage/speed query modifiers, on-before-move block/thaw chances,
/// application immunity, and capture bonus. One table row per status replaces scattered switches, so a
/// new status/condition is new data, not a new branch. Hooks are represented today by which parameters
/// are populated (residual → on_turn_end, damage mult → on_damage_query, …); executable data hooks are
/// a later catalog layer.
/// </summary>
public sealed record StatusConditionDef
{
    public required PersistentStatus Status { get; init; }
    public int ResidualDenominator { get; init; }              // on_turn_end: maxHp / denom (0 = no residual)
    public bool ResidualRamps { get; init; }                   // toxic: n/16 ramping counter
    public double SpeedMultiplier { get; init; } = 1.0;        // modify_stat_query(speed)
    public double PhysicalDamageMultiplier { get; init; } = 1.0; // on_damage_query (burn cuts physical)
    public double FullBlockChance { get; init; }               // on_before_move (paralysis)
    public double ThawChance { get; init; }                    // on_before_move (freeze)
    public IReadOnlyList<string> ImmuneTypes { get; init; } = []; // application immunity by type slug
    public double CaptureBonus { get; init; } = 1.0;
}

/// <summary>The status condition registry (Gen III/IV). The single source of per-status behavior data.</summary>
public static class StatusConditions
{
    private static readonly IReadOnlyDictionary<PersistentStatus, StatusConditionDef> Registry =
        new StatusConditionDef[]
        {
            new() { Status = PersistentStatus.Burn, ResidualDenominator = 8, PhysicalDamageMultiplier = 0.5,
                    ImmuneTypes = ["fire"], CaptureBonus = 1.5 },
            new() { Status = PersistentStatus.Poison, ResidualDenominator = 8,
                    ImmuneTypes = ["poison", "steel"], CaptureBonus = 1.5 },
            new() { Status = PersistentStatus.Toxic, ResidualDenominator = 16, ResidualRamps = true,
                    ImmuneTypes = ["poison", "steel"], CaptureBonus = 1.5 },
            new() { Status = PersistentStatus.Paralysis, SpeedMultiplier = 0.25, FullBlockChance = 0.25,
                    CaptureBonus = 1.5 },
            new() { Status = PersistentStatus.Sleep, CaptureBonus = 2.0 },
            new() { Status = PersistentStatus.Freeze, ThawChance = 0.20, ImmuneTypes = ["ice"],
                    CaptureBonus = 2.0 },
        }.ToDictionary(d => d.Status);

    public static StatusConditionDef For(PersistentStatus status) => Registry[status];
}
