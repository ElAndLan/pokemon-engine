using System.Collections.ObjectModel;
using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattleOverlayLayer { PermanentInstance, FormOrSnapshot, Additive, Suppression }

[Flags]
public enum BattleOverlayCleanup { None = 0, Switch = 1, Faint = 2, BattleEnd = 4 }

public enum BattleEffectiveValueKind
{
    HeldItem,
    Ability,
    CreatureTypes,
    Stats,
    MoveList,
    MoveType,
    MoveClass,
    Form,
    Decoy,
    Metric,
    Transform,
}

public enum BattleMetric { Weight, Height }
public enum BattleOverlayTraceKind { Applied, Ticked, Expired, Removed, Transferred, Resolved, Superseded, Suppressed, SuppressionIgnored }
public enum BattleOverlayRemovalReason { Switch, Faint, BattleEnd, Expired }

public sealed record BattleDecoyState(int Hp, int MaxHp);

public sealed record BattleEffectiveMove(
    BattleMove Definition,
    int PpOwnerSlot,
    EntityId Type,
    DamageClass DamageClass)
{
    public static BattleEffectiveMove FromBase(BattleMove move, int slot)
    {
        ArgumentNullException.ThrowIfNull(move);
        return new BattleEffectiveMove(move, slot, move.Type, move.DamageClass);
    }
}

public sealed record BattleMoveTypeRule(EntityId Type, EntityId? MatchType = null);

public sealed record BattleEffectiveValues
{
    public BattleEffectiveValues(EntityId? heldItem, EntityId? ability, IReadOnlyList<EntityId> creatureTypes,
        Stats stats, IReadOnlyList<BattleEffectiveMove> moves, string? formId = null,
        BattleDecoyState? decoy = null, IReadOnlyDictionary<BattleMetric, int>? metrics = null,
        IReadOnlyList<BattleMoveTypeRule>? moveTypeRules = null)
    {
        ArgumentNullException.ThrowIfNull(creatureTypes);
        ArgumentNullException.ThrowIfNull(moves);
        HeldItem = heldItem;
        Ability = ability;
        CreatureTypes = Array.AsReadOnly(creatureTypes.ToArray());
        Stats = stats;
        Moves = Array.AsReadOnly(moves.ToArray());
        FormId = formId;
        Decoy = decoy;
        var capturedMetrics = new SortedDictionary<BattleMetric, int>();
        foreach ((BattleMetric metric, int value) in metrics ?? new Dictionary<BattleMetric, int>())
            capturedMetrics.Add(metric, value);
        Metrics = new ReadOnlyDictionary<BattleMetric, int>(capturedMetrics);
        MoveTypeRules = Array.AsReadOnly((moveTypeRules ?? []).ToArray());
    }

    public EntityId? HeldItem { get; internal init; }
    public EntityId? Ability { get; internal init; }
    public IReadOnlyList<EntityId> CreatureTypes { get; internal init; }
    public Stats Stats { get; internal init; }
    public IReadOnlyList<BattleEffectiveMove> Moves { get; internal init; }
    public string? FormId { get; internal init; }
    public BattleDecoyState? Decoy { get; internal init; }
    public IReadOnlyDictionary<BattleMetric, int> Metrics { get; internal init; }
    public IReadOnlyList<BattleMoveTypeRule> MoveTypeRules { get; internal init; }
}

public sealed record BattleOverlayOwner(BattleSide Side, int PartyIndex, BattleSlot? Slot = null);
public sealed record BattleOverlaySource(BattleSlot? Slot = null, int? PartyIndex = null, EntityId? Entity = null);

public abstract record BattleOverlayPayload
{
    internal BattleOverlayPayload() { }
    public abstract BattleEffectiveValueKind Kind { get; }
    public abstract string ResolutionKey { get; }
}

public sealed record HeldItemOverlay(EntityId? Item) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.HeldItem;
    public override string ResolutionKey => "held_item";
}

public sealed record AbilityOverlay(EntityId? Ability) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.Ability;
    public override string ResolutionKey => "ability";
}

public sealed record CreatureTypesOverlay(IReadOnlyList<EntityId> Types) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.CreatureTypes;
    public override string ResolutionKey => "creature_types";
}

public sealed record StatsOverlay(Stats Stats) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.Stats;
    public override string ResolutionKey => "stats";
}

public sealed record MoveListOverlay(IReadOnlyList<BattleEffectiveMove> Moves) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.MoveList;
    public override string ResolutionKey => "move_list";
}

public sealed record MoveSlotOverlay(int MoveSlot, BattleEffectiveMove Move) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.MoveList;
    public override string ResolutionKey => $"move_list_slot_{MoveSlot}";
}

public sealed record MoveTypeOverlay(int MoveSlot, EntityId Type) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.MoveType;
    public override string ResolutionKey => $"move_type_{MoveSlot}";
}

