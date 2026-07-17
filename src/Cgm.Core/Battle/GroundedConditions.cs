using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum GroundedState { Airborne, Grounded }
public enum GroundedStateScope { Target, Field }

public static class GroundedConditions
{
    public const int DefaultTurns = 5;
    private const string StateCounter = "state";

    public static readonly BattleConditionDefinition FieldGrounded = Definition(
        "field:grounded", BattleConditionScope.Field, "field_grounded", GroundedState.Grounded,
        BattleConditionSwitchPolicy.StayScope, BattleConditionFaintPolicy.Persist);
    public static readonly BattleConditionDefinition CreatureGrounded = Definition(
        "grounded:forced", BattleConditionScope.Creature, "grounded_state", GroundedState.Grounded,
        BattleConditionSwitchPolicy.Remove, BattleConditionFaintPolicy.Remove);
    public static readonly BattleConditionDefinition CreatureAirborne = Definition(
        "grounded:airborne", BattleConditionScope.Creature, "grounded_state", GroundedState.Airborne,
        BattleConditionSwitchPolicy.Remove, BattleConditionFaintPolicy.Remove);

    public static IReadOnlyList<BattleConditionDefinition> Definitions { get; } =
        [FieldGrounded, CreatureGrounded, CreatureAirborne];

    public static BattleConditionDefinition For(GroundedState state, GroundedStateScope scope) => scope switch
    {
        GroundedStateScope.Field when state == GroundedState.Grounded => FieldGrounded,
        GroundedStateScope.Target when state == GroundedState.Grounded => CreatureGrounded,
        GroundedStateScope.Target when state == GroundedState.Airborne => CreatureAirborne,
        _ => throw new ArgumentException("Field grounded state cannot be airborne."),
    };

    public static BattleQueryResult Query(BattleCreature creature, IReadOnlyList<EntityId> effectiveTypes,
        BattleConditionOwner owner, IEnumerable<BattleConditionInstance>? conditions = null,
        BattleQueryContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(creature);
        ArgumentNullException.ThrowIfNull(effectiveTypes);
        if (owner.Scope != BattleConditionScope.Creature || owner.Side is null || owner.PartyIndex is null)
            throw new ArgumentException("Grounded queries require a creature owner identity.", nameof(owner));

        var modifiers = new List<BattleQueryModifier>();
        int insertion = 0;
        foreach (BattleConditionInstance condition in conditions ?? [])
        {
            if (!condition.Definition.Hooks.Contains(BattleConditionHook.GroundedQuery)
                || condition.Definition.Scope == BattleConditionScope.Creature
                    && (condition.Owner.Side != owner.Side || condition.Owner.PartyIndex != owner.PartyIndex))
                continue;
            AddModifier(modifiers, State(condition), condition.Definition.Scope == BattleConditionScope.Field,
                ref insertion);
        }

        foreach (Effect effect in creature.AbilityHooks
            .Where(hook => hook.Hook == AbilityHookPoint.OnGroundedQuery)
            .SelectMany(hook => hook.Effects)
            .Concat(creature.HeldItemBattleEffects)
            .Where(effect => effect.Op == "groundedModify"))
            AddModifier(modifiers, ParseState(effect), field: false, ref insertion);

        int intrinsic = effectiveTypes.Any(type => type.Slug == "flying") ? 0 : 1;
        return BattleQuery.Evaluate(BattleQueryId.Grounded, new BattleQueryValue(intrinsic), modifiers, context);
    }

    private static void AddModifier(List<BattleQueryModifier> modifiers, GroundedState state, bool field,
        ref int insertion) => modifiers.Add(new BattleQueryModifier(
            BattleQueryStage.Hooks,
            BattleQueryOperation.Replace,
            new BattleQueryValue(state == GroundedState.Grounded ? 1 : 0),
            field ? 400 : state == GroundedState.Grounded ? 300 : 200,
            field ? BattleQueryOwnerScope.Field : BattleQueryOwnerScope.Target,
            insertion++));

    private static GroundedState State(BattleConditionInstance condition) =>
        condition.Counters.TryGetValue(StateCounter, out int value) && value is 0 or 1
            ? value == 1 ? GroundedState.Grounded : GroundedState.Airborne
            : throw new InvalidOperationException($"Condition '{condition.Definition.Id}' has invalid grounded state.");

    private static GroundedState ParseState(Effect effect)
    {
        if (effect.Params is null || !effect.Params.TryGetValue("state", out var value))
            throw new InvalidOperationException("groundedModify requires a validated state.");
        return value.GetString() switch
        {
            "grounded" => GroundedState.Grounded,
            "airborne" => GroundedState.Airborne,
            _ => throw new InvalidOperationException("groundedModify requires a validated state."),
        };
    }

    private static BattleConditionDefinition Definition(string id, BattleConditionScope scope, string stackingKey,
        GroundedState state, BattleConditionSwitchPolicy switchPolicy, BattleConditionFaintPolicy faintPolicy) => new()
    {
        Id = new BattleConditionId(id),
        Scope = scope,
        Hooks = [BattleConditionHook.GroundedQuery],
        DefaultDuration = DefaultTurns,
        DurationCheckpoint = BattleIntentCheckpoint.TurnEnd,
        InitialCounters = new Dictionary<string, int> { [StateCounter] = state == GroundedState.Grounded ? 1 : 0 },
        StackingKey = stackingKey,
        StackingPolicy = BattleConditionStackingPolicy.Replace,
        SwitchPolicy = switchPolicy,
        FaintPolicy = faintPolicy,
    };
}
