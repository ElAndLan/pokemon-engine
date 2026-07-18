using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum MoveReferenceSelector
{
    UserKnown,
    TargetKnown,
    UserLastUsed,
    TargetLastUsed,
    PartyKnown,
    AuthoredPool,
    EnvironmentPool,
    ExplicitReference,
}

public enum CalledMovePpOwner { Caller, Called }

public sealed record MoveReferenceCandidate(
    BattleMove Move,
    BattleSlot? OwnerSlot,
    int PartyIndex,
    int MoveIndex);

public sealed record CallMoveProfile(
    MoveReferenceSelector Selector,
    CalledMovePpOwner PpOwner,
    IReadOnlyList<EntityId> AuthoredPool,
    IReadOnlyDictionary<BattleEnvironment, EntityId> EnvironmentPool,
    IReadOnlySet<string> ExcludedTags);

public static class MoveReferenceResolver
{
    public const int MaximumDepth = 8;
    public static readonly IReadOnlySet<string> DefaultExcludedTags =
        new HashSet<string>(["uncallable"], StringComparer.Ordinal);

    public static MoveReferenceCandidate? Select(IEnumerable<MoveReferenceCandidate> candidates,
        IReadOnlySet<string> excludedTags, bool requirePp, IRng rng, out int? draw, out int count)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(excludedTags);
        ArgumentNullException.ThrowIfNull(rng);
        MoveReferenceCandidate[] eligible = candidates
            .Where(candidate => candidate.Move is not null
                && (!requirePp || candidate.Move.HasPp)
                && !candidate.Move.Tags.Any(excludedTags.Contains))
            .DistinctBy(candidate => candidate.Move.Move)
            .ToArray();
        count = eligible.Length;
        if (eligible.Length == 0)
        {
            draw = null;
            return null;
        }
        if (eligible.Length == 1)
        {
            draw = null;
            return eligible[0];
        }
        draw = rng.Next(eligible.Length);
        return eligible[draw.Value];
    }
}

public enum TurnOrderIntentKind { ActNext, ActLast, BoostPower, RepeatPending }

public sealed record TurnOrderIntentProfile(
    TurnOrderIntentKind Kind,
    Fraction PowerMultiplier = default)
{
    public Fraction EffectivePowerMultiplier => PowerMultiplier == default ? new Fraction(3, 2) : PowerMultiplier;
}

public enum PairedActionMode { FollowUp, Combine }
public enum PairedActionSideEffect { None, SpeedReduction, ResidualDamage, SecondaryChanceBoost }
public sealed record PairedActionOption(
    string Partner,
    EntityId? Type,
    PairedActionSideEffect SideEffect = PairedActionSideEffect.None);
public sealed record PairedActionProfile(
    string Key,
    string Member,
    PairedActionMode Mode,
    IReadOnlyList<PairedActionOption> Options,
    Fraction PowerMultiplier);