public sealed record MoveClassOverlay(int MoveSlot, DamageClass DamageClass) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.MoveClass;
    public override string ResolutionKey => $"move_class_{MoveSlot}";
}

public sealed record FormOverlay(string? FormId) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.Form;
    public override string ResolutionKey => "form";
}

public sealed record DecoyOverlay(BattleDecoyState? Decoy) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.Decoy;
    public override string ResolutionKey => "decoy";
}

public sealed record MetricOverlay(BattleMetric Metric, int Value) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.Metric;
    public override string ResolutionKey => $"metric_{Metric.ToString().ToLowerInvariant()}";
}

public sealed record TransformOverlay(
    EntityId? Ability,
    IReadOnlyList<EntityId> Types,
    Stats Stats,
    IReadOnlyList<BattleEffectiveMove> Moves,
    string? FormId,
    int Weight) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.Transform;
    public override string ResolutionKey => "transform";
}

public sealed record TypeAdditionOverlay(string ContributionKey, IReadOnlyList<EntityId> Types) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.CreatureTypes;
    public override string ResolutionKey => $"creature_types_add_{ContributionKey}";
}

public sealed record MoveTypeRuleOverlay(string ContributionKey, BattleMoveTypeRule Rule) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.MoveType;
    public override string ResolutionKey => $"move_type_rule_{ContributionKey}";
}

public sealed record StatDeltaOverlay(string ContributionKey, Stats Delta) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.Stats;
    public override string ResolutionKey => $"stats_add_{ContributionKey}";
}

public sealed record DerivedStatOverlay(StatKind Stat, int Value) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.Stats;
    public override string ResolutionKey => $"stats_value_{Stat.ToString().ToLowerInvariant()}";
}

public sealed record MetricDeltaOverlay(string ContributionKey, BattleMetric Metric, int Delta) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.Metric;
    public override string ResolutionKey => $"metric_{Metric.ToString().ToLowerInvariant()}_add_{ContributionKey}";
}

public sealed record MetricValueOverlay(BattleMetric Metric, int Value) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => BattleEffectiveValueKind.Metric;
    public override string ResolutionKey => $"metric_value_{Metric.ToString().ToLowerInvariant()}";
}

public sealed record SuppressionOverlay(BattleEffectiveValueKind SuppressedKind) : BattleOverlayPayload
{
    public override BattleEffectiveValueKind Kind => SuppressedKind;
    public override string ResolutionKey => $"suppress_{SuppressedKind.ToString().ToLowerInvariant()}";
}

public sealed record BattleOverlayApplication(
    BattleOverlayOwner Owner,
    BattleOverlaySource Source,
    BattleOverlayLayer Layer,
    BattleOverlayPayload Payload,
    int Turn,
    int ActionSequence,
    int? Duration = null,
    BattleIntentCheckpoint? DurationCheckpoint = null,
    BattleOverlayCleanup Cleanup = BattleOverlayCleanup.BattleEnd);

public sealed record BattleOverlayInstance(
    long Sequence,
    BattleOverlayOwner Owner,
    BattleOverlaySource Source,
    BattleOverlayLayer Layer,
    BattleOverlayPayload Payload,
    int AppliedTurn,
    int AppliedActionSequence,
    int? RemainingDuration,
    BattleIntentCheckpoint? DurationCheckpoint,
    BattleOverlayCleanup Cleanup);

public sealed record BattleOverlayTraceEntry(
    int Turn,
    int ActionSequence,
    BattleOverlayTraceKind Kind,
    long Sequence,
    BattleOverlayLayer Layer,
    BattleEffectiveValueKind ValueKind,
    string ResolutionKey,
    BattleOverlaySource Source,
    BattleOverlayOwner? OwnerBefore,
    BattleOverlayOwner? OwnerAfter,
    int? DurationBefore,
    int? DurationAfter,
    BattleOverlayRemovalReason? RemovalReason = null);

public sealed record BattleOverlayChangeSet(
    IReadOnlyList<BattleOverlayInstance> Affected,
    IReadOnlyList<BattleOverlayTraceEntry> Trace);

public sealed record BattleEffectiveResult(
    BattleEffectiveValues Values,
    IReadOnlyList<BattleOverlayTraceEntry> Trace);

public sealed class BattleOverlayStore
{
    private readonly List<BattleOverlayInstance> _entries = [];
    private long _nextSequence;

    public BattleOverlayChangeSet Apply(BattleOverlayApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return ApplyMany([application]);
    }

