using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleStatMetricMutationTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly BattleOverlayOwner User = new(BattleSide.Player, 0, new(BattleSide.Player, 0));
    private static readonly BattleOverlayOwner Target = new(BattleSide.Enemy, 0, new(BattleSide.Enemy, 0));

    [Fact]
    public void CompilerAdmitsClosedStatMetricVocabularyAndRejectsMalformedRows()
    {
        Assert.IsType<DerivedStatMutationEffect>(Assert.Single(Compile("average",
            Op("derivedStatMutation", ("operation", "average"), ("stat", "atk"))).SecondaryEffects));
        Assert.IsType<DerivedStatMutationEffect>(Assert.Single(Compile("split",
            Op("derivedStatMutation", ("operation", "split"), ("group", "defense"))).SecondaryEffects));
        Assert.IsType<MetricMutationEffect>(Assert.Single(Compile("metric",
            Op("metricMutation", ("operation", "add"), ("subject", "self"),
                ("metric", "weight"), ("value", -10), ("duration", 2))).SecondaryEffects));

        Assert.Throws<ArgumentException>(() => Compile("chance",
            Op("derivedStatMutation", 50, ("operation", "average"), ("stat", "atk"))));
        Assert.Throws<ArgumentException>(() => Compile("missing_stat",
            Op("derivedStatMutation", ("operation", "average"))));
        Assert.Throws<ArgumentException>(() => Compile("hp",
            Op("derivedStatMutation", ("operation", "swap"), ("stat", "hp"))));
        Assert.Throws<ArgumentException>(() => Compile("replace_missing",
            Op("metricMutation", ("operation", "replace"), ("subject", "self"), ("metric", "weight"))));
        Assert.Throws<ArgumentException>(() => Compile("subject_missing",
            Op("metricMutation", ("operation", "replace"), ("metric", "weight"), ("value", 2))));
        Assert.Throws<ArgumentException>(() => Compile("add_zero",
            Op("metricMutation", ("operation", "add"), ("subject", "self"),
                ("metric", "height"), ("value", 0))));
        Assert.Throws<ArgumentException>(() => Compile("swap_subject",
            Op("metricMutation", ("operation", "swap"), ("subject", "target"), ("metric", "height"))));
        Assert.Throws<ArgumentException>(() => Compile("duration",
            Op("metricMutation", ("operation", "replace"), ("subject", "self"),
                ("metric", "weight"), ("value", 2), ("duration", 0))));
    }

    [Fact]
    public void SplitUsesOneEffectiveSnapshotAndTerminalValuesDoNotDoubleEarlierAdditions()
    {
        var overlays = new BattleOverlayStore();
        overlays.Apply(new BattleOverlayApplication(User, new(), BattleOverlayLayer.Additive,
            new StatDeltaOverlay("prior", new Stats(0, 1, 0, 1, 0, 0)), 0, 0));
        var state = new BattleStatMetricState(overlays);
        BattleEffectiveValues userBase = Values(new Stats(100, 10, 30, 20, 40, 50), 100, 10);
        BattleEffectiveValues targetBase = Values(new Stats(100, 14, 50, 26, 60, 70), 40, 5);

        DerivedStatMutationResult result = state.Mutate(
            new DerivedStatMutationEffect(BattleDerivedStatOperation.Split, Group: BattleDerivedStatGroup.Offense),
            User, userBase, false, Target, targetBase, false, 0, 1);

        Assert.True(result.Succeeded);
        Assert.Equal(4, result.Changes.Count);
        Assert.Equal(new Stats(100, 12, 30, 23, 40, 50), overlays.Resolve(User, userBase).Values.Stats);
        Assert.Equal(new Stats(100, 12, 50, 23, 60, 70), overlays.Resolve(Target, targetBase).Values.Stats);
        Assert.Equal(new Stats(100, 10, 30, 20, 40, 50), userBase.Stats);
    }

    [Fact]
    public void AverageFloorsOddValuesAndSwapFeedsTheSharedSpeedQuery()
    {
        var overlays = new BattleOverlayStore();
        var state = new BattleStatMetricState(overlays);
        BattleEffectiveValues userBase = Values(new Stats(100, 11, 30, 20, 40, 31), 100, 10);
        BattleEffectiveValues targetBase = Values(new Stats(100, 14, 50, 26, 60, 50), 40, 5);

        state.Mutate(new DerivedStatMutationEffect(BattleDerivedStatOperation.Average, StatKind.Atk),
            User, userBase, false, Target, targetBase, false, 0, 0);
        state.Mutate(new DerivedStatMutationEffect(BattleDerivedStatOperation.Swap, StatKind.Spe),
            User, userBase, false, Target, targetBase, false, 0, 1);

        Assert.Equal(12, overlays.Resolve(User, userBase).Values.Stats.Atk);
        Assert.Equal(12, overlays.Resolve(Target, targetBase).Values.Stats.Atk);
        BattleCreature user = Creature("query_user", Wait(), new Stats(100, 11, 30, 20, 40, 31));
        BattleCreature target = Creature("query_target", Wait(), new Stats(100, 14, 50, 26, 60, 50));
        PhysicalFormulaInputs inputs = PhysicalMetricFormulas.Inputs(user, target, overlays, User, Target);
        Assert.Equal(50, inputs.SourceSpeed);
        Assert.Equal(31, inputs.TargetSpeed);
    }

    [Fact]
    public void MetricReplaceAddSwapClampAndCleanupUseEffectiveValuesAtomically()
    {
        var overlays = new BattleOverlayStore();
        var state = new BattleStatMetricState(overlays);
        BattleEffectiveValues userBase = Values(new Stats(100, 10, 10, 10, 10, 10), 100, 10);
        BattleEffectiveValues targetBase = Values(new Stats(100, 10, 10, 10, 10, 10), 40, 5);

        state.Mutate(new MetricMutationEffect(BattleMetricMutationOperation.Replace,
                StageEffectScope.Self, BattleMetric.Weight, 80, 1),
            User, userBase, false, Target, targetBase, false, 0, 0);
        state.Mutate(new MetricMutationEffect(BattleMetricMutationOperation.Add,
                StageEffectScope.Target, BattleMetric.Weight, -100),
            User, userBase, false, Target, targetBase, false, 0, 1);
        MetricMutationResult swap = state.Mutate(new MetricMutationEffect(BattleMetricMutationOperation.Swap,
                StageEffectScope.Self, BattleMetric.Height),
            User, userBase, false, Target, targetBase, false, 0, 2);

        Assert.True(swap.Succeeded);
        Assert.Equal(80, overlays.Resolve(User, userBase).Values.Metrics[BattleMetric.Weight]);
        Assert.Equal(1, overlays.Resolve(Target, targetBase).Values.Metrics[BattleMetric.Weight]);
        Assert.Equal(5, overlays.Resolve(User, userBase).Values.Metrics[BattleMetric.Height]);
        Assert.Equal(10, overlays.Resolve(Target, targetBase).Values.Metrics[BattleMetric.Height]);

        overlays.CompleteCheckpoint(BattleIntentCheckpoint.TurnEnd, 1, 0);
        Assert.Equal(100, overlays.Resolve(User, userBase).Values.Metrics[BattleMetric.Weight]);
        overlays.OwnerSwitched(BattleSide.Player, 0, null, 1, 1);
        Assert.Equal(10, overlays.Resolve(User with { Slot = null }, userBase).Values.Metrics[BattleMetric.Height]);
        overlays.OwnerFainted(BattleSide.Enemy, 0, 1, 2);
        Assert.Equal(5, overlays.Resolve(Target, targetBase).Values.Metrics[BattleMetric.Height]);
        overlays.OwnerSwitched(BattleSide.Player, 0, User.Slot, 1, 3);
        state.Mutate(new MetricMutationEffect(BattleMetricMutationOperation.Replace,
                StageEffectScope.Self, BattleMetric.Weight, 70),
            User, userBase, false, Target, targetBase, false, 1, 4);
        overlays.EndBattle(1, 5);
        Assert.Equal(100, overlays.Resolve(User with { Slot = null }, userBase).Values.Metrics[BattleMetric.Weight]);
    }

    [Fact]
    public void OverflowAndFaintedPreflightLeaveTheOverlayStoreUnchanged()
    {
        var overlays = new BattleOverlayStore();
        var state = new BattleStatMetricState(overlays);
        BattleEffectiveValues maximum = Values(new Stats(100, 10, 10, 10, 10, 10), int.MaxValue, 1);
        BattleEffectiveValues target = Values(new Stats(100, 10, 10, 10, 10, 10), 1, 1);

        MetricMutationResult overflow = state.Mutate(new MetricMutationEffect(BattleMetricMutationOperation.Add,
                StageEffectScope.Self, BattleMetric.Weight, 1),
            User, maximum, false, Target, target, false, 0, 0);
        DerivedStatMutationResult fainted = state.Mutate(
            new DerivedStatMutationEffect(BattleDerivedStatOperation.Split, Group: BattleDerivedStatGroup.Defense),
            User, maximum, false, Target, target, true, 0, 1);

        Assert.Equal(BattleStatMetricFailure.Overflow, overflow.Failure);
        Assert.Equal(BattleStatMetricFailure.Fainted, fainted.Failure);
        Assert.Empty(overlays.Snapshot());

        Assert.Throws<ArgumentException>(() => overlays.ApplyMany([
            new BattleOverlayApplication(User, new(), BattleOverlayLayer.Additive,
                new DerivedStatOverlay(StatKind.Atk, 20), 0, 2),
            new BattleOverlayApplication(Target, new(), BattleOverlayLayer.Additive,
                new MetricValueOverlay(BattleMetric.Weight, 0), 0, 2),
        ]));
        Assert.Empty(overlays.Snapshot());
    }

    [Fact]
    public void ControllerEmitsStableDerivedAndMetricEventsAndTypedTraces()
    {
        BattleMove move = Compile("controller",
            Op("derivedStatMutation", ("operation", "average"), ("stat", "atk")),
            Op("metricMutation", ("operation", "swap"), ("metric", "weight")));
        BattleCreature user = Creature("controller_user", move,
            new Stats(100, 11, 10, 10, 10, 100), weight: 100);
        BattleCreature target = Creature("controller_target", Wait(),
            new Stats(100, 14, 10, 10, 10, 10), weight: 40);
        var battle = new BattleController(user, target,
            new TypeChart([new TypeDef { Id = Normal }]), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Equal(2, events.OfType<DerivedStatMutated>().Count());
        Assert.Equal(2, events.OfType<MetricMutated>().Count());
        Assert.Collection(battle.Trace.Where(trace => trace.Kind is EffectTraceKind.DerivedStatMutation
                or EffectTraceKind.MetricMutation),
            trace => Assert.Equal(2, trace.Value),
            trace => Assert.Equal(2, trace.Value));
        Assert.Equal(40, battle.Overlays.Resolve(User,
            PhysicalMetricFormulas.BaseEffectiveValues(user)).Values.Metrics[BattleMetric.Weight]);
    }

    [Fact]
    public void StatMutationFamilyMatchesDeterministicLifecycleGolden()
    {
        static string Run()
        {
            BattleMove move = MoveCompiler.ToBattleMove(new Move
            {
                Id = EntityId.Parse("move:stat_mutation_replay"), Name = "stat mutation replay", Type = Normal,
                DamageClass = DamageClass.Physical, Power = 40, Pp = 10, Target = MoveTarget.Selected,
                Effects =
                [
                    Op("damage"),
                    Op("statStageMutation", ("operation", "steal"), ("subject", "target")),
                    Op("derivedStatMutation", ("operation", "split"), ("group", "offense")),
                    Op("metricMutation", ("operation", "add"), ("subject", "self"),
                        ("metric", "weight"), ("value", -10)),
                ],
            });
            BattleCreature user = Creature("replay_user", move,
                new Stats(200, 80, 100, 100, 100, 100), weight: 100);
            BattleCreature target = Creature("replay_target", Wait(),
                new Stats(200, 120, 100, 100, 100, 10), weight: 40);
            target.SetStage(StatKind.Atk, 2);
            var battle = new BattleController(user, target,
                new TypeChart([new TypeDef { Id = Normal }]), new Rng(7));

            IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());
            return string.Join('\n', events.Select(EventRow)
                .Concat(battle.Trace.Where(trace => trace.Kind is EffectTraceKind.StatStageMutation
                        or EffectTraceKind.DerivedStatMutation or EffectTraceKind.MetricMutation)
                    .Select(trace => $"trace:{trace.Kind}:{trace.SourceSlot.Side}:{trace.TargetSlot?.Side}:"
                        + $"{trace.Performed}:{trace.Value}:{trace.EventStartIndex}-{trace.EventEndIndex}")));
        }

        string first = Run();
        Assert.Equal(first, Run());
        Assert.Equal(Golden("stat-mutation"), first);
    }

    private static BattleEffectiveValues Values(Stats stats, int weight, int height) => new(
        null, null, [Normal], stats, [BattleEffectiveMove.FromBase(Wait(), 0)],
        metrics: new Dictionary<BattleMetric, int>
        {
            [BattleMetric.Weight] = weight,
            [BattleMetric.Height] = height,
        });

    private static BattleCreature Creature(string slug, BattleMove move, Stats stats,
        int weight = 1, int height = 1) => new(EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
        stats, [move], weightHectograms: weight, heightDecimeters: height);

    private static BattleMove Wait() => new(EntityId.Parse("move:wait"), Normal,
        DamageClass.Status, null, null, 10, 0, 0);

    private static BattleMove Compile(string slug, params Effect[] effects) =>
        MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse($"move:{slug}"), Name = slug, Type = Normal,
            DamageClass = DamageClass.Status, Pp = 10, Target = MoveTarget.Selected, Effects = effects,
        });

    private static string EventRow(BattleEvent battleEvent) => battleEvent switch
    {
        MoveUsed used => $"used:{used.Slot.Side}:{used.Move}",
        StatStageChanged changed => $"stage:{changed.Slot.Side}:{changed.Stat}:{changed.Delta}",
        DamageDealt damage => $"damage:{damage.Slot.Side}:{damage.Amount}",
        DerivedStatMutated changed => $"derived:{changed.Side}:{changed.Stat}:"
            + $"{changed.Before}->{changed.After}:{changed.Operation}",
        MetricMutated changed => $"metric:{changed.Side}:{changed.Metric}:"
            + $"{changed.Before}->{changed.After}:{changed.Operation}",
        _ => $"event:{battleEvent.GetType().Name}",
    };

    private static string Golden(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden"))
        .Replace("\r\n", "\n").TrimEnd();

    private static Effect Op(string op, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Params = values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static Effect Op(string op, int chance, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Chance = chance,
        Params = values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value)),
    };
}
