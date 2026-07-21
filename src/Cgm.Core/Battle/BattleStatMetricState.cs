using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattleDerivedStatOperation { Average, Split, Swap }
public enum BattleDerivedStatGroup { Offense, Defense }
public enum BattleMetricMutationOperation { Replace, Add, Swap }
public enum BattleStatMetricFailure { None, Fainted, Overflow }

public sealed record DerivedStatMutationChange(
    BattleOverlayOwner Owner,
    StatKind Stat,
    int Before,
    int After);

public sealed record MetricMutationChange(
    BattleOverlayOwner Owner,
    BattleMetric Metric,
    int Before,
    int After);

public sealed record DerivedStatMutationResult(
    BattleStatMetricFailure Failure,
    IReadOnlyList<DerivedStatMutationChange> Changes)
{
    public bool Succeeded => Failure == BattleStatMetricFailure.None;
}

public sealed record MetricMutationResult(
    BattleStatMetricFailure Failure,
    IReadOnlyList<MetricMutationChange> Changes)
{
    public bool Succeeded => Failure == BattleStatMetricFailure.None;
}

public sealed class BattleStatMetricState(BattleOverlayStore overlays)
{
    private static readonly StatKind[] Offense = [StatKind.Atk, StatKind.Spa];
    private static readonly StatKind[] Defense = [StatKind.Def, StatKind.Spd];

    public DerivedStatMutationResult Mutate(
        DerivedStatMutationEffect effect,
        BattleOverlayOwner user,
        BattleEffectiveValues userBase,
        bool userFainted,
        BattleOverlayOwner target,
        BattleEffectiveValues targetBase,
        bool targetFainted,
        int turn,
        int actionSequence)
    {
        if (userFainted || targetFainted)
            return new DerivedStatMutationResult(BattleStatMetricFailure.Fainted, []);

        Stats userStats = overlays.Resolve(user, userBase).Values.Stats;
        Stats targetStats = overlays.Resolve(target, targetBase).Values.Stats;
        StatKind[] stats = effect.Operation == BattleDerivedStatOperation.Split
            ? effect.Group == BattleDerivedStatGroup.Offense ? Offense : Defense
            : [effect.Stat!.Value];
        var changes = new List<DerivedStatMutationChange>();
        foreach (StatKind stat in stats)
        {
            int userBefore = Value(userStats, stat);
            int targetBefore = Value(targetStats, stat);
            int userAfter;
            int targetAfter;
            if (effect.Operation == BattleDerivedStatOperation.Swap)
            {
                userAfter = targetBefore;
                targetAfter = userBefore;
            }
            else
            {
                userAfter = targetAfter = (int)(((long)userBefore + targetBefore) / 2);
            }
            if (userAfter != userBefore)
                changes.Add(new DerivedStatMutationChange(user, stat, userBefore, userAfter));
            if (targetAfter != targetBefore)
                changes.Add(new DerivedStatMutationChange(target, stat, targetBefore, targetAfter));
        }

        Apply(changes.Select(change => new BattleOverlayApplication(change.Owner,
            Source(user), BattleOverlayLayer.Additive, new DerivedStatOverlay(change.Stat, change.After),
            turn, actionSequence, Cleanup: Cleanup())).ToArray());
        return new DerivedStatMutationResult(BattleStatMetricFailure.None, changes);
    }

    public MetricMutationResult Mutate(
        MetricMutationEffect effect,
        BattleOverlayOwner user,
        BattleEffectiveValues userBase,
        bool userFainted,
        BattleOverlayOwner target,
        BattleEffectiveValues targetBase,
        bool targetFainted,
        int turn,
        int actionSequence)
    {
        bool needsTarget = effect.Operation == BattleMetricMutationOperation.Swap
            || effect.Subject == StageEffectScope.Target;
        if (userFainted || needsTarget && targetFainted)
            return new MetricMutationResult(BattleStatMetricFailure.Fainted, []);

        BattleOverlayOwner subject = effect.Subject == StageEffectScope.Target ? target : user;
        BattleEffectiveValues subjectBase = effect.Subject == StageEffectScope.Target ? targetBase : userBase;
        int before = overlays.Resolve(subject, subjectBase).Values.Metrics[effect.Metric];
        var changes = new List<MetricMutationChange>();
        if (effect.Operation == BattleMetricMutationOperation.Swap)
        {
            int userBefore = overlays.Resolve(user, userBase).Values.Metrics[effect.Metric];
            int targetBefore = overlays.Resolve(target, targetBase).Values.Metrics[effect.Metric];
            if (targetBefore != userBefore)
            {
                changes.Add(new MetricMutationChange(user, effect.Metric, userBefore, targetBefore));
                changes.Add(new MetricMutationChange(target, effect.Metric, targetBefore, userBefore));
            }
        }
        else
        {
            long calculated = effect.Operation == BattleMetricMutationOperation.Replace
                ? effect.Value!.Value
                : Math.Max(1L, (long)before + effect.Value!.Value);
            if (calculated > int.MaxValue)
                return new MetricMutationResult(BattleStatMetricFailure.Overflow, []);
            int after = (int)calculated;
            if (after != before)
                changes.Add(new MetricMutationChange(subject, effect.Metric, before, after));
        }

        Apply(changes.Select(change => new BattleOverlayApplication(change.Owner,
            Source(user), BattleOverlayLayer.Additive, new MetricValueOverlay(change.Metric, change.After),
            turn, actionSequence, effect.Duration, effect.Duration is null ? null : BattleIntentCheckpoint.TurnEnd,
            Cleanup())).ToArray());
        return new MetricMutationResult(BattleStatMetricFailure.None, changes);
    }

    private void Apply(IReadOnlyList<BattleOverlayApplication> applications) => overlays.ApplyMany(applications);

    private static BattleOverlaySource Source(BattleOverlayOwner user) =>
        new(user.Slot, user.PartyIndex);

    private static BattleOverlayCleanup Cleanup() =>
        BattleOverlayCleanup.Switch | BattleOverlayCleanup.Faint | BattleOverlayCleanup.BattleEnd;

    private static int Value(Stats stats, StatKind stat) => stat switch
    {
        StatKind.Atk => stats.Atk,
        StatKind.Def => stats.Def,
        StatKind.Spa => stats.Spa,
        StatKind.Spd => stats.Spd,
        StatKind.Spe => stats.Spe,
        _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "Only derived stats can be mutated."),
    };
}
