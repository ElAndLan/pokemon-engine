using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>The random checks of a damaging hit (BATTLE_DAMAGE_CALC §6/§9), drawn from the injected
/// <see cref="IRng"/>. Draw order in the resolver is fixed: crit → damage roll → secondary effects.</summary>
public static class BattleRolls
{
    /// <summary>Null accuracy (e.g. Swift) always hits; otherwise a d100 under the accuracy value.</summary>
    public static bool Hits(int? accuracy, IRng rng) => accuracy is not int acc || rng.Next(100) < acc;

    public static bool Hits(int? accuracy, int accuracyStage, int evasionStage, IRng rng) =>
        Hits(accuracy, accuracyStage, evasionStage, rng, out _);

    public static bool Hits(int? accuracy, int accuracyStage, int evasionStage, IRng rng, out int? draw)
    {
        if (accuracy is not int acc) { draw = null; return true; }
        acc = BattleQuery.ResolveInteger(BattleQueryId.Accuracy, acc,
            [new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply,
                BattleQuery.AccuracyStageMultiplier(accuracyStage, evasionStage), InsertionOrder: 0)]);
        draw = rng.Next(100);
        return draw.Value < acc;
    }

    /// <summary>Gen III/IV crit chance by stage: 1/16, 1/8, 1/4, 1/3, then 1/2.</summary>
    public static BattleQueryValue CritChanceValue(int stage) => stage switch
    {
        <= 0 => new BattleQueryValue(1, 16),
        1 => new BattleQueryValue(1, 8),
        2 => new BattleQueryValue(1, 4),
        3 => new BattleQueryValue(1, 3),
        _ => new BattleQueryValue(1, 2),
    };

    public static double CritChance(int stage)
    {
        BattleQueryValue chance = CritChanceValue(stage);
        return (double)chance.Numerator / chance.Denominator;
    }

    public static bool IsCrit(int stage, IRng rng) => rng.NextDouble() < CritChance(stage);
    public static bool IsCrit(int stage, IRng rng, out double draw) => (draw = rng.NextDouble()) < CritChance(stage);
    internal static bool IsCrit(BattleQueryValue chance, IRng rng, out double draw) =>
        (draw = rng.NextDouble()) < (double)chance.Numerator / chance.Denominator;

    /// <summary>The 85–100 damage roll (inclusive).</summary>
    public static int DamageRoll(IRng rng) => 85 + rng.Next(16);
    public static int DamageRoll(IRng rng, out int draw) => 85 + (draw = rng.Next(16));
}
