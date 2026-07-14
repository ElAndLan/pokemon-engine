using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleHookDispatchTests
{
    private static readonly BattleSlot Player0 = new(BattleSide.Player, 0);
    private static readonly BattleSlot Player1 = new(BattleSide.Player, 1);
    private static readonly BattleSlot Enemy0 = new(BattleSide.Enemy, 0);

    [Fact]
    public void CompleteOrderGolden_UsesPriorityScopeTopologySequenceAndPayload()
    {
        BattleHookRegistration[] registrations =
        [
            Filter("low_field", BattleHookScope.Field, new(), priority: -1, sequence: 0),
            Filter("move", BattleHookScope.Move, new(BattleSide.Player, Player0, 0), sequence: 0),
            Filter("item", BattleHookScope.Item, new(BattleSide.Player, Player0, 0), sequence: 0),
            Filter("ability", BattleHookScope.Ability, new(BattleSide.Player, Player0, 0), sequence: 0),
            Filter("creature_enemy", BattleHookScope.Creature, new(BattleSide.Enemy, Enemy0, 0), sequence: 0),
            Filter("creature_player", BattleHookScope.Creature, new(BattleSide.Player, Player0, 2), sequence: 0),
            Filter("slot_enemy", BattleHookScope.Slot, new(BattleSide.Enemy, Enemy0), sequence: 0),
            Filter("slot_player_one", BattleHookScope.Slot, new(BattleSide.Player, Player1), sequence: 0),
            Filter("slot_player_zero", BattleHookScope.Slot, new(BattleSide.Player, Player0), sequence: 0),
            Filter("side_enemy", BattleHookScope.Side, new(BattleSide.Enemy), sequence: 0),
            Filter("side_player", BattleHookScope.Side, new(BattleSide.Player), sequence: 0),
            Filter("field_second", BattleHookScope.Field, new(), sequence: 2),
            Filter("field_first_payload_one", BattleHookScope.Field, new(), sequence: 1, payloadIndex: 1,
                instanceSlug: "field_first"),
            Filter("field_first_payload_zero", BattleHookScope.Field, new(), sequence: 1, payloadIndex: 0,
                instanceSlug: "field_first"),
            Filter("high_move", BattleHookScope.Move, new(BattleSide.Enemy, Enemy0, 0), priority: 5, sequence: 0),
            Filter("wrong_checkpoint", BattleHookScope.Field, new(), checkpoint: BattleConditionHook.BasePowerQuery,
                priority: 99, sequence: 0),
        ];

        BattleHookDispatchSnapshot snapshot = BattleHookDispatcher.Collect(
            new(7, BattleConditionHook.TurnEnd), registrations);

        Assert.Equal(
        [
            "high_move", "field_first_payload_zero", "field_first_payload_one", "field_second",
            "side_player", "side_enemy", "slot_player_zero", "slot_player_one", "slot_enemy",
            "creature_player", "creature_enemy", "ability", "item", "move", "low_field",
        ], snapshot.Filters().Select(filter => filter.Filter.Value).ToArray());
        Assert.All(snapshot.Trace, row => Assert.True(row.Invoked));
    }

    [Fact]
    public void EveryCatalogCheckpointCollectsOnlyItsOwnRegistration()
    {
        foreach (BattleConditionHook checkpoint in Enum.GetValues<BattleConditionHook>())
        {
            BattleHookRegistration[] registrations = Enum.GetValues<BattleConditionHook>()
                .Select((hook, index) => Filter($"hook_{index}", BattleHookScope.Field, new(), hook,
                    sequence: index))
                .ToArray();

            BattleHookDispatchSnapshot snapshot = BattleHookDispatcher.Collect(new(0, checkpoint), registrations);

            Assert.Equal($"hook_{(int)checkpoint}", Assert.Single(snapshot.Filters()).Filter.Value);
        }
    }

    [Fact]
    public void ConditionAddAndRemoveAfterCaptureWaitForNextCheckpoint()
    {
        BattleConditionDefinition first = Definition("field:first");
        BattleConditionDefinition second = Definition("field:second");
        var stores = new BattleConditionStores(new BattleConditionRegistry([first, second]));
        stores.Apply(Application(first.Id, action: 0));
        BattleHookRegistration[] captured = Registrations(stores);

        BattleHookDispatchSnapshot current = BattleHookDispatcher.Collect(
            new(1, BattleConditionHook.TurnEnd), captured);
        stores.EndBattle(0, 2);
        stores.Apply(Application(second.Id, action: 3));

        Assert.Equal("first", Assert.Single(current.Filters()).Filter.Value);
        BattleHookDispatchSnapshot next = BattleHookDispatcher.Collect(
            new(4, BattleConditionHook.TurnEnd), Registrations(stores));
        Assert.Equal("second", Assert.Single(next.Filters()).Filter.Value);
    }

    [Fact]
    public void DuplicateInstancePayloadInvokesOnceAfterCanonicalSort()
    {
        BattleHookRegistration duplicate = Filter("same", BattleHookScope.Field, new(), priority: 10,
            sequence: 0);

        BattleHookDispatchSnapshot snapshot = BattleHookDispatcher.Collect(
            new(3, BattleConditionHook.TurnEnd), [duplicate, duplicate]);

        Assert.Equal("same", Assert.Single(snapshot.Filters()).Filter.Value);
        Assert.Equal([true, false], snapshot.Trace.Select(row => row.Invoked).ToArray());

        BattleHookRegistration conflict = duplicate with
        {
            Payload = new BattleHookFilter(new BattleHookFilterId("conflict"), BattleHookFilterDecision.Allow),
        };
        Assert.Throws<ArgumentException>(() => BattleHookDispatcher.Collect(
            new(3, BattleConditionHook.TurnEnd), [duplicate, conflict]));
    }

    [Fact]
    public void NestedIntentBudgetFailsWholeSnapshotAndEmitsVisibleError()
    {
        BattleHookRegistration[] registrations =
        [
            Intent("first", 0),
            Intent("second", 1),
        ];
        var queue = new BattleIntentQueue();

        BattleHookDispatchSnapshot snapshot = BattleHookDispatcher.Collect(
            new(9, BattleConditionHook.AfterMove, 63), registrations);
        BattleHookCompletion completion = BattleHookDispatcher.Complete(snapshot, queue);

        Assert.False(snapshot.Succeeded);
        Assert.Empty(snapshot.Invocations);
        Assert.Equal(65, snapshot.Failure!.AttemptedCount);
        HookDispatchFailed failure = Assert.IsType<HookDispatchFailed>(Assert.Single(completion.Events));
        Assert.Equal(BattleHookDispatcher.MaximumEmittedIntents, failure.Limit);
        Assert.Empty(completion.EnqueuedIntents);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void SuccessfulCompletionAtomicallyEnqueuesCapturedIntentsOnceAtTail()
    {
        var queue = new BattleIntentQueue();
        BattleHookDispatchSnapshot snapshot = BattleHookDispatcher.Collect(
            new(4, BattleConditionHook.AfterMove, 62), [Intent("later", 1), Intent("first", 0)]);

        Assert.Equal(0, queue.Count);
        Assert.Equal(64, snapshot.EmittedIntentCount);
        BattleHookCompletion completion = BattleHookDispatcher.Complete(snapshot, queue);

        Assert.Equal([0L, 1L], completion.EnqueuedIntents.Select(intent => intent.Sequence).ToArray());
        Assert.Equal(2, queue.Count);
        Assert.Throws<ArgumentException>(() => BattleHookDispatcher.Complete(snapshot, queue));
    }

    [Fact]
    public void ResolverAndAiReadIdenticalCanonicalQueryModifiers()
    {
        BattleHookRegistration[] registrations =
        [
            Query("late", BattleHookScope.Item, new(BattleSide.Player, Player0, 0), 1,
                BattleQueryOperation.Add, new BattleQueryValue(5)),
            Query("early", BattleHookScope.Field, new(), 7,
                BattleQueryOperation.Multiply, new BattleQueryValue(3, 2)),
        ];

        IReadOnlyList<BattleQueryModifier> resolver = BattleHookDispatcher.Collect(
            new(2, BattleConditionHook.BasePowerQuery), registrations).QueryModifiers(BattleQueryId.BasePower);
        IReadOnlyList<BattleQueryModifier> ai = BattleHookDispatcher.Collect(
            new(2, BattleConditionHook.BasePowerQuery), registrations.Reverse()).QueryModifiers(BattleQueryId.BasePower);

        Assert.Equal(resolver, ai);
        Assert.Equal([0, 1], resolver.Select(modifier => modifier.InsertionOrder).ToArray());
        Assert.Equal(
            BattleQuery.Evaluate(BattleQueryId.BasePower, new BattleQueryValue(20), resolver).FinalValue,
            BattleQuery.Evaluate(BattleQueryId.BasePower, new BattleQueryValue(20), ai).FinalValue);
    }

    [Fact]
    public void DictionaryEnumerationCannotChangeDispatchOrTrace()
    {
        BattleHookRegistration first = Filter("first", BattleHookScope.Side, new(BattleSide.Player), sequence: 0);
        BattleHookRegistration second = Filter("second", BattleHookScope.Side, new(BattleSide.Enemy), sequence: 0);
        var forward = new Dictionary<string, BattleHookRegistration> { ["a"] = first, ["b"] = second };
        var reverse = new Dictionary<string, BattleHookRegistration> { ["b"] = second, ["a"] = first };

        BattleHookDispatchSnapshot a = BattleHookDispatcher.Collect(new(0, BattleConditionHook.TurnEnd), forward.Values);
        BattleHookDispatchSnapshot b = BattleHookDispatcher.Collect(new(0, BattleConditionHook.TurnEnd), reverse.Values);

        Assert.Equal(a.Invocations, b.Invocations);
        Assert.Equal(a.Trace, b.Trace);
    }

    [Fact]
    public void ConditionFactoryAndRegistrationValidationRejectInvalidInputsBeforeCompletion()
    {
        BattleConditionDefinition definition = Definition("field:valid");
        var stores = new BattleConditionStores(new BattleConditionRegistry([definition]));
        BattleConditionInstance condition = Assert.Single(stores.Apply(Application(definition.Id, 0)).Affected);

        Assert.Throws<ArgumentException>(() => BattleHookRegistration.ForCondition(condition,
            BattleConditionHook.BeforeMove, 0, 0,
            new BattleHookFilter(new BattleHookFilterId("valid"), BattleHookFilterDecision.Allow)));
        Assert.Throws<ArgumentException>(() => BattleHookDispatcher.Collect(
            new(0, BattleConditionHook.TurnEnd),
            [Filter("bad_owner", BattleHookScope.Field, new(BattleSide.Player), sequence: 0)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleHookDispatcher.Collect(
            new(0, BattleConditionHook.TurnEnd),
            [Intent("valid", 0) with { Payload = new BattleHookIntent(IntentRequest() with { DueTurn = -1 }) }]));
    }

    private static BattleHookRegistration Filter(string label, BattleHookScope scope, BattleHookOwner owner,
        BattleConditionHook checkpoint = BattleConditionHook.TurnEnd, int priority = 0, long sequence = 0,
        int payloadIndex = 0, string? instanceSlug = null) => new(
            checkpoint, priority, scope, owner, new BattleHookInstanceId($"test:{instanceSlug ?? label}"),
            sequence, payloadIndex,
            new BattleHookFilter(new BattleHookFilterId(label), BattleHookFilterDecision.Allow));

    private static BattleHookRegistration Query(string label, BattleHookScope scope, BattleHookOwner owner,
        int priority, BattleQueryOperation operation, BattleQueryValue operand) => new(
            BattleConditionHook.BasePowerQuery, priority, scope, owner, new BattleHookInstanceId($"test:{label}"),
            0, 0, new BattleHookQueryModifier(BattleQueryId.BasePower,
                new BattleQueryModifier(BattleQueryStage.Hooks, operation, operand)));

    private static BattleHookRegistration Intent(string label, long sequence) => new(
        BattleConditionHook.AfterMove, 0, BattleHookScope.Move, new(BattleSide.Player, Player0, 0),
        new BattleHookInstanceId($"move:{label}"), sequence, 0, new BattleHookIntent(IntentRequest()));

    private static BattleIntentRequest IntentRequest() => new(
        0, BattleIntentCheckpoint.AfterMove,
        new BattleIntentOwner(BattleIntentOwnerScope.Field, BattleSide.Player),
        new BattleIntentTarget(BattleIntentTargetPolicy.Field),
        new SkipActionIntent(), EntityId.Parse("move:hook_source"), 0);

    private static BattleConditionDefinition Definition(string id) => new()
    {
        Id = new BattleConditionId(id),
        Scope = BattleConditionScope.Field,
        Hooks = [BattleConditionHook.TurnEnd],
        StackingKey = id.Replace(':', '_'),
        StackingPolicy = BattleConditionStackingPolicy.Reject,
        SwitchPolicy = BattleConditionSwitchPolicy.StayScope,
        FaintPolicy = BattleConditionFaintPolicy.Persist,
    };

    private static BattleConditionApplication Application(BattleConditionId id, int action) =>
        new(id, new BattleConditionOwner(BattleConditionScope.Field), new BattleConditionSource(), 0, action);

    private static BattleHookRegistration[] Registrations(BattleConditionStores stores) => stores.Snapshot()
        .Select(condition => BattleHookRegistration.ForCondition(condition, BattleConditionHook.TurnEnd,
            0, 0, new BattleHookFilter(new BattleHookFilterId(condition.Definition.Id.Value.Split(':')[1]),
                BattleHookFilterDecision.Allow)))
        .ToArray();
}
