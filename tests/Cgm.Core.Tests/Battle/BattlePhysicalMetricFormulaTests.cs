using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattlePhysicalMetricFormulaTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    [Theory]
    [InlineData(1, 100, 25, 1, 150, 1)]
    [InlineData(4, 100, 25, 1, 150, 2)]
    [InlineData(596, 100, 25, 1, 150, 150)]
    [InlineData(1000, 1, 25, 1, 150, 150)]
    public void LinearRatio_FloorsOffsetsAndCaps(int numerator, int denominator, int scale, int offset, int cap,
        int expected) =>
        Assert.Equal(expected, PhysicalMetricFormulas.LinearRatio(numerator, denominator, scale, offset, cap));

    [Fact]
    public void LinearRatio_RejectsNonpositiveDenominatorsAndCheckedOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalMetricFormulas.LinearRatio(1, 0, 1, 0, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhysicalMetricFormulas.RatioBand(1, -1, Bands()));
        Assert.Throws<OverflowException>(() =>
            PhysicalMetricFormulas.LinearRatio(int.MaxValue, 1, int.MaxValue, int.MaxValue, null));
    }

    [Theory]
    [InlineData(9, 40)]
    [InlineData(10, 60)]
    [InlineData(19, 60)]
    [InlineData(20, 80)]
    [InlineData(29, 80)]
    [InlineData(30, 120)]
    [InlineData(39, 120)]
    [InlineData(40, 150)]
    [InlineData(41, 150)]
    public void SpeedRatioBands_CoverEveryBoundary(int numerator, int expected) =>
        Assert.Equal(expected, PhysicalMetricFormulas.RatioBand(numerator, 10, Bands()));

    [Theory]
    [InlineData(19, 40)]
    [InlineData(20, 60)]
    [InlineData(29, 60)]
    [InlineData(30, 80)]
    [InlineData(39, 80)]
    [InlineData(40, 100)]
    [InlineData(49, 100)]
    [InlineData(50, 120)]
    [InlineData(51, 120)]
    public void MetricRatioBands_CoverEveryBoundary(int numerator, int expected) =>
        Assert.Equal(expected, PhysicalMetricFormulas.RatioBand(numerator, 10,
            [new(0, 40), new(2, 60), new(3, 80), new(4, 100), new(5, 120)]));

    [Theory]
    [InlineData(0, 20)]
    [InlineData(99, 20)]
    [InlineData(100, 40)]
    [InlineData(249, 40)]
    [InlineData(250, 60)]
    [InlineData(499, 60)]
    [InlineData(500, 80)]
    [InlineData(999, 80)]
    [InlineData(1000, 100)]
    [InlineData(1999, 100)]
    [InlineData(2000, 120)]
    public void MetricBands_CoverEveryBoundary(int value, int expected) =>
        Assert.Equal(expected, PhysicalMetricFormulas.Band(value,
        [
            new(0, 20), new(100, 40), new(250, 60), new(500, 80), new(1000, 100), new(2000, 120),
        ]));

    [Fact]
    public void Inputs_UseEffectiveSpeedAndBothBaseMetrics()
    {
        BattleCreature source = Creature("source", Inert(), speed: 400, weight: 800, height: 18);
        BattleCreature target = Creature("target", Inert(), speed: 100, weight: 200, height: 7);
        source.SetStage(StatKind.Spe, 1);
        source.SetStatus(PersistentStatus.Paralysis);

        PhysicalFormulaInputs inputs = PhysicalMetricFormulas.Inputs(source, target);

        Assert.Equal(new(150, 100, 800, 200, 18, 7), inputs);
        Assert.Equal(800, PhysicalMetricFormulas.Value(inputs, BattleMetric.Weight, HpRatioPowerSource.User));
        Assert.Equal(200, PhysicalMetricFormulas.Value(inputs, BattleMetric.Weight, HpRatioPowerSource.Target));
        Assert.Equal(18, PhysicalMetricFormulas.Value(inputs, BattleMetric.Height, HpRatioPowerSource.User));
        Assert.Equal(7, PhysicalMetricFormulas.Value(inputs, BattleMetric.Height, HpRatioPowerSource.Target));
        Assert.Equal(150, PhysicalMetricFormulas.Speed(inputs, HpRatioPowerSource.User));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicalMetricFormulas.Value(inputs, (BattleMetric)999, HpRatioPowerSource.User));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicalMetricFormulas.Speed(inputs, (HpRatioPowerSource)999));
        Assert.Equal(26, PhysicalMetricFormulas.LinearRatio(int.MaxValue, int.MaxValue, 25, 1, 150));
        Assert.Equal(150, PhysicalMetricFormulas.Band(int.MaxValue, Bands()));
    }

    [Fact]
    public void PhysicalMetrics_DoNotDependOnGroundedOrAirborneTyping()
    {
        EntityId air = EntityId.Parse("type:air");
        BattleCreature grounded = Creature("grounded", Inert(), weight: 500);
        BattleCreature airborne = new(EntityId.Parse("species:airborne"), "Airborne", 50, [air],
            new Stats(1000, 100, 100, 100, 100, 100), [Inert()], weightHectograms: 500,
            heightDecimeters: 10);

        Assert.Equal(PhysicalMetricFormulas.Inputs(grounded, grounded).SourceWeight,
            PhysicalMetricFormulas.Inputs(airborne, airborne).SourceWeight);
    }

    [Fact]
    public void Compiler_ProducesAllTypedPhysicalFormulas()
    {
        BattleMove speedLinear = Compile(null, Op("speedRatioPower", ("numerator", "target"),
            ("denominator", "user"), ("scale", 25), ("offset", 1), ("cap", 150)));
        BattleMove speedBands = Compile(null, Op("speedRatioPower", ("numerator", "user"),
            ("denominator", "target"), ("bands", "0:40,1:60,2:80,3:120,4:150")));
        BattleMove metricBand = Compile(null, Op("metricBandPower", ("metric", "weight"),
            ("subject", "target"), ("bands", "0:20,100:40")));
        BattleMove metricRatio = Compile(null, Op("metricRatioPower", ("metric", "height"),
            ("numerator", "user"), ("denominator", "target"), ("bands", "0:40,2:60")));

        Assert.Equal(new SpeedRatioPowerEffect(HpRatioPowerSource.Target, HpRatioPowerSource.User,
            25, 1, 150, []), Assert.Single(speedLinear.SecondaryEffects));
        Assert.Equal([new(0, 40), new(1, 60), new(2, 80), new(3, 120), new(4, 150)],
            Assert.IsType<SpeedRatioPowerEffect>(Assert.Single(speedBands.SecondaryEffects)).Bands);
        Assert.Equal(BattleMetric.Weight,
            Assert.IsType<MetricBandPowerEffect>(Assert.Single(metricBand.SecondaryEffects)).Metric);
        Assert.Equal(BattleMetric.Height,
            Assert.IsType<MetricRatioPowerEffect>(Assert.Single(metricRatio.SecondaryEffects)).Metric);
        Assert.All([speedLinear, speedBands, metricBand, metricRatio],
            move => Assert.True(HpStatusFormulas.HasBasePower(move)));
    }

    [Fact]
    public void Compiler_RejectsMalformedOrAmbiguousPhysicalFormulas()
    {
        Assert.Throws<ArgumentException>(() => Compile(null, Op("speedRatioPower", ("numerator", "user"),
            ("denominator", "user"), ("scale", 1))));
        Assert.Throws<ArgumentException>(() => Compile(null, Op("speedRatioPower", ("numerator", "user"),
            ("denominator", "target"), ("scale", 1), ("bands", "0:40"))));
        Assert.Throws<ArgumentException>(() => Compile(null, Op("speedRatioPower", ("numerator", "user"),
            ("denominator", "target"), ("bands", "0:40"), ("cap", 100))));
        Assert.Throws<ArgumentException>(() => Compile(null, Op("speedRatioPower", ("numerator", "user"),
            ("denominator", "target"), ("scale", 0))));
        Assert.Throws<ArgumentException>(() => Compile(null, Op("metricRatioPower", ("metric", "weight"),
            ("numerator", "target"), ("denominator", "target"), ("bands", "0:40"))));
        Assert.Throws<ArgumentException>(() => Compile(null, Op("metricBandPower", ("metric", "weight"),
            ("subject", "target"), ("bands", "1:20"))));
        Assert.Throws<ArgumentException>(() => Compile(null, Op("metricBandPower", ("metric", "weight"),
            ("subject", "target"), ("bands", "0:20,0:40"))));
        Assert.Throws<ArgumentException>(() => Compile(null, Op("metricBandPower", ("metric", "weight"),
            ("subject", "target"), ("bands", "0:0"))));
        Assert.Throws<ArgumentException>(() => Compile(null, Op("metricBandPower", ("metric", "mass"),
            ("subject", "target"), ("bands", "0:20"))));
        Assert.Throws<ArgumentException>(() => Compile(null,
            Op("metricBandPower", ("metric", "weight"), ("subject", "target"), ("bands", "0:20")),
            Op("metricRatioPower", ("metric", "weight"), ("numerator", "user"),
                ("denominator", "target"), ("bands", "0:40"))));
        Effect chance = new()
        {
            Op = "metricBandPower", Chance = 50,
            Params = Op("metricBandPower", ("metric", "weight"), ("subject", "target"), ("bands", "0:20")).Params,
        };
        Assert.Throws<ArgumentException>(() => Compile(null, chance));
    }

    [Fact]
    public void Resolver_UsesPerTargetEffectiveSpeedAndTracesReplacementPower()
    {
        BattleMove move = Compile(null, Op("speedRatioPower", ("numerator", "user"),
            ("denominator", "target"), ("bands", "0:40,1:60,2:80,3:120,4:150")));
        BattleCreature source = Creature("source", move, speed: 400);
        BattleCreature target = Creature("target", Inert(), speed: 100);
        var battle = Battle(source, target);

        battle.ResolveTurn(new UseMove(0), new Pass());

        BattleQueryResult power = Assert.Single(battle.QueryTrace,
            entry => entry.Result.Query == BattleQueryId.BasePower).Result;
        Assert.Equal(150, power.FinalValue.ToInt32());
        Assert.Contains(power.Steps, step => step.Applied && step.Operation == BattleQueryOperation.Replace);
        Assert.True(target.CurrentHp < target.MaxHp);
    }

    [Fact]
    public void Resolver_UsesOverlaidStatsForSpeedPowerAndTurnOrder()
    {
        BattleMove move = Compile(null, Op("speedRatioPower", ("numerator", "user"),
            ("denominator", "target"), ("bands", "0:40,1:60,2:80,3:120,4:150")));
        BattleCreature source = Creature("source", move, speed: 400);
        BattleCreature target = Creature("target", Inert(), speed: 100);
        var battle = Battle(source, target);
        battle.Overlays.Apply(new BattleOverlayApplication(
            new(BattleSide.Player, 0, new BattleSlot(BattleSide.Player, 0)), new(),
            BattleOverlayLayer.FormOrSnapshot, new StatsOverlay(source.Stats with { Spe = 100 }), 0, 0));

        battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Equal(60, Assert.Single(battle.QueryTrace,
            entry => entry.Result.Query == BattleQueryId.BasePower).Result.FinalValue.ToInt32());
        Assert.Contains(battle.QueryTrace, entry => entry.Result.Query == BattleQueryId.Speed
            && entry.SourceSlot == new BattleSlot(BattleSide.Player, 0)
            && entry.Result.AuthoredBase.ToInt32() == 100);
    }

    [Fact]
    public void Resolver_UsesMetricOverlaysWithoutMutatingBaseSpeciesValues()
    {
        BattleMove move = Compile(null, Op("metricBandPower", ("metric", "weight"),
            ("subject", "target"), ("bands", "0:20,100:40,250:60,500:80,1000:100,2000:120")));
        BattleCreature source = Creature("source", move);
        BattleCreature target = Creature("target", Inert(), weight: 99);
        var battle = Battle(source, target);
        var owner = new BattleOverlayOwner(BattleSide.Enemy, 0, new BattleSlot(BattleSide.Enemy, 0));
        battle.Overlays.Apply(new BattleOverlayApplication(owner, new(), BattleOverlayLayer.FormOrSnapshot,
            new MetricOverlay(BattleMetric.Weight, 2000), 0, 0));

        battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Equal(120, Assert.Single(battle.QueryTrace,
            entry => entry.Result.Query == BattleQueryId.BasePower).Result.FinalValue.ToInt32());
        Assert.Equal(99, target.WeightHectograms);
    }

    [Fact]
    public void SmartAi_ScoresNullPowerPhysicalFormulasAndHonorsOverlays()
    {
        BattleMove formula = Compile(null, Op("metricRatioPower", ("metric", "weight"),
            ("numerator", "user"), ("denominator", "target"), ("bands", "0:40,2:60,3:80,4:100,5:120")));
        BattleMove fixedMove = new(EntityId.Parse("move:fixed"), Normal, DamageClass.Special, 80, null, 20, 0, 0);
        BattleCreature source = Creature("source", formula, weight: 100, additional: fixedMove);
        BattleCreature target = Creature("target", Inert(), weight: 100);
        var overlays = new BattleOverlayStore();
        overlays.Apply(new BattleOverlayApplication(
            new(BattleSide.Enemy, 0, new BattleSlot(BattleSide.Enemy, 0)), new(), BattleOverlayLayer.FormOrSnapshot,
            new MetricOverlay(BattleMetric.Weight, 500), 0, 0));

        SmartAiDecision decision = SmartAi.ChooseAction(new SmartAiContext([source], 0, [target], 0,
            Chart(), new ZeroRng(), Overlays: overlays));

        Assert.Equal(0, Assert.IsType<UseMove>(decision.Action).MoveIndex);
        Assert.True(decision.Scores[0].Score > decision.Scores[1].Score);
    }

    [Fact]
    public void Creature_RejectsNonpositivePhysicalMetrics()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Creature("zero", Inert(), weight: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Creature("negative", Inert(), height: -1));
    }

    private static FormulaPowerBand[] Bands() =>
        [new(0, 40), new(1, 60), new(2, 80), new(3, 120), new(4, 150)];

    private static BattleMove Compile(int? power, params Effect[] effects) => MoveCompiler.ToBattleMove(new Move
    {
        Id = EntityId.Parse("move:formula"), Name = "Formula", Type = Normal,
        DamageClass = DamageClass.Special, Power = power, Accuracy = null, Pp = 20,
        Target = MoveTarget.Selected, Effects = effects,
    });

    private static Effect Op(string op, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Params = values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static BattleCreature Creature(string id, BattleMove move, int speed = 100, int weight = 100,
        int height = 10, BattleMove? additional = null) =>
        new(EntityId.Parse($"species:{id}"), id, 50, [Normal], new Stats(1000, 100, 100, 100, 100, speed),
            additional is null ? [move] : [move, additional], weightHectograms: weight, heightDecimeters: height);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 20, 0, 0);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);
    private static BattleController Battle(BattleCreature source, BattleCreature target) =>
        new(source, target, Chart(), new ZeroRng());

    private sealed class ZeroRng : IRng
    {
        public int Next(int maxExclusive) => maxExclusive == 16 ? 15 : 0;
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
        public double NextDouble() => 0.99;
    }
}
