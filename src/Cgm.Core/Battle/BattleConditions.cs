namespace Cgm.Core.Battle;

public readonly record struct BattleConditionId
{
    public BattleConditionId(string value)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value));
        string[] parts = value.Split(':');
        if (parts.Length != 2 || !ValidToken(parts[0]) || !ValidToken(parts[1]))
            throw new ArgumentException("Condition IDs must use lowercase <family>:<slug> tokens.", nameof(value));
        Value = value;
    }

    public string Value { get; } = string.Empty;
    public override string ToString() => Value;

    internal static bool ValidToken(string? value) => !string.IsNullOrWhiteSpace(value)
        && value.All(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_');
}

public enum BattleConditionScope { Field, Weather, Terrain, Room, Side, Slot, Creature }
public enum BattleConditionStackingPolicy { Reject, Refresh, Replace, Stack }
public enum BattleConditionSwitchPolicy { Remove, FollowOwner, StayScope }
public enum BattleConditionFaintPolicy { Remove, Persist }

public enum BattleConditionHook
{
    BattleStart,
    BeforeTurn,
    ActionSelection,
    MoveAvailability,
    ObedienceCheck,
    TargetingQuery,
    GroundedQuery,
    PriorityQuery,
    TurnOrderQuery,
    TryMove,
    BeforeMove,
    ChargeStart,
    AccuracyQuery,
    TryHit,
    MoveTypeQuery,
    BasePowerQuery,
    StatQuery,
    CriticalQuery,
    DamageQuery,
    HealingQuery,
    Hit,
    AfterDamage,
    Contact,
    SecondaryEffect,
    StatusAttempt,
    AfterMove,
    SwitchOut,
    SwitchIn,
    Faint,
    TurnEnd,
    WeatherChange,
    TerrainChange,
    ItemTrigger,
    AbilityTrigger,
    FormChange,
    CaptureAttempt,
    RewardAward,
    FieldInteraction,
}

public sealed record BattleConditionDefinition
{
    public required BattleConditionId Id { get; init; }
    public required BattleConditionScope Scope { get; init; }
    public IReadOnlyList<BattleConditionHook> Hooks { get; init; } = [];
    public int? DefaultDuration { get; init; }
    public BattleIntentCheckpoint? DurationCheckpoint { get; init; }
    public IReadOnlyDictionary<string, int> InitialCounters { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<string> Tags { get; init; } = [];
    public required string StackingKey { get; init; }
    public required BattleConditionStackingPolicy StackingPolicy { get; init; }
    public int MaximumStacks { get; init; } = 1;
    public required BattleConditionSwitchPolicy SwitchPolicy { get; init; }
    public required BattleConditionFaintPolicy FaintPolicy { get; init; }
    public EntryHazardProfile? EntryHazard { get; init; }
    public ProtectionProfile? Protection { get; init; }
}

public sealed record BattleConditionOwner(
    BattleConditionScope Scope,
    BattleSide? Side = null,
    BattleSlot? Slot = null,
    int? PartyIndex = null);

public sealed record BattleConditionSource(BattleSlot? Slot = null, int? PartyIndex = null);

public sealed record BattleConditionApplication(
    BattleConditionId Condition,
    BattleConditionOwner Owner,
    BattleConditionSource Source,
    int Turn,
    int ActionSequence,
    int? Duration = null);

public enum BattleConditionSourceTarget { Any, User, Target, Environment }

public sealed record BattleConditionSelector(
    BattleConditionScope Scope,
    BattleConditionId? Condition,
    string? Tag,
    bool All,
    BattleConditionSourceTarget Source = BattleConditionSourceTarget.Any);

public enum BattleConditionMutationOutcome { Applied, NoMatch, Conflict }

public sealed record BattleConditionMutationResult(
    BattleConditionMutationOutcome Outcome,
    BattleConditionChangeSet Changes);

public sealed record BattleConditionInstance(
    long Sequence,
    BattleConditionDefinition Definition,
    BattleConditionOwner Owner,
    BattleConditionSource Source,
    int AppliedTurn,
    int AppliedActionSequence,
    int? RemainingDuration,
    IReadOnlyList<string> Tags,
    IReadOnlyDictionary<string, int> Counters,
    int StackCount);

public enum BattleConditionRejectionReason { Duplicate, StackLimit }
public enum BattleConditionCleanupReason { Switch, Faint, BattleEnd, Effect }
public enum BattleConditionTraceKind { Applied, Rejected, Refreshed, Replaced, Stacked, Ticked, Expired, Removed, Transferred }

public sealed record BattleConditionTraceEntry(
    int Turn,
    int ActionSequence,
    BattleConditionTraceKind Kind,
    BattleConditionId Condition,
    long? Sequence,
    long? ReplacedSequence,
    BattleConditionScope Scope,
    BattleConditionOwner? OwnerBefore,
    BattleConditionOwner? OwnerAfter,
    int? DurationBefore,
    int? DurationAfter,
    int? StacksBefore,
    int? StacksAfter,
    BattleConditionRejectionReason? RejectionReason = null,
    BattleConditionCleanupReason? CleanupReason = null);

public sealed record BattleConditionChangeSet(
    IReadOnlyList<BattleConditionInstance> Affected,
    IReadOnlyList<BattleEvent> Events,
    IReadOnlyList<BattleConditionTraceEntry> Trace);

public sealed class BattleConditionStores
{
    private readonly BattleConditionRegistry _registry;
    private readonly Dictionary<BattleConditionScope, List<BattleConditionInstance>> _stores =
        Enum.GetValues<BattleConditionScope>().ToDictionary(scope => scope, _ => new List<BattleConditionInstance>());
    private long _nextSequence;

