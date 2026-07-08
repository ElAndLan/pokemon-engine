namespace Cgm.Core.Model;

/// <summary>
/// Evaluates a trigger / door-lock condition string against story flags (Phase 13 badge gating).
/// v1 grammar (mirrors the encounter <c>RequiredFlag</c> pattern, kept deliberately small): a null or
/// empty condition is unconditionally met; a leading <c>!</c> negates; otherwise the token is a flag
/// key, met when that flag is truthy (bool true / int ≠ 0). The vocabulary can grow in later phases
/// (int comparisons, and/or) when a consumer needs it.
/// </summary>
public static class TriggerCondition
{
    public static bool IsMet(string? condition, Func<string, bool> flag)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return true;

        string c = condition.Trim();
        return c.StartsWith('!') ? !IsMet(c[1..], flag) : flag(c);
    }

    public static bool IsMet(string? condition, FlagStore flags) => IsMet(condition, flags.GetBool);
}
