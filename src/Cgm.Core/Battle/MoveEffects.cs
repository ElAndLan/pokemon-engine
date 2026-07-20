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

public enum DamageQueryOwner { User, Target }
public sealed record DamageStatSelector(DamageQueryOwner Owner, StatKind Stat);
public sealed record DamageStatQueryEffect(
    DamageStatSelector? Offensive,
    DamageStatSelector? Defensive) : MoveEffect;

public enum DamageClassQueryMode { Physical, Special, HigherOffense }
public sealed record DamageClassQueryEffect(DamageClassQueryMode Mode) : MoveEffect;

public enum EffectivenessQueryMode { Standard, Inverse, Neutral }
public enum StabQuerySource { User, Target, None }
public sealed record EffectivenessQueryEffect(
    EffectivenessQueryMode Mode,
    EntityId? AdditionalType,
    EntityId? DefendingType,
    BattleQueryValue? DefendingTypeMultiplier,
    StabQuerySource StabSource = StabQuerySource.User) : MoveEffect;

public sealed record MoveQueryModifierEffect(
    BattleQueryId Query,
    BattleQueryOperation Operation,
    BattleQueryValue Operand) : MoveEffect;

public enum AccuracyQueryMode { Bypass, IgnoreTargetEvasion }
public sealed record AccuracyQueryEffect(AccuracyQueryMode Mode) : MoveEffect;

public enum OneShotQuery { Accuracy, CriticalChance }
public sealed record OneShotQueryEffect(OneShotQuery Query, int Duration) : MoveEffect;

public enum StageEffectScope { Self, Target, Both }
public enum StageSwapGroup { All, Offense, Defense }
public enum MoveGateKind
{
    FirstAction,
    NotPreviousMove,
    PreviousActionFailed,
    SourceBeforeTarget,
    SourceAfterTarget,
    TargetAction,
    DamageReceived,
}
public enum MoveGateTiming { Selection, BeforeMove, AfterMoveUsed }
public enum MoveGateTargetClass { AnyMove, DamagingMove, StatusMove }
public enum MoveGateDamageMode { Require, Forbid }
public enum QueueActionGateOwner { Slot, Creature }
public enum SemiInvulnerableState { Air, Underground, Underwater, Vanished }
public enum DelayedHealBasis { SourceMaxHp, TargetMaxHp }
public enum HpFractionRecipient { Self, Target }
public enum HpFractionOperation { Heal, Damage }
public enum HpFractionBasis { MaxHp, CurrentHp }
public enum StatusPowerSubject { User, Target }
public enum StatusCountSubject { User, Target, Both }
public enum TerrainMoveSubject { Field, User, Target }
public enum SideConditionTarget { Source, Target }
public enum SideConditionTiming { BeforeDamage, AfterHit }
public enum BattleVolatileStatus { Confusion, Flinch, Bound, Seeded, Protected }
public sealed record ItemRequireEffect(BattleItemSubject Subject, BattleItemRequirement Requirement) : MoveEffect;
public sealed record ItemMutationEffect(
    BattleItemOperation Operation,
    BattleItemSubject Subject = BattleItemSubject.User,
    int? Duration = null,
    string Cause = "move") : MoveEffect;
public sealed record AbilityMutationEffect(
    BattleAbilityOperation Operation,
    BattleAbilitySubject Source = BattleAbilitySubject.Target,
    BattleAbilitySubject Subject = BattleAbilitySubject.User,
    EntityId? Ability = null) : MoveEffect;
public sealed record TypeMutationEffect(
    BattleTypeOperation Operation,
    BattleTypeSubject Subject,
    BattleTypeSubject? Source,
    IReadOnlyList<EntityId>? Types) : MoveEffect;
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

/// <summary>create a decoy (Substitute) costing a fraction of the user's max HP (15F-6).</summary>
public sealed record DecoyEffect(Fraction Cost) : MoveEffect;

/// <summary>transform the user into the target (copy effective identity via overlays) (15F-6).</summary>
public sealed record TransformEffect : MoveEffect;

/// <summary>Mimic: temporarily replace this move's own slot with the target's last-used move (15F-6).</summary>
public sealed record MoveReplaceEffect : MoveEffect;

/// <summary>Baton Pass: switch the user out and transfer its stat stages to the incoming creature (15G-1).</summary>
public sealed record BatonPassEffect : MoveEffect;