    public BattleConditionStores(BattleConditionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    public BattleConditionChangeSet Apply(BattleConditionApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        BattleConditionDefinition definition = _registry.For(application.Condition);
        return Apply(application, definition);
    }

    public BattleConditionChangeSet Apply(BattleConditionApplication application,
        BattleConditionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(application);
        definition = BattleConditionRegistry.Normalize(definition);
        if (application.Condition != definition.Id)
            throw new ArgumentException("Condition application and definition IDs must match.", nameof(definition));
        ValidateApplication(application, definition);
        int? duration = application.Duration ?? definition.DefaultDuration;
        List<BattleConditionInstance> store = _stores[definition.Scope];
        BattleConditionInstance? existing = store.SingleOrDefault(instance =>
            instance.Owner == application.Owner
            && string.Equals(instance.Definition.StackingKey, definition.StackingKey, StringComparison.Ordinal));
        if (existing is not null
            && (existing.Definition.EntryHazard is not null && definition.EntryHazard is null
                || existing.Definition.EntryHazard is null && definition.EntryHazard is not null
                || existing.Definition.EntryHazard is { } first && definition.EntryHazard is { } second
                    && !EntryHazardConditions.Equivalent(first, second)))
            throw new ArgumentException("Conditions sharing a stacking key must use the same entry-hazard profile.", nameof(definition));
        if (existing is not null && existing.Definition.Id == definition.Id
            && (existing.Definition.Protection is not null && definition.Protection is null
                || existing.Definition.Protection is null && definition.Protection is not null
                || existing.Definition.Protection is { } firstProtection
                    && definition.Protection is { } secondProtection
                    && !ProtectionConditions.Equivalent(firstProtection, secondProtection)))
            throw new ArgumentException("Conditions sharing a stacking key must use the same protection profile.", nameof(definition));

        if (existing is not null)
        {
            return definition.StackingPolicy switch
            {
                BattleConditionStackingPolicy.Reject => Rejected(existing, application, BattleConditionRejectionReason.Duplicate),
                BattleConditionStackingPolicy.Refresh => Refresh(store, existing, application, duration),
                BattleConditionStackingPolicy.Replace => Replace(store, existing, application, definition, duration),
                BattleConditionStackingPolicy.Stack when existing.StackCount >= definition.MaximumStacks
                    => Rejected(existing, application, BattleConditionRejectionReason.StackLimit),
                BattleConditionStackingPolicy.Stack => Stack(store, existing, application),
                _ => throw new ArgumentOutOfRangeException(nameof(definition), definition.StackingPolicy, "Unknown stacking policy."),
            };
        }

        BattleConditionInstance created = Create(application, definition, duration);
        store.Add(created);
        return Change(created,
            new ConditionApplied(definition.Id, definition.Scope, application.Owner, created.Sequence),
            Trace(application.Turn, application.ActionSequence, BattleConditionTraceKind.Applied, created));
    }

    public BattleConditionChangeSet CompleteCheckpoint(BattleIntentCheckpoint checkpoint, int turn, int actionSequence)
    {
        if (!Enum.IsDefined(checkpoint))
            throw new ArgumentOutOfRangeException(nameof(checkpoint), checkpoint, "Unknown condition checkpoint.");
        ValidateTime(turn, actionSequence);
        var affected = new List<BattleConditionInstance>();
        var events = new List<BattleEvent>();
        var trace = new List<BattleConditionTraceEntry>();
        BattleConditionInstance[] captured = Snapshot()
            .Where(instance => instance.RemainingDuration is not null
                && instance.Definition.DurationCheckpoint == checkpoint)
            .ToArray();

        foreach (BattleConditionInstance before in captured)
        {
            List<BattleConditionInstance> store = _stores[before.Definition.Scope];
            int index = store.FindIndex(instance => instance.Sequence == before.Sequence);
            if (index < 0)
                continue;
            BattleConditionInstance after = before with { RemainingDuration = before.RemainingDuration - 1 };
            affected.Add(after);
            trace.Add(Trace(turn, actionSequence, BattleConditionTraceKind.Ticked, after,
                before: before));
            if (after.RemainingDuration == 0)
            {
                store.RemoveAt(index);
                events.Add(new ConditionExpired(after.Definition.Id, after.Definition.Scope, after.Owner, after.Sequence));
                trace.Add(Trace(turn, actionSequence, BattleConditionTraceKind.Expired, after,
                    before: after, removed: true));
            }
            else
            {
                store[index] = after;
            }
        }
        return new BattleConditionChangeSet(affected, events, trace);
    }

    public BattleConditionChangeSet OwnerSwitched(BattleSide side, int partyIndex, BattleSlot? destination,
        int turn, int actionSequence)
    {
        ValidateIdentity(side, partyIndex, destination);
        ValidateTime(turn, actionSequence);
        var changes = new ChangeBuilder();
        foreach (BattleConditionInstance instance in Snapshot(BattleConditionScope.Creature)
            .Where(instance => instance.Owner.Side == side && instance.Owner.PartyIndex == partyIndex))
        {
            if (instance.Definition.SwitchPolicy == BattleConditionSwitchPolicy.Remove)
            {
                Remove(instance, BattleConditionCleanupReason.Switch, turn, actionSequence, changes);
                continue;
            }
            BattleConditionOwner owner = instance.Owner with { Slot = destination };
            ReplaceStored(instance, instance with { Owner = owner });
            changes.Affected.Add(instance with { Owner = owner });
            changes.Events.Add(new ConditionTransferred(instance.Definition.Id, instance.Definition.Scope,
                instance.Owner, owner, instance.Sequence));
            changes.Trace.Add(Trace(turn, actionSequence, BattleConditionTraceKind.Transferred,
                instance with { Owner = owner }, before: instance));
        }
        return changes.Build();
    }

    public BattleConditionChangeSet OwnerFainted(BattleSide side, int partyIndex, int turn, int actionSequence)
    {
        ValidateIdentity(side, partyIndex, null);
        ValidateTime(turn, actionSequence);
        var changes = new ChangeBuilder();
        foreach (BattleConditionInstance instance in Snapshot(BattleConditionScope.Creature)
            .Where(instance => instance.Owner.Side == side
                && instance.Owner.PartyIndex == partyIndex
                && instance.Definition.FaintPolicy == BattleConditionFaintPolicy.Remove))
            Remove(instance, BattleConditionCleanupReason.Faint, turn, actionSequence, changes);
        return changes.Build();
    }

    public BattleConditionChangeSet SourceLeft(BattleSide side, int partyIndex,
        BattleConditionCleanupReason reason, int turn, int actionSequence)
    {
        ValidateIdentity(side, partyIndex, null);
        if (reason is not (BattleConditionCleanupReason.Switch or BattleConditionCleanupReason.Faint))
            throw new ArgumentException("Source cleanup requires switch or faint.", nameof(reason));
        ValidateTime(turn, actionSequence);
        var changes = new ChangeBuilder();
        foreach (BattleConditionInstance instance in Snapshot().Where(instance =>
            instance.Tags.Contains("source_bound", StringComparer.Ordinal)
            && instance.Source.Slot?.Side == side && instance.Source.PartyIndex == partyIndex))
            Remove(instance, reason, turn, actionSequence, changes);
        return changes.Build();
    }

    public BattleConditionChangeSet EndBattle(int turn, int actionSequence)
    {
        ValidateTime(turn, actionSequence);
        var changes = new ChangeBuilder();
        foreach (BattleConditionInstance instance in Snapshot())
            Remove(instance, BattleConditionCleanupReason.BattleEnd, turn, actionSequence, changes);
        return changes.Build();
    }

    public BattleConditionChangeSet Remove(BattleConditionId condition, BattleConditionOwner owner,
        int turn, int actionSequence)
    {
        ValidateTime(turn, actionSequence);
        var changes = new ChangeBuilder();
        BattleConditionInstance? instance = Snapshot()
            .SingleOrDefault(candidate => candidate.Definition.Id == condition && candidate.Owner == owner);
        if (instance is not null)
            Remove(instance, BattleConditionCleanupReason.Effect, turn, actionSequence, changes);
        return changes.Build();
    }

    public BattleConditionChangeSet RemoveTagged(BattleConditionScope scope, BattleConditionOwner owner,
        string tag, int turn, int actionSequence)
    {
        if (!Enum.IsDefined(scope))
            throw new ArgumentOutOfRangeException(nameof(scope));
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ValidateTime(turn, actionSequence);
        var changes = new ChangeBuilder();
        foreach (BattleConditionInstance instance in Snapshot(scope).Where(instance =>
            instance.Owner == owner && instance.Tags.Contains(tag, StringComparer.Ordinal)))
            Remove(instance, BattleConditionCleanupReason.Effect, turn, actionSequence, changes);
        return changes.Build();
    }

    public BattleConditionMutationResult RemoveSelected(BattleConditionSelector selector,
        BattleConditionOwner owner, BattleConditionSource userSource, BattleConditionSource targetSource,
        int turn, int actionSequence)
    {
        ValidateMutation(selector, owner, userSource, targetSource, turn, actionSequence);
        BattleConditionInstance[] selected = Select(selector, owner, userSource, targetSource);
        if (selected.Length == 0)
            return NoMatch();
        var changes = new ChangeBuilder();
        foreach (BattleConditionInstance instance in selected)
            Remove(instance, BattleConditionCleanupReason.Effect, turn, actionSequence, changes);
        return Applied(changes);
    }

    public BattleConditionMutationResult TransferSelected(BattleConditionSelector selector,
        BattleConditionOwner from, BattleConditionOwner to,
        BattleConditionSource userSource, BattleConditionSource targetSource,
        bool resetDuration, bool resetCounters, int turn, int actionSequence)
    {
        ValidateMutation(selector, from, userSource, targetSource, turn, actionSequence);
        ValidateOwner(to, selector.Scope);
        if (selector.Scope is not (BattleConditionScope.Side or BattleConditionScope.Slot
            or BattleConditionScope.Creature))
            throw new ArgumentException("Condition transfer supports side, slot, or creature scope.", nameof(selector));
        if (from == to)
            throw new ArgumentException("Condition transfer requires distinct owners.", nameof(to));
        BattleConditionInstance[] selected = Select(selector, from, userSource, targetSource);
        if (selected.Length == 0)
            return NoMatch();
        if (!CanReset(selected, resetDuration) || HasStackingConflict(selector.Scope,
                selected.ToDictionary(instance => instance.Sequence, _ => to)))
            return Conflict();
        var changes = new ChangeBuilder();
        foreach (BattleConditionInstance before in selected)
            Move(before, to, resetDuration, resetCounters, turn, actionSequence, changes);
        return Applied(changes);
    }

    public BattleConditionMutationResult SwapSelected(BattleConditionSelector selector,
        BattleConditionOwner first, BattleConditionOwner second,
        BattleConditionSource userSource, BattleConditionSource targetSource,
        bool resetDuration, bool resetCounters, int turn, int actionSequence)
    {
        ValidateMutation(selector, first, userSource, targetSource, turn, actionSequence);
        ValidateOwner(second, selector.Scope);
        if (selector.Scope is not (BattleConditionScope.Side or BattleConditionScope.Slot))
            throw new ArgumentException("Condition swap supports side or slot scope.", nameof(selector));
        if (first == second)
            throw new ArgumentException("Condition swap requires distinct owners.", nameof(second));
        BattleConditionInstance[] firstSelected = Select(selector, first, userSource, targetSource);
        BattleConditionInstance[] secondSelected = Select(selector, second, userSource, targetSource);
        HashSet<long> selectedSequences = [.. firstSelected.Select(instance => instance.Sequence),
            .. secondSelected.Select(instance => instance.Sequence)];
        BattleConditionInstance[] selected = Snapshot(selector.Scope)
            .Where(instance => selectedSequences.Contains(instance.Sequence)).ToArray();
        if (selected.Length == 0)
            return NoMatch();
        var destinations = firstSelected.ToDictionary(instance => instance.Sequence, _ => second);
        foreach (BattleConditionInstance instance in secondSelected)
            destinations.Add(instance.Sequence, first);
        if (!CanReset(selected, resetDuration) || HasStackingConflict(selector.Scope, destinations))
            return Conflict();
        var changes = new ChangeBuilder();
        foreach (BattleConditionInstance before in selected)
            Move(before, destinations[before.Sequence], resetDuration, resetCounters,
                turn, actionSequence, changes);
        return Applied(changes);
    }

    public IReadOnlyList<BattleConditionInstance> Snapshot() => InOrder(_stores.Values.SelectMany(store => store)).ToArray();

    public IReadOnlyList<BattleConditionInstance> Snapshot(BattleConditionScope scope)
    {
        if (!Enum.IsDefined(scope))
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown condition scope.");
        return InOrder(_stores[scope]).ToArray();
    }

    private BattleConditionInstance Create(BattleConditionApplication application,
        BattleConditionDefinition definition, int? duration)
    {
        if (_nextSequence == long.MaxValue)
            throw new OverflowException("Battle condition sequence is exhausted.");
        return new BattleConditionInstance(_nextSequence++, definition, application.Owner, application.Source,
            application.Turn, application.ActionSequence, duration, definition.Tags, definition.InitialCounters, 1);
    }

    private BattleConditionChangeSet Replace(List<BattleConditionInstance> store, BattleConditionInstance existing,
        BattleConditionApplication application, BattleConditionDefinition definition, int? duration)
    {
        BattleConditionInstance created = Create(application, definition, duration);
        store[store.FindIndex(instance => instance.Sequence == existing.Sequence)] = created;
        return Change(created,
            new ConditionReplaced(definition.Id, definition.Scope, application.Owner, existing.Sequence, created.Sequence),
            Trace(application.Turn, application.ActionSequence, BattleConditionTraceKind.Replaced, created,
                before: existing, replacedSequence: existing.Sequence));
    }

    private static BattleConditionChangeSet Refresh(List<BattleConditionInstance> store,
        BattleConditionInstance existing, BattleConditionApplication application, int? duration)
    {
        BattleConditionInstance refreshed = existing with { RemainingDuration = duration };
        store[store.FindIndex(instance => instance.Sequence == existing.Sequence)] = refreshed;
        return Change(refreshed,
            new ConditionRefreshed(existing.Definition.Id, existing.Definition.Scope, existing.Owner,
                existing.Sequence, duration),
            Trace(application.Turn, application.ActionSequence, BattleConditionTraceKind.Refreshed, refreshed,
                before: existing));
    }

    private static BattleConditionChangeSet Stack(List<BattleConditionInstance> store,
        BattleConditionInstance existing, BattleConditionApplication application)
    {
        BattleConditionInstance stacked = existing with { StackCount = existing.StackCount + 1 };
        store[store.FindIndex(instance => instance.Sequence == existing.Sequence)] = stacked;
        return Change(stacked,
            new ConditionStacked(existing.Definition.Id, existing.Definition.Scope, existing.Owner,
                existing.Sequence, stacked.StackCount),
            Trace(application.Turn, application.ActionSequence, BattleConditionTraceKind.Stacked, stacked,
                before: existing));
    }

    private static BattleConditionChangeSet Rejected(BattleConditionInstance existing,
        BattleConditionApplication application, BattleConditionRejectionReason reason) =>
        Change(existing,
            new ConditionApplicationRejected(existing.Definition.Id, existing.Definition.Scope,
                existing.Owner, existing.Sequence, reason),
            Trace(application.Turn, application.ActionSequence, BattleConditionTraceKind.Rejected, existing,
                before: existing, rejectionReason: reason));

    private static BattleConditionChangeSet Change(BattleConditionInstance instance, BattleEvent battleEvent,
        BattleConditionTraceEntry trace) => new([instance], [battleEvent], [trace]);

    private void Remove(BattleConditionInstance instance, BattleConditionCleanupReason reason,
        int turn, int actionSequence, ChangeBuilder changes)
    {
        _stores[instance.Definition.Scope].RemoveAll(candidate => candidate.Sequence == instance.Sequence);
        changes.Affected.Add(instance);
        changes.Events.Add(new ConditionRemoved(instance.Definition.Id, instance.Definition.Scope,
            instance.Owner, instance.Sequence, reason));
        changes.Trace.Add(Trace(turn, actionSequence, BattleConditionTraceKind.Removed, instance,
            before: instance, removed: true, cleanupReason: reason));
    }

    private void ReplaceStored(BattleConditionInstance before, BattleConditionInstance after)
    {
        List<BattleConditionInstance> store = _stores[before.Definition.Scope];
        store[store.FindIndex(instance => instance.Sequence == before.Sequence)] = after;
    }

    private void Move(BattleConditionInstance before, BattleConditionOwner owner,
        bool resetDuration, bool resetCounters, int turn, int actionSequence, ChangeBuilder changes)
    {
        BattleConditionInstance after = before with
        {
            Owner = owner,
            RemainingDuration = resetDuration ? before.Definition.DefaultDuration : before.RemainingDuration,
            Counters = resetCounters ? before.Definition.InitialCounters : before.Counters,
            StackCount = resetCounters ? 1 : before.StackCount,
        };
        ReplaceStored(before, after);
        changes.Affected.Add(after);
        changes.Events.Add(new ConditionTransferred(after.Definition.Id, after.Definition.Scope,
            before.Owner, owner, after.Sequence));
        changes.Trace.Add(Trace(turn, actionSequence, BattleConditionTraceKind.Transferred,
            after, before: before));
    }

    private BattleConditionInstance[] Select(BattleConditionSelector selector, BattleConditionOwner owner,
        BattleConditionSource userSource, BattleConditionSource targetSource) => Snapshot(selector.Scope)
        .Where(instance => instance.Owner == owner
            && (selector.All
                || selector.Condition is { } condition && instance.Definition.Id == condition
                || selector.Tag is { } tag && instance.Tags.Contains(tag, StringComparer.Ordinal))
            && selector.Source switch
            {
                BattleConditionSourceTarget.Any => true,
                BattleConditionSourceTarget.User => SameSourceCreature(instance.Source, userSource),
                BattleConditionSourceTarget.Target => SameSourceCreature(instance.Source, targetSource),
                BattleConditionSourceTarget.Environment => instance.Source == new BattleConditionSource(),
                _ => false,
            })
        .ToArray();

    private static bool SameSourceCreature(BattleConditionSource actual, BattleConditionSource expected) =>
        actual.PartyIndex == expected.PartyIndex && actual.Slot?.Side == expected.Slot?.Side;

    private bool HasStackingConflict(BattleConditionScope scope,
        IReadOnlyDictionary<long, BattleConditionOwner> destinations) => Snapshot(scope)
        .Select(instance => (Owner: destinations.TryGetValue(instance.Sequence, out BattleConditionOwner? owner)
                ? owner : instance.Owner,
            instance.Definition.StackingKey))
        .GroupBy(value => value)
        .Any(group => group.Count() > 1);

    private static bool CanReset(IEnumerable<BattleConditionInstance> selected, bool resetDuration) =>
        !resetDuration || selected.All(instance => instance.Definition.DefaultDuration is not null);

    private static void ValidateMutation(BattleConditionSelector selector, BattleConditionOwner owner,
        BattleConditionSource userSource, BattleConditionSource targetSource, int turn, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (!Enum.IsDefined(selector.Scope) || !Enum.IsDefined(selector.Source))
            throw new ArgumentException("Condition selector enums must be defined.", nameof(selector));
        int modes = (selector.Condition is null ? 0 : 1) + (selector.Tag is null ? 0 : 1) + (selector.All ? 1 : 0);
        if (modes != 1 || selector.Tag is { } tag && !BattleConditionId.ValidToken(tag))
            throw new ArgumentException("Condition selector requires exactly one valid condition, tag, or all mode.", nameof(selector));
        ValidateOwner(owner, selector.Scope);
        ValidateSource(userSource);
        ValidateSource(targetSource);
        ValidateTime(turn, actionSequence);
    }

    private static BattleConditionMutationResult Applied(ChangeBuilder changes) =>
        new(BattleConditionMutationOutcome.Applied, changes.Build());

    private static BattleConditionMutationResult NoMatch() =>
        new(BattleConditionMutationOutcome.NoMatch, new BattleConditionChangeSet([], [], []));

    private static BattleConditionMutationResult Conflict() =>
        new(BattleConditionMutationOutcome.Conflict, new BattleConditionChangeSet([], [], []));

    private static BattleConditionTraceEntry Trace(int turn, int actionSequence, BattleConditionTraceKind kind,
        BattleConditionInstance instance, BattleConditionInstance? before = null,
        long? replacedSequence = null, bool removed = false,
        BattleConditionRejectionReason? rejectionReason = null, BattleConditionCleanupReason? cleanupReason = null) =>
        new(turn, actionSequence, kind, instance.Definition.Id, instance.Sequence, replacedSequence,
            instance.Definition.Scope, before?.Owner, removed ? null : instance.Owner,
            before?.RemainingDuration, removed ? null : instance.RemainingDuration,
            before?.StackCount, removed ? null : instance.StackCount, rejectionReason, cleanupReason);

    private static IOrderedEnumerable<BattleConditionInstance> InOrder(IEnumerable<BattleConditionInstance> instances) =>
        instances.OrderBy(instance => instance.Definition.Scope)
            .ThenBy(instance => instance.Owner.Side is null ? 2 : (int)instance.Owner.Side.Value)
            .ThenBy(instance => instance.Owner.Slot?.Position ?? int.MaxValue)
            .ThenBy(instance => instance.Owner.PartyIndex ?? int.MaxValue)
            .ThenBy(instance => instance.Sequence);

    private static void ValidateApplication(BattleConditionApplication application, BattleConditionDefinition definition)
    {
        ValidateTime(application.Turn, application.ActionSequence);
        ValidateOwner(application.Owner, definition.Scope);
        ValidateSource(application.Source);
        if (application.Duration is <= 0)
            throw new ArgumentException("Condition application duration must be positive.", nameof(application));
        int? duration = application.Duration ?? definition.DefaultDuration;
        if ((duration is not null) != (definition.DurationCheckpoint is not null))
            throw new ArgumentException("Condition application duration does not match its checkpoint contract.", nameof(application));
    }

    private static void ValidateOwner(BattleConditionOwner owner, BattleConditionScope scope)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!Enum.IsDefined(owner.Scope) || owner.Scope != scope)
            throw new ArgumentException("Condition owner scope must match its definition.", nameof(owner));
        if (owner.Side is { } ownerSide && !Enum.IsDefined(ownerSide))
            throw new ArgumentException("Condition owner side must be defined.", nameof(owner));
        if (owner.Slot is { } ownerSlot && (!Enum.IsDefined(ownerSlot.Side) || ownerSlot.Position < 0))
            throw new ArgumentException("Condition owner slot must be valid.", nameof(owner));

