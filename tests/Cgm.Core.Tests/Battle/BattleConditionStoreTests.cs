using System.Text.Json;
using Cgm.Core.Battle;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleConditionStoreTests
{
    private static readonly BattleSlot Player0 = new(BattleSide.Player, 0);
    private static readonly BattleSlot Player1 = new(BattleSide.Player, 1);

    [Fact]
    public void RegistryNormalizesHooksTagsAndCounters()
    {
        BattleConditionDefinition definition = Definition("field:ordered", BattleConditionScope.Field) with
        {
            Hooks = [BattleConditionHook.TurnEnd, BattleConditionHook.SwitchIn],
            Tags = ["zeta", "alpha"],
            InitialCounters = new Dictionary<string, int> { ["zeta"] = 2, ["alpha"] = 1 },
        };

        BattleConditionDefinition normalized = new BattleConditionRegistry([definition]).For(definition.Id);

        Assert.Equal([BattleConditionHook.SwitchIn, BattleConditionHook.TurnEnd], normalized.Hooks);
        Assert.Equal(["alpha", "zeta"], normalized.Tags);
        Assert.Equal(["alpha", "zeta"], normalized.InitialCounters.Keys);
    }

    [Fact]
    public void EveryScopeUsesItsExactStoreAndStableEnumerationOrder()
    {
        BattleConditionDefinition[] definitions =
        [
            Definition("creature:neutral", BattleConditionScope.Creature),
            Definition("slot:neutral", BattleConditionScope.Slot),
            Definition("side:neutral", BattleConditionScope.Side),
            Definition("room:neutral", BattleConditionScope.Room),
            Definition("terrain:neutral", BattleConditionScope.Terrain),
            Definition("weather:neutral", BattleConditionScope.Weather),
            Definition("field:neutral", BattleConditionScope.Field),
        ];
        var stores = new BattleConditionStores(new BattleConditionRegistry(definitions));

        foreach (BattleConditionDefinition definition in definitions)
            stores.Apply(Application(definition.Id, Owner(definition.Scope), turn: 2, action: 4));

        Assert.Equal(Enum.GetValues<BattleConditionScope>(), stores.Snapshot().Select(instance => instance.Definition.Scope));
        Assert.All(definitions, definition => Assert.Single(stores.Snapshot(definition.Scope)));
        Assert.Throws<ArgumentException>(() => stores.Apply(Application(
            new BattleConditionId("field:neutral"), Owner(BattleConditionScope.Side), 2, 4)));
        Assert.Single(stores.Snapshot(BattleConditionScope.Field));
        Assert.Equal(7, stores.EndBattle(3, 0).Events.OfType<ConditionRemoved>().Count());
        Assert.Empty(stores.Snapshot());
    }

    [Fact]
    public void EnumerationUsesOwnerTopologyThenSequenceWithinOneScope()
    {
        BattleConditionDefinition side = Definition("side:order", BattleConditionScope.Side);
        BattleConditionDefinition slot = Definition("slot:order", BattleConditionScope.Slot);
        BattleConditionDefinition creature = Definition("creature:order", BattleConditionScope.Creature);
        var stores = Stores(side, slot, creature);
        stores.Apply(Application(side.Id, new(BattleConditionScope.Side, BattleSide.Enemy), 0, 0));
        stores.Apply(Application(side.Id, new(BattleConditionScope.Side, BattleSide.Player), 0, 1));
        stores.Apply(Application(slot.Id, new(BattleConditionScope.Slot, BattleSide.Player, Player1), 0, 2));
        stores.Apply(Application(slot.Id, new(BattleConditionScope.Slot, BattleSide.Player, Player0), 0, 3));
        stores.Apply(Application(creature.Id, new(BattleConditionScope.Creature, BattleSide.Player, Player0, 2), 0, 4));
        stores.Apply(Application(creature.Id, new(BattleConditionScope.Creature, BattleSide.Player, Player0, 1), 0, 5));

        Assert.Equal([BattleSide.Player, BattleSide.Enemy],
            stores.Snapshot(BattleConditionScope.Side).Select(instance => instance.Owner.Side!.Value));
        Assert.Equal([0, 1], stores.Snapshot(BattleConditionScope.Slot).Select(instance => instance.Owner.Slot!.Value.Position));
        Assert.Equal([1, 2], stores.Snapshot(BattleConditionScope.Creature).Select(instance => instance.Owner.PartyIndex!.Value));

        var inactiveStores = Stores(creature);
        BattleConditionInstance inactive = Assert.Single(inactiveStores.Apply(Application(creature.Id,
            new(BattleConditionScope.Creature, BattleSide.Player, null, 3), 0, 0)).Affected);
        Assert.Null(inactive.Owner.Slot);
    }

    [Fact]
    public void RejectPolicyPreservesExistingAndReportsDuplicate()
    {
        BattleConditionDefinition definition = Definition("side:reject", BattleConditionScope.Side);
        var stores = Stores(definition);
        BattleConditionChangeSet applied = stores.Apply(Application(definition.Id,
            Owner(BattleConditionScope.Side), 1, 2));
        BattleConditionInstance first = Assert.Single(applied.Affected);
        BattleConditionTraceEntry appliedTrace = Assert.Single(applied.Trace);
        Assert.Null(appliedTrace.OwnerBefore);
        Assert.Equal(first.Owner, appliedTrace.OwnerAfter);

        BattleConditionChangeSet duplicate = stores.Apply(Application(definition.Id,
            Owner(BattleConditionScope.Side), 3, 4));

        Assert.Equal(first, Assert.Single(stores.Snapshot()));
        Assert.IsType<ConditionApplicationRejected>(Assert.Single(duplicate.Events));
        Assert.Equal(BattleConditionRejectionReason.Duplicate, Assert.Single(duplicate.Trace).RejectionReason);
    }

    [Fact]
    public void RefreshPolicyChangesOnlyDuration()
    {
        BattleConditionDefinition definition = Definition("side:refresh", BattleConditionScope.Side,
            BattleConditionStackingPolicy.Refresh, duration: 3);
        var stores = Stores(definition);
        BattleConditionSource source = new(Player0, 0);
        BattleConditionInstance first = Assert.Single(stores.Apply(Application(definition.Id,
            Owner(BattleConditionScope.Side), 1, 2, source)).Affected);

        BattleConditionChangeSet refresh = stores.Apply(Application(definition.Id,
            Owner(BattleConditionScope.Side), 9, 8, new(Player1, 1), duration: 5));
        BattleConditionInstance current = Assert.Single(stores.Snapshot());

        Assert.Equal(first.Sequence, current.Sequence);
        Assert.Equal(first.Source, current.Source);
        Assert.Equal(first.AppliedTurn, current.AppliedTurn);
        Assert.Equal(first.AppliedActionSequence, current.AppliedActionSequence);
        Assert.Equal(first.Tags, current.Tags);
        Assert.Equal(first.Counters, current.Counters);
        Assert.Equal(5, current.RemainingDuration);
        Assert.IsType<ConditionRefreshed>(Assert.Single(refresh.Events));
    }

    [Fact]
    public void ReplacePolicyCreatesNewInstanceAcrossOneReplacementFamily()
    {
        BattleConditionDefinition firstDefinition = Definition("weather:first", BattleConditionScope.Weather,
            BattleConditionStackingPolicy.Replace, duration: 5, stackingKey: "weather");
        BattleConditionDefinition secondDefinition = Definition("weather:second", BattleConditionScope.Weather,
            BattleConditionStackingPolicy.Replace, duration: 4, stackingKey: "weather");
        var stores = Stores(firstDefinition, secondDefinition);
        BattleConditionInstance first = Assert.Single(stores.Apply(Application(firstDefinition.Id,
            Owner(BattleConditionScope.Weather), 0, 1)).Affected);

        BattleConditionChangeSet replacement = stores.Apply(Application(secondDefinition.Id,
            Owner(BattleConditionScope.Weather), 2, 3));
        BattleConditionInstance current = Assert.Single(stores.Snapshot());

        Assert.Equal(secondDefinition.Id, current.Definition.Id);
        Assert.True(current.Sequence > first.Sequence);
        ConditionReplaced battleEvent = Assert.IsType<ConditionReplaced>(Assert.Single(replacement.Events));
        Assert.Equal(first.Sequence, battleEvent.ReplacedSequence);
        Assert.Equal(first.Sequence, Assert.Single(replacement.Trace).ReplacedSequence);
    }

    [Fact]
    public void StackPolicyIncrementsOneInstanceAndRejectsAtMaximum()
    {
        BattleConditionDefinition definition = Definition("side:layers", BattleConditionScope.Side,
            BattleConditionStackingPolicy.Stack, maximumStacks: 3);
        var stores = Stores(definition);
        BattleConditionApplication application = Application(definition.Id, Owner(BattleConditionScope.Side), 0, 1);

        BattleConditionInstance first = Assert.Single(stores.Apply(application).Affected);
        stores.Apply(application with { Turn = 1, ActionSequence = 2 });
        BattleConditionChangeSet third = stores.Apply(application with { Turn = 2, ActionSequence = 3 });
        BattleConditionChangeSet capped = stores.Apply(application with { Turn = 3, ActionSequence = 4 });

        BattleConditionInstance current = Assert.Single(stores.Snapshot());
        Assert.Equal(first.Sequence, current.Sequence);
        Assert.Equal(3, current.StackCount);
        Assert.Equal(3, Assert.IsType<ConditionStacked>(Assert.Single(third.Events)).Stacks);
        Assert.Equal(BattleConditionRejectionReason.StackLimit,
            Assert.IsType<ConditionApplicationRejected>(Assert.Single(capped.Events)).Reason);
    }

    [Fact]
    public void DurationOneAndManyExpireAfterExactCheckpointCompletions()
    {
        BattleConditionDefinition one = Definition("field:one", BattleConditionScope.Field,
            duration: 1, stackingKey: "one");
        BattleConditionDefinition many = Definition("field:many", BattleConditionScope.Field,
            duration: 3, stackingKey: "many");
        var stores = Stores(one, many);
        stores.Apply(Application(one.Id, Owner(BattleConditionScope.Field), 0, 0));
        stores.Apply(Application(many.Id, Owner(BattleConditionScope.Field), 0, 1));

        Assert.Empty(stores.CompleteCheckpoint(BattleIntentCheckpoint.BeforeMove, 0, 2).Affected);
        BattleConditionChangeSet firstTick = stores.CompleteCheckpoint(BattleIntentCheckpoint.TurnEnd, 0, 3);
        Assert.Single(firstTick.Events.OfType<ConditionExpired>());
        Assert.Equal(2, Assert.Single(stores.Snapshot()).RemainingDuration);
        stores.CompleteCheckpoint(BattleIntentCheckpoint.TurnEnd, 1, 0);
        BattleConditionChangeSet lastTick = stores.CompleteCheckpoint(BattleIntentCheckpoint.TurnEnd, 2, 0);

        Assert.Single(lastTick.Events.OfType<ConditionExpired>());
        Assert.Null(lastTick.Trace.Last().OwnerAfter);
        Assert.Empty(stores.Snapshot());
        Assert.Equal(5, firstTick.Trace.Count + lastTick.Trace.Count);
    }

    [Fact]
    public void VariableDurationAndSourceIdentityAreValidatedAndPreserved()
    {
        BattleConditionDefinition definition = Definition("slot:variable", BattleConditionScope.Slot) with
        {
            DurationCheckpoint = BattleIntentCheckpoint.TurnEnd,
        };
        var stores = Stores(definition);
        BattleConditionApplication missingDuration = Application(definition.Id,
            Owner(BattleConditionScope.Slot), 0, 0, new(Player0, 2));

        Assert.Throws<ArgumentException>(() => stores.Apply(missingDuration));
        Assert.Throws<ArgumentException>(() => stores.Apply(missingDuration with
        {
            Duration = 2,
            Source = new BattleConditionSource(Player0, null),
        }));

        BattleConditionInstance instance = Assert.Single(stores.Apply(missingDuration with { Duration = 2 }).Affected);
        Assert.Equal(Player0, instance.Source.Slot);
        Assert.Equal(2, instance.Source.PartyIndex);
        Assert.Throws<ArgumentException>(() => stores.Apply(missingDuration with { Duration = 0 }));
        Assert.Single(stores.Snapshot());
    }

    [Fact]
    public void SwitchFaintAndBattleEndCleanupFollowDefinitionPolicies()
    {
        BattleConditionDefinition switchRemove = Definition("creature:switch_remove", BattleConditionScope.Creature,
            stackingKey: "switch_remove", faintPolicy: BattleConditionFaintPolicy.Persist);
        BattleConditionDefinition faintRemove = Definition("creature:faint_remove", BattleConditionScope.Creature,
            stackingKey: "faint_remove", switchPolicy: BattleConditionSwitchPolicy.FollowOwner);
        BattleConditionDefinition persistent = Definition("creature:persistent", BattleConditionScope.Creature,
            stackingKey: "persistent", switchPolicy: BattleConditionSwitchPolicy.FollowOwner,
            faintPolicy: BattleConditionFaintPolicy.Persist);
        BattleConditionDefinition slot = Definition("slot:persistent", BattleConditionScope.Slot);
        BattleConditionDefinition side = Definition("side:persistent", BattleConditionScope.Side);
        BattleConditionDefinition field = Definition("field:persistent", BattleConditionScope.Field);
        var stores = Stores(switchRemove, faintRemove, persistent, slot, side, field);
        BattleConditionOwner creatureOwner = Owner(BattleConditionScope.Creature);
        foreach (BattleConditionDefinition definition in new[] { switchRemove, faintRemove, persistent })
            stores.Apply(Application(definition.Id, creatureOwner, 0, 0));
        stores.Apply(Application(slot.Id, Owner(BattleConditionScope.Slot), 0, 0));
        stores.Apply(Application(side.Id, Owner(BattleConditionScope.Side), 0, 0));
        stores.Apply(Application(field.Id, Owner(BattleConditionScope.Field), 0, 0));

        BattleConditionChangeSet switched = stores.OwnerSwitched(BattleSide.Player, 2, Player1, 1, 0);
        Assert.Single(switched.Events.OfType<ConditionRemoved>());
        Assert.Equal(2, switched.Events.OfType<ConditionTransferred>().Count());
        Assert.All(stores.Snapshot(BattleConditionScope.Creature), instance => Assert.Equal(Player1, instance.Owner.Slot));

        BattleConditionChangeSet fainted = stores.OwnerFainted(BattleSide.Player, 2, 1, 1);
        Assert.Single(fainted.Events.OfType<ConditionRemoved>());
        BattleConditionChangeSet reserve = stores.OwnerSwitched(BattleSide.Player, 2, null, 1, 2);
        Assert.Single(reserve.Events.OfType<ConditionTransferred>());
        Assert.Null(Assert.Single(stores.Snapshot(BattleConditionScope.Creature)).Owner.Slot);

        BattleConditionChangeSet ended = stores.EndBattle(2, 0);
        Assert.Equal(4, ended.Events.OfType<ConditionRemoved>().Count());
        Assert.All(ended.Events.OfType<ConditionRemoved>(), item => Assert.Equal(BattleConditionCleanupReason.BattleEnd, item.Reason));
        Assert.All(ended.Trace, item => Assert.Null(item.OwnerAfter));
        Assert.Empty(stores.Snapshot());
    }

    [Fact]
    public void DefinitionAndApplicationValidationFailBeforeMutation()
    {
        BattleConditionDefinition valid = Definition("field:valid", BattleConditionScope.Field);

        Assert.Throws<ArgumentException>(() => new BattleConditionId("invalid"));
        Assert.Throws<ArgumentException>(() => new BattleConditionId("Field:invalid"));
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid, valid]));
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid with { Id = default }]));
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid with { StackingKey = "Bad" }]));
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid with { DefaultDuration = 0 }]));
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid with { DefaultDuration = 2 }]));
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid with
        {
            DurationCheckpoint = (BattleIntentCheckpoint)99,
        }]));
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid with
        {
            StackingPolicy = BattleConditionStackingPolicy.Refresh,
        }]));
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid with
        {
            StackingPolicy = BattleConditionStackingPolicy.Stack,
        }]));
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid with { MaximumStacks = 2 }]));
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid with
        {
            Hooks = [BattleConditionHook.TurnEnd, BattleConditionHook.TurnEnd],
        }]));
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid with { Hooks = [(BattleConditionHook)99] }]));
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid with { Tags = ["bad-tag"] }]));
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid with { Tags = ["same", "same"] }]));
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid with
        {
            InitialCounters = new Dictionary<string, int> { ["count"] = -1 },
        }]));
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid with
        {
            SwitchPolicy = BattleConditionSwitchPolicy.Remove,
        }]));
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid with
        {
            FaintPolicy = BattleConditionFaintPolicy.Remove,
        }]));

        BattleConditionDefinition replacement = valid with
        {
            Id = new BattleConditionId("field:replacement"),
            StackingPolicy = BattleConditionStackingPolicy.Replace,
        };
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid, replacement]));
        Assert.Throws<ArgumentException>(() => new BattleConditionRegistry([valid, valid with
        {
            Id = new BattleConditionId("field:same_policy"),
        }]));

        var stores = Stores(valid);
        Assert.Throws<KeyNotFoundException>(() => stores.Apply(Application(
            new BattleConditionId("field:missing"), Owner(BattleConditionScope.Field), 0, 0)));
        Assert.Throws<ArgumentException>(() => stores.Apply(Application(valid.Id,
            Owner(BattleConditionScope.Field), 0, 0, new BattleConditionSource(Player0, null))));
        Assert.Throws<ArgumentOutOfRangeException>(() => stores.CompleteCheckpoint((BattleIntentCheckpoint)99, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => stores.CompleteCheckpoint(BattleIntentCheckpoint.TurnEnd, -1, 0));
        Assert.Empty(stores.Snapshot());
    }

    [Fact]
    public void IdenticalScriptsProduceStableSnapshotsEventsAndTrace()
    {
        static (string Snapshot, string Events, string Trace) Replay()
        {
            BattleConditionDefinition field = Definition("field:replay", BattleConditionScope.Field,
                duration: 2, stackingKey: "field_replay");
            BattleConditionDefinition side = Definition("side:replay", BattleConditionScope.Side,
                BattleConditionStackingPolicy.Stack, maximumStacks: 2, stackingKey: "side_replay");
            var stores = Stores(field, side);
            var events = new List<BattleEvent>();
            var trace = new List<BattleConditionTraceEntry>();
            foreach (BattleConditionChangeSet change in new[]
            {
                stores.Apply(Application(side.Id, Owner(BattleConditionScope.Side), 0, 2)),
                stores.Apply(Application(field.Id, Owner(BattleConditionScope.Field), 0, 1)),
                stores.Apply(Application(side.Id, Owner(BattleConditionScope.Side), 0, 3)),
                stores.CompleteCheckpoint(BattleIntentCheckpoint.TurnEnd, 0, 4),
            })
            {
                events.AddRange(change.Events);
                trace.AddRange(change.Trace);
            }
            return (JsonSerializer.Serialize(stores.Snapshot()),
                string.Join('|', events.Select(entry => $"{entry.GetType().Name}:{entry}")),
                JsonSerializer.Serialize(trace));
        }

        var first = Replay();
        var second = Replay();

        Assert.Equal(first, second);
        Assert.Contains("field:replay", first.Snapshot);
    }

    private static BattleConditionStores Stores(params BattleConditionDefinition[] definitions) =>
        new(new BattleConditionRegistry(definitions));

    private static BattleConditionDefinition Definition(string id, BattleConditionScope scope,
        BattleConditionStackingPolicy policy = BattleConditionStackingPolicy.Reject,
        int? duration = null,
        int maximumStacks = 1,
        string? stackingKey = null,
        BattleConditionSwitchPolicy? switchPolicy = null,
        BattleConditionFaintPolicy faintPolicy = BattleConditionFaintPolicy.Remove) => new()
        {
            Id = new BattleConditionId(id),
            Scope = scope,
            Hooks = duration is null ? [] : [BattleConditionHook.TurnEnd],
            DefaultDuration = duration,
            DurationCheckpoint = duration is null ? null : BattleIntentCheckpoint.TurnEnd,
            StackingKey = stackingKey ?? id.Replace(':', '_'),
            StackingPolicy = policy,
            MaximumStacks = maximumStacks,
            SwitchPolicy = switchPolicy ?? (scope == BattleConditionScope.Creature
                ? BattleConditionSwitchPolicy.Remove
                : BattleConditionSwitchPolicy.StayScope),
            FaintPolicy = scope == BattleConditionScope.Creature ? faintPolicy : BattleConditionFaintPolicy.Persist,
        };

    private static BattleConditionOwner Owner(BattleConditionScope scope) => scope switch
    {
        BattleConditionScope.Creature => new(scope, BattleSide.Player, Player0, 2),
        BattleConditionScope.Side => new(scope, BattleSide.Enemy),
        BattleConditionScope.Slot => new(scope, BattleSide.Player, Player1),
        _ => new(scope),
    };

    private static BattleConditionApplication Application(BattleConditionId id, BattleConditionOwner owner,
        int turn, int action, BattleConditionSource? source = null, int? duration = null) =>
        new(id, owner, source ?? new BattleConditionSource(), turn, action, duration);
}
