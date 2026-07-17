namespace Cgm.Core.Battle;

public sealed record BattleAccuracyQueryResult(BattleQueryResult Query, bool Bypass);

public static class BattleActionQueries
{
    private static readonly HashSet<BattleQueryId> Allowed =
    [
        BattleQueryId.Accuracy,
        BattleQueryId.CriticalChance,
        BattleQueryId.Priority,
        BattleQueryId.FinalDamage,
        BattleQueryId.Healing,
    ];

    public static BattleAccuracyQueryResult Accuracy(BattleMove move, int authored,
        BattleCreature source, BattleCreature target, bool externalBypass, bool guaranteed,
        IEnumerable<BattleQueryModifier>? externalModifiers, BattleQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context);
        AccuracyQueryEffect? rule = move.SecondaryEffects.OfType<AccuracyQueryEffect>().SingleOrDefault();
        if (rule is not null && !Enum.IsDefined(rule.Mode))
            throw new ArgumentOutOfRangeException(nameof(move), rule.Mode, "Unknown accuracy-query mode.");
        bool bypass = externalBypass || guaranteed || rule?.Mode == AccuracyQueryMode.Bypass;
        var modifiers = new List<BattleQueryModifier>();
        if (guaranteed)
            modifiers.Add(new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Replace,
                new BattleQueryValue(100), InsertionOrder: 0));
        if (!bypass)
        {
            int evasion = rule?.Mode == AccuracyQueryMode.IgnoreTargetEvasion
                ? 0 : target.Stage(StatKind.Evasion);
            modifiers.Add(new BattleQueryModifier(BattleQueryStage.SourceTargetState,
                BattleQueryOperation.Multiply,
                BattleQuery.AccuracyStageMultiplier(source.Stage(StatKind.Accuracy), evasion),
                InsertionOrder: modifiers.Count));
        }
        Append(modifiers, externalModifiers);
        Append(modifiers, MoveModifiers(move, BattleQueryId.Accuracy));
        BattleQueryResult query = BattleQuery.Evaluate(BattleQueryId.Accuracy,
            new BattleQueryValue(authored), modifiers, context);
        return new BattleAccuracyQueryResult(query,
            bypass && (!guaranteed || query.FinalValue == new BattleQueryValue(100)));
    }

    public static BattleQueryResult CriticalChance(BattleMove move, int stage,
        bool guaranteed, IEnumerable<BattleQueryModifier>? externalModifiers,
        BattleQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(context);
        var modifiers = new List<BattleQueryModifier>();
        if (guaranteed)
            modifiers.Add(new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Replace,
                new BattleQueryValue(1), InsertionOrder: 0));
        Append(modifiers, MoveModifiers(move, BattleQueryId.CriticalChance));
        Append(modifiers, externalModifiers);
        return BattleQuery.Evaluate(BattleQueryId.CriticalChance, BattleRolls.CritChanceValue(stage),
            modifiers, context);
    }

    public static BattleQueryResult Priority(BattleMove move,
        IEnumerable<BattleQueryModifier>? externalModifiers, BattleQueryContext context) =>
        EvaluateInteger(move, BattleQueryId.Priority, move.Priority, externalModifiers, context);

    public static BattleQueryResult FinalDamage(BattleMove move, int damage,
        IEnumerable<BattleQueryModifier>? externalModifiers, BattleQueryContext context) =>
        EvaluateInteger(move, BattleQueryId.FinalDamage, damage, externalModifiers, context);

    public static BattleQueryResult Healing(BattleMove move, int amount,
        IEnumerable<BattleQueryModifier>? externalModifiers, BattleQueryContext context) =>
        EvaluateInteger(move, BattleQueryId.Healing, amount, externalModifiers, context);

    public static IReadOnlyList<BattleQueryModifier> MoveModifiers(BattleMove move, BattleQueryId query)
    {
        ArgumentNullException.ThrowIfNull(move);
        if (!Allowed.Contains(query))
            throw new ArgumentOutOfRangeException(nameof(query), query, "Query is not move-modifiable in 15C-7.");
        return move.SecondaryEffects.OfType<MoveQueryModifierEffect>()
            .Where(effect => effect.Query == query)
            .Select((effect, index) => Modifier(effect, index))
            .ToArray();
    }

    public static void Validate(MoveQueryModifierEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if (!Allowed.Contains(effect.Query))
            throw new ArgumentOutOfRangeException(nameof(effect), effect.Query,
                "Query is not move-modifiable in 15C-7.");
        if (!Enum.IsDefined(effect.Operation))
            throw new ArgumentOutOfRangeException(nameof(effect), effect.Operation,
                "Unknown query-modifier operation.");
        if (!effect.Operand.IsValid)
            throw new ArgumentException("Query-modifier denominators must be positive.", nameof(effect));
        if (effect.Query != BattleQueryId.CriticalChance
            && effect.Operation != BattleQueryOperation.Multiply && !effect.Operand.IsInteger)
            throw new ArgumentException("Only multiply accepts a fractional integer-query operand.", nameof(effect));
        if (effect.Operation == BattleQueryOperation.Multiply && effect.Operand.Numerator < 0)
            throw new ArgumentOutOfRangeException(nameof(effect), "Query multipliers cannot be negative.");
        if (effect.Query is BattleQueryId.FinalDamage or BattleQueryId.Healing
            && effect.Operand.Numerator < 0)
            throw new ArgumentOutOfRangeException(nameof(effect),
                "Damage and healing query operands cannot be negative.");
    }

    private static BattleQueryResult EvaluateInteger(BattleMove move, BattleQueryId query, int authored,
        IEnumerable<BattleQueryModifier>? externalModifiers, BattleQueryContext context)
    {
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(context);
        var modifiers = new List<BattleQueryModifier>();
        Append(modifiers, MoveModifiers(move, query));
        Append(modifiers, externalModifiers);
        return BattleQuery.Evaluate(query, new BattleQueryValue(authored), modifiers, context);
    }

    private static BattleQueryModifier Modifier(MoveQueryModifierEffect effect, int insertion)
    {
        Validate(effect);
        return new BattleQueryModifier(BattleQueryStage.Hooks, effect.Operation, effect.Operand,
            OwnerScope: BattleQueryOwnerScope.Source, InsertionOrder: insertion);
    }

    private static void Append(List<BattleQueryModifier> target,
        IEnumerable<BattleQueryModifier>? source)
    {
        foreach (BattleQueryModifier modifier in source ?? [])
            target.Add(modifier with { InsertionOrder = target.Count });
    }
}

