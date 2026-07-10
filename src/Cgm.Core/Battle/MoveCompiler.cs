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
        List<StageEffect> stageEffects = [];
        List<MoveEffect> effects = [];
        StageAllEffect? stageAllEffect = null;
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
        StatKind? offensiveStatOverride = null, defensiveStatOverride = null;
        TargetHpThresholdPower? targetHpThresholdPower = null;
        HpRatioPower? hpRatioPower = null;

        foreach (Effect e in move.Effects)
        {
            int chance = e.Chance ?? 100;
            switch (e.Op)
            {
                case "damage":
                    break; // damage is implicit from Power; the op is just a marker

                case "noBattleEffect":
                case "postBattleReward":
                    break; // intentional no-op/reward marker; outside battle-state mutation

                case "drain":
                    drain = ReadFraction(e, 1, 2);
                    effects.Add(new DrainEffect(drain.Value));
                    break;

                case "recoil":
                    recoil = ReadFraction(e, 1, 4);
                    recoilOnMiss = Bool(e, "onMiss");
                    if (!recoilOnMiss)
                        effects.Add(new RecoilEffect(recoil.Value));
                    break;

                case "heal":
                    heal = ReadFraction(e, 1, 2);
                    effects.Add(new HealEffect(heal.Value));
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
                    effects.Add(new CritBoostEffect(critBoost));
                    break;

                case "selfDestruct":
                    selfDestruct = true;
                    effects.Add(new SelfDestructEffect());
                    break;

                case "leechSeed":
                    leechSeed = true;
                    effects.Add(new LeechSeedEffect());
                    break;

                case "spikes": // preset for apply_condition(side:entry_hazard_damage) (catalog §9.4)
                    setsSpikes = true;
                    effects.Add(new EntryHazardEffect());
                    break;

                case "weather": // apply_condition(field:weather) (catalog §7.6)
                    CheckAllowedParams(e, "weather");
                    setsWeather = Parse<Weather>(Str(e, "weather"), "weather");
                    effects.Add(new SetWeatherEffect(setsWeather));
                    break;

                case "stealthRock": // apply_condition(side:entry_hazard_damage, type_scaled) (catalog §9.4)
                    setsStealthRock = true;
                    effects.Add(new StealthRockEffect());
                    break;

                case "bind": // apply_condition(volatile:partial_trap) (catalog §7.2)
                    binds = true;
                    effects.Add(new BindEffect());
                    break;

                case "protect": // apply_condition(volatile:protect_family) (catalog §7.2)
                    isProtect = true;
                    effects.Add(new ProtectEffect());
                    break;

                case "forceSwitch": // switch_flow(force_target_switch) (catalog §9.6)
                    forcesSwitch = true;
                    effects.Add(new ForceSwitchEffect());
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

                case "moveGate":
                    if (chance != 100)
                        throw new ArgumentException("moveGate does not support chance.");
                    CheckAllowedParams(e, "kind");
                    effects.Add(new MoveGateEffect(Parse<MoveGateKind>(Str(e, "kind"), "kind")));
                    break;

                case "queueActionGate":
                    if (chance != 100)
                        throw new ArgumentException("queueActionGate does not support chance.");
                    CheckAllowedParams(e, "turns");
                    int turns = e.Params?.ContainsKey("turns") == true ? Int(e, "turns") : 1;
                    if (turns <= 0)
                        throw new ArgumentException("queueActionGate turns must be positive.");
                    effects.Add(new QueueActionGateEffect(turns));
                    break;

                case "damageStatOverride": // damage stat query override
                    if (chance != 100)
                        throw new ArgumentException("damageStatOverride does not support chance.");
                    CheckAllowedParams(e, "offensiveStat", "defensiveStat");
                    offensiveStatOverride = OptionalStat(e, "offensiveStat", StatKind.Atk, StatKind.Def, StatKind.Spa, StatKind.Spd);
                    defensiveStatOverride = OptionalStat(e, "defensiveStat", StatKind.Def, StatKind.Spd);
                    if (offensiveStatOverride is null && defensiveStatOverride is null)
                        throw new ArgumentException("damageStatOverride requires offensiveStat or defensiveStat.");
                    break;

                case "targetHpThresholdPower": // base-power query modifier
                    if (chance != 100)
                        throw new ArgumentException("targetHpThresholdPower does not support chance.");
                    CheckAllowedParams(e, "thresholdNum", "thresholdDen", "multiplierNum", "multiplierDen");
                    targetHpThresholdPower = new TargetHpThresholdPower(
                        new Fraction(Int(e, "thresholdNum"), Int(e, "thresholdDen")),
                        new Fraction(Int(e, "multiplierNum"), Int(e, "multiplierDen")));
                    if (targetHpThresholdPower.Threshold.Num <= 0 || targetHpThresholdPower.Threshold.Den <= 0
                        || targetHpThresholdPower.Multiplier.Num <= 0 || targetHpThresholdPower.Multiplier.Den <= 0)
                    {
                        throw new ArgumentException("targetHpThresholdPower params must be positive.");
                    }
                    break;

                case "hpRatioPower": // base-power query modifier
                    if (chance != 100)
                        throw new ArgumentException("hpRatioPower does not support chance.");
                    CheckAllowedParams(e, "source");
                    hpRatioPower = new HpRatioPower(Parse<HpRatioPowerSource>(Str(e, "source"), "source"));
                    break;

                case "ailment":
                    string a = Str(e, "ailment", "status");
                    if (a.Equals("confusion", StringComparison.OrdinalIgnoreCase))
                    {
                        confuseChance = chance;
                        AddChanceEffect(effects, new ConfusionEffect(), chance);
                    }
                    else
                    {
                        ailment = Parse<PersistentStatus>(a, "ailment");
                        ailmentChance = chance;
                        AddChanceEffect(effects, new AilmentEffect(ailment.Value), chance);
                    }
                    break;

                case "statStage":
                    StatKind stat = ParseStat(Str(e, "stat"));
                    if (stat == StatKind.Hp)
                        throw new NotSupportedException("statStage op cannot target HP.");
                    int statDelta = Int(e, "delta");
                    bool onSelf = Bool(e, "onSelf") || move.Target == MoveTarget.User;
                    stageEffects.Add(new StageEffect(stat, statDelta, onSelf, chance));
                    AddChanceEffect(effects, new StatChangeEffect(stat, statDelta, onSelf), chance);
                    break;

                case "statStageAll":
                    CheckAllowedParams(e, "delta", "onSelf");
                    int delta = Int(e, "delta");
                    if (delta == 0 || delta is < -6 or > 6)
                        throw new ArgumentException("statStageAll delta must be nonzero and within -6..6.");
                    stageAllEffect = new StageAllEffect(delta, Bool(e, "onSelf") || move.Target == MoveTarget.User, chance);
                    AddChanceEffect(effects, new StatChangeAllEffect(stageAllEffect.Delta, stageAllEffect.OnSelf), chance);
                    break;

                case "hpCost":
                    if (chance != 100)
                        throw new ArgumentException("hpCost does not support chance.");
                    CheckAllowedParams(e, "num", "den", "allowFaint");
                    Fraction cost = ReadFraction(e, 1, 2);
                    if (cost.Num <= 0 || cost.Den <= 0)
                        throw new ArgumentException("hpCost num and den must be positive.");
                    effects.Add(new HpCostEffect(cost, Bool(e, "allowFaint")));
                    break;

                case "statStageReset":
                    CheckAllowedParams(e, "scope");
                    AddChanceEffect(effects, new StatResetEffect(Parse<StageEffectScope>(Str(e, "scope"), "scope")), chance);
                    break;

                case "statStageCopy":
                    CheckAllowedParams(e, "from", "to");
                    StageEffectScope from = ParseSingleScope(Str(e, "from"), "from");
                    StageEffectScope to = ParseSingleScope(Str(e, "to"), "to");
                    if (from == to)
                        throw new ArgumentException("statStageCopy from and to must differ.");
                    AddChanceEffect(effects, new StatCopyEffect(from, to), chance);
                    break;

                case "statStageSwap":
                    CheckAllowedParams(e, "group");
                    string group = e.Params?.ContainsKey("group") == true ? Str(e, "group") : "all";
                    AddChanceEffect(effects, new StatSwapEffect(Parse<StageSwapGroup>(group, "group")), chance);
                    break;

                case "statStageInvert":
                    CheckAllowedParams(e, "onSelf");
                    AddChanceEffect(effects, new StatInvertEffect(Bool(e, "onSelf") || move.Target == MoveTarget.User), chance);
                    break;

                case "flinch":
                    flinchChance = chance;
                    AddChanceEffect(effects, new FlinchEffect(), chance);
                    break;

                default:
                    throw new NotSupportedException($"Effect op '{e.Op}' needs Battle v5 controller support.");
            }
        }

        return new BattleMove(move.Id, move.Type, move.DamageClass, move.Power, move.Accuracy, move.Pp,
            move.Priority, move.CritStage, ailment, ailmentChance, stageEffects.FirstOrDefault(), confuseChance, flinchChance,
            drain, recoil, recoilOnMiss, heal, multiHitMin, multiHitMax,
            fixedDamage, fixedDamageLevel, ohko, critBoost, selfDestruct, leechSeed, setsSpikes, setsWeather,
            setsStealthRock, binds, isProtect, forcesSwitch, counterCategory, bypassAccuracy, chargeTurn,
            multiTurnLock, move.MakesContact, stageEffects: stageEffects, target: move.Target,
            stageAllEffect: stageAllEffect, secondaryEffects: effects,
            offensiveStatOverride: offensiveStatOverride, defensiveStatOverride: defensiveStatOverride,
            targetHpThresholdPower: targetHpThresholdPower, hpRatioPower: hpRatioPower);
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

    private static string Str(Effect e, string key, string alias) =>
        e.Params is not null && e.Params.ContainsKey(key) ? Str(e, key) : Str(e, alias);

    private static int Int(Effect e, string key) => Field(e, key).GetInt32();

    private static bool Bool(Effect e, string key) =>
        e.Params is not null && e.Params.TryGetValue(key, out JsonElement v) && v.GetBoolean();

    private static void AddChanceEffect(List<MoveEffect> effects, MoveEffect effect, int chance)
    {
        if (chance > 0)
            effects.Add(effect with { Chance = chance });
    }

    private static void CheckAllowedParams(Effect e, params string[] allowed)
    {
        if (e.Params is null)
            return;
        foreach (string key in e.Params.Keys)
            if (!allowed.Contains(key))
                throw new ArgumentException($"Effect op '{e.Op}' has unknown param '{key}'.");
    }

    private static T Parse<T>(string value, string what) where T : struct, Enum =>
        Enum.TryParse(value, ignoreCase: true, out T result)
            ? result
            : throw new ArgumentException($"Unknown {what} '{value}'.");

    private static StageEffectScope ParseSingleScope(string value, string what)
    {
        StageEffectScope scope = Parse<StageEffectScope>(value, what);
        return scope == StageEffectScope.Both
            ? throw new ArgumentException($"statStageCopy {what} cannot be both.")
            : scope;
    }

    private static StatKind? OptionalStat(Effect e, string key, params StatKind[] allowed)
    {
        if (e.Params is null || !e.Params.ContainsKey(key))
            return null;
        StatKind stat = ParseStat(Str(e, key));
        return allowed.Contains(stat)
            ? stat
            : throw new ArgumentException($"damageStatOverride {key} cannot be {stat}.");
    }

    private static StatKind ParseStat(string value) => value.ToLowerInvariant() switch
    {
        "acc" or "accuracy" => StatKind.Accuracy,
        "eva" or "evasion" => StatKind.Evasion,
        _ => Parse<StatKind>(value, "stat"),
    };
}
