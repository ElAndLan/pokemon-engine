using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public sealed record SmartAiWeights
{
    public double KoBonus { get; init; } = 1000;
    public double StatusValue { get; init; } = 60;
    public double SetupValue { get; init; } = 40;
    public double HazardValue { get; init; } = 35;
    public double ProtectValue { get; init; } = 45;
    public double ForceSwitchValue { get; init; } = 90;
    public double ItemHealThreshold { get; init; } = 0.25;
    // A voluntary switch must improve the matchup by this relative margin after tempo/hazard costs.
    public double SwitchThreshold { get; init; } = 100;
    // Advanced-tier score noise. Spec band 5–10%; measured: 0.10 threw ~7% of mirror games to chance
    // (53.5%→61.0% vs Basic when halved), so 0.05 — decisive but still non-mechanical on close calls.
    public double NoiseFraction { get; init; } = 0.05;
}

public sealed record TrainerBattleItem(EntityId Item, int Count, int HealAmount);

public sealed class SmartAiMemory
{
    public int LastVoluntarySwitchTurn { get; private set; } = -99;
    public EntityId? LastPlayerMove { get; private set; }
    public int RepeatedPlayerMoveCount { get; private set; }
    public HashSet<EntityId> SeenPlayerMoves { get; } = [];

    public void ObservePlayerAction(BattleAction action, BattleCreature player)
    {
        if (action is not UseMove use || use.MoveIndex < 0 || use.MoveIndex >= player.Moves.Count)
            return;

        EntityId move = player.Moves[use.MoveIndex].Move;
        RepeatedPlayerMoveCount = move == LastPlayerMove ? RepeatedPlayerMoveCount + 1 : 1;
        LastPlayerMove = move;
        SeenPlayerMoves.Add(move);
    }

    public void MarkVoluntarySwitch(int turn) => LastVoluntarySwitchTurn = turn;
}

public sealed record SmartAiContext(
    IReadOnlyList<BattleCreature> EnemyParty,
    int EnemyActive,
    IReadOnlyList<BattleCreature> PlayerParty,
    int PlayerActive,
    TypeChart Chart,
    IRng Rng,
    IReadOnlyList<TrainerBattleItem>? Items = null,
    int Turn = 0,
    SmartAiMemory? Memory = null,
    SmartAiWeights? Weights = null,
    BattleOverlayStore? Overlays = null,
    BattleActionHistory? ActionHistory = null,
    IReadOnlyDictionary<EntityId, Item>? ItemData = null,
    IReadOnlyList<BattleConditionInstance>? Conditions = null,
    string Ruleset = BattleRulesets.Gen4Like,
    BattleEnvironment NaturalEnvironment = BattleEnvironment.Building,
    int ActiveSlotsPerSide = 1,
    int SnapshottedLiveTargets = 1)
{
    public BattleEnvironmentState Environment => BattleEnvironmentState.Resolve(NaturalEnvironment, Conditions);
}

public sealed record AiScoreComponent(string Name, double Value);
public sealed record AiCandidateScore(BattleAction Action, double Score, IReadOnlyList<AiScoreComponent> Components);
public sealed record SmartAiDecision(BattleAction Action, IReadOnlyList<AiCandidateScore> Scores);

public static class SmartAi
{
    private const int MidpointRoll = 92;

    public static int ChooseMove(BattleCreature attacker, BattleCreature defender, TypeChart chart, IRng rng,
        SmartAiWeights? weights = null) =>
        ((UseMove)ChooseAction(new SmartAiContext([attacker], 0, [defender], 0, chart, rng, Weights: weights)).Action).MoveIndex;

    public static SmartAiDecision ChooseAction(SmartAiContext context)
    {
        if (context.SnapshottedLiveTargets <= 0
            || context.SnapshottedLiveTargets > context.ActiveSlotsPerSide)
            throw new ArgumentOutOfRangeException(nameof(context),
                "Snapshotted live targets must fit the active battle topology.");
        SmartAiWeights weights = context.Weights ?? new SmartAiWeights();
        BattleCreature active = context.EnemyParty[context.EnemyActive];
        BattleCreature target = context.PlayerParty[context.PlayerActive];
        if (active.ChargingMoveIndex is { } releaseIndex)
        {
            var release = new AiCandidateScore(new UseMove(releaseIndex), 0,
                [new("chargeRelease", 0)]);
            return new SmartAiDecision(release.Action, [release]);
        }
        var scores = new List<AiCandidateScore>();
        bool previousActionFailed = context.ActionHistory?.PreviousActionFailed(
            new BattleHistoryOwner(BattleSide.Enemy, context.EnemyActive,
                new BattleSlot(BattleSide.Enemy, 0))) == true;

        for (int i = 0; i < active.Moves.Count; i++)
            if (active.Moves[i].HasPp && BattleActionGates.SourceHistoryAllows(
                    active.Moves[i], active, previousActionFailed))
                scores.Add(ScoreMove(new UseMove(i), active, target, context, weights, context.Rng));

        scores.AddRange(ScoreItems(context, weights));

        if (CanSwitch(active) && context.Turn - (context.Memory?.LastVoluntarySwitchTurn ?? -99) >= 3)
            scores.AddRange(ScoreSwitches(context, weights, BestMoveScore(scores)));

        if (scores.Count == 0 && active.Moves.Any(move => move.HasPp))
            scores.Add(new AiCandidateScore(new Pass(), 0, [new("actionGateFallback", 0)]));
        else if (scores.Count == 0)
            scores.Add(new AiCandidateScore(new UseMove(FirstUsableOrZero(active)), 0, [new("fallback", 0)]));

        AiCandidateScore best = scores.OrderByDescending(s => s.Score).First();
        if (best.Action is Switch && context.Memory is not null)
            context.Memory.MarkVoluntarySwitch(context.Turn);
        return new SmartAiDecision(best.Action, scores);
    }

