using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>Pure arithmetic for the two non-trivial stat-stage mutations added in Phase 15F-5 —
/// positive-boost theft and single-draw random raise. Both derive their outputs from the caller's
/// captured pre-mutation snapshot and clamp to the −6..+6 range; the only RNG is the one documented
/// draw. Reset/copy/swap/invert stay as their existing controller effects.</summary>
public static class BattleStageMutation
{
    public static readonly IReadOnlyList<StatKind> Stageable =
        [StatKind.Atk, StatKind.Def, StatKind.Spa, StatKind.Spd, StatKind.Spe, StatKind.Accuracy, StatKind.Evasion];

    public static (IReadOnlyDictionary<StatKind, int> User, IReadOnlyDictionary<StatKind, int> Target) Steal(
        IReadOnlyDictionary<StatKind, int> user, IReadOnlyDictionary<StatKind, int> target,
        IReadOnlyList<StatKind>? stats = null)
    {
        Validate(target);
        return (Map(user, stats, (stat, value) => StatStages.Clamp(value + Math.Max(0, target[stat]))),
            Map(target, stats, (_, value) => Math.Min(value, 0)));
    }

    /// <summary>Raises one randomly chosen eligible stat (below +6) by <paramref name="delta"/> using
    /// enum order and a single draw. An empty eligible pool changes nothing and reports no chosen stat.</summary>
    public static (StatKind? Chosen, IReadOnlyDictionary<StatKind, int> Result) RandomRaise(
        IReadOnlyDictionary<StatKind, int> current, int delta, IRng rng, IReadOnlyList<StatKind>? stats = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        Validate(current);
        StatKind[] eligible = Subset(stats).Where(stat => current[stat] < StatStages.Max)
            .OrderBy(stat => (int)stat).ToArray();
        if (eligible.Length == 0)
            return (null, current);
        StatKind chosen = eligible[rng.Next(eligible.Length)];
        return (chosen, Map(current, [chosen], (_, value) => StatStages.Apply(value, delta)));
    }

    private static IReadOnlyDictionary<StatKind, int> Map(IReadOnlyDictionary<StatKind, int> current,
        IReadOnlyList<StatKind>? stats, Func<StatKind, int, int> transform)
    {
        Validate(current);
        IReadOnlySet<StatKind> affected = Subset(stats);
        return Stageable.ToDictionary(stat => stat,
            stat => affected.Contains(stat) ? transform(stat, current[stat]) : current[stat]);
    }

    private static IReadOnlySet<StatKind> Subset(IReadOnlyList<StatKind>? stats)
    {
        if (stats is null)
            return Stageable.ToHashSet();
        if (stats.Count == 0 || stats.Any(stat => !Stageable.Contains(stat)) || stats.Distinct().Count() != stats.Count)
            throw new ArgumentException("Stage mutation stats must be a unique nonempty subset of the stageable stats.",
                nameof(stats));
        return stats.ToHashSet();
    }

    private static void Validate(IReadOnlyDictionary<StatKind, int> stages)
    {
        ArgumentNullException.ThrowIfNull(stages);
        foreach (StatKind stat in Stageable)
            if (!stages.TryGetValue(stat, out int value) || value < StatStages.Min || value > StatStages.Max)
                throw new ArgumentException("Stage snapshot must define every stageable stat within −6..+6.",
                    nameof(stages));
    }
}
