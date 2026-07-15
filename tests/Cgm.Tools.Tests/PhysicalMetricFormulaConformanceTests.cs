using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation.Rules;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class PhysicalMetricFormulaConformanceTests
{
    private static MoveConformanceCatalog Catalog => CgmJson.Deserialize<MoveConformanceCatalog>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MoveConformance", "definitions.v1.json")));

    public static IEnumerable<object[]> CertifiedRecords() => Catalog.Entries
        .Where(entry => entry.TestIds.Any(id =>
            id.StartsWith("PhysicalMetricFormulaConformanceTests.", StringComparison.Ordinal)))
        .Select(entry => new object[] { entry });

    [Theory]
    [MemberData(nameof(CertifiedRecords))]
    public void Certified(MoveConformanceRecord record)
    {
        Move move = record.Mechanics.ToMove(record.ReferenceKey);
        Project project = new(new ProjectSettings { Name = "Physical Formula Conformance" },
            new Dictionary<EntityId, IEntity> { [move.Id] = move });
        Assert.Empty(new MoveRule().Check(project));
        BattleMove compiled = MoveCompiler.ToBattleMove(move);
        BattleCreature source = Creature("source", compiled, move.Type, 400);
        BattleCreature target = Creature("target", Inert(move.Type), move.Type, 100);
        var battle = new BattleController(source, target,
            new TypeChart([new TypeDef { Id = move.Type }]), new MaxDamageRng());

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        PhysicalFormulaInputs inputs = PhysicalMetricFormulas.Inputs(source, target);
        MoveEffect formula = Assert.Single(compiled.SecondaryEffects,
            effect => effect is SpeedRatioPowerEffect or MetricBandPowerEffect or MetricRatioPowerEffect);
        int expected = formula switch
        {
            SpeedRatioPowerEffect speed when speed.Scale is { } scale => PhysicalMetricFormulas.LinearRatio(
                PhysicalMetricFormulas.Speed(inputs, speed.Numerator),
                PhysicalMetricFormulas.Speed(inputs, speed.Denominator), scale, speed.Offset, speed.Cap),
            SpeedRatioPowerEffect speed => PhysicalMetricFormulas.RatioBand(
                PhysicalMetricFormulas.Speed(inputs, speed.Numerator),
                PhysicalMetricFormulas.Speed(inputs, speed.Denominator), speed.Bands),
            MetricBandPowerEffect metric => PhysicalMetricFormulas.Band(
                PhysicalMetricFormulas.Value(inputs, metric.Metric, metric.Subject), metric.Bands),
            MetricRatioPowerEffect metric => PhysicalMetricFormulas.RatioBand(
                PhysicalMetricFormulas.Value(inputs, metric.Metric, metric.Numerator),
                PhysicalMetricFormulas.Value(inputs, metric.Metric, metric.Denominator), metric.Bands),
            _ => throw new InvalidOperationException("Unknown physical formula."),
        };
        Assert.Equal(expected, Assert.Single(battle.QueryTrace,
            entry => entry.Result.Query == BattleQueryId.BasePower).Result.FinalValue.ToInt32());
        Assert.Contains($"PhysicalMetricFormulaConformanceTests.Certified({record.ReferenceKey})", record.TestIds);
        Assert.Contains(record.MechanicFamilies,
            family => family is "speedRatioPower" or "metricBandPower" or "metricRatioPower");
    }

    private static BattleCreature Creature(string slug, BattleMove move, EntityId type, int speed) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [type],
            new Stats(1000, 100, 100, 100, 100, speed), [move], weightHectograms: speed,
            heightDecimeters: speed);

    private static BattleMove Inert(EntityId type) =>
        new(EntityId.Parse("move:inert"), type, DamageClass.Status, null, null, 20, 0, 0);

    private sealed class MaxDamageRng : IRng
    {
        public int Next(int maxExclusive) => maxExclusive == 16 ? 15 : 0;
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
        public double NextDouble() => 0.99;
    }
}
