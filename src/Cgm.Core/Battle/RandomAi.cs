using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>The `random` trainer AI (Battle v3): pick uniformly among the moves that still have PP.
/// Deterministic for a given <see cref="IRng"/>. Used for low-effort trainers; `basic` is the default.</summary>
public static class RandomAi
{
    public static int ChooseMove(BattleCreature attacker, IRng rng)
    {
        var usable = new List<int>();
        for (int i = 0; i < attacker.Moves.Count; i++)
            if (attacker.Moves[i].HasPp)
                usable.Add(i);

        return usable.Count == 0 ? 0 : usable[rng.Next(usable.Count)]; // no PP → Struggle fallback (v1)
    }
}