/// <summary>Pivot (U-turn/Volt Switch): switch the user out after the move resolves, no transfer (15G-1).</summary>
public sealed record PivotSwitchEffect : MoveEffect;

/// <summary>Revenge (Metal Burst/Comeuppance): return a multiple of the damage the user took this turn,
/// of any class, to the target — the "sum" damage-memory consumer (15G-3).</summary>
public sealed record RevengeDamageEffect(Fraction Multiplier) : MoveEffect;

/// <summary>Bide: the user stores damage taken over <paramref name="StoreTurns"/> locked turns, then
/// unleashes twice the stored total at an opponent — the cross-turn damage-memory consumer (15G-3).</summary>
public sealed record BideEffect(int StoreTurns) : MoveEffect;

/// <summary>reset_stat_stages over self, target, or both active creatures.</summary>
public sealed record StatResetEffect(StageEffectScope Scope) : MoveEffect;

/// <summary>copy_stat_stages between active creatures.</summary>
public sealed record StatCopyEffect(StageEffectScope From, StageEffectScope To) : MoveEffect;

/// <summary>swap_stat_stages between active creatures.</summary>
public sealed record StatSwapEffect(StageSwapGroup Group) : MoveEffect;

/// <summary>invert_stat_stages on the user or target.</summary>
public sealed record StatInvertEffect(bool OnSelf) : MoveEffect;

/// <summary>steal_positive_stat_stages: the user takes the target's stat boosts (15F-5).</summary>
public sealed record StatStealEffect : MoveEffect;

/// <summary>raise one random eligible stat stage by a delta on the user or target (15F-5).</summary>
public sealed record RandomStatRaiseEffect(int Delta, bool OnSelf) : MoveEffect;

/// <summary>swap a raw derived stat (e.g. Speed Swap) between user and target via overlays (15F-5).</summary>
public sealed record DerivedStatSwapEffect(StatKind Stat) : MoveEffect;

public enum DerivedStatGroup { Offense, Defense }

/// <summary>average a derived stat group across user and target (Power/Guard Split) via overlays (15F-5).</summary>
public sealed record DerivedStatSplitEffect(DerivedStatGroup Group) : MoveEffect;

/// <summary>apply_condition(volatile:flinch) on the target.</summary>
public sealed record FlinchEffect : MoveEffect;

/// <summary>apply_condition(volatile:leech_seed) on the target.</summary>
public sealed record LeechSeedEffect : MoveEffect;

/// <summary>apply_condition(volatile:partial_trap) on the target (Bind/Wrap/Fire Spin).</summary>
public sealed record BindEffect : MoveEffect;

/// <summary>Applies one typed personal or side protection profile.</summary>
public sealed record ProtectEffect(ProtectionProfile Profile) : MoveEffect;

/// <summary>Bypasses personal and side protection without removing either condition.</summary>
public sealed record ProtectionBypassEffect : MoveEffect;

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

/// <summary>Cures a matching persistent status from the user or one materialized target.</summary>
public sealed record StatusCureEffect(
    HpFractionRecipient Recipient,
    IReadOnlyList<PersistentStatus> Statuses,
    bool RequireDamage) : MoveEffect;

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

/// <summary>Applies a generic, typed entry-hazard side condition.</summary>
public sealed record SetEntryHazardEffect(EntryHazardProfile Hazard) : MoveEffect;

/// <summary>apply_condition(field:weather) — sets battlefield weather (catalog §7.6).</summary>
public sealed record SetWeatherEffect(Weather Weather) : MoveEffect;

public sealed record SetTerrainEffect(Terrain Terrain) : MoveEffect;

public sealed record GroundedStateEffect(
    GroundedState State,
    GroundedStateScope Scope,
    int Duration) : MoveEffect;
public sealed record SetFieldConditionEffect(BattleFieldCondition Condition, int Duration) : MoveEffect;
public sealed record SetSideConditionEffect(
    BattleSideCondition Condition,
    int Duration,
    SideConditionTarget Side = SideConditionTarget.Source) : MoveEffect;
public sealed record ApplyActionFilterEffect(
    ActionFilterKind Filter,
    SideConditionTarget Owner,
    int? Duration = null,
    string? MoveTag = null) : MoveEffect;
