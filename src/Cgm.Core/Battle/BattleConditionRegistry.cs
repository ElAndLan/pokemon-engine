using System.Collections.ObjectModel;

namespace Cgm.Core.Battle;

public sealed class BattleConditionRegistry
{
    private readonly IReadOnlyDictionary<BattleConditionId, BattleConditionDefinition> _definitions;

    public BattleConditionRegistry(IEnumerable<BattleConditionDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        BattleConditionDefinition[] normalized = definitions.Select(Normalize).ToArray();
        if (normalized.Select(definition => definition.Id).Distinct().Count() != normalized.Length)
            throw new ArgumentException("Condition definition IDs must be unique.", nameof(definitions));

        foreach (IGrouping<(BattleConditionScope Scope, string Key), BattleConditionDefinition> group in normalized
            .GroupBy(definition => (definition.Scope, definition.StackingKey)))
        {
            if (group.Select(definition => definition.StackingPolicy).Distinct().Count() != 1)
                throw new ArgumentException("Definitions sharing a scope and stacking key must share one policy.", nameof(definitions));
            if (group.First().StackingPolicy != BattleConditionStackingPolicy.Replace && group.Count() > 1)
                throw new ArgumentException("Only replacement families may share a scope and stacking key.", nameof(definitions));
        }

        _definitions = new ReadOnlyDictionary<BattleConditionId, BattleConditionDefinition>(
            normalized.ToDictionary(definition => definition.Id));
    }

    public BattleConditionDefinition For(BattleConditionId id) =>
        _definitions.TryGetValue(id, out BattleConditionDefinition? definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown battle condition '{id}'.");

    internal static BattleConditionDefinition Normalize(BattleConditionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrEmpty(definition.Id.Value))
            throw new ArgumentException("Condition definition ID cannot be default.", nameof(definition));
        if (!Enum.IsDefined(definition.Scope)
            || !Enum.IsDefined(definition.StackingPolicy)
            || !Enum.IsDefined(definition.SwitchPolicy)
            || !Enum.IsDefined(definition.FaintPolicy))
            throw new ArgumentException("Condition definition enums must be defined.", nameof(definition));
        if (!BattleConditionId.ValidToken(definition.StackingKey))
            throw new ArgumentException("Condition stacking keys must be lowercase tokens.", nameof(definition));
        if (definition.MaximumStacks <= 0
            || (definition.StackingPolicy == BattleConditionStackingPolicy.Stack) != (definition.MaximumStacks > 1))
            throw new ArgumentException("Only stack definitions use a maximum above one.", nameof(definition));
        if (definition.DefaultDuration is <= 0)
            throw new ArgumentException("Condition duration must be positive.", nameof(definition));
        if (definition.DefaultDuration is not null && definition.DurationCheckpoint is null)
            throw new ArgumentException("Timed conditions require a duration checkpoint.", nameof(definition));
        if (definition.DurationCheckpoint is { } checkpoint && !Enum.IsDefined(checkpoint))
            throw new ArgumentException("Condition duration checkpoint must be defined.", nameof(definition));
        if (definition.StackingPolicy == BattleConditionStackingPolicy.Refresh && definition.DurationCheckpoint is null)
            throw new ArgumentException("Refresh conditions require a duration checkpoint.", nameof(definition));

        BattleConditionHook[] hooks = definition.Hooks?.ToArray()
            ?? throw new ArgumentException("Condition hook list cannot be null.", nameof(definition));
        if (hooks.Any(hook => !Enum.IsDefined(hook)) || hooks.Distinct().Count() != hooks.Length)
            throw new ArgumentException("Condition hooks must be defined and unique.", nameof(definition));
        hooks = hooks.Order().ToArray();

        string[] tags = definition.Tags?.ToArray()
            ?? throw new ArgumentException("Condition tag list cannot be null.", nameof(definition));
        if (tags.Any(tag => !BattleConditionId.ValidToken(tag)) || tags.Distinct(StringComparer.Ordinal).Count() != tags.Length)
            throw new ArgumentException("Condition tags must be unique lowercase tokens.", nameof(definition));
        Array.Sort(tags, StringComparer.Ordinal);

        IReadOnlyDictionary<string, int> counters = definition.InitialCounters
            ?? throw new ArgumentException("Condition counters cannot be null.", nameof(definition));
        if (counters.Any(counter => !BattleConditionId.ValidToken(counter.Key) || counter.Value < 0))
            throw new ArgumentException("Condition counters require lowercase keys and nonnegative values.", nameof(definition));
        var orderedCounters = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach ((string key, int value) in counters)
            orderedCounters.Add(key, value);

        bool cleanupValid = definition.Scope == BattleConditionScope.Creature
            ? definition.SwitchPolicy is BattleConditionSwitchPolicy.Remove or BattleConditionSwitchPolicy.FollowOwner
            : definition.SwitchPolicy == BattleConditionSwitchPolicy.StayScope
                && definition.FaintPolicy == BattleConditionFaintPolicy.Persist;
        if (!cleanupValid)
            throw new ArgumentException("Condition cleanup policies do not match the store scope.", nameof(definition));
        if (definition.EntryHazard is { } hazard
            && (definition.Scope != BattleConditionScope.Side
                || definition.Id != EntryHazardConditions.Validate(hazard).Id
                || !hooks.Contains(BattleConditionHook.SwitchIn)
                || definition.DefaultDuration is not null))
            throw new ArgumentException("Entry-hazard definitions require matching permanent side-scoped switch-in rows.", nameof(definition));

        return definition with
        {
            Hooks = Array.AsReadOnly(hooks),
            Tags = Array.AsReadOnly(tags),
            InitialCounters = new ReadOnlyDictionary<string, int>(orderedCounters),
            EntryHazard = definition.EntryHazard is { } entryHazard
                ? EntryHazardConditions.Normalize(entryHazard) : null,
        };
    }
}
