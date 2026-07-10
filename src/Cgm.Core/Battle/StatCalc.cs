using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum StatKind { Hp, Atk, Def, Spa, Spd, Spe, Accuracy, Evasion }

/// <summary>The 25 natures: each raises one stat by 10% and lowers another by 10% (five are
/// neutral). HP is never affected. Keyed by lowercase name (matches PokeAPI/DATA_SCHEMA).</summary>
public static class Natures
{
    private static readonly IReadOnlyDictionary<string, (StatKind Up, StatKind Down)> Table =
        new Dictionary<string, (StatKind, StatKind)>
        {
            ["hardy"] = (StatKind.Atk, StatKind.Atk),
            ["lonely"] = (StatKind.Atk, StatKind.Def),
            ["brave"] = (StatKind.Atk, StatKind.Spe),
            ["adamant"] = (StatKind.Atk, StatKind.Spa),
            ["naughty"] = (StatKind.Atk, StatKind.Spd),
            ["bold"] = (StatKind.Def, StatKind.Atk),
            ["docile"] = (StatKind.Def, StatKind.Def),
            ["relaxed"] = (StatKind.Def, StatKind.Spe),
            ["impish"] = (StatKind.Def, StatKind.Spa),
            ["lax"] = (StatKind.Def, StatKind.Spd),
            ["timid"] = (StatKind.Spe, StatKind.Atk),
            ["hasty"] = (StatKind.Spe, StatKind.Def),
            ["serious"] = (StatKind.Spe, StatKind.Spe),
            ["jolly"] = (StatKind.Spe, StatKind.Spa),
            ["naive"] = (StatKind.Spe, StatKind.Spd),
            ["modest"] = (StatKind.Spa, StatKind.Atk),
            ["mild"] = (StatKind.Spa, StatKind.Def),
            ["quiet"] = (StatKind.Spa, StatKind.Spe),
            ["bashful"] = (StatKind.Spa, StatKind.Spa),
            ["rash"] = (StatKind.Spa, StatKind.Spd),
            ["calm"] = (StatKind.Spd, StatKind.Atk),
            ["gentle"] = (StatKind.Spd, StatKind.Def),
            ["sassy"] = (StatKind.Spd, StatKind.Spe),
            ["careful"] = (StatKind.Spd, StatKind.Spa),
            ["quirky"] = (StatKind.Spd, StatKind.Spd),
        };

    public static IReadOnlyCollection<string> All => (IReadOnlyCollection<string>)Table.Keys;

    public static bool IsValid(string nature) => Table.ContainsKey(nature);

    public static double Multiplier(string nature, StatKind stat)
    {
        if (stat == StatKind.Hp || !Table.TryGetValue(nature, out (StatKind Up, StatKind Down) n) || n.Up == n.Down)
            return 1.0;
        if (stat == n.Up) return 1.1;
        if (stat == n.Down) return 0.9;
        return 1.0;
    }
}

/// <summary>Gen 3+ battle-stat formulas from base + IVs (0–31) + EVs (0–252) + nature + level
/// (BATTLE_SYSTEM_SPEC / BATTLE_DAMAGE_CALC §3). All integer math; nature multiply floors last.</summary>
public static class StatCalc
{
    public static Stats Compute(Stats bases, Stats ivs, Stats evs, string nature, int level)
    {
        int hp = (2 * bases.Hp + ivs.Hp + evs.Hp / 4) * level / 100 + level + 10;
        return new Stats(
            hp,
            Other(bases.Atk, ivs.Atk, evs.Atk, level, Natures.Multiplier(nature, StatKind.Atk)),
            Other(bases.Def, ivs.Def, evs.Def, level, Natures.Multiplier(nature, StatKind.Def)),
            Other(bases.Spa, ivs.Spa, evs.Spa, level, Natures.Multiplier(nature, StatKind.Spa)),
            Other(bases.Spd, ivs.Spd, evs.Spd, level, Natures.Multiplier(nature, StatKind.Spd)),
            Other(bases.Spe, ivs.Spe, evs.Spe, level, Natures.Multiplier(nature, StatKind.Spe)));
    }

    private static int Other(int b, int iv, int ev, int level, double nature) =>
        (int)(((2 * b + iv + ev / 4) * level / 100 + 5) * nature);
}
