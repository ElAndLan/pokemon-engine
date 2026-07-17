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
    public StatusChanceFormula? ChanceFormula { get; init; }
}

public enum StageEffectScope { Self, Target, Both }
public enum StageSwapGroup { All, Offense, Defense }
public enum MoveGateKind { FirstAction, NotPreviousMove }
public enum HpFractionRecipient { Self, Target }
public enum HpFractionOperation { Heal, Damage }
public enum HpFractionBasis { MaxHp, CurrentHp }
public enum StatusPowerSubject { User, Target }
public enum StatusCountSubject { User, Target, Both }
public enum TerrainMoveSubject { Field, User, Target }
public enum BattleVolatileStatus { Confusion, Flinch, Bound, Seeded, Protected }
public sealed record FormulaPowerBand(int MinInclusive, int Power);
public sealed record StatusChanceFormula(
    StatusPowerSubject Subject,
    PersistentStatus? Status,
    BattleVolatileStatus? Volatile,
    Fraction Multiplier);

/// <summary>apply_condition(persistent status) — burn/poison/paralysis/… on the target.</summary>
public sealed record AilmentEffect(PersistentStatus Status) : MoveEffect;

/// <summary>apply_condition(volatile:confusion) on the target.</summary>
public sealed record ConfusionEffect : MoveEffect;

/// <summary>modify_stat_stage on the user (<paramref name="OnSelf"/>) or target.</summary>
public sealed record StatChangeEffect(StatKind Stat, int Delta, bool OnSelf) : MoveEffect;

/// <summary>modify_stat_stage bundle for Atk/Def/SpA/SpD/Spe. One chance roll gates the whole bundle.</summary>
public sealed record StatChangeAllEffect(int Delta, bool OnSelf) : MoveEffect;

/// <summary>pay_cost(percent_max_hp) before later authored effects.</summary>
public sealed record HpCostEffect(Fraction Fraction, bool AllowFaint) : MoveEffect;

/// <summary>reset_stat_stages over self, target, or both active creatures.</summary>
public sealed record StatResetEffect(StageEffectScope Scope) : MoveEffect;

/// <summary>copy_stat_stages between active creatures.</summary>
public sealed record StatCopyEffect(StageEffectScope From, StageEffectScope To) : MoveEffect;

/// <summary>swap_stat_stages between active creatures.</summary>
public sealed record StatSwapEffect(StageSwapGroup Group) : MoveEffect;

/// <summary>invert_stat_stages on the user or target.</summary>
public sealed record StatInvertEffect(bool OnSelf) : MoveEffect;

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

/// <summary>Atomically exchanges the user and selected ally's active-slot assignments.</summary>
public sealed record PositionSwapEffect : MoveEffect;

public sealed record RedirectEffect(int Priority, IReadOnlySet<DamageClass> AcceptedClasses, IReadOnlySet<DamageClass> BypassClasses,
    IReadOnlySet<string> AcceptedTags, IReadOnlySet<string> BypassTags) : MoveEffect;

/// <summary>heal_hp(from_damage_dealt) — the user drains a fraction of the damage it dealt.</summary>
public sealed record DrainEffect(Fraction Fraction) : MoveEffect;

/// <summary>apply_recoil_or_crash(percent_damage_dealt) — the user takes a fraction of the damage dealt.</summary>
public sealed record RecoilEffect(Fraction Fraction) : MoveEffect;

/// <summary>heal_hp(percent_max_hp) on the user or target, with optional weather fraction overrides.</summary>
public sealed record HealEffect(
    Fraction Fraction,
    HpFractionRecipient Recipient = HpFractionRecipient.Self,
    IReadOnlyDictionary<Weather, Fraction>? WeatherFractions = null,
    IReadOnlyDictionary<Terrain, Fraction>? TerrainFractions = null) : MoveEffect;

/// <summary>Mutates a recipient's current or max-HP fraction through the shared HP primitives.</summary>
public sealed record HpFractionEffect(
    HpFractionRecipient Recipient,
    HpFractionOperation Operation,
    HpFractionBasis Basis,
    Fraction Fraction) : MoveEffect;

/// <summary>Changes base power when the move user or target has a matching persistent status.</summary>
public sealed record StatusPowerEffect(
    StatusPowerSubject Subject,
    PersistentStatus? Status,
    Fraction Multiplier,
    bool IgnoreSourceBurnPenalty,
    BattleVolatileStatus? Volatile = null) : MoveEffect;

public sealed record StatusCountPowerEffect(
    StatusCountSubject Subject,
    IReadOnlyList<PersistentStatus> PersistentStatuses,
    IReadOnlyList<BattleVolatileStatus> VolatileStatuses,
    int Base,
    int PerStatus) : MoveEffect;