    private static AiCandidateScore ScoreMove(UseMove action, BattleCreature attacker, BattleCreature defender,
        SmartAiContext context, SmartAiWeights weights, IRng rng)
    {
        BattleMove move = attacker.Moves[action.MoveIndex];
        var c = new List<AiScoreComponent>();
        if (defender.SemiInvulnerableState is { } state
            && !move.SecondaryEffects.OfType<SemiInvulnerableHitEffect>()
                .Any(effect => effect.States.Contains(state)))
        {
            c.Add(new("semiInvulnerableMiss", -1_000_000));
            return new AiCandidateScore(action, c[0].Value, c);
        }
        if (move.SecondaryEffects.OfType<TerrainGateEffect>().Any() && ActiveTerrain(context) == Terrain.None)
        {
            c.Add(new("terrainGate", -1_000_000));
            return new AiCandidateScore(action, c[0].Value, c);
        }
        if (move.SecondaryEffects.OfType<FieldMoveGateEffect>().Any(gate =>
            context.Conditions is not null && FieldConditions.Active(context.Conditions, gate.Condition)))
        {
            c.Add(new("fieldMoveGate", -1_000_000));
            return new AiCandidateScore(action, c[0].Value, c);
        }

        PhysicalFormulaInputs? inputs = !PhysicalMetricFormulas.HasPowerFormula(move) ? null
            : PhysicalInputs(attacker, defender, context);
        BattleActionFormulaInputs? actionInputs = null;
        if (ActionHistoryFormulas.HasPowerFormula(move))
        {
            int sourceSpeed = AiSpeed(attacker, BattleSide.Enemy, context.EnemyActive, context);
            int targetSpeed = AiSpeed(defender, BattleSide.Player, context.PlayerActive, context);
            actionInputs = (context.ActionHistory ?? new BattleActionHistory()).PreviewPowerInputs(
                new BattleHistoryOwner(BattleSide.Enemy, context.EnemyActive, new BattleSlot(BattleSide.Enemy, 0)),
                new BattleHistoryOwner(BattleSide.Player, context.PlayerActive, new BattleSlot(BattleSide.Player, 0)),
                move.Move, ActsBefore(sourceSpeed, targetSpeed, context), ActsBefore(targetSpeed, sourceSpeed, context));
        }
        bool hasResourceInputs = TryResourceInputs(attacker, defender, move, context.EnemyParty,
            context.EnemyActive, BattleSide.Enemy, context, out PartyResourceFormulaInputs? resourceInputs);
        FieldMovePreview weatherMove = PreviewFieldMove(context, move, attacker, defender);
        double damage = hasResourceInputs
            ? ExpectedDamage(attacker, defender, move, context.Chart, inputs, actionInputs, resourceInputs,
                weatherMove, WeatherDamageModifiers(context, weatherMove.Type), context.Conditions, context.Ruleset,
                context)
            : 0;
        if (!TerrainAllowsPriorityHit(context, move, attacker, defender))
            damage = 0;
        int authoredAccuracy = move.Ohko ? EffectMath.OhkoAccuracy(attacker.Level, defender.Level) : move.Accuracy ?? 100;
        BattleHookDispatchSnapshot? weatherAccuracy = WeatherAccuracyHooks(context, move);
        bool weatherBypass = weatherAccuracy?.Filters().Any(filter => filter is
            { Filter.Value: "accuracy_bypass", Decision: BattleHookFilterDecision.Allow }) == true;
        bool baseBypass = move.BypassAccuracy || (!move.Ohko && move.Accuracy is null) || weatherBypass;
        var accuracyModifiers = weatherAccuracy?.QueryModifiers(BattleQueryId.Accuracy).ToList() ?? [];
        if (context.Conditions is not null)
            accuracyModifiers.AddRange(FieldConditions.CollectAccuracyHooks(context.Conditions, 0)
                .QueryModifiers(BattleQueryId.Accuracy));
        bool guaranteedAccuracy = context.Conditions is not null
            && OneShotQueryConditions.FindAccuracy(context.Conditions,
                CreatureOwner(BattleSide.Player, context.PlayerActive),
                CreatureSource(BattleSide.Enemy, context.EnemyActive)) is not null;
        BattleAccuracyQueryResult accuracyQuery = BattleActionQueries.Accuracy(move, authoredAccuracy,
            attacker, defender, baseBypass, guaranteedAccuracy, accuracyModifiers,
            new BattleQueryContext(new BattleSlot(BattleSide.Enemy, 0), attacker,
                new BattleSlot(BattleSide.Player, 0), defender, ActiveWeather(context), context.Ruleset,
                ActiveTerrain(context)));
        double accuracy = accuracyQuery.Bypass ? 1 : accuracyQuery.Query.FinalValue.ToInt32() / 100.0;
        c.Add(new("damage", damage * accuracy));
        if (damage >= defender.CurrentHp && damage > 0)
            c.Add(new("ko", weights.KoBonus * accuracy));

        if (move.Ailment is { } ailment && move.AilmentChance > 0
            && StatusEffects.CanApplyStatus(defender.Status)
            && !StatusEffects.TypeImmuneToStatus(ailment, defender.Types)
            && WeatherAllowsStatus(context, ailment)
            && TerrainAllowsStatus(context, ailment, defender)
            && SideAllowsStatus(context, attacker, move)
            && SideAllowsHit(context, attacker, defender, move)
            && TerrainAllowsPriorityHit(context, move, attacker, defender))
        {
            AilmentEffect? effect = move.SecondaryEffects.OfType<AilmentEffect>().FirstOrDefault();
            int chance = effect is null ? move.AilmentChance
                : SecondaryChance(context, move, effect, attacker, defender);
            c.Add(new("status", weights.StatusValue * HpFraction(defender) * chance / 100.0));
        }

        if (move.StageEffect is { OnSelf: true, Delta: > 0 } setup
            && !ThreatensKo(defender, attacker, context.Chart, context))
            c.Add(new("setup", weights.SetupValue * setup.Delta));

        if (move.SecondaryEffects.OfType<SetEntryHazardEffect>().Any(effect =>
                CanAddHazard(context, BattleSide.Player, effect.Hazard))
            && context.PlayerParty.Count(p => !p.IsFainted) > 1)
            c.Add(new("hazard", weights.HazardValue * (context.PlayerParty.Count(p => !p.IsFainted) - 1)));

        if (move.SecondaryEffects.OfType<ProtectEffect>().SingleOrDefault() is { } protection
            && CanApplyProtection(context, attacker, protection.Profile)
            && ThreatensKo(defender, attacker, context.Chart, context))
            c.Add(new("protect", weights.ProtectValue * ProtectionConditions.SuccessChance(
                protection.Profile, attacker.ProtectChain, context.Ruleset)));

        if (move.ForcesSwitch && HasDangerousBoost(defender))
            c.Add(new("forceSwitch", weights.ForceSwitchValue));

        if (move.Heal is { } heal && attacker.CurrentHp < attacker.MaxHp / 2)
        {
            int recovery = EffectMath.HealAmount(attacker.MaxHp, heal.Num, heal.Den);
            IReadOnlyList<BattleQueryModifier> healingModifiers = [];
            if (move.SecondaryEffects.OfType<HealEffect>().LastOrDefault(effect =>
                    effect.Recipient == HpFractionRecipient.Self) is { } healEffect)
                healingModifiers = WeatherHealingModifiers(context, healEffect, attacker.MaxHp);
            recovery = BattleActionQueries.Healing(move, recovery, healingModifiers,
                new BattleQueryContext(new BattleSlot(BattleSide.Enemy, 0), attacker,
                    new BattleSlot(BattleSide.Enemy, 0), attacker, ActiveWeather(context),
                    context.Ruleset, ActiveTerrain(context))).FinalValue.ToInt32();
            c.Add(new("recovery", Math.Min(recovery, attacker.MaxHp - attacker.CurrentHp)));
        }

        if (move.Recoil is { } recoil && damage > 0)
            c.Add(new("recoilRisk", -EffectMath.RecoilDamage((int)damage, recoil.Num, recoil.Den)));
        if (move.SelfDestruct)
            c.Add(new("selfKoRisk", -attacker.CurrentHp));

        c.Add(new("noise", c.Sum(x => x.Value) * ((rng.NextDouble() * 2 - 1) * weights.NoiseFraction)));
        return new AiCandidateScore(action, c.Sum(x => x.Value), c);
    }

