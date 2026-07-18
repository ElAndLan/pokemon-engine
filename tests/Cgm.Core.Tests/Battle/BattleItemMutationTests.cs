using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleItemMutationTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId First = EntityId.Parse("item:first_charm");
    private static readonly EntityId Second = EntityId.Parse("item:second_charm");
    private static readonly BattleOverlayOwner User = new(BattleSide.Player, 0, new(BattleSide.Player, 0));
    private static readonly BattleOverlayOwner Target = new(BattleSide.Enemy, 0, new(BattleSide.Enemy, 0));

    [Fact]
    public void CompilerLocksRequirementsMutationShapeAndTargetCompatibility()
    {
        BattleMove compiled = Compile("swap", MoveTarget.Selected,
            Op("itemRequire", ("subject", "user"), ("state", "held")),
            Op("itemMutation", ("operation", "swap")));

        Assert.IsType<ItemRequireEffect>(compiled.SecondaryEffects[0]);
        Assert.Equal(BattleItemOperation.Swap,
            Assert.IsType<ItemMutationEffect>(compiled.SecondaryEffects[1]).Operation);
        Assert.Throws<ArgumentException>(() => Compile("bad_chance", MoveTarget.User,
            Op("itemMutation", 50, ("operation", "consume"))));
        Assert.Throws<ArgumentException>(() => Compile("bad_target", MoveTarget.User,
            Op("itemMutation", ("operation", "steal"))));
        Assert.Throws<ArgumentException>(() => Compile("bad_duration", MoveTarget.User,
            Op("itemMutation", ("operation", "consume"), ("duration", 2))));
        Assert.Throws<ArgumentException>(() => Compile("bad_transfer_subject", MoveTarget.Selected,
            Op("itemMutation", ("operation", "swap"), ("subject", "target"))));
    }

    [Fact]
    public void RequireFailsBeforePpAccuracyAndRng()
    {
        BattleMove gated = Compile("requires_item", MoveTarget.User,
            Op("itemRequire", ("subject", "user"), ("state", "held")));
        var rng = new CountingRng();
        var battle = new BattleController(Creature("user", gated), Creature("target", Inert(), speed: 1), Chart(), rng,
            itemData: Items());

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Contains(events, e => e is MoveFailed { Reason: MoveFailureReason.ItemRequirementNotMet });
        Assert.Equal(10, gated.Pp);
        Assert.Equal(0, rng.Calls);
    }

    [Fact]
    public void GiveStealAndSwapValidateCapacityThenCommitAtomically()
    {
        var overlays = new BattleOverlayStore();
        var state = new BattleItemState(overlays, Items().ToDictionary(item => item.Id));
        BattleEffectiveValues user = Values(First);
        BattleEffectiveValues target = Values(Second);

        BattleItemMutationResult occupied = state.Mutate(BattleItemOperation.Give,
            User, user, false, Target, target, false, 1, 1, "give");
        Assert.Equal(BattleItemMutationFailure.Occupied, occupied.Failure);
        Assert.Empty(overlays.Snapshot());

        BattleItemMutationResult swapped = state.Mutate(BattleItemOperation.Swap,
            User, user, false, Target, target, false, 1, 2, "swap");
        Assert.True(swapped.Succeeded);
        Assert.Equal(Second, overlays.Resolve(User, user).Values.HeldItem);
        Assert.Equal(First, overlays.Resolve(Target, target).Values.HeldItem);
        Assert.Equal(2, swapped.Changes.Count);
    }

    [Fact]
    public void ProtectionAndFaintFailuresLeaveStateUntouched()
    {
        Item guarded = Item(First, new Effect
        {
            Op = "itemMutationGuard",
            Params = Params(("operations", "steal,swap,remove,destroy")),
        });
        var overlays = new BattleOverlayStore();
        var state = new BattleItemState(overlays, new[] { guarded, Item(Second) }.ToDictionary(item => item.Id));

        Assert.Equal(BattleItemMutationFailure.Protected, state.Mutate(BattleItemOperation.Steal,
            User, Values(null), false, Target, Values(First), false, 1, 1, "steal").Failure);
        Assert.Equal(BattleItemMutationFailure.Protected, state.Mutate(BattleItemOperation.Swap,
            User, Values(Second), false, Target, Values(First), false, 1, 2, "swap").Failure);
        Assert.Equal(BattleItemMutationFailure.Fainted, state.Mutate(BattleItemOperation.Swap,
            User, Values(Second), true, Target, Values(First), false, 1, 3, "swap").Failure);
        Assert.Empty(overlays.Snapshot());
    }

    [Fact]
    public void ConsumptionHistoryAgesAndRestoreConsumesOnlyOnSuccess()
    {
        var overlays = new BattleOverlayStore();
        var state = new BattleItemState(overlays, Items().ToDictionary(item => item.Id));
        BattleEffectiveValues first = Values(First);

        Assert.True(state.Mutate(BattleItemOperation.Consume, User, first, false,
            Target, Values(null), false, 1, 1, "first").Succeeded);
        Assert.Equal(First, state.LastConsumed(User)!.Item);

        overlays.Apply(new BattleOverlayApplication(User, new(), BattleOverlayLayer.PermanentInstance,
            new HeldItemOverlay(Second), 2, 1));
        Assert.True(state.Mutate(BattleItemOperation.Consume, User, first, false,
            Target, Values(null), false, 2, 2, "second").Succeeded);
        Assert.Equal(Second, state.LastConsumed(User)!.Item);

        overlays.Apply(new BattleOverlayApplication(User, new(), BattleOverlayLayer.PermanentInstance,
            new HeldItemOverlay(First), 3, 1));
        Assert.Equal(BattleItemMutationFailure.Occupied, state.Mutate(BattleItemOperation.Restore,
            User, first, false, Target, Values(null), false, 3, 2, "restore").Failure);
        Assert.Equal(Second, state.LastConsumed(User)!.Item);

        overlays.Apply(new BattleOverlayApplication(User, new(), BattleOverlayLayer.PermanentInstance,
            new HeldItemOverlay(null), 3, 3));
        Assert.True(state.Mutate(BattleItemOperation.Restore,
            User, first, false, Target, Values(null), false, 3, 4, "restore").Succeeded);
        Assert.Null(state.LastConsumed(User));
        Assert.Equal(Second, overlays.Resolve(User, first).Values.HeldItem);
    }

    [Fact]
    public void RemoveDestroySuppressAndStickyProtectionUseTheSameAtomicPath()
    {
        var overlays = new BattleOverlayStore();
        var state = new BattleItemState(overlays, Items().ToDictionary(item => item.Id));
        BattleEffectiveValues target = Values(First);

        Assert.Equal(BattleItemMutationFailure.Protected, state.Mutate(BattleItemOperation.Remove,
            User, Values(null), false, Target, target, false, 1, 1, "remove",
            stickyProtection: (_, _) => true).Failure);
        Assert.Empty(overlays.Snapshot());

        Assert.True(state.Mutate(BattleItemOperation.Destroy,
            User, Values(null), false, Target, target, false, 1, 2, "destroy").Succeeded);
        Assert.Null(overlays.Resolve(Target, target).Values.HeldItem);

        overlays.Apply(new BattleOverlayApplication(User, new(), BattleOverlayLayer.PermanentInstance,
            new HeldItemOverlay(Second), 1, 3));
        Assert.True(state.Mutate(BattleItemOperation.Suppress,
            User, Values(null), false, Target, Values(null), false, 1, 4, "suppress", 1).Succeeded);
        Assert.Null(overlays.Resolve(User, Values(null)).Values.HeldItem);
        overlays.CompleteCheckpoint(BattleIntentCheckpoint.TurnEnd, 1, 5);
        Assert.Equal(Second, overlays.Resolve(User, Values(null)).Values.HeldItem);
    }

    [Fact]
    public void BattleEndClearsConsumptionHistory()
    {
        var state = new BattleItemState(new BattleOverlayStore(), Items().ToDictionary(item => item.Id));
        Assert.True(state.Mutate(BattleItemOperation.Consume, User, Values(First), false,
            Target, Values(null), false, 1, 1, "consume").Succeeded);

        state.EndBattle();

        Assert.Null(state.LastConsumed(User));
    }

    [Fact]
    public void ResolverEmitsMutationTraceAndStolenItemDrivesNextPowerQuery()
    {
        BattleMove steal = Compile("steal", MoveTarget.Selected,
            Op("itemMutation", ("operation", "steal")));
        BattleMove itemPower = MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse("move:item_power"), Name = "item power", Type = Normal,
            DamageClass = DamageClass.Physical, Accuracy = 100, Pp = 10,
            Effects = [Op("itemDataPower", ("field", "flingPower"))],
        });
        BattleCreature user = Creature("user", steal, itemPower);
        BattleCreature target = Creature("target", Inert(), heldItem: First);
        var battle = new BattleController(user, target, Chart(), new CountingRng(), itemData: Items());

        IReadOnlyList<BattleEvent> first = battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Contains(first, e => e is HeldItemMutated
            { Side: BattleSide.Player, Before: null, After: var item } && item == First);
        EffectTraceEntry mutation = Assert.Single(battle.Trace,
            trace => trace.Kind == EffectTraceKind.HeldItemMutation);
        Assert.True(mutation.Performed);
        Assert.True(mutation.EventEndIndex > mutation.EventStartIndex);

        battle.ResolveTurn(new UseMove(1), new UseMove(0));
        Assert.Contains(battle.Trace, trace => trace.Kind == EffectTraceKind.Damage && trace.Value > 0);
    }

    [Fact]
    public void SmartAiRejectsKnownOwnRequirementAndKeepsMutationScoringNeutral()
    {
        BattleMove conditional = new(EntityId.Parse("move:conditional"), Normal, DamageClass.Physical,
            200, 100, 10, 0, 0, secondaryEffects:
            [
                new ItemRequireEffect(BattleItemSubject.User, BattleItemRequirement.Empty),
                new ItemMutationEffect(BattleItemOperation.Remove, BattleItemSubject.Target),
            ]);
        BattleMove weak = new(EntityId.Parse("move:weak"), Normal, DamageClass.Physical,
            20, 100, 10, 0, 0);
        BattleCreature defender = Creature("defender", Inert(), speed: 1);

        SmartAiDecision blocked = SmartAi.ChooseAction(new SmartAiContext(
            [Creature("holder", conditional, weak, First)], 0, [defender], 0, Chart(), new CountingRng(),
            Weights: new SmartAiWeights { NoiseFraction = 0 }));
        Assert.Equal(new UseMove(1), blocked.Action);
        Assert.Contains(blocked.Scores.Single(score => score.Action == new UseMove(0)).Components,
            component => component is { Name: "itemRequirement", Value: -1_000_000 });

        SmartAiDecision allowed = SmartAi.ChooseAction(new SmartAiContext(
            [Creature("empty", conditional, weak)], 0, [defender], 0, Chart(), new CountingRng(),
            Weights: new SmartAiWeights { NoiseFraction = 0 }));
        Assert.Equal(new UseMove(0), allowed.Action);
        Assert.Contains(allowed.Scores.Single(score => score.Action == new UseMove(0)).Components,
            component => component is { Name: "itemMutation", Value: 0 });
    }

    [Fact]
    public void MutationEventAndTraceReplayIsStable()
    {
        BattleMove swap = Compile("swap_replay", MoveTarget.Selected,
            Op("itemMutation", ("operation", "swap")));
        var battle = new BattleController(Creature("user", swap, heldItem: First, speed: 100),
            Creature("target", Inert(), heldItem: Second, speed: 1), Chart(), new CountingRng(),
            itemData: Items());

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());
        string replay = string.Join('\n', events.Select(EventRow)
            .Concat(battle.Trace.Where(trace => trace.Kind == EffectTraceKind.HeldItemMutation)
                .Select(trace => $"trace:{trace.Turn}:{trace.ActionSequence}:{trace.SourceSlot.Side}:"
                    + $"{trace.TargetSlot?.Side}:{trace.Performed}:{trace.Value}:"
                    + $"{trace.EventStartIndex}-{trace.EventEndIndex}")));

        Assert.Equal(Golden("item-mutation"), replay);
    }

    private static BattleEffectiveValues Values(EntityId? item) => new(item, null, [Normal],
        new Stats(100, 50, 50, 50, 50, 50), [], metrics: new Dictionary<BattleMetric, int>
        {
            [BattleMetric.Weight] = 1,
            [BattleMetric.Height] = 1,
        });

    private static IReadOnlyList<Item> Items() => [Item(First, flingPower: 80), Item(Second)];
    private static Item Item(EntityId id, Effect? effect = null, int? flingPower = null) => new()
    {
        Id = id,
        Name = id.Slug,
        Holdable = true,
        FlingPower = flingPower,
        BattleEffects = effect is null ? [] : [effect],
    };

    private static BattleMove Compile(string slug, MoveTarget target, params Effect[] effects) =>
        MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse($"move:{slug}"), Name = slug, Type = Normal,
            DamageClass = DamageClass.Status, Pp = 10, Target = target, Effects = effects,
        });

    private static BattleCreature Creature(string slug, BattleMove move, BattleMove? other = null,
        EntityId? heldItem = null, int speed = 50) => new(EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
            new Stats(500, 100, 100, 100, 100, speed), other is null ? [move] : [move, other],
            heldItem: heldItem);

    private static BattleMove Inert() => new(EntityId.Parse("move:inert"), Normal,
        DamageClass.Status, null, null, 20, 0, 0);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static string EventRow(BattleEvent battleEvent) => battleEvent switch
    {
        MoveUsed used => $"used:{used.Slot.Side}:{used.Move}",
        HeldItemMutated changed => $"item:{changed.Side}:{changed.PartyIndex}:{changed.Before}:"
            + $"{changed.After}:{changed.Operation}:{changed.Cause}",
        _ => $"event:{battleEvent.GetType().Name}",
    };

    private static string Golden(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden"))
        .Replace("\r\n", "\n").TrimEnd();

    private static Effect Op(string op, params (string Key, object Value)[] values) =>
        Op(op, null, values);

    private static Effect Op(string op, int? chance, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Chance = chance,
        Params = values.Length == 0 ? null : Params(values),
    };

    private static IReadOnlyDictionary<string, JsonElement> Params(
        params (string Key, object Value)[] values) => values.ToDictionary(value => value.Key,
            value => JsonSerializer.SerializeToElement(value.Value));

    private sealed class CountingRng : IRng
    {
        public int Calls { get; private set; }
        public int Next(int maxExclusive) { Calls++; return 0; }
        public int Next(int minInclusive, int maxExclusive) { Calls++; return minInclusive; }
        public double NextDouble() { Calls++; return 0; }
    }
}