public static class OneShotQueryConditions
{
    public static readonly BattleConditionDefinition Accuracy = Definition(
        "query:next_accuracy", BattleConditionHook.AccuracyQuery, "next_accuracy", sourceBound: true);
    public static readonly BattleConditionDefinition CriticalChance = Definition(
        "query:next_critical", BattleConditionHook.CriticalQuery, "next_critical", sourceBound: false);
    public static IReadOnlyList<BattleConditionDefinition> Definitions => [Accuracy, CriticalChance];

    public static BattleConditionDefinition For(OneShotQuery query) => query switch
    {
        OneShotQuery.Accuracy => Accuracy,
        OneShotQuery.CriticalChance => CriticalChance,
        _ => throw new ArgumentOutOfRangeException(nameof(query), query, "Unknown one-shot query."),
    };

    public static BattleConditionInstance? FindAccuracy(
        IEnumerable<BattleConditionInstance> conditions, BattleConditionOwner targetOwner,
        BattleConditionSource source) => conditions.SingleOrDefault(instance =>
            instance.Definition.Id == Accuracy.Id && instance.Owner == targetOwner
            && SameSource(instance.Source, source));

    public static BattleConditionInstance? FindCritical(
        IEnumerable<BattleConditionInstance> conditions, BattleConditionOwner sourceOwner) =>
        conditions.SingleOrDefault(instance =>
            instance.Definition.Id == CriticalChance.Id && instance.Owner == sourceOwner);

    private static BattleConditionDefinition Definition(string id, BattleConditionHook hook,
        string key, bool sourceBound) => new()
    {
        Id = new BattleConditionId(id),
        Scope = BattleConditionScope.Creature,
        Hooks = [hook],
        DefaultDuration = 2,
        DurationCheckpoint = BattleIntentCheckpoint.TurnEnd,
        Tags = sourceBound ? ["query_condition", "one_shot", "source_bound"]
            : ["query_condition", "one_shot"],
        StackingKey = key,
        StackingPolicy = BattleConditionStackingPolicy.Replace,
        SwitchPolicy = BattleConditionSwitchPolicy.Remove,
        FaintPolicy = BattleConditionFaintPolicy.Remove,
    };

    private static bool SameSource(BattleConditionSource left, BattleConditionSource right) =>
        left.PartyIndex == right.PartyIndex && left.Slot?.Side == right.Slot?.Side;
}