    public BattleOverlayChangeSet ApplyMany(IReadOnlyList<BattleOverlayApplication> applications)
    {
        ArgumentNullException.ThrowIfNull(applications);
        if (applications.Count == 0)
            return new BattleOverlayChangeSet([], []);
        BattleOverlayPayload[] payloads = applications.Select(ValidateApplication).ToArray();
        if (applications.Count > long.MaxValue - _nextSequence)
            throw new OverflowException("Battle overlay sequence is exhausted.");

        var instances = new BattleOverlayInstance[applications.Count];
        var trace = new BattleOverlayTraceEntry[applications.Count];
        for (int i = 0; i < applications.Count; i++)
        {
            BattleOverlayApplication application = applications[i];
            var instance = new BattleOverlayInstance(_nextSequence++, application.Owner, application.Source,
                application.Layer, payloads[i], application.Turn, application.ActionSequence, application.Duration,
                application.DurationCheckpoint, application.Cleanup);
            _entries.Add(instance);
            instances[i] = instance;
            trace[i] = Trace(application.Turn, application.ActionSequence, BattleOverlayTraceKind.Applied,
                instance, null, instance.Owner, null, instance.RemainingDuration);
        }
        return new BattleOverlayChangeSet(instances, trace);
    }

    public BattleEffectiveResult Resolve(BattleOverlayOwner owner, BattleEffectiveValues baseValues,
        IEnumerable<long>? ignoredSuppressions = null, int turn = 0, int actionSequence = 0)
    {
        ValidateOwner(owner);
        ValidateTime(turn, actionSequence);
        BattleEffectiveValues values = NormalizeValues(baseValues);
        HashSet<long> ignored = (ignoredSuppressions ?? []).ToHashSet();
        if (ignored.Any(sequence => sequence < 0))
            throw new ArgumentOutOfRangeException(nameof(ignoredSuppressions), "Ignored suppression sequences cannot be negative.");

        BattleOverlayInstance[] candidates = _entries.Where(entry => SameCreature(entry.Owner, owner)).ToArray();
        if (ignored.Any(sequence => !candidates.Any(entry => entry.Sequence == sequence
                && entry.Payload is SuppressionOverlay)))
            throw new ArgumentException("Ignored sequences must identify suppression overlays owned by this creature.",
                nameof(ignoredSuppressions));
        HashSet<long> winners = candidates
            .GroupBy(entry => (entry.Layer, entry.Payload.ResolutionKey))
            .Select(group => group.MaxBy(entry => entry.Sequence)!.Sequence)
            .ToHashSet();
        var trace = candidates.Where(entry => !winners.Contains(entry.Sequence))
            .OrderBy(entry => entry.Layer).ThenBy(entry => entry.Sequence)
            .Select(entry => Trace(turn, actionSequence, BattleOverlayTraceKind.Superseded, entry,
                entry.Owner, entry.Owner, entry.RemainingDuration, entry.RemainingDuration))
            .ToList();

        foreach (BattleOverlayInstance entry in candidates.Where(entry => winners.Contains(entry.Sequence))
            .OrderBy(entry => entry.Layer)
            .ThenBy(entry => entry.Sequence)
            .ThenBy(entry => entry.Payload.ResolutionKey, StringComparer.Ordinal))
        {
            if (entry.Payload is SuppressionOverlay suppression)
            {
                bool bypassed = ignored.Contains(entry.Sequence);
                if (!bypassed)
                    values = Suppress(values, suppression.SuppressedKind);
                trace.Add(Trace(turn, actionSequence,
                    bypassed ? BattleOverlayTraceKind.SuppressionIgnored : BattleOverlayTraceKind.Suppressed,
                    entry, entry.Owner, entry.Owner, entry.RemainingDuration, entry.RemainingDuration));
                continue;
            }

            values = Apply(values, entry.Payload);
            trace.Add(Trace(turn, actionSequence, BattleOverlayTraceKind.Resolved, entry,
                entry.Owner, entry.Owner, entry.RemainingDuration, entry.RemainingDuration));
        }

        return new BattleEffectiveResult(values, trace.ToArray());
    }

    public BattleOverlayChangeSet CompleteCheckpoint(BattleIntentCheckpoint checkpoint, int turn, int actionSequence)
    {
        if (!Enum.IsDefined(checkpoint))
            throw new ArgumentOutOfRangeException(nameof(checkpoint), checkpoint, "Unknown overlay checkpoint.");
        ValidateTime(turn, actionSequence);
        var affected = new List<BattleOverlayInstance>();
        var trace = new List<BattleOverlayTraceEntry>();
        foreach (BattleOverlayInstance before in Snapshot()
            .Where(entry => entry.RemainingDuration is not null && entry.DurationCheckpoint == checkpoint))
        {
            int index = _entries.FindIndex(entry => entry.Sequence == before.Sequence);
            if (index < 0)
                continue;
            BattleOverlayInstance after = before with { RemainingDuration = before.RemainingDuration - 1 };
            affected.Add(after);
            trace.Add(Trace(turn, actionSequence, BattleOverlayTraceKind.Ticked, after,
                before.Owner, after.Owner, before.RemainingDuration, after.RemainingDuration));
            if (after.RemainingDuration == 0)
            {
                _entries.RemoveAt(index);
                trace.Add(Trace(turn, actionSequence, BattleOverlayTraceKind.Expired, after,
                    after.Owner, null, after.RemainingDuration, null, BattleOverlayRemovalReason.Expired));
            }
            else
            {
                _entries[index] = after;
            }
        }
        return new BattleOverlayChangeSet(affected, trace);
    }

