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
    int OwnSpikeLayers = 0,
    bool OwnStealthRock = false,
    BattleOverlayStore? Overlays = null,
    BattleActionHistory? ActionHistory = null,
    IReadOnlyDictionary<EntityId, Item>? ItemData = null,
    IReadOnlyList<BattleConditionInstance>? Conditions = null,
    string Ruleset = BattleRulesets.Gen4Like,
    BattleEnvironment NaturalEnvironment = BattleEnvironment.Building,
    int ActiveSlotsPerSide = 1)
{
    public BattleEnvironmentState Environment => BattleEnvironmentState.Resolve(NaturalEnvironment, Conditions);
}

public sealed record AiScoreComponent(string Name, double Value);
public sealed record AiCandidateScore(BattleAction Action, double Score, IReadOnlyList<AiScoreComponent> Components);
public sealed record SmartAiDecision(BattleAction Action, IReadOnlyList<AiCandidateScore> Scores);

public static class SmartAi
{
    private const int MidpointRoll = 92;
    private static readonly EntityId RockType = EntityId.Parse("type:rock"); // mirrors BattleController stealth-rock scaling

    public static int ChooseMove(BattleCreature attacker, BattleCreature defender, TypeChart chart, IRng rng,
        SmartAiWeights? weights = null) =>
        ((UseMove)ChooseAction(new SmartAiContext([attacker], 0, [defender], 0, chart, rng, Weights: weights)).Action).MoveIndex;

