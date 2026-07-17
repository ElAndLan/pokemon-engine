using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattleIntentCheckpoint { TurnStart, PreAction, BeforeMove, AfterMove, TurnEnd }
public enum BattleIntentOwnerScope { Creature, Slot, Side, Field }
public enum BattleIntentTargetPolicy { SnapshotSlot, LiveSlot, Source, Side, Field }
public enum BattleIntentPayloadKind { SkipAction, ReleaseMove }
public enum BattleIntentSwitchPolicy { Cancel, FollowOwner, StaySlot }
public enum BattleIntentFaintPolicy { Cancel, Persist }

public sealed record BattleIntentOwner(
    BattleIntentOwnerScope Scope,
    BattleSide Side,
    BattleSlot? LastKnownSlot = null,
    int? PartyIndex = null,
    BattleIntentSwitchPolicy? SwitchPolicy = null,
    BattleIntentFaintPolicy? FaintPolicy = null);

public sealed record BattleIntentTarget(
    BattleIntentTargetPolicy Policy,
    BattleSlot? Slot = null,
    int? SnapshotPartyIndex = null,
    BattleSide? Side = null);

public abstract record BattleIntentPayload
{
    internal BattleIntentPayload() { }
    public abstract BattleIntentPayloadKind Kind { get; }
}

public sealed record SkipActionIntent : BattleIntentPayload
{
    public override BattleIntentPayloadKind Kind => BattleIntentPayloadKind.SkipAction;
}

public sealed record ReleaseMoveIntent(int MoveIndex, SemiInvulnerableState? State) : BattleIntentPayload
{
    public override BattleIntentPayloadKind Kind => BattleIntentPayloadKind.ReleaseMove;
}

public sealed record BattleIntentRequest(
    int DueTurn,
    BattleIntentCheckpoint Checkpoint,
    BattleIntentOwner Owner,
    BattleIntentTarget Target,
    BattleIntentPayload Payload,
    EntityId SourceMove,
    int SourceActionSequence,
    string Ruleset = BattleRulesets.Gen4Like);

public sealed record BattleIntent(
    long Sequence,
    int DueTurn,
    BattleIntentCheckpoint Checkpoint,
    BattleIntentOwner Owner,
    BattleIntentTarget Target,
    BattleIntentPayload Payload,
    EntityId SourceMove,
    int SourceActionSequence,
    string Ruleset);

public sealed record BattleIntentCleanupResult(
    IReadOnlyList<BattleIntent> Cancelled,
    IReadOnlyList<BattleIntent> Transferred);

public sealed record BattleIntentResolvedTarget(
    BattleIntentTargetPolicy Policy,
    BattleSlot? Slot,
    BattleSide? Side,
    bool IsField);

public sealed record BattleIntentDebugEntry(
    long Sequence,
    int DueTurn,
    BattleIntentCheckpoint Checkpoint,
    BattleIntentOwnerScope OwnerScope,
    BattleSide OwnerSide,
    BattleSlot? OwnerSlot,
    int? OwnerPartyIndex,
    BattleIntentTargetPolicy TargetPolicy,
    BattleSlot? TargetSlot,
    int? SnapshotPartyIndex,
    BattleSide? TargetSide,
    BattleIntentPayloadKind Payload,
    int? PayloadMoveIndex,
    SemiInvulnerableState? PayloadSemiInvulnerableState,
    EntityId SourceMove,
    int SourceActionSequence,
    string Ruleset);

public sealed class BattleIntentPreview
{
    internal BattleIntentPreview(BattleIntentQueue owner, int turn, BattleIntentCheckpoint checkpoint,
        long sequenceBoundary, IReadOnlyList<BattleIntent> entries)
    {
        Owner = owner;
        Turn = turn;
        Checkpoint = checkpoint;
        SequenceBoundary = sequenceBoundary;
        Entries = entries;
    }

    internal BattleIntentQueue Owner { get; }
    internal bool Consumed { get; set; }
    internal bool Completed { get; set; }
    public int Turn { get; }
    public BattleIntentCheckpoint Checkpoint { get; }
    public long SequenceBoundary { get; }
    public IReadOnlyList<BattleIntent> Entries { get; }
}