    public BattleOverlayChangeSet OwnerSwitched(BattleSide side, int partyIndex, BattleSlot? destination,
        int turn, int actionSequence)
    {
        ValidateIdentity(side, partyIndex, destination);
        ValidateTime(turn, actionSequence);
        var affected = new List<BattleOverlayInstance>();
        var trace = new List<BattleOverlayTraceEntry>();
        foreach (BattleOverlayInstance before in Snapshot()
            .Where(entry => entry.Owner.Side == side && entry.Owner.PartyIndex == partyIndex))
        {
            int index = _entries.FindIndex(entry => entry.Sequence == before.Sequence);
            if ((before.Cleanup & BattleOverlayCleanup.Switch) != 0)
            {
                _entries.RemoveAt(index);
                affected.Add(before);
                trace.Add(Trace(turn, actionSequence, BattleOverlayTraceKind.Removed, before,
                    before.Owner, null, before.RemainingDuration, null, BattleOverlayRemovalReason.Switch));
            }
            else
            {
                BattleOverlayInstance after = before with { Owner = before.Owner with { Slot = destination } };
                _entries[index] = after;
                affected.Add(after);
                trace.Add(Trace(turn, actionSequence, BattleOverlayTraceKind.Transferred, after,
                    before.Owner, after.Owner, before.RemainingDuration, after.RemainingDuration));
            }
        }
        return new BattleOverlayChangeSet(affected, trace);
    }

    public BattleOverlayChangeSet OwnerFainted(BattleSide side, int partyIndex, int turn, int actionSequence) =>
        RemoveWhere(entry => entry.Owner.Side == side && entry.Owner.PartyIndex == partyIndex
            && (entry.Cleanup & BattleOverlayCleanup.Faint) != 0, turn, actionSequence,
            BattleOverlayRemovalReason.Faint);

    public BattleOverlayChangeSet EndBattle(int turn, int actionSequence) =>
        RemoveWhere(_ => true, turn, actionSequence, BattleOverlayRemovalReason.BattleEnd);

    public BattleOverlayChangeSet RemoveTypeAdditions(BattleOverlayOwner owner, int turn, int actionSequence)
    {
        ValidateOwner(owner);
        return RemoveWhere(entry => SameCreature(entry.Owner, owner)
            && entry.Layer == BattleOverlayLayer.Additive
            && entry.Payload is TypeAdditionOverlay,
            turn, actionSequence, BattleOverlayRemovalReason.Expired);
    }

    public BattleOverlayChangeSet Remove(IReadOnlyCollection<long> sequences, int turn, int actionSequence)
    {
        ArgumentNullException.ThrowIfNull(sequences);
        if (sequences.Any(sequence => sequence < 0))
            throw new ArgumentOutOfRangeException(nameof(sequences), "Overlay sequences cannot be negative.");
        var selected = sequences.ToHashSet();
        return RemoveWhere(entry => selected.Contains(entry.Sequence), turn, actionSequence,
            BattleOverlayRemovalReason.Expired);
    }

    public IReadOnlyList<BattleOverlayInstance> Snapshot() => _entries
        .OrderBy(entry => entry.Owner.Side)
        .ThenBy(entry => entry.Owner.Slot?.Position ?? int.MaxValue)
        .ThenBy(entry => entry.Owner.PartyIndex)
        .ThenBy(entry => entry.Layer)
        .ThenBy(entry => entry.Sequence)
        .ToArray();

    public bool HasTransform(BattleOverlayOwner owner)
    {
        ValidateOwner(owner);
        return _entries.Any(entry => SameCreature(entry.Owner, owner) && entry.Payload is TransformOverlay);
    }

    private BattleOverlayChangeSet RemoveWhere(Func<BattleOverlayInstance, bool> predicate,
        int turn, int actionSequence, BattleOverlayRemovalReason reason)
    {
        ValidateTime(turn, actionSequence);
        BattleOverlayInstance[] removed = Snapshot().Where(predicate).ToArray();
        var sequences = removed.Select(entry => entry.Sequence).ToHashSet();
        _entries.RemoveAll(entry => sequences.Contains(entry.Sequence));
        return new BattleOverlayChangeSet(removed, removed.Select(entry => Trace(turn, actionSequence,
            BattleOverlayTraceKind.Removed, entry, entry.Owner, null, entry.RemainingDuration, null, reason)).ToArray());
    }

