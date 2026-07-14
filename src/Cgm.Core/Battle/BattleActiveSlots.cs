namespace Cgm.Core.Battle;

/// <summary>Mutable mapping from stable active slots to party indices for one battle topology.</summary>
public sealed class BattleActiveSlots
{
    private readonly int[][] _partyIndexes;

    public BattleActiveSlots(BattleTopology topology)
    {
        Topology = topology ?? throw new ArgumentNullException(nameof(topology));
        _partyIndexes =
        [
            Enumerable.Repeat(-1, topology.ActiveSlotsPerSide).ToArray(),
            Enumerable.Repeat(-1, topology.ActiveSlotsPerSide).ToArray(),
        ];
    }

    public BattleTopology Topology { get; }

    public int PartyIndex(BattleSlot slot)
    {
        ValidateSlot(slot);
        int partyIndex = _partyIndexes[(int)slot.Side][slot.Position];
        return partyIndex >= 0
            ? partyIndex
            : throw new InvalidOperationException($"Active slot {slot} has no party assignment.");
    }

    public void Assign(BattleSlot slot, int partyIndex)
    {
        ValidateSlot(slot);
        if (partyIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(partyIndex), "Party index cannot be negative.");

        int[] sideSlots = _partyIndexes[(int)slot.Side];
        if (sideSlots.Where((assigned, position) => position != slot.Position).Contains(partyIndex))
            throw new ArgumentException($"Party member {partyIndex} is already active on {slot.Side}.", nameof(partyIndex));

        sideSlots[slot.Position] = partyIndex;
    }

    public bool IsActive(BattleSide side, int partyIndex) =>
        Enum.IsDefined(side) && partyIndex >= 0 && _partyIndexes[(int)side].Contains(partyIndex);

    /// <summary>Atomically exchanges two active slots on one side.</summary>
    public void Swap(BattleSlot first, BattleSlot second)
    {
        ValidateSlot(first);
        ValidateSlot(second);
        if (first.Side != second.Side)
            throw new ArgumentException("Only allied active slots can be exchanged.");
        if (first == second)
            throw new ArgumentException("An active slot cannot be exchanged with itself.");

        int[] sideSlots = _partyIndexes[(int)first.Side];
        (sideSlots[first.Position], sideSlots[second.Position]) = (sideSlots[second.Position], sideSlots[first.Position]);
    }

    private void ValidateSlot(BattleSlot slot)
    {
        if (!Topology.Contains(slot))
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "Slot is outside the battle topology.");
    }
}
