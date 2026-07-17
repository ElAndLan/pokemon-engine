using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattleFieldCondition { TrickRoom, WonderRoom, MagicRoom, Gravity, MudSport, WaterSport }

public static class FieldConditions
{
    public const int DefaultTurns = 5;
    public static readonly BattleConditionOwner FieldOwner = new(BattleConditionScope.Field);
    public static readonly BattleConditionOwner RoomOwner = new(BattleConditionScope.Room);

    private static readonly IReadOnlyDictionary<BattleFieldCondition, BattleConditionDefinition> Rows =
        new Dictionary<BattleFieldCondition, BattleConditionDefinition>
        {
            [BattleFieldCondition.TrickRoom] = Timed("room:trick", BattleConditionScope.Room, "room_trick",
                BattleConditionHook.TurnOrderQuery),
            [BattleFieldCondition.WonderRoom] = Timed("room:wonder", BattleConditionScope.Room, "room_wonder",
                BattleConditionHook.StatQuery),
            [BattleFieldCondition.MagicRoom] = Timed("room:magic", BattleConditionScope.Room, "room_magic",
                BattleConditionHook.ItemTrigger),
            [BattleFieldCondition.Gravity] = Timed("field:gravity", BattleConditionScope.Field, "field_gravity",
                BattleConditionHook.GroundedQuery, BattleConditionHook.AccuracyQuery,
                BattleConditionHook.MoveAvailability),
            [BattleFieldCondition.MudSport] = Timed("field:mud_sport", BattleConditionScope.Field, "field_mud_sport",
                BattleConditionHook.BasePowerQuery),
            [BattleFieldCondition.WaterSport] = Timed("field:water_sport", BattleConditionScope.Field, "field_water_sport",
                BattleConditionHook.BasePowerQuery),
        };
    private static readonly IReadOnlyDictionary<BattleFieldCondition, BattleConditionDefinition> ClassicSports =
        new Dictionary<BattleFieldCondition, BattleConditionDefinition>
        {
            [BattleFieldCondition.MudSport] = SourceBound("field:mud_sport_classic", "field_mud_sport_classic"),
            [BattleFieldCondition.WaterSport] = SourceBound("field:water_sport_classic", "field_water_sport_classic"),
        };

    public static IReadOnlyList<BattleConditionDefinition> Definitions { get; } =
        [.. Rows.Values, .. ClassicSports.Values];
    public static BattleConditionDefinition For(BattleFieldCondition condition) => Rows[condition];
    public static BattleConditionDefinition For(BattleFieldCondition condition, string ruleset) =>
        ruleset == BattleRulesets.Gen4Like && ClassicSports.TryGetValue(condition, out BattleConditionDefinition? row)
            ? row : For(condition);
    public static BattleFieldCondition For(BattleConditionId id) => Rows.Concat(ClassicSports)
        .Single(row => row.Value.Id == id).Key;

    public static bool Active(IEnumerable<BattleConditionInstance> conditions, BattleFieldCondition condition) =>
        conditions.Any(instance => Matches(instance, condition));

    public static StatKind DefensiveStat(IEnumerable<BattleConditionInstance> conditions, StatKind authored) =>
        !Active(conditions, BattleFieldCondition.WonderRoom) ? authored : authored switch
        {
            StatKind.Def => StatKind.Spd,
            StatKind.Spd => StatKind.Def,
            _ => authored,
        };

    public static BattleHookDispatchSnapshot CollectAccuracyHooks(
        IEnumerable<BattleConditionInstance> conditions, int actionSequence) =>
        QueryHook(conditions, BattleFieldCondition.Gravity, BattleConditionHook.AccuracyQuery,
            BattleQueryId.Accuracy, new BattleQueryValue(5, 3), actionSequence);

    public static BattleHookDispatchSnapshot CollectBasePowerHooks(IEnumerable<BattleConditionInstance> conditions,
        string moveType, string ruleset, int actionSequence)
    {
        var registrations = new List<BattleHookRegistration>();
        AddSport(BattleFieldCondition.MudSport, "electric");
        AddSport(BattleFieldCondition.WaterSport, "fire");
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.BasePowerQuery), registrations);

        void AddSport(BattleFieldCondition condition, string affectedType)
        {
            if (!string.Equals(moveType, affectedType, StringComparison.OrdinalIgnoreCase)
                || !Active(conditions, condition))
                return;
            BattleQueryValue multiplier = ruleset == BattleRulesets.Gen4Like
                ? new BattleQueryValue(1, 2)
                : new BattleQueryValue(1, 3);
            BattleConditionInstance instance = conditions.Single(item => Matches(item, condition));
            registrations.Add(BattleHookRegistration.ForCondition(instance, BattleConditionHook.BasePowerQuery,
                0, registrations.Count, new BattleHookQueryModifier(BattleQueryId.BasePower,
                    new BattleQueryModifier(BattleQueryStage.Hooks, BattleQueryOperation.Multiply, multiplier,
                        OwnerScope: BattleQueryOwnerScope.Field))));
        }
    }

    private static BattleHookDispatchSnapshot QueryHook(IEnumerable<BattleConditionInstance> conditions,
        BattleFieldCondition condition, BattleConditionHook hook, BattleQueryId query, BattleQueryValue value,
        int actionSequence)
    {
        BattleConditionInstance? instance = conditions.SingleOrDefault(item => Matches(item, condition));
        BattleHookRegistration[] registrations = instance is null ? []
            : [BattleHookRegistration.ForCondition(instance, hook, 0, 0,
                new BattleHookQueryModifier(query, new BattleQueryModifier(BattleQueryStage.Hooks,
                    BattleQueryOperation.Multiply, value, OwnerScope: BattleQueryOwnerScope.Field)))];
        return BattleHookDispatcher.Collect(new BattleHookDispatchContext(actionSequence, hook), registrations);
    }

    private static BattleConditionDefinition Timed(string id, BattleConditionScope scope, string stackingKey,
        params BattleConditionHook[] hooks) => new()
    {
        Id = new BattleConditionId(id),
        Scope = scope,
        Hooks = hooks,
        DefaultDuration = DefaultTurns,
        DurationCheckpoint = BattleIntentCheckpoint.TurnEnd,
        StackingKey = stackingKey,
        StackingPolicy = BattleConditionStackingPolicy.Reject,
        SwitchPolicy = BattleConditionSwitchPolicy.StayScope,
        FaintPolicy = BattleConditionFaintPolicy.Persist,
    };

    private static bool Matches(BattleConditionInstance instance, BattleFieldCondition condition) =>
        instance.Definition.Id == For(condition).Id
        || ClassicSports.TryGetValue(condition, out BattleConditionDefinition? classic)
            && instance.Definition.Id == classic.Id;

    private static BattleConditionDefinition SourceBound(string id, string stackingKey) => new()
    {
        Id = new BattleConditionId(id),
        Scope = BattleConditionScope.Field,
        Hooks = [BattleConditionHook.BasePowerQuery],
        Tags = ["source_bound"],
        StackingKey = stackingKey,
        StackingPolicy = BattleConditionStackingPolicy.Reject,
        SwitchPolicy = BattleConditionSwitchPolicy.StayScope,
        FaintPolicy = BattleConditionFaintPolicy.Persist,
    };
}
