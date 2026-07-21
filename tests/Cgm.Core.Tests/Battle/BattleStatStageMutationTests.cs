using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleStatStageMutationTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    [Fact]
    public void CompilerAdmitsClosedStageMutationVocabularyAndRejectsMalformedRows()
    {
        Assert.IsType<StatStageMutationEffect>(Assert.Single(Compile("maximize", MoveTarget.User,
            Op("statStageMutation", ("operation", "maximize"), ("stat", "atk"))).SecondaryEffects));
        Assert.IsType<StatStageMutationEffect>(Assert.Single(Compile("random", MoveTarget.User,
            Op("statStageMutation", ("operation", "random"), ("delta", 2))).SecondaryEffects));
        Assert.IsType<StatStageMutationEffect>(Assert.Single(Compile("steal", MoveTarget.Selected,
            Op("statStageMutation", ("operation", "steal"), ("subject", "target"))).SecondaryEffects));

        Assert.Throws<ArgumentException>(() => Compile("chance", MoveTarget.User,
            Op("statStageMutation", 50, ("operation", "random"), ("delta", 2))));
        Assert.Throws<ArgumentException>(() => Compile("missing_stat", MoveTarget.User,
            Op("statStageMutation", ("operation", "maximize"))));
        Assert.Throws<ArgumentException>(() => Compile("bad_delta", MoveTarget.User,
            Op("statStageMutation", ("operation", "random"), ("delta", 0))));
        Assert.Throws<ArgumentException>(() => Compile("steal_self", MoveTarget.Selected,
            Op("statStageMutation", ("operation", "steal"), ("subject", "self"))));
        Assert.Throws<ArgumentException>(() => Compile("target_without_target", MoveTarget.User,
            Op("statStageMutation", ("operation", "random"), ("subject", "target"), ("delta", 2))));
    }

    [Fact]
    public void MaximizeUsesTheSharedClampAndEmitsOnlyTheActualDelta()
    {
        BattleMove move = Compile("maximize", MoveTarget.User,
            Op("statStageMutation", ("operation", "maximize"), ("stat", "atk")));
        BattleCreature user = Creature("user", 100, move);
        user.SetStage(StatKind.Atk, 2);
        var rng = new CountingRng(0);
        var battle = Battle(user, Creature("target", 10, Wait()), rng);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Equal(StatStages.Max, user.Stage(StatKind.Atk));
        Assert.Equal(4, Assert.Single(events.OfType<StatStageChanged>()).Delta);
        EffectTraceEntry trace = Assert.Single(battle.Trace, row => row.Kind == EffectTraceKind.StatStageMutation);
        Assert.True(trace.Performed);
        Assert.Null(trace.DrawResult);
        Assert.Equal(0, rng.Calls);

        IReadOnlyList<BattleEvent> capped = battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Empty(capped.OfType<StatStageChanged>());
        Assert.False(battle.Trace.Last(row => row.Kind == EffectTraceKind.StatStageMutation).Performed);
    }

    [Fact]
    public void DamagingStealAppliesBeforeTheSameHitsDamageQueryAndOnlyOnce()
    {
        BattleMove move = MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse("move:pre_damage_steal"), Name = "pre damage steal", Type = Normal,
            DamageClass = DamageClass.Physical, Power = 40, Accuracy = null, Pp = 10,
            Target = MoveTarget.Selected,
            Effects = [Op("damage"), Op("statStageMutation", ("operation", "steal"), ("subject", "target"))],
        });
        BattleCreature user = Creature("steal_user", 100, move);
        BattleCreature target = Creature("steal_target", 10, Wait());
        target.SetStage(StatKind.Atk, 6);
        var battle = Battle(user, target, new CountingRng(0));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Equal(6, user.Stage(StatKind.Atk));
        Assert.Equal(0, target.Stage(StatKind.Atk));
        int stageIndex = events.ToList().FindIndex(row => row is StatStageChanged);
        int damageIndex = events.ToList().FindIndex(row => row is DamageDealt);
        Assert.InRange(stageIndex, 0, damageIndex - 1);
        Assert.Equal(2, events.OfType<StatStageChanged>().Count());
        Assert.Single(battle.Trace, row => row.Kind == EffectTraceKind.StatStageMutation);
        Assert.True(events.OfType<DamageDealt>().Single().Amount > 50);
    }

    [Fact]
    public void RandomStageUsesEnumOrderAndExactZeroOneManyDrawPolicy()
    {
        BattleMove move = Compile("random", MoveTarget.User,
            Op("statStageMutation", ("operation", "random"), ("delta", 2)));

        BattleCreature many = Creature("many", 100, move);
        var manyRng = new CountingRng(6);
        var manyBattle = Battle(many, Creature("many_target", 10, Wait()), manyRng);
        manyBattle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Equal(2, many.Stage(StatKind.Evasion));
        EffectTraceEntry manyTrace = Assert.Single(manyBattle.Trace,
            row => row.Kind == EffectTraceKind.StatStageMutation);
        Assert.Equal(6, manyTrace.DrawResult);
        Assert.Equal(7, manyTrace.DrawBound);
        Assert.Equal(1, manyRng.Calls);

        BattleCreature one = Creature("one", 100, move);
        foreach (StatKind stat in StageStats().Where(stat => stat != StatKind.Spe))
            one.SetStage(stat, StatStages.Max);
        var oneRng = new CountingRng(0);
        var oneBattle = Battle(one, Creature("one_target", 10, Wait()), oneRng);
        oneBattle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Equal(2, one.Stage(StatKind.Spe));
        Assert.Equal(0, oneRng.Calls);

        BattleCreature zero = Creature("zero", 100, move);
        foreach (StatKind stat in StageStats())
            zero.SetStage(stat, StatStages.Max);
        var zeroRng = new CountingRng(0);
        var zeroBattle = Battle(zero, Creature("zero_target", 10, Wait()), zeroRng);
        IReadOnlyList<BattleEvent> zeroEvents = zeroBattle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Empty(zeroEvents.OfType<StatStageChanged>());
        Assert.False(Assert.Single(zeroBattle.Trace,
            row => row.Kind == EffectTraceKind.StatStageMutation).Performed);
        Assert.Equal(0, zeroRng.Calls);
    }

    [Fact]
    public void StealSnapshotsAllSlotsAndCommitsPositiveStagesAtomicallyWithClamp()
    {
        BattleMove move = Compile("steal", MoveTarget.Selected,
            Op("statStageMutation", ("operation", "steal"), ("subject", "target")));
        BattleCreature user = Creature("user", 100, move);
        BattleCreature target = Creature("target", 10, Wait());
        user.SetStage(StatKind.Atk, 5);
        user.SetStage(StatKind.Spa, -6);
        target.SetStage(StatKind.Atk, 4);
        target.SetStage(StatKind.Def, -2);
        target.SetStage(StatKind.Spa, 3);
        var battle = Battle(user, target, new CountingRng(0));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Equal(6, user.Stage(StatKind.Atk));
        Assert.Equal(-3, user.Stage(StatKind.Spa));
        Assert.Equal(0, target.Stage(StatKind.Atk));
        Assert.Equal(-2, target.Stage(StatKind.Def));
        Assert.Equal(0, target.Stage(StatKind.Spa));
        Assert.Equal(
            [(BattleSide.Enemy, StatKind.Atk, -4), (BattleSide.Player, StatKind.Atk, 1),
             (BattleSide.Enemy, StatKind.Spa, -3), (BattleSide.Player, StatKind.Spa, 3)],
            events.OfType<StatStageChanged>().Select(e => (e.Side, e.Stat, e.Delta)).ToArray());
        EffectTraceEntry trace = Assert.Single(battle.Trace, row => row.Kind == EffectTraceKind.StatStageMutation);
        Assert.Equal(4, trace.Value);
        Assert.Equal(4, trace.EventEndIndex - trace.EventStartIndex);

        IReadOnlyList<BattleEvent> empty = battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Empty(empty.OfType<StatStageChanged>());
        Assert.False(battle.Trace.Last(row => row.Kind == EffectTraceKind.StatStageMutation).Performed);
    }

    [Fact]
    public void SmartAiValuesVisibleSetupAndRejectsANoEffectOnlyMutation()
    {
        BattleMove weak = new(EntityId.Parse("move:weak"), Normal, DamageClass.Physical,
            5, 100, 10, 0, 0);
        BattleMove maximize = Compile("maximize", MoveTarget.User,
            Op("statStageMutation", ("operation", "maximize"), ("stat", "atk")));
        BattleCreature user = new(EntityId.Parse("species:ai"), "AI", 50, [Normal],
            new Stats(200, 100, 100, 100, 100, 100), [weak, maximize]);
        BattleCreature target = Creature("ai_target", 10, Wait());
        SmartAiContext Context() => new([user], 0, [target], 0,
            new TypeChart([new TypeDef { Id = Normal }]), new Rng(1),
            Weights: new SmartAiWeights { NoiseFraction = 0 });

        Assert.Equal(new UseMove(1), SmartAi.ChooseAction(Context()).Action);

        user.SetStage(StatKind.Atk, StatStages.Max);
        SmartAiDecision capped = SmartAi.ChooseAction(Context());
        Assert.Equal(new UseMove(0), capped.Action);
        Assert.Contains(Assert.Single(capped.Scores, score => score.Action == new UseMove(1)).Components,
            component => component.Name == "statStageMutationNoEffect" && component.Value < 0);
    }

    private static StatKind[] StageStats() =>
        [StatKind.Atk, StatKind.Def, StatKind.Spa, StatKind.Spd, StatKind.Spe, StatKind.Accuracy, StatKind.Evasion];

    private static BattleController Battle(BattleCreature user, BattleCreature target, IRng rng) =>
        new(user, target, new TypeChart([new TypeDef { Id = Normal }]), rng);

    private static BattleCreature Creature(string slug, int speed, BattleMove move) => new(
        EntityId.Parse($"species:{slug}"), slug, 50, [Normal], new Stats(200, 100, 100, 100, 100, speed), [move]);

    private static BattleMove Wait() => new(EntityId.Parse("move:wait"), Normal,
        DamageClass.Status, null, null, 10, 0, 0);

    private static BattleMove Compile(string slug, MoveTarget target, params Effect[] effects) =>
        MoveCompiler.ToBattleMove(new Move
        {
            Id = EntityId.Parse($"move:{slug}"), Name = slug, Type = Normal,
            DamageClass = DamageClass.Status, Pp = 10, Target = target, Effects = effects,
        });

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

    private sealed class CountingRng(int result) : IRng
    {
        public int Calls { get; private set; }
        public int Next(int maxExclusive)
        {
            Calls++;
            return result;
        }
        public int Next(int minInclusive, int maxExclusive) => minInclusive + Next(maxExclusive - minInclusive);
        public double NextDouble() => 0;
    }
}
