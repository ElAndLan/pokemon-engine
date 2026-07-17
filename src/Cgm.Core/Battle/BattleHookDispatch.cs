using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattleHookScope { Field, Side, Slot, Creature, Ability, Item, Move }
public enum BattleHookPayloadKind { QueryModifier, Filter, Intent, MoveType }
public enum BattleHookFilterDecision { Allow, Deny }
public enum BattleHookDispatchFailureKind { IntentLimitExceeded }

public readonly record struct BattleHookInstanceId
{
    public BattleHookInstanceId(string value)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value));
        string[] parts = value.Split(':');
        if (parts.Length != 2 || !BattleConditionId.ValidToken(parts[0]) || !BattleConditionId.ValidToken(parts[1]))
            throw new ArgumentException("Hook instance IDs must use lowercase <kind>:<slug> tokens.", nameof(value));
        Value = value;
    }

    public string Value { get; } = string.Empty;
    public override string ToString() => Value;
}

public readonly record struct BattleHookFilterId
{
    public BattleHookFilterId(string value)
    {
        if (!BattleConditionId.ValidToken(value))
            throw new ArgumentException("Hook filter IDs must be lowercase tokens.", nameof(value));
        Value = value;
    }

    public string Value { get; } = string.Empty;
    public override string ToString() => Value;
}

public sealed record BattleHookOwner(
    BattleSide? Side = null,
    BattleSlot? Slot = null,
    int? PartyIndex = null);

public abstract record BattleHookPayload
{
    internal BattleHookPayload() { }
    public abstract BattleHookPayloadKind Kind { get; }
}

public sealed record BattleHookQueryModifier(
    BattleQueryId Query,
    BattleQueryModifier Modifier) : BattleHookPayload
{
    public override BattleHookPayloadKind Kind => BattleHookPayloadKind.QueryModifier;
}

public sealed record BattleHookFilter(
    BattleHookFilterId Filter,
    BattleHookFilterDecision Decision) : BattleHookPayload
{
    public override BattleHookPayloadKind Kind => BattleHookPayloadKind.Filter;
}

public sealed record BattleHookIntent(BattleIntentRequest Request) : BattleHookPayload
{
    public override BattleHookPayloadKind Kind => BattleHookPayloadKind.Intent;
}

public sealed record BattleHookMoveType(EntityId Type) : BattleHookPayload
{
    public override BattleHookPayloadKind Kind => BattleHookPayloadKind.MoveType;
}

public sealed record BattleHookRegistration(
    BattleConditionHook Checkpoint,
    int Priority,
    BattleHookScope Scope,
    BattleHookOwner Owner,
    BattleHookInstanceId Instance,
    long Sequence,
    int PayloadIndex,
    BattleHookPayload Payload)
{
    public static BattleHookRegistration ForCondition(BattleConditionInstance condition,
        BattleConditionHook checkpoint, int priority, int payloadIndex, BattleHookPayload payload)
    {
        ArgumentNullException.ThrowIfNull(condition);
        if (!condition.Definition.Hooks.Contains(checkpoint))
            throw new ArgumentException("The condition definition does not declare this hook.", nameof(checkpoint));

        BattleHookScope scope = condition.Definition.Scope switch
        {
            BattleConditionScope.Field or BattleConditionScope.Weather or BattleConditionScope.Terrain
                or BattleConditionScope.Room => BattleHookScope.Field,
            BattleConditionScope.Side => BattleHookScope.Side,
            BattleConditionScope.Slot => BattleHookScope.Slot,
            BattleConditionScope.Creature => BattleHookScope.Creature,
            _ => throw new ArgumentOutOfRangeException(nameof(condition), condition.Definition.Scope,
                "Unknown condition scope."),
        };
        return new BattleHookRegistration(checkpoint, priority, scope,
            new BattleHookOwner(condition.Owner.Side, condition.Owner.Slot, condition.Owner.PartyIndex),
            new BattleHookInstanceId($"condition:{condition.Sequence}"), condition.Sequence, payloadIndex, payload);
    }
}

public sealed record BattleHookDispatchContext(
    int ActionSequence,
    BattleConditionHook Checkpoint,
    int PreviouslyEmittedIntents = 0);

public sealed record BattleHookTraceEntry(
    int ActionSequence,
    BattleConditionHook Checkpoint,
    int Priority,
    BattleHookScope Scope,
    BattleHookOwner Owner,
    BattleHookInstanceId Instance,
    long Sequence,
    int PayloadIndex,
    BattleHookPayloadKind PayloadKind,
    bool Invoked);

public sealed record BattleHookDispatchFailure(
    BattleHookDispatchFailureKind Kind,
    int Limit,
    int AttemptedCount);

