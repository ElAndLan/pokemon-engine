using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum PartyMemberFilter { Living, Fainted, Contributing }
public enum FriendshipPowerMode { Current, Missing }
public enum PpPowerTiming { BeforeSpend, AfterSpend }
public enum ItemPowerField { FlingPower }

public sealed record WeightedPowerEntry(int Weight, int Power);
public sealed record PartyResourceFormulaInputs(
    int LivingParty,
    int FaintedParty,
    int ContributingParty,
    int Friendship,
    int PpBeforeSpend,
    int PpAfterSpend,
    int SourcePositiveStages,
    int TargetPositiveStages,
    int? ItemPower,
    int? RandomPower);

public static class PartyResourceFormulas
{
    private static readonly StatKind[] Stages =
    [
        StatKind.Atk, StatKind.Def, StatKind.Spa, StatKind.Spd, StatKind.Spe,
        StatKind.Accuracy, StatKind.Evasion,
    ];

    public static bool HasPowerFormula(BattleMove move) => move.SecondaryEffects.Any(effect => effect is
        PartyCountPowerEffect or FriendshipPowerEffect or PpPowerEffect or PositiveStagePowerEffect
        or ItemDataPowerEffect or RandomTablePowerEffect);

    public static PartyResourceFormulaInputs Inputs(IReadOnlyList<BattleCreature> sourceParty,
        BattleCreature source, BattleCreature target, int ppBeforeSpend, int ppAfterSpend,
        int? itemPower = null, int? randomPower = null)
    {
        ArgumentNullException.ThrowIfNull(sourceParty);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        if (ppBeforeSpend < 0 || ppAfterSpend < 0 || ppAfterSpend > ppBeforeSpend)
            throw new ArgumentOutOfRangeException(nameof(ppAfterSpend), "PP snapshots require 0 <= after <= before.");

        return new PartyResourceFormulaInputs(
            Count(sourceParty, PartyMemberFilter.Living),
            Count(sourceParty, PartyMemberFilter.Fainted),
            Count(sourceParty, PartyMemberFilter.Contributing),
            source.Friendship,
            ppBeforeSpend,
            ppAfterSpend,
            PositiveStageSum(source),
            PositiveStageSum(target),
            itemPower,
            randomPower);
    }

    public static int Count(IEnumerable<BattleCreature> party, PartyMemberFilter filter)
    {
        ArgumentNullException.ThrowIfNull(party);
        return filter switch
        {
            PartyMemberFilter.Living => party.Count(creature => !creature.IsFainted),
            PartyMemberFilter.Fainted => party.Count(creature => creature.IsFainted),
            PartyMemberFilter.Contributing => party.Count(creature => !creature.IsFainted && creature.Status is null),
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unknown party filter."),
        };
    }

    public static int PositiveStageSum(BattleCreature creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        return Stages.Sum(stat => Math.Max(0, creature.Stage(stat)));
    }

    public static int Linear(int basis, int basePower, int perUnit, int? cap)
    {
        if (basis < 0 || basePower < 0 || perUnit < 0 || basePower == 0 && perUnit == 0
            || cap is <= 0 || cap is { } maximum && maximum < basePower)
            throw new ArgumentOutOfRangeException(nameof(basis), "Linear power inputs or cap are invalid.");
        long value = checked(basePower + checked((long)basis * perUnit));
        if (cap is { } ceiling)
            value = Math.Min(value, ceiling);
        return (int)Math.Min(value, int.MaxValue);
    }

    public static int FriendshipPower(int friendship, FriendshipPowerMode mode)
    {
        if (friendship is < 0 or > 255 || !Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(friendship));
        int value = mode == FriendshipPowerMode.Current ? friendship : 255 - friendship;
        return Math.Max(1, checked(value * 10 / 25));
    }

    public static int PpPower(int pp, IReadOnlyList<FormulaPowerBand> bands)
    {
        if (pp < 0)
            throw new ArgumentOutOfRangeException(nameof(pp));
        ArgumentNullException.ThrowIfNull(bands);
        if (bands.Count == 0 || bands[0].MinInclusive != 0
            || bands.Any(band => band.MinInclusive < 0 || band.Power <= 0)
            || bands.Zip(bands.Skip(1)).Any(pair => pair.First.MinInclusive >= pair.Second.MinInclusive))
            throw new ArgumentException("PP bands require a zero first bound, increasing minima, and positive powers.",
                nameof(bands));
        return bands.Last(band => pp >= band.MinInclusive).Power;
    }

    public static int SelectWeightedPower(IReadOnlyList<WeightedPowerEntry> entries, IRng rng,
        out int? draw, out int totalWeight)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(rng);
        ValidateEntries(entries, out totalWeight, out int positiveEntries);
        draw = positiveEntries == 1 ? null : rng.Next(totalWeight);
        int selected = draw ?? 0;
        int cumulative = 0;
        foreach (WeightedPowerEntry entry in entries)
        {
            cumulative = checked(cumulative + entry.Weight);
            if (entry.Weight > 0 && selected < cumulative)
                return entry.Power;
        }
        throw new InvalidOperationException("A validated weighted table must select one entry.");
    }

    public static int ExpectedWeightedPower(IReadOnlyList<WeightedPowerEntry> entries)
    {
        ValidateEntries(entries, out int totalWeight, out _);
        long weighted = 0;
        foreach (WeightedPowerEntry entry in entries)
            weighted = checked(weighted + checked((long)entry.Weight * entry.Power));
        return checked((int)(weighted / totalWeight));
    }

    private static void ValidateEntries(IReadOnlyList<WeightedPowerEntry> entries,
        out int totalWeight, out int positiveEntries)
    {
        totalWeight = 0;
        positiveEntries = 0;
        foreach (WeightedPowerEntry entry in entries)
        {
            if (entry.Weight < 0 || entry.Power <= 0)
                throw new ArgumentOutOfRangeException(nameof(entries), "Weights must be nonnegative and powers positive.");
            totalWeight = checked(totalWeight + entry.Weight);
            if (entry.Weight > 0)
                positiveEntries++;
        }
        if (positiveEntries == 0)
            throw new ArgumentException("A weighted table requires at least one positive entry.", nameof(entries));
    }
}