public sealed record StatusChanceEffect(StatusChanceFormula Formula) : MoveEffect;
public enum HpEqualizeMode { Average, MatchSource }
public sealed record HpEqualizeEffect(HpEqualizeMode Mode) : MoveEffect;
public sealed record CannotKoEffect(int Floor) : MoveEffect;
public sealed record SpeedRatioPowerEffect(
    HpRatioPowerSource Numerator,
    HpRatioPowerSource Denominator,
    int? Scale,
    int Offset,
    int? Cap,
    IReadOnlyList<FormulaPowerBand> Bands) : MoveEffect;
public sealed record MetricBandPowerEffect(
    BattleMetric Metric,
    HpRatioPowerSource Subject,
    IReadOnlyList<FormulaPowerBand> Bands) : MoveEffect;
public sealed record MetricRatioPowerEffect(
    BattleMetric Metric,
    HpRatioPowerSource Numerator,
    HpRatioPowerSource Denominator,
    IReadOnlyList<FormulaPowerBand> Bands) : MoveEffect;
public sealed record ConsecutivePowerEffect(
    ConsecutivePowerScope Scope,
    ConsecutivePowerMode Mode,
    int Step,
    int Cap) : MoveEffect;
public sealed record HistoryPowerEffect(HistoryPowerCondition Condition, Fraction Multiplier) : MoveEffect;
public sealed record PartyCountPowerEffect(
    PartyMemberFilter Filter, int Base, int PerMember, int? Cap) : MoveEffect;
public sealed record FriendshipPowerEffect(FriendshipPowerMode Mode) : MoveEffect;
public sealed record PpPowerEffect(PpPowerTiming Timing, IReadOnlyList<FormulaPowerBand> Bands) : MoveEffect;
public sealed record PositiveStagePowerEffect(
    StatusPowerSubject Subject, int Base, int PerStage, int? Cap) : MoveEffect;
public sealed record ItemDataPowerEffect(ItemPowerField Field) : MoveEffect;
public sealed record RandomTablePowerEffect(IReadOnlyList<WeightedPowerEntry> Entries) : MoveEffect;

/// <summary>apply_condition(volatile:focus_energy) — raises the user's crit stage.</summary>
public sealed record CritBoostEffect(int Stages) : MoveEffect;

/// <summary>pay_cost(faint_self) — the user faints after connecting (Explosion).</summary>
public sealed record SelfDestructEffect : MoveEffect;

/// <summary>apply_condition(side:entry_hazard_damage) — adds a Spikes layer to the target's side,
/// damaging creatures that later switch in (catalog §7.3).</summary>
public sealed record EntryHazardEffect : MoveEffect;

/// <summary>apply_condition(field:weather) — sets battlefield weather (catalog §7.6).</summary>
public sealed record SetWeatherEffect(Weather Weather) : MoveEffect;

public sealed record SetTerrainEffect(Terrain Terrain) : MoveEffect;
public sealed record TerrainMoveEffect(
    TerrainMoveSubject Subject,
    IReadOnlyDictionary<Terrain, EntityId> TypeOverrides,
    IReadOnlyDictionary<Terrain, Fraction> PowerMultipliers,
    IReadOnlyDictionary<Terrain, int> PriorityModifiers,
    IReadOnlySet<Terrain> SpreadTerrains) : MoveEffect;
public sealed record TerrainGateEffect : MoveEffect;
public sealed record RemoveTerrainEffect : MoveEffect;

/// <summary>Changes this move's accuracy under authored weather conditions.</summary>
public sealed record WeatherAccuracyEffect(
    IReadOnlySet<Weather> BypassWeather,
    IReadOnlyDictionary<Weather, int> AccuracyOverrides) : MoveEffect;

/// <summary>Changes this move's effective type, base power, or charge requirement under authored weather rows.</summary>
public sealed record WeatherMoveEffect(
    IReadOnlyDictionary<Weather, EntityId> TypeOverrides,
    IReadOnlyDictionary<Weather, Fraction> PowerMultipliers,
    IReadOnlySet<Weather> SkipChargeWeather) : MoveEffect;

/// <summary>apply_condition(side:entry_hazard_damage, type_scaled) — Stealth-Rock-style hazard on the
/// target's side, dealing type-scaled damage on switch-in (catalog §7.3).</summary>
public sealed record StealthRockEffect : MoveEffect;

/// <summary>Pre-move legality gate evaluated before PP, RNG, damage, and later effects.</summary>
public sealed record MoveGateEffect(MoveGateKind Kind) : MoveEffect;

/// <summary>Queues a source-slot action skip for a future turn.</summary>
public sealed record QueueActionGateEffect(int Turns) : MoveEffect;