public sealed class BattleHookDispatchSnapshot
{
    internal BattleHookDispatchSnapshot(BattleHookDispatchContext context,
        IReadOnlyList<BattleHookRegistration> invocations,
        IReadOnlyList<BattleHookTraceEntry> trace,
        int emittedIntentCount,
        BattleHookDispatchFailure? failure)
    {
        Context = context;
        Invocations = invocations;
        Trace = trace;
        EmittedIntentCount = emittedIntentCount;
        Failure = failure;
    }

    internal bool Completed { get; set; }
    public BattleHookDispatchContext Context { get; }
    public IReadOnlyList<BattleHookRegistration> Invocations { get; }
    public IReadOnlyList<BattleHookTraceEntry> Trace { get; }
    public int EmittedIntentCount { get; }
    public BattleHookDispatchFailure? Failure { get; }
    public bool Succeeded => Failure is null;

    public IReadOnlyList<BattleQueryModifier> QueryModifiers(BattleQueryId query)
    {
        if (!Enum.IsDefined(query))
            throw new ArgumentOutOfRangeException(nameof(query), query, "Unknown battle query.");

        int insertion = 0;
        return Invocations
            .Where(entry => entry.Payload is BattleHookQueryModifier payload && payload.Query == query)
            .Select(entry =>
            {
                BattleQueryModifier modifier = ((BattleHookQueryModifier)entry.Payload).Modifier;
                return modifier with { Priority = entry.Priority, InsertionOrder = insertion++ };
            })
            .ToArray();
    }

    public IReadOnlyList<BattleHookFilter> Filters() =>
        Invocations.Select(entry => entry.Payload).OfType<BattleHookFilter>().ToArray();

    public IReadOnlyList<EntityId> MoveTypes() =>
        Invocations.Select(entry => entry.Payload).OfType<BattleHookMoveType>().Select(payload => payload.Type).ToArray();
}

public sealed record BattleHookCompletion(
    IReadOnlyList<BattleIntent> EnqueuedIntents,
    IReadOnlyList<BattleEvent> Events);

public static partial class BattleHookDispatcher
{
    public const int MaximumEmittedIntents = 64;

    public static BattleHookDispatchSnapshot Collect(BattleHookDispatchContext context,
        IEnumerable<BattleHookRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(registrations);
        ValidateContext(context);

        BattleHookRegistration[] captured = registrations.ToArray();
        foreach (BattleHookRegistration registration in captured)
            Validate(registration);
        if (captured
            .GroupBy(registration => (registration.Checkpoint, registration.Instance, registration.PayloadIndex))
            .Any(group => group.Distinct().Count() > 1))
            throw new ArgumentException("Duplicate hook invocation identities must have identical registrations.",
                nameof(registrations));

        BattleHookRegistration[] ordered = captured
            .Where(registration => registration.Checkpoint == context.Checkpoint)
            .OrderByDescending(registration => registration.Priority)
            .ThenBy(registration => registration.Scope)
            .ThenBy(registration => SideOrder(registration.Owner.Side))
            .ThenBy(registration => registration.Owner.Slot?.Position ?? int.MaxValue)
            .ThenBy(registration => registration.Owner.PartyIndex ?? int.MaxValue)
            .ThenBy(registration => registration.Sequence)
            .ThenBy(registration => registration.Instance.Value, StringComparer.Ordinal)
            .ThenBy(registration => registration.PayloadIndex)
            .ToArray();

        var invoked = new List<BattleHookRegistration>();
        var trace = new List<BattleHookTraceEntry>(ordered.Length);
        var identities = new HashSet<(BattleHookInstanceId Instance, int Payload)>();
        int emittedIntents = context.PreviouslyEmittedIntents;
        foreach (BattleHookRegistration registration in ordered)
        {
            bool invoke = identities.Add((registration.Instance, registration.PayloadIndex));
            trace.Add(Trace(context, registration, invoke));
            if (!invoke)
                continue;
            invoked.Add(registration);
            if (registration.Payload is BattleHookIntent && ++emittedIntents > MaximumEmittedIntents)
            {
                return new BattleHookDispatchSnapshot(context, [], trace.ToArray(), emittedIntents,
                    new BattleHookDispatchFailure(BattleHookDispatchFailureKind.IntentLimitExceeded,
                        MaximumEmittedIntents, emittedIntents));
            }
        }

        return new BattleHookDispatchSnapshot(context, invoked.ToArray(), trace.ToArray(), emittedIntents, null);
    }

    public static BattleHookCompletion Complete(BattleHookDispatchSnapshot snapshot, BattleIntentQueue intentQueue)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(intentQueue);
        if (snapshot.Completed)
            throw new ArgumentException("Hook dispatch snapshots may be completed only once.", nameof(snapshot));

        snapshot.Completed = true;
        if (snapshot.Failure is { } failure)
        {
            return new BattleHookCompletion([],
            [
                new HookDispatchFailed(snapshot.Context.ActionSequence, snapshot.Context.Checkpoint,
                    failure.Kind, failure.Limit),
            ]);
        }

