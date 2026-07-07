namespace Cgm.Core.Model;

/// <summary>
/// The six standard experience curves (DATA_SCHEMA.md §4.15). These are fixed reference data, not
/// per-project entities — species reference one by key. The level→exp tables are populated when
/// leveling is built (Phase 9); v1 only needs the valid key set for validation.
/// </summary>
public static class GrowthRates
{
    public static readonly IReadOnlySet<string> Keys = new HashSet<string>
    {
        "fast", "medium-fast", "medium-slow", "slow", "erratic", "fluctuating",
    };

    public static bool IsValid(string key) => Keys.Contains(key);
}
