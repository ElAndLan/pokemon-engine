namespace Cgm.Core.Battle;

public enum Weather { None, Rain, Sun, Sandstorm, Hail }

/// <summary>
/// A weather as a data-defined field condition (EFFECT_TYPES_CATALOG §7.6): its hook parameters —
/// on_turn_end residual (denominator + type immunities) and on_damage_query type modifiers (a boosted
/// ×1.5 and a weakened ×0.5 move type). One table row per weather; new weather is data, not branches.
/// </summary>
public sealed record WeatherDef
{
    public required Weather Weather { get; init; }
    public int ResidualDenominator { get; init; }                 // on_turn_end chip: maxHp / denom (0 = none)
    public IReadOnlyList<string> ResidualImmuneTypes { get; init; } = [];
    public string? BoostedMoveType { get; init; }                 // on_damage_query ×1.5
    public string? WeakenedMoveType { get; init; }                // on_damage_query ×0.5
}

/// <summary>The weather registry + query helpers. Weather lasts <see cref="DefaultTurns"/> turns.</summary>
public static class WeatherConditions
{
    public const int DefaultTurns = 5;

    private static readonly IReadOnlyDictionary<Weather, WeatherDef> Registry =
        new WeatherDef[]
        {
            new() { Weather = Weather.None },
            new() { Weather = Weather.Rain, BoostedMoveType = "water", WeakenedMoveType = "fire" },
            new() { Weather = Weather.Sun, BoostedMoveType = "fire", WeakenedMoveType = "water" },
            new() { Weather = Weather.Sandstorm, ResidualDenominator = 16,
                    ResidualImmuneTypes = ["rock", "ground", "steel"] },
            new() { Weather = Weather.Hail, ResidualDenominator = 16, ResidualImmuneTypes = ["ice"] },
        }.ToDictionary(d => d.Weather);

    public static WeatherDef For(Weather weather) => Registry[weather];

    /// <summary>on_damage_query type modifier: ×1.5 for the boosted type, ×0.5 for the weakened one.</summary>
    public static double DamageMultiplier(Weather weather, string moveTypeSlug)
    {
        WeatherDef def = For(weather);
        if (moveTypeSlug == def.BoostedMoveType) return 1.5;
        if (moveTypeSlug == def.WeakenedMoveType) return 0.5;
        return 1.0;
    }
}