    private static IEnumerable<AiCandidateScore> ScoreSwitches(SmartAiContext context, SmartAiWeights weights, double bestMove)
    {
        BattleCreature player = context.PlayerParty[context.PlayerActive];
        for (int i = 0; i < context.EnemyParty.Count; i++)
        {
            if (i == context.EnemyActive || context.EnemyParty[i].IsFainted)
                continue;

            BattleCreature active = context.EnemyParty[context.EnemyActive];
            BattleCreature incoming = context.EnemyParty[i];
            // Value the switch RELATIVE to staying: how much better is the incoming matchup than the
            // current one, net of the turn we forfeit and the hazard the incoming eats on entry. Absolute
            // offense would double-count what the current mon already provides and overvalue switching.
            double offenseGain = BestDamage(incoming, player, context.Chart, context, context.EnemyParty, i)
                - BestDamage(active, player, context.Chart, context, context.EnemyParty, context.EnemyActive);
            double damageAvoided = PredictedDamage(player, active, context) - PredictedDamage(player, incoming, context);
            var c = new List<AiScoreComponent>
            {
                new("stayBaseline", bestMove),
                new("switchTempo", -25),
                new("offenseGain", offenseGain),
                new("damageAvoided", damageAvoided),
                new("switchInHazard", -SwitchInHazardDamage(incoming, context)),
            };
            double relativeGain = c.Skip(1).Sum(x => x.Value);
            if (relativeGain >= weights.SwitchThreshold)
                yield return new AiCandidateScore(new Switch(i), c.Sum(x => x.Value), c);
        }
    }

    private static double BestMoveScore(IEnumerable<AiCandidateScore> scores) =>
        scores.Where(s => s.Action is UseMove).Select(s => s.Score).DefaultIfEmpty(0).Max();

    private static IEnumerable<AiCandidateScore> ScoreItems(SmartAiContext context, SmartAiWeights weights)
    {
        if (context.Items is null)
            yield break;

        BattleCreature active = context.EnemyParty[context.EnemyActive];
        if (active.IsFainted || HpFraction(active) > weights.ItemHealThreshold)
            yield break;

        foreach (TrainerBattleItem item in context.Items)
        {
            if (item.Count <= 0 || item.HealAmount <= 0)
                continue;

            int restored = Math.Min(item.HealAmount, active.MaxHp - active.CurrentHp);
            if (restored <= 0)
                continue;

            yield return new AiCandidateScore(
                new UseBattleItem(item.Item, context.EnemyActive, item.HealAmount),
                restored,
                [new("itemHeal", restored)]);
        }
    }

    private static double BestDamage(BattleCreature attacker, BattleCreature defender, TypeChart chart,
        SmartAiContext? context = null, IReadOnlyList<BattleCreature>? sourceParty = null, int sourceIndex = 0) =>
        attacker.Moves.Where(m => m.HasPp).Select(move =>
        {
            if (context is null)
                return ExpectedDamage(attacker, defender, move, chart);
            if (sourceParty is null)
            {
                FieldMovePreview weather = PreviewFieldMove(context, move, attacker, defender);
                return ExpectedDamage(attacker, defender, move, chart, weather: weather,
                    modifiers: WeatherDamageModifiers(context, weather.Type), conditions: context.Conditions,
                    ruleset: context.Ruleset, context: context);
            }
            if (!TryResourceInputs(attacker, defender, move, sourceParty, sourceIndex, BattleSide.Enemy,
                context, out PartyResourceFormulaInputs? inputs))
                return 0;
            FieldMovePreview resourceWeather = PreviewFieldMove(context, move, attacker, defender);
            return ExpectedDamage(attacker, defender, move, chart, resourceInputs: inputs,
                weather: resourceWeather, modifiers: WeatherDamageModifiers(context, resourceWeather.Type),
                conditions: context.Conditions, ruleset: context.Ruleset, context: context);
        }).DefaultIfEmpty(0).Max();

    private static double PredictedDamage(BattleCreature attacker, BattleCreature defender, SmartAiContext context)
    {
        if (context.Memory?.RepeatedPlayerMoveCount >= 2
            && context.Memory.LastPlayerMove is { } last
            && attacker.Moves.FirstOrDefault(m => m.Move == last && m.HasPp) is { } repeated)
        {
            FieldMovePreview weather = PreviewFieldMove(context, repeated, attacker, defender);
            return ExpectedDamage(attacker, defender, repeated, context.Chart, weather: weather,
                modifiers: WeatherDamageModifiers(context, weather.Type), conditions: context.Conditions,
                ruleset: context.Ruleset, context: context);
        }

        return BestDamage(attacker, defender, context.Chart, context);
    }

    private static bool ThreatensKo(BattleCreature attacker, BattleCreature defender, TypeChart chart,
        SmartAiContext? context = null) =>
        BestDamage(attacker, defender, chart, context) >= defender.CurrentHp;

