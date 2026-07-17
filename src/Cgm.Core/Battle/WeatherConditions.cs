using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum Weather { None, Rain, Sun, Sandstorm, Hail, Snow }

/// <summary>
/// A weather as a data-defined field condition (EFFECT_TYPES_CATALOG §7.6): its hook parameters —
/// on_turn_end residual, numeric query modifiers, and on_status_attempt denials. One table row per
/// weather; new weather is data, not branches.
/// </summary>
public sealed record WeatherDef
{
    public required Weather Weather { get; init; }
    public BattleConditionDefinition? Definition { get; init; }
    public int ResidualDenominator { get; init; }                 // on_turn_end chip: maxHp / denom (0 = none)
    public IReadOnlyList<string> ResidualImmuneTypes { get; init; } = [];
    public string? BoostedMoveType { get; init; }                 // on_damage_query ×1.5
    public string? WeakenedMoveType { get; init; }                // on_damage_query ×0.5
    public IReadOnlyList<PersistentStatus> BlockedStatuses { get; init; } = [];
    public IReadOnlyList<string> Rulesets { get; init; } =
        [BattleRulesets.Gen4Like, BattleRulesets.ModernReference];
    public EntityId? BoostedStatType { get; init; }
    public StatKind? BoostedStat { get; init; }
}

/// <summary>The weather condition definitions and typed hook payloads.</summary>
public static class WeatherConditions
{
    public const int DefaultTurns = 5;
    private static readonly BattleConditionOwner Owner = new(BattleConditionScope.Weather);

    private static readonly IReadOnlyDictionary<Weather, WeatherDef> Registry =
        new WeatherDef[]
        {
            new() { Weather = Weather.None },
            new() { Weather = Weather.Rain, Definition = Condition("rain", BattleConditionHook.AccuracyQuery,
                        BattleConditionHook.ChargeStart, BattleConditionHook.MoveTypeQuery,
                        BattleConditionHook.BasePowerQuery, BattleConditionHook.DamageQuery,
                        BattleConditionHook.HealingQuery),
                    BoostedMoveType = "water", WeakenedMoveType = "fire" },
            new() { Weather = Weather.Sun, Definition = Condition("sun", BattleConditionHook.AccuracyQuery,
                        BattleConditionHook.ChargeStart, BattleConditionHook.MoveTypeQuery,
                        BattleConditionHook.BasePowerQuery, BattleConditionHook.DamageQuery, BattleConditionHook.HealingQuery,
                        BattleConditionHook.StatusAttempt),
                    BoostedMoveType = "fire", WeakenedMoveType = "water",
                    BlockedStatuses = Array.AsReadOnly([PersistentStatus.Freeze]) },
            new() { Weather = Weather.Sandstorm, Definition = Condition("sandstorm", BattleConditionHook.HealingQuery,
                        BattleConditionHook.ChargeStart, BattleConditionHook.MoveTypeQuery,
                        BattleConditionHook.BasePowerQuery, BattleConditionHook.StatQuery,
                        BattleConditionHook.TurnEnd),
                    ResidualDenominator = 16,
                    ResidualImmuneTypes = ["rock", "ground", "steel"],
                    BoostedStatType = EntityId.Parse("type:rock"), BoostedStat = StatKind.Spd },
            new() { Weather = Weather.Hail, Definition = Condition("hail", BattleConditionHook.AccuracyQuery,
                        BattleConditionHook.ChargeStart, BattleConditionHook.MoveTypeQuery, BattleConditionHook.BasePowerQuery,
                        BattleConditionHook.HealingQuery, BattleConditionHook.TurnEnd),
                    ResidualDenominator = 16, ResidualImmuneTypes = ["ice"],
                    Rulesets = [BattleRulesets.Gen4Like] },
            new() { Weather = Weather.Snow, Definition = Condition("snow", BattleConditionHook.AccuracyQuery,
                        BattleConditionHook.ChargeStart, BattleConditionHook.MoveTypeQuery,
                        BattleConditionHook.BasePowerQuery, BattleConditionHook.HealingQuery,
                        BattleConditionHook.StatQuery),
                    BoostedStatType = EntityId.Parse("type:ice"), BoostedStat = StatKind.Def,
                    Rulesets = [BattleRulesets.ModernReference] },
        }.ToDictionary(d => d.Weather);

    private static readonly IReadOnlyDictionary<BattleConditionId, WeatherDef> ByCondition = Registry.Values
        .Where(definition => definition.Definition is not null)
        .ToDictionary(definition => definition.Definition!.Id);

    public static IReadOnlyList<BattleConditionDefinition> Definitions { get; } = Array.AsReadOnly(Registry.Values
        .Where(definition => definition.Definition is not null)
        .Select(definition => definition.Definition!)
        .ToArray());

    public static WeatherDef For(Weather weather) => Registry[weather];
    public static WeatherDef For(BattleConditionId condition) => ByCondition[condition];
    public static BattleConditionOwner FieldOwner => Owner;

