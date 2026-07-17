using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattleSideCondition
{
    PhysicalScreen,
    SpecialScreen,
    AllDamageScreen,
    StatusGuard,
    StageDropGuard,
    SpeedBoost,
}

public static class SideConditions
{
    public const int DefaultTurns = 5;

    private static readonly IReadOnlyDictionary<BattleSideCondition, BattleConditionDefinition> Rows =
        new Dictionary<BattleSideCondition, BattleConditionDefinition>
        {
            [BattleSideCondition.PhysicalScreen] = Screen("side:physical_screen", "side_physical_screen"),
            [BattleSideCondition.SpecialScreen] = Screen("side:special_screen", "side_special_screen"),
            [BattleSideCondition.AllDamageScreen] = Screen("side:all_damage_screen", "side_all_damage_screen"),
            [BattleSideCondition.StatusGuard] = Guard("side:status_guard", "side_status_guard",
                BattleConditionHook.StatusAttempt, "status_guard"),
            [BattleSideCondition.StageDropGuard] = Guard("side:stage_drop_guard", "side_stage_drop_guard",
                BattleConditionHook.SecondaryEffect, "stage_guard"),
            [BattleSideCondition.SpeedBoost] = Speed("side:speed_boost", "side_speed_boost"),
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
            return Empty(BattleConditionHook.DamageQuery, actionSequence);
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

    public static BattleHookDispatchSnapshot CollectStatusHooks(
        IEnumerable<BattleConditionInstance> conditions, BattleSide sourceSide, BattleSide targetSide,
        bool bypass, int actionSequence) => CollectGuardHooks(conditions, sourceSide, targetSide,
            BattleSideCondition.StatusGuard, BattleConditionHook.StatusAttempt, "status_attempt", bypass,
            actionSequence);

    public static BattleHookDispatchSnapshot CollectConfusionHooks(
        IEnumerable<BattleConditionInstance> conditions, BattleSide sourceSide, BattleSide targetSide,
        bool bypass, int actionSequence) => CollectGuardHooks(conditions, sourceSide, targetSide,
            BattleSideCondition.StatusGuard, BattleConditionHook.StatusAttempt, "confusion_attempt", bypass,
            actionSequence);

    public static BattleHookDispatchSnapshot CollectStageDropHooks(
        IEnumerable<BattleConditionInstance> conditions, BattleSide sourceSide, BattleSide targetSide,
        bool bypass, int actionSequence) => CollectGuardHooks(conditions, sourceSide, targetSide,
            BattleSideCondition.StageDropGuard, BattleConditionHook.SecondaryEffect, "stage_drop_attempt", bypass,
            actionSequence);

    public static BattleHookDispatchSnapshot CollectSpeedHooks(
        IEnumerable<BattleConditionInstance> conditions, BattleSide side, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        if (!Enum.IsDefined(side))
            throw new ArgumentOutOfRangeException(nameof(side));
        BattleConditionInstance? instance = conditions.SingleOrDefault(item =>
            item.Owner == Owner(side) && item.Definition.Id == For(BattleSideCondition.SpeedBoost).Id);
        BattleHookRegistration[] registrations = instance is null ? []
            : [BattleHookRegistration.ForCondition(instance, BattleConditionHook.StatQuery, 0, 0,
                new BattleHookQueryModifier(BattleQueryId.Speed,
                    new BattleQueryModifier(BattleQueryStage.Hooks, BattleQueryOperation.Multiply,
                        new BattleQueryValue(2), OwnerScope: BattleQueryOwnerScope.SourceSide)))];
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.StatQuery), registrations);
    }

    private static bool Applies(BattleConditionInstance instance, DamageClass damageClass) =>
        instance.Definition.Id == For(BattleSideCondition.AllDamageScreen).Id
        || damageClass == DamageClass.Physical
            && instance.Definition.Id == For(BattleSideCondition.PhysicalScreen).Id
        || damageClass == DamageClass.Special
            && instance.Definition.Id == For(BattleSideCondition.SpecialScreen).Id;

    private static BattleHookDispatchSnapshot CollectGuardHooks(
        IEnumerable<BattleConditionInstance> conditions, BattleSide sourceSide, BattleSide targetSide,
        BattleSideCondition condition, BattleConditionHook hook, string filterId, bool bypass, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        if (!Enum.IsDefined(sourceSide) || !Enum.IsDefined(targetSide))
            throw new ArgumentOutOfRangeException(nameof(targetSide));
        if (sourceSide == targetSide || bypass)
            return Empty(hook, actionSequence);
        BattleConditionInstance? instance = conditions.SingleOrDefault(item =>
            item.Owner == Owner(targetSide) && item.Definition.Id == For(condition).Id);
        BattleHookRegistration[] registrations = instance is null ? []
            : [BattleHookRegistration.ForCondition(instance, hook, 0, 0,
                new BattleHookFilter(new BattleHookFilterId(filterId), BattleHookFilterDecision.Deny))];
        return BattleHookDispatcher.Collect(new BattleHookDispatchContext(actionSequence, hook), registrations);
    }

    private static BattleHookDispatchSnapshot Empty(BattleConditionHook hook, int actionSequence) =>
        BattleHookDispatcher.Collect(new BattleHookDispatchContext(actionSequence, hook), []);

    private static BattleConditionDefinition Screen(string id, string stackingKey) => new()
    {
        Id = new BattleConditionId(id),
        Scope = BattleConditionScope.Side,
        Hooks = [BattleConditionHook.DamageQuery],
        DefaultDuration = DefaultTurns,
        DurationCheckpoint = BattleIntentCheckpoint.TurnEnd,
        Tags = ["screen", "barrier"],
        StackingKey = stackingKey,
        StackingPolicy = BattleConditionStackingPolicy.Reject,
        SwitchPolicy = BattleConditionSwitchPolicy.StayScope,
        FaintPolicy = BattleConditionFaintPolicy.Persist,
    };

    private static BattleConditionDefinition Guard(string id, string stackingKey,
        BattleConditionHook hook, string tag) => new()
    {
        Id = new BattleConditionId(id),
        Scope = BattleConditionScope.Side,
        Hooks = [hook],
        DefaultDuration = DefaultTurns,
        DurationCheckpoint = BattleIntentCheckpoint.TurnEnd,
        Tags = [tag, "barrier"],
        StackingKey = stackingKey,
        StackingPolicy = BattleConditionStackingPolicy.Reject,
        SwitchPolicy = BattleConditionSwitchPolicy.StayScope,
        FaintPolicy = BattleConditionFaintPolicy.Persist,
    };

    private static BattleConditionDefinition Speed(string id, string stackingKey) => new()
    {
        Id = new BattleConditionId(id),
        Scope = BattleConditionScope.Side,
        Hooks = [BattleConditionHook.StatQuery],
        DefaultDuration = 4,
        DurationCheckpoint = BattleIntentCheckpoint.TurnEnd,
        Tags = ["speed_order"],
        StackingKey = stackingKey,
        StackingPolicy = BattleConditionStackingPolicy.Reject,
        SwitchPolicy = BattleConditionSwitchPolicy.StayScope,
        FaintPolicy = BattleConditionFaintPolicy.Persist,
    };
}
