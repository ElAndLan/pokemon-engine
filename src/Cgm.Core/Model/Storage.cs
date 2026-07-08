namespace Cgm.Core.Model;

/// <summary>PC box operations (Phase 10): moving creatures between the party and boxes. Enforces the
/// party max (6) and the rule that the party can never be left without a healthy member. All ops
/// return whether they succeeded; totals are conserved on a move.</summary>
public static class Storage
{
    /// <summary>Move a party member into a box. Fails on a full box, bad index, or if it would leave
    /// the party with no healthy (non-fainted) member.</summary>
    public static bool DepositToBox(List<CreatureInstance> party, List<CreatureInstance> box,
        int partyIndex, int boxCapacity)
    {
        if (partyIndex < 0 || partyIndex >= party.Count)
            return false;
        if (box.Count >= boxCapacity)
            return false;
        if (WouldStrandParty(party, partyIndex))
            return false;

        box.Add(party[partyIndex]);
        party.RemoveAt(partyIndex);
        return true;
    }

    /// <summary>Move a boxed creature into the party. Fails if the party is full or the slot is bad.</summary>
    public static bool WithdrawToParty(List<CreatureInstance> party, List<CreatureInstance> box, int boxSlot)
    {
        if (boxSlot < 0 || boxSlot >= box.Count)
            return false;
        if (party.Count >= PartyStorage.MaxParty)
            return false;

        party.Add(box[boxSlot]);
        box.RemoveAt(boxSlot);
        return true;
    }

    public static bool ReleaseFromBox(List<CreatureInstance> box, int slot)
    {
        if (slot < 0 || slot >= box.Count)
            return false;
        box.RemoveAt(slot);
        return true;
    }

    /// <summary>Release a party member — blocked if it would strand the party (no healthy member left).</summary>
    public static bool ReleaseFromParty(List<CreatureInstance> party, int index)
    {
        if (index < 0 || index >= party.Count || WouldStrandParty(party, index))
            return false;
        party.RemoveAt(index);
        return true;
    }

    private static bool WouldStrandParty(List<CreatureInstance> party, int removingIndex)
    {
        for (int i = 0; i < party.Count; i++)
            if (i != removingIndex && party[i].CurHp > 0)
                return false; // another healthy member remains
        return true;
    }
}