public sealed class BattleIntentQueue
{
    private readonly List<BattleIntent> _entries = [];
    private long _nextSequence;

    public int Count => _entries.Count;

    public BattleIntent Enqueue(BattleIntentRequest request)
        => EnqueueRange([request]).Single();

    public IReadOnlyList<BattleIntent> EnqueueRange(IEnumerable<BattleIntentRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        BattleIntentRequest[] captured = requests.ToArray();
        foreach (BattleIntentRequest request in captured)
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateRequest(request);
        }
        if (captured.Length > long.MaxValue - _nextSequence)
            throw new OverflowException("Battle intent sequence is exhausted.");

        var enqueued = new List<BattleIntent>(captured.Length);
        foreach (BattleIntentRequest request in captured)
        {
            var intent = new BattleIntent(_nextSequence++, request.DueTurn, request.Checkpoint, request.Owner,
                request.Target, request.Payload, request.SourceMove, request.SourceActionSequence, request.Ruleset);
            _entries.Add(intent);
            enqueued.Add(intent);
        }
        return enqueued.ToArray();
    }

    public BattleIntentPreview Preview(int turn, BattleIntentCheckpoint checkpoint)
    {
        if (turn < 0)
            throw new ArgumentOutOfRangeException(nameof(turn), "Preview turn cannot be negative.");
        if (!Enum.IsDefined(checkpoint))
            throw new ArgumentOutOfRangeException(nameof(checkpoint), checkpoint, "Unknown intent checkpoint.");

        long boundary = _nextSequence;
        BattleIntent[] entries = InOrder(_entries
            .Where(intent => intent.Sequence < boundary && intent.DueTurn <= turn && intent.Checkpoint == checkpoint))
            .ToArray();
        return new BattleIntentPreview(this, turn, checkpoint, boundary, entries);
    }

    public IReadOnlyList<BattleIntent> Consume(BattleIntentPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (!ReferenceEquals(preview.Owner, this) || preview.Consumed)
            throw new ArgumentException("Intent preview is stale, foreign, or already consumed.", nameof(preview));
        if (preview.Entries.Any(previewed => !_entries.Contains(previewed)))
            throw new ArgumentException("Intent preview no longer matches the queue.", nameof(preview));

        preview.Consumed = true;
        var sequences = preview.Entries.Select(intent => intent.Sequence).ToHashSet();
        _entries.RemoveAll(intent => sequences.Contains(intent.Sequence));
        return preview.Entries;
    }

    public IReadOnlyList<BattleIntent> Complete(BattleIntentPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (!ReferenceEquals(preview.Owner, this) || !preview.Consumed || preview.Completed)
            throw new ArgumentException("Intent preview is foreign, unconsumed, or already completed.", nameof(preview));

        preview.Completed = true;
        return InOrder(_entries
            .Where(intent => intent.Sequence >= preview.SequenceBoundary
                && intent.DueTurn <= preview.Turn
                && intent.Checkpoint == preview.Checkpoint))
            .ToArray();
    }

    public BattleIntentCleanupResult OwnerSwitched(BattleSide side, int partyIndex, BattleSlot? destination)
    {
        if (!Enum.IsDefined(side) || partyIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(partyIndex), "Switch cleanup requires a valid owner identity.");
        var cancelled = new List<BattleIntent>();
        var transferred = new List<BattleIntent>();

        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            BattleIntent intent = _entries[i];
            if (intent.Owner.Scope != BattleIntentOwnerScope.Creature
                || intent.Owner.Side != side
                || intent.Owner.PartyIndex != partyIndex)
                continue;

            if (intent.Owner.SwitchPolicy == BattleIntentSwitchPolicy.FollowOwner && destination is { } slot)
            {
                BattleIntent moved = intent with { Owner = intent.Owner with { LastKnownSlot = slot } };
                _entries[i] = moved;
                transferred.Add(moved);
            }
            else
            {
                _entries.RemoveAt(i);
                cancelled.Add(intent);
            }
        }
        return new BattleIntentCleanupResult(InOrder(cancelled).ToArray(), InOrder(transferred).ToArray());
    }

    public IReadOnlyList<BattleIntent> OwnerFainted(BattleSide side, int partyIndex)
    {
        if (!Enum.IsDefined(side) || partyIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(partyIndex), "Faint cleanup requires a valid owner identity.");
        return RemoveWhere(intent => intent.Owner.Scope == BattleIntentOwnerScope.Creature
            && intent.Owner.Side == side
            && intent.Owner.PartyIndex == partyIndex
            && intent.Owner.FaintPolicy == BattleIntentFaintPolicy.Cancel);
    }

    public IReadOnlyList<BattleIntent> EndBattle() => RemoveWhere(_ => true);

    public BattleIntentResolvedTarget? ResolveTarget(BattleIntent intent,
        Func<BattleSlot, int?> livingPartyAt,
        Func<BattleSide, int, BattleSlot?> activeSlotFor)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(livingPartyAt);
        ArgumentNullException.ThrowIfNull(activeSlotFor);

        return intent.Target.Policy switch
        {
            BattleIntentTargetPolicy.SnapshotSlot when livingPartyAt(intent.Target.Slot!.Value) == intent.Target.SnapshotPartyIndex
                => new(intent.Target.Policy, intent.Target.Slot, intent.Target.Slot.Value.Side, false),
            BattleIntentTargetPolicy.SnapshotSlot => null,
            BattleIntentTargetPolicy.LiveSlot when livingPartyAt(intent.Target.Slot!.Value) is not null
                => new(intent.Target.Policy, intent.Target.Slot, intent.Target.Slot.Value.Side, false),
            BattleIntentTargetPolicy.LiveSlot => null,
            BattleIntentTargetPolicy.Source when intent.Owner.Scope == BattleIntentOwnerScope.Creature
                && activeSlotFor(intent.Owner.Side, intent.Owner.PartyIndex!.Value) is { } source
                => new(intent.Target.Policy, source, source.Side, false),
            BattleIntentTargetPolicy.Source when intent.Owner.LastKnownSlot is { } ownerSlot
                && livingPartyAt(ownerSlot) is not null
                => new(intent.Target.Policy, ownerSlot, ownerSlot.Side, false),
            BattleIntentTargetPolicy.Source => null,
            BattleIntentTargetPolicy.Side => new(intent.Target.Policy, null, intent.Target.Side, false),
            BattleIntentTargetPolicy.Field => new(intent.Target.Policy, null, null, true),
            _ => throw new ArgumentOutOfRangeException(nameof(intent), intent.Target.Policy, "Unknown intent target policy."),
        };
    }

    public IReadOnlyList<BattleIntentDebugEntry> DebugSnapshot() => InOrder(_entries)
        .Select(intent => new BattleIntentDebugEntry(intent.Sequence, intent.DueTurn, intent.Checkpoint,
            intent.Owner.Scope, intent.Owner.Side, intent.Owner.LastKnownSlot, intent.Owner.PartyIndex,
            intent.Target.Policy, intent.Target.Slot, intent.Target.SnapshotPartyIndex, intent.Target.Side,
            intent.Payload.Kind, (intent.Payload as ReleaseMoveIntent)?.MoveIndex,
            (intent.Payload as ReleaseMoveIntent)?.State,
            intent.SourceMove, intent.SourceActionSequence, intent.Ruleset))
        .ToArray();

    private IReadOnlyList<BattleIntent> RemoveWhere(Func<BattleIntent, bool> predicate)
    {
        BattleIntent[] removed = InOrder(_entries.Where(predicate)).ToArray();
        var sequences = removed.Select(intent => intent.Sequence).ToHashSet();
        _entries.RemoveAll(intent => sequences.Contains(intent.Sequence));
        return removed;
    }

    private static IOrderedEnumerable<BattleIntent> InOrder(IEnumerable<BattleIntent> intents) => intents
        .OrderBy(intent => intent.DueTurn)
        .ThenBy(intent => intent.Checkpoint)
        .ThenBy(intent => intent.Sequence);

    internal static void ValidateRequest(BattleIntentRequest request)
    {
        if (request.DueTurn < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Intent due turn cannot be negative.");
        if (!Enum.IsDefined(request.Checkpoint))
            throw new ArgumentOutOfRangeException(nameof(request), request.Checkpoint, "Unknown intent checkpoint.");
        if (request.SourceMove.Category != EntityCategory.Move || !EntityId.IsValidSlug(request.SourceMove.Slug))
            throw new ArgumentException("Intent source metadata requires a move ID.", nameof(request));
        if (request.SourceActionSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Intent source action sequence cannot be negative.");
        if (string.IsNullOrWhiteSpace(request.Ruleset))
            throw new ArgumentException("Intent ruleset cannot be blank.", nameof(request));
        ArgumentNullException.ThrowIfNull(request.Owner);
        ArgumentNullException.ThrowIfNull(request.Target);
        ArgumentNullException.ThrowIfNull(request.Payload);
        if (request.Payload is ReleaseMoveIntent release)
        {
            if (release.MoveIndex < 0 || release.State is { } state && !Enum.IsDefined(state))
                throw new ArgumentException("Release-move payload requires a valid move index and state.", nameof(request));
        }
        ValidateOwner(request.Owner);
        ValidateTarget(request.Target);
    }

    private static void ValidateOwner(BattleIntentOwner owner)
    {
        if (!Enum.IsDefined(owner.Scope) || !Enum.IsDefined(owner.Side))
            throw new ArgumentException("Intent owner scope and side must be defined.", nameof(owner));
        if (owner.LastKnownSlot is { } ownerSlot
            && (!Enum.IsDefined(ownerSlot.Side) || ownerSlot.Position < 0))
            throw new ArgumentException("Intent owner slot must be valid.", nameof(owner));

        bool valid = owner.Scope switch
        {
            BattleIntentOwnerScope.Creature => owner.LastKnownSlot is { } slot && slot.Side == owner.Side
                && owner.PartyIndex is >= 0
                && owner.SwitchPolicy is BattleIntentSwitchPolicy.Cancel or BattleIntentSwitchPolicy.FollowOwner
                && owner.FaintPolicy is BattleIntentFaintPolicy.Cancel or BattleIntentFaintPolicy.Persist,
            BattleIntentOwnerScope.Slot => owner.LastKnownSlot is { } slot && slot.Side == owner.Side
                && owner.PartyIndex is null
                && owner.SwitchPolicy == BattleIntentSwitchPolicy.StaySlot
                && owner.FaintPolicy == BattleIntentFaintPolicy.Persist,
            BattleIntentOwnerScope.Side => owner.LastKnownSlot is null && owner.PartyIndex is null
                && owner.SwitchPolicy is null && owner.FaintPolicy is null,
            BattleIntentOwnerScope.Field => owner.LastKnownSlot is null && owner.PartyIndex is null
                && owner.SwitchPolicy is null && owner.FaintPolicy is null,
            _ => false,
        };
        if (!valid)
            throw new ArgumentException("Intent owner fields do not match its scope and cleanup policy.", nameof(owner));
    }

    private static void ValidateTarget(BattleIntentTarget target)
    {
        if (!Enum.IsDefined(target.Policy))
            throw new ArgumentException("Intent target policy must be defined.", nameof(target));
        if (target.Slot is { } targetSlot
            && (!Enum.IsDefined(targetSlot.Side) || targetSlot.Position < 0))
            throw new ArgumentException("Intent target slot must be valid.", nameof(target));
        if (target.Side is { } targetSide && !Enum.IsDefined(targetSide))
            throw new ArgumentException("Intent target side must be defined.", nameof(target));

        bool valid = target.Policy switch
        {
            BattleIntentTargetPolicy.SnapshotSlot => target.Slot is { } slot
                && target.SnapshotPartyIndex is >= 0 && target.Side == slot.Side,
            BattleIntentTargetPolicy.LiveSlot => target.Slot is { } slot
                && target.SnapshotPartyIndex is null && target.Side == slot.Side,
            BattleIntentTargetPolicy.Source => target.Slot is null && target.SnapshotPartyIndex is null && target.Side is null,
            BattleIntentTargetPolicy.Side => target.Slot is null && target.SnapshotPartyIndex is null && target.Side is not null,
            BattleIntentTargetPolicy.Field => target.Slot is null && target.SnapshotPartyIndex is null && target.Side is null,
            _ => false,
        };
        if (!valid)
            throw new ArgumentException("Intent target fields do not match its policy.", nameof(target));
    }
}