        BattleIntentRequest[] requests = snapshot.Invocations
            .Select(registration => registration.Payload)
            .OfType<BattleHookIntent>()
            .Select(payload => payload.Request)
            .ToArray();
        return new BattleHookCompletion(intentQueue.EnqueueRange(requests), []);
    }

    private static BattleHookTraceEntry Trace(BattleHookDispatchContext context,
        BattleHookRegistration registration, bool invoked) => new(
        context.ActionSequence, context.Checkpoint, registration.Priority, registration.Scope,
        registration.Owner, registration.Instance, registration.Sequence, registration.PayloadIndex,
        registration.Payload.Kind, invoked);

    private static int SideOrder(BattleSide? side) => side is null ? 2 : (int)side.Value;

    private static void ValidateContext(BattleHookDispatchContext context)
    {
        if (context.ActionSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(context), "Hook action sequence cannot be negative.");
        if (!Enum.IsDefined(context.Checkpoint))
            throw new ArgumentOutOfRangeException(nameof(context), context.Checkpoint, "Unknown hook checkpoint.");
        if (context.PreviouslyEmittedIntents is < 0 or > MaximumEmittedIntents)
            throw new ArgumentOutOfRangeException(nameof(context),
                $"Previously emitted intents must be between zero and {MaximumEmittedIntents}.");
    }

    private static void Validate(BattleHookRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(registration.Owner);
        ArgumentNullException.ThrowIfNull(registration.Payload);
        if (!Enum.IsDefined(registration.Checkpoint) || !Enum.IsDefined(registration.Scope))
            throw new ArgumentException("Hook checkpoint and scope must be defined.", nameof(registration));
        if (string.IsNullOrEmpty(registration.Instance.Value))
            throw new ArgumentException("Hook instance ID cannot be default.", nameof(registration));
        if (registration.Sequence < 0 || registration.PayloadIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(registration),
                "Hook sequence and payload index cannot be negative.");
        ValidateOwner(registration.Scope, registration.Owner);
        ValidatePayload(registration.Payload);
    }

    private static void ValidateOwner(BattleHookScope scope, BattleHookOwner owner)
    {
        if (owner.Side is { } side && !Enum.IsDefined(side))
            throw new ArgumentException("Hook owner side must be defined.", nameof(owner));
        if (owner.Slot is { } slot && (!Enum.IsDefined(slot.Side) || slot.Position < 0))
            throw new ArgumentException("Hook owner slot must be valid.", nameof(owner));

        bool valid = scope switch
        {
            BattleHookScope.Field => owner.Side is null && owner.Slot is null && owner.PartyIndex is null,
            BattleHookScope.Side => owner.Side is not null && owner.Slot is null && owner.PartyIndex is null,
            BattleHookScope.Slot => owner.Slot is { } exactSlot && owner.Side == exactSlot.Side
                && owner.PartyIndex is null,
            BattleHookScope.Creature => owner.Side is { } creatureSide && owner.PartyIndex is >= 0
                && (owner.Slot is null || owner.Slot.Value.Side == creatureSide),
            BattleHookScope.Ability or BattleHookScope.Item or BattleHookScope.Move
                => owner.Slot is { } sourceSlot && owner.Side == sourceSlot.Side && owner.PartyIndex is >= 0,
            _ => false,
        };
        if (!valid)
            throw new ArgumentException("Hook owner fields do not match the source scope.", nameof(owner));
    }

    private static void ValidatePayload(BattleHookPayload payload)
    {
        if (!Enum.IsDefined(payload.Kind))
            throw new ArgumentException("Hook payload kind must be defined.", nameof(payload));
        switch (payload)
        {
            case BattleHookQueryModifier query:
                if (!Enum.IsDefined(query.Query)
                    || !query.Modifier.Operand.IsValid
                    || query.Modifier.Stage is not (BattleQueryStage.SourceTargetState
                        or BattleQueryStage.Hooks or BattleQueryStage.RulesetOverride)
                    || !Enum.IsDefined(query.Modifier.Operation)
                    || !Enum.IsDefined(query.Modifier.OwnerScope)
                    || query.Modifier.InsertionOrder < 0)
                    throw new ArgumentException("Hook query modifier is invalid.", nameof(payload));
                break;
            case BattleHookFilter filter when string.IsNullOrEmpty(filter.Filter.Value)
                || !Enum.IsDefined(filter.Decision):
                throw new ArgumentException("Hook filter is invalid.", nameof(payload));
            case BattleHookIntent intent:
                ArgumentNullException.ThrowIfNull(intent.Request);
                BattleIntentQueue.ValidateRequest(intent.Request);
                break;
            case BattleHookMoveType moveType when moveType.Type.Category != EntityCategory.Type:
                throw new ArgumentException("Hook move type must be a type EntityId.", nameof(payload));
            case BattleHookMoveType:
                break;
            case BattleHookFilter:
                break;
            default:
                throw new ArgumentException("Unknown hook payload type.", nameof(payload));
        }
    }
}
