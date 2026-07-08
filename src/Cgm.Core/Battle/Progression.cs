using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>Old→new level and the learnset moves gained crossing it.</summary>
public readonly record struct LevelUpResult(int OldLevel, int NewLevel, IReadOnlyList<EntityId> MovesLearned)
{
    public bool LeveledUp => NewLevel > OldLevel;
}

/// <summary>Post-battle progression (Phase 9): applying exp (level-up + learnset) and EV yields with
/// the Gen caps. Pure — instances are immutable, so these return updated copies.</summary>
public static class Progression
{
    public const int EvPerStatCap = 252;
    public const int EvTotalCap = 510;

    public static (CreatureInstance Updated, LevelUpResult Result) GainExp(
        CreatureInstance creature, int amount, string growthRate, IReadOnlyList<LearnsetEntry> learnset)
    {
        long newExp = creature.Exp + Math.Max(0, amount);
        int newLevel = ExpCurve.LevelForExp(growthRate, newExp);

        List<EntityId> learned = learnset
            .Where(e => e.Level > creature.Level && e.Level <= newLevel)
            .Select(e => e.Move)
            .ToList();

        var updated = creature with { Exp = newExp, Level = newLevel };
        return (updated, new LevelUpResult(creature.Level, newLevel, learned));
    }

    public static CreatureInstance ApplyEvYield(CreatureInstance creature, Stats yield) =>
        creature with { Evs = AddCapped(creature.Evs, yield) };

    private static Stats AddCapped(Stats ev, Stats yield)
    {
        int[] cur = [ev.Hp, ev.Atk, ev.Def, ev.Spa, ev.Spd, ev.Spe];
        int[] add = [yield.Hp, yield.Atk, yield.Def, yield.Spa, yield.Spd, yield.Spe];
        int total = cur.Sum();

        for (int i = 0; i < 6; i++)
        {
            int room = Math.Min(EvPerStatCap - cur[i], EvTotalCap - total);
            int gain = Math.Clamp(add[i], 0, Math.Max(0, room));
            cur[i] += gain;
            total += gain;
        }

        return new Stats(cur[0], cur[1], cur[2], cur[3], cur[4], cur[5]);
    }
}
