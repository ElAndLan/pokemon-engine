using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>Turn order (BATTLE_SYSTEM_SPEC): higher move priority first, then higher effective
/// Speed, with a speed tie broken randomly.</summary>
public static class TurnOrder
{
    /// <summary>True if actor A acts before actor B.</summary>
    public static bool AFirst(int priorityA, int speedA, int priorityB, int speedB, IRng rng)
    {
        if (priorityA != priorityB) return priorityA > priorityB;
        if (speedA != speedB) return speedA > speedB;
        return rng.Next(2) == 0; // speed tie → coin flip
    }
}
