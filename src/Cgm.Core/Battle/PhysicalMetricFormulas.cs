using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public sealed record PhysicalFormulaInputs(
    int SourceSpeed,
    int TargetSpeed,
    int SourceWeight,
    int TargetWeight,
    int SourceHeight,
    int TargetHeight);

public static class PhysicalMetricFormulas
{
    public static bool HasPowerFormula(BattleMove move) => move.SecondaryEffects.Any(effect =>
        effect is SpeedRatioPowerEffect or MetricBandPowerEffect or MetricRatioPowerEffect);

    public static BattleQueryResult SpeedQuery(BattleCreature creature, BattleQueryContext? context = null) =>
        SpeedQuery(creature, creature.Stats.Spe, null, context);

    public static BattleQueryResult SpeedQuery(BattleCreature creature,
        IEnumerable<BattleQueryModifier> modifiers, BattleQueryContext? context = null) =>
        SpeedQuery(creature, creature.Stats.Spe, modifiers, context);

    public static BattleQueryResult SpeedQuery(BattleCreature creature, BattleOverlayStore overlays,
        BattleOverlayOwner owner, BattleQueryContext? context = null) =>
        SpeedQuery(creature, EffectiveValues(creature, overlays, owner).Stats.Spe, null, context);

    public static BattleQueryResult SpeedQuery(BattleCreature creature, BattleOverlayStore overlays,
        BattleOverlayOwner owner, IEnumerable<BattleQueryModifier> modifiers, BattleQueryContext? context = null) =>
        SpeedQuery(creature, EffectiveValues(creature, overlays, owner).Stats.Spe, modifiers, context);

    private static BattleQueryResult SpeedQuery(BattleCreature creature, int authoredSpeed,
        IEnumerable<BattleQueryModifier>? modifiers, BattleQueryContext? context) =>
        BattleQuery.Evaluate(BattleQueryId.Speed, new BattleQueryValue(authoredSpeed),
        [
            new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply,
                BattleQuery.StatStageMultiplier(creature.Stage(StatKind.Spe)), InsertionOrder: 0),
            new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply,
                creature.Status == PersistentStatus.Paralysis ? new BattleQueryValue(1, 4) : new BattleQueryValue(1),
                InsertionOrder: 1),
            .. modifiers ?? [],
        ], context);

    public static PhysicalFormulaInputs Inputs(BattleCreature source, BattleCreature target) => new(
        SpeedQuery(source).FinalValue.ToInt32(),
        SpeedQuery(target).FinalValue.ToInt32(),
        source.WeightHectograms,
        target.WeightHectograms,
        source.HeightDecimeters,
        target.HeightDecimeters);

    public static PhysicalFormulaInputs Inputs(BattleCreature source, BattleCreature target,
        BattleOverlayStore overlays, BattleOverlayOwner sourceOwner, BattleOverlayOwner targetOwner)
        => Inputs(source, target, overlays, sourceOwner, targetOwner, [], []);

    public static PhysicalFormulaInputs Inputs(BattleCreature source, BattleCreature target,
        BattleOverlayStore overlays, BattleOverlayOwner sourceOwner, BattleOverlayOwner targetOwner,
        IEnumerable<BattleQueryModifier> sourceSpeedModifiers,
        IEnumerable<BattleQueryModifier> targetSpeedModifiers)
    {
        BattleEffectiveValues sourceValues = EffectiveValues(source, overlays, sourceOwner);
        BattleEffectiveValues targetValues = EffectiveValues(target, overlays, targetOwner);
        return new PhysicalFormulaInputs(
            SpeedQuery(source, sourceValues.Stats.Spe, sourceSpeedModifiers, null).FinalValue.ToInt32(),
            SpeedQuery(target, targetValues.Stats.Spe, targetSpeedModifiers, null).FinalValue.ToInt32(),
            sourceValues.Metrics[BattleMetric.Weight],
            targetValues.Metrics[BattleMetric.Weight],
            sourceValues.Metrics[BattleMetric.Height],
            targetValues.Metrics[BattleMetric.Height]);
    }

    public static int LinearRatio(int numerator, int denominator, int scale, int offset, int? cap)
    {
        if (denominator <= 0)
            throw new ArgumentOutOfRangeException(nameof(denominator), "Formula denominator must be positive.");
        int value = checked(offset + (int)(checked((long)scale * numerator) / denominator));
        return cap is { } maximum ? Math.Min(value, maximum) : value;
    }

    public static int RatioBand(int numerator, int denominator, IReadOnlyList<FormulaPowerBand> bands)
    {
        if (denominator <= 0)
            throw new ArgumentOutOfRangeException(nameof(denominator), "Formula denominator must be positive.");
        return Band(numerator / denominator, bands);
    }

    public static int Band(int value, IReadOnlyList<FormulaPowerBand> bands) =>
        bands.Last(band => value >= band.MinInclusive).Power;

    public static int Value(PhysicalFormulaInputs inputs, BattleMetric metric, HpRatioPowerSource subject) =>
        (metric, subject) switch
        {
            (BattleMetric.Weight, HpRatioPowerSource.User) => inputs.SourceWeight,
            (BattleMetric.Weight, HpRatioPowerSource.Target) => inputs.TargetWeight,
            (BattleMetric.Height, HpRatioPowerSource.User) => inputs.SourceHeight,
            (BattleMetric.Height, HpRatioPowerSource.Target) => inputs.TargetHeight,
            _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown physical metric."),
        };

    public static int Speed(PhysicalFormulaInputs inputs, HpRatioPowerSource subject) => subject switch
    {
        HpRatioPowerSource.User => inputs.SourceSpeed,
        HpRatioPowerSource.Target => inputs.TargetSpeed,
        _ => throw new ArgumentOutOfRangeException(nameof(subject), subject, "Unknown formula subject."),
    };

    public static BattleEffectiveValues EffectiveValues(BattleCreature creature, BattleOverlayStore overlays,
        BattleOverlayOwner owner) => overlays.Resolve(owner, BaseEffectiveValues(creature)).Values;

    public static BattleEffectiveValues BaseEffectiveValues(BattleCreature creature) => new(
            creature.HeldItem,
            creature.Ability,
            creature.Types,
            creature.Stats,
            creature.Moves.Select(BattleEffectiveMove.FromBase).ToArray(),
            creature.FormId,
            metrics: new Dictionary<BattleMetric, int>
            {
                [BattleMetric.Weight] = creature.WeightHectograms,
                [BattleMetric.Height] = creature.HeightDecimeters,
            });

    private static BattleQueryResult SpeedQuery(BattleCreature creature, int authoredSpeed) =>
        SpeedQuery(creature, authoredSpeed, null, null);
}
