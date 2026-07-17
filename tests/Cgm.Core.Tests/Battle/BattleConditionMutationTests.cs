using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleConditionMutationTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly BattleSlot Player0 = new(BattleSide.Player, 0);
    private static readonly BattleSlot Player1 = new(BattleSide.Player, 1);
    private static readonly BattleSlot Enemy0 = new(BattleSide.Enemy, 0);
    private static readonly BattleConditionSource UserSource = new(Player0, 0);
    private static readonly BattleConditionSource TargetSource = new(Enemy0, 0);

    [Fact]
    public void Compiler_AdmitsClosedRemoveTransferAndSwapOps()
    {
        BattleMove remove = Compile(MoveTarget.User, Op("conditionRemove",
            ("scope", "side"), ("owner", "user"), ("tag", "screen"), ("source", "environment")));
        Assert.Contains(remove.SecondaryEffects, effect => effect is RemoveConditionEffect
        {
            Owner: SideConditionTarget.Source,
            Selector: { Scope: BattleConditionScope.Side, Tag: "screen",
                Source: BattleConditionSourceTarget.Environment },
        });
        Assert.IsType<RemoveConditionEffect>(Compile(MoveTarget.AllOpponents,
            Op("conditionRemove", ("scope", "side"), ("owner", "user"),
                ("all", true), ("source", "target"))).SecondaryEffects.Single());

        BattleMove transfer = Compile(MoveTarget.Selected, Op("conditionTransfer",
            ("scope", "creature"), ("from", "target"), ("to", "user"),
            ("condition", "creature:neutral"), ("resetDuration", true), ("resetCounters", true)));
        Assert.Contains(transfer.SecondaryEffects, effect => effect is TransferConditionEffect
        {
            From: SideConditionTarget.Target, To: SideConditionTarget.Source,
            ResetDuration: true, ResetCounters: true,
        });

        BattleMove swap = Compile(MoveTarget.Ally, Op("conditionSwap",
            ("scope", "slot"), ("all", true)));
        Assert.Contains(swap.SecondaryEffects, effect => effect is SwapConditionEffect
        {
            Selector: { Scope: BattleConditionScope.Slot, All: true },
        });
    }

    [Fact]
    public void Compiler_RejectsMalformedMutationSiblings()
    {
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.User,
            Op("conditionRemove", ("scope", "side"), ("owner", "user"))));
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.User,
            Op("conditionRemove", ("scope", "side"), ("owner", "user"),
                ("tag", "screen"), ("all", true))));
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.User,
            Op("conditionRemove", ("scope", "field"), ("owner", "target"), ("all", true))));
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.AllOpponents,
            Op("conditionTransfer", ("scope", "side"), ("from", "target"), ("to", "user"),
                ("all", true))));
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.Selected,
            Op("conditionTransfer", ("scope", "field"), ("from", "target"), ("to", "user"),
                ("all", true))));
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.Selected,
            Op("conditionTransfer", ("scope", "side"), ("from", "user"), ("to", "user"),
                ("all", true))));
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.Selected,
            Op("conditionSwap", ("scope", "creature"), ("all", true))));
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.Ally,
            Op("conditionTransfer", ("scope", "side"), ("from", "user"), ("to", "target"),
                ("all", true))));
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.Ally,
            Op("conditionSwap", ("scope", "side"), ("all", true))));
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.Selected,
            Op("conditionSwap", 50, ("scope", "side"), ("all", true))));
        Assert.Throws<ArgumentException>(() => Compile(MoveTarget.Selected,
            Op("conditionSwap", ("scope", "side"), ("all", true), ("extra", 1))));
    }

    [Fact]
    public void RemoveSelectorsCoverIdTagAllAndSourceWithVisibleNoMatch()
    {
        BattleConditionDefinition first = Definition("side:first", BattleConditionScope.Side,
            tags: ["group"]);
        BattleConditionDefinition second = Definition("side:second", BattleConditionScope.Side,
            tags: ["group"]);
        BattleConditionDefinition third = Definition("side:third", BattleConditionScope.Side,
            tags: ["other"]);
        BattleConditionOwner owner = SideOwner(BattleSide.Player);
        BattleConditionStores stores = Stores(first, second, third);
        Apply(stores, first, owner, new BattleConditionSource(Player1, 0));
        Apply(stores, second, owner, new BattleConditionSource());
        Apply(stores, third, owner, TargetSource);

        BattleConditionMutationResult bySource = stores.RemoveSelected(
            Selector(BattleConditionScope.Side, tag: "group", source: BattleConditionSourceTarget.User),
            owner, UserSource, TargetSource, 1, 0);
        Assert.Equal(BattleConditionMutationOutcome.Applied, bySource.Outcome);
        Assert.Equal(first.Id, Assert.Single(bySource.Changes.Affected).Definition.Id);

        BattleConditionMutationResult noMatch = stores.RemoveSelected(
            Selector(BattleConditionScope.Side, condition: first.Id), owner,
            UserSource, TargetSource, 1, 1);
        Assert.Equal(BattleConditionMutationOutcome.NoMatch, noMatch.Outcome);
        Assert.Empty(noMatch.Changes.Events);

        BattleConditionMutationResult environment = stores.RemoveSelected(
            Selector(BattleConditionScope.Side, tag: "group",
                source: BattleConditionSourceTarget.Environment), owner,
            UserSource, TargetSource, 1, 2);
        Assert.Equal(second.Id, Assert.Single(environment.Changes.Affected).Definition.Id);

        BattleConditionMutationResult target = stores.RemoveSelected(
            Selector(BattleConditionScope.Side, condition: third.Id,
                source: BattleConditionSourceTarget.Target), owner,
            UserSource, TargetSource, 1, 3);
        Assert.Equal(third.Id, Assert.Single(target.Changes.Affected).Definition.Id);

        Apply(stores, second, owner, new BattleConditionSource());
        Apply(stores, third, owner, TargetSource);
        BattleConditionMutationResult all = stores.RemoveSelected(
            Selector(BattleConditionScope.Side, all: true), owner,
            UserSource, TargetSource, 1, 4);
        Assert.Equal([second.Id, third.Id], all.Changes.Affected.Select(item => item.Definition.Id));
        Assert.Empty(stores.Snapshot());
    }

    [Fact]
    public void RemoveSelectedSupportsEveryScopeOwnerShape()
    {
        BattleConditionDefinition[] definitions = Enum.GetValues<BattleConditionScope>()
            .Select(scope => Definition($"{scope.ToString().ToLowerInvariant()}:neutral", scope))
            .ToArray();
        BattleConditionStores stores = Stores(definitions);
        foreach (BattleConditionDefinition definition in definitions)
            Apply(stores, definition, Owner(definition.Scope));

        foreach (BattleConditionDefinition definition in definitions)
        {
            BattleConditionMutationResult result = stores.RemoveSelected(
                Selector(definition.Scope, condition: definition.Id), Owner(definition.Scope),
                UserSource, TargetSource, 1, (int)definition.Scope);
            Assert.Equal(BattleConditionMutationOutcome.Applied, result.Outcome);
        }
        Assert.Empty(stores.Snapshot());
    }

    [Fact]
    public void TransferPreservesIdentityAndCanResetDurationCountersAndStacks()
    {
        BattleConditionDefinition definition = Definition("side:layered", BattleConditionScope.Side,
            duration: 3, policy: BattleConditionStackingPolicy.Stack, maximumStacks: 3,
            counters: new Dictionary<string, int> { ["count"] = 2 });
        BattleConditionStores stores = Stores(definition);
        BattleConditionOwner from = SideOwner(BattleSide.Player);
        BattleConditionOwner to = SideOwner(BattleSide.Enemy);
        Apply(stores, definition, from, UserSource);
        Apply(stores, definition, from, UserSource);
        stores.CompleteCheckpoint(BattleIntentCheckpoint.TurnEnd, 0, 2);
        BattleConditionInstance before = Assert.Single(stores.Snapshot());

        BattleConditionMutationResult moved = stores.TransferSelected(
            Selector(BattleConditionScope.Side, all: true), from, to,
            UserSource, TargetSource, false, false, 1, 0);
        BattleConditionInstance preserved = Assert.Single(stores.Snapshot());
        Assert.Equal(BattleConditionMutationOutcome.Applied, moved.Outcome);
        Assert.Equal(before with { Owner = to }, preserved);

        stores.TransferSelected(Selector(BattleConditionScope.Side, all: true), to, from,
            UserSource, TargetSource, true, true, 1, 1);
        BattleConditionInstance reset = Assert.Single(stores.Snapshot());
        Assert.Equal(before.Sequence, reset.Sequence);
        Assert.Equal(before.Source, reset.Source);
        Assert.Equal(before.AppliedTurn, reset.AppliedTurn);
        Assert.Equal(3, reset.RemainingDuration);
        Assert.Equal(1, reset.StackCount);
        Assert.Equal(2, reset.Counters["count"]);
    }

    [Fact]
    public void TransferSupportsIntraSideSlotsAndCrossSideCreatures()
    {
        BattleConditionDefinition slot = Definition("slot:marker", BattleConditionScope.Slot);
        BattleConditionDefinition creature = Definition("creature:marker", BattleConditionScope.Creature);
        BattleConditionStores stores = Stores(slot, creature);
        BattleConditionOwner playerCreature = new(BattleConditionScope.Creature,
            BattleSide.Player, Player0, 0);
        BattleConditionOwner enemyCreature = new(BattleConditionScope.Creature,
            BattleSide.Enemy, Enemy0, 0);
        Apply(stores, slot, SlotOwner(Player0));
        Apply(stores, creature, playerCreature);

        BattleConditionMutationResult slotMove = stores.TransferSelected(
            Selector(BattleConditionScope.Slot, all: true), SlotOwner(Player0), SlotOwner(Player1),
            UserSource, TargetSource, false, false, 1, 0);
        BattleConditionMutationResult creatureMove = stores.TransferSelected(
            Selector(BattleConditionScope.Creature, all: true), playerCreature, enemyCreature,
            UserSource, TargetSource, false, false, 1, 1);

        Assert.Equal(Player1, Assert.Single(slotMove.Changes.Affected).Owner.Slot);
        BattleConditionOwner movedCreature = Assert.Single(creatureMove.Changes.Affected).Owner;
        Assert.Equal(BattleSide.Enemy, movedCreature.Side);
        Assert.Equal(0, movedCreature.PartyIndex);
    }

    [Fact]
    public void TransferConflictAndVariableResetRollbackAtomically()
    {
        BattleConditionDefinition first = Definition("side:first", BattleConditionScope.Side,
            stackingKey: "shared");
        BattleConditionOwner from = SideOwner(BattleSide.Player);
        BattleConditionOwner to = SideOwner(BattleSide.Enemy);
        BattleConditionStores stores = Stores(first);
        Apply(stores, first, from);
        Apply(stores, first, to);
        string snapshot = JsonSerializer.Serialize(stores.Snapshot());

        BattleConditionMutationResult conflict = stores.TransferSelected(
            Selector(BattleConditionScope.Side, condition: first.Id), from, to,
            UserSource, TargetSource, false, false, 1, 0);
        Assert.Equal(BattleConditionMutationOutcome.Conflict, conflict.Outcome);
        Assert.Equal(snapshot, JsonSerializer.Serialize(stores.Snapshot()));

        BattleConditionDefinition variable = Definition("slot:variable", BattleConditionScope.Slot) with
        {
            DurationCheckpoint = BattleIntentCheckpoint.TurnEnd,
        };
        stores = Stores(variable);
        Apply(stores, variable, SlotOwner(Player0), duration: 2);
        snapshot = JsonSerializer.Serialize(stores.Snapshot());
        BattleConditionMutationResult resetConflict = stores.TransferSelected(
            Selector(BattleConditionScope.Slot, all: true), SlotOwner(Player0), SlotOwner(Player1),
            UserSource, TargetSource, true, false, 1, 0);
        Assert.Equal(BattleConditionMutationOutcome.Conflict, resetConflict.Outcome);
        Assert.Equal(snapshot, JsonSerializer.Serialize(stores.Snapshot()));
    }

    [Fact]
    public void SideAndSlotSwapExchangeSelectedSetsIncludingEmptyOwner()
    {
        BattleConditionDefinition first = Definition("side:first", BattleConditionScope.Side, tags: ["swap"]);
        BattleConditionDefinition second = Definition("side:second", BattleConditionScope.Side, tags: ["swap"]);
        BattleConditionStores stores = Stores(first, second);
        Apply(stores, first, SideOwner(BattleSide.Player));
        Apply(stores, second, SideOwner(BattleSide.Enemy));
        long[] sequences = stores.Snapshot().Select(item => item.Sequence).ToArray();

        BattleConditionMutationResult exchanged = stores.SwapSelected(
            Selector(BattleConditionScope.Side, tag: "swap"),
            SideOwner(BattleSide.Player), SideOwner(BattleSide.Enemy),
            UserSource, TargetSource, false, false, 1, 0);
        Assert.Equal(BattleConditionMutationOutcome.Applied, exchanged.Outcome);
        Assert.Equal(sequences, stores.Snapshot().Select(item => item.Sequence).Order().ToArray());
        Assert.Equal(BattleSide.Enemy, stores.Snapshot().Single(item => item.Definition.Id == first.Id).Owner.Side);
        Assert.Equal(BattleSide.Player, stores.Snapshot().Single(item => item.Definition.Id == second.Id).Owner.Side);

        BattleConditionDefinition slot = Definition("slot:marker", BattleConditionScope.Slot);
        stores = Stores(slot);
        Apply(stores, slot, SlotOwner(Player0));
        BattleConditionMutationResult emptySwap = stores.SwapSelected(
            Selector(BattleConditionScope.Slot, all: true), SlotOwner(Player0), SlotOwner(Player1),
            UserSource, TargetSource, false, false, 1, 0);
        Assert.Equal(Player1, Assert.Single(emptySwap.Changes.Affected).Owner.Slot);
        Assert.Equal(Player1, Assert.Single(stores.Snapshot()).Owner.Slot);
    }

    [Fact]
    public void TransferPreservesSequenceAndDispatcherHookOrder()
    {
        BattleConditionDefinition first = Definition("side:first_hook", BattleConditionScope.Side,
            tags: ["hooks"]) with { Hooks = [BattleConditionHook.TryHit] };
        BattleConditionDefinition second = Definition("side:second_hook", BattleConditionScope.Side,
            tags: ["hooks"]) with { Hooks = [BattleConditionHook.TryHit] };
        BattleConditionStores stores = Stores(first, second);
        BattleConditionOwner from = SideOwner(BattleSide.Player);
        BattleConditionOwner to = SideOwner(BattleSide.Enemy);
        Apply(stores, first, from);
        Apply(stores, second, from);
        long[] before = HookOrder(stores.Snapshot());

        BattleConditionMutationResult result = stores.TransferSelected(
            Selector(BattleConditionScope.Side, tag: "hooks"), from, to,
            UserSource, TargetSource, false, false, 1, 0);

        Assert.Equal(before, HookOrder(stores.Snapshot()));
        Assert.Equal(before, result.Changes.Events.OfType<ConditionTransferred>()
            .Select(item => item.Sequence));
    }

    [Fact]
    public void ResolverTransfersTaggedSideConditionAndExposesNoMatchAndTrace()
    {
        BattleMove screen = new(EntityId.Parse("move:screen"), Normal, DamageClass.Status,
            null, null, 10, 4, 0, target: MoveTarget.UsersField,
            secondaryEffects: [new SetSideConditionEffect(BattleSideCondition.PhysicalScreen, 5)]);
        BattleMove transfer = Compile(MoveTarget.Selected, Op("conditionTransfer",
            ("scope", "side"), ("from", "target"), ("to", "user"), ("tag", "screen")));
        BattleCreature player = Creature("player", 50, screen, Wait("player_wait"));
        BattleCreature enemy = Creature("enemy", 100, Wait("enemy_wait"), transfer);
        var rng = new CountingRng();
        var battle = new BattleController(player, enemy, Chart(), rng);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        IReadOnlyList<BattleEvent> moved = battle.ResolveTurn(new UseMove(1), new UseMove(1));
        BattleConditionInstance current = Assert.Single(battle.ConditionSnapshot,
            item => item.Definition.Id == SideConditions.For(BattleSideCondition.PhysicalScreen).Id);
        Assert.Equal(BattleSide.Enemy, current.Owner.Side);
        Assert.Contains(moved, item => item is ConditionTransferred);
        Assert.Contains(battle.Trace, item => item is
            { Kind: EffectTraceKind.ConditionTransfer, Performed: true, Value: 1 });

        IReadOnlyList<BattleEvent> noMatch = battle.ResolveTurn(new UseMove(1), new UseMove(1));
        Assert.Contains(noMatch, item => item is ConditionOperationNoOp
            { Operation: BattleConditionOperation.Transfer, Scope: BattleConditionScope.Side });
        Assert.Contains(battle.Trace, item => item is
            { Kind: EffectTraceKind.ConditionTransfer, Performed: false, Value: 0 });
        Assert.Equal(0, rng.Draws);
    }

    [Fact]
    public void ResolverRejectsDestinationConflictWithoutPartialMutation()
    {
        BattleMove screen = new(EntityId.Parse("move:screen"), Normal, DamageClass.Status,
            null, null, 10, 4, 0, target: MoveTarget.UsersField,
            secondaryEffects: [new SetSideConditionEffect(BattleSideCondition.PhysicalScreen, 5)]);
        BattleMove transfer = Compile(MoveTarget.Selected, Op("conditionTransfer",
            ("scope", "side"), ("from", "user"), ("to", "target"), ("tag", "screen")));
        BattleCreature player = Creature("player", 100, screen, transfer);
        BattleCreature enemy = Creature("enemy", 50, screen, Wait("wait"));
        var battle = new BattleController(player, enemy, Chart(), new CountingRng());
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        var before = battle.ConditionSnapshot.Select(item =>
            (item.Definition.Id, item.Owner, item.Sequence, item.Source, item.StackCount)).ToArray();

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(1), new UseMove(1));

        Assert.Equal(before, battle.ConditionSnapshot.Select(item =>
            (item.Definition.Id, item.Owner, item.Sequence, item.Source, item.StackCount)).ToArray());
        Assert.Contains(events, item => item is ConditionOperationRejected
            { Operation: BattleConditionOperation.Transfer, Scope: BattleConditionScope.Side });
        Assert.Contains(battle.Trace, item => item is
            { Kind: EffectTraceKind.ConditionTransfer, Performed: false, Value: 0 });
    }

    [Fact]
    public void ConditionMutationFamily_MatchesDeterministicGolden()
    {
        static string Run()
        {
            BattleMove physical = new(EntityId.Parse("move:physical_screen"), Normal, DamageClass.Status,
                null, null, 10, 4, 0, target: MoveTarget.UsersField,
                secondaryEffects: [new SetSideConditionEffect(BattleSideCondition.PhysicalScreen, 5)]);
            BattleMove special = new(EntityId.Parse("move:special_screen"), Normal, DamageClass.Status,
                null, null, 10, 4, 0, target: MoveTarget.UsersField,
                secondaryEffects: [new SetSideConditionEffect(BattleSideCondition.SpecialScreen, 5)]);
            BattleMove swap = Compile(MoveTarget.Selected, Op("conditionSwap",
                ("scope", "side"), ("tag", "screen")));
            BattleMove remove = Compile(MoveTarget.User, Op("conditionRemove",
                ("scope", "side"), ("owner", "user"), ("tag", "screen")));
            var rng = new CountingRng();
            var battle = new BattleController(
                Creature("player", 100, physical, swap, remove),
                Creature("enemy", 50, special, Wait("wait")), Chart(), rng);
            var events = new List<BattleEvent>();
            events.AddRange(battle.ResolveTurn(new UseMove(0), new UseMove(0)));
            events.AddRange(battle.ResolveTurn(new UseMove(1), new UseMove(1)));
            events.AddRange(battle.ResolveTurn(new UseMove(2), new UseMove(1)));
            Assert.Equal(0, rng.Draws);
            Assert.Single(battle.ConditionSnapshot);
            Assert.Equal(BattleSide.Enemy, battle.ConditionSnapshot[0].Owner.Side);
            return string.Join('\n',
            [
                "events",
                .. events.Select(EventGolden).Where(value => value is not null)!,
                "condition-trace",
                .. battle.ConditionTrace.Where(item => item.Kind is BattleConditionTraceKind.Transferred
                        or BattleConditionTraceKind.Removed)
                    .Select(item => $"{item.Kind}:{item.Condition}:{item.Sequence}:" +
                        $"{item.OwnerBefore?.Side}->{item.OwnerAfter?.Side}"),
                "effect-trace",
                .. battle.Trace.Where(item => item.Kind is EffectTraceKind.ConditionSwap
                        or EffectTraceKind.ConditionRemoval)
                    .Select(item => $"{item.Kind}:{item.Performed}:{item.Value}"),
            ]);
        }

        string first = Run();
        Assert.Equal(first, Run());
        Assert.Equal(Golden("condition-mutation"), first);
    }

    private static BattleConditionSelector Selector(BattleConditionScope scope,
        BattleConditionId? condition = null, string? tag = null, bool all = false,
        BattleConditionSourceTarget source = BattleConditionSourceTarget.Any) =>
        new(scope, condition, tag, all, source);

    private static BattleConditionStores Stores(params BattleConditionDefinition[] definitions) =>
        new(new BattleConditionRegistry(definitions));

    private static void Apply(BattleConditionStores stores, BattleConditionDefinition definition,
        BattleConditionOwner owner, BattleConditionSource? source = null, int? duration = null) =>
        stores.Apply(new BattleConditionApplication(definition.Id, owner,
            source ?? new BattleConditionSource(), 0, 0, duration));

    private static BattleConditionDefinition Definition(string id, BattleConditionScope scope,
        int? duration = null, BattleConditionStackingPolicy policy = BattleConditionStackingPolicy.Reject,
        int maximumStacks = 1, string? stackingKey = null, IReadOnlyList<string>? tags = null,
        IReadOnlyDictionary<string, int>? counters = null) => new()
        {
            Id = new BattleConditionId(id), Scope = scope,
            Hooks = duration is null ? [] : [BattleConditionHook.TurnEnd],
            DefaultDuration = duration,
            DurationCheckpoint = duration is null ? null : BattleIntentCheckpoint.TurnEnd,
            InitialCounters = counters ?? new Dictionary<string, int>(),
            Tags = tags ?? [],
            StackingKey = stackingKey ?? id.Replace(':', '_'),
            StackingPolicy = policy,
            MaximumStacks = maximumStacks,
            SwitchPolicy = scope == BattleConditionScope.Creature
                ? BattleConditionSwitchPolicy.FollowOwner : BattleConditionSwitchPolicy.StayScope,
            FaintPolicy = scope == BattleConditionScope.Creature
                ? BattleConditionFaintPolicy.Remove : BattleConditionFaintPolicy.Persist,
        };

    private static BattleConditionOwner Owner(BattleConditionScope scope) => scope switch
    {
        BattleConditionScope.Side => SideOwner(BattleSide.Player),
        BattleConditionScope.Slot => SlotOwner(Player0),
        BattleConditionScope.Creature => new(scope, BattleSide.Player, Player0, 0),
        _ => new(scope),
    };

    private static BattleConditionOwner SideOwner(BattleSide side) =>
        new(BattleConditionScope.Side, side);

    private static BattleConditionOwner SlotOwner(BattleSlot slot) =>
        new(BattleConditionScope.Slot, slot.Side, slot);

    private static BattleMove Compile(MoveTarget target, Effect effect) => MoveCompiler.ToBattleMove(new Move
    {
        Id = EntityId.Parse("move:condition_mutation"), Name = "Condition Mutation", Type = Normal,
        DamageClass = DamageClass.Status, Pp = 10, Target = target, Effects = [effect],
    });

    private static Effect Op(string op, params (string Key, object Value)[] values) => Op(op, null, values);

    private static Effect Op(string op, int? chance, params (string Key, object Value)[] values) => new()
    {
        Op = op, Chance = chance,
        Params = values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static BattleCreature Creature(string slug, int speed, params BattleMove[] moves) => new(
        EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
        new Stats(400, 100, 100, 100, 100, speed), moves);

    private static BattleMove Wait(string slug) => new(EntityId.Parse($"move:{slug}"), Normal,
        DamageClass.Status, null, null, 10, 0, 0, target: MoveTarget.User);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static long[] HookOrder(IEnumerable<BattleConditionInstance> conditions)
    {
        BattleHookRegistration[] registrations = conditions.Select((condition, index) =>
            BattleHookRegistration.ForCondition(condition, BattleConditionHook.TryHit, 0, index,
                new BattleHookFilter(new BattleHookFilterId("mutation_probe"),
                    BattleHookFilterDecision.Deny))).ToArray();
        return BattleHookDispatcher.Collect(
                new BattleHookDispatchContext(1, BattleConditionHook.TryHit), registrations)
            .Trace.Select(item => item.Sequence).ToArray();
    }

    private static string Golden(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden"))
        .Replace("\r\n", "\n").TrimEnd();

    private static string? EventGolden(BattleEvent item) => item switch
    {
        MoveUsed e => $"MoveUsed:{e.Slot.Side}:{e.Slot.Position}:{e.Move}",
        ConditionApplied e => $"ConditionApplied:{e.Condition}:{e.Owner.Side}",
        ConditionTransferred e => $"ConditionTransferred:{e.Condition}:{e.Sequence}:" +
            $"{e.PreviousOwner.Side}->{e.Owner.Side}",
        ConditionRemoved e => $"ConditionRemoved:{e.Condition}:{e.Sequence}:{e.Owner.Side}:{e.Reason}",
        _ => null,
    };

    private sealed class CountingRng : IRng
    {
        public int Draws { get; private set; }
        public int Next(int maxExclusive) { Draws++; return 0; }
        public int Next(int minInclusive, int maxExclusive) { Draws++; return minInclusive; }
        public double NextDouble() { Draws++; return 0; }
    }
}
