using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleTypeStateTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Ember = EntityId.Parse("type:ember");
    private static readonly EntityId Tide = EntityId.Parse("type:tide");
    private static readonly EntityId Stone = EntityId.Parse("type:stone");
    private static readonly BattleOverlayOwner Owner = new(BattleSide.Player, 0, new BattleSlot(BattleSide.Player, 0));

    [Fact]
    public void CompilerAdmitsClosedTypeVocabularyAndRejectsMalformedRows()
    {
        BattleMove move = Compile("valid", MoveTarget.Selected,
            Op("typeRequire", ("subject", "user"), ("type", Normal.ToString())),
            Op("typeMutation", ("operation", "replace"), ("subject", "user"),
                ("source", "fixed"), ("type", Ember.ToString())),
            Op("moveTypeQuery", ("source", "userPrimary")),
            Op("moveTypeOverride", ("subject", "target"), ("type", Tide.ToString()), ("duration", 1)));
        Assert.Collection(move.SecondaryEffects,
            effect => Assert.IsType<TypeRequireEffect>(effect),
            effect => Assert.IsType<TypeMutationEffect>(effect),
            effect => Assert.IsType<MoveTypeQueryEffect>(effect),
            effect => Assert.IsType<MoveTypeOverrideEffect>(effect));

        Assert.Throws<ArgumentException>(() => Compile("chance", MoveTarget.Selected,
            Op("typeRequire", 50, ("subject", "user"), ("type", Normal.ToString()))));
        Assert.Throws<ArgumentException>(() => Compile("bad_remove", MoveTarget.Selected,
            Op("typeMutation", ("operation", "remove"), ("subject", "user"),
                ("source", "fixed"), ("type", Ember.ToString()))));
        Assert.Throws<ArgumentException>(() => Compile("bad_override", MoveTarget.Selected,
            Op("moveTypeOverride", ("subject", "allActive"), ("type", Tide.ToString()), ("duration", 2))));
    }

    [Fact]
    public void ReplaceAddAndRequiredRemoveUseAtomicEffectiveOverlays()
    {
        var overlays = new BattleOverlayStore();
        var state = new BattleTypeState(overlays, Chart());
        BattleEffectiveValues values = Values([Normal]);
        var effect = new TypeMutationEffect(BattleTypeOperation.Replace, BattleTypeSubject.User,
            BattleTypeSource.Fixed, Ember);

        BattleTypeMutationResult replaced = state.Mutate(effect, Owner, values, false, Owner, values, false,
            BattleEnvironment.Building, new BattleActionHistory(), new Rng(1), 0, 0);
        Assert.True(replaced.Succeeded);
        Assert.Equal([Ember], state.Effective(Owner, values));

        BattleTypeMutationResult duplicate = state.Mutate(
            effect with { Operation = BattleTypeOperation.Add }, Owner, values, false, Owner, values, false,
            BattleEnvironment.Building, new BattleActionHistory(), new Rng(1), 0, 1);
        Assert.False(duplicate.Succeeded);
        Assert.Equal([Ember], state.Effective(Owner, values));

        BattleTypeMutationResult removed = state.Mutate(
            effect with { Operation = BattleTypeOperation.Remove, FallbackType = Tide }, Owner, values, false,
            Owner, values, false, BattleEnvironment.Building, new BattleActionHistory(), new Rng(1), 0, 2);
        Assert.True(removed.Succeeded);
        Assert.Equal([Tide], state.Effective(Owner, values));
    }

    [Fact]
    public void MoveTypeOverrideResolvesInInsertionOrderAndCanFilter()
    {
        var overlays = new BattleOverlayStore();
        var state = new BattleTypeState(overlays, Chart());
        BattleEffectiveValues values = Values([Normal]);
        state.ApplyOverride(new MoveTypeOverrideEffect(BattleTypeSubject.User, Ember, null, 1),
            [Owner], new BattleOverlaySource(Owner.Slot, Owner.PartyIndex), 0, 0);
        state.ApplyOverride(new MoveTypeOverrideEffect(BattleTypeSubject.User, Tide, Ember, 1),
            [Owner], new BattleOverlaySource(Owner.Slot, Owner.PartyIndex), 0, 1);

        BattleEffectiveValues resolved = overlays.Resolve(Owner, values).Values;
        Assert.Equal([new BattleMoveTypeRule(Ember), new BattleMoveTypeRule(Tide, Ember)], resolved.MoveTypeRules);
    }

    [Fact]
    public void ReplacementAndRemovalClearEarlierAdditiveTypeContributions()
    {
        var overlays = new BattleOverlayStore();
        var state = new BattleTypeState(overlays, Chart());
        BattleEffectiveValues values = Values([Normal]);
        TypeMutationEffect add = new(BattleTypeOperation.Add, BattleTypeSubject.User,
            BattleTypeSource.Fixed, Ember);
        TypeMutationEffect remove = new(BattleTypeOperation.Remove, BattleTypeSubject.User,
            BattleTypeSource.Fixed, Ember, Tide);

        Assert.True(state.Mutate(add, Owner, values, false, Owner, values, false,
            BattleEnvironment.Building, new BattleActionHistory(), new Rng(1), 0, 0).Succeeded);
        Assert.Equal([Normal, Ember], state.Effective(Owner, values));
        Assert.True(state.Mutate(remove, Owner, values, false, Owner, values, false,
            BattleEnvironment.Building, new BattleActionHistory(), new Rng(1), 0, 1).Succeeded);
        Assert.Equal([Normal], state.Effective(Owner, values));
    }

    [Fact]
    public void ResistanceSourceUsesOrdinalCandidatesAndOneBoundedDraw()
    {
        TypeChart chart = new([
            new TypeDef { Id = Normal },
            new TypeDef { Id = Ember, HalfDamageTo = [Stone, Tide] },
            new TypeDef { Id = Stone },
            new TypeDef { Id = Tide },
        ]);
        var overlays = new BattleOverlayStore();
        var state = new BattleTypeState(overlays, chart);
        BattleEffectiveValues values = Values([Normal]);
        BattleActionHistory history = HistoryWithDamage(Ember);
        var rng = new CountingRng(1);
        TypeMutationEffect effect = new(BattleTypeOperation.Replace, BattleTypeSubject.User,
            BattleTypeSource.ResistantToLastDamage);

        BattleTypeMutationResult result = state.Mutate(effect, Owner, values, false, Owner, values, false,
            BattleEnvironment.Building, history, rng, 1, 1);

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Draw);
        Assert.Equal(2, result.DrawBound);
        Assert.Equal(1, rng.Calls);
        Assert.Equal([Tide], state.Effective(Owner, values));
    }

    [Fact]
    public void ResistanceSourceSkipsDrawForZeroOrOneCandidate()
    {
        TypeMutationEffect effect = new(BattleTypeOperation.Replace, BattleTypeSubject.User,
            BattleTypeSource.ResistantToLastDamage);
        BattleEffectiveValues values = Values([Normal]);

        var oneRng = new CountingRng(0);
        var oneState = new BattleTypeState(new BattleOverlayStore(), new TypeChart([
            new TypeDef { Id = Normal },
            new TypeDef { Id = Ember, HalfDamageTo = [Tide] },
            new TypeDef { Id = Tide },
        ]));
        BattleTypeMutationResult one = oneState.Mutate(effect, Owner, values, false, Owner, values, false,
            BattleEnvironment.Building, HistoryWithDamage(Ember), oneRng, 1, 1);

        Assert.True(one.Succeeded);
        Assert.Null(one.Draw);
        Assert.Null(one.DrawBound);
        Assert.Equal(0, oneRng.Calls);
        Assert.Equal([Tide], oneState.Effective(Owner, values));

        var zeroRng = new CountingRng(0);
        var zeroState = new BattleTypeState(new BattleOverlayStore(), new TypeChart([
            new TypeDef { Id = Normal },
            new TypeDef { Id = Ember },
        ]));
        BattleTypeMutationResult zero = zeroState.Mutate(effect, Owner, values, false, Owner, values, false,
            BattleEnvironment.Building, HistoryWithDamage(Ember), zeroRng, 1, 1);

        Assert.Equal(BattleTypeMutationFailure.MissingSource, zero.Failure);
        Assert.Null(zero.Draw);
        Assert.Null(zero.DrawBound);
        Assert.Equal(0, zeroRng.Calls);
        Assert.Equal([Normal], zeroState.Effective(Owner, values));
    }

    [Fact]
    public void ResistanceSourceControllerTraceCapturesDrawBoundAndEventRange()
    {
        TypeChart chart = new([
            new TypeDef { Id = Normal },
            new TypeDef { Id = Ember, HalfDamageTo = [Stone, Tide] },
            new TypeDef { Id = Stone },
            new TypeDef { Id = Tide },
        ]);
        BattleMove mutate = new(EntityId.Parse("move:resist"), Normal, DamageClass.Status,
            null, null, 10, 0, 0,
            secondaryEffects: [new TypeMutationEffect(BattleTypeOperation.Replace,
                BattleTypeSubject.User, BattleTypeSource.ResistantToLastDamage)]);
        BattleCreature player = new(EntityId.Parse("species:player"), "Player", 50, [Normal],
            new Stats(200, 100, 100, 100, 100, 100), [mutate]);
        BattleCreature enemy = new(EntityId.Parse("species:enemy"), "Enemy", 50, [Normal],
            new Stats(200, 100, 100, 100, 100, 10),
            [new BattleMove(EntityId.Parse("move:history_hit"), Ember, DamageClass.Special,
                10, null, 10, 0, 0)]);
        var rng = new CountingRng(1);
        var battle = new BattleController(player, enemy, chart, rng);
        battle.ResolveTurn(new Pass(), new UseMove(0));
        rng.Reset(1);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        EffectTraceEntry trace = Assert.Single(battle.Trace,
            item => item.Kind == EffectTraceKind.TypeMutation);
        Assert.Equal(1, trace.DrawResult);
        Assert.Equal(2, trace.DrawBound);
        Assert.Equal(1, rng.Calls);
        Assert.Single(events.OfType<TypesMutated>());
        Assert.Equal(1, trace.EventEndIndex - trace.EventStartIndex);
        Assert.Equal([Tide], battle.Overlays.Resolve(Owner, Values([Normal])).Values.CreatureTypes);
    }

    [Fact]
    public void MutationEventAndTraceReplayIsStable()
    {
        BattleMove mutate = Compile("type_replay", MoveTarget.Selected,
            Op("typeMutation", ("operation", "replace"), ("subject", "target"),
                ("source", "fixed"), ("type", Tide.ToString())));
        BattleCreature player = new(EntityId.Parse("species:player"), "Player", 50, [Normal],
            new Stats(200, 100, 100, 100, 100, 100), [mutate]);
        BattleCreature enemy = new(EntityId.Parse("species:enemy"), "Enemy", 50, [Normal],
            new Stats(200, 100, 100, 100, 100, 10),
            [new BattleMove(EntityId.Parse("move:wait"), Normal, DamageClass.Status,
                null, null, 10, 0, 0)]);
        var battle = new BattleController(player, enemy,
            new TypeChart([new TypeDef { Id = Normal }, new TypeDef { Id = Tide }]), new CountingRng(0));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());
        string replay = string.Join('\n', events.Select(EventRow)
            .Concat(battle.Trace.Where(trace => trace.Kind == EffectTraceKind.TypeMutation)
                .Select(trace => $"trace:{trace.Turn}:{trace.ActionSequence}:{trace.SourceSlot.Side}:"
                    + $"{trace.TargetSlot?.Side}:{trace.Performed}:{trace.Value}:"
                    + $"{trace.EventStartIndex}-{trace.EventEndIndex}")));

        Assert.Equal(Golden("type-mutation"), replay);
    }

    [Fact]
    public void ControllerCopyUsesSelectedTargetEffectiveTypes()
    {
        BattleMove copy = Compile("copy_target", MoveTarget.Selected,
            Op("typeMutation", ("operation", "copy"), ("subject", "user"), ("source", "target")));
        BattleCreature player = new(EntityId.Parse("species:player"), "Player", 50, [Normal],
            new Stats(200, 100, 100, 100, 100, 100), [copy]);
        BattleCreature enemy = new(EntityId.Parse("species:enemy"), "Enemy", 50, [Tide],
            new Stats(200, 100, 100, 100, 100, 10),
            [new BattleMove(EntityId.Parse("move:wait"), Normal, DamageClass.Status,
                null, null, 10, 0, 0)]);
        var battle = new BattleController(player, enemy,
            new TypeChart([new TypeDef { Id = Normal }, new TypeDef { Id = Tide }]), new CountingRng(0));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        TypesMutated changed = Assert.Single(events.OfType<TypesMutated>());
        Assert.Equal(BattleSide.Player, changed.Side);
        Assert.Equal([Normal], changed.Before);
        Assert.Equal([Tide], changed.After);
        Assert.Equal([Tide], battle.Overlays.Resolve(Owner, Values([Normal])).Values.CreatureTypes);
    }

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }, new TypeDef { Id = Ember }, new TypeDef { Id = Tide }]);

    private static BattleActionHistory HistoryWithDamage(EntityId type)
    {
        var history = new BattleActionHistory();
        SeedDamage(history, type);
        return history;
    }

    private static void SeedDamage(BattleActionHistory history, EntityId type)
    {
        BattleHistoryOwner source = new(BattleSide.Enemy, 0, new BattleSlot(BattleSide.Enemy, 0));
        BattleHistoryOwner target = new(BattleSide.Player, 0, new BattleSlot(BattleSide.Player, 0));
        EntityId move = EntityId.Parse("move:history_hit");
        history.BeginTurn(0, [new BattleActionPlan(source, BattlePlannedActionKind.Move, DamageClass.Special)]);
        BattleActionAttemptId attempt = history.BeginMove(1, source, move);
        history.MarkStarted(attempt);
        history.RecordDamage(new BattleDamageRecord(attempt, source, target, move, DamageClass.Special,
            type, BattleDamageCause.Standard, 1, true, true, BattleDamageFailure.None,
            10, 10, 10, false, false, false, false));
        history.Complete(attempt, BattleActionResult.Connected, [target]);
    }

    private static BattleEffectiveValues Values(IReadOnlyList<EntityId> types) => new(
        null, null, types, new Stats(10, 10, 10, 10, 10, 10),
        [BattleEffectiveMove.FromBase(new BattleMove(EntityId.Parse("move:test"), Normal,
            DamageClass.Physical, 10, 100, 10, 0, 0), 0)]);

    private static BattleMove Compile(string slug, MoveTarget target, params Effect[] effects) =>
        MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse($"move:{slug}"), Name = slug, Type = Normal,
            DamageClass = DamageClass.Status, Pp = 10, Target = target, Effects = effects,
        });

    private static Effect Op(string op, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Params = values.ToDictionary(value => value.Key,
            value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static Effect Op(string op, int chance, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Chance = chance,
        Params = values.ToDictionary(value => value.Key,
            value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static string EventRow(BattleEvent battleEvent) => battleEvent switch
    {
        MoveUsed used => $"used:{used.Slot.Side}:{used.Move}",
        TypesMutated changed => $"types:{changed.Side}:{changed.PartyIndex}:"
            + $"{string.Join(',', changed.Before)}:{string.Join(',', changed.After)}:{changed.Operation}",
        _ => $"event:{battleEvent.GetType().Name}",
    };

    private static string Golden(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden"))
        .Replace("\r\n", "\n").TrimEnd();

    private sealed class CountingRng(int result) : IRng
    {
        private int _result = result;
        public int Calls { get; private set; }
        public int Next(int maxExclusive)
        {
            Calls++;
            return _result;
        }
        public int Next(int minInclusive, int maxExclusive) => minInclusive + Next(maxExclusive - minInclusive);
        public double NextDouble() => 0;
        public void Reset(int nextResult)
        {
            _result = nextResult;
            Calls = 0;
        }
    }
}
