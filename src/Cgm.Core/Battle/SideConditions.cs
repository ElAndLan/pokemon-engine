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
    CriticalGuard,
    SpeedReduction,
    ResidualDamage,
    SecondaryChanceBoost,
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
            [BattleSideCondition.CriticalGuard] = Critical("side:critical_guard", "side_critical_guard"),
            [BattleSideCondition.SpeedReduction] = Combo("side:speed_reduction", "side_speed_reduction",
                BattleConditionHook.StatQuery, "speed_reduction"),
            [BattleSideCondition.ResidualDamage] = Combo("side:residual_damage", "side_residual_damage",
                BattleConditionHook.TurnEnd, "residual_damage"),
            [BattleSideCondition.SecondaryChanceBoost] = Combo("side:secondary_chance_boost",
                "side_secondary_chance_boost", BattleConditionHook.SecondaryEffect, "secondary_chance"),
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
        var registrations = new List<BattleHookRegistration>();
        foreach (BattleConditionInstance instance in conditions.Where(item => item.Owner == Owner(side)))
        {
            (BattleQueryValue Multiplier, int Priority)? modifier =
                instance.Definition.Id == For(BattleSideCondition.SpeedBoost).Id
                    ? (new BattleQueryValue(2), 10)
                    : instance.Definition.Id == For(BattleSideCondition.SpeedReduction).Id
                        ? (new BattleQueryValue(1, 4), 0)
                        : null;
            if (modifier is not { } value)
                continue;
            registrations.Add(BattleHookRegistration.ForCondition(instance, BattleConditionHook.StatQuery,
                value.Priority, 0, new BattleHookQueryModifier(BattleQueryId.Speed,
                    new BattleQueryModifier(BattleQueryStage.Hooks, BattleQueryOperation.Multiply,
                        value.Multiplier, OwnerScope: BattleQueryOwnerScope.SourceSide))));
        }
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.StatQuery), registrations);
    }

    public static BattleHookDispatchSnapshot CollectSecondaryChanceHooks(
        IEnumerable<BattleConditionInstance> conditions, BattleSide sourceSide, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        if (!Enum.IsDefined(sourceSide))
            throw new ArgumentOutOfRangeException(nameof(sourceSide));
        BattleConditionInstance? instance = conditions.SingleOrDefault(item =>
            item.Owner == Owner(sourceSide)
            && item.Definition.Id == For(BattleSideCondition.SecondaryChanceBoost).Id);
        BattleHookRegistration[] registrations = instance is null ? []
            : [BattleHookRegistration.ForCondition(instance, BattleConditionHook.SecondaryEffect, 0, 0,
                new BattleHookQueryModifier(BattleQueryId.SecondaryChance,
                    new BattleQueryModifier(BattleQueryStage.Hooks, BattleQueryOperation.Multiply,
                        new BattleQueryValue(2), OwnerScope: BattleQueryOwnerScope.SourceSide)))];
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.SecondaryEffect), registrations);
    }

    public static BattleHookDispatchSnapshot CollectCriticalHooks(
        IEnumerable<BattleConditionInstance> conditions, BattleSide sourceSide, BattleSide targetSide,
        int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        if (!Enum.IsDefined(sourceSide) || !Enum.IsDefined(targetSide))
            throw new ArgumentOutOfRangeException(nameof(targetSide));
        BattleConditionInstance? instance = sourceSide == targetSide ? null : conditions.SingleOrDefault(item =>
            item.Owner == Owner(targetSide) && item.Definition.Id == For(BattleSideCondition.CriticalGuard).Id);
        BattleHookRegistration[] registrations = instance is null ? []
            : [BattleHookRegistration.ForCondition(instance, BattleConditionHook.CriticalQuery, 0, 0,
                new BattleHookQueryModifier(BattleQueryId.CriticalChance,
                    new BattleQueryModifier(BattleQueryStage.Hooks, BattleQueryOperation.Min,
                        new BattleQueryValue(0), OwnerScope: BattleQueryOwnerScope.TargetSide)))];
        return BattleHookDispatcher.Collect(
            new BattleHookDispatchContext(actionSequence, BattleConditionHook.CriticalQuery), registrations);
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

    private static BattleConditionDefinition Critical(string id, string stackingKey) => new()
    {
        Id = new BattleConditionId(id),
        Scope = BattleConditionScope.Side,
        Hooks = [BattleConditionHook.CriticalQuery],
        DefaultDuration = DefaultTurns,
        DurationCheckpoint = BattleIntentCheckpoint.TurnEnd,
        Tags = ["critical_guard"],
        StackingKey = stackingKey,
        StackingPolicy = BattleConditionStackingPolicy.Reject,
        SwitchPolicy = BattleConditionSwitchPolicy.StayScope,
        FaintPolicy = BattleConditionFaintPolicy.Persist,
    };

    private static BattleConditionDefinition Combo(string id, string stackingKey,
        BattleConditionHook hook, string tag) => new()
    {
        Id = new BattleConditionId(id),
        Scope = BattleConditionScope.Side,
        Hooks = [hook],
        DefaultDuration = 4,
        DurationCheckpoint = BattleIntentCheckpoint.TurnEnd,
        Tags = ["combo_effect", tag],
        StackingKey = stackingKey,
        StackingPolicy = BattleConditionStackingPolicy.Reject,
        SwitchPolicy = BattleConditionSwitchPolicy.StayScope,
        FaintPolicy = BattleConditionFaintPolicy.Persist,
    };
}
