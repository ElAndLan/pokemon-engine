using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>Generates a fresh <see cref="CreatureInstance"/> (wild encounters, starters): seeded
/// random IVs (0–31) and nature, zero EVs, exp at the level's threshold, full HP. Moves are chosen
/// by the caller (from the learnset).</summary>
public static class InstanceGen
{
    public static Stats RandomIvs(IRng rng) =>
        new(rng.Next(32), rng.Next(32), rng.Next(32), rng.Next(32), rng.Next(32), rng.Next(32));

    public static string RandomNature(IRng rng)
    {
        var all = Natures.All.ToList();
        return all[rng.Next(all.Count)];
    }

    public static CreatureInstance Create(EntityId species, Stats baseStats, string growthRate,
        int level, IReadOnlyList<MoveSlot> moves, IRng rng, string otName = "")
    {
        Stats ivs = RandomIvs(rng);
        string nature = RandomNature(rng);
        Stats evs = default; // zero EVs on generation
        Stats computed = StatCalc.Compute(baseStats, ivs, evs, nature, level);

        return new CreatureInstance
        {
            Species = species,
            Level = level,
            Exp = ExpCurve.TotalExp(growthRate, level),
            Ivs = ivs,
            Evs = evs,
            Nature = nature,
            CurHp = computed.Hp,
            Moves = moves,
            OtName = otName,
        };
    }
}
