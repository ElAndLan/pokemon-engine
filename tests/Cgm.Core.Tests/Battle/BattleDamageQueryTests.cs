using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleDamageQueryTests
{
    private static readonly EntityId Neutral = EntityId.Parse("type:neutral");
    private static readonly EntityId Flare = EntityId.Parse("type:flare");
    private static readonly EntityId Tide = EntityId.Parse("type:tide");
    private static readonly EntityId Leaf = EntityId.Parse("type:leaf");
    private static readonly EntityId Air = EntityId.Parse("type:air");

    [Fact]
    public void Compiler_AdmitsClosedDamageQueryRows()
    {
        BattleMove move = Compile(
            Op("damageStatOverride", ("offensiveStat", "atk"), ("offensiveOwner", "target"),
                ("defensiveStat", "spd"), ("defensiveOwner", "user")),
            Op("damageClassQuery", ("mode", "special")),
            Op("effectivenessQuery", ("mode", "inverse"), ("additionalType", "type:air"),
                ("defendingType", "type:tide"), ("num", 2), ("den", 1), ("stabSource", "target")));

        Assert.Contains(move.SecondaryEffects, effect => effect is DamageStatQueryEffect
        {
            Offensive: { Owner: DamageQueryOwner.Target, Stat: StatKind.Atk },
            Defensive: { Owner: DamageQueryOwner.User, Stat: StatKind.Spd },
        });
        Assert.Contains(move.SecondaryEffects, effect => effect is DamageClassQueryEffect
            { Mode: DamageClassQueryMode.Special });
        Assert.Contains(move.SecondaryEffects, effect => effect is EffectivenessQueryEffect
        {
            Mode: EffectivenessQueryMode.Inverse,
            AdditionalType: var additional,
            DefendingType: var defending,
            StabSource: StabQuerySource.Target,
        } && additional == Air && defending == Tide);
    }

    [Fact]
    public void Compiler_RejectsMalformedAndIncompatibleRows()
    {
        Assert.Throws<ArgumentException>(() => Compile(Op("damageStatOverride",
            ("offensiveOwner", "target"))));
        Assert.Throws<ArgumentException>(() => Compile(Op("damageClassQuery", ("mode", "status"))));
        Assert.Throws<ArgumentException>(() => Compile(Op("effectivenessQuery")));
        Assert.Throws<ArgumentException>(() => Compile(Op("effectivenessQuery",
            ("defendingType", "type:tide"), ("num", 2))));
        Assert.Throws<ArgumentException>(() => Compile(Op("effectivenessQuery",
            ("additionalType", "move:not_a_type"))));
        Assert.Throws<ArgumentException>(() => Compile(Op("effectivenessQuery",
            ("defendingType", "type:tide"), ("num", 0), ("den", 1))));
        Assert.Throws<ArgumentException>(() => Compile(
            Op("damageClassQuery", ("mode", "higherOffense")),
            Op("damageStatOverride", ("offensiveStat", "atk"))));
        Assert.Throws<ArgumentException>(() => Compile(
            Op("damageClassQuery", ("mode", "physical")),
            Op("damageClassQuery", ("mode", "special"))));
    }

    [Fact]
    public void Identity_UsesOverlayThenConditionAndHigherStagedOffenseWithEnvironment()
    {
        BattleMove move = Move("identity", Neutral, DamageClass.Physical,
            new DamageClassQueryEffect(DamageClassQueryMode.HigherOffense));
        BattleCreature source = Creature("source", [Neutral], new Stats(200, 120, 80, 90, 80, 100), move);
        source.ChangeStage(StatKind.Spa, 2);
        var overlays = new BattleOverlayStore();
        var owner = new BattleOverlayOwner(BattleSide.Player, 0, new BattleSlot(BattleSide.Player, 0));
        overlays.Apply(new BattleOverlayApplication(owner, new BattleOverlaySource(),
            BattleOverlayLayer.FormOrSnapshot, new MoveTypeOverlay(0, Tide), 0, 0));
        overlays.Apply(new BattleOverlayApplication(owner, new BattleOverlaySource(),
            BattleOverlayLayer.FormOrSnapshot, new MoveClassOverlay(0, DamageClass.Physical), 0, 0));

        BattleMoveIdentityQueryResult result = BattleDamageQueries.Identity(move, 0, source,
            PhysicalMetricFormulas.EffectiveValues(source, overlays, owner),
            BattleEnvironmentState.Resolve(BattleEnvironment.Cave, Terrain.Grassy), Flare);

        Assert.Equal(Neutral, result.AuthoredType);
        Assert.Equal(Flare, result.EffectiveType);
        Assert.Equal(DamageClass.Special, result.EffectiveClass);
        Assert.Equal(BattleEnvironment.Cave, result.NaturalEnvironment);
        Assert.Equal(BattleEnvironment.GrassyTerrain, result.EffectiveEnvironment);
    }

    [Fact]
    public void Effectiveness_CoversStandardInverseAdditionalOverrideAndStabSources()
    {
        TypeChart chart = Chart();
        BattleMove move = Move("query", Flare, DamageClass.Special,
            new EffectivenessQueryEffect(EffectivenessQueryMode.Standard, Air, Tide,
                new BattleQueryValue(2), StabQuerySource.Target));
        BattleCreature source = Creature("source", [Flare], Stats(), move);
        BattleCreature target = Creature("target", [Tide, Leaf], Stats(), Wait());
        BattleEffectiveValues sourceValues = PhysicalMetricFormulas.BaseEffectiveValues(source);
        BattleEffectiveValues targetValues = PhysicalMetricFormulas.BaseEffectiveValues(target);
        BattleMoveIdentityQueryResult identity = BattleDamageQueries.Identity(move, 0, source,
            sourceValues, BattleEnvironmentState.Resolve(BattleEnvironment.Building));

        BattleDamageQueryResult standard = BattleDamageQueries.Resolve(move, identity, source, target,
            sourceValues, targetValues, chart, 1, new BattleQueryContext(Source: source, Target: target));
        BattleMove inverseMove = Move("inverse", Flare, DamageClass.Special,
            new EffectivenessQueryEffect(EffectivenessQueryMode.Inverse, null, null, null,
                StabQuerySource.None));
        BattleDamageQueryResult inverse = BattleDamageQueries.Resolve(inverseMove,
            BattleDamageQueries.Identity(inverseMove, 0, source,
                PhysicalMetricFormulas.BaseEffectiveValues(Creature("inverse_source", [Flare], Stats(), inverseMove)),
                BattleEnvironmentState.Resolve(BattleEnvironment.Building)),
            source, target, sourceValues, targetValues, chart, 1,
            new BattleQueryContext(Source: source, Target: target));

        Assert.Equal(new BattleQueryValue(4), standard.Effectiveness.FinalValue);
        Assert.Equal(new BattleQueryValue(1), standard.Stab);
        Assert.Equal(new BattleQueryValue(1), inverse.Stab);
        Assert.Equal(new BattleQueryValue(1), inverse.Effectiveness.FinalValue);
    }

    [Fact]
    public void Resolver_UsesTargetOwnedOffenseEffectiveStatsTypesAndClass()
    {
        BattleMove queryMove = Compile(
            Op("damageStatOverride", ("offensiveOwner", "target"), ("offensiveStat", "atk")));
        BattleCreature source = Creature("source", [Neutral], new Stats(300, 20, 80, 20, 80, 100), queryMove);
        BattleCreature target = Creature("target", [Neutral], new Stats(400, 180, 80, 20, 80, 1), Wait());
        var battle = new BattleController(source, target, Chart(), new FakeRng(ints: [15], doubles: [0.99]));
        var sourceOwner = new BattleOverlayOwner(BattleSide.Player, 0, new BattleSlot(BattleSide.Player, 0));
        battle.Overlays.Apply(new BattleOverlayApplication(sourceOwner, new BattleOverlaySource(),
            BattleOverlayLayer.FormOrSnapshot, new MoveTypeOverlay(0, Flare), 0, 0));
        battle.Overlays.Apply(new BattleOverlayApplication(sourceOwner, new BattleOverlaySource(),
            BattleOverlayLayer.FormOrSnapshot, new MoveClassOverlay(0, DamageClass.Special), 0, 0));
        battle.Overlays.Apply(new BattleOverlayApplication(
            new BattleOverlayOwner(BattleSide.Enemy, 0, new BattleSlot(BattleSide.Enemy, 0)),
            new BattleOverlaySource(), BattleOverlayLayer.FormOrSnapshot,
            new StatsOverlay(target.Stats with { Atk = 240 }), 0, 0));
        battle.Overlays.Apply(new BattleOverlayApplication(
            new BattleOverlayOwner(BattleSide.Enemy, 0, new BattleSlot(BattleSide.Enemy, 0)),
            new BattleOverlaySource(), BattleOverlayLayer.FormOrSnapshot,
            new CreatureTypesOverlay([Leaf]), 0, 0));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        BattleDamageQueryTraceEntry trace = Assert.Single(battle.DamageQueryTrace);
        Assert.Equal(Flare, trace.Result.Identity.EffectiveType);
        Assert.Equal(DamageClass.Special, trace.Result.Identity.EffectiveClass);
        Assert.Equal(new DamageStatSelector(DamageQueryOwner.Target, StatKind.Atk), trace.Result.Offensive);
        Assert.Equal(new BattleQueryValue(2), trace.Result.Effectiveness.FinalValue);
        Assert.True(target.CurrentHp < 300);
        Assert.Equal(DamageClass.Special, Assert.Single(battle.ActionHistory.DamageSnapshot()).DamageClass);
    }

    [Fact]
    public void DirectTypedQueryRejectsInvalidSelectorsAtTheSharedBoundary()
    {
        BattleMove move = Move("invalid_typed", Neutral, DamageClass.Physical,
            new DamageStatQueryEffect(new DamageStatSelector(DamageQueryOwner.User, StatKind.Spe), null));
        BattleCreature source = Creature("source", [Neutral], Stats(), move);
        BattleCreature target = Creature("target", [Neutral], Stats(), Wait());
        BattleEffectiveValues sourceValues = PhysicalMetricFormulas.BaseEffectiveValues(source);

        Assert.Throws<ArgumentOutOfRangeException>(() => BattleDamageQueries.Resolve(move,
            BattleDamageQueries.Identity(move, 0, source, sourceValues,
                BattleEnvironmentState.Resolve(BattleEnvironment.Building)), source, target, sourceValues,
            PhysicalMetricFormulas.BaseEffectiveValues(target), Chart(), 1,
            new BattleQueryContext(Source: source, Target: target)));
    }

    [Fact]
    public void SelfHpFractionDamageDoesNotQueryTheOpponentType()
    {
        BattleMove cost = new(EntityId.Parse("move:self_cost"), Flare, DamageClass.Physical,
            null, null, 10, 0, 0, secondaryEffects:
            [new HpFractionEffect(HpFractionRecipient.Self, HpFractionOperation.Damage,
                HpFractionBasis.MaxHp, new Fraction(1, 4))]);
        BattleCreature source = Creature("source", [Flare], Stats(speed: 100), cost);
        BattleCreature immuneTarget = Creature("target", [Air], Stats(), Wait());
        var battle = new BattleController(source, immuneTarget, Chart(), new FakeRng());

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(225, source.CurrentHp);
        Assert.Empty(battle.DamageQueryTrace);
    }

    [Fact]
    public void FinalImmunitySkipsCombatRngAndTypedOverrideCanRemoveIt()
    {
        BattleCreature immuneTarget = Creature("immune", [Air], Stats(), Wait());
        BattleMove blocked = Move("blocked", Flare, DamageClass.Special);
        var blockedBattle = new BattleController(Creature("source", [Flare], Stats(100), blocked), immuneTarget,
            Chart(), new FakeRng());
        blockedBattle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(immuneTarget.MaxHp, immuneTarget.CurrentHp);
        Assert.Equal(new BattleQueryValue(0), Assert.Single(blockedBattle.DamageQueryTrace)
            .Result.Effectiveness.FinalValue);

        BattleMove overrideMove = Move("override", Flare, DamageClass.Special,
            new EffectivenessQueryEffect(EffectivenessQueryMode.Standard, null, Air,
                new BattleQueryValue(2)));
        BattleCreature overriddenTarget = Creature("overridden", [Air], Stats(), Wait());
        var overrideBattle = new BattleController(Creature("source_two", [Flare], Stats(100), overrideMove),
            overriddenTarget, Chart(), new FakeRng(ints: [15], doubles: [0.99]));
        overrideBattle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.True(overriddenTarget.CurrentHp < overriddenTarget.MaxHp);
        Assert.Equal(new BattleQueryValue(2), Assert.Single(overrideBattle.DamageQueryTrace)
            .Result.Effectiveness.FinalValue);
    }

    [Fact]
    public void DoublesTracePinsSnapshottedSpreadForEachTarget()
    {
        BattleMove spread = new(EntityId.Parse("move:spread_query"), Neutral, DamageClass.Special,
            60, null, 10, 0, 0, target: MoveTarget.AllOpponents);
        var battle = new BattleController(
            [Creature("source", [Neutral], Stats(speed: 100), spread), Creature("ally", [Neutral], Stats(), Wait())],
            [Creature("target_a", [Neutral], Stats(), Wait()), Creature("target_b", [Neutral], Stats(), Wait())],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(),
            new FakeRng(ints: [15, 15], doubles: [0.99, 0.99]));

        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new(new BattleSlot(BattleSide.Player, 0), new UseMove(0)),
            new(new BattleSlot(BattleSide.Player, 1), new Pass()),
            new(new BattleSlot(BattleSide.Enemy, 0), new Pass()),
            new(new BattleSlot(BattleSide.Enemy, 1), new Pass()),
        ]));

        Assert.Equal(2, battle.DamageQueryTrace.Count);
        Assert.All(battle.DamageQueryTrace, trace => Assert.True(trace.Result.Spread));
    }

    [Fact]
    public void SmartAiUsesTheSnapshottedLiveTargetCountForSpread()
    {
        BattleMove spread = new(EntityId.Parse("move:ai_spread_query"), Neutral, DamageClass.Special,
            60, null, 10, 0, 0, target: MoveTarget.AllOpponents);
        BattleCreature source = Creature("source", [Neutral], Stats(), spread);
        BattleCreature target = Creature("target", [Neutral], Stats(), Wait());

        double singleTarget = DamageScore(new SmartAiContext([source], 0, [target], 0, Chart(),
            new FakeRng(doubles: [0.5]), Weights: new SmartAiWeights { NoiseFraction = 0 },
            ActiveSlotsPerSide: 2, SnapshottedLiveTargets: 1));
        double twoTargets = DamageScore(new SmartAiContext([source], 0, [target], 0, Chart(),
            new FakeRng(doubles: [0.5]), Weights: new SmartAiWeights { NoiseFraction = 0 },
            ActiveSlotsPerSide: 2, SnapshottedLiveTargets: 2));

        Assert.Equal(37, singleTarget);
        Assert.Equal(28, twoTargets);
    }

    [Fact]
    public void SmartAiAndResolverUseTheSameDamageQueryInputs()
    {
        BattleMove move = Move("parity", Flare, DamageClass.Physical,
            new DamageStatQueryEffect(new DamageStatSelector(DamageQueryOwner.Target, StatKind.Atk), null),
            new EffectivenessQueryEffect(EffectivenessQueryMode.Standard, null, Tide,
                new BattleQueryValue(2), StabQuerySource.None));
        BattleCreature source = Creature("source", [Leaf], new Stats(300, 40, 100, 40, 100, 100), move);
        BattleCreature target = Creature("target", [Tide], new Stats(400, 160, 100, 80, 100, 1), Wait());
        var battle = new BattleController(source, target, Chart(),
            new FakeRng(ints: [7], doubles: [0.99]));
        int resolved = battle.ResolveTurn(new UseMove(0), new UseMove(0))
            .OfType<DamageDealt>().Single(item => item.Slot.Side == BattleSide.Enemy).Amount;

        SmartAiDecision decision = SmartAi.ChooseAction(new SmartAiContext(
            [source], 0, [target], 0, Chart(), new FakeRng(doubles: [0.5])));
        double preview = decision.Scores.Single(score => score.Action is UseMove { MoveIndex: 0 })
            .Components.Single(component => component.Name == "damage").Value;

        Assert.Equal(resolved, preview);
    }

    [Fact]
    public void DamageQueryFamily_MatchesDeterministicGolden()
    {
        static string Run()
        {
            BattleMove move = Move("golden_query", Flare, DamageClass.Physical,
                new DamageClassQueryEffect(DamageClassQueryMode.HigherOffense),
                new EffectivenessQueryEffect(EffectivenessQueryMode.Standard, Air, null, null,
                    StabQuerySource.None));
            BattleCreature source = Creature("golden_source", [Flare],
                new Stats(300, 70, 90, 150, 90, 100), move);
            BattleCreature target = Creature("golden_target", [Leaf], Stats(), Wait());
            var battle = new BattleController(source, target, Chart(),
                new FakeRng(ints: [7], doubles: [0.99]), fieldInputs: new BattleFieldInputs(
                    Ruleset: BattleRulesets.ModernReference,
                    NaturalEnvironment: BattleEnvironment.Cave, InitialTerrain: Terrain.Grassy));
            battle.Overlays.Apply(new BattleOverlayApplication(
                new BattleOverlayOwner(BattleSide.Player, 0, new BattleSlot(BattleSide.Player, 0)),
                new BattleOverlaySource(), BattleOverlayLayer.FormOrSnapshot,
                new MoveTypeOverlay(0, Tide), 0, 0));

            IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));
            BattleDamageQueryResult query = Assert.Single(battle.DamageQueryTrace).Result;
            return string.Join('\n',
            [
                $"identity:{query.Identity.AuthoredType}->{query.Identity.EffectiveType}:" +
                    $"{query.Identity.AuthoredClass}->{query.Identity.EffectiveClass}:" +
                    $"{query.Identity.NaturalEnvironment}->{query.Identity.EffectiveEnvironment}",
                $"stats:{query.Offensive.Owner}/{query.Offensive.Stat}:" +
                    $"{query.Defensive.Owner}/{query.Defensive.Stat}",
                $"multipliers:stab={query.Stab}:effectiveness={query.Effectiveness.FinalValue}:spread={query.Spread}",
                .. query.Effectiveness.Steps.Select(step =>
                    $"effectiveness:{step.Stage}:{step.Operation}:{step.Input}->{step.Output}"),
                .. events.OfType<DamageDealt>().Select(item =>
                    $"damage:{item.Slot.Side}/{item.Slot.Position}:{item.Amount}:{item.Effectiveness}:{item.Crit}"),
            ]);
        }

        string first = Run();
        Assert.Equal(first, Run());
        Assert.Equal(Golden("damage-query"), first);
    }

    private static BattleMove Compile(params Effect[] effects) => MoveCompiler.ToBattleMove(new Move
    {
        Id = EntityId.Parse("move:compiled_query"), Name = "Compiled Query", Type = Neutral,
        DamageClass = DamageClass.Physical, Power = 80, Accuracy = null, Pp = 10,
        Target = MoveTarget.Selected, Effects = effects,
    });

    private static BattleMove Move(string slug, EntityId type, DamageClass damageClass,
        params MoveEffect[] effects) => new(EntityId.Parse($"move:{slug}"), type, damageClass,
        80, null, 10, 0, 0, secondaryEffects: effects);

    private static BattleMove Wait() => new(EntityId.Parse("move:wait"), Neutral,
        DamageClass.Status, null, null, 10, 0, 0);

    private static BattleCreature Creature(string slug, IReadOnlyList<EntityId> types, Stats stats,
        params BattleMove[] moves) => new(EntityId.Parse($"species:{slug}"), slug, 50, types, stats, moves);

    private static Stats Stats(int speed = 50) => new(300, 100, 100, 100, 100, speed);

    private static TypeChart Chart() => new([
        new TypeDef { Id = Neutral },
        new TypeDef { Id = Flare, DoubleDamageTo = [Leaf], HalfDamageTo = [Tide], NoDamageTo = [Air] },
        new TypeDef { Id = Tide, DoubleDamageTo = [Flare], HalfDamageTo = [Leaf] },
        new TypeDef { Id = Leaf, DoubleDamageTo = [Tide], HalfDamageTo = [Flare] },
        new TypeDef { Id = Air, DoubleDamageTo = [Leaf], HalfDamageTo = [Tide] },
    ]);

    private static Effect Op(string op, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Params = values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static string Golden(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden"))
        .Replace("\r\n", "\n").TrimEnd();

    private static double DamageScore(SmartAiContext context) => SmartAi.ChooseAction(context).Scores
        .Single(score => score.Action is UseMove { MoveIndex: 0 }).Components
        .Single(component => component.Name == "damage").Value;
}