        bool valid = scope switch
        {
            BattleConditionScope.Creature => owner.Side is { } creatureSide
                && owner.PartyIndex is >= 0
                && (owner.Slot is null || owner.Slot.Value.Side == creatureSide),
            BattleConditionScope.Side => owner.Side is not null && owner.Slot is null && owner.PartyIndex is null,
            BattleConditionScope.Slot => owner.Slot is { } exactSlot && owner.Side == exactSlot.Side && owner.PartyIndex is null,
            BattleConditionScope.Field or BattleConditionScope.Weather or BattleConditionScope.Terrain or BattleConditionScope.Room
                => owner.Side is null && owner.Slot is null && owner.PartyIndex is null,
            _ => false,
        };
        if (!valid)
            throw new ArgumentException("Condition owner fields do not match its scope.", nameof(owner));
    }

    private static void ValidateSource(BattleConditionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if ((source.Slot is null) != (source.PartyIndex is null)
            || source.PartyIndex is < 0
            || source.Slot is { } slot && (!Enum.IsDefined(slot.Side) || slot.Position < 0))
            throw new ArgumentException("Condition source must contain both a valid slot and party index, or neither.", nameof(source));
    }

    private static void ValidateIdentity(BattleSide side, int partyIndex, BattleSlot? destination)
    {
        if (!Enum.IsDefined(side) || partyIndex < 0
            || destination is { } slot && (!Enum.IsDefined(slot.Side) || slot.Side != side || slot.Position < 0))
            throw new ArgumentException("Condition cleanup requires a valid owner identity and destination.");
    }

    private static void ValidateTime(int turn, int actionSequence)
    {
        if (turn < 0 || actionSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(turn), "Condition turn and action sequence cannot be negative.");
    }

    private sealed class ChangeBuilder
    {
        public List<BattleConditionInstance> Affected { get; } = [];
        public List<BattleEvent> Events { get; } = [];
        public List<BattleConditionTraceEntry> Trace { get; } = [];
        public BattleConditionChangeSet Build() => new(Affected, Events, Trace);
    }
}
