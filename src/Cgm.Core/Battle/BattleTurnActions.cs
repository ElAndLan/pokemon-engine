using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public sealed record BattleActionSubmission(BattleSlot Source, BattleAction Action, BattleSlot? Target = null);

/// <summary>One complete turn's submitted actions, normalized to immutable topology order.</summary>
public sealed class BattleTurnActions
{
    public BattleTurnActions(BattleTopology topology, IReadOnlyList<BattleActionSubmission> submitted)
    {
        Topology = topology ?? throw new ArgumentNullException(nameof(topology));
        ArgumentNullException.ThrowIfNull(submitted);
        if (submitted.Count != topology.Slots.Count)
            throw new ArgumentException("Every active slot must submit exactly one action.", nameof(submitted));

        var actions = new List<BattleActionSubmission>(topology.Slots.Count);
        foreach (BattleSlot slot in topology.Slots)
        {
            BattleActionSubmission[] matches = submitted.Where(action => action.Source == slot).ToArray();
            if (matches.Length != 1)
                throw new ArgumentException($"Active slot {slot} must submit exactly one action.", nameof(submitted));
            if (matches[0].Action is null)
                throw new ArgumentException($"Active slot {slot} submitted a null action.", nameof(submitted));
            if (matches[0].Target is { } target && !topology.Contains(target))
                throw new ArgumentException($"Target slot {target} is outside the battle topology.", nameof(submitted));
            actions.Add(matches[0]);
        }

        if (submitted.Any(action => !topology.Contains(action.Source)))
            throw new ArgumentException("Submitted action source is outside the battle topology.", nameof(submitted));

        Actions = actions;
    }

    public BattleTopology Topology { get; }
    public IReadOnlyList<BattleActionSubmission> Actions { get; }

    public BattleActionSubmission For(BattleSlot slot) =>
        Actions.Single(action => action.Source == slot);
}

public sealed record BattleScheduledAction(BattleActionSubmission Submission, int Priority, int Speed);

public static class BattleTurnOrder
{
    /// <summary>Orders scheduled actions by priority, speed, then deterministic RNG tie groups.</summary>
    public static IReadOnlyList<BattleScheduledAction> Order(
        IReadOnlyList<BattleScheduledAction> scheduled,
        IRng rng)
    {
        ArgumentNullException.ThrowIfNull(scheduled);
        ArgumentNullException.ThrowIfNull(rng);

        var ordered = new List<BattleScheduledAction>(scheduled.Count);
        foreach (IGrouping<(int Priority, int Speed), BattleScheduledAction> group in scheduled
            .GroupBy(action => (action.Priority, action.Speed))
            .OrderByDescending(group => group.Key.Priority)
            .ThenByDescending(group => group.Key.Speed))
        {
            List<BattleScheduledAction> tied = [.. group];
            for (int index = tied.Count - 1; index > 0; index--)
            {
                int swap = index - rng.Next(index + 1);
                (tied[index], tied[swap]) = (tied[swap], tied[index]);
            }
            ordered.AddRange(tied);
        }

        return ordered;
    }
}
