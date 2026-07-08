namespace Cgm.Core.Model;

/// <summary>Rolls a wild encounter from an <see cref="EncounterTable"/> (MASTER_PLAN §11): picks a
/// slot by weight among those eligible for the current time/flags, then rolls a level in range.
/// Pure and seeded.</summary>
public static class EncounterRoll
{
    public static EncounterSlot? PickSlot(EncounterTable table, IRng rng,
        TimeOfDay? time = null, Func<string, bool>? flagSet = null)
    {
        List<EncounterSlot> eligible = table.Slots.Where(s => Eligible(s, time, flagSet)).ToList();
        int total = eligible.Sum(s => s.Weight);
        if (total <= 0)
            return null;

        int r = rng.Next(total);
        int acc = 0;
        foreach (EncounterSlot s in eligible)
        {
            acc += s.Weight;
            if (r < acc)
                return s;
        }
        return eligible[^1]; // unreachable given total > 0
    }

    public static int RollLevel(EncounterSlot slot, IRng rng) =>
        rng.Next(slot.MinLevel, slot.MaxLevel + 1);

    /// <summary>Whether a step in an encounter zone triggers an encounter (per-step probability).</summary>
    public static bool Triggers(double perStepRate, IRng rng) => rng.NextDouble() < perStepRate;

    /// <summary>Repel suppresses a wild encounter whose level is below the party lead's, while charges
    /// remain (Phase 10).</summary>
    public static bool RepelSuppresses(int repelStepsRemaining, int leadLevel, int wildLevel) =>
        repelStepsRemaining > 0 && wildLevel < leadLevel;

    private static bool Eligible(EncounterSlot s, TimeOfDay? time, Func<string, bool>? flagSet) =>
        (s.TimeOfDay is null || time is null || s.TimeOfDay == time)
        && (s.RequiredFlag is null || (flagSet?.Invoke(s.RequiredFlag) ?? true));
}