    private static BattleEffectiveValues Apply(BattleEffectiveValues values, BattleOverlayPayload payload) => payload switch
    {
        HeldItemOverlay item => values with { HeldItem = item.Item },
        AbilityOverlay ability => values with { Ability = ability.Ability },
        CreatureTypesOverlay types => values with { CreatureTypes = types.Types },
        StatsOverlay stats => values with { Stats = stats.Stats },
        MoveListOverlay moves => values with { Moves = moves.Moves },
        MoveSlotOverlay move => values with { Moves = ReplaceMove(values.Moves, move.MoveSlot, _ => move.Move) },
        MoveTypeOverlay type => values with { Moves = ReplaceMove(values.Moves, type.MoveSlot,
            move => move with { Type = type.Type }) },
        MoveClassOverlay moveClass => values with { Moves = ReplaceMove(values.Moves, moveClass.MoveSlot,
            move => move with { DamageClass = moveClass.DamageClass }) },
        FormOverlay form => values with { FormId = form.FormId },
        DecoyOverlay decoy => values with { Decoy = decoy.Decoy },
        MetricOverlay metric => values with { Metrics = SetMetric(values.Metrics!, metric.Metric, metric.Value) },
        TransformOverlay transform => values with
        {
            Ability = transform.Ability,
            CreatureTypes = transform.Types,
            Stats = transform.Stats,
            Moves = transform.Moves,
            FormId = transform.FormId,
            Metrics = SetMetric(values.Metrics!, BattleMetric.Weight, transform.Weight),
        },
        TypeAdditionOverlay types => values with { CreatureTypes = AddTypes(values.CreatureTypes, types.Types) },
        MoveTypeRuleOverlay rule => values with { MoveTypeRules = values.MoveTypeRules.Append(rule.Rule).ToArray() },
        StatDeltaOverlay stats => values with { Stats = AddStats(values.Stats, stats.Delta) },
        DerivedStatOverlay stat => values with { Stats = SetStat(values.Stats, stat.Stat, stat.Value) },
        MetricDeltaOverlay metric => values with
        {
            Metrics = SetMetric(values.Metrics!, metric.Metric,
                Math.Max(1, checked(values.Metrics!.GetValueOrDefault(metric.Metric, 1) + metric.Delta))),
        },
        MetricValueOverlay metric => values with
        {
            Metrics = SetMetric(values.Metrics!, metric.Metric, metric.Value),
        },
        _ => throw new ArgumentOutOfRangeException(nameof(payload), payload.GetType().Name, "Unknown overlay payload."),
    };

    private static BattleEffectiveValues Suppress(BattleEffectiveValues values, BattleEffectiveValueKind kind) => kind switch
    {
        BattleEffectiveValueKind.HeldItem => values with { HeldItem = null },
        BattleEffectiveValueKind.Ability => values with { Ability = null },
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only held item and ability can be suppressed."),
    };

    private static IReadOnlyList<BattleEffectiveMove> ReplaceMove(IReadOnlyList<BattleEffectiveMove> moves,
        int slot, Func<BattleEffectiveMove, BattleEffectiveMove> replace)
    {
        if (slot < 0 || slot >= moves.Count)
            throw new ArgumentOutOfRangeException(nameof(slot), "Overlay move slot is outside the effective move list.");
        BattleEffectiveMove[] result = moves.ToArray();
        result[slot] = replace(result[slot]);
        return result;
    }

    private static IReadOnlyList<EntityId> AddTypes(IReadOnlyList<EntityId> current, IReadOnlyList<EntityId> additions)
    {
        var result = current.ToList();
        foreach (EntityId type in additions)
            if (!result.Contains(type))
                result.Add(type);
        return result.ToArray();
    }

    private static Stats AddStats(Stats value, Stats delta) => new(
        Math.Max(1, checked(value.Hp + delta.Hp)), Math.Max(1, checked(value.Atk + delta.Atk)),
        Math.Max(1, checked(value.Def + delta.Def)), Math.Max(1, checked(value.Spa + delta.Spa)),
        Math.Max(1, checked(value.Spd + delta.Spd)), Math.Max(1, checked(value.Spe + delta.Spe)));

    private static Stats SetStat(Stats value, StatKind stat, int replacement) => stat switch
    {
        StatKind.Atk => value with { Atk = replacement },
        StatKind.Def => value with { Def = replacement },
        StatKind.Spa => value with { Spa = replacement },
        StatKind.Spd => value with { Spd = replacement },
        StatKind.Spe => value with { Spe = replacement },
        _ => throw new ArgumentOutOfRangeException(nameof(stat), stat, "Only derived stats can be overlaid."),
    };