    private static double ExpectedDamage(BattleCreature attacker, BattleCreature defender, BattleMove move, TypeChart chart,
        PhysicalFormulaInputs? physicalInputs = null, BattleActionFormulaInputs? actionInputs = null,
        PartyResourceFormulaInputs? resourceInputs = null,
        FieldMovePreview? weather = null,
        IReadOnlyList<BattleQueryModifier>? modifiers = null,
        IReadOnlyList<BattleConditionInstance>? conditions = null,
        string ruleset = BattleRulesets.Gen4Like,
        SmartAiContext? context = null)
    {
        BattleEffectiveValues sourceValues = EffectiveValues(attacker, context);
        BattleEffectiveValues targetValues = EffectiveValues(defender, context);
        int moveSlot = MoveSlot(attacker, move);
        EntityId? conditionType = weather is { TypeOverridden: true } ? weather.Type : null;
        BattleMoveIdentityQueryResult identity = BattleDamageQueries.Identity(move, moveSlot,
            attacker, sourceValues, context?.Environment ?? BattleEnvironmentState.Resolve(BattleEnvironment.Building),
            conditionType);
        EntityId moveType = identity.EffectiveType;
        if (context is not null && !SideAllowsHit(context, attacker, defender, move))
            return 0;
        if (conditions is not null && TerrainConditions.CanBlockPriorityTarget(move.Target)
            && TerrainConditions.CollectPriorityHooks(conditions,
                EffectivePriority(conditions, move, attacker, context), IsGrounded(defender, context), 0)
                .Filters().Any(filter => filter is
                    { Filter.Value: "priority_hit", Decision: BattleHookFilterDecision.Deny }))
            return 0;
        int liveTargets = context is not null && move.Target is MoveTarget.AllOpponents
            or MoveTarget.AllOtherPokemon or MoveTarget.AllPokemon
            ? context.SnapshottedLiveTargets : 1;
        var damageContext = new BattleQueryContext(Source: attacker, Target: defender,
            Ruleset: ruleset, Weather: context is null ? Weather.None : ActiveWeather(context),
            Terrain: context is null ? Terrain.None : ActiveTerrain(context));
        BattleDamageQueryResult damageQuery = BattleDamageQueries.Resolve(move, identity,
            attacker, defender, sourceValues, targetValues, chart, Math.Max(1, liveTargets), damageContext);
        double eff = TypeChart.ToDouble(damageQuery.Effectiveness.FinalValue);
        if (eff <= 0)
            return 0;

        if (move.SecondaryEffects.OfType<HpFractionEffect>().SingleOrDefault() is
            { Recipient: HpFractionRecipient.Target, Operation: HpFractionOperation.Damage } fraction)
        {
            int amount = EffectMath.HpFractionAmount(defender.CurrentHp, defender.MaxHp, fraction.Basis, fraction.Fraction);
            int fractionFloor = HpStatusFormulas.CannotKoFloor(move);
            return fractionFloor > 0
                ? Math.Min(amount, Math.Max(0, defender.CurrentHp - fractionFloor))
                : amount;
        }
        if (move.SecondaryEffects.OfType<HpEqualizeEffect>().SingleOrDefault() is { Mode: HpEqualizeMode.MatchSource })
            return Math.Max(0, defender.CurrentHp - attacker.CurrentHp);

        if (move.Ohko)
            return BattleActionQueries.FinalDamage(move,
                attacker.Level >= defender.Level ? defender.CurrentHp : 0, null, damageContext)
                .FinalValue.ToInt32();
        if (move.FixedDamageLevel)
            return BattleActionQueries.FinalDamage(move, attacker.Level, null, damageContext)
                .FinalValue.ToInt32();
        if (move.FixedDamage is int fixedDamage)
            return BattleActionQueries.FinalDamage(move, fixedDamage, null, damageContext)
                .FinalValue.ToInt32();
        if (!HpStatusFormulas.HasBasePower(move))
            return 0;

        if (PartyResourceFormulas.HasPowerFormula(move) && resourceInputs is null)
        {
            if (move.SecondaryEffects.OfType<ItemDataPowerEffect>().Any())
                return 0;
            int? randomPower = move.SecondaryEffects.OfType<RandomTablePowerEffect>().SingleOrDefault() is { } table
                ? PartyResourceFormulas.ExpectedWeightedPower(table.Entries)
                : null;
            resourceInputs = PartyResourceFormulas.Inputs([attacker], attacker, defender,
                move.Pp, Math.Max(0, move.Pp - 1), randomPower: randomPower);
        }

        bool physical = identity.EffectiveClass == DamageClass.Physical;
        if (physicalInputs is null && PhysicalMetricFormulas.HasPowerFormula(move) && context is not null)
            physicalInputs = PhysicalInputs(attacker, defender, context);
        HpStatusPowerQuery powerQuery = HpStatusFormulas.PowerQuery(
            move, attacker, defender, physicalInputs, actionInputs, resourceInputs,
            IsPersonallyProtected(attacker, context), IsPersonallyProtected(defender, context));
        var powerModifiers = (weather is null
            ? powerQuery.Modifiers
            : [.. powerQuery.Modifiers, .. weather.PowerModifiers]).ToList();
        if (defender.SemiInvulnerableState is { } semiState
            && move.SecondaryEffects.OfType<SemiInvulnerableHitEffect>()
                .SingleOrDefault(effect => effect.States.Contains(semiState))?.PowerMultiplier is { } semiPower)
        {
            powerModifiers.Add(new BattleQueryModifier(BattleQueryStage.SourceTargetState,
                BattleQueryOperation.Multiply, new BattleQueryValue(semiPower.Num, semiPower.Den),
                InsertionOrder: powerModifiers.Count));
        }
        if (conditions is not null)
            powerModifiers.AddRange(FieldConditions.CollectBasePowerHooks(conditions, moveType.Slug, ruleset, 0)
                .QueryModifiers(BattleQueryId.BasePower)
                .Select(modifier => modifier with { InsertionOrder = powerModifiers.Count }));
        int power = BattleQuery.ResolveInteger(BattleQueryId.BasePower, powerQuery.AuthoredBase, powerModifiers);
        DamageStatSelector attackSelector = damageQuery.Offensive with
        {
            Stat = FieldConditions.DefensiveStat(conditions ?? [], damageQuery.Offensive.Stat),
        };
        DamageStatSelector defenseSelector = damageQuery.Defensive with
        {
            Stat = FieldConditions.DefensiveStat(conditions ?? [], damageQuery.Defensive.Stat),
        };
        BattleCreature attackOwner = BattleDamageQueries.Owner(attackSelector, attacker, defender);
        BattleCreature defenseOwner = BattleDamageQueries.Owner(defenseSelector, attacker, defender);
        BattleEffectiveValues attackValues = BattleDamageQueries.Owner(attackSelector, sourceValues, targetValues);
        BattleEffectiveValues defenseValues = BattleDamageQueries.Owner(defenseSelector, sourceValues, targetValues);
        StatKind attackStat = attackSelector.Stat;
        StatKind defenseStat = defenseSelector.Stat;
        double stab = TypeChart.ToDouble(damageQuery.Stab);
        bool burn = attacker.Status == PersistentStatus.Burn && physical && !powerQuery.IgnoreSourceBurnPenalty;
        BattleSide sourceSide = BattleSide.Enemy, targetSide = BattleSide.Player;
        int sourcePartyIndex = context?.EnemyActive ?? 0;
        if (context is not null)
        {
            if (TryOwner(attacker, context, out BattleSide resolvedSourceSide, out int resolvedSourceIndex))
            {
                sourceSide = resolvedSourceSide;
                sourcePartyIndex = resolvedSourceIndex;
            }
            if (TryOwner(defender, context, out BattleSide resolvedTargetSide, out _))
                targetSide = resolvedTargetSide;
        }

        IReadOnlyList<BattleQueryModifier> criticalModifiers = conditions is null
            ? [] : SideConditions.CollectCriticalHooks(conditions, sourceSide, targetSide, 0)
                .QueryModifiers(BattleQueryId.CriticalChance);
        bool guaranteedCritical = conditions is not null && OneShotQueryConditions.FindCritical(
            conditions, CreatureOwner(sourceSide, sourcePartyIndex)) is not null;
        double ordinaryCriticalChance = TypeChart.ToDouble(BattleActionQueries.CriticalChance(move,
            move.CritStage + attacker.CritStageBonus, false, criticalModifiers, damageContext).FinalValue);
        double firstCriticalChance = guaranteedCritical
            ? TypeChart.ToDouble(BattleActionQueries.CriticalChance(move,
                move.CritStage + attacker.CritStageBonus, true, criticalModifiers, damageContext).FinalValue)
            : ordinaryCriticalChance;

        int HitDamage(bool critical)
        {
            int attackStage = attackOwner.Stage(attackStat);
            int defenseStage = defenseOwner.Stage(defenseStat);
            if (critical)
            {
                attackStage = Math.Max(0, attackStage);
                defenseStage = Math.Min(0, defenseStage);
            }
            int attack = BattleQuery.ResolveInteger(BattleQueryId.OffensiveStat,
                StatValue(attackValues.Stats, attackStat),
                [new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply,
                    BattleQuery.StatStageMultiplier(attackStage), InsertionOrder: 0),
                 .. WeatherStatModifiers(conditions, attackValues.CreatureTypes, attackStat,
                    BattleQueryId.OffensiveStat, ruleset)]);
            int defense = BattleQuery.ResolveInteger(BattleQueryId.DefensiveStat,
                StatValue(defenseValues.Stats, defenseStat),
                [new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply,
                    BattleQuery.StatStageMultiplier(defenseStage), InsertionOrder: 0),
                 .. WeatherStatModifiers(conditions, defenseValues.CreatureTypes, defenseStat,
                    BattleQueryId.DefensiveStat, ruleset)]);
            var finalModifiers = modifiers?.ToList() ?? [];
            if (conditions is not null)
            {
                finalModifiers.AddRange(TerrainConditions.CollectDamageHooks(conditions, moveType.Slug,
                    IsGrounded(attacker, context), IsGrounded(defender, context), actionSequence: 0)
                    .QueryModifiers(BattleQueryId.FinalDamage));
                finalModifiers.AddRange(SideConditions.CollectDamageHooks(conditions, targetSide,
                    identity.EffectiveClass, context?.ActiveSlotsPerSide ?? 1, critical,
                    BypassesSideConditions(attacker, move, "screen"), actionSequence: 0)
                    .QueryModifiers(BattleQueryId.FinalDamage));
            }
            return BattleActionQueries.FinalDamage(move,
                DamageCalc.Compute(attacker.Level, power, attack, defense, eff, stab, critical,
                    MidpointRoll, burn, damageQuery.Spread ? 2 : 1), finalModifiers, damageContext)
                .FinalValue.ToInt32();
        }

        int normalHit = HitDamage(critical: false);
        int criticalHit = firstCriticalChance > 0 || ordinaryCriticalChance > 0
            ? HitDamage(critical: true) : normalHit;
        double firstHit = normalHit * (1 - firstCriticalChance) + criticalHit * firstCriticalChance;
        double ordinaryHit = normalHit * (1 - ordinaryCriticalChance) + criticalHit * ordinaryCriticalChance;
        double hits = move.MultiHitMax >= 2 ? (move.MultiHitMin + move.MultiHitMax) / 2.0 : 1;
        double total = firstHit + Math.Max(0, hits - 1) * ordinaryHit;
        int floor = HpStatusFormulas.CannotKoFloor(move);
        return floor > 0 ? Math.Min(total, Math.Max(0, defender.CurrentHp - floor)) : total;
    }

    private static bool BypassesSideConditions(BattleCreature attacker, BattleMove move, string tag) =>
        move.SecondaryEffects.OfType<SideConditionBypassEffect>().Any(effect => effect.Tag == tag)
        || (tag is "screen" or "side_protection")
            && move.SecondaryEffects.OfType<RemoveSideConditionEffect>().Any(effect =>
                (effect.Tag == tag || tag == "screen" && effect.Tag == "barrier")
                && effect.Side == SideConditionTarget.Target
                && effect.Timing == SideConditionTiming.BeforeDamage)
        || attacker.AbilityHooks.SelectMany(hook => hook.Effects).Any(effect =>
            effect.Op == "sideConditionBypass"
            && effect.Params?.TryGetValue("tag", out var tagValue) == true
            && tagValue.GetString() == tag);

    private sealed record FieldMovePreview(EntityId Type, bool TypeOverridden,
        IReadOnlyList<BattleQueryModifier> PowerModifiers);

    private static FieldMovePreview PreviewFieldMove(SmartAiContext context, BattleMove move,
        BattleCreature source, BattleCreature target)
    {
        if (context.Conditions is null)
            return new(move.Type, false, []);
        if (move.SecondaryEffects.OfType<WeatherMoveEffect>().SingleOrDefault() is { } weather)
        {
            BattleHookDispatchSnapshot typeHooks = WeatherConditions.CollectMoveTypeHooks(context.Conditions, weather, 0);
            EntityId replacement = typeHooks.MoveTypes().SingleOrDefault();
            bool replaced = replacement != default;
            return new(replaced ? replacement : move.Type, replaced,
                WeatherConditions.CollectBasePowerHooks(context.Conditions, weather, 0)
                .QueryModifiers(BattleQueryId.BasePower));
        }
        if (move.SecondaryEffects.OfType<TerrainMoveEffect>().SingleOrDefault() is not { } terrain)
            return new(move.Type, false, []);
        BattleHookDispatchSnapshot terrainType = TerrainConditions.CollectMoveTypeHooks(
            context.Conditions, terrain, IsGrounded(source, context), 0);
        EntityId terrainReplacement = terrainType.MoveTypes().SingleOrDefault();
        bool terrainReplaced = terrainReplacement != default;
        return new(terrainReplaced ? terrainReplacement : move.Type, terrainReplaced,
            TerrainConditions.CollectBasePowerHooks(context.Conditions, terrain,
                IsGrounded(source, context), IsGrounded(target, context), 0)
                .QueryModifiers(BattleQueryId.BasePower));
    }

    private static IReadOnlyList<BattleQueryModifier> WeatherDamageModifiers(
        SmartAiContext context, EntityId moveType) => context.Conditions is null
        ? []
        : WeatherConditions.CollectDamageHooks(context.Conditions, moveType.Slug, actionSequence: 0)
            .QueryModifiers(BattleQueryId.FinalDamage);

    private static IReadOnlyList<BattleQueryModifier> WeatherStatModifiers(
        IReadOnlyList<BattleConditionInstance>? conditions, IReadOnlyList<EntityId> types,
        StatKind stat, BattleQueryId query, string ruleset) => conditions is null
        ? []
        : WeatherConditions.CollectStatHooks(conditions, types, stat, query, ruleset, actionSequence: 0)
            .QueryModifiers(query)
            .Select(modifier => modifier with { InsertionOrder = 1 })
            .ToArray();

    private static BattleHookDispatchSnapshot? WeatherAccuracyHooks(SmartAiContext context, BattleMove move) =>
        context.Conditions is not null
        && move.SecondaryEffects.OfType<WeatherAccuracyEffect>().SingleOrDefault() is { } effect
            ? WeatherConditions.CollectAccuracyHooks(context.Conditions, effect, actionSequence: 0)
            : null;

    private static bool WeatherAllowsStatus(SmartAiContext context, PersistentStatus status) =>
        context.Conditions is null || !WeatherConditions.CollectStatusHooks(context.Conditions, status, 0)
            .Filters().Any(filter => filter is
                { Filter.Value: "status_attempt", Decision: BattleHookFilterDecision.Deny });

    private static bool TerrainAllowsStatus(SmartAiContext context, PersistentStatus status,
        BattleCreature target) => context.Conditions is null
        || !TerrainConditions.CollectStatusHooks(context.Conditions, status, IsGrounded(target, context), 0)
            .Filters().Any(filter => filter is
                { Filter.Value: "status_attempt", Decision: BattleHookFilterDecision.Deny });

    private static bool SideAllowsStatus(SmartAiContext context, BattleCreature source, BattleMove move) =>
        context.Conditions is null
        || !SideConditions.CollectStatusHooks(context.Conditions, BattleSide.Enemy, BattleSide.Player,
            BypassesSideConditions(source, move, "status_guard"), 0).Filters().Any(filter => filter is
                { Filter.Value: "status_attempt", Decision: BattleHookFilterDecision.Deny });

    private static bool SideAllowsHit(SmartAiContext context, BattleCreature source,
        BattleCreature target, BattleMove move)
    {
        if (context.Conditions is null
            || !TryOwner(source, context, out BattleSide sourceSide, out _)
            || !TryOwner(target, context, out BattleSide targetSide, out _))
            return true;
        BattleHookDispatchSnapshot protection = SideConditions.CollectProtectionHooks(
            context.Conditions, new BattleSlot(sourceSide, 0), new BattleSlot(targetSide, 0), move,
            EffectivePriority(context.Conditions, move, source, context), context.Ruleset,
            BypassesSideConditions(source, move, "side_protection"), 0);
        return !protection.Filters().Any(filter => filter is
            { Filter.Value: "side_protection", Decision: BattleHookFilterDecision.Deny });
    }

    private static bool TerrainAllowsPriorityHit(SmartAiContext context, BattleMove move,
        BattleCreature source, BattleCreature target) => context.Conditions is null
        || !TerrainConditions.CanBlockPriorityTarget(move.Target)
        || !TerrainConditions.CollectPriorityHooks(context.Conditions,
            EffectivePriority(context.Conditions, move, source, context), IsGrounded(target, context), 0)
            .Filters().Any(filter => filter is
                { Filter.Value: "priority_hit", Decision: BattleHookFilterDecision.Deny });

    private static int EffectivePriority(IReadOnlyList<BattleConditionInstance> conditions,
        BattleMove move, BattleCreature source, SmartAiContext? context = null)
    {
        IReadOnlyList<BattleQueryModifier> modifiers =
            move.SecondaryEffects.OfType<TerrainMoveEffect>().SingleOrDefault() is { } effect
                ? TerrainConditions.CollectMovePriorityHooks(conditions, effect,
                    IsGrounded(source, context), 0).QueryModifiers(BattleQueryId.Priority)
                : [];
        return BattleActionQueries.Priority(move, modifiers,
            new BattleQueryContext(Source: source,
                Weather: context is null ? Weather.None : ActiveWeather(context),
                Ruleset: context?.Ruleset ?? BattleRulesets.Gen4Like,
                Terrain: context is null ? Terrain.None : ActiveTerrain(context))).FinalValue.ToInt32();
    }

    private static Terrain ActiveTerrain(SmartAiContext context) => context.Conditions?
        .SingleOrDefault(instance => instance.Definition.Scope == BattleConditionScope.Terrain) is { } condition
            ? TerrainConditions.For(condition.Definition.Id).Terrain
            : Terrain.None;

    private static Weather ActiveWeather(SmartAiContext context) => context.Conditions?
        .SingleOrDefault(instance => instance.Definition.Scope == BattleConditionScope.Weather) is { } condition
            ? WeatherConditions.For(condition.Definition.Id).Weather
            : Weather.None;

    private static bool IsGrounded(BattleCreature creature, SmartAiContext? context)
    {
        if (context is null || !TryOwner(creature, context, out BattleSide side, out int partyIndex))
            return TerrainConditions.GroundedQuery(creature).FinalValue.ToInt32() == 1;
        BattleSlot? slot = partyIndex == (side == BattleSide.Enemy ? context.EnemyActive : context.PlayerActive)
            ? new BattleSlot(side, 0)
            : null;
        var overlayOwner = new BattleOverlayOwner(side, partyIndex, slot);
        IReadOnlyList<EntityId> types = context.Overlays is { } overlays
            ? PhysicalMetricFormulas.EffectiveValues(creature, overlays, overlayOwner).CreatureTypes
            : creature.Types;
        return GroundedConditions.Query(creature, types,
            new BattleConditionOwner(BattleConditionScope.Creature, side, slot, partyIndex),
            context.Conditions, suppressHeldItems: ItemsSuppressed(context)).FinalValue.ToInt32() == 1;
    }

    private static bool TryOwner(BattleCreature creature, SmartAiContext context,
        out BattleSide side, out int partyIndex)
    {
        for (int i = 0; i < context.EnemyParty.Count; i++)
            if (ReferenceEquals(creature, context.EnemyParty[i]))
            {
                side = BattleSide.Enemy;
                partyIndex = i;
                return true;
            }
        for (int i = 0; i < context.PlayerParty.Count; i++)
            if (ReferenceEquals(creature, context.PlayerParty[i]))
            {
                side = BattleSide.Player;
                partyIndex = i;
                return true;
            }
        side = default;
        partyIndex = -1;
        return false;
    }

    private static BattleConditionOwner CreatureOwner(BattleSide side, int partyIndex) =>
        new(BattleConditionScope.Creature, side, new BattleSlot(side, 0), partyIndex);

    private static BattleConditionSource CreatureSource(BattleSide side, int partyIndex) =>
        new(new BattleSlot(side, 0), partyIndex);

    private static IReadOnlyList<BattleQueryModifier> WeatherHealingModifiers(
        SmartAiContext context, HealEffect effect, int maxHp) => context.Conditions is null
        ? []
        : [.. WeatherConditions.CollectHealingHooks(context.Conditions, effect, maxHp, actionSequence: 0)
                .QueryModifiers(BattleQueryId.Healing),
           .. TerrainConditions.CollectHealingHooks(context.Conditions, effect, maxHp, actionSequence: 0)
                .QueryModifiers(BattleQueryId.Healing)];

    private static bool TryResourceInputs(BattleCreature attacker, BattleCreature defender, BattleMove move,
        IReadOnlyList<BattleCreature> sourceParty, int sourceIndex, BattleSide sourceSide, SmartAiContext context,
        out PartyResourceFormulaInputs? inputs)
    {
        inputs = null;
        if (!PartyResourceFormulas.HasPowerFormula(move))
            return true;

        int? itemPower = null;
        if (move.SecondaryEffects.OfType<ItemDataPowerEffect>().Any())
        {
            if (ItemsSuppressed(context))
                return false;
            EntityId? heldItem = context.Overlays is { } overlays
                ? PhysicalMetricFormulas.EffectiveValues(attacker, overlays,
                    new BattleOverlayOwner(sourceSide, sourceIndex, new BattleSlot(sourceSide, 0))).HeldItem
                : attacker.HeldItem;
            if (heldItem is not { } itemId || context.ItemData is null
                || !context.ItemData.TryGetValue(itemId, out Item? item) || item.FlingPower is not > 0)
                return false;
            itemPower = item.FlingPower;
        }

        int? randomPower = move.SecondaryEffects.OfType<RandomTablePowerEffect>().SingleOrDefault() is { } table
            ? PartyResourceFormulas.ExpectedWeightedPower(table.Entries)
            : null;
        inputs = PartyResourceFormulas.Inputs(sourceParty, attacker, defender,
            move.Pp, Math.Max(0, move.Pp - 1), itemPower, randomPower);
        return true;
    }

    /// <summary>Expected direct HP loss from visible damage hazards on the AI's own side.</summary>
    private static double SwitchInHazardDamage(BattleCreature incoming, SmartAiContext context)
    {
        if (context.Conditions is null)
            return 0;
        IReadOnlyList<EntityId> types = EffectiveTypes(incoming, context);
        bool grounded = IsGrounded(incoming, context);
        return EntryHazardConditions.Active(context.Conditions, BattleSide.Enemy)
            .Where(instance => instance.Definition.EntryHazard is { Kind: EntryHazardKind.Damage })
            .Sum(instance =>
            {
                EntryHazardProfile profile = instance.Definition.EntryHazard!;
                if (profile.GroundedOnly && !grounded)
                    return 0;
                double effectiveness = profile.DamageType is { } type
                    ? context.Chart.Effectiveness(type, types) : 1;
                return EntryHazardConditions.Damage(profile, instance.StackCount, incoming.MaxHp, effectiveness);
            });
    }

    private static bool CanAddHazard(SmartAiContext context, BattleSide side, EntryHazardProfile profile)
    {
        BattleConditionInstance? active = context.Conditions?
            .SingleOrDefault(instance => instance.Owner == SideConditions.Owner(side)
                && instance.Definition.Id == profile.Id);
        return active is null || active.StackCount < profile.MaximumLayers;
    }

    private static bool CanApplyProtection(SmartAiContext context, BattleCreature source,
        ProtectionProfile profile)
    {
        if (context.Conditions is null || !TryOwner(source, context, out BattleSide side, out int partyIndex))
            return true;
        if (profile.Scope == ProtectionScope.Personal)
            return ProtectionConditions.Active(context.Conditions, side, partyIndex) is null;
        BattleSideCondition condition = profile.Filter == ProtectionFilter.Priority
            ? BattleSideCondition.PriorityProtection : BattleSideCondition.MultiTargetProtection;
        return !SideConditions.Active(context.Conditions, side, condition);
    }

    private static bool IsPersonallyProtected(BattleCreature creature, SmartAiContext? context)
    {
        if (context?.Conditions is null
            || !TryOwner(creature, context, out BattleSide side, out int partyIndex))
            return false;
        return ProtectionConditions.Active(context.Conditions, side, partyIndex) is not null;
    }

    private static IReadOnlyList<EntityId> EffectiveTypes(BattleCreature creature, SmartAiContext context)
    {
        if (!TryOwner(creature, context, out BattleSide side, out int partyIndex) || context.Overlays is null)
            return creature.Types;
        BattleSlot? slot = partyIndex == (side == BattleSide.Enemy ? context.EnemyActive : context.PlayerActive)
            ? new BattleSlot(side, 0) : null;
        return PhysicalMetricFormulas.EffectiveValues(creature, context.Overlays,
            new BattleOverlayOwner(side, partyIndex, slot)).CreatureTypes;
    }

    private static BattleEffectiveValues EffectiveValues(BattleCreature creature, SmartAiContext? context)
    {
        if (context is null || context.Overlays is null
            || !TryOwner(creature, context, out BattleSide side, out int partyIndex))
            return PhysicalMetricFormulas.BaseEffectiveValues(creature);
        BattleSlot? slot = partyIndex == (side == BattleSide.Enemy ? context.EnemyActive : context.PlayerActive)
            ? new BattleSlot(side, 0) : null;
        return PhysicalMetricFormulas.EffectiveValues(creature, context.Overlays,
            new BattleOverlayOwner(side, partyIndex, slot));
    }

    private static int MoveSlot(BattleCreature creature, BattleMove move)
    {
        for (int index = 0; index < creature.Moves.Count; index++)
            if (ReferenceEquals(creature.Moves[index], move))
                return index;
        throw new InvalidOperationException("AI move preview requires a move owned by the source creature.");
    }

    private static bool HasDangerousBoost(BattleCreature c) =>
        c.Stage(StatKind.Atk) > 1 || c.Stage(StatKind.Spa) > 1 || c.Stage(StatKind.Spe) > 1;

    private static bool CanSwitch(BattleCreature c) => !c.IsTrapped && !c.IsCharging && !c.IsLocked;

    private static int AiSpeed(BattleCreature creature, BattleSide side, int partyIndex, SmartAiContext context)
    {
        IReadOnlyList<BattleQueryModifier> modifiers = SideSpeedModifiers(context, side);
        return context.Overlays is { } overlays
            ? PhysicalMetricFormulas.SpeedQuery(creature, overlays,
                new BattleOverlayOwner(side, partyIndex, new BattleSlot(side, 0)), modifiers).FinalValue.ToInt32()
            : PhysicalMetricFormulas.SpeedQuery(creature, modifiers).FinalValue.ToInt32();
    }

    private static PhysicalFormulaInputs PhysicalInputs(
        BattleCreature attacker, BattleCreature defender, SmartAiContext context)
    {
        (BattleSide sourceSide, int sourceIndex) = Owner(context, attacker);
        (BattleSide targetSide, int targetIndex) = Owner(context, defender);
        IReadOnlyList<BattleQueryModifier> source = SideSpeedModifiers(context, sourceSide);
        IReadOnlyList<BattleQueryModifier> target = SideSpeedModifiers(context, targetSide);
        if (context.Overlays is { } overlays)
            return PhysicalMetricFormulas.Inputs(attacker, defender, overlays,
                new BattleOverlayOwner(sourceSide, sourceIndex, new BattleSlot(sourceSide, 0)),
                new BattleOverlayOwner(targetSide, targetIndex, new BattleSlot(targetSide, 0)),
                source, target);
        return new PhysicalFormulaInputs(
            PhysicalMetricFormulas.SpeedQuery(attacker, source).FinalValue.ToInt32(),
            PhysicalMetricFormulas.SpeedQuery(defender, target).FinalValue.ToInt32(),
            attacker.WeightHectograms, defender.WeightHectograms,
            attacker.HeightDecimeters, defender.HeightDecimeters);
    }

    private static (BattleSide Side, int PartyIndex) Owner(SmartAiContext context, BattleCreature creature)
    {
        int enemy = IndexOfReference(context.EnemyParty, creature);
        if (enemy >= 0)
            return (BattleSide.Enemy, enemy);
        int player = IndexOfReference(context.PlayerParty, creature);
        return player >= 0 ? (BattleSide.Player, player)
            : throw new ArgumentException("AI speed subject is outside both visible parties.", nameof(creature));
    }

    private static int IndexOfReference(IReadOnlyList<BattleCreature> party, BattleCreature creature)
    {
        for (int index = 0; index < party.Count; index++)
            if (ReferenceEquals(party[index], creature))
                return index;
        return -1;
    }

    private static IReadOnlyList<BattleQueryModifier> SideSpeedModifiers(
        SmartAiContext context, BattleSide side) => context.Conditions is null ? []
        : SideConditions.CollectSpeedHooks(context.Conditions, side, 0)
            .QueryModifiers(BattleQueryId.Speed);

    private static int SecondaryChance(SmartAiContext context, BattleMove move, MoveEffect effect,
        BattleCreature source, BattleCreature target)
    {
        IReadOnlyList<BattleQueryModifier> modifiers = context.Conditions is null
            || move.DamageClass == DamageClass.Status ? []
            : SideConditions.CollectSecondaryChanceHooks(context.Conditions, BattleSide.Enemy, 0)
                .QueryModifiers(BattleQueryId.SecondaryChance);
        return HpStatusFormulas.SecondaryChanceQuery(effect, source, target, modifiers).FinalValue.ToInt32();
    }

    private static bool ActsBefore(int sourceSpeed, int targetSpeed, SmartAiContext context) =>
        context.Conditions is not null
        && FieldConditions.Active(context.Conditions, BattleFieldCondition.TrickRoom)
            ? sourceSpeed < targetSpeed : sourceSpeed > targetSpeed;

    private static bool ItemsSuppressed(SmartAiContext context) => context.Conditions is not null
        && FieldConditions.Active(context.Conditions, BattleFieldCondition.MagicRoom);

    private static int StatValue(Stats stats, StatKind stat) => stat switch
    {
        StatKind.Atk => stats.Atk,
        StatKind.Def => stats.Def,
        StatKind.Spa => stats.Spa,
        StatKind.Spd => stats.Spd,
        _ => throw new ArgumentException($"Stat {stat} is not valid for damage scoring."),
    };

    private static double HpFraction(BattleCreature c) => c.CurrentHp / (double)c.MaxHp;

    private static int FirstUsableOrZero(BattleCreature attacker)
    {
        for (int i = 0; i < attacker.Moves.Count; i++)
            if (attacker.Moves[i].HasPp)
                return i;
        return 0;
    }
}
