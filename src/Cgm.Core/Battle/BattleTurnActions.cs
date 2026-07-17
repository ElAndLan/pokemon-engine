using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public abstract record BattleActionSelection;
public sealed record ActiveSlotSelection(BattleSlot Slot) : BattleActionSelection;
public sealed record PartyMemberSelection(BattleSide Side, int PartyIndex) : BattleActionSelection;
public sealed record MoveReferenceSelection(BattleSlot Slot, int MoveIndex) : BattleActionSelection;
public sealed record BattleReplacementSelection(BattleSlot Slot, int PartyIndex);

public sealed record BattleActionSubmission(BattleSlot Source, BattleAction Action, BattleActionSelection? Selection = null)
{
    public BattleSlot? Target => (Selection as ActiveSlotSelection)?.Slot;
    internal int? TargetPartySnapshot { get; init; }

    public BattleActionSubmission(BattleSlot source, BattleAction action, BattleSlot target)
        : this(source, action, new ActiveSlotSelection(target)) { }
}

/// <summary>One complete turn's submitted actions, normalized to immutable topology order.</summary>
public sealed class BattleTurnActions
{
    public BattleTurnActions(BattleTopology topology, IReadOnlyList<BattleActionSubmission> submitted)
    {
        Topology = topology ?? throw new ArgumentNullException(nameof(topology));
        ArgumentNullException.ThrowIfNull(submitted);
        var actions = new List<BattleActionSubmission>(submitted.Count);
        foreach (BattleSlot slot in topology.Slots)
        {
            BattleActionSubmission[] matches = submitted.Where(action => action.Source == slot).ToArray();
            if (matches.Length > 1)
                throw new ArgumentException($"Active slot {slot} submitted more than one action.", nameof(submitted));
            if (matches.Length == 0)
                continue;
            if (matches[0].Action is null)
                throw new ArgumentException($"Active slot {slot} submitted a null action.", nameof(submitted));
            if (matches[0].Selection is ActiveSlotSelection { Slot: var target } && !topology.Contains(target))
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
        IRng rng,
        Action<BattleScheduledAction, int, int, int>? tieDraw = null,
        bool reverseSpeed = false)
    {
        ArgumentNullException.ThrowIfNull(scheduled);
        ArgumentNullException.ThrowIfNull(rng);

        var ordered = new List<BattleScheduledAction>(scheduled.Count);
        foreach (IGrouping<(int Priority, int Speed), BattleScheduledAction> group in scheduled
            .GroupBy(action => (action.Priority, action.Speed))
            .OrderByDescending(group => group.Key.Priority)
            .ThenBy(group => reverseSpeed ? group.Key.Speed : -group.Key.Speed))
        {
            List<BattleScheduledAction> tied = [.. group];
            for (int index = tied.Count - 1; index > 0; index--)
            {
                int bound = index + 1;
                int draw = rng.Next(bound);
                int swap = index - draw;
                tieDraw?.Invoke(tied[index], draw, bound, swap);
                (tied[index], tied[swap]) = (tied[swap], tied[index]);
            }
            ordered.AddRange(tied);
        }

        return ordered;
    }
}