    private static IReadOnlyDictionary<BattleMetric, int> SetMetric(
        IReadOnlyDictionary<BattleMetric, int> metrics, BattleMetric metric, int value)
    {
        var result = new SortedDictionary<BattleMetric, int>();
        foreach ((BattleMetric key, int existing) in metrics)
            result.Add(key, existing);
        result[metric] = value;
        return new ReadOnlyDictionary<BattleMetric, int>(result);
    }

    private static BattleEffectiveValues NormalizeValues(BattleEffectiveValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        ValidateEntity(values.HeldItem, EntityCategory.Item, "held item");
        ValidateEntity(values.Ability, EntityCategory.Ability, "ability");
        EntityId[] types = NormalizeTypes(values.CreatureTypes, "Base creature types");
        ValidatePositiveStats(values.Stats, "Base effective stats");
        BattleEffectiveMove[] moves = NormalizeMoves(values.Moves);
        ValidateToken(values.FormId, "Base form ID");
        ValidateDecoy(values.Decoy);
        var metrics = new SortedDictionary<BattleMetric, int>();
        foreach ((BattleMetric metric, int value) in values.Metrics)
        {
            if (!Enum.IsDefined(metric) || value <= 0)
                throw new ArgumentException("Base metrics require defined keys and positive values.", nameof(values));
            metrics.Add(metric, value);
        }
        BattleMoveTypeRule[] moveTypeRules = values.MoveTypeRules.ToArray();
        if (moveTypeRules.Any(rule => !ValidEntity(rule.Type, EntityCategory.Type)
                || rule.MatchType is { } match && !ValidEntity(match, EntityCategory.Type)))
            throw new ArgumentException("Base move-type rules require valid type IDs.", nameof(values));
        return values with
        {
            CreatureTypes = Array.AsReadOnly(types),
            Moves = Array.AsReadOnly(moves),
            Metrics = new ReadOnlyDictionary<BattleMetric, int>(metrics),
            MoveTypeRules = Array.AsReadOnly(moveTypeRules),
        };
    }

    private static BattleOverlayPayload NormalizePayload(BattleOverlayPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return payload switch
        {
            HeldItemOverlay item => ValidEntity(item.Item, EntityCategory.Item, "held item", item),
            AbilityOverlay ability => ValidEntity(ability.Ability, EntityCategory.Ability, "ability", ability),
            CreatureTypesOverlay types => types with { Types = Array.AsReadOnly(NormalizeTypes(types.Types, "Overlay types")) },
            StatsOverlay stats when Positive(stats.Stats) => stats,
            StatsOverlay => throw new ArgumentException("Replacement stats must all be positive.", nameof(payload)),
            MoveListOverlay moves => moves with { Moves = Array.AsReadOnly(NormalizeMoves(moves.Moves)) },
            MoveSlotOverlay move when move.MoveSlot >= 0 => move with
            {
                Move = NormalizeMoves([move.Move])[0],
            },
            MoveSlotOverlay => throw new ArgumentException("Move-slot overlays require a valid slot.", nameof(payload)),
            MoveTypeOverlay type when type.MoveSlot >= 0 && ValidEntity(type.Type, EntityCategory.Type) => type,
            MoveTypeOverlay => throw new ArgumentException("Move-type overlays require a valid slot and type ID.", nameof(payload)),
            MoveClassOverlay moveClass when moveClass.MoveSlot >= 0 && Enum.IsDefined(moveClass.DamageClass) => moveClass,
            MoveClassOverlay => throw new ArgumentException("Move-class overlays require a valid slot and class.", nameof(payload)),
            FormOverlay form => ValidToken(form.FormId, "Overlay form ID", form),
            DecoyOverlay decoy => ValidDecoy(decoy.Decoy, decoy),
            MetricOverlay metric when Enum.IsDefined(metric.Metric) && metric.Value > 0 => metric,
            MetricOverlay => throw new ArgumentException("Replacement metrics must be defined and positive.", nameof(payload)),
            TransformOverlay transform => NormalizeTransform(transform),
            TypeAdditionOverlay addition when BattleConditionId.ValidToken(addition.ContributionKey)
                => addition with { Types = Array.AsReadOnly(NormalizeTypes(addition.Types, "Type additions")) },
            TypeAdditionOverlay => throw new ArgumentException("Type additions require a lowercase contribution key.", nameof(payload)),
            MoveTypeRuleOverlay rule when BattleConditionId.ValidToken(rule.ContributionKey)
                && ValidEntity(rule.Rule.Type, EntityCategory.Type)
                && (rule.Rule.MatchType is null || ValidEntity(rule.Rule.MatchType.Value, EntityCategory.Type)) => rule,
            MoveTypeRuleOverlay => throw new ArgumentException("Move-type rules require a lowercase key and valid type IDs.", nameof(payload)),
            StatDeltaOverlay delta when BattleConditionId.ValidToken(delta.ContributionKey) => delta,
            StatDeltaOverlay => throw new ArgumentException("Stat deltas require a lowercase contribution key.", nameof(payload)),
            DerivedStatOverlay stat when stat.Stat is (StatKind.Atk or StatKind.Def or StatKind.Spa
                or StatKind.Spd or StatKind.Spe) && stat.Value > 0 => stat,
            DerivedStatOverlay => throw new ArgumentException("Derived-stat values require a non-HP battle stat and positive value.", nameof(payload)),
            MetricDeltaOverlay delta when BattleConditionId.ValidToken(delta.ContributionKey) && Enum.IsDefined(delta.Metric) => delta,
            MetricDeltaOverlay => throw new ArgumentException("Metric deltas require a lowercase key and defined metric.", nameof(payload)),
            MetricValueOverlay metric when Enum.IsDefined(metric.Metric) && metric.Value > 0 => metric,
            MetricValueOverlay => throw new ArgumentException("Metric values require a defined metric and positive value.", nameof(payload)),
            SuppressionOverlay suppression when suppression.SuppressedKind is BattleEffectiveValueKind.HeldItem
                or BattleEffectiveValueKind.Ability => suppression,
            SuppressionOverlay => throw new ArgumentException("Only held-item and ability suppression is supported.", nameof(payload)),
            _ => throw new ArgumentException("Unknown overlay payload type.", nameof(payload)),
        };
    }

