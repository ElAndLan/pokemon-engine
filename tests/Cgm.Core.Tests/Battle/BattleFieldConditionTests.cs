using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleFieldConditionTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Electric = EntityId.Parse("type:electric");
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Flying = EntityId.Parse("type:flying");

    private static TypeChart Chart() => new([
        new TypeDef { Id = Normal }, new TypeDef { Id = Electric },
        new TypeDef { Id = Fire }, new TypeDef { Id = Flying },
    ]);

    private static BattleMove Field(BattleFieldCondition condition, string slug, int duration = 5) =>
        new(EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0,
            target: MoveTarget.EntireField,
            secondaryEffects: [new SetFieldConditionEffect(condition, duration)]);

    private static BattleMove Hit(EntityId type, string slug, DamageClass damageClass = DamageClass.Special,
        params MoveEffect[] effects) => new(EntityId.Parse($"move:{slug}"), type, damageClass, 90, 100, 20, 0, 0,
            secondaryEffects: effects);

    private static BattleMove Inert(string slug = "inert") =>
        new(EntityId.Parse($"move:{slug}"), Normal, DamageClass.Status, null, null, 20, 0, 0);

    private static BattleCreature Creature(string slug, int speed, Stats? stats = null,
        IReadOnlyList<EntityId>? types = null, IReadOnlyList<Effect>? held = null, params BattleMove[] moves) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, types ?? [Normal],
            stats ?? new Stats(400, 100, 100, 100, 100, speed), moves,
            heldItemBattleEffects: held);

    [Fact]
    public void Compiler_AdmitsStrictFieldConditionAndGravityGateRows()
    {
        Move room = DataMove("fieldCondition", ("condition", "trickRoom")) with
            { Target = MoveTarget.EntireField };
        Assert.Contains(MoveCompiler.ToBattleMove(room).SecondaryEffects,
            effect => effect is SetFieldConditionEffect { Condition: BattleFieldCondition.TrickRoom, Duration: 5 });

        Move blocked = DataMove("fieldMoveGate", ("condition", "gravity"));
        Assert.Contains(MoveCompiler.ToBattleMove(blocked).SecondaryEffects,
            effect => effect is FieldMoveGateEffect { Condition: BattleFieldCondition.Gravity });
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(
            DataMove("fieldCondition", ("condition", "magicRoom"))));
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(
            DataMove("fieldMoveGate", ("condition", "wonderRoom"))));
    }

    [Fact]
    public void Rooms_CoexistToggleAndExpireThroughTheSharedStore()
    {
        BattleCreature source = Creature("source", 100, moves:
            [Field(BattleFieldCondition.TrickRoom, "trick", 3),
             Field(BattleFieldCondition.WonderRoom, "wonder", 3)]);
        var battle = new BattleController(source, Creature("target", 1, moves: [Inert()]), Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));
        Assert.Equal(2, battle.ConditionSnapshot.Count);
        Assert.Contains(battle.ConditionSnapshot, row => row.Definition.Id == new BattleConditionId("room:trick"));
        Assert.Contains(battle.ConditionSnapshot, row => row.Definition.Id == new BattleConditionId("room:wonder"));

        battle.ResolveTurn(new UseMove(1), new UseMove(0));
        Assert.Empty(battle.ConditionSnapshot);
        Assert.Contains(battle.ConditionTrace, row => row.Kind == BattleConditionTraceKind.Expired);
        Assert.Contains(battle.ConditionTrace, row => row.Kind == BattleConditionTraceKind.Removed);
    }

    [Fact]
    public void TrickRoom_ReversesSpeedButNotPriority()
    {
        BattleMove priority = new(EntityId.Parse("move:priority"), Normal, DamageClass.Special,
            90, 100, 20, 1, 0);
        BattleCreature fast = Creature("fast", 200, moves:
            [Field(BattleFieldCondition.TrickRoom, "trick"), Hit(Normal, "fast_hit"), priority]);
        BattleCreature slow = Creature("slow", 20, moves: [Hit(Normal, "slow_hit")]);
        var battle = new BattleController(fast, slow, Chart(), new Rng(4));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        EntityId[] order = battle.ResolveTurn(new UseMove(1), new UseMove(0))
            .OfType<MoveUsed>().Select(row => row.Move).ToArray();

        Assert.Equal([EntityId.Parse("move:slow_hit"), EntityId.Parse("move:fast_hit")], order);

        EntityId[] priorityOrder = battle.ResolveTurn(new UseMove(2), new UseMove(0))
            .OfType<MoveUsed>().Select(row => row.Move).ToArray();
        Assert.Equal(EntityId.Parse("move:priority"), priorityOrder[0]);
    }

    [Fact]
    public void WonderRoom_SwapsDefensiveStatAndStageRouting()
    {
        int normal = DamageWithOptionalField(null, BattleRulesets.ModernReference,
            new Stats(400, 100, 40, 100, 240, 10), Electric);
        int wonder = DamageWithOptionalField(BattleFieldCondition.WonderRoom, BattleRulesets.ModernReference,
            new Stats(400, 100, 40, 100, 240, 10), Electric);
        Assert.True(wonder > normal);
    }

    [Fact]
    public void MagicRoom_SuppressesHeldGroundingWithoutDeletingTheItemEffect()
    {
        IReadOnlyList<Effect> grounding = [new Effect
        {
            Op = "groundedModify",
            Params = Params(("state", "grounded")),
        }];
        BattleCreature source = Creature("source", 100, moves:
            [Field(BattleFieldCondition.MagicRoom, "magic"), Inert("source_inert")]);
        BattleCreature target = Creature("target", 1, types: [Flying], held: grounding,
            moves: [Inert(), Inert("alternate")]);
        target.SetChoiceLock(0);
        var battle = new BattleController(source, target, Chart(), new Rng(1));

        Assert.True(battle.IsGrounded(new BattleSlot(BattleSide.Enemy, 0)));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.False(battle.IsGrounded(new BattleSlot(BattleSide.Enemy, 0)));
        Assert.Single(target.HeldItemBattleEffects);
        Assert.Contains(battle.ResolveTurn(new UseMove(1), new UseMove(1)),
            row => row is MoveUsed { Move: var move } && move == EntityId.Parse("move:alternate"));
    }

    [Fact]
    public void Gravity_GroundsBoostsAccuracyAndBlocksMarkedMovesBeforePp()
    {
        BattleMove gravity = Field(BattleFieldCondition.Gravity, "gravity");
        BattleMove accurate = new(EntityId.Parse("move:accurate"), Normal, DamageClass.Special,
            40, 60, 20, 0, 0);
        BattleMove blocked = Hit(Normal, "blocked", DamageClass.Special,
            new FieldMoveGateEffect(BattleFieldCondition.Gravity));
        BattleCreature source = Creature("source", 100, moves: [gravity, accurate, blocked]);
        BattleCreature target = Creature("target", 1, types: [Flying], moves: [Inert()]);
        var battle = new BattleController(source, target, Chart(), new Rng(3));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.True(battle.IsGrounded(new BattleSlot(BattleSide.Enemy, 0)));
        battle.ResolveTurn(new UseMove(1), new UseMove(0));
        Assert.Equal(100, battle.QueryTrace.Last(row => row.Result.Query == BattleQueryId.Accuracy
            && row.SourceSlot.Side == BattleSide.Player).Result.FinalValue.ToInt32());
        int pp = blocked.Pp;

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(2), new UseMove(0));

        Assert.Equal(pp, blocked.Pp);
        Assert.Contains(events, row => row is MoveFailed { Reason: MoveFailureReason.FieldConditionBlocked });

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(1, Assert.Single(battle.ConditionSnapshot).RemainingDuration);
        Assert.Contains(battle.ConditionTrace, row => row.Kind == BattleConditionTraceKind.Rejected);
    }

    [Theory]
    [InlineData(BattleRulesets.Gen4Like, 2)]
    [InlineData(BattleRulesets.ModernReference, 3)]
    public void Sports_ApplyRulesetBasePowerVectors(string ruleset, int denominator)
    {
        int clear = DamageWithOptionalField(null, ruleset, new Stats(400, 100, 100, 100, 100, 10), Electric);
        int sport = DamageWithOptionalField(BattleFieldCondition.MudSport, ruleset,
            new Stats(400, 100, 100, 100, 100, 10), Electric);
        Assert.InRange(sport, clear / denominator - 1, clear / denominator + 1);
    }

    [Fact]
    public void ClassicSport_IsSourceBoundWhileModernSportExpiresByDuration()
    {
        BattleCreature classicSource = Creature("classic_source", 100, moves:
            [Field(BattleFieldCondition.MudSport, "sport")]);
        var classic = new BattleController([classicSource, Creature("reserve", 90, moves: [Inert("reserve_move")])],
            [Creature("target", 1, moves: [Inert()])], Chart(), new Rng(1),
            fieldInputs: new BattleFieldInputs(BattleRulesets.Gen4Like));
        classic.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(new BattleConditionId("field:mud_sport_classic"),
            Assert.Single(classic.ConditionSnapshot).Definition.Id);

        classic.ResolveTurn(new Switch(1), new UseMove(0));

        Assert.Empty(classic.ConditionSnapshot);
        Assert.Contains(classic.ConditionTrace, row => row.CleanupReason == BattleConditionCleanupReason.Switch);

        var modern = new BattleController(Creature("modern", 100, moves:
            [Field(BattleFieldCondition.MudSport, "modern_sport", 1)]),
            Creature("modern_target", 1, moves: [Inert("modern_inert")]), Chart(), new Rng(1),
            fieldInputs: new BattleFieldInputs(BattleRulesets.ModernReference));
        modern.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Empty(modern.ConditionSnapshot);
        Assert.Contains(modern.ConditionTrace, row => row.Kind == BattleConditionTraceKind.Expired);
    }

    [Fact]
    public void SmartAi_UsesGravityAccuracyAndSportDamageQueries()
    {
        BattleCreature attacker = Creature("ai", 100, moves: [Hit(Electric, "electric")]);
        BattleCreature defender = Creature("defender", 1, moves: [Inert()]);
        double clear = Score(attacker, defender, []);
        double sport = Score(attacker, defender, [Condition(BattleFieldCondition.MudSport)]);
        Assert.True(sport < clear);

        BattleMove inaccurate = new(EntityId.Parse("move:inaccurate"), Normal, DamageClass.Special,
            90, 60, 20, 0, 0);
        BattleCreature gravityUser = Creature("gravity_ai", 100, moves: [inaccurate]);
        double ordinaryAccuracy = Score(gravityUser, defender, []);
        double gravityAccuracy = Score(gravityUser, defender, [Condition(BattleFieldCondition.Gravity)]);
        Assert.True(gravityAccuracy > ordinaryAccuracy);

        BattleCreature unusualDefense = Creature("unusual", 1,
            stats: new Stats(400, 100, 40, 100, 240, 1), moves: [Inert("unusual_inert")]);
        Assert.True(Score(attacker, unusualDefense, [Condition(BattleFieldCondition.WonderRoom)])
            > Score(attacker, unusualDefense, []));

        BattleMove gated = Hit(Normal, "gated", DamageClass.Special,
            new FieldMoveGateEffect(BattleFieldCondition.Gravity));
        SmartAiDecision decision = SmartAi.ChooseAction(new SmartAiContext(
            [Creature("gated_ai", 100, moves: [gated])], 0, [defender], 0, Chart(), new Rng(1),
            Weights: new SmartAiWeights { NoiseFraction = 0 },
            Conditions: [Condition(BattleFieldCondition.Gravity)]));
        Assert.Equal(-1_000_000, Assert.Single(decision.Scores).Score);
    }

    [Fact]
    public void FieldConditionReplay_IsDeterministic()
    {
        (IReadOnlyList<BattleEvent> Events, IReadOnlyList<BattleConditionTraceEntry> Trace) Run()
        {
            var battle = new BattleController(
                Creature("source", 100, moves: [Field(BattleFieldCondition.TrickRoom, "trick")]),
                Creature("target", 10, moves: [Inert()]), Chart(), new Rng(123));
            battle.ResolveTurn(new UseMove(0), new UseMove(0));
            battle.ResolveTurn(new UseMove(0), new UseMove(0));
            return (battle.Log.ToArray(), battle.ConditionTrace.ToArray());
        }

        var first = Run();
        var second = Run();
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(first.Trace, second.Trace);
    }

    private static int DamageWithOptionalField(BattleFieldCondition? condition, string ruleset,
        Stats targetStats, EntityId type)
    {
        var moves = new List<BattleMove>();
        if (condition is { } field)
            moves.Add(Field(field, "field"));
        moves.Add(Hit(type, "hit"));
        BattleCreature source = Creature("source", 100, moves: [.. moves]);
        BattleCreature target = Creature("target", 1, stats: targetStats, moves: [Inert()]);
        var battle = new BattleController(source, target, Chart(), new Rng(9),
            fieldInputs: new BattleFieldInputs(ruleset));
        if (condition is not null)
            battle.ResolveTurn(new UseMove(0), new UseMove(0));
        int before = target.CurrentHp;
        battle.ResolveTurn(new UseMove(condition is null ? 0 : 1), new UseMove(0));
        return before - target.CurrentHp;
    }

    private static double Score(BattleCreature attacker, BattleCreature defender,
        IReadOnlyList<BattleConditionInstance> conditions) => SmartAi.ChooseAction(new SmartAiContext(
            [attacker], 0, [defender], 0, Chart(), new Rng(1), Weights: new SmartAiWeights { NoiseFraction = 0 },
            Conditions: conditions, Ruleset: BattleRulesets.ModernReference)).Scores.Single().Components
            .Single(component => component.Name == "damage").Value;

    private static BattleConditionInstance Condition(BattleFieldCondition condition) => new(0,
        FieldConditions.For(condition), FieldConditions.For(condition).Scope == BattleConditionScope.Room
            ? FieldConditions.RoomOwner : FieldConditions.FieldOwner,
        new BattleConditionSource(), 0, 0, 5,
        [], new Dictionary<string, int>(), 1);

    private static Move DataMove(string op, params (string Key, object Value)[] values) => new()
    {
        Id = EntityId.Parse("move:data"), Name = "Data", Type = Normal,
        DamageClass = DamageClass.Status, Accuracy = 100, Pp = 10,
        Effects = [new Effect { Op = op, Params = Params(values) }],
    };

    private static IReadOnlyDictionary<string, JsonElement> Params(params (string Key, object Value)[] values) =>
        values.ToDictionary(row => row.Key, row => JsonSerializer.SerializeToElement(row.Value));
}
