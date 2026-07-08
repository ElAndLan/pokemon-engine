namespace Cgm.Core.Battle;

/// <summary>Things that change a creature's happiness (Phase 13). Gains from living with the player,
/// a loss on fainting.</summary>
public enum HappinessEvent { LevelUp, Walk, Vitamin, Faint }

/// <summary>
/// Happiness / friendship changes (Phase 13, Gen III/IV-approximate). The delta depends on the
/// current value's bracket — creatures that already like you gain more slowly. Pure and clamped to
/// 0–255. Rates below are the design's source of truth (no separate progression spec exists; this
/// mirrors how Exp/EV rules are documented in code).
/// </summary>
public static class Happiness
{
    public const int Min = 0;
    public const int Max = 255;

    /// <summary>Happiness gains apply once per this many steps walked (Gen III/IV).</summary>
    public const int WalkStepInterval = 128;

    // Brackets: 0 = [0,99] (low), 1 = [100,199], 2 = [200,255] (high). Deltas per bracket:
    private static readonly IReadOnlyDictionary<HappinessEvent, int[]> Deltas = new Dictionary<HappinessEvent, int[]>
    {
        [HappinessEvent.LevelUp] = [5, 4, 3],
        [HappinessEvent.Walk] = [2, 2, 1],
        [HappinessEvent.Vitamin] = [5, 3, 2],
        [HappinessEvent.Faint] = [-1, -1, -1],
    };

    public static int Delta(HappinessEvent evt, int current) => Deltas[evt][Bracket(current)];

    /// <summary>The new happiness after an event, clamped to 0–255.</summary>
    public static int Apply(int current, HappinessEvent evt) =>
        Math.Clamp(current + Delta(evt, current), Min, Max);

    private static int Bracket(int happiness) => happiness < 100 ? 0 : happiness < 200 ? 1 : 2;
}
