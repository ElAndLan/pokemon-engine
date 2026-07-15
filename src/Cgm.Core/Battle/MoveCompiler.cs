using System.Globalization;
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
        HpBandPower? hpBandPower = null;

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
                    CheckAllowedParams(e, "num", "den", "recipient", "weather");
                    Fraction healFraction = ReadFraction(e, 1, 2);
                    HpFractionRecipient healRecipient = e.Params?.ContainsKey("recipient") == true
                        ? Parse<HpFractionRecipient>(Str(e, "recipient"), "recipient")
                        : HpFractionRecipient.Self;
                    if (healRecipient == HpFractionRecipient.Self)
                        heal = healFraction;
                    effects.Add(new HealEffect(healFraction, healRecipient, ParseWeatherFractions(e)));
                    break;

                case "hpFraction":
                    if (chance != 100)
                        throw new ArgumentException("hpFraction does not support chance.");
                    CheckAllowedParams(e, "recipient", "operation", "basis", "num", "den");
                    Fraction hpFraction = new(Int(e, "num"), Int(e, "den"));
                    if (hpFraction.Num <= 0 || hpFraction.Den <= 0)
                        throw new ArgumentException("hpFraction num and den must be positive.");
                    effects.Add(new HpFractionEffect(
                        Parse<HpFractionRecipient>(Str(e, "recipient"), "recipient"),
                        Parse<HpFractionOperation>(Str(e, "operation"), "operation"),
                        Parse<HpFractionBasis>(Str(e, "basis"), "basis"),
                        hpFraction));
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

                case "positionSwap":
                    if (chance != 100)
                        throw new ArgumentException("positionSwap does not support chance.");
                    CheckAllowedParams(e);
                    if (move.Target != MoveTarget.Ally)
                        throw new ArgumentException("positionSwap requires the ally target.");
                    effects.Add(new PositionSwapEffect());
                    break;

                case "redirect":
                    if (chance != 100)
                        throw new ArgumentException("redirect does not support chance.");
                    CheckAllowedParams(e, "priority", "classes", "bypassClasses", "tags", "bypassTags");
                    effects.Add(new RedirectEffect(
                        e.Params?.ContainsKey("priority") == true ? Int(e, "priority") : 0,
                        ParseClasses(Str(e, "classes")),
                        e.Params?.ContainsKey("bypassClasses") == true ? ParseClasses(Str(e, "bypassClasses")) : new HashSet<DamageClass>(),
                        e.Params?.ContainsKey("tags") == true ? ParseRedirectTags(Str(e, "tags")) : new HashSet<string>(),
                        e.Params?.ContainsKey("bypassTags") == true ? ParseRedirectTags(Str(e, "bypassTags")) : new HashSet<string>()));
                    break;

                case "counterDamage": // deal_damage(counter_received_damage) (catalog §9.2)
                    counterCategory = Parse<DamageClass>(Str(e, "category"), "category");
                    break;

                case "accuracyBypass": // sure-hit (catalog §3.3 accuracy_check bypass)
                    bypassAccuracy = true;
                    break;

                case "weatherAccuracy":
                    if (chance != 100)
                        throw new ArgumentException("weatherAccuracy does not support chance.");
                    if (move.Accuracy is null || move.DamageClass == DamageClass.Status || ohko)
                        throw new ArgumentException("weatherAccuracy requires a non-OHKO damaging move with authored accuracy.");
                    CheckAllowedParams(e, "bypass", "overrides");
                    IReadOnlySet<Weather> bypassWeather = ParseWeatherList(e, "bypass");
                    IReadOnlyDictionary<Weather, int> weatherOverrides = ParseWeatherAccuracyOverrides(e);
                    if (bypassWeather.Count == 0 && weatherOverrides.Count == 0)
                        throw new ArgumentException("weatherAccuracy requires bypass or overrides.");
                    if (bypassWeather.Overlaps(weatherOverrides.Keys))
                        throw new ArgumentException("weatherAccuracy cannot bypass and override the same weather.");
                    effects.Add(new WeatherAccuracyEffect(bypassWeather, weatherOverrides));
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
                    if (targetHpThresholdPower is not null)
                        throw new ArgumentException("A move can declare only one targetHpThresholdPower effect.");
                    if (chance != 100)
                        throw new ArgumentException("targetHpThresholdPower does not support chance.");
                    CheckAllowedParams(e, "thresholdNum", "thresholdDen", "multiplierNum", "multiplierDen", "inclusive");
                    targetHpThresholdPower = new TargetHpThresholdPower(
                        new Fraction(Int(e, "thresholdNum"), Int(e, "thresholdDen")),
                        new Fraction(Int(e, "multiplierNum"), Int(e, "multiplierDen")),
                        !e.Params!.ContainsKey("inclusive") || Bool(e, "inclusive"));
                    if (targetHpThresholdPower.Threshold.Num <= 0 || targetHpThresholdPower.Threshold.Den <= 0
                        || targetHpThresholdPower.Threshold.Num > targetHpThresholdPower.Threshold.Den
                        || targetHpThresholdPower.Multiplier.Num <= 0 || targetHpThresholdPower.Multiplier.Den <= 0)
                    {
                        throw new ArgumentException("targetHpThresholdPower params must be positive.");
                    }
                    break;

                case "hpRatioPower": // base-power query modifier
                    if (hpRatioPower is not null)
                        throw new ArgumentException("A move can declare only one hpRatioPower effect.");
                    if (chance != 100)
                        throw new ArgumentException("hpRatioPower does not support chance.");
                    CheckAllowedParams(e, "source", "basis", "scale", "offset");
                    HpRatioPowerBasis ratioBasis = e.Params?.ContainsKey("basis") == true
                        ? Parse<HpRatioPowerBasis>(Str(e, "basis"), "basis")
                        : HpRatioPowerBasis.Current;
                    int? scale = e.Params?.ContainsKey("scale") == true ? Int(e, "scale") : null;
                    int offset = e.Params?.ContainsKey("offset") == true ? Int(e, "offset") : 0;
                    if (scale is < 0 || offset < 0 || scale == 0 && offset == 0 || scale is null && offset != 0)
                        throw new ArgumentException("hpRatioPower scale/offset must be nonnegative and not both zero.");
                    hpRatioPower = new HpRatioPower(Parse<HpRatioPowerSource>(Str(e, "source"), "source"), ratioBasis, scale, offset);
                    break;

                case "hpBandPower":
                    if (hpBandPower is not null)
                        throw new ArgumentException("A move can declare only one hpBandPower effect.");
                    if (chance != 100)
                        throw new ArgumentException("hpBandPower does not support chance.");
                    CheckAllowedParams(e, "source", "scale", "bands");
                    int bandScale = Int(e, "scale");
                    IReadOnlyList<HpPowerBand> bands = ParseHpBands(Str(e, "bands"));
                    if (bandScale <= 0 || bands[^1].UpperInclusive < bandScale)
                        throw new ArgumentException("hpBandPower requires a positive scale covered by its final band.");
                    hpBandPower = new HpBandPower(Parse<HpRatioPowerSource>(Str(e, "source"), "source"), bandScale, bands);
                    break;

                case "statusPower": // base-power query modifier
                    if (chance != 100)
                        throw new ArgumentException("statusPower does not support chance.");
                    CheckAllowedParams(e, "subject", "status", "volatile", "multiplierNum", "multiplierDen", "ignoreSourceBurnPenalty");
                    if (effects.OfType<StatusPowerEffect>().Any())
                        throw new ArgumentException("A move can declare only one statusPower effect.");
                    (PersistentStatus? status, BattleVolatileStatus? volatileStatus) = ParseStatusPredicate(e);
                    Fraction statusMultiplier = new(Int(e, "multiplierNum"), Int(e, "multiplierDen"));
                    if (statusMultiplier.Num <= 0 || statusMultiplier.Den <= 0)
                        throw new ArgumentException("statusPower multiplier params must be positive.");
                    StatusPowerSubject statusSubject = Parse<StatusPowerSubject>(Str(e, "subject"), "subject");
                    bool ignoreBurn = Bool(e, "ignoreSourceBurnPenalty");
                    if (ignoreBurn && (statusSubject != StatusPowerSubject.User || volatileStatus is not null))
                        throw new ArgumentException("statusPower burn-penalty bypass requires a persistent user-status predicate.");
                    effects.Add(new StatusPowerEffect(
                        statusSubject,
                        status,
                        statusMultiplier,
                        ignoreBurn,
                        volatileStatus));
                    break;

                case "statusCountPower":
                    if (chance != 100)
                        throw new ArgumentException("statusCountPower does not support chance.");
                    CheckAllowedParams(e, "subject", "statuses", "volatiles", "base", "perStatus");
                    int countBase = Int(e, "base"), perStatus = Int(e, "perStatus");
                    if (countBase < 0 || perStatus < 0 || countBase == 0 && perStatus == 0)
                        throw new ArgumentException("statusCountPower base/perStatus must be nonnegative and not both zero.");
                    IReadOnlyList<PersistentStatus> statuses = ParseList<PersistentStatus>(e, "statuses");
                    IReadOnlyList<BattleVolatileStatus> volatiles = ParseList<BattleVolatileStatus>(e, "volatiles");
                    if (statuses.Count + volatiles.Count == 0)
                        throw new ArgumentException("statusCountPower requires at least one status predicate.");
                    effects.Add(new StatusCountPowerEffect(Parse<StatusCountSubject>(Str(e, "subject"), "subject"),
                        statuses, volatiles, countBase, perStatus));
                    break;

                case "statusChance":
                    if (chance != 100)
                        throw new ArgumentException("statusChance does not support chance.");
                    CheckAllowedParams(e, "subject", "status", "volatile", "num", "den");
                    (PersistentStatus? chanceStatus, BattleVolatileStatus? chanceVolatile) = ParseStatusPredicate(e);
                    Fraction chanceMultiplier = new(Int(e, "num"), Int(e, "den"));
                    if (chanceMultiplier.Num <= 0 || chanceMultiplier.Den <= 0)
                        throw new ArgumentException("statusChance multiplier must be positive.");
                    effects.Add(new StatusChanceEffect(new StatusChanceFormula(
                        Parse<StatusPowerSubject>(Str(e, "subject"), "subject"), chanceStatus, chanceVolatile, chanceMultiplier)));
                    break;

                case "hpEqualize":
                    if (effects.OfType<HpEqualizeEffect>().Any())
                        throw new ArgumentException("A move can declare only one hpEqualize effect.");
                    if (move.Target != MoveTarget.Selected)
                        throw new ArgumentException("hpEqualize requires the selected target.");
                    if (chance != 100)
                        throw new ArgumentException("hpEqualize does not support chance.");
                    CheckAllowedParams(e, "mode");
                    effects.Add(new HpEqualizeEffect(Parse<HpEqualizeMode>(Str(e, "mode"), "mode")));
                    break;

                case "cannotKo":
                    if (effects.OfType<CannotKoEffect>().Any())
                        throw new ArgumentException("A move can declare only one cannotKo effect.");
                    if (move.DamageClass == DamageClass.Status)
                        throw new ArgumentException("cannotKo requires a damaging move.");
                    if (chance != 100)
                        throw new ArgumentException("cannotKo does not support chance.");
                    CheckAllowedParams(e, "floor");
                    int hpFloor = e.Params?.ContainsKey("floor") == true ? Int(e, "floor") : 1;
                    if (hpFloor < 1)
                        throw new ArgumentException("cannotKo floor must be positive.");
                    effects.Add(new CannotKoEffect(hpFloor));
                    break;

                case "speedRatioPower":
                    if (chance != 100)
                        throw new ArgumentException("speedRatioPower does not support chance.");
                    CheckAllowedParams(e, "numerator", "denominator", "scale", "offset", "cap", "bands");
                    HpRatioPowerSource speedNumerator = Parse<HpRatioPowerSource>(Str(e, "numerator"), "numerator");
                    HpRatioPowerSource speedDenominator = Parse<HpRatioPowerSource>(Str(e, "denominator"), "denominator");
                    if (speedNumerator == speedDenominator)
                        throw new ArgumentException("speedRatioPower numerator and denominator must differ.");
                    bool hasScale = e.Params?.ContainsKey("scale") == true;
                    bool hasBands = e.Params?.ContainsKey("bands") == true;
                    if (hasScale == hasBands)
                        throw new ArgumentException("speedRatioPower requires exactly one of scale or bands.");
                    int? speedScale = hasScale ? Int(e, "scale") : null;
                    int speedOffset = e.Params?.ContainsKey("offset") == true ? Int(e, "offset") : 0;
                    int? speedCap = e.Params?.ContainsKey("cap") == true ? Int(e, "cap") : null;
                    if (speedScale is <= 0 || speedOffset < 0 || speedCap is <= 0
                        || hasBands && (e.Params!.ContainsKey("offset") || e.Params.ContainsKey("cap")))
                        throw new ArgumentException("speedRatioPower scale/cap must be positive and offset nonnegative; bands cannot compose them.");
                    effects.Add(new SpeedRatioPowerEffect(speedNumerator, speedDenominator, speedScale, speedOffset,
                        speedCap, hasBands ? ParseFormulaBands(Str(e, "bands")) : []));
                    break;

                case "metricBandPower":
                    if (chance != 100)
                        throw new ArgumentException("metricBandPower does not support chance.");
                    CheckAllowedParams(e, "metric", "subject", "bands");
                    effects.Add(new MetricBandPowerEffect(Parse<BattleMetric>(Str(e, "metric"), "metric"),
                        Parse<HpRatioPowerSource>(Str(e, "subject"), "subject"), ParseFormulaBands(Str(e, "bands"))));
                    break;

                case "metricRatioPower":
                    if (chance != 100)
                        throw new ArgumentException("metricRatioPower does not support chance.");
                    CheckAllowedParams(e, "metric", "numerator", "denominator", "bands");
                    HpRatioPowerSource metricNumerator = Parse<HpRatioPowerSource>(Str(e, "numerator"), "numerator");
                    HpRatioPowerSource metricDenominator = Parse<HpRatioPowerSource>(Str(e, "denominator"), "denominator");
                    if (metricNumerator == metricDenominator)
                        throw new ArgumentException("metricRatioPower numerator and denominator must differ.");
                    effects.Add(new MetricRatioPowerEffect(Parse<BattleMetric>(Str(e, "metric"), "metric"),
                        metricNumerator, metricDenominator, ParseFormulaBands(Str(e, "bands"))));
                    break;

                case "consecutivePower":
                    if (chance != 100)
                        throw new ArgumentException("consecutivePower does not support chance.");
                    CheckAllowedParams(e, "scope", "mode", "step", "cap");
                    if (move.DamageClass == DamageClass.Status || move.Power is not > 0)
                        throw new ArgumentException("consecutivePower requires authored power on a damaging move.");
                    int consecutiveStep = Int(e, "step");
                    int consecutiveCap = Int(e, "cap");
                    if (consecutiveStep <= 0 || consecutiveCap < move.Power)
                        throw new ArgumentException("consecutivePower step must be positive and cap cannot be below authored power.");
                    effects.Add(new ConsecutivePowerEffect(
                        Parse<ConsecutivePowerScope>(Str(e, "scope"), "scope"),
                        Parse<ConsecutivePowerMode>(Str(e, "mode"), "mode"),
                        consecutiveStep, consecutiveCap));
                    break;

                case "historyPower":
                    if (chance != 100)
                        throw new ArgumentException("historyPower does not support chance.");
                    CheckAllowedParams(e, "condition", "multiplierNum", "multiplierDen");
                    if (move.DamageClass == DamageClass.Status || move.Power is not > 0)
                        throw new ArgumentException("historyPower requires authored power on a damaging move.");
                    var historyMultiplier = new BattleQueryValue(
                        Int(e, "multiplierNum"), Int(e, "multiplierDen"));
                    if (historyMultiplier.Numerator <= 0)
                        throw new ArgumentException("historyPower multiplier must be positive.");
                    effects.Add(new HistoryPowerEffect(
                        Parse<HistoryPowerCondition>(Str(e, "condition"), "condition"),
                        new Fraction(checked((int)historyMultiplier.Numerator),
                            checked((int)historyMultiplier.Denominator))));
                    break;

                case "partyCountPower":
                    RequireReplacementPower(move, e, "filter", "base", "perMember", "cap");
                    int partyBase = Int(e, "base"), perMember = Int(e, "perMember");
                    int? partyCap = e.Params?.ContainsKey("cap") == true ? Int(e, "cap") : null;
                    ValidateLinearPower("partyCountPower", partyBase, perMember, partyCap);
                    effects.Add(new PartyCountPowerEffect(
                        Parse<PartyMemberFilter>(Str(e, "filter"), "party filter"), partyBase, perMember, partyCap));
                    break;

                case "friendshipPower":
                    RequireReplacementPower(move, e, "mode");
                    effects.Add(new FriendshipPowerEffect(
                        Parse<FriendshipPowerMode>(Str(e, "mode"), "friendship mode")));
                    break;

                case "ppPower":
                    RequireReplacementPower(move, e, "timing", "bands");
                    IReadOnlyList<FormulaPowerBand> ppBands = ParseFormulaBands(Str(e, "bands"));
                    if (ppBands.Any(band => band.MinInclusive > move.Pp))
                        throw new ArgumentException("ppPower band minima cannot exceed the move's maximum PP.");
                    effects.Add(new PpPowerEffect(Parse<PpPowerTiming>(Str(e, "timing"), "PP timing"), ppBands));
                    break;

                case "positiveStagePower":
                    RequireReplacementPower(move, e, "subject", "base", "perStage", "cap");
                    int stageBase = Int(e, "base"), perStage = Int(e, "perStage");
                    int? stageCap = e.Params?.ContainsKey("cap") == true ? Int(e, "cap") : null;
                    ValidateLinearPower("positiveStagePower", stageBase, perStage, stageCap);
                    effects.Add(new PositiveStagePowerEffect(
                        Parse<StatusPowerSubject>(Str(e, "subject"), "stage subject"),
                        stageBase, perStage, stageCap));
                    break;

                case "itemDataPower":
                    RequireReplacementPower(move, e, "field");
                    effects.Add(new ItemDataPowerEffect(Parse<ItemPowerField>(Str(e, "field"), "item power field")));
                    break;

                case "randomTablePower":
                    RequireReplacementPower(move, e, "entries");
                    IReadOnlyList<WeightedPowerEntry> entries = ParseWeightedPowerEntries(Str(e, "entries"));
                    _ = PartyResourceFormulas.ExpectedWeightedPower(entries);
                    effects.Add(new RandomTablePowerEffect(entries));
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

        effects = BindStatusChance(effects);
        int replacementPowerFormulas = (hpRatioPower?.Scale is not null ? 1 : 0) + (hpBandPower is not null ? 1 : 0)
            + effects.Count(effect => effect is StatusCountPowerEffect or SpeedRatioPowerEffect
                or MetricBandPowerEffect or MetricRatioPowerEffect or ConsecutivePowerEffect
                or PartyCountPowerEffect or FriendshipPowerEffect or PpPowerEffect
                or PositiveStagePowerEffect or ItemDataPowerEffect or RandomTablePowerEffect);
        if (replacementPowerFormulas > 1)
            throw new ArgumentException("A move can declare only one replacement base-power formula.");
        if (effects.OfType<HistoryPowerEffect>().Count() > 1)
            throw new ArgumentException("A move can declare only one historyPower condition.");
        if (effects.OfType<WeatherAccuracyEffect>().Count() > 1
            || (bypassAccuracy || ohko) && effects.OfType<WeatherAccuracyEffect>().Any())
            throw new ArgumentException("A move can declare one weatherAccuracy op and cannot combine it with accuracyBypass or ohko.");
        if (move.DamageClass != DamageClass.Status && move.Power is null && replacementPowerFormulas == 0
            && fixedDamage is null && !fixedDamageLevel && !ohko && counterCategory is null
            && !effects.OfType<HpFractionEffect>().Any(effect => effect.Operation == HpFractionOperation.Damage)
            && !effects.OfType<HpEqualizeEffect>().Any(effect => effect.Mode == HpEqualizeMode.MatchSource))
            throw new ArgumentException("Damaging moves without authored power require a replacement base-power formula.");

        return new BattleMove(move.Id, move.Type, move.DamageClass, move.Power, move.Accuracy, move.Pp,
            move.Priority, move.CritStage, ailment, ailmentChance, stageEffects.FirstOrDefault(), confuseChance, flinchChance,
            drain, recoil, recoilOnMiss, heal, multiHitMin, multiHitMax,
            fixedDamage, fixedDamageLevel, ohko, critBoost, selfDestruct, leechSeed, setsSpikes, setsWeather,
            setsStealthRock, binds, isProtect, forcesSwitch, counterCategory, bypassAccuracy, chargeTurn,
            multiTurnLock, move.MakesContact, stageEffects: stageEffects, target: move.Target,
            stageAllEffect: stageAllEffect, secondaryEffects: effects,
            offensiveStatOverride: offensiveStatOverride, defensiveStatOverride: defensiveStatOverride,
            targetHpThresholdPower: targetHpThresholdPower, hpRatioPower: hpRatioPower, hpBandPower: hpBandPower);
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

    private static IReadOnlyList<HpPowerBand> ParseHpBands(string value)
    {
        HpPowerBand[] bands = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split(':', StringSplitOptions.TrimEntries))
            .Select(parts => parts.Length == 2
                && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int upper)
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int power)
                ? new HpPowerBand(upper, power)
                : throw new ArgumentException("hpBandPower bands must use comma-separated upper:power integers."))
            .ToArray();
        if (bands.Length == 0 || bands.Any(band => band.UpperInclusive < 0 || band.Power <= 0)
            || bands.Zip(bands.Skip(1)).Any(pair => pair.First.UpperInclusive >= pair.Second.UpperInclusive))
            throw new ArgumentException("hpBandPower bands require increasing nonnegative bounds and positive powers.");
        return bands;
    }

    private static IReadOnlyList<FormulaPowerBand> ParseFormulaBands(string value)
    {
        FormulaPowerBand[] bands = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split(':', StringSplitOptions.TrimEntries))
            .Select(parts => parts.Length == 2
                && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int minimum)
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int power)
                ? new FormulaPowerBand(minimum, power)
                : throw new ArgumentException("Formula bands must use comma-separated minimum:power integers."))
            .ToArray();
        if (bands.Length == 0 || bands[0].MinInclusive != 0
            || bands.Any(band => band.MinInclusive < 0 || band.Power <= 0)
            || bands.Zip(bands.Skip(1)).Any(pair => pair.First.MinInclusive >= pair.Second.MinInclusive))
            throw new ArgumentException("Formula bands require a zero first bound, increasing nonnegative minima, and positive powers.");
        return bands;
    }

    private static IReadOnlyList<WeightedPowerEntry> ParseWeightedPowerEntries(string value)
    {
        WeightedPowerEntry[] entries = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split(':', StringSplitOptions.TrimEntries))
            .Select(parts => parts.Length == 2
                && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int weight)
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int power)
                ? new WeightedPowerEntry(weight, power)
                : throw new ArgumentException("randomTablePower entries must use comma-separated weight:power integers."))
            .ToArray();
        if (entries.Length == 0 || entries.Any(entry => entry.Weight < 0 || entry.Power <= 0)
            || entries.All(entry => entry.Weight == 0))
            throw new ArgumentException("randomTablePower requires nonnegative weights, positive powers, and a positive total weight.");
        return entries;
    }

    private static void RequireReplacementPower(Move move, Effect effect, params string[] parameters)
    {
        if ((effect.Chance ?? 100) != 100)
            throw new ArgumentException($"{effect.Op} does not support chance.");
        if (move.DamageClass == DamageClass.Status)
            throw new ArgumentException($"{effect.Op} requires a damaging move.");
        CheckAllowedParams(effect, parameters);
    }

    private static void ValidateLinearPower(string op, int basePower, int perUnit, int? cap)
    {
        if (basePower < 0 || perUnit < 0 || basePower == 0 && perUnit == 0
            || cap is <= 0 || cap is { } maximum && maximum < basePower)
            throw new ArgumentException($"{op} requires nonnegative nonzero terms and a positive cap not below base.");
    }

    private static (PersistentStatus? Status, BattleVolatileStatus? Volatile) ParseStatusPredicate(Effect effect)
    {
        bool hasStatus = effect.Params?.ContainsKey("status") == true;
        bool hasVolatile = effect.Params?.ContainsKey("volatile") == true;
        if (hasStatus == hasVolatile)
            throw new ArgumentException($"Effect op '{effect.Op}' requires exactly one of status or volatile.");
        if (hasVolatile)
            return (null, Parse<BattleVolatileStatus>(Str(effect, "volatile"), "volatile"));
        string value = Str(effect, "status");
        return (value.Equals("any", StringComparison.OrdinalIgnoreCase) ? null : Parse<PersistentStatus>(value, "status"), null);
    }

    private static IReadOnlyList<T> ParseList<T>(Effect effect, string key) where T : struct, Enum
    {
        if (effect.Params?.ContainsKey(key) != true)
            return [];
        T[] values = Str(effect, key).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Parse<T>(value, key)).ToArray();
        if (values.Distinct().Count() != values.Length)
            throw new ArgumentException($"Effect op '{effect.Op}' {key} must not contain duplicates.");
        return values;
    }

    private static List<MoveEffect> BindStatusChance(List<MoveEffect> effects)
    {
        for (int index = 0; index < effects.Count; index++)
        {
            if (effects[index] is not StatusChanceEffect modifier)
                continue;
            if (index + 1 >= effects.Count || !IsChanceCapable(effects[index + 1]))
                throw new ArgumentException("statusChance must immediately precede one chance-capable secondary effect.");
            effects[index + 1] = effects[index + 1] with { ChanceFormula = modifier.Formula };
            effects.RemoveAt(index--);
        }
        return effects;
    }

    private static bool IsChanceCapable(MoveEffect effect) => effect is AilmentEffect or ConfusionEffect
        or FlinchEffect or StatChangeEffect or StatChangeAllEffect or StatResetEffect or StatCopyEffect
        or StatSwapEffect or StatInvertEffect;

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

    private static IReadOnlySet<DamageClass> ParseClasses(string value)
    {
        DamageClass[] parsed = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => Parse<DamageClass>(part, "classes"))
            .ToArray();
        if (parsed.Distinct().Count() != parsed.Length)
            throw new ArgumentException("redirect classes must not contain duplicates.");
        HashSet<DamageClass> classes = [.. parsed];
        return classes.Count > 0 ? classes : throw new ArgumentException("redirect classes must not be empty.");
    }

    private static IReadOnlySet<Weather> ParseWeatherList(Effect effect, string key)
    {
        if (effect.Params?.ContainsKey(key) != true)
            return new HashSet<Weather>();
        Weather[] values = Str(effect, key)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => ParseWeather(value, key))
            .ToArray();
        if (values.Length == 0 || values.Contains(Weather.None) || values.Distinct().Count() != values.Length)
            throw new ArgumentException($"weatherAccuracy {key} must contain unique active weather values.");
        return new HashSet<Weather>(values);
    }

    private static IReadOnlyDictionary<Weather, int> ParseWeatherAccuracyOverrides(Effect effect)
    {
        if (effect.Params?.ContainsKey("overrides") != true)
            return new Dictionary<Weather, int>();
        var overrides = new Dictionary<Weather, int>();
        foreach (string entry in Str(effect, "overrides")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = entry.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 2
                || !TryParseWeather(parts[0], out Weather weather)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int accuracy)
                || accuracy is < 1 or > 100
                || !overrides.TryAdd(weather, accuracy))
                throw new ArgumentException("weatherAccuracy overrides require unique weather:accuracy rows in the 1..100 range.");
        }
        if (overrides.Count == 0)
            throw new ArgumentException("weatherAccuracy overrides cannot be empty.");
        return overrides;
    }

    private static IReadOnlyDictionary<Weather, Fraction>? ParseWeatherFractions(Effect effect)
    {
        if (effect.Params?.ContainsKey("weather") != true)
            return null;
        var fractions = new Dictionary<Weather, Fraction>();
        foreach (string entry in Str(effect, "weather")
            .Split(',', StringSplitOptions.TrimEntries))
        {
            string[] row = entry.Split(':', StringSplitOptions.TrimEntries);
            string[] ratio = row.Length == 2
                ? row[1].Split('/', StringSplitOptions.TrimEntries)
                : [];
            if (row.Length != 2 || ratio.Length != 2
                || !TryParseWeather(row[0], out Weather weather)
                || !int.TryParse(ratio[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int numerator)
                || !int.TryParse(ratio[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int denominator)
                || numerator <= 0 || denominator <= 0 || numerator > denominator
                || !fractions.TryAdd(weather, new Fraction(numerator, denominator)))
                throw new ArgumentException("heal weather requires unique active weather:num/den rows with fractions in (0,1].");
        }
        return fractions.Count > 0
            ? fractions
            : throw new ArgumentException("heal weather cannot be empty.");
    }

    private static Weather ParseWeather(string value, string what) => TryParseWeather(value, out Weather weather)
        ? weather
        : throw new ArgumentException($"Unknown {what} '{value}'.");

    private static bool TryParseWeather(string value, out Weather weather)
    {
        weather = Weather.None;
        return !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            && Enum.TryParse(value, true, out weather)
            && weather != Weather.None
            && Enum.IsDefined(weather);
    }

    private static IReadOnlySet<string> ParseRedirectTags(string value)
    {
        string[] tags = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(tag => tag.ToLowerInvariant())
            .ToArray();
        if (tags.Length == 0 || tags.Any(tag => tag is not ("damaging" or "status" or "contact")))
            throw new ArgumentException("redirect tags must be damaging, status, or contact.");
        if (tags.Distinct(StringComparer.Ordinal).Count() != tags.Length)
            throw new ArgumentException("redirect tags must not contain duplicates.");
        return new HashSet<string>(tags, StringComparer.Ordinal);
    }
}
