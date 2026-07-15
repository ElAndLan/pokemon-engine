using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public sealed record HpStatusPowerQuery(
    int AuthoredBase,
    IReadOnlyList<BattleQueryModifier> Modifiers,
    bool IgnoreSourceBurnPenalty);

public static class HpStatusFormulas
{
    public static bool AtThreshold(int currentHp, int maxHp, Fraction threshold, bool inclusive)
    {
        if (maxHp <= 0)
            return false;
        long left = checked((long)currentHp * threshold.Den);
        long right = checked((long)maxHp * threshold.Num);
        return inclusive ? left <= right : left < right;
    }

    public static int RatioAmount(int currentHp, int maxHp, HpRatioPowerBasis basis, int scale, int offset)
    {
        if (maxHp <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxHp), "Maximum HP must be positive.");
        int hp = basis == HpRatioPowerBasis.Current ? currentHp : maxHp - currentHp;
        return checked(offset + (int)(checked((long)scale * hp) / maxHp));
    }

    public static int BandPower(int currentHp, int maxHp, int scale, IReadOnlyList<HpPowerBand> bands)
    {
        int scaled = RatioAmount(currentHp, maxHp, HpRatioPowerBasis.Current, scale, 0);
        return bands.First(band => scaled <= band.UpperInclusive).Power;
    }

    public static bool Matches(BattleCreature creature, PersistentStatus? status, BattleVolatileStatus? volatileStatus)
    {
        if (volatileStatus is { } volatileValue)
            return volatileValue switch
            {
                BattleVolatileStatus.Confusion => creature.IsConfused,
                BattleVolatileStatus.Flinch => creature.Flinched,
                BattleVolatileStatus.Bound => creature.IsTrapped,
                BattleVolatileStatus.Seeded => creature.Seeded,
                BattleVolatileStatus.Protected => creature.Protected,
                _ => throw new ArgumentOutOfRangeException(nameof(volatileStatus), volatileValue, "Unknown volatile status."),
            };
        return creature.Status is { } current && (status is null || current == status);
    }

    public static int Count(BattleCreature creature, IReadOnlyList<PersistentStatus> persistent,
        IReadOnlyList<BattleVolatileStatus> volatileStatuses) =>
        (creature.Status is { } status && persistent.Contains(status) ? 1 : 0)
        + volatileStatuses.Count(value => Matches(creature, null, value));

    public static HpStatusPowerQuery PowerQuery(BattleMove move, BattleCreature source, BattleCreature target,
        PhysicalFormulaInputs? physicalInputs = null, BattleActionFormulaInputs? actionInputs = null,
        PartyResourceFormulaInputs? resourceInputs = null)
    {
        var modifiers = new List<BattleQueryModifier>();
        int insertion = 0;
        if (move.TargetHpThresholdPower is { } threshold
            && AtThreshold(target.CurrentHp, target.MaxHp, threshold.Threshold, threshold.Inclusive))
            modifiers.Add(Multiply(threshold.Multiplier, insertion++));

        if (move.HpRatioPower is { } ratio)
        {
            BattleCreature subject = ratio.Source == HpRatioPowerSource.User ? source : target;
            if (ratio.Scale is { } scale)
                modifiers.Add(Replace(Math.Max(1, RatioAmount(subject.CurrentHp, subject.MaxHp,
                    ratio.Basis, scale, ratio.Offset)), insertion++));
            else
            {
                int hp = ratio.Basis == HpRatioPowerBasis.Current ? subject.CurrentHp : subject.MaxHp - subject.CurrentHp;
                modifiers.Add(new BattleQueryModifier(BattleQueryStage.SourceTargetState,
                    BattleQueryOperation.Multiply, new BattleQueryValue(hp, subject.MaxHp), InsertionOrder: insertion++));
            }
        }

        if (move.HpBandPower is { } band)
        {
            BattleCreature subject = band.Source == HpRatioPowerSource.User ? source : target;
            modifiers.Add(Replace(BandPower(subject.CurrentHp, subject.MaxHp, band.Scale, band.Bands), insertion++));
        }

        bool ignoreBurn = false;
        if (move.SecondaryEffects.OfType<StatusPowerEffect>().SingleOrDefault() is { } statusPower)
        {
            BattleCreature subject = statusPower.Subject == StatusPowerSubject.User ? source : target;
            bool matches = Matches(subject, statusPower.Status, statusPower.Volatile);
            if (matches)
                modifiers.Add(Multiply(statusPower.Multiplier, insertion++));
            ignoreBurn = matches && statusPower.IgnoreSourceBurnPenalty;
        }

        if (move.SecondaryEffects.OfType<StatusCountPowerEffect>().SingleOrDefault() is { } statusCount)
        {
            int count = statusCount.Subject switch
            {
                StatusCountSubject.User => Count(source, statusCount.PersistentStatuses, statusCount.VolatileStatuses),
                StatusCountSubject.Target => Count(target, statusCount.PersistentStatuses, statusCount.VolatileStatuses),
                StatusCountSubject.Both => Count(source, statusCount.PersistentStatuses, statusCount.VolatileStatuses)
                    + Count(target, statusCount.PersistentStatuses, statusCount.VolatileStatuses),
                _ => throw new ArgumentOutOfRangeException(nameof(statusCount.Subject), statusCount.Subject,
                    "Unknown status-count subject."),
            };
            modifiers.Add(Replace(checked(statusCount.Base + count * statusCount.PerStatus), insertion++));
        }

        if (move.SecondaryEffects.OfType<SpeedRatioPowerEffect>().SingleOrDefault() is { } speedRatio)
        {
            PhysicalFormulaInputs inputs = physicalInputs ?? PhysicalMetricFormulas.Inputs(source, target);
            int numerator = PhysicalMetricFormulas.Speed(inputs, speedRatio.Numerator);
            int denominator = PhysicalMetricFormulas.Speed(inputs, speedRatio.Denominator);
            int power = speedRatio.Scale is { } scale
                ? PhysicalMetricFormulas.LinearRatio(numerator, denominator, scale, speedRatio.Offset, speedRatio.Cap)
                : PhysicalMetricFormulas.RatioBand(numerator, denominator, speedRatio.Bands);
            modifiers.Add(Replace(power, insertion++));
        }
        if (move.SecondaryEffects.OfType<MetricBandPowerEffect>().SingleOrDefault() is { } metricBand)
        {
            PhysicalFormulaInputs inputs = physicalInputs ?? PhysicalMetricFormulas.Inputs(source, target);
            modifiers.Add(Replace(PhysicalMetricFormulas.Band(
                PhysicalMetricFormulas.Value(inputs, metricBand.Metric, metricBand.Subject), metricBand.Bands), insertion++));
        }
        if (move.SecondaryEffects.OfType<MetricRatioPowerEffect>().SingleOrDefault() is { } metricRatio)
        {
            PhysicalFormulaInputs inputs = physicalInputs ?? PhysicalMetricFormulas.Inputs(source, target);
            modifiers.Add(Replace(PhysicalMetricFormulas.RatioBand(
                PhysicalMetricFormulas.Value(inputs, metricRatio.Metric, metricRatio.Numerator),
                PhysicalMetricFormulas.Value(inputs, metricRatio.Metric, metricRatio.Denominator),
                metricRatio.Bands), insertion++));
        }
        if (move.SecondaryEffects.OfType<ConsecutivePowerEffect>().SingleOrDefault() is { } consecutive)
        {
            BattleActionFormulaInputs inputs = actionInputs ?? new(0, 0, false, false, false, false);
            int prior = consecutive.Scope == ConsecutivePowerScope.CreatureConnected
                ? inputs.PriorCreatureConnections
                : inputs.PriorSideAttemptedTurns;
            modifiers.Add(Replace(ActionHistoryFormulas.ConsecutivePower(
                move.Power ?? 1, prior, consecutive.Mode, consecutive.Step, consecutive.Cap), insertion++));
        }
        if (move.SecondaryEffects.OfType<HistoryPowerEffect>().SingleOrDefault() is { } historyPower
            && Matches(historyPower.Condition, actionInputs ?? new(0, 0, false, false, false, false)))
            modifiers.Add(Multiply(historyPower.Multiplier, insertion++));

        if (PartyResourceFormulas.HasPowerFormula(move))
        {
            PartyResourceFormulaInputs inputs = resourceInputs
                ?? throw new InvalidOperationException("Party/resource power formulas require captured action inputs.");
            if (move.SecondaryEffects.OfType<PartyCountPowerEffect>().SingleOrDefault() is { } party)
            {
                int count = party.Filter switch
                {
                    PartyMemberFilter.Living => inputs.LivingParty,
                    PartyMemberFilter.Fainted => inputs.FaintedParty,
                    PartyMemberFilter.Contributing => inputs.ContributingParty,
                    _ => throw new ArgumentOutOfRangeException(nameof(party.Filter)),
                };
                modifiers.Add(Replace(PartyResourceFormulas.Linear(
                    count, party.Base, party.PerMember, party.Cap), insertion++));
            }
            if (move.SecondaryEffects.OfType<FriendshipPowerEffect>().SingleOrDefault() is { } friendship)
                modifiers.Add(Replace(PartyResourceFormulas.FriendshipPower(
                    inputs.Friendship, friendship.Mode), insertion++));
            if (move.SecondaryEffects.OfType<PpPowerEffect>().SingleOrDefault() is { } pp)
                modifiers.Add(Replace(PartyResourceFormulas.PpPower(
                    pp.Timing == PpPowerTiming.BeforeSpend ? inputs.PpBeforeSpend : inputs.PpAfterSpend,
                    pp.Bands), insertion++));
            if (move.SecondaryEffects.OfType<PositiveStagePowerEffect>().SingleOrDefault() is { } stages)
                modifiers.Add(Replace(PartyResourceFormulas.Linear(
                    stages.Subject == StatusPowerSubject.User
                        ? inputs.SourcePositiveStages : inputs.TargetPositiveStages,
                    stages.Base, stages.PerStage, stages.Cap), insertion++));
            if (move.SecondaryEffects.OfType<ItemDataPowerEffect>().Any())
                modifiers.Add(Replace(inputs.ItemPower
                    ?? throw new InvalidOperationException("Item-data power requires an available item value."), insertion++));
            if (move.SecondaryEffects.OfType<RandomTablePowerEffect>().Any())
                modifiers.Add(Replace(inputs.RandomPower
                    ?? throw new InvalidOperationException("Random-table power requires an action selection."), insertion++));
        }

        return new HpStatusPowerQuery(move.Power ?? 1, modifiers, ignoreBurn);
    }

    public static bool HasBasePower(BattleMove move) => move.Power is not null || move.HpBandPower is not null
        || move.HpRatioPower?.Scale is not null || move.SecondaryEffects.Any(effect => effect is StatusCountPowerEffect
            or SpeedRatioPowerEffect or MetricBandPowerEffect or MetricRatioPowerEffect or ConsecutivePowerEffect
            or PartyCountPowerEffect or FriendshipPowerEffect or PpPowerEffect or PositiveStagePowerEffect
            or ItemDataPowerEffect or RandomTablePowerEffect);

    private static bool Matches(HistoryPowerCondition condition, BattleActionFormulaInputs inputs) => condition switch
    {
        HistoryPowerCondition.SourceBeforeTarget => inputs.SourceBeforeTarget,
        HistoryPowerCondition.SourceAfterTarget => inputs.SourceAfterTarget,
        HistoryPowerCondition.PreviousActionFailed => inputs.PreviousActionFailed,
        HistoryPowerCondition.AllyFaintedPreviousTurn => inputs.AllyFaintedPreviousTurn,
        _ => throw new ArgumentOutOfRangeException(nameof(condition), condition, "Unknown history-power condition."),
    };

    public static int CannotKoFloor(BattleMove move) =>
        move.SecondaryEffects.OfType<CannotKoEffect>().SingleOrDefault()?.Floor ?? 0;

    public static BattleQueryResult SecondaryChanceQuery(MoveEffect effect, BattleCreature source, BattleCreature target)
    {
        BattleQueryModifier[] modifiers = effect.ChanceFormula is { } formula
            && Matches(formula.Subject == StatusPowerSubject.User ? source : target, formula.Status, formula.Volatile)
            ? [Multiply(formula.Multiplier, 0)]
            : [];
        return BattleQuery.Evaluate(BattleQueryId.SecondaryChance, new BattleQueryValue(effect.Chance), modifiers);
    }

    private static BattleQueryModifier Multiply(Fraction fraction, int insertion) =>
        new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply,
            new BattleQueryValue(fraction.Num, fraction.Den), InsertionOrder: insertion);

    private static BattleQueryModifier Replace(int value, int insertion) =>
        new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Replace,
            new BattleQueryValue(value), InsertionOrder: insertion);
}
