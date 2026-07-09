using System.Text.Json;
using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>
/// Compiles a data-defined <see cref="Move"/> (its closed <see cref="Move.Effects"/> op palette) into a
/// runtime <see cref="BattleMove"/> — the "moves are data, not code" bridge (DATA_SCHEMA §4.4a). v4 ops
/// (<c>damage</c>, <c>ailment</c>, <c>statStage</c>, <c>flinch</c>) map onto the current engine; the
/// stateful v5 ops (drain/heal/multiHit/…) throw until the controller resolves them in their own batch.
/// </summary>
public static class MoveCompiler
{
    public static BattleMove ToBattleMove(Move move)
    {
        PersistentStatus? ailment = null;
        int ailmentChance = 0, confuseChance = 0, flinchChance = 0;
        StageEffect? stageEffect = null;
        Fraction? drain = null, recoil = null, heal = null;
        bool recoilOnMiss = false;
        int multiHitMin = 0, multiHitMax = 0;
        int? fixedDamage = null;
        bool fixedDamageLevel = false, ohko = false, selfDestruct = false, leechSeed = false, setsSpikes = false;
        bool setsStealthRock = false, binds = false, isProtect = false, forcesSwitch = false, bypassAccuracy = false;
        bool chargeTurn = false, multiTurnLock = false;
        int critBoost = 0;
        Weather setsWeather = Weather.None;
        DamageClass? counterCategory = null;

        foreach (Effect e in move.Effects)
        {
            int chance = e.Chance ?? 100;
            switch (e.Op)
            {
                case "damage":
                    break; // damage is implicit from Power; the op is just a marker

                case "drain":
                    drain = ReadFraction(e, 1, 2);
                    break;

                case "recoil":
                    recoil = ReadFraction(e, 1, 4);
                    recoilOnMiss = Bool(e, "onMiss");
                    break;

                case "heal":
                    heal = ReadFraction(e, 1, 2);
                    break;

                case "multiHit":
                    multiHitMin = Int(e, "min");
                    multiHitMax = Int(e, "max");
                    if (multiHitMin < 1 || multiHitMax < multiHitMin)
                        throw new ArgumentException($"multiHit range {multiHitMin}–{multiHitMax} is invalid.");
                    break;

                case "fixedDamage":
                    fixedDamageLevel = Bool(e, "levelBased");
                    if (!fixedDamageLevel)
                        fixedDamage = Int(e, "amount");
                    break;

                case "ohko":
                    ohko = true;
                    break;

                case "critBoost":
                    critBoost = e.Params?.TryGetValue("stages", out JsonElement s) == true ? s.GetInt32() : 2;
                    break;

                case "selfDestruct":
                    selfDestruct = true;
                    break;

                case "leechSeed":
                    leechSeed = true;
                    break;

                case "spikes": // preset for apply_condition(side:entry_hazard_damage) (catalog §9.4)
                    setsSpikes = true;
                    break;

                case "weather": // apply_condition(field:weather) (catalog §7.6)
                    setsWeather = Parse<Weather>(Str(e, "weather"), "weather");
                    break;

                case "stealthRock": // apply_condition(side:entry_hazard_damage, type_scaled) (catalog §9.4)
                    setsStealthRock = true;
                    break;

                case "bind": // apply_condition(volatile:partial_trap) (catalog §7.2)
                    binds = true;
                    break;

                case "protect": // apply_condition(volatile:protect_family) (catalog §7.2)
                    isProtect = true;
                    break;

                case "forceSwitch": // switch_flow(force_target_switch) (catalog §9.6)
                    forcesSwitch = true;
                    break;

                case "counterDamage": // deal_damage(counter_received_damage) (catalog §9.2)
                    counterCategory = Parse<DamageClass>(Str(e, "category"), "category");
                    break;

                case "accuracyBypass": // sure-hit (catalog §3.3 accuracy_check bypass)
                    bypassAccuracy = true;
                    break;

                case "chargeTurn": // two-turn move (catalog §7.2 charge)
                    chargeTurn = true;
                    break;

                case "multiTurnLock": // Thrash/Outrage rampage lock (catalog §9.3)
                    multiTurnLock = true;
                    break;

                case "ailment":
                    string a = Str(e, "ailment");
                    if (a.Equals("confusion", StringComparison.OrdinalIgnoreCase))
                        confuseChance = chance;
                    else
                    {
                        ailment = Parse<PersistentStatus>(a, "ailment");
                        ailmentChance = chance;
                    }
                    break;

                case "statStage":
                    StatKind stat = Parse<StatKind>(Str(e, "stat"), "stat");
                    if (stat == StatKind.Hp)
                        throw new NotSupportedException("statStage op cannot target HP.");
                    stageEffect = new StageEffect(stat, Int(e, "delta"), Bool(e, "onSelf"), chance);
                    break;

                case "flinch":
                    flinchChance = chance;
                    break;

                default:
                    throw new NotSupportedException($"Effect op '{e.Op}' needs Battle v5 controller support.");
            }
        }

        return new BattleMove(move.Id, move.Type, move.DamageClass, move.Power, move.Accuracy, move.Pp,
            move.Priority, move.CritStage, ailment, ailmentChance, stageEffect, confuseChance, flinchChance,
            drain, recoil, recoilOnMiss, heal, multiHitMin, multiHitMax,
            fixedDamage, fixedDamageLevel, ohko, critBoost, selfDestruct, leechSeed, setsSpikes, setsWeather,
            setsStealthRock, binds, isProtect, forcesSwitch, counterCategory, bypassAccuracy, chargeTurn,
            multiTurnLock, move.MakesContact);
    }

    /// <summary>Reads a <c>{ num, den }</c> fraction, defaulting either component when absent.</summary>
    private static Fraction ReadFraction(Effect e, int defNum, int defDen)
    {
        int num = e.Params?.TryGetValue("num", out JsonElement n) == true ? n.GetInt32() : defNum;
        int den = e.Params?.TryGetValue("den", out JsonElement d) == true ? d.GetInt32() : defDen;
        if (den == 0)
            throw new ArgumentException($"Effect op '{e.Op}' has a zero denominator.");
        return new Fraction(num, den);
    }

    private static JsonElement Field(Effect e, string key) =>
        e.Params is not null && e.Params.TryGetValue(key, out JsonElement v)
            ? v
            : throw new ArgumentException($"Effect op '{e.Op}' is missing required param '{key}'.");

    private static string Str(Effect e, string key) =>
        Field(e, key).GetString() ?? throw new ArgumentException($"Effect op '{e.Op}' param '{key}' is not a string.");

    private static int Int(Effect e, string key) => Field(e, key).GetInt32();

    private static bool Bool(Effect e, string key) =>
        e.Params is not null && e.Params.TryGetValue(key, out JsonElement v) && v.GetBoolean();

    private static T Parse<T>(string value, string what) where T : struct, Enum =>
        Enum.TryParse(value, ignoreCase: true, out T result)
            ? result
            : throw new ArgumentException($"Unknown {what} '{value}'.");
}
