using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum Terrain { None, Electric, Grassy, Misty, Psychic }

public enum BattleEnvironment
{
    Building,
    Cave,
    DeepWater,
    Desert,
    Grass,
    Mountain,
    Ocean,
    Pond,
    Road,
    ShallowWater,
    Snow,
    TallGrass,
    ElectricTerrain,
    GrassyTerrain,
    MistyTerrain,
    PsychicTerrain,
}

public readonly record struct BattleEnvironmentState
{
    private BattleEnvironmentState(BattleEnvironment natural, BattleEnvironment effective)
    {
        Natural = natural;
        Effective = effective;
    }

    public BattleEnvironment Natural { get; }
    public BattleEnvironment Effective { get; }

    public static BattleEnvironmentState Resolve(BattleEnvironment natural, Terrain terrain = Terrain.None)
    {
        if (!TerrainConditions.IsNaturalEnvironment(natural))
            throw new ArgumentOutOfRangeException(nameof(natural), natural,
                "Natural environment must be a known non-terrain value.");
        if (!Enum.IsDefined(terrain))
            throw new ArgumentOutOfRangeException(nameof(terrain), terrain, "Unknown terrain.");
        return new BattleEnvironmentState(natural,
            terrain == Terrain.None ? natural : TerrainConditions.Environment(terrain));
    }

    public static BattleEnvironmentState Resolve(BattleEnvironment natural,
        IEnumerable<BattleConditionInstance>? conditions)
    {
        BattleConditionInstance? condition = conditions?.SingleOrDefault(instance =>
            instance.Definition.Scope == BattleConditionScope.Terrain);
        Terrain terrain = condition is null ? Terrain.None : TerrainConditions.For(condition.Definition.Id).Terrain;
        return Resolve(natural, terrain);
    }
}

public sealed record TerrainDef
{
    public required Terrain Terrain { get; init; }
    public BattleConditionDefinition? Definition { get; init; }
    public string? BoostedMoveType { get; init; }
    public string? WeakenedMoveType { get; init; }
    public IReadOnlyList<PersistentStatus> BlockedStatuses { get; init; } = [];
    public bool BlocksAllStatuses { get; init; }
    public bool BlocksConfusion { get; init; }
    public bool BlocksPriority { get; init; }
    public int HealingDenominator { get; init; }
}

public static class TerrainConditions
{
    public const int DefaultTurns = 5;
    private static readonly BattleConditionOwner Owner = new(BattleConditionScope.Terrain);

    private static readonly IReadOnlyDictionary<Terrain, TerrainDef> Registry = new TerrainDef[]
    {
        new() { Terrain = Terrain.None },
        new()
        {
            Terrain = Terrain.Electric,
            Definition = Condition("electric", BattleConditionHook.DamageQuery, BattleConditionHook.StatusAttempt),
            BoostedMoveType = "electric",
            BlockedStatuses = Array.AsReadOnly([PersistentStatus.Sleep]),
        },
        new()
        {
            Terrain = Terrain.Grassy,
            Definition = Condition("grassy", BattleConditionHook.DamageQuery, BattleConditionHook.TurnEnd),
            BoostedMoveType = "grass",
            HealingDenominator = 16,
        },
        new()
        {
            Terrain = Terrain.Misty,
            Definition = Condition("misty", BattleConditionHook.DamageQuery, BattleConditionHook.StatusAttempt),
            WeakenedMoveType = "dragon",
            BlocksAllStatuses = true,
            BlocksConfusion = true,
        },
        new()
        {
            Terrain = Terrain.Psychic,
            Definition = Condition("psychic", BattleConditionHook.PriorityQuery, BattleConditionHook.TryHit,
                BattleConditionHook.DamageQuery),
            BoostedMoveType = "psychic",
            BlocksPriority = true,
        },
    }.ToDictionary(definition => definition.Terrain);

    private static readonly IReadOnlyDictionary<BattleConditionId, TerrainDef> ByCondition = Registry.Values
        .Where(definition => definition.Definition is not null)
        .ToDictionary(definition => definition.Definition!.Id);