    private static BattleOverlayPayload ValidateApplication(BattleOverlayApplication application)
    {
        ValidateOwner(application.Owner);
        ValidateSource(application.Source);
        ValidateTime(application.Turn, application.ActionSequence);
        if (!Enum.IsDefined(application.Layer))
            throw new ArgumentException("Overlay layer must be defined.", nameof(application));
        BattleOverlayPayload payload = NormalizePayload(application.Payload);
        bool layerMatches = application.Layer switch
        {
            BattleOverlayLayer.PermanentInstance or BattleOverlayLayer.FormOrSnapshot
                => payload is HeldItemOverlay or AbilityOverlay or CreatureTypesOverlay or StatsOverlay
                    or MoveListOverlay or MoveSlotOverlay or MoveTypeOverlay or MoveClassOverlay or FormOverlay or DecoyOverlay or MetricOverlay
                    or TransformOverlay,
            BattleOverlayLayer.Additive => payload is TypeAdditionOverlay or MoveTypeRuleOverlay
                or StatDeltaOverlay or DerivedStatOverlay or MetricDeltaOverlay or MetricValueOverlay,
            BattleOverlayLayer.Suppression => payload is SuppressionOverlay,
            _ => false,
        };
        if (!layerMatches)
            throw new ArgumentException("Overlay payload does not match its precedence layer.", nameof(application));
        BattleOverlayCleanup defined = BattleOverlayCleanup.Switch | BattleOverlayCleanup.Faint | BattleOverlayCleanup.BattleEnd;
        if ((application.Cleanup & ~defined) != 0 || (application.Cleanup & BattleOverlayCleanup.BattleEnd) == 0)
            throw new ArgumentException("Battle overlays require defined cleanup flags including battle end.", nameof(application));
        if (application.Duration is <= 0
            || (application.Duration is null) != (application.DurationCheckpoint is null)
            || (application.DurationCheckpoint is { } checkpoint && !Enum.IsDefined(checkpoint)))
            throw new ArgumentException("Overlay duration and checkpoint must form one valid optional pair.", nameof(application));
        return payload;
    }