    public static SmartAiDecision ChooseAction(SmartAiContext context)
    {
        SmartAiWeights weights = context.Weights ?? new SmartAiWeights();
        BattleCreature active = context.EnemyParty[context.EnemyActive];
        BattleCreature target = context.PlayerParty[context.PlayerActive];
        var scores = new List<AiCandidateScore>();

        for (int i = 0; i < active.Moves.Count; i++)
            if (active.Moves[i].HasPp)
                scores.Add(ScoreMove(new UseMove(i), active, target, context, weights, context.Rng));

        scores.AddRange(ScoreItems(context, weights));

        if (CanSwitch(active) && context.Turn - (context.Memory?.LastVoluntarySwitchTurn ?? -99) >= 3)
            scores.AddRange(ScoreSwitches(context, weights, BestMoveScore(scores)));

        if (scores.Count == 0)
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
            : context.Overlays is { } overlays
                ? PhysicalMetricFormulas.Inputs(attacker, defender, overlays,
                    new BattleOverlayOwner(BattleSide.Enemy, context.EnemyActive, new BattleSlot(BattleSide.Enemy, 0)),
                    new BattleOverlayOwner(BattleSide.Player, context.PlayerActive, new BattleSlot(BattleSide.Player, 0)))
                : PhysicalMetricFormulas.Inputs(attacker, defender);
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
        bool alwaysHits = move.BypassAccuracy || (!move.Ohko && move.Accuracy is null) || weatherBypass;
        var accuracyModifiers = weatherAccuracy?.QueryModifiers(BattleQueryId.Accuracy).ToList() ?? [];
        if (!alwaysHits)
        {
            accuracyModifiers.Add(new BattleQueryModifier(BattleQueryStage.SourceTargetState,
                BattleQueryOperation.Multiply,
                BattleQuery.AccuracyStageMultiplier(attacker.Stage(StatKind.Accuracy), defender.Stage(StatKind.Evasion)),
                InsertionOrder: accuracyModifiers.Count));
            if (context.Conditions is not null)
                accuracyModifiers.AddRange(FieldConditions.CollectAccuracyHooks(context.Conditions, 0)
                    .QueryModifiers(BattleQueryId.Accuracy)
                    .Select(modifier => modifier with { InsertionOrder = accuracyModifiers.Count }));
        }
        int resolvedAccuracy = BattleQuery.ResolveInteger(BattleQueryId.Accuracy, authoredAccuracy,
            accuracyModifiers);
        double accuracy = alwaysHits ? 1 : resolvedAccuracy / 100.0;
        c.Add(new("damage", damage * accuracy));
        if (damage >= defender.CurrentHp && damage > 0)
            c.Add(new("ko", weights.KoBonus * accuracy));

        if (move.Ailment is { } ailment && move.AilmentChance > 0
            && StatusEffects.CanApplyStatus(defender.Status)
            && !StatusEffects.TypeImmuneToStatus(ailment, defender.Types)
            && WeatherAllowsStatus(context, ailment)
            && TerrainAllowsStatus(context, ailment, defender)
            && TerrainAllowsPriorityHit(context, move, attacker, defender))
        {
            AilmentEffect? effect = move.SecondaryEffects.OfType<AilmentEffect>().FirstOrDefault();
            int chance = effect is null ? move.AilmentChance
                : HpStatusFormulas.SecondaryChanceQuery(effect, attacker, defender).FinalValue.ToInt32();
            c.Add(new("status", weights.StatusValue * HpFraction(defender) * chance / 100.0));
        }

        if (move.StageEffect is { OnSelf: true, Delta: > 0 } setup
            && !ThreatensKo(defender, attacker, context.Chart, context))
            c.Add(new("setup", weights.SetupValue * setup.Delta));

        if ((move.SetsSpikes || move.SetsStealthRock) && context.PlayerParty.Count(p => !p.IsFainted) > 1)
            c.Add(new("hazard", weights.HazardValue * (context.PlayerParty.Count(p => !p.IsFainted) - 1)));

        if (move.IsProtect && ThreatensKo(defender, attacker, context.Chart, context))
            c.Add(new("protect", weights.ProtectValue / Math.Pow(2, attacker.ProtectChain)));

        if (move.ForcesSwitch && HasDangerousBoost(defender))
            c.Add(new("forceSwitch", weights.ForceSwitchValue));

        if (move.Heal is { } heal && attacker.CurrentHp < attacker.MaxHp / 2)
        {
            int recovery = EffectMath.HealAmount(attacker.MaxHp, heal.Num, heal.Den);
            if (move.SecondaryEffects.OfType<HealEffect>().LastOrDefault(effect =>
                    effect.Recipient == HpFractionRecipient.Self) is { } healEffect)
                recovery = BattleQuery.ResolveInteger(BattleQueryId.Healing, recovery,
                    WeatherHealingModifiers(context, healEffect, attacker.MaxHp));
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
        EntityId moveType = weather?.Type ?? move.Type;
        if (conditions is not null && TerrainConditions.CanBlockPriorityTarget(move.Target)
            && TerrainConditions.CollectPriorityHooks(conditions,
                EffectivePriority(conditions, move, attacker, context), IsGrounded(defender, context), 0)
                .Filters().Any(filter => filter is
                    { Filter.Value: "priority_hit", Decision: BattleHookFilterDecision.Deny }))
            return 0;
        double eff = chart.Effectiveness(moveType, defender.Types);
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
            return BattleQuery.ResolveInteger(BattleQueryId.FinalDamage,
                attacker.Level >= defender.Level ? defender.CurrentHp : 0);
        if (move.FixedDamageLevel)
            return BattleQuery.ResolveInteger(BattleQueryId.FinalDamage, attacker.Level);
        if (move.FixedDamage is int fixedDamage)
            return BattleQuery.ResolveInteger(BattleQueryId.FinalDamage, fixedDamage);
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

        bool physical = move.DamageClass == DamageClass.Physical;
        HpStatusPowerQuery powerQuery = HpStatusFormulas.PowerQuery(
            move, attacker, defender, physicalInputs, actionInputs, resourceInputs);
        var powerModifiers = (weather is null
            ? powerQuery.Modifiers
            : [.. powerQuery.Modifiers, .. weather.PowerModifiers]).ToList();
        if (conditions is not null)
            powerModifiers.AddRange(FieldConditions.CollectBasePowerHooks(conditions, moveType.Slug, ruleset, 0)
                .QueryModifiers(BattleQueryId.BasePower)
                .Select(modifier => modifier with { InsertionOrder = powerModifiers.Count }));
        int power = BattleQuery.ResolveInteger(BattleQueryId.BasePower, powerQuery.AuthoredBase, powerModifiers);
        StatKind attackStat = FieldConditions.DefensiveStat(conditions ?? [],
            move.OffensiveStatOverride ?? (physical ? StatKind.Atk : StatKind.Spa));
        StatKind defenseStat = FieldConditions.DefensiveStat(conditions ?? [],
            move.DefensiveStatOverride ?? (physical ? StatKind.Def : StatKind.Spd));
        int a = BattleQuery.ResolveInteger(BattleQueryId.OffensiveStat,
            StatValue(attacker.Stats, attackStat),
            [new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply,
                BattleQuery.StatStageMultiplier(attacker.Stage(attackStat)), InsertionOrder: 0),
             .. WeatherStatModifiers(conditions, attacker.Types, attackStat, BattleQueryId.OffensiveStat, ruleset)]);
        int d = BattleQuery.ResolveInteger(BattleQueryId.DefensiveStat,
            StatValue(defender.Stats, defenseStat),
            [new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply,
                BattleQuery.StatStageMultiplier(defender.Stage(defenseStat)), InsertionOrder: 0),
             .. WeatherStatModifiers(conditions, defender.Types, defenseStat, BattleQueryId.DefensiveStat, ruleset)]);
        double stab = TypeChart.Stab(moveType, attacker.Types);
        bool burn = attacker.Status == PersistentStatus.Burn && physical && !powerQuery.IgnoreSourceBurnPenalty;
        var finalModifiers = modifiers?.ToList() ?? [];
        if (conditions is not null)
        {
            BattleHookDispatchSnapshot terrain = TerrainConditions.CollectDamageHooks(conditions, moveType.Slug,
                IsGrounded(attacker, context), IsGrounded(defender, context), actionSequence: 0);
            finalModifiers.AddRange(terrain.QueryModifiers(BattleQueryId.FinalDamage)
                .Select(modifier => modifier with { InsertionOrder = finalModifiers.Count }));
            BattleSide targetSide = context?.EnemyParty.Any(creature => ReferenceEquals(creature, defender)) == true
                ? BattleSide.Enemy : BattleSide.Player;
            BattleHookDispatchSnapshot side = SideConditions.CollectDamageHooks(conditions, targetSide,
                move.DamageClass, context?.ActiveSlotsPerSide ?? 1, critical: false,
                BypassesSideConditions(attacker, move), actionSequence: 0);
            finalModifiers.AddRange(side.QueryModifiers(BattleQueryId.FinalDamage)
                .Select(modifier => modifier with { InsertionOrder = finalModifiers.Count }));
        }
        int oneHit = BattleQuery.ResolveInteger(BattleQueryId.FinalDamage,
            DamageCalc.Compute(attacker.Level, power, a, d, eff, stab, crit: false, MidpointRoll, burn), finalModifiers);
        double hits = move.MultiHitMax >= 2 ? (move.MultiHitMin + move.MultiHitMax) / 2.0 : 1;
        double total = oneHit * hits;
        int floor = HpStatusFormulas.CannotKoFloor(move);
        return floor > 0 ? Math.Min(total, Math.Max(0, defender.CurrentHp - floor)) : total;
    }

    private static bool BypassesSideConditions(BattleCreature attacker, BattleMove move) =>
        move.SecondaryEffects.OfType<SideConditionBypassEffect>().Any(effect => effect.Tag == "screen")
        || move.SecondaryEffects.OfType<RemoveSideConditionEffect>().Any(effect =>
            effect.Tag == "screen" && effect.Side == SideConditionTarget.Target
                && effect.Timing == SideConditionTiming.BeforeDamage)
        || attacker.AbilityHooks.SelectMany(hook => hook.Effects).Any(effect =>
            effect.Op == "sideConditionBypass"
            && effect.Params?.TryGetValue("tag", out var tag) == true
            && tag.GetString() == "screen");

    private sealed record FieldMovePreview(EntityId Type, IReadOnlyList<BattleQueryModifier> PowerModifiers);

    private static FieldMovePreview PreviewFieldMove(SmartAiContext context, BattleMove move,
        BattleCreature source, BattleCreature target)
    {
        if (context.Conditions is null)
            return new(move.Type, []);
        if (move.SecondaryEffects.OfType<WeatherMoveEffect>().SingleOrDefault() is { } weather)
        {
            BattleHookDispatchSnapshot typeHooks = WeatherConditions.CollectMoveTypeHooks(context.Conditions, weather, 0);
            EntityId type = typeHooks.MoveTypes().SingleOrDefault() is { } replacement && replacement != default
                ? replacement : move.Type;
            return new(type, WeatherConditions.CollectBasePowerHooks(context.Conditions, weather, 0)
                .QueryModifiers(BattleQueryId.BasePower));
        }
        if (move.SecondaryEffects.OfType<TerrainMoveEffect>().SingleOrDefault() is not { } terrain)
            return new(move.Type, []);
        BattleHookDispatchSnapshot terrainType = TerrainConditions.CollectMoveTypeHooks(
            context.Conditions, terrain, IsGrounded(source, context), 0);
        EntityId terrainMoveType = terrainType.MoveTypes().SingleOrDefault() is { } replacementType
            && replacementType != default ? replacementType : move.Type;
        return new(terrainMoveType, TerrainConditions.CollectBasePowerHooks(context.Conditions, terrain,
            IsGrounded(source, context), IsGrounded(target, context), 0).QueryModifiers(BattleQueryId.BasePower));
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
        if (move.SecondaryEffects.OfType<TerrainMoveEffect>().SingleOrDefault() is not { } effect)
            return move.Priority;
        return BattleQuery.ResolveInteger(BattleQueryId.Priority, move.Priority,
            TerrainConditions.CollectMovePriorityHooks(conditions, effect,
                IsGrounded(source, context), 0).QueryModifiers(BattleQueryId.Priority));
    }

    private static Terrain ActiveTerrain(SmartAiContext context) => context.Conditions?
        .SingleOrDefault(instance => instance.Definition.Scope == BattleConditionScope.Terrain) is { } condition
            ? TerrainConditions.For(condition.Definition.Id).Terrain
            : Terrain.None;

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

    /// <summary>Expected HP a reserve loses on switch-in to the AI's own side (stealth rock, type-scaled,
    /// then spikes) — mirrors <see cref="BattleController.OnSwitchIn"/> so switch valuation stops walking
    /// creatures into hazards.</summary>
    private static double SwitchInHazardDamage(BattleCreature incoming, SmartAiContext context)
    {
        double dmg = 0;
        if (context.OwnStealthRock)
            dmg += EffectMath.TypeScaledHazardDamage(incoming.MaxHp, context.Chart.Effectiveness(RockType, incoming.Types));
        if (context.OwnSpikeLayers > 0)
            dmg += EffectMath.HazardDamage(incoming.MaxHp, context.OwnSpikeLayers);
        return dmg;
    }

    private static bool HasDangerousBoost(BattleCreature c) =>
        c.Stage(StatKind.Atk) > 1 || c.Stage(StatKind.Spa) > 1 || c.Stage(StatKind.Spe) > 1;

    private static bool CanSwitch(BattleCreature c) => !c.IsTrapped && !c.IsCharging && !c.IsLocked;

    private static int AiSpeed(BattleCreature creature, BattleSide side, int partyIndex, SmartAiContext context) =>
        context.Overlays is { } overlays
            ? PhysicalMetricFormulas.SpeedQuery(creature, overlays,
                new BattleOverlayOwner(side, partyIndex, new BattleSlot(side, 0))).FinalValue.ToInt32()
            : PhysicalMetricFormulas.SpeedQuery(creature).FinalValue.ToInt32();

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