    public static IReadOnlyList<BattleConditionDefinition> Definitions { get; } = Array.AsReadOnly(Registry.Values
        .Where(definition => definition.Definition is not null)
        .Select(definition => definition.Definition!)
        .ToArray());

    public static TerrainDef For(Terrain terrain) => Registry[terrain];
    public static TerrainDef For(BattleConditionId condition) => ByCondition[condition];
    public static BattleConditionOwner FieldOwner => Owner;
    public static bool IsNaturalEnvironment(BattleEnvironment environment) => environment is
        BattleEnvironment.Building or BattleEnvironment.Cave or BattleEnvironment.DeepWater
        or BattleEnvironment.Desert or BattleEnvironment.Grass or BattleEnvironment.Mountain
        or BattleEnvironment.Ocean or BattleEnvironment.Pond or BattleEnvironment.Road
        or BattleEnvironment.ShallowWater or BattleEnvironment.Snow or BattleEnvironment.TallGrass;
    public static bool Supports(Terrain terrain, string ruleset) =>
        Enum.IsDefined(terrain) && BattleRulesets.IsSupported(ruleset)
        && (terrain == Terrain.None || ruleset == BattleRulesets.ModernReference);

    public static BattleQueryResult GroundedQuery(BattleCreature creature,
        IEnumerable<BattleQueryModifier>? modifiers = null, BattleQueryContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(creature);
        int intrinsic = creature.Types.Any(type => type.Slug == "flying") ? 0 : 1;
        return BattleQuery.Evaluate(BattleQueryId.Grounded, new BattleQueryValue(intrinsic), modifiers,
            context ?? new BattleQueryContext(Source: creature));
    }

