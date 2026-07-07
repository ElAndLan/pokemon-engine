namespace Cgm.Core.Model;

public enum DepositTarget { Party, Box }

/// <summary>Where a deposited creature landed: the party, or box <see cref="BoxIndex"/>.</summary>
public readonly record struct DepositResult(DepositTarget Target, int BoxIndex);

/// <summary>Auto-deposit routing for caught creatures (DATA_SCHEMA §9 / MASTER_PLAN §8): party if it
/// has room (max 6), else the first box with room. Returns null if everything is full.</summary>
public static class PartyStorage
{
    public const int MaxParty = 6;

    public static DepositResult? Deposit(CreatureInstance creature, List<CreatureInstance> party,
        List<List<CreatureInstance>> boxes, int boxCapacity)
    {
        if (party.Count < MaxParty)
        {
            party.Add(creature);
            return new DepositResult(DepositTarget.Party, -1);
        }

        for (int b = 0; b < boxes.Count; b++)
        {
            if (boxes[b].Count < boxCapacity)
            {
                boxes[b].Add(creature);
                return new DepositResult(DepositTarget.Box, b);
            }
        }

        return null; // party and every box are full
    }
}