    private static void ValidateOwner(BattleOverlayOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!Enum.IsDefined(owner.Side) || owner.PartyIndex < 0
            || (owner.Slot is { } slot && (!Enum.IsDefined(slot.Side) || slot.Position < 0 || slot.Side != owner.Side)))
            throw new ArgumentException("Overlay owner requires a valid side, party index, and matching optional slot.", nameof(owner));
    }

    private static void ValidateSource(BattleOverlaySource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if ((source.Slot is null) != (source.PartyIndex is null)
            || source.PartyIndex is < 0
            || (source.Slot is { } slot && (!Enum.IsDefined(slot.Side) || slot.Position < 0))
            || (source.Entity is { } entity && entity == default))
            throw new ArgumentException("Overlay source requires a complete valid slot/party identity and optional entity ID.", nameof(source));
    }

    private static void ValidateIdentity(BattleSide side, int partyIndex, BattleSlot? destination)
    {
        if (!Enum.IsDefined(side) || partyIndex < 0
            || (destination is { } slot && (!Enum.IsDefined(slot.Side) || slot.Position < 0 || slot.Side != side)))
            throw new ArgumentException("Overlay cleanup identity is invalid.");
    }

    private static void ValidateTime(int turn, int actionSequence)
    {
        if (turn < 0 || actionSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(turn), "Overlay turn and action sequence cannot be negative.");
    }

    private static BattleEffectiveMove[] NormalizeMoves(IReadOnlyList<BattleEffectiveMove> moves)
    {
        if (moves is null)
            throw new ArgumentException("Effective move list cannot be null.", nameof(moves));
        BattleEffectiveMove[] result = moves.ToArray();
        foreach (BattleEffectiveMove move in result)
        {
            ArgumentNullException.ThrowIfNull(move);
            ArgumentNullException.ThrowIfNull(move.Definition);
            if (move.Definition.Move.Category != EntityCategory.Move || move.PpOwnerSlot < 0
                || !ValidEntity(move.Type, EntityCategory.Type) || !Enum.IsDefined(move.DamageClass))
                throw new ArgumentException("Effective moves require move/type IDs, a defined class, and nonnegative PP owner.", nameof(moves));
        }
        return result;
    }

    private static TransformOverlay NormalizeTransform(TransformOverlay transform)
    {
        ValidateEntity(transform.Ability, EntityCategory.Ability, "Transform ability");
        EntityId[] types = NormalizeTypes(transform.Types, "Transform types");
        ValidatePositiveStats(transform.Stats, "Transform stats");
        BattleEffectiveMove[] moves = NormalizeMoves(transform.Moves)
            .Select(move => move with
            {
                Definition = move.Definition.WithPpPool(move.Definition.Pp, move.Definition.MaxPp),
            }).ToArray();
        if (moves.Length == 0 || transform.Weight <= 0)
            throw new ArgumentException("Transform snapshots require moves and positive weight.", nameof(transform));
        ValidateToken(transform.FormId, "Transform form ID");
        return transform with
        {
            Types = Array.AsReadOnly(types),
            Moves = Array.AsReadOnly(moves),
        };
    }

    private static EntityId[] NormalizeTypes(IReadOnlyList<EntityId> types, string label)
    {
        if (types is null || types.Count == 0 || types.Any(type => !ValidEntity(type, EntityCategory.Type))
            || types.Distinct().Count() != types.Count)
            throw new ArgumentException($"{label} must be a nonempty unique type-ID list.", nameof(types));
        return types.ToArray();
    }

    private static bool Positive(Stats stats) => stats.Hp > 0 && stats.Atk > 0 && stats.Def > 0
        && stats.Spa > 0 && stats.Spd > 0 && stats.Spe > 0;

    private static void ValidatePositiveStats(Stats stats, string label)
    {
        if (!Positive(stats))
            throw new ArgumentException($"{label} must all be positive.", nameof(stats));
    }

    private static bool ValidEntity(EntityId id, EntityCategory category) => id != default && id.Category == category;

    private static void ValidateEntity(EntityId? id, EntityCategory category, string label)
    {
        if (id is { } value && !ValidEntity(value, category))
            throw new ArgumentException($"Effective {label} must use the {category} category.", nameof(id));
    }

    private static T ValidEntity<T>(EntityId? id, EntityCategory category, string label, T value)
    {
        ValidateEntity(id, category, label);
        return value;
    }

    private static void ValidateToken(string? value, string label)
    {
        if (value is not null && !BattleConditionId.ValidToken(value))
            throw new ArgumentException($"{label} must be a lowercase token.", nameof(value));
    }

    private static T ValidToken<T>(string? token, string label, T value)
    {
        ValidateToken(token, label);
        return value;
    }

    private static void ValidateDecoy(BattleDecoyState? decoy)
    {
        if (decoy is { } value && (value.Hp <= 0 || value.MaxHp <= 0 || value.Hp > value.MaxHp))
            throw new ArgumentException("Decoy HP must be positive and no greater than max HP.", nameof(decoy));
    }

    private static T ValidDecoy<T>(BattleDecoyState? decoy, T value)
    {
        ValidateDecoy(decoy);
        return value;
    }

    private static bool SameCreature(BattleOverlayOwner left, BattleOverlayOwner right) =>
        left.Side == right.Side && left.PartyIndex == right.PartyIndex;

    private static BattleOverlayTraceEntry Trace(int turn, int actionSequence, BattleOverlayTraceKind kind,
        BattleOverlayInstance instance, BattleOverlayOwner? ownerBefore, BattleOverlayOwner? ownerAfter,
        int? durationBefore, int? durationAfter, BattleOverlayRemovalReason? removalReason = null) => new(turn, actionSequence, kind, instance.Sequence,
        instance.Layer, instance.Payload.Kind, instance.Payload.ResolutionKey,
        instance.Source, ownerBefore, ownerAfter, durationBefore, durationAfter, removalReason);
}
