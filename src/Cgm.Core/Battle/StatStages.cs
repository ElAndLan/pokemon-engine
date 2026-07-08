namespace Cgm.Core.Battle;

/// <summary>In-battle stat stages (−6..+6), Battle v4 (BATTLE_DAMAGE_CALC §7). Offensive/defensive
/// stats use (2+n)/2 up, 2/(2+|n|) down; accuracy/evasion use the 3-based table.</summary>
public static class StatStages
{
    public const int Min = -6;
    public const int Max = 6;

    public static int Clamp(int stage) => Math.Clamp(stage, Min, Max);

    /// <summary>Adds a delta, clamped to the −6..+6 range; returns the new stage.</summary>
    public static int Apply(int current, int delta) => Clamp(current + delta);

    /// <summary>Multiplier for Attack/Defense/Sp.Atk/Sp.Def/Speed at a stage.</summary>
    public static double Multiplier(int stage)
    {
        stage = Clamp(stage);
        return stage >= 0 ? (2.0 + stage) / 2.0 : 2.0 / (2 - stage);
    }

    /// <summary>Multiplier for Accuracy/Evasion at a stage.</summary>
    public static double AccEvaMultiplier(int stage)
    {
        stage = Clamp(stage);
        return stage >= 0 ? (3.0 + stage) / 3.0 : 3.0 / (3 - stage);
    }
}