public sealed record CallMoveEffect(CallMoveProfile Profile) : MoveEffect;
public sealed record TurnOrderIntentEffect(TurnOrderIntentProfile Profile) : MoveEffect;
public sealed record PairedActionEffect(PairedActionProfile Profile) : MoveEffect;
public sealed record SideConditionBypassEffect(string Tag) : MoveEffect;
public sealed record RemoveSideConditionEffect(
    string Tag, SideConditionTarget Side, SideConditionTiming Timing) : MoveEffect;
public sealed record RemoveConditionEffect(
    BattleConditionSelector Selector,
    SideConditionTarget Owner) : MoveEffect;
public sealed record TransferConditionEffect(
    BattleConditionSelector Selector,
    SideConditionTarget From,
    SideConditionTarget To,
    bool ResetDuration,
    bool ResetCounters) : MoveEffect;
public sealed record SwapConditionEffect(
    BattleConditionSelector Selector,
    bool ResetDuration,
    bool ResetCounters) : MoveEffect;
public sealed record FieldMoveGateEffect(BattleFieldCondition Condition) : MoveEffect;
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

/// <summary>Typed action-legality gate evaluated at its authored timing checkpoint.</summary>
public sealed record MoveGateEffect(
    MoveGateKind Kind,
    MoveGateTiming Timing = MoveGateTiming.BeforeMove,
    MoveGateTargetClass? TargetClass = null,
    MoveGateDamageMode? DamageMode = null,
    DamageClass? DamageClass = null) : MoveEffect;

/// <summary>Queues a source-slot or source-creature action skip for a future turn.</summary>
public sealed record QueueActionGateEffect(
    int Turns,
    QueueActionGateOwner Owner = QueueActionGateOwner.Slot) : MoveEffect;

public sealed record ChargeMoveEffect(
    SemiInvulnerableState? State = null,
    BattleIntentTargetPolicy TargetPolicy = BattleIntentTargetPolicy.LiveSlot) : MoveEffect;

public sealed record SemiInvulnerableHitEffect(
    IReadOnlySet<SemiInvulnerableState> States,
    Fraction? PowerMultiplier = null) : MoveEffect;

public sealed record ChargeStartStatEffect(StatKind Stat, int Delta) : MoveEffect;
public sealed record MultiTurnPowerBoostEffect(string Key, Fraction Multiplier) : MoveEffect;

public sealed record DelayedDamageEffect(
    int Turns,
    bool SourceRequired = false,
    bool UniquePerSlot = false) : MoveEffect;

public sealed record DelayedHealEffect(
    int Turns,
    Fraction Fraction,
    DelayedHealBasis Basis = DelayedHealBasis.SourceMaxHp,
    BattleIntentTargetPolicy TargetPolicy = BattleIntentTargetPolicy.LiveSlot,
    bool SourceRequired = false) : MoveEffect;

public sealed record DelayedStatusEffect(
    int Turns,
    PersistentStatus Status,
    BattleIntentTargetPolicy TargetPolicy = BattleIntentTargetPolicy.SnapshotSlot,
    bool SourceRequired = false) : MoveEffect;

public sealed record ReplacementRestoreEffect(
    bool RestoreHp = true,
    bool CureStatus = true,
    bool RestorePp = false) : MoveEffect;

public static class BattleChargeMechanics
{
    public static void Validate(ChargeMoveEffect? charge, IReadOnlyList<MoveEffect> effects)
    {
        ArgumentNullException.ThrowIfNull(effects);
        if (effects.OfType<ChargeMoveEffect>().Any())
            throw new ArgumentException("Charge metadata belongs on the move, not its resolved effect list.");
        if (charge is { } metadata
            && (metadata.State is { } state && !Enum.IsDefined(state)
                || metadata.TargetPolicy is not (BattleIntentTargetPolicy.LiveSlot
                    or BattleIntentTargetPolicy.SnapshotSlot)))
            throw new ArgumentException("Charge metadata requires a defined state and live/snapshot target policy.");

        ChargeStartStatEffect[] startStats = effects.OfType<ChargeStartStatEffect>().ToArray();
        if ((charge is null && startStats.Length > 0)
            || startStats.Any(effect => !Enum.IsDefined(effect.Stat) || effect.Delta is < -6 or > 6 or 0)
            || startStats.Select(effect => effect.Stat).Distinct().Count() != startStats.Length)
            throw new ArgumentException("Charge-start stats require one valid nonzero row per stat on a charge move.");

        SemiInvulnerableHitEffect[] hits = effects.OfType<SemiInvulnerableHitEffect>().ToArray();
        if (hits.Length > 1 || hits.Any(effect => effect.States.Count == 0
            || effect.States.Any(state => !Enum.IsDefined(state))
            || effect.PowerMultiplier is { Num: <= 0 } or { Den: <= 0 }))
            throw new ArgumentException("Semi-invulnerable hit rows require unique defined states and positive power.");
    }
}

