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
        bool fixedDamageLevel = false, ohko = false, selfDestruct = false, leechSeed = false;
        bool binds = false, forcesSwitch = false, bypassAccuracy = false;
        ChargeMoveEffect? charge = null;
        MultiTurnLockProfile? multiTurnLock = null;
        int critBoost = 0;
        Weather setsWeather = Weather.None;
        DamageClass? counterCategory = null;
        StatKind? offensiveStatOverride = null, defensiveStatOverride = null;
        TargetHpThresholdPower? targetHpThresholdPower = null;
        HpRatioPower? hpRatioPower = null;
        HpBandPower? hpBandPower = null;
        var moveTags = new HashSet<string>(StringComparer.Ordinal);

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

                case "moveTags":
                    if (chance != 100)
                        throw new ArgumentException("moveTags does not support chance.");
                    CheckAllowedParams(e, "tags");
                    foreach (string tag in Str(e, "tags").Split(',', StringSplitOptions.RemoveEmptyEntries
                        | StringSplitOptions.TrimEntries))
                    {
                        if (!BattleConditionId.ValidToken(tag) || !moveTags.Add(tag))
                            throw new ArgumentException("moveTags requires unique lowercase comma-separated tokens.");
                    }
                    if (moveTags.Count == 0)
                        throw new ArgumentException("moveTags requires at least one tag.");
                    break;

                case "actionFilter":
                    if (chance != 100)
                        throw new ArgumentException("actionFilter does not support chance.");
                    CheckAllowedParams(e, "filter", "owner", "duration", "tag");
                    ActionFilterKind filter = ParseNamed<ActionFilterKind>(Str(e, "filter"), "action filter");
                    SideConditionTarget filterOwner = e.Params?.ContainsKey("owner") == true
                        ? ParseNamed<SideConditionTarget>(Str(e, "owner"), "action filter owner")
                        : filter == ActionFilterKind.BlockKnownBySource ? SideConditionTarget.Source : SideConditionTarget.Target;
                    string? filterTag = e.Params?.ContainsKey("tag") == true ? Str(e, "tag") : null;
                    BattleConditionDefinition filterDefinition = ActionFilterConditions.For(filter, filterTag);
                    int? filterDuration = e.Params?.ContainsKey("duration") == true ? Int(e, "duration")
                        : filterDefinition.DefaultDuration;
                    if (filterOwner == SideConditionTarget.Target && !IsActiveCreatureTarget(move.Target)
                        || filter == ActionFilterKind.BlockKnownBySource
                            && filterOwner != SideConditionTarget.Source)
                        throw new ArgumentException("actionFilter owner is incompatible with the move target or filter.");
                    if ((filterDuration is not null) != (filterDefinition.DurationCheckpoint is not null)
                        || filterDuration is <= 0)
                        throw new ArgumentException("actionFilter duration does not match its registry row.");
                    effects.Add(new ApplyActionFilterEffect(filter, filterOwner, filterDuration, filterTag));
                    break;

                case "callMove":
                    if (chance != 100)
                        throw new ArgumentException("callMove does not support chance.");
                    CheckAllowedParams(e, "selector", "ppOwner", "pool", "environment", "excludeTags");
                    if (move.DamageClass != DamageClass.Status)
                        throw new ArgumentException("callMove requires a status move.");
                    MoveReferenceSelector selector = ParseNamed<MoveReferenceSelector>(
                        Str(e, "selector"), "move-reference selector");
                    CalledMovePpOwner ppOwner = e.Params?.ContainsKey("ppOwner") == true
                        ? ParseNamed<CalledMovePpOwner>(Str(e, "ppOwner"), "called-move PP owner")
                        : CalledMovePpOwner.Caller;
                    IReadOnlyList<EntityId> pool = e.Params?.ContainsKey("pool") == true
                        ? ParseMoveIds(Str(e, "pool"), "callMove pool") : [];
                    IReadOnlyDictionary<BattleEnvironment, EntityId> environment =
                        e.Params?.ContainsKey("environment") == true
                            ? ParseEnvironmentMoves(Str(e, "environment"))
                            : new Dictionary<BattleEnvironment, EntityId>();
                    IReadOnlySet<string> excluded = e.Params?.ContainsKey("excludeTags") == true
                        ? ParseLowercaseTokens(Str(e, "excludeTags"), "callMove excludeTags")
                        : MoveReferenceResolver.DefaultExcludedTags;
                    if ((selector == MoveReferenceSelector.AuthoredPool) != (pool.Count > 0)
                        || (selector == MoveReferenceSelector.EnvironmentPool) != (environment.Count > 0))
                        throw new ArgumentException("callMove pool parameters must match their selector.");
                    if (selector == MoveReferenceSelector.ExplicitReference
                        && move.Target != MoveTarget.SpecificMove)
                        throw new ArgumentException("Explicit callMove requires the specific-move target.");
                    if (selector is MoveReferenceSelector.TargetKnown or MoveReferenceSelector.TargetLastUsed
                        && !IsActiveCreatureTarget(move.Target))
                        throw new ArgumentException("Target move selectors require an active-creature target.");
                    if (effects.OfType<CallMoveEffect>().Any())
                        throw new ArgumentException("A move can declare only one callMove effect.");
                    effects.Add(new CallMoveEffect(new CallMoveProfile(selector, ppOwner, pool,
                        environment, excluded)));
                    break;

                case "turnOrderIntent":
                    if (chance != 100)
                        throw new ArgumentException("turnOrderIntent does not support chance.");
                    CheckAllowedParams(e, "kind", "num", "den");
                    if (move.DamageClass != DamageClass.Status || !IsSingleActiveCreatureTarget(move.Target))
                        throw new ArgumentException("turnOrderIntent requires a status move with one active-creature target.");
                    TurnOrderIntentKind orderKind = ParseNamed<TurnOrderIntentKind>(
                        Str(e, "kind"), "turn-order intent");
                    Fraction orderPower = orderKind == TurnOrderIntentKind.BoostPower
                        ? ReadFraction(e, 3, 2) : default;
                    if (orderKind != TurnOrderIntentKind.BoostPower
                        && e.Params?.Keys.Any(key => key is "num" or "den") == true
                        || orderPower != default && (orderPower.Num <= 0 || orderPower.Den <= 0))
                        throw new ArgumentException("turnOrderIntent power fraction is invalid for its kind.");
                    if (effects.OfType<TurnOrderIntentEffect>().Any())
                        throw new ArgumentException("A move can declare only one turnOrderIntent effect.");
                    effects.Add(new TurnOrderIntentEffect(new TurnOrderIntentProfile(orderKind, orderPower)));
                    break;

                case "pairedAction":
                    if (chance != 100)
                        throw new ArgumentException("pairedAction does not support chance.");
                    CheckAllowedParams(e, "key", "member", "mode", "pairs", "num", "den");
                    if (move.DamageClass == DamageClass.Status || move.Power is not > 0
                        || move.Target is not MoveTarget.Selected)
                        throw new ArgumentException("pairedAction requires a damaging selected-target move.");
                    string pairKey = LowercaseToken(Str(e, "key"), "pairedAction key");
                    string pairMember = LowercaseToken(Str(e, "member"), "pairedAction member");
                    PairedActionMode pairMode = ParseNamed<PairedActionMode>(Str(e, "mode"), "paired-action mode");
                    IReadOnlyList<PairedActionOption> pairOptions = ParsePairedOptions(Str(e, "pairs"));
                    Fraction pairPower = ReadFraction(e, 2, 1);
                    if (pairPower.Num <= 0 || pairPower.Den <= 0
                        || pairOptions.Select(option => option.Partner).Distinct(StringComparer.Ordinal).Count()
                            != pairOptions.Count
                        || pairMode == PairedActionMode.Combine
                            && pairOptions.Any(option => option.Type is null
                                || option.SideEffect == PairedActionSideEffect.None))
                        throw new ArgumentException("pairedAction profile is incomplete or invalid.");
                    if (effects.OfType<PairedActionEffect>().Any())
                        throw new ArgumentException("A move can declare only one pairedAction effect.");
                    effects.Add(new PairedActionEffect(new PairedActionProfile(pairKey, pairMember,
                        pairMode, pairOptions, pairPower)));
                    break;

                case "itemRequire":
                    if (chance != 100)
                        throw new ArgumentException("itemRequire does not support chance.");
                    CheckAllowedParams(e, "subject", "state");
                    BattleItemSubject requireSubject = ParseNamed<BattleItemSubject>(
                        Str(e, "subject"), "item requirement subject");
                    BattleItemRequirement requirement = ParseNamed<BattleItemRequirement>(
                        Str(e, "state"), "item requirement state");
                    if (requireSubject == BattleItemSubject.Target && !IsActiveCreatureTarget(move.Target))
                        throw new ArgumentException("Target item requirements need an active-creature target.");
                    if (effects.OfType<ItemRequireEffect>().Any(effect => effect.Subject == requireSubject))
                        throw new ArgumentException("A move can declare only one item requirement per subject.");
                    effects.Add(new ItemRequireEffect(requireSubject, requirement));
                    break;

                case "itemMutation":
                    if (chance != 100)
                        throw new ArgumentException("itemMutation does not support chance.");
                    CheckAllowedParams(e, "operation", "subject", "duration", "cause");
                    BattleItemOperation itemOperation = ParseNamed<BattleItemOperation>(
                        Str(e, "operation"), "item mutation operation");
                    BattleItemSubject itemSubject = e.Params?.ContainsKey("subject") == true
                        ? ParseNamed<BattleItemSubject>(Str(e, "subject"), "item mutation subject")
                        : itemOperation is BattleItemOperation.Steal or BattleItemOperation.Remove
                            or BattleItemOperation.Destroy ? BattleItemSubject.Target : BattleItemSubject.User;
                    int? itemDuration = e.Params?.ContainsKey("duration") == true ? Int(e, "duration") : null;
                    string itemCause = e.Params?.ContainsKey("cause") == true
                        ? LowercaseToken(Str(e, "cause"), "item mutation cause") : "move";
                    bool needsTarget = itemSubject == BattleItemSubject.Target || itemOperation is
                        BattleItemOperation.Give or BattleItemOperation.Steal or BattleItemOperation.Swap;
                    bool transfers = itemOperation is BattleItemOperation.Give or BattleItemOperation.Steal
                        or BattleItemOperation.Swap;
                    if (transfers && (move.Target != MoveTarget.Selected
                            || e.Params?.ContainsKey("subject") == true)
                        || !transfers && needsTarget && !IsActiveCreatureTarget(move.Target))
                        throw new ArgumentException("This item mutation requires an active-creature target.");
                    if ((itemOperation == BattleItemOperation.Suppress) != itemDuration.HasValue
                        || itemDuration is <= 0 or > 16)
                        throw new ArgumentException("Only item suppression requires duration 1..16.");
                    if (effects.OfType<ItemMutationEffect>().Any())
                        throw new ArgumentException("A move can declare only one itemMutation effect.");
                    effects.Add(new ItemMutationEffect(itemOperation, itemSubject, itemDuration, itemCause));
                    break;

                case "abilityMutation":
                    if (chance != 100)
                        throw new ArgumentException("abilityMutation does not support chance.");
                    CheckAllowedParams(e, "operation", "source", "subject", "ability");
                    BattleAbilityOperation abilityOperation = ParseNamed<BattleAbilityOperation>(
                        Str(e, "operation"), "ability mutation operation");
                    BattleAbilitySubject abilitySource = e.Params?.ContainsKey("source") == true
                        ? ParseNamed<BattleAbilitySubject>(Str(e, "source"), "ability mutation source")
                        : BattleAbilitySubject.Target;
                    BattleAbilitySubject abilitySubject = e.Params?.ContainsKey("subject") == true
                        ? ParseNamed<BattleAbilitySubject>(Str(e, "subject"), "ability mutation subject")
                        : abilityOperation is BattleAbilityOperation.Replace or BattleAbilityOperation.Suppress
                            ? BattleAbilitySubject.Target : BattleAbilitySubject.User;
                    EntityId? replacementAbility = e.Params?.ContainsKey("ability") == true
                        ? EntityId.Parse(Str(e, "ability")) : null;
                    if (abilitySource == BattleAbilitySubject.UserAndAllies
                        || !IsActiveCreatureTarget(move.Target) || move.Target == MoveTarget.User
                        || abilityOperation == BattleAbilityOperation.Copy && abilitySource == abilitySubject
                        || abilityOperation == BattleAbilityOperation.Swap
                            && (e.Params?.ContainsKey("source") == true || e.Params?.ContainsKey("subject") == true)
                        || abilityOperation == BattleAbilityOperation.Replace != replacementAbility.HasValue
                        || abilityOperation is BattleAbilityOperation.Replace or BattleAbilityOperation.Suppress
                            && e.Params?.ContainsKey("source") == true)
                        throw new ArgumentException("abilityMutation parameters do not match the operation or move target.");
                    if (effects.OfType<AbilityMutationEffect>().Any())
                        throw new ArgumentException("A move can declare only one abilityMutation effect.");
                    effects.Add(new AbilityMutationEffect(abilityOperation, abilitySource, abilitySubject,
                        replacementAbility));
                    break;

                case "typeMutation":
                    if (chance != 100)
                        throw new ArgumentException("typeMutation does not support chance.");
                    CheckAllowedParams(e, "operation", "subject", "source", "types");
                    BattleTypeOperation typeOperation = ParseNamed<BattleTypeOperation>(
                        Str(e, "operation"), "type mutation operation");
                    BattleTypeSubject typeSubject = e.Params?.ContainsKey("subject") == true
                        ? ParseNamed<BattleTypeSubject>(Str(e, "subject"), "type mutation subject")
                        : BattleTypeSubject.User;
                    BattleTypeSubject? typeSource = e.Params?.ContainsKey("source") == true
                        ? ParseNamed<BattleTypeSubject>(Str(e, "source"), "type mutation source")
                        : typeOperation == BattleTypeOperation.Copy ? BattleTypeSubject.Target : null;
                    IReadOnlyList<EntityId>? typeList = e.Params?.ContainsKey("types") == true
                        ? ParseTypeList(Str(e, "types")) : null;
                    bool typeCopy = typeOperation == BattleTypeOperation.Copy;
                    if (typeCopy != typeSource.HasValue || typeCopy == (typeList is not null)
                        || typeCopy && typeSource == typeSubject
                        || (typeSubject == BattleTypeSubject.Target || typeSource == BattleTypeSubject.Target)
                            && (!IsActiveCreatureTarget(move.Target) || move.Target == MoveTarget.User))
                        throw new ArgumentException("typeMutation parameters do not match the operation or move target.");
                    if (effects.OfType<TypeMutationEffect>().Any())
                        throw new ArgumentException("A move can declare only one typeMutation effect.");
                    effects.Add(new TypeMutationEffect(typeOperation, typeSubject, typeSource, typeList));
                    break;

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
                    CheckAllowedParams(e, "num", "den", "recipient", "weather", "terrain");
                    Fraction healFraction = ReadFraction(e, 1, 2);
                    HpFractionRecipient healRecipient = e.Params?.ContainsKey("recipient") == true
                        ? Parse<HpFractionRecipient>(Str(e, "recipient"), "recipient")
                        : HpFractionRecipient.Self;
                    if (healRecipient == HpFractionRecipient.Self)
                        heal = healFraction;
                    IReadOnlyDictionary<Weather, Fraction>? healWeather = ParseWeatherFractions(e);
                    IReadOnlyDictionary<Terrain, Fraction>? healTerrain = ParseTerrainFractions(e);
                    if (healWeather is not null && healTerrain is not null)
                        throw new ArgumentException("heal cannot combine weather and terrain replacement tables.");
                    effects.Add(new HealEffect(healFraction, healRecipient, healWeather, healTerrain));
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
                    ValidateHazardOp(move, e);
                    effects.Add(new SetEntryHazardEffect(EntryHazardConditions.LegacyLayeredDamage));
                    break;

                case "entryHazardDamage":
                    ValidateHazardOp(move, e, "key", "maxLayers", "groundedOnly", "fractions", "type", "num", "den");
                    effects.Add(new SetEntryHazardEffect(ParseDamageHazard(e)));
                    break;

                case "entryHazardStatus":
                    ValidateHazardOp(move, e, "key", "maxLayers", "groundedOnly", "statuses", "absorbTypes");
                    effects.Add(new SetEntryHazardEffect(ParseStatusHazard(e)));
                    break;

                case "entryHazardStage":
                    ValidateHazardOp(move, e, "key", "groundedOnly", "stat", "delta");
                    effects.Add(new SetEntryHazardEffect(ParseStageHazard(e)));
                    break;

                case "weather": // apply_condition(field:weather) (catalog §7.6)
                    CheckAllowedParams(e, "weather");
                    setsWeather = Parse<Weather>(Str(e, "weather"), "weather");
                    effects.Add(new SetWeatherEffect(setsWeather));
                    break;

                case "terrain":
                    if (chance != 100)
                        throw new ArgumentException("terrain does not support chance.");
                    CheckAllowedParams(e, "terrain");
                    Terrain terrain = Parse<Terrain>(Str(e, "terrain"), "terrain");
                    if (terrain == Terrain.None)
                        throw new ArgumentException("terrain requires an active terrain row.");
                    effects.Add(new SetTerrainEffect(terrain));
                    break;

                case "groundedState":
                    if (chance != 100)
                        throw new ArgumentException("groundedState does not support chance.");
                    CheckAllowedParams(e, "state", "scope", "duration");
                    GroundedState groundedState = ParseNamed<GroundedState>(Str(e, "state"), "grounded state");
                    GroundedStateScope groundedScope = e.Params?.ContainsKey("scope") == true
                        ? ParseNamed<GroundedStateScope>(Str(e, "scope"), "grounded scope")
                        : GroundedStateScope.Target;
                    int groundedDuration = e.Params?.ContainsKey("duration") == true
                        ? Int(e, "duration")
                        : GroundedConditions.DefaultTurns;
                    if (groundedDuration <= 0)
                        throw new ArgumentException("groundedState duration must be positive.");
                    if (groundedScope == GroundedStateScope.Field
                        && (groundedState != GroundedState.Grounded || move.Target != MoveTarget.EntireField))
                        throw new ArgumentException("Field groundedState requires grounded state and target entire-field.");
                    if (groundedScope == GroundedStateScope.Target && move.Target is
                        MoveTarget.UsersField or MoveTarget.OpponentsField or MoveTarget.EntireField
                        or MoveTarget.FaintingPokemon or MoveTarget.SpecificMove)
                        throw new ArgumentException("Target groundedState requires an active-creature move target.");
                    effects.Add(new GroundedStateEffect(groundedState, groundedScope, groundedDuration));
                    break;

                case "fieldCondition":
                    if (chance != 100)
                        throw new ArgumentException("fieldCondition does not support chance.");
                    CheckAllowedParams(e, "condition", "duration");
                    if (move.Target != MoveTarget.EntireField || move.DamageClass != DamageClass.Status)
                        throw new ArgumentException("fieldCondition requires a status move targeting entire-field.");
                    BattleFieldCondition fieldCondition = ParseNamed<BattleFieldCondition>(
                        Str(e, "condition"), "field condition");
                    int fieldDuration = e.Params?.ContainsKey("duration") == true
                        ? Int(e, "duration") : FieldConditions.DefaultTurns;
                    if (fieldDuration <= 0)
                        throw new ArgumentException("fieldCondition duration must be positive.");
                    if (effects.OfType<SetFieldConditionEffect>().Any())
                        throw new ArgumentException("A move can declare only one fieldCondition effect.");
                    effects.Add(new SetFieldConditionEffect(fieldCondition, fieldDuration));
                    break;

                case "sideCondition":
                    if (chance != 100)
                        throw new ArgumentException("sideCondition does not support chance.");
                    CheckAllowedParams(e, "condition", "duration", "side");
                    SideConditionTarget conditionSide = e.Params?.ContainsKey("side") == true
                        ? ParseNamed<SideConditionTarget>(Str(e, "side"), "side condition target")
                        : SideConditionTarget.Source;
                    MoveTarget requiredTarget = conditionSide == SideConditionTarget.Source
                        ? MoveTarget.UsersField : MoveTarget.OpponentsField;
                    if (move.Target != requiredTarget || move.DamageClass != DamageClass.Status)
                        throw new ArgumentException($"sideCondition targeting {conditionSide} requires a status move targeting {requiredTarget}.");
                    BattleSideCondition sideCondition = ParseNamed<BattleSideCondition>(
                        Str(e, "condition"), "side condition");
                    SideConditionTarget requiredSide = sideCondition is BattleSideCondition.SpeedReduction
                        or BattleSideCondition.ResidualDamage
                        ? SideConditionTarget.Target
                        : SideConditionTarget.Source;
                    if (conditionSide != requiredSide)
                        throw new ArgumentException($"sideCondition '{sideCondition}' requires side '{requiredSide}'.");
                    int sideDuration = e.Params?.ContainsKey("duration") == true
                        ? Int(e, "duration")
                        : SideConditions.For(sideCondition).DefaultDuration!.Value;
                    if (sideDuration <= 0)
                        throw new ArgumentException("sideCondition duration must be positive.");
                    if ((sideCondition is BattleSideCondition.PriorityProtection
                        or BattleSideCondition.MultiTargetProtection
                        or BattleSideCondition.StatusProtection
                        or BattleSideCondition.DamageProtection)
                        && sideDuration != 1)
                        throw new ArgumentException("Side protection conditions require duration 1.");
                    if (effects.OfType<SetSideConditionEffect>().Any())
                        throw new ArgumentException("A move can declare only one sideCondition effect.");
                    effects.Add(new SetSideConditionEffect(sideCondition, sideDuration, conditionSide));
                    break;

                case "sideConditionBypass":
                    if (chance != 100)
                        throw new ArgumentException("sideConditionBypass does not support chance.");
                    CheckAllowedParams(e, "tag");
                    string bypassTag = Str(e, "tag");
                    if (bypassTag is not ("screen" or "status_guard" or "stage_guard" or "side_protection"))
                        throw new ArgumentException("sideConditionBypass has an unknown side-condition tag.");
                    bool compatibleBypass = bypassTag switch
                    {
                        "screen" => move.DamageClass != DamageClass.Status && move.Power is not null,
                        "status_guard" => move.Effects.Any(effect => effect.Op == "ailment"),
                        "stage_guard" => move.Effects.Any(effect =>
                            (effect.Op is "statStage" or "statStageAll") && Int(effect, "delta") < 0),
                        "side_protection" => move.Target is not (MoveTarget.User or MoveTarget.UsersField
                            or MoveTarget.OpponentsField or MoveTarget.EntireField or MoveTarget.FaintingPokemon
                            or MoveTarget.SpecificMove),
                        _ => false,
                    };
                    if (!compatibleBypass)
                        throw new ArgumentException($"sideConditionBypass tag '{bypassTag}' has no compatible move effect.");
                    if (move.Target is MoveTarget.UsersField or MoveTarget.OpponentsField or MoveTarget.EntireField
                        or MoveTarget.FaintingPokemon or MoveTarget.SpecificMove)
                        throw new ArgumentException("sideConditionBypass requires an active-creature move target.");
                    if (effects.OfType<SideConditionBypassEffect>().Any())
                        throw new ArgumentException("A move can declare only one sideConditionBypass effect.");
                    effects.Add(new SideConditionBypassEffect(bypassTag));
                    break;

                case "removeSideCondition":
                    if (chance != 100)
                        throw new ArgumentException("removeSideCondition does not support chance.");
                    CheckAllowedParams(e, "tag", "side", "timing");
                    string removeTag = Str(e, "tag");
                    if (removeTag is not ("screen" or "status_guard" or "stage_guard" or "barrier"
                        or "side_protection" or "entry_hazard" or "hazard"))
                        throw new ArgumentException("removeSideCondition has an unknown side-condition tag.");
                    SideConditionTarget removeSide = ParseNamed<SideConditionTarget>(Str(e, "side"), "side condition target");
                    SideConditionTiming removeTiming = ParseNamed<SideConditionTiming>(Str(e, "timing"), "side condition timing");
                    if (removeTiming == SideConditionTiming.BeforeDamage
                        && (move.DamageClass == DamageClass.Status || move.Power is null))
                        throw new ArgumentException("beforeDamage removeSideCondition requires a damaging move with authored power.");
                    if (removeSide == SideConditionTarget.Target && move.Target is
                        MoveTarget.UsersField or MoveTarget.OpponentsField or MoveTarget.EntireField
                        or MoveTarget.FaintingPokemon or MoveTarget.SpecificMove)
                        throw new ArgumentException("Target removeSideCondition requires an active-creature move target.");
                    if (effects.OfType<RemoveSideConditionEffect>().Any())
                        throw new ArgumentException("A move can declare only one removeSideCondition effect.");
                    effects.Add(new RemoveSideConditionEffect(removeTag, removeSide, removeTiming));
                    break;

                case "conditionRemove":
                    if (chance != 100)
                        throw new ArgumentException("conditionRemove does not support chance.");
                    CheckAllowedParams(e, "scope", "owner", "condition", "tag", "all", "source");
                    BattleConditionSelector removeSelector = ParseConditionSelector(e);
                    SideConditionTarget removeOwner = ParseConditionOwner(Str(e, "owner"));
                    if (removeSelector.Scope is BattleConditionScope.Field or BattleConditionScope.Weather
                        or BattleConditionScope.Terrain or BattleConditionScope.Room
                        && removeOwner != SideConditionTarget.Source)
                        throw new ArgumentException("Field-owned condition removal requires owner source.");
                    if (removeOwner == SideConditionTarget.Target && !IsActiveCreatureTarget(move.Target))
                        throw new ArgumentException("Target condition removal requires an active-creature target.");
                    if (removeSelector.Source == BattleConditionSourceTarget.Target
                        && !IsActiveCreatureTarget(move.Target))
                        throw new ArgumentException("Target-source condition removal requires an active-creature target.");
                    if (effects.OfType<RemoveConditionEffect>().Any())
                        throw new ArgumentException("A move can declare only one conditionRemove effect.");
                    effects.Add(new RemoveConditionEffect(removeSelector, removeOwner));
                    break;

                case "conditionTransfer":
                    if (chance != 100)
                        throw new ArgumentException("conditionTransfer does not support chance.");
                    CheckAllowedParams(e, "scope", "from", "to", "condition", "tag", "all", "source",
                        "resetDuration", "resetCounters");
                    BattleConditionSelector transferSelector = ParseConditionSelector(e);
                    if (transferSelector.Scope is not (BattleConditionScope.Side or BattleConditionScope.Slot
                        or BattleConditionScope.Creature) || !IsSingleActiveCreatureTarget(move.Target))
                        throw new ArgumentException("Condition transfer requires side, slot, or creature scope and one active-creature target.");
                    if (transferSelector.Scope == BattleConditionScope.Side && move.Target == MoveTarget.Ally)
                        throw new ArgumentException("Side condition transfer requires a target on another side.");
                    SideConditionTarget transferFrom = ParseConditionOwner(Str(e, "from"));
                    SideConditionTarget transferTo = ParseConditionOwner(Str(e, "to"));
                    if (transferFrom == transferTo)
                        throw new ArgumentException("Condition transfer requires distinct owners.");
                    if (effects.OfType<TransferConditionEffect>().Any())
                        throw new ArgumentException("A move can declare only one conditionTransfer effect.");
                    effects.Add(new TransferConditionEffect(transferSelector, transferFrom, transferTo,
                        OptionalBool(e, "resetDuration"), OptionalBool(e, "resetCounters")));
                    break;

                case "conditionSwap":
                    if (chance != 100)
                        throw new ArgumentException("conditionSwap does not support chance.");
                    CheckAllowedParams(e, "scope", "condition", "tag", "all", "source",
                        "resetDuration", "resetCounters");
                    BattleConditionSelector swapSelector = ParseConditionSelector(e);
                    if (swapSelector.Scope is not (BattleConditionScope.Side or BattleConditionScope.Slot)
                        || !IsSingleActiveCreatureTarget(move.Target))
                        throw new ArgumentException("Condition swap requires side or slot scope and one active-creature target.");
                    if (swapSelector.Scope == BattleConditionScope.Side && move.Target == MoveTarget.Ally)
                        throw new ArgumentException("Side condition swap requires a target on another side.");
                    if (effects.OfType<SwapConditionEffect>().Any())
                        throw new ArgumentException("A move can declare only one conditionSwap effect.");
                    effects.Add(new SwapConditionEffect(swapSelector,
                        OptionalBool(e, "resetDuration"), OptionalBool(e, "resetCounters")));
                    break;

                case "fieldMoveGate":
                    if (chance != 100)
                        throw new ArgumentException("fieldMoveGate does not support chance.");
                    CheckAllowedParams(e, "condition");
                    BattleFieldCondition gateCondition = ParseNamed<BattleFieldCondition>(
                        Str(e, "condition"), "field condition");
                    if (gateCondition != BattleFieldCondition.Gravity)
                        throw new ArgumentException("fieldMoveGate currently admits only gravity.");
                    if (effects.OfType<FieldMoveGateEffect>().Any())
                        throw new ArgumentException("A move can declare only one fieldMoveGate effect.");
                    effects.Add(new FieldMoveGateEffect(gateCondition));
                    break;

                case "terrainMove":
                    if (chance != 100)
                        throw new ArgumentException("terrainMove does not support chance.");
                    if (move.DamageClass == DamageClass.Status || move.Power is null)
                        throw new ArgumentException("terrainMove requires a damaging move with authored power.");
                    CheckAllowedParams(e, "subject", "types", "power", "priority", "spread");
                    TerrainMoveSubject terrainSubject = Parse<TerrainMoveSubject>(Str(e, "subject"), "subject");
                    IReadOnlyDictionary<Terrain, EntityId> terrainTypes = ParseTerrainTypes(e);
                    IReadOnlyDictionary<Terrain, Fraction> terrainPower = ParseTerrainPower(e);
                    IReadOnlyDictionary<Terrain, int> terrainPriority = ParseTerrainPriority(e, move.Priority);
                    IReadOnlySet<Terrain> spreadTerrains = ParseTerrainList(e, "spread");
                    if (terrainTypes.Count + terrainPower.Count + terrainPriority.Count + spreadTerrains.Count == 0)
                        throw new ArgumentException("terrainMove requires types, power, priority, or spread rows.");
                    if (terrainSubject == TerrainMoveSubject.Target
                        && (terrainTypes.Count + terrainPriority.Count + spreadTerrains.Count > 0))
                        throw new ArgumentException("target-subject terrainMove supports power rows only.");
                    if (spreadTerrains.Count > 0 && move.Target != MoveTarget.Selected)
                        throw new ArgumentException("terrainMove spread requires the selected target.");
                    effects.Add(new TerrainMoveEffect(terrainSubject, terrainTypes, terrainPower,
                        terrainPriority, spreadTerrains));
                    break;

                case "terrainGate":
                    if (chance != 100)
                        throw new ArgumentException("terrainGate does not support chance.");
                    CheckAllowedParams(e);
                    effects.Add(new TerrainGateEffect());
                    break;

                case "removeTerrain":
                    if (chance != 100)
                        throw new ArgumentException("removeTerrain does not support chance.");
                    CheckAllowedParams(e);
                    effects.Add(new RemoveTerrainEffect());
                    break;

                case "stealthRock": // apply_condition(side:entry_hazard_damage, type_scaled) (catalog §9.4)
                    ValidateHazardOp(move, e);
                    effects.Add(new SetEntryHazardEffect(EntryHazardConditions.LegacyTypeScaledDamage));
                    break;

                case "bind": // apply_condition(volatile:partial_trap) (catalog §7.2)
                    binds = true;
                    effects.Add(new BindEffect());
                    break;

                case "protect": // legacy alias for the default typed personal profile
                    if (chance != 100)
                        throw new ArgumentException("protect does not support chance.");
                    CheckAllowedParams(e);
                    if (move.DamageClass != DamageClass.Status || move.Target != MoveTarget.User)
                        throw new ArgumentException("protect requires a self-targeted status move.");
                    if (effects.OfType<ProtectEffect>().Any())
                        throw new ArgumentException("A move can declare only one protection effect.");
                    effects.Add(new ProtectEffect(ProtectionConditions.LegacyPersonal));
                    break;

                case "protection":
                    if (chance != 100)
                        throw new ArgumentException("protection does not support chance.");
                    CheckAllowedParams(e, "key", "scope", "filter", "chain", "drawGuaranteed", "contact");
                    if (move.DamageClass != DamageClass.Status)
                        throw new ArgumentException("protection requires a status move.");
                    if (effects.OfType<ProtectEffect>().Any())
                        throw new ArgumentException("A move can declare only one protection effect.");
                    ProtectionScope protectionScope = Parse<ProtectionScope>(Str(e, "scope"), "protection scope");
                    ProtectionFilter protectionFilter = Parse<ProtectionFilter>(Str(e, "filter"), "protection filter");
                    ProtectionChainMode protectionChain = Parse<ProtectionChainMode>(Str(e, "chain"), "protection chain");
                    bool drawGuaranteed = RequiredBool(e, "drawGuaranteed");
                    IReadOnlyList<ProtectionContactEffect> contact = e.Params?.ContainsKey("contact") == true
                        ? ParseProtectionContact(Str(e, "contact")) : [];
                    ProtectionProfile profile = protectionScope == ProtectionScope.Personal
                        ? ProtectionConditions.Personal(Str(e, "key"), protectionChain, drawGuaranteed, contact)
                        : ProtectionConditions.Side(Str(e, "key"), protectionFilter, protectionChain, drawGuaranteed);
                    if (profile.Filter != protectionFilter)
                        throw new ArgumentException("Personal protection requires the all filter.");
                    if (profile.Scope == ProtectionScope.Personal && move.Target != MoveTarget.User
                        || profile.Scope == ProtectionScope.Side && move.Target != MoveTarget.UsersField)
                        throw new ArgumentException("Protection scope does not match the authored target.");
                    if (profile.Scope == ProtectionScope.Side && contact.Count > 0)
                        throw new ArgumentException("Side protection does not support contact payloads.");
                    effects.Add(new ProtectEffect(profile));
                    break;

                case "protectionBypass":
                    if (chance != 100)
                        throw new ArgumentException("protectionBypass does not support chance.");
                    CheckAllowedParams(e);
                    if (move.Target is MoveTarget.User or MoveTarget.UsersField or MoveTarget.OpponentsField
                        or MoveTarget.EntireField or MoveTarget.FaintingPokemon or MoveTarget.SpecificMove)
                        throw new ArgumentException("protectionBypass requires an externally directed active-creature target.");
                    if (effects.OfType<ProtectionBypassEffect>().Any())
                        throw new ArgumentException("A move can declare only one protectionBypass effect.");
                    effects.Add(new ProtectionBypassEffect());
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

                case "weatherMove":
                    if (chance != 100)
                        throw new ArgumentException("weatherMove does not support chance.");
                    if (move.DamageClass == DamageClass.Status || move.Power is null)
                        throw new ArgumentException("weatherMove requires a damaging move with authored power.");
                    CheckAllowedParams(e, "types", "power", "skipCharge");
                    IReadOnlyDictionary<Weather, EntityId> weatherTypes = ParseWeatherTypes(e);
                    IReadOnlyDictionary<Weather, Fraction> weatherPower = ParseWeatherPower(e);
                    IReadOnlySet<Weather> skipChargeWeather = ParseWeatherList(e, "skipCharge", "weatherMove");
                    if (weatherTypes.Count == 0 && weatherPower.Count == 0 && skipChargeWeather.Count == 0)
                        throw new ArgumentException("weatherMove requires types, power, or skipCharge rows.");
                    effects.Add(new WeatherMoveEffect(weatherTypes, weatherPower, skipChargeWeather));
                    break;

                case "chargeTurn":
                    if (chance != 100 || charge is not null)
                        throw new ArgumentException("chargeTurn is unique and does not support chance.");
                    CheckAllowedParams(e, "state", "targetPolicy");
                    SemiInvulnerableState? chargeState = e.Params?.ContainsKey("state") == true
                        ? ParseNamed<SemiInvulnerableState>(Str(e, "state"), "semi-invulnerable state") : null;
                    BattleIntentTargetPolicy chargeTargetPolicy = e.Params?.ContainsKey("targetPolicy") == true
                        ? ParseNamed<BattleIntentTargetPolicy>(Str(e, "targetPolicy"), "charge target policy")
                        : BattleIntentTargetPolicy.LiveSlot;
                    if (chargeTargetPolicy is not (BattleIntentTargetPolicy.LiveSlot
                        or BattleIntentTargetPolicy.SnapshotSlot))
                        throw new ArgumentException("chargeTurn targetPolicy must be liveSlot or snapshotSlot.");
                    charge = new ChargeMoveEffect(chargeState, chargeTargetPolicy);
                    break;

                case "semiInvulnerableHit":
                    if (chance != 100 || effects.OfType<SemiInvulnerableHitEffect>().Any())
                        throw new ArgumentException("semiInvulnerableHit is unique and does not support chance.");
                    CheckAllowedParams(e, "states", "powerNum", "powerDen");
                    SemiInvulnerableState[] hitStates = Str(e, "states")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(value => ParseNamed<SemiInvulnerableState>(value, "semi-invulnerable state"))
                        .ToArray();
                    if (hitStates.Length == 0 || hitStates.Distinct().Count() != hitStates.Length)
                        throw new ArgumentException("semiInvulnerableHit states must be nonempty and unique.");
                    bool hasPowerNum = e.Params?.ContainsKey("powerNum") == true;
                    bool hasPowerDen = e.Params?.ContainsKey("powerDen") == true;
                    if (hasPowerNum != hasPowerDen)
                        throw new ArgumentException("semiInvulnerableHit power requires powerNum and powerDen.");
                    Fraction? hitPower = hasPowerNum ? new Fraction(Int(e, "powerNum"), Int(e, "powerDen")) : null;
                    if (hitPower is { Num: <= 0 } or { Den: <= 0 })
                        throw new ArgumentException("semiInvulnerableHit power fraction must be positive.");
                    effects.Add(new SemiInvulnerableHitEffect(hitStates.ToHashSet(), hitPower));
                    break;

                case "chargeStartStat":
                    if (chance != 100)
                        throw new ArgumentException("chargeStartStat does not support chance.");
                    CheckAllowedParams(e, "stat", "delta");
                    StatKind chargeStat = ParseNamed<StatKind>(Str(e, "stat"), "charge-start stat");
                    int chargeDelta = Int(e, "delta");
                    if (chargeDelta is < -6 or > 6 or 0)
                        throw new ArgumentException("chargeStartStat delta must be -6..-1 or 1..6.");
                    if (effects.OfType<ChargeStartStatEffect>().Any(effect => effect.Stat == chargeStat))
                        throw new ArgumentException("chargeStartStat may declare each stat once.");
                    effects.Add(new ChargeStartStatEffect(chargeStat, chargeDelta));
                    break;

                case "delayedDamage":
                    if (chance != 100)
                        throw new ArgumentException("delayedDamage does not support chance.");
                    CheckAllowedParams(e, "turns", "sourceRequired", "uniquePerSlot");
                    effects.Add(new DelayedDamageEffect(Int(e, "turns"),
                        Bool(e, "sourceRequired"), Bool(e, "uniquePerSlot")));
                    break;

                case "delayedHeal":
                    if (chance != 100)
                        throw new ArgumentException("delayedHeal does not support chance.");
                    CheckAllowedParams(e, "turns", "num", "den", "basis", "targetPolicy",
                        "sourceRequired");
                    effects.Add(new DelayedHealEffect(
                        Int(e, "turns"),
                        new Fraction(Int(e, "num"), Int(e, "den")),
                        e.Params?.ContainsKey("basis") == true
                            ? Parse<DelayedHealBasis>(Str(e, "basis"), "delayed-heal basis")
                            : DelayedHealBasis.SourceMaxHp,
                        e.Params?.ContainsKey("targetPolicy") == true
                            ? Parse<BattleIntentTargetPolicy>(Str(e, "targetPolicy"), "delayed-heal target policy")
                            : BattleIntentTargetPolicy.LiveSlot,
                        Bool(e, "sourceRequired")));
                    break;

                case "delayedStatus":
                    if (chance != 100)
                        throw new ArgumentException("delayedStatus does not support chance.");
                    CheckAllowedParams(e, "turns", "status", "targetPolicy", "sourceRequired");
                    effects.Add(new DelayedStatusEffect(
                        Int(e, "turns"),
                        Parse<PersistentStatus>(Str(e, "status"), "delayed status"),
                        e.Params?.ContainsKey("targetPolicy") == true
                            ? Parse<BattleIntentTargetPolicy>(Str(e, "targetPolicy"), "delayed-status target policy")
                            : BattleIntentTargetPolicy.SnapshotSlot,
                        Bool(e, "sourceRequired")));
                    break;

                case "replacementRestore":
                    if (chance != 100)
                        throw new ArgumentException("replacementRestore does not support chance.");
                    CheckAllowedParams(e, "hp", "status", "pp");
                    effects.Add(new ReplacementRestoreEffect(
                        e.Params?.ContainsKey("hp") != true || Bool(e, "hp"),
                        e.Params?.ContainsKey("status") != true || Bool(e, "status"),
                        Bool(e, "pp")));
                    break;

                case "multiTurnLock":
                    if (chance != 100)
                        throw new ArgumentException("multiTurnLock does not support chance.");
                    if (multiTurnLock is not null)
                        throw new ArgumentException("A move may declare multiTurnLock only once.");
                    CheckAllowedParams(e, "minTurns", "maxTurns", "repeatPaysPp", "powerNum", "powerDen",
                        "maxPowerStep", "endOnFailure", "endEffect", "powerBoostKey");
                    int minTurns = e.Params?.ContainsKey("minTurns") == true ? Int(e, "minTurns") : 2;
                    int maxTurns = e.Params?.ContainsKey("maxTurns") == true ? Int(e, "maxTurns") : 3;
                    int powerNum = e.Params?.ContainsKey("powerNum") == true ? Int(e, "powerNum") : 1;
                    int powerDen = e.Params?.ContainsKey("powerDen") == true ? Int(e, "powerDen") : 1;
                    int maxPowerStep = e.Params?.ContainsKey("maxPowerStep") == true ? Int(e, "maxPowerStep") : 0;
                    if (minTurns < 1 || maxTurns < minTurns || maxTurns > 16)
                        throw new ArgumentException("multiTurnLock requires 1 <= minTurns <= maxTurns <= 16.");
                    if (powerNum < 1 || powerDen < 1 || maxPowerStep < 0 || maxPowerStep >= maxTurns)
                        throw new ArgumentException("multiTurnLock power scaling must be positive and capped below maxTurns.");
                    multiTurnLock = new MultiTurnLockProfile(minTurns, maxTurns,
                        e.Params?.ContainsKey("repeatPaysPp") == true && Bool(e, "repeatPaysPp"),
                        new Fraction(powerNum, powerDen), maxPowerStep,
                        e.Params?.ContainsKey("endOnFailure") == true && Bool(e, "endOnFailure"),
                        e.Params?.ContainsKey("endEffect") == true
                            ? Parse<MultiTurnLockEndEffect>(Str(e, "endEffect"), "multi-turn end effect")
                            : MultiTurnLockEndEffect.Confusion,
                        e.Params?.ContainsKey("powerBoostKey") == true ? Str(e, "powerBoostKey") : null);
                    break;

                case "multiTurnPowerBoost":
                    if (chance != 100)
                        throw new ArgumentException("multiTurnPowerBoost does not support chance.");
                    CheckAllowedParams(e, "key", "num", "den");
                    string boostKey = Str(e, "key");
                    Fraction boost = ReadFraction(e, 2, 1);
                    if (string.IsNullOrWhiteSpace(boostKey) || boost.Num < 1 || boost.Den < 1)
                        throw new ArgumentException("multiTurnPowerBoost requires a key and positive multiplier.");
                    effects.Add(new MultiTurnPowerBoostEffect(boostKey, boost));
                    break;

                case "moveGate":
                    if (chance != 100)
                        throw new ArgumentException("moveGate does not support chance.");
                    CheckAllowedParams(e, "kind", "timing", "targetClass", "damageMode", "damageClass");
                    MoveGateKind gateKind = Parse<MoveGateKind>(Str(e, "kind"), "kind");
                    MoveGateTiming gateTiming = e.Params?.ContainsKey("timing") == true
                        ? Parse<MoveGateTiming>(Str(e, "timing"), "timing")
                        : MoveGateTiming.BeforeMove;
                    MoveGateTargetClass? gateTargetClass = e.Params?.ContainsKey("targetClass") == true
                        ? Parse<MoveGateTargetClass>(Str(e, "targetClass"), "targetClass") : null;
                    MoveGateDamageMode? gateDamageMode = e.Params?.ContainsKey("damageMode") == true
                        ? Parse<MoveGateDamageMode>(Str(e, "damageMode"), "damageMode") : null;
                    DamageClass? gateDamageClass = e.Params?.ContainsKey("damageClass") == true
                        ? Parse<DamageClass>(Str(e, "damageClass"), "damageClass") : null;
                    var moveGate = new MoveGateEffect(gateKind, gateTiming, gateTargetClass,
                        gateDamageMode, gateDamageClass);
                    BattleActionGates.Validate(moveGate);
                    if (effects.OfType<MoveGateEffect>().Any(gate =>
                        gate.Kind == gateKind && gate.Timing == gateTiming))
                        throw new ArgumentException("A move can declare one moveGate per kind/timing pair.");
                    effects.Add(moveGate);
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

                case "recharge":
                    if (chance != 100)
                        throw new ArgumentException("recharge does not support chance.");
                    CheckAllowedParams(e, "turns");
                    int rechargeTurns = e.Params?.ContainsKey("turns") == true ? Int(e, "turns") : 1;
                    if (rechargeTurns <= 0)
                        throw new ArgumentException("recharge turns must be positive.");
                    if (effects.OfType<QueueActionGateEffect>().Any(gate =>
                        gate.Owner == QueueActionGateOwner.Creature))
                        throw new ArgumentException("A move can declare only one recharge effect.");
                    effects.Add(new QueueActionGateEffect(rechargeTurns, QueueActionGateOwner.Creature));
                    break;

                case "damageStatOverride": // damage stat query override
                    if (chance != 100)
                        throw new ArgumentException("damageStatOverride does not support chance.");
                    CheckAllowedParams(e, "offensiveStat", "defensiveStat", "offensiveOwner", "defensiveOwner");
                    if (effects.OfType<DamageStatQueryEffect>().Any())
                        throw new ArgumentException("A move can declare only one damageStatOverride effect.");
                    offensiveStatOverride = OptionalStat(e, "offensiveStat", StatKind.Atk, StatKind.Def, StatKind.Spa, StatKind.Spd);
                    defensiveStatOverride = OptionalStat(e, "defensiveStat", StatKind.Def, StatKind.Spd);
                    if (offensiveStatOverride is null && defensiveStatOverride is null)
                        throw new ArgumentException("damageStatOverride requires offensiveStat or defensiveStat.");
                    if (e.Params?.ContainsKey("offensiveOwner") == true && offensiveStatOverride is null
                        || e.Params?.ContainsKey("defensiveOwner") == true && defensiveStatOverride is null)
                        throw new ArgumentException("A damage-stat owner requires its matching stat selector.");
                    effects.Add(new DamageStatQueryEffect(
                        offensiveStatOverride is { } offensive
                            ? new DamageStatSelector(ParseDamageOwner(e, "offensiveOwner", DamageQueryOwner.User), offensive)
                            : null,
                        defensiveStatOverride is { } defensive
                            ? new DamageStatSelector(ParseDamageOwner(e, "defensiveOwner", DamageQueryOwner.Target), defensive)
                            : null));
                    break;

                case "damageClassQuery":
                    if (chance != 100)
                        throw new ArgumentException("damageClassQuery does not support chance.");
                    CheckAllowedParams(e, "mode");
                    if (effects.OfType<DamageClassQueryEffect>().Any())
                        throw new ArgumentException("A move can declare only one damageClassQuery effect.");
                    effects.Add(new DamageClassQueryEffect(
                        ParseNamed<DamageClassQueryMode>(Str(e, "mode"), "damage-class query mode")));
                    break;

                case "effectivenessQuery":
                    if (chance != 100)
                        throw new ArgumentException("effectivenessQuery does not support chance.");
                    CheckAllowedParams(e, "mode", "additionalType", "defendingType", "num", "den", "stabSource");
                    if (effects.OfType<EffectivenessQueryEffect>().Any())
                        throw new ArgumentException("A move can declare only one effectivenessQuery effect.");
                    bool hasDefendingType = e.Params?.ContainsKey("defendingType") == true;
                    bool hasNum = e.Params?.ContainsKey("num") == true;
                    bool hasDen = e.Params?.ContainsKey("den") == true;
                    if (hasDefendingType != hasNum || hasDefendingType != hasDen)
                        throw new ArgumentException("Effectiveness defending-type overrides require defendingType, num, and den.");
                    int multiplierNum = hasDefendingType ? Int(e, "num") : 1;
                    int multiplierDen = hasDefendingType ? Int(e, "den") : 1;
                    if (multiplierNum <= 0 || multiplierDen <= 0)
                        throw new ArgumentException("Effectiveness override fractions must be positive.");
                    BattleQueryValue? defendingMultiplier = hasDefendingType
                        ? new BattleQueryValue(multiplierNum, multiplierDen) : null;
                    EffectivenessQueryMode mode = e.Params?.ContainsKey("mode") == true
                        ? ParseNamed<EffectivenessQueryMode>(Str(e, "mode"), "effectiveness query mode")
                        : EffectivenessQueryMode.Standard;
                    EntityId? additionalType = e.Params?.ContainsKey("additionalType") == true
                        ? ParseTypeEntityId(Str(e, "additionalType"), "additionalType") : null;
                    EntityId? defendingType = hasDefendingType
                        ? ParseTypeEntityId(Str(e, "defendingType"), "defendingType") : null;
                    StabQuerySource stabSource = e.Params?.ContainsKey("stabSource") == true
                        ? ParseNamed<StabQuerySource>(Str(e, "stabSource"), "STAB source")
                        : StabQuerySource.User;
                    if (mode == EffectivenessQueryMode.Standard && additionalType is null
                        && defendingType is null && stabSource == StabQuerySource.User)
                        throw new ArgumentException("effectivenessQuery requires a nondefault query rule.");
                    effects.Add(new EffectivenessQueryEffect(mode, additionalType, defendingType,
                        defendingMultiplier, stabSource));
                    break;

                case "queryModifier":
                    if (chance != 100)
                        throw new ArgumentException("queryModifier does not support chance.");
                    CheckAllowedParams(e, "query", "operation", "num", "den");
                    BattleQueryId query = ParseNamed<BattleQueryId>(Str(e, "query"), "battle query");
                    BattleQueryOperation operation = ParseNamed<BattleQueryOperation>(
                        Str(e, "operation"), "battle query operation");
                    int queryNum = Int(e, "num");
                    int queryDen = e.Params?.ContainsKey("den") == true ? Int(e, "den") : 1;
                    var queryModifier = new MoveQueryModifierEffect(query, operation,
                        new BattleQueryValue(queryNum, queryDen));
                    BattleActionQueries.Validate(queryModifier);
                    if (effects.OfType<MoveQueryModifierEffect>().Any(effect =>
                        effect.Query == query && effect.Operation == operation))
                        throw new ArgumentException("A move can declare one queryModifier per query/operation pair.");
                    effects.Add(queryModifier);
                    break;

                case "accuracyRule":
                    if (chance != 100)
                        throw new ArgumentException("accuracyRule does not support chance.");
                    CheckAllowedParams(e, "mode");
                    if (effects.OfType<AccuracyQueryEffect>().Any())
                        throw new ArgumentException("A move can declare only one accuracyRule effect.");
                    effects.Add(new AccuracyQueryEffect(
                        ParseNamed<AccuracyQueryMode>(Str(e, "mode"), "accuracy-query mode")));
                    break;

                case "nextQuery":
                    if (chance != 100)
                        throw new ArgumentException("nextQuery does not support chance.");
                    CheckAllowedParams(e, "query", "duration");
                    int queryDuration = e.Params?.ContainsKey("duration") == true ? Int(e, "duration") : 2;
                    if (queryDuration is < 1 or > 8)
                        throw new ArgumentException("nextQuery duration must be in 1..8.");
                    if (effects.OfType<OneShotQueryEffect>().Any())
                        throw new ArgumentException("A move can declare only one nextQuery effect.");
                    effects.Add(new OneShotQueryEffect(
                        ParseNamed<OneShotQuery>(Str(e, "query"), "one-shot query"), queryDuration));
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

                case "statStageSteal":
                    CheckAllowedParams(e);
                    AddChanceEffect(effects, new StatStealEffect(), chance);
                    break;

                case "statStageRandomRaise":
                    CheckAllowedParams(e, "delta", "onSelf");
                    int randomRaiseDelta = e.Params?.ContainsKey("delta") == true ? Int(e, "delta") : 2;
                    if (randomRaiseDelta <= 0)
                        throw new ArgumentException("statStageRandomRaise delta must be positive.");
                    AddChanceEffect(effects,
                        new RandomStatRaiseEffect(randomRaiseDelta, Bool(e, "onSelf") || move.Target == MoveTarget.User),
                        chance);
                    break;

                case "derivedStatSwap":
                    CheckAllowedParams(e, "stat");
                    StatKind swapStat = ParseNamed<StatKind>(Str(e, "stat"), "derived stat");
                    if (swapStat != StatKind.Spe)
                        throw new ArgumentException("derivedStatSwap currently supports only the speed stat.");
                    if (!IsActiveCreatureTarget(move.Target) || move.Target == MoveTarget.User)
                        throw new ArgumentException("derivedStatSwap requires an active-creature target.");
                    if (effects.OfType<DerivedStatSwapEffect>().Any())
                        throw new ArgumentException("A move can declare only one derivedStatSwap effect.");
                    effects.Add(new DerivedStatSwapEffect(swapStat));
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
        if (effects.OfType<WeatherMoveEffect>().Count() > 1)
            throw new ArgumentException("A move can declare only one weatherMove op.");
        if (effects.OfType<GroundedStateEffect>().Count() > 1)
            throw new ArgumentException("A move can declare only one groundedState op.");
        if (effects.OfType<TerrainMoveEffect>().Count() > 1)
            throw new ArgumentException("A move can declare only one terrainMove op.");
        if (effects.OfType<TerrainGateEffect>().Count() > 1 || effects.OfType<RemoveTerrainEffect>().Count() > 1)
            throw new ArgumentException("A move can declare at most one terrainGate and one removeTerrain op.");
        if (effects.OfType<WeatherMoveEffect>().Any() && effects.OfType<TerrainMoveEffect>().Any())
            throw new ArgumentException("A move cannot combine weatherMove and terrainMove query rows.");
        if (effects.OfType<DamageClassQueryEffect>().Any() && move.DamageClass == DamageClass.Status)
            throw new ArgumentException("damageClassQuery requires a damaging authored class.");
        if (effects.OfType<DamageClassQueryEffect>().Any(effect => effect.Mode == DamageClassQueryMode.HigherOffense)
            && effects.OfType<DamageStatQueryEffect>().Any())
            throw new ArgumentException("higherOffense class selection cannot combine with explicit damage-stat selectors.");
        if (effects.OfType<EffectivenessQueryEffect>().Any() && move.DamageClass == DamageClass.Status)
            throw new ArgumentException("effectivenessQuery requires a damaging authored class.");
        if (effects.OfType<MoveQueryModifierEffect>().Any(effect =>
                effect.Query is BattleQueryId.CriticalChance or BattleQueryId.FinalDamage)
            && move.DamageClass == DamageClass.Status)
            throw new ArgumentException("Critical-chance and final-damage query modifiers require a damaging move.");
        if (effects.OfType<MoveQueryModifierEffect>().Any(effect => effect.Query == BattleQueryId.Healing)
            && !effects.Any(effect => effect is DrainEffect or HealEffect
                or HpFractionEffect { Operation: HpFractionOperation.Heal } or DelayedHealEffect))
            throw new ArgumentException("Healing query modifiers require move-originated healing.");
        if (effects.OfType<OneShotQueryEffect>().SingleOrDefault() is { } oneShot)
        {
            if (move.DamageClass != DamageClass.Status)
                throw new ArgumentException("nextQuery requires a status move.");
            if (oneShot.Query == OneShotQuery.CriticalChance && move.Target != MoveTarget.User)
                throw new ArgumentException("A criticalChance nextQuery requires the user target.");
            if (oneShot.Query == OneShotQuery.Accuracy && move.Target != MoveTarget.Selected)
                throw new ArgumentException("An accuracy nextQuery requires the selected target.");
        }
        if (effects.OfType<MoveGateEffect>().Any(gate => gate.Kind is MoveGateKind.SourceBeforeTarget
                or MoveGateKind.SourceAfterTarget or MoveGateKind.TargetAction)
            && move.Target != MoveTarget.Selected)
            throw new ArgumentException("Target-relative moveGate rows require the selected target.");
        if (effects.OfType<QueueActionGateEffect>().Any(gate => gate.Owner == QueueActionGateOwner.Creature)
            && move.DamageClass == DamageClass.Status)
            throw new ArgumentException("recharge requires a damaging move.");
        if (charge is null && effects.OfType<WeatherMoveEffect>().Any(effect => effect.SkipChargeWeather.Count > 0))
            throw new ArgumentException("weatherMove skipCharge requires chargeTurn.");
        if (charge is null && effects.OfType<ChargeStartStatEffect>().Any())
            throw new ArgumentException("chargeStartStat requires chargeTurn.");
        if (charge is not null && move.Target is (MoveTarget.FaintingPokemon or MoveTarget.SpecificMove))
            throw new ArgumentException("chargeTurn does not support party-member or move-reference targets.");
        if (multiTurnLock is not null && (move.DamageClass == DamageClass.Status || move.Power is null))
            throw new ArgumentException("multiTurnLock requires a damaging move with authored power.");
        if (multiTurnLock is not null && charge is not null)
            throw new ArgumentException("multiTurnLock cannot be combined with chargeTurn.");
        BattleDelayedMechanics.Validate(effects, move.DamageClass, move.Power, move.Target, selfDestruct,
            replacementPowerFormulas > 0);
        int replacementIndex = effects.FindIndex(effect => effect is ReplacementRestoreEffect);
        int selfDestructIndex = effects.FindIndex(effect => effect is SelfDestructEffect);
        if (replacementIndex >= 0 && replacementIndex > selfDestructIndex)
            throw new ArgumentException("replacementRestore must execute before selfDestruct.");
        if (move.DamageClass != DamageClass.Status && move.Power is null && replacementPowerFormulas == 0
            && fixedDamage is null && !fixedDamageLevel && !ohko && counterCategory is null
            && !effects.OfType<HpFractionEffect>().Any(effect => effect.Operation == HpFractionOperation.Damage)
            && !effects.OfType<HpEqualizeEffect>().Any(effect => effect.Mode == HpEqualizeMode.MatchSource))
            throw new ArgumentException("Damaging moves without authored power require a replacement base-power formula.");

        return new BattleMove(move.Id, move.Type, move.DamageClass, move.Power, move.Accuracy, move.Pp,
            move.Priority, move.CritStage, ailment, ailmentChance, stageEffects.FirstOrDefault(), confuseChance, flinchChance,
            drain, recoil, recoilOnMiss, heal, multiHitMin, multiHitMax,
            fixedDamage, fixedDamageLevel, ohko, critBoost, selfDestruct, leechSeed, setsWeather,
            binds, forcesSwitch, counterCategory, bypassAccuracy, charge is not null,
            multiTurnLock is not null, move.MakesContact, stageEffects: stageEffects, target: move.Target,
            stageAllEffect: stageAllEffect, secondaryEffects: effects,
            offensiveStatOverride: offensiveStatOverride, defensiveStatOverride: defensiveStatOverride,
            targetHpThresholdPower: targetHpThresholdPower, hpRatioPower: hpRatioPower,
            hpBandPower: hpBandPower, charge: charge, multiTurnLockProfile: multiTurnLock, tags: moveTags.ToArray());
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

    private static T ParseNamed<T>(string value, string what) where T : struct, Enum =>
        !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
        && Enum.TryParse(value, ignoreCase: true, out T result)
        && Enum.IsDefined(result)
            ? result
            : throw new ArgumentException($"Unknown {what} '{value}'.");

    private static IReadOnlyList<EntityId> ParseTypeList(string value)
    {
        EntityId[] types = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(EntityId.Parse).ToArray();
        if (types.Length == 0 || types.Any(type => type.Category != EntityCategory.Type)
            || types.Distinct().Count() != types.Length)
            throw new ArgumentException("typeMutation 'types' must be a unique nonempty list of type IDs.");
        return types;
    }

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

    private static void ValidateHazardOp(Move move, Effect effect, params string[] parameters)
    {
        if ((effect.Chance ?? 100) != 100)
            throw new ArgumentException($"{effect.Op} does not support chance.");
        if (move.DamageClass != DamageClass.Status)
            throw new ArgumentException($"{effect.Op} requires a status move.");
        if (move.Target is not (MoveTarget.OpponentsField or MoveTarget.UsersField))
            throw new ArgumentException($"{effect.Op} requires a side-field target.");
        CheckAllowedParams(effect, parameters);
    }

    private static EntryHazardProfile ParseDamageHazard(Effect effect)
    {
        string key = Str(effect, "key");
        int maximumLayers = Int(effect, "maxLayers");
        bool hasFractions = effect.Params?.ContainsKey("fractions") == true;
        bool hasType = effect.Params?.ContainsKey("type") == true;
        if (hasFractions == hasType)
            throw new ArgumentException("entryHazardDamage requires exactly one of fractions or type.");
        if (hasFractions)
        {
            IReadOnlyList<Fraction> fractions = ParseFractions(Str(effect, "fractions"), "entryHazardDamage fractions");
            if (fractions.Count != maximumLayers)
                throw new ArgumentException("entryHazardDamage fractions must contain one row per layer.");
            if (effect.Params!.ContainsKey("num") || effect.Params.ContainsKey("den"))
                throw new ArgumentException("Layer-scaled entryHazardDamage does not accept num or den.");
            return EntryHazardConditions.LayeredDamage(key, fractions, RequiredBool(effect, "groundedOnly"));
        }

        if (maximumLayers != 1)
            throw new ArgumentException("Type-scaled entryHazardDamage supports exactly one layer.");
        bool hasNum = effect.Params?.ContainsKey("num") == true;
        bool hasDen = effect.Params?.ContainsKey("den") == true;
        if (hasNum != hasDen)
            throw new ArgumentException("Type-scaled entryHazardDamage requires both num and den when either is supplied.");
        EntityId type = ParseTypeId(Str(effect, "type"), "entryHazardDamage type");
        return EntryHazardConditions.TypeScaledDamage(key, type, ReadFraction(effect, 1, 8),
            RequiredBool(effect, "groundedOnly"));
    }

    private static EntryHazardProfile ParseStatusHazard(Effect effect)
    {
        int maximumLayers = Int(effect, "maxLayers");
        PersistentStatus[] statuses = Str(effect, "statuses")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => Parse<PersistentStatus>(value, "entry-hazard status"))
            .ToArray();
        if (statuses.Length != maximumLayers)
            throw new ArgumentException("entryHazardStatus statuses must contain one row per layer.");
        EntityId[] parsedAbsorbTypes = effect.Params?.ContainsKey("absorbTypes") == true
            ? Str(effect, "absorbTypes").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => ParseTypeId(value, "entryHazardStatus absorbTypes")).ToArray()
            : [];
        if (parsedAbsorbTypes.Distinct().Count() != parsedAbsorbTypes.Length)
            throw new ArgumentException("entryHazardStatus absorbTypes must not contain duplicates.");
        HashSet<EntityId> absorbTypes = [.. parsedAbsorbTypes];
        if (effect.Params?.ContainsKey("absorbTypes") == true && absorbTypes.Count == 0)
            throw new ArgumentException("entryHazardStatus absorbTypes cannot be empty.");
        return EntryHazardConditions.Status(Str(effect, "key"), statuses, absorbTypes,
            RequiredBool(effect, "groundedOnly"));
    }

    private static EntryHazardProfile ParseStageHazard(Effect effect) => EntryHazardConditions.Stage(
        Str(effect, "key"), ParseStat(Str(effect, "stat")), Int(effect, "delta"),
        RequiredBool(effect, "groundedOnly"));

    private static IReadOnlyList<ProtectionContactEffect> ParseProtectionContact(string value)
    {
        string[] rows = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (rows.Length == 0)
            throw new ArgumentException("Protection contact payload cannot be empty.");
        var effects = new List<ProtectionContactEffect>(rows.Length);
        foreach (string row in rows)
        {
            string[] parts = row.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts[1].Length == 0)
                throw new ArgumentException("Protection contact rows require kind:value.");
            switch (parts[0])
            {
                case "damage":
                    string[] fraction = parts[1].Split('/', StringSplitOptions.TrimEntries);
                    if (fraction.Length != 2
                        || !int.TryParse(fraction[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int num)
                        || !int.TryParse(fraction[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int den))
                        throw new ArgumentException("Protection contact damage requires num/den.");
                    effects.Add(new ProtectionContactDamage(new Fraction(num, den)));
                    break;
                case "status":
                    effects.Add(new ProtectionContactStatus(Parse<PersistentStatus>(parts[1], "protection contact status")));
                    break;
                case "stage":
                    string[] stage = parts[1].Split('/', StringSplitOptions.TrimEntries);
                    if (stage.Length != 2
                        || !int.TryParse(stage[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int delta))
                        throw new ArgumentException("Protection contact stage requires stat/delta.");
                    effects.Add(new ProtectionContactStage(ParseStat(stage[0]), delta));
                    break;
                default:
                    throw new ArgumentException($"Unknown protection contact kind '{parts[0]}'.");
            }
        }
        return effects;
    }

    private static BattleConditionSelector ParseConditionSelector(Effect effect)
    {
        BattleConditionScope scope = ParseNamed<BattleConditionScope>(Str(effect, "scope"), "condition scope");
        BattleConditionId? condition = effect.Params?.ContainsKey("condition") == true
            ? new BattleConditionId(Str(effect, "condition")) : null;
        string? tag = effect.Params?.ContainsKey("tag") == true ? Str(effect, "tag") : null;
        bool all = effect.Params?.ContainsKey("all") == true && RequiredBool(effect, "all");
        int modes = (condition is null ? 0 : 1) + (tag is null ? 0 : 1) + (all ? 1 : 0);
        if (modes != 1 || tag is not null && !BattleConditionId.ValidToken(tag))
            throw new ArgumentException("Condition mutation requires exactly one valid condition, tag, or all:true selector.");
        BattleConditionSourceTarget source = effect.Params?.ContainsKey("source") == true
            ? ParseNamed<BattleConditionSourceTarget>(Str(effect, "source"), "condition source")
            : BattleConditionSourceTarget.Any;
        return new BattleConditionSelector(scope, condition, tag, all, source);
    }

    private static SideConditionTarget ParseConditionOwner(string value) => value switch
    {
        "user" => SideConditionTarget.Source,
        "target" => SideConditionTarget.Target,
        _ => throw new ArgumentException($"Unknown condition owner '{value}'."),
    };

    private static IReadOnlyList<Fraction> ParseFractions(string value, string label)
    {
        Fraction[] fractions = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(row => row.Split('/', StringSplitOptions.TrimEntries))
            .Select(parts => parts.Length == 2
                && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int num)
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int den)
                ? new Fraction(num, den)
                : throw new ArgumentException($"{label} must use comma-separated num/den rows."))
            .ToArray();
        return fractions.Length > 0 ? fractions : throw new ArgumentException($"{label} cannot be empty.");
    }

    private static IReadOnlyList<EntityId> ParseMoveIds(string value, string label)
    {
        EntityId[] ids = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => EntityId.TryParse(token, out EntityId id) && id.Category == EntityCategory.Move
                ? id : throw new ArgumentException($"{label} requires comma-separated move EntityIds."))
            .ToArray();
        if (ids.Length == 0 || ids.Distinct().Count() != ids.Length)
            throw new ArgumentException($"{label} requires at least one unique move EntityId.");
        return Array.AsReadOnly(ids);
    }

    private static IReadOnlyDictionary<BattleEnvironment, EntityId> ParseEnvironmentMoves(string value)
    {
        var rows = new Dictionary<BattleEnvironment, EntityId>();
        foreach (string row in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = row.Split('=', StringSplitOptions.TrimEntries);
            if (parts.Length != 2
                || !Enum.TryParse(parts[0], true, out BattleEnvironment environment)
                || !Enum.IsDefined(environment)
                || !EntityId.TryParse(parts[1], out EntityId move) || move.Category != EntityCategory.Move
                || !rows.TryAdd(environment, move))
                throw new ArgumentException("callMove environment requires unique environment=move:id rows.");
        }
        return rows.Count > 0 ? rows
            : throw new ArgumentException("callMove environment cannot be empty.");
    }

    private static IReadOnlySet<string> ParseLowercaseTokens(string value, string label)
    {
        string[] tokens = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0 || tokens.Any(token => !BattleConditionId.ValidToken(token))
            || tokens.Distinct(StringComparer.Ordinal).Count() != tokens.Length)
            throw new ArgumentException($"{label} requires unique lowercase tokens.");
        return new HashSet<string>(tokens, StringComparer.Ordinal);
    }

    private static string LowercaseToken(string value, string label) => BattleConditionId.ValidToken(value)
        ? value : throw new ArgumentException($"{label} requires one lowercase token.");

    private static IReadOnlyList<PairedActionOption> ParsePairedOptions(string value)
    {
        var options = new List<PairedActionOption>();
        foreach (string row in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = row.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length != 3 || !BattleConditionId.ValidToken(parts[0])
                || !Enum.TryParse(parts[2], true, out PairedActionSideEffect sideEffect)
                || !Enum.IsDefined(sideEffect))
                throw new ArgumentException("pairedAction pairs require partner:type-or-none:side-effect rows.");
            EntityId? type = parts[1] == "none" ? null : ParseTypeId(parts[1], "pairedAction type");
            options.Add(new PairedActionOption(parts[0], type, sideEffect));
        }
        return options.Count > 0 ? options
            : throw new ArgumentException("pairedAction pairs cannot be empty.");
    }

    private static EntityId ParseTypeId(string value, string label) => BattleConditionId.ValidToken(value)
        ? EntityId.Parse($"type:{value}")
        : throw new ArgumentException($"{label} must be a lowercase type slug.");

    private static bool RequiredBool(Effect effect, string key) => Field(effect, key).GetBoolean();

    private static bool OptionalBool(Effect effect, string key) =>
        effect.Params?.ContainsKey(key) == true && RequiredBool(effect, key);

    private static bool IsSingleActiveCreatureTarget(MoveTarget target) => target is
        MoveTarget.Selected or MoveTarget.Ally or MoveTarget.RandomOpponent;

    private static bool IsActiveCreatureTarget(MoveTarget target) => target is not
        (MoveTarget.UsersField or MoveTarget.OpponentsField or MoveTarget.EntireField
            or MoveTarget.FaintingPokemon or MoveTarget.SpecificMove);

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

    private static DamageQueryOwner ParseDamageOwner(Effect effect, string key, DamageQueryOwner fallback) =>
        effect.Params?.ContainsKey(key) == true
            ? ParseNamed<DamageQueryOwner>(Str(effect, key), key)
            : fallback;

    private static EntityId ParseTypeEntityId(string value, string label)
    {
        EntityId id = value.Contains(':', StringComparison.Ordinal)
            ? EntityId.Parse(value) : ParseTypeId(value, label);
        return id.Category == EntityCategory.Type ? id
            : throw new ArgumentException($"{label} must identify a type entity.");
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

    private static IReadOnlySet<Weather> ParseWeatherList(Effect effect, string key, string op = "weatherAccuracy")
    {
        if (effect.Params?.ContainsKey(key) != true)
            return new HashSet<Weather>();
        Weather[] values = Str(effect, key)
            .Split(',', StringSplitOptions.TrimEntries)
            .Select(value => ParseWeather(value, key))
            .ToArray();
        if (values.Length == 0 || values.Contains(Weather.None) || values.Distinct().Count() != values.Length)
            throw new ArgumentException($"{op} {key} must contain unique active weather values.");
        return new HashSet<Weather>(values);
    }

    private static IReadOnlyDictionary<Weather, EntityId> ParseWeatherTypes(Effect effect)
    {
        if (effect.Params?.ContainsKey("types") != true)
            return new Dictionary<Weather, EntityId>();
        var result = new Dictionary<Weather, EntityId>();
        foreach (string entry in Str(effect, "types").Split(',', StringSplitOptions.TrimEntries))
        {
            string[] row = entry.Split(':', StringSplitOptions.TrimEntries);
            if (row.Length != 2 || !TryParseWeather(row[0], out Weather weather)
                || !BattleConditionId.ValidToken(row[1]) || !result.TryAdd(weather, EntityId.Parse($"type:{row[1]}")))
                throw new ArgumentException("weatherMove types require unique weather:type-slug rows.");
        }
        return result.Count > 0 ? result : throw new ArgumentException("weatherMove types cannot be empty.");
    }

    private static IReadOnlyDictionary<Weather, Fraction> ParseWeatherPower(Effect effect)
    {
        if (effect.Params?.ContainsKey("power") != true)
            return new Dictionary<Weather, Fraction>();
        var result = new Dictionary<Weather, Fraction>();
        foreach (string entry in Str(effect, "power").Split(',', StringSplitOptions.TrimEntries))
        {
            string[] row = entry.Split(':', StringSplitOptions.TrimEntries);
            string[] ratio = row.Length == 2 ? row[1].Split('/', StringSplitOptions.TrimEntries) : [];
            if (row.Length != 2 || ratio.Length != 2 || !TryParseWeather(row[0], out Weather weather)
                || !int.TryParse(ratio[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int num)
                || !int.TryParse(ratio[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int den)
                || num <= 0 || den <= 0 || !result.TryAdd(weather, new Fraction(num, den)))
                throw new ArgumentException("weatherMove power requires unique active weather:num/den rows with positive ratios.");
        }
        return result.Count > 0 ? result : throw new ArgumentException("weatherMove power cannot be empty.");
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

    private static IReadOnlySet<Terrain> ParseTerrainList(Effect effect, string key)
    {
        if (effect.Params?.ContainsKey(key) != true)
            return new HashSet<Terrain>();
        Terrain[] values = Str(effect, key).Split(',', StringSplitOptions.TrimEntries)
            .Select(value => ParseTerrain(value, key)).ToArray();
        if (values.Length == 0 || values.Contains(Terrain.None) || values.Distinct().Count() != values.Length)
            throw new ArgumentException($"terrainMove {key} must contain unique active terrain values.");
        return new HashSet<Terrain>(values);
    }

    private static IReadOnlyDictionary<Terrain, EntityId> ParseTerrainTypes(Effect effect)
    {
        if (effect.Params?.ContainsKey("types") != true)
            return new Dictionary<Terrain, EntityId>();
        var result = new Dictionary<Terrain, EntityId>();
        foreach (string entry in Str(effect, "types").Split(',', StringSplitOptions.TrimEntries))
        {
            string[] row = entry.Split(':', StringSplitOptions.TrimEntries);
            if (row.Length != 2 || !TryParseTerrain(row[0], out Terrain terrain)
                || !BattleConditionId.ValidToken(row[1]) || !result.TryAdd(terrain, EntityId.Parse($"type:{row[1]}")))
                throw new ArgumentException("terrainMove types require unique terrain:type-slug rows.");
        }
        return result.Count > 0 ? result : throw new ArgumentException("terrainMove types cannot be empty.");
    }

    private static IReadOnlyDictionary<Terrain, Fraction> ParseTerrainPower(Effect effect) =>
        ParseTerrainRatioTable(effect, "power", "terrainMove power", maximumOne: false)
        ?? new Dictionary<Terrain, Fraction>();

    private static IReadOnlyDictionary<Terrain, int> ParseTerrainPriority(Effect effect, int authoredPriority)
    {
        if (effect.Params?.ContainsKey("priority") != true)
            return new Dictionary<Terrain, int>();
        var result = new Dictionary<Terrain, int>();
        foreach (string entry in Str(effect, "priority").Split(',', StringSplitOptions.TrimEntries))
        {
            string[] row = entry.Split(':', StringSplitOptions.TrimEntries);
            if (row.Length != 2 || !TryParseTerrain(row[0], out Terrain terrain)
                || !int.TryParse(row[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int delta)
                || authoredPriority + delta is < -7 or > 7 || !result.TryAdd(terrain, delta))
                throw new ArgumentException("terrainMove priority requires unique terrain:delta rows with effective priority in -7..7.");
        }
        return result.Count > 0 ? result : throw new ArgumentException("terrainMove priority cannot be empty.");
    }

    private static IReadOnlyDictionary<Terrain, Fraction>? ParseTerrainFractions(Effect effect) =>
        ParseTerrainRatioTable(effect, "terrain", "heal terrain", maximumOne: true);

    private static IReadOnlyDictionary<Terrain, Fraction>? ParseTerrainRatioTable(
        Effect effect, string key, string label, bool maximumOne)
    {
        if (effect.Params?.ContainsKey(key) != true)
            return null;
        var result = new Dictionary<Terrain, Fraction>();
        foreach (string entry in Str(effect, key).Split(',', StringSplitOptions.TrimEntries))
        {
            string[] row = entry.Split(':', StringSplitOptions.TrimEntries);
            string[] ratio = row.Length == 2 ? row[1].Split('/', StringSplitOptions.TrimEntries) : [];
            if (row.Length != 2 || ratio.Length != 2 || !TryParseTerrain(row[0], out Terrain terrain)
                || !int.TryParse(ratio[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int num)
                || !int.TryParse(ratio[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int den)
                || num <= 0 || den <= 0 || maximumOne && num > den
                || !result.TryAdd(terrain, new Fraction(num, den)))
                throw new ArgumentException($"{label} requires unique active terrain:num/den rows with positive ratios{(maximumOne ? " in (0,1]" : "")}.");
        }
        return result.Count > 0 ? result : throw new ArgumentException($"{label} cannot be empty.");
    }

    private static Terrain ParseTerrain(string value, string what) => TryParseTerrain(value, out Terrain terrain)
        ? terrain
        : throw new ArgumentException($"Unknown {what} '{value}'.");

    private static bool TryParseTerrain(string value, out Terrain terrain)
    {
        terrain = Terrain.None;
        return !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            && Enum.TryParse(value, true, out terrain) && terrain != Terrain.None && Enum.IsDefined(terrain);
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