    public static BattleHookDispatchSnapshot CollectDamageHooks(IEnumerable<BattleConditionInstance> conditions,
        string moveTypeSlug, bool sourceGrounded, bool targetGrounded, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentException.ThrowIfNullOrEmpty(moveTypeSlug);
        BattleConditionInstance? condition = Active(conditions);
        TerrainDef? terrain = condition is null ? null : For(condition.Definition.Id);
        BattleQueryValue? operand = terrain is null ? null
            : sourceGrounded && moveTypeSlug == terrain.BoostedMoveType ? new BattleQueryValue(3, 2)
            : targetGrounded && moveTypeSlug == terrain.WeakenedMoveType ? new BattleQueryValue(1, 2)
            : null;
        BattleHookRegistration[] registrations = operand is null
            ? []
            : [BattleHookRegistration.ForCondition(condition!, BattleConditionHook.DamageQuery, 0, 0,
                new BattleHookQueryModifier(BattleQueryId.FinalDamage,
                    new BattleQueryModifier(BattleQueryStage.Hooks, BattleQueryOperation.Multiply,
                        operand.Value, OwnerScope: BattleQueryOwnerScope.Field)))];
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.DamageQuery), registrations);
    }

    public static BattleHookDispatchSnapshot CollectStatusHooks(IEnumerable<BattleConditionInstance> conditions,
        PersistentStatus status, bool targetGrounded, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        if (!Enum.IsDefined(status))
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown persistent status.");
        BattleConditionInstance? condition = Active(conditions);
        TerrainDef? terrain = condition is null ? null : For(condition.Definition.Id);
        bool denied = targetGrounded && terrain is not null
            && (terrain.BlocksAllStatuses || terrain.BlockedStatuses.Contains(status));
        return FilterSnapshot(condition, denied, BattleConditionHook.StatusAttempt, "status_attempt", actionSequence);
    }

    public static BattleHookDispatchSnapshot CollectConfusionHooks(IEnumerable<BattleConditionInstance> conditions,
        bool targetGrounded, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        BattleConditionInstance? condition = Active(conditions);
        bool denied = targetGrounded && condition is not null && For(condition.Definition.Id).BlocksConfusion;
        return FilterSnapshot(condition, denied, BattleConditionHook.StatusAttempt, "confusion_attempt", actionSequence);
    }

    public static BattleHookDispatchSnapshot CollectPriorityHooks(IEnumerable<BattleConditionInstance> conditions,
        int priority, bool targetGrounded, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        BattleConditionInstance? condition = Active(conditions);
        bool denied = priority > 0 && targetGrounded && condition is not null
            && For(condition.Definition.Id).BlocksPriority;
        return FilterSnapshot(condition, denied, BattleConditionHook.TryHit, "priority_hit", actionSequence);
    }

    public static BattleHookDispatchSnapshot CollectMoveTypeHooks(IEnumerable<BattleConditionInstance> conditions,
        TerrainMoveEffect effect, bool sourceGrounded, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(effect);
        BattleConditionInstance? condition = Active(conditions);
        Terrain terrain = condition is null ? Terrain.None : For(condition.Definition.Id).Terrain;
        bool eligible = effect.Subject == TerrainMoveSubject.Field
            || effect.Subject == TerrainMoveSubject.User && sourceGrounded;
        BattleHookRegistration[] registrations = condition is not null && eligible
            && effect.TypeOverrides.TryGetValue(terrain, out EntityId type)
            ? [BattleHookRegistration.ForCondition(condition, BattleConditionHook.MoveTypeQuery, 0, 0,
                new BattleHookMoveType(type))]
            : [];
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.MoveTypeQuery), registrations);
    }

    public static BattleHookDispatchSnapshot CollectBasePowerHooks(IEnumerable<BattleConditionInstance> conditions,
        TerrainMoveEffect effect, bool sourceGrounded, bool targetGrounded, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(effect);
        BattleConditionInstance? condition = Active(conditions);
        Terrain terrain = condition is null ? Terrain.None : For(condition.Definition.Id).Terrain;
        bool eligible = effect.Subject switch
        {
            TerrainMoveSubject.Field => true,
            TerrainMoveSubject.User => sourceGrounded,
            TerrainMoveSubject.Target => targetGrounded,
            _ => throw new ArgumentOutOfRangeException(nameof(effect), effect.Subject, "Unknown terrain subject."),
        };
        BattleHookRegistration[] registrations = condition is not null && eligible
            && effect.PowerMultipliers.TryGetValue(terrain, out Fraction fraction)
            ? [BattleHookRegistration.ForCondition(condition, BattleConditionHook.BasePowerQuery, 0, 0,
                new BattleHookQueryModifier(BattleQueryId.BasePower,
                    new BattleQueryModifier(BattleQueryStage.Hooks, BattleQueryOperation.Multiply,
                        new BattleQueryValue(fraction.Num, fraction.Den), OwnerScope: BattleQueryOwnerScope.Field)))]
            : [];
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.BasePowerQuery), registrations);
    }

    public static BattleHookDispatchSnapshot CollectMovePriorityHooks(IEnumerable<BattleConditionInstance> conditions,
        TerrainMoveEffect effect, bool sourceGrounded, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(effect);
        BattleConditionInstance? condition = Active(conditions);
        Terrain terrain = condition is null ? Terrain.None : For(condition.Definition.Id).Terrain;
        bool eligible = effect.Subject == TerrainMoveSubject.Field
            || effect.Subject == TerrainMoveSubject.User && sourceGrounded;
        BattleHookRegistration[] registrations = condition is not null && eligible
            && effect.PriorityModifiers.TryGetValue(terrain, out int modifier)
            ? [BattleHookRegistration.ForCondition(condition, BattleConditionHook.PriorityQuery, 0, 0,
                new BattleHookQueryModifier(BattleQueryId.Priority,
                    new BattleQueryModifier(BattleQueryStage.Hooks, BattleQueryOperation.Add,
                        new BattleQueryValue(modifier), OwnerScope: BattleQueryOwnerScope.Field)))]
            : [];
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.PriorityQuery), registrations);
    }

    public static BattleHookDispatchSnapshot CollectHealingHooks(IEnumerable<BattleConditionInstance> conditions,
        HealEffect effect, int maxHp, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(effect);
        if (maxHp <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHp), "Maximum HP must be positive.");
        BattleConditionInstance? condition = Active(conditions);
        Terrain terrain = condition is null ? Terrain.None : For(condition.Definition.Id).Terrain;
        BattleHookRegistration[] registrations = condition is not null
            && effect.TerrainFractions?.TryGetValue(terrain, out Fraction fraction) == true
            ? [BattleHookRegistration.ForCondition(condition, BattleConditionHook.HealingQuery, 0, 0,
                new BattleHookQueryModifier(BattleQueryId.Healing,
                    new BattleQueryModifier(BattleQueryStage.Hooks, BattleQueryOperation.Replace,
                        new BattleQueryValue(EffectMath.HealAmount(maxHp, fraction.Num, fraction.Den)),
                        OwnerScope: BattleQueryOwnerScope.Field)))]
            : [];
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.HealingQuery), registrations);
    }

    public static bool Spreads(IEnumerable<BattleConditionInstance> conditions, TerrainMoveEffect effect,
        bool sourceGrounded)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(effect);
        BattleConditionInstance? condition = Active(conditions);
        bool eligible = effect.Subject == TerrainMoveSubject.Field
            || effect.Subject == TerrainMoveSubject.User && sourceGrounded;
        return condition is not null && eligible
            && effect.SpreadTerrains.Contains(For(condition.Definition.Id).Terrain);
    }

    public static BattleEnvironment Environment(Terrain terrain) => terrain switch
    {
        Terrain.Electric => BattleEnvironment.ElectricTerrain,
        Terrain.Grassy => BattleEnvironment.GrassyTerrain,
        Terrain.Misty => BattleEnvironment.MistyTerrain,
        Terrain.Psychic => BattleEnvironment.PsychicTerrain,
        _ => throw new ArgumentOutOfRangeException(nameof(terrain), terrain, "Clear terrain has no terrain environment."),
    };

    public static bool CanBlockPriorityTarget(MoveTarget target) => target is
        MoveTarget.Selected or MoveTarget.AllOpponents or MoveTarget.AllOtherPokemon
        or MoveTarget.RandomOpponent or MoveTarget.SelectedPokemonMeFirst;

    private static BattleHookDispatchSnapshot FilterSnapshot(BattleConditionInstance? condition, bool denied,
        BattleConditionHook hook, string filter, int actionSequence)
    {
        BattleHookRegistration[] registrations = condition is not null && denied
            ? [BattleHookRegistration.ForCondition(condition, hook, 0, 0,
                new BattleHookFilter(new BattleHookFilterId(filter), BattleHookFilterDecision.Deny))]
            : [];
        return BattleHookDispatcher.Collect(new BattleHookDispatchContext(actionSequence, hook), registrations);
    }

    private static BattleConditionDefinition Condition(string slug, params BattleConditionHook[] hooks) => new()
    {
        Id = new BattleConditionId($"terrain:{slug}"),
        Scope = BattleConditionScope.Terrain,
        Hooks = hooks.Concat([
            BattleConditionHook.MoveTypeQuery,
            BattleConditionHook.BasePowerQuery,
            BattleConditionHook.PriorityQuery,
            BattleConditionHook.HealingQuery,
        ]).Distinct().ToArray(),
        DefaultDuration = DefaultTurns,
        DurationCheckpoint = BattleIntentCheckpoint.TurnEnd,
        Tags = [slug],
        StackingKey = "terrain",
        StackingPolicy = BattleConditionStackingPolicy.Replace,
        SwitchPolicy = BattleConditionSwitchPolicy.StayScope,
        FaintPolicy = BattleConditionFaintPolicy.Persist,
    };

    private static BattleConditionInstance? Active(IEnumerable<BattleConditionInstance> conditions) =>
        conditions.SingleOrDefault(instance => instance.Definition.Scope == BattleConditionScope.Terrain);
}
