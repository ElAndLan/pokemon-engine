using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattleSideCondition { PhysicalScreen, SpecialScreen, AllDamageScreen }

public static class SideConditions
{
    public const int DefaultTurns = 5;

    private static readonly IReadOnlyDictionary<BattleSideCondition, BattleConditionDefinition> Rows =
        new Dictionary<BattleSideCondition, BattleConditionDefinition>
        {
            [BattleSideCondition.PhysicalScreen] = Screen("side:physical_screen", "side_physical_screen"),
            [BattleSideCondition.SpecialScreen] = Screen("side:special_screen", "side_special_screen"),
            [BattleSideCondition.AllDamageScreen] = Screen("side:all_damage_screen", "side_all_damage_screen"),
        };

    public static IReadOnlyList<BattleConditionDefinition> Definitions { get; } = [.. Rows.Values];
    public static BattleConditionDefinition For(BattleSideCondition condition) => Rows[condition];
    public static BattleConditionOwner Owner(BattleSide side) => new(BattleConditionScope.Side, side);

    public static bool Active(IEnumerable<BattleConditionInstance> conditions, BattleSide side,
        BattleSideCondition condition) => conditions.Any(instance =>
            instance.Owner == Owner(side) && instance.Definition.Id == For(condition).Id);

    public static BattleHookDispatchSnapshot CollectDamageHooks(
        IEnumerable<BattleConditionInstance> conditions,
        BattleSide targetSide,
        DamageClass damageClass,
        int activeSlotsPerSide,
        bool critical,
        bool bypass,
        int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        if (!Enum.IsDefined(targetSide))
            throw new ArgumentOutOfRangeException(nameof(targetSide));
        if (damageClass == DamageClass.Status || critical || bypass)
            return Empty(actionSequence);
        if (activeSlotsPerSide is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(activeSlotsPerSide), "Side screens support singles or doubles topology.");

        BattleQueryValue multiplier = activeSlotsPerSide == 1
            ? new BattleQueryValue(1, 2)
            : new BattleQueryValue(2, 3);
        var registrations = new List<BattleHookRegistration>();
        foreach (BattleConditionInstance instance in conditions.Where(instance =>
            instance.Owner == Owner(targetSide) && Applies(instance, damageClass)))
        {
            registrations.Add(BattleHookRegistration.ForCondition(instance, BattleConditionHook.DamageQuery,
                0, registrations.Count, new BattleHookQueryModifier(BattleQueryId.FinalDamage,
                    new BattleQueryModifier(BattleQueryStage.Hooks, BattleQueryOperation.Multiply, multiplier,
                        OwnerScope: BattleQueryOwnerScope.TargetSide))));
        }
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.DamageQuery), registrations);
    }

    private static bool Applies(BattleConditionInstance instance, DamageClass damageClass) =>
        instance.Definition.Id == For(BattleSideCondition.AllDamageScreen).Id
        || damageClass == DamageClass.Physical
            && instance.Definition.Id == For(BattleSideCondition.PhysicalScreen).Id
        || damageClass == DamageClass.Special
            && instance.Definition.Id == For(BattleSideCondition.SpecialScreen).Id;

    private static BattleHookDispatchSnapshot Empty(int actionSequence) => BattleHookDispatcher.Collect(
        new BattleHookDispatchContext(actionSequence, BattleConditionHook.DamageQuery), []);

    private static BattleConditionDefinition Screen(string id, string stackingKey) => new()
    {
        Id = new BattleConditionId(id),
        Scope = BattleConditionScope.Side,
        Hooks = [BattleConditionHook.DamageQuery],
        DefaultDuration = DefaultTurns,
        DurationCheckpoint = BattleIntentCheckpoint.TurnEnd,
        Tags = ["screen"],
        StackingKey = stackingKey,
        StackingPolicy = BattleConditionStackingPolicy.Reject,
        SwitchPolicy = BattleConditionSwitchPolicy.StayScope,
        FaintPolicy = BattleConditionFaintPolicy.Persist,
    };
}