public static class BattleDelayedMechanics
{
    public const int MaxTurns = 16;

    public static void Validate(IReadOnlyList<MoveEffect> effects, DamageClass damageClass,
        int? power, MoveTarget target, bool selfDestruct, bool hasLegacyPowerFormula = false)
    {
        ArgumentNullException.ThrowIfNull(effects);
        DelayedDamageEffect[] damage = effects.OfType<DelayedDamageEffect>().ToArray();
        DelayedHealEffect[] healing = effects.OfType<DelayedHealEffect>().ToArray();
        DelayedStatusEffect[] statuses = effects.OfType<DelayedStatusEffect>().ToArray();
        ReplacementRestoreEffect[] replacements = effects.OfType<ReplacementRestoreEffect>().ToArray();
        if (damage.Length > 1 || healing.Length > 1 || statuses.Length > 1 || replacements.Length > 1)
            throw new ArgumentException("Each delayed effect op may appear at most once.");
        if (damage.Length + healing.Length + statuses.Length + replacements.Length > 1)
            throw new ArgumentException("A move may author only one delayed payload family.");

        if (damage.SingleOrDefault() is { } delayedDamage
            && (damageClass == DamageClass.Status || power is not > 0 || target is not (MoveTarget.Selected
                or MoveTarget.SelectedPokemonMeFirst) || !ValidTurns(delayedDamage.Turns)
                || hasLegacyPowerFormula || effects.Any(IsUnsupportedDelayedPowerEffect)))
            throw new ArgumentException("Delayed damage requires a selected fixed-power damaging move and 1..16 turns.");
        if (healing.SingleOrDefault() is { } delayedHeal
            && (!ValidTurns(delayedHeal.Turns) || delayedHeal.Fraction.Num <= 0
                || delayedHeal.Fraction.Den <= 0 || !Enum.IsDefined(delayedHeal.Basis)
                || delayedHeal.TargetPolicy is not (BattleIntentTargetPolicy.LiveSlot
                    or BattleIntentTargetPolicy.SnapshotSlot)
                || target is not (MoveTarget.User or MoveTarget.Selected)))
            throw new ArgumentException("Delayed healing requires a valid fraction, slot policy, target, and 1..16 turns.");
        if (statuses.SingleOrDefault() is { } delayedStatus
            && (!ValidTurns(delayedStatus.Turns) || !Enum.IsDefined(delayedStatus.Status)
                || delayedStatus.TargetPolicy is not (BattleIntentTargetPolicy.LiveSlot
                    or BattleIntentTargetPolicy.SnapshotSlot)
                || target is not (MoveTarget.User or MoveTarget.Selected)))
            throw new ArgumentException("Delayed status requires a valid status, slot policy, target, and 1..16 turns.");
        if (replacements.SingleOrDefault() is { } replacement
            && (!selfDestruct || target != MoveTarget.User
                || !replacement.RestoreHp && !replacement.CureStatus && !replacement.RestorePp))
            throw new ArgumentException("Replacement restore requires self-destruction, a self target, and a selected resource.");
    }

    private static bool ValidTurns(int turns) => turns is >= 1 and <= MaxTurns;

    private static bool IsUnsupportedDelayedPowerEffect(MoveEffect effect) => effect is
        StatusPowerEffect or StatusCountPowerEffect or SpeedRatioPowerEffect
        or MetricBandPowerEffect or MetricRatioPowerEffect
        or ConsecutivePowerEffect or HistoryPowerEffect or PartyCountPowerEffect or FriendshipPowerEffect
        or PpPowerEffect or PositiveStagePowerEffect or ItemDataPowerEffect or RandomTablePowerEffect
        or WeatherMoveEffect or TerrainMoveEffect or EffectivenessQueryEffect
        || effect is MoveQueryModifierEffect { Query: BattleQueryId.BasePower };
}
