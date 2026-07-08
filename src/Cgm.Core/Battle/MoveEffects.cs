using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>
/// A compiled effect on a move — a primitive op plus its params (EFFECT_TYPES_CATALOG §3). A move
/// carries an *ordered list* of these; the resolver dispatches each to a shared primitive. These are
/// effect-type records (the primitive palette), never per-move code — Growl and Swords Dance are both
/// a <see cref="StatChangeEffect"/>, Ember and Will-O-Wisp both an <see cref="AilmentEffect"/>.
/// </summary>
public abstract record MoveEffect
{
    /// <summary>Percent chance the effect applies (chance_gate); 100 = guaranteed.</summary>
    public int Chance { get; init; } = 100;
}

/// <summary>apply_condition(persistent status) — burn/poison/paralysis/… on the target.</summary>
public sealed record AilmentEffect(PersistentStatus Status) : MoveEffect;

/// <summary>apply_condition(volatile:confusion) on the target.</summary>
public sealed record ConfusionEffect : MoveEffect;

/// <summary>modify_stat_stage on the user (<paramref name="OnSelf"/>) or target.</summary>
public sealed record StatChangeEffect(StatKind Stat, int Delta, bool OnSelf) : MoveEffect;

/// <summary>apply_condition(volatile:flinch) on the target.</summary>
public sealed record FlinchEffect : MoveEffect;

/// <summary>apply_condition(volatile:leech_seed) on the target.</summary>
public sealed record LeechSeedEffect : MoveEffect;

/// <summary>apply_condition(volatile:partial_trap) on the target (Bind/Wrap/Fire Spin).</summary>
public sealed record BindEffect : MoveEffect;

/// <summary>apply_condition(volatile:protect_family) on the user, with success-chain decay.</summary>
public sealed record ProtectEffect : MoveEffect;

/// <summary>switch_flow(force_target_switch) — forces the target out (Roar/Whirlwind).</summary>
public sealed record ForceSwitchEffect : MoveEffect;

/// <summary>heal_hp(from_damage_dealt) — the user drains a fraction of the damage it dealt.</summary>
public sealed record DrainEffect(Fraction Fraction) : MoveEffect;

/// <summary>apply_recoil_or_crash(percent_damage_dealt) — the user takes a fraction of the damage dealt.</summary>
public sealed record RecoilEffect(Fraction Fraction) : MoveEffect;

/// <summary>heal_hp(percent_max_hp) on the user (Recover-style).</summary>
public sealed record HealEffect(Fraction Fraction) : MoveEffect;

/// <summary>apply_condition(volatile:focus_energy) — raises the user's crit stage.</summary>
public sealed record CritBoostEffect(int Stages) : MoveEffect;

/// <summary>pay_cost(faint_self) — the user faints after connecting (Explosion).</summary>
public sealed record SelfDestructEffect : MoveEffect;

/// <summary>apply_condition(side:entry_hazard_damage) — adds a Spikes layer to the target's side,
/// damaging creatures that later switch in (catalog §7.3).</summary>
public sealed record EntryHazardEffect : MoveEffect;

/// <summary>apply_condition(field:weather) — sets battlefield weather (catalog §7.6).</summary>
public sealed record SetWeatherEffect(Weather Weather) : MoveEffect;

/// <summary>apply_condition(side:entry_hazard_damage, type_scaled) — Stealth-Rock-style hazard on the
/// target's side, dealing type-scaled damage on switch-in (catalog §7.3).</summary>
public sealed record StealthRockEffect : MoveEffect;
