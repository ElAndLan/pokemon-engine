using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>The `basic` trainer AI (Battle v3): greedily pick the move with the highest expected
/// damage against the target, treating immune/status moves as worthless and ignoring PP-less moves.
/// Ties broken randomly. The tougher `smart` AI is a later phase.</summary>
public static class BasicAi
{
    public static int ChooseMove(BattleCreature attacker, BattleCreature defender, TypeChart chart, IRng rng)
    {
        double best = -1;
        var ties = new List<int>();

        for (int i = 0; i < attacker.Moves.Count; i++)
        {
            if (!attacker.Moves[i].HasPp)
                continue;

            double score = ExpectedDamage(attacker, defender, attacker.Moves[i], chart);
            if (score > best)
            {
                best = score;
                ties.Clear();
                ties.Add(i);
            }
            else if (score == best)
            {
                ties.Add(i);
            }
        }

        if (ties.Count == 0)
            return FirstUsableOrZero(attacker); // no PP anywhere → Struggle fallback (v1)

        return ties[rng.Next(ties.Count)];
    }

    private static double ExpectedDamage(BattleCreature atk, BattleCreature def, BattleMove move, TypeChart chart)
    {
        if (move.Power is not int power)
            return 0; // status move — basic AI doesn't value it

        double eff = chart.Effectiveness(move.Type, def.Types);
        if (eff <= 0)
            return 0; // immune — useless

        power = BattleQuery.ResolveInteger(BattleQueryId.BasePower, power);
        bool physical = move.DamageClass == DamageClass.Physical;
        int a = BattleQuery.ResolveInteger(BattleQueryId.OffensiveStat, physical ? atk.Stats.Atk : atk.Stats.Spa);
        int d = BattleQuery.ResolveInteger(BattleQueryId.DefensiveStat, physical ? def.Stats.Def : def.Stats.Spd);
        double stab = TypeChart.Stab(move.Type, atk.Types);

        return BattleQuery.ResolveInteger(BattleQueryId.FinalDamage,
            DamageCalc.Compute(atk.Level, power, a, d, eff, stab, crit: false, roll: 92, burn: false));
    }

    private static int FirstUsableOrZero(BattleCreature atk)
    {
        for (int i = 0; i < atk.Moves.Count; i++)
            if (atk.Moves[i].HasPp)
                return i;
        return 0;
    }
}