    public static bool Supports(Weather weather, string ruleset) =>
        BattleRulesets.IsSupported(ruleset) && For(weather).Rulesets.Contains(ruleset, StringComparer.Ordinal);

    public static BattleHookDispatchSnapshot CollectDamageHooks(
        IEnumerable<BattleConditionInstance> conditions, string moveTypeSlug, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentException.ThrowIfNullOrEmpty(moveTypeSlug);
        var registrations = new List<BattleHookRegistration>();
        foreach (BattleConditionInstance condition in conditions.Where(instance =>
            instance.Definition.Scope == BattleConditionScope.Weather))
        {
            WeatherDef weather = For(condition.Definition.Id);
            BattleQueryValue? operand = moveTypeSlug == weather.BoostedMoveType ? new(3, 2)
                : moveTypeSlug == weather.WeakenedMoveType ? new(1, 2)
                : null;
            if (operand is null)
                continue;
            registrations.Add(BattleHookRegistration.ForCondition(condition, BattleConditionHook.DamageQuery,
                priority: 0, payloadIndex: 0,
                new BattleHookQueryModifier(BattleQueryId.FinalDamage,
                    new BattleQueryModifier(BattleQueryStage.Hooks, BattleQueryOperation.Multiply,
                        operand.Value, OwnerScope: BattleQueryOwnerScope.Field))));
        }
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.DamageQuery), registrations);
    }

    public static BattleHookDispatchSnapshot CollectAccuracyHooks(
        IEnumerable<BattleConditionInstance> conditions, WeatherAccuracyEffect effect, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(effect);
        BattleConditionInstance? condition = conditions.SingleOrDefault(instance =>
            instance.Definition.Scope == BattleConditionScope.Weather);
        if (condition is null)
            return BattleHookDispatcher.Collect(
                new BattleHookDispatchContext(actionSequence, BattleConditionHook.AccuracyQuery), []);

        Weather weather = For(condition.Definition.Id).Weather;
        var registrations = new List<BattleHookRegistration>();
        if (effect.BypassWeather.Contains(weather))
        {
            registrations.Add(BattleHookRegistration.ForCondition(condition, BattleConditionHook.AccuracyQuery,
                priority: 0, payloadIndex: 0,
                new BattleHookQueryModifier(BattleQueryId.Accuracy,
                    new BattleQueryModifier(BattleQueryStage.SourceTargetState, BattleQueryOperation.Replace,
                        new BattleQueryValue(100), OwnerScope: BattleQueryOwnerScope.Field))));
            registrations.Add(BattleHookRegistration.ForCondition(condition, BattleConditionHook.AccuracyQuery,
                priority: 0, payloadIndex: 1,
                new BattleHookFilter(new BattleHookFilterId("accuracy_bypass"), BattleHookFilterDecision.Allow)));
        }
        else if (effect.AccuracyOverrides.TryGetValue(weather, out int accuracy))
        {
            registrations.Add(BattleHookRegistration.ForCondition(condition, BattleConditionHook.AccuracyQuery,
                priority: 0, payloadIndex: 0,
                new BattleHookQueryModifier(BattleQueryId.Accuracy,
                    new BattleQueryModifier(BattleQueryStage.SourceTargetState, BattleQueryOperation.Replace,
                        new BattleQueryValue(accuracy), OwnerScope: BattleQueryOwnerScope.Field))));
        }
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.AccuracyQuery), registrations);
    }

    public static BattleHookDispatchSnapshot CollectMoveTypeHooks(
        IEnumerable<BattleConditionInstance> conditions, WeatherMoveEffect effect, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(effect);
        BattleConditionInstance? condition = Active(conditions);
        BattleHookRegistration[] registrations = condition is not null
            && effect.TypeOverrides.TryGetValue(For(condition.Definition.Id).Weather, out EntityId type)
            ? [BattleHookRegistration.ForCondition(condition, BattleConditionHook.MoveTypeQuery, 0, 0,
                new BattleHookMoveType(type))]
            : [];
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.MoveTypeQuery), registrations);
    }

    public static BattleHookDispatchSnapshot CollectBasePowerHooks(
        IEnumerable<BattleConditionInstance> conditions, WeatherMoveEffect effect, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(effect);
        BattleConditionInstance? condition = Active(conditions);
        BattleHookRegistration[] registrations = condition is not null
            && effect.PowerMultipliers.TryGetValue(For(condition.Definition.Id).Weather, out Fraction fraction)
            ? [BattleHookRegistration.ForCondition(condition, BattleConditionHook.BasePowerQuery, 0, 0,
                new BattleHookQueryModifier(BattleQueryId.BasePower,
                    new BattleQueryModifier(BattleQueryStage.Hooks, BattleQueryOperation.Multiply,
                        new BattleQueryValue(fraction.Num, fraction.Den), OwnerScope: BattleQueryOwnerScope.Field)))]
            : [];
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.BasePowerQuery), registrations);
    }

    public static BattleHookDispatchSnapshot CollectChargeHooks(
        IEnumerable<BattleConditionInstance> conditions, WeatherMoveEffect effect, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(effect);
        BattleConditionInstance? condition = Active(conditions);
        BattleHookRegistration[] registrations = condition is not null
            && effect.SkipChargeWeather.Contains(For(condition.Definition.Id).Weather)
            ? [BattleHookRegistration.ForCondition(condition, BattleConditionHook.ChargeStart, 0, 0,
                new BattleHookFilter(new BattleHookFilterId("charge_required"), BattleHookFilterDecision.Deny))]
            : [];
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.ChargeStart), registrations);
    }

    public static BattleHookDispatchSnapshot CollectStatHooks(
        IEnumerable<BattleConditionInstance> conditions, IReadOnlyList<EntityId> ownerTypes,
        StatKind stat, BattleQueryId query, string ruleset, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(ownerTypes);
        if (query is not (BattleQueryId.OffensiveStat or BattleQueryId.DefensiveStat))
            throw new ArgumentOutOfRangeException(nameof(query), query, "Weather stat hooks require a stat query.");
        if (!BattleRulesets.IsSupported(ruleset))
            throw new ArgumentException("Unknown battle ruleset profile.", nameof(ruleset));

        BattleConditionInstance? condition = Active(conditions);
        WeatherDef? weather = condition is null ? null : For(condition.Definition.Id);
        BattleHookRegistration[] registrations = weather is not null
            && weather.Rulesets.Contains(ruleset, StringComparer.Ordinal)
            && weather.BoostedStat == stat
            && weather.BoostedStatType is { } type
            && ownerTypes.Contains(type)
            ? [BattleHookRegistration.ForCondition(condition!, BattleConditionHook.StatQuery, 0, 0,
                new BattleHookQueryModifier(query,
                    new BattleQueryModifier(BattleQueryStage.Hooks, BattleQueryOperation.Multiply,
                        new BattleQueryValue(3, 2), OwnerScope: BattleQueryOwnerScope.Field)))]
            : [];
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.StatQuery), registrations);
    }

    public static BattleHookDispatchSnapshot CollectStatusHooks(
        IEnumerable<BattleConditionInstance> conditions, PersistentStatus status, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown persistent status.");
        BattleConditionInstance? condition = conditions.SingleOrDefault(instance =>
            instance.Definition.Scope == BattleConditionScope.Weather);
        BattleHookRegistration[] registrations = condition is not null
            && For(condition.Definition.Id).BlockedStatuses.Contains(status)
            ? [BattleHookRegistration.ForCondition(condition, BattleConditionHook.StatusAttempt,
                priority: 0, payloadIndex: 0,
                new BattleHookFilter(new BattleHookFilterId("status_attempt"), BattleHookFilterDecision.Deny))]
            : [];
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.StatusAttempt), registrations);
    }

    public static BattleHookDispatchSnapshot CollectHealingHooks(
        IEnumerable<BattleConditionInstance> conditions, HealEffect effect, int maxHp, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(effect);
        if (maxHp <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHp), "Maximum HP must be positive.");
        BattleConditionInstance? condition = conditions.SingleOrDefault(instance =>
            instance.Definition.Scope == BattleConditionScope.Weather);
        BattleHookRegistration[] registrations = condition is not null
            && effect.WeatherFractions?.TryGetValue(For(condition.Definition.Id).Weather, out Fraction fraction) == true
            ? [BattleHookRegistration.ForCondition(condition, BattleConditionHook.HealingQuery,
                priority: 0, payloadIndex: 0,
                new BattleHookQueryModifier(BattleQueryId.Healing,
                    new BattleQueryModifier(BattleQueryStage.Hooks, BattleQueryOperation.Replace,
                        new BattleQueryValue(EffectMath.HealAmount(maxHp, fraction.Num, fraction.Den)),
                        OwnerScope: BattleQueryOwnerScope.Field)))]
            : [];
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.HealingQuery), registrations);
    }

    private static BattleConditionDefinition Condition(string slug, params BattleConditionHook[] hooks) => new()
    {
        Id = new BattleConditionId($"weather:{slug}"),
        Scope = BattleConditionScope.Weather,
        Hooks = hooks,
        DefaultDuration = DefaultTurns,
        DurationCheckpoint = BattleIntentCheckpoint.TurnEnd,
        Tags = [slug],
        StackingKey = "weather",
        StackingPolicy = BattleConditionStackingPolicy.Replace,
        SwitchPolicy = BattleConditionSwitchPolicy.StayScope,
        FaintPolicy = BattleConditionFaintPolicy.Persist,
    };

    private static BattleConditionInstance? Active(IEnumerable<BattleConditionInstance> conditions) =>
        conditions.SingleOrDefault(instance => instance.Definition.Scope == BattleConditionScope.Weather);
}
