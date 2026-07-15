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
    IReadOnlyList<BattleConditionInstance>? Conditions = null);

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
                move.Move, sourceSpeed > targetSpeed, sourceSpeed < targetSpeed);
        }
        bool hasResourceInputs = TryResourceInputs(attacker, defender, move, context.EnemyParty,
            context.EnemyActive, BattleSide.Enemy, context, out PartyResourceFormulaInputs? resourceInputs);
        double damage = hasResourceInputs
            ? ExpectedDamage(attacker, defender, move, context.Chart, inputs, actionInputs, resourceInputs,
                WeatherDamageModifiers(context, move))
            : 0;
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
            && WeatherAllowsStatus(context, ailment))
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
                return ExpectedDamage(attacker, defender, move, chart,
                    modifiers: WeatherDamageModifiers(context, move));
            return TryResourceInputs(attacker, defender, move, sourceParty, sourceIndex, BattleSide.Enemy,
                context, out PartyResourceFormulaInputs? inputs)
                    ? ExpectedDamage(attacker, defender, move, chart, resourceInputs: inputs,
                        modifiers: WeatherDamageModifiers(context, move))
                    : 0;
        }).DefaultIfEmpty(0).Max();

    private static double PredictedDamage(BattleCreature attacker, BattleCreature defender, SmartAiContext context)
    {
        if (context.Memory?.RepeatedPlayerMoveCount >= 2
            && context.Memory.LastPlayerMove is { } last
            && attacker.Moves.FirstOrDefault(m => m.Move == last && m.HasPp) is { } repeated)
            return ExpectedDamage(attacker, defender, repeated, context.Chart,
                modifiers: WeatherDamageModifiers(context, repeated));

        return BestDamage(attacker, defender, context.Chart, context);
    }

    private static bool ThreatensKo(BattleCreature attacker, BattleCreature defender, TypeChart chart,
        SmartAiContext? context = null) =>
        BestDamage(attacker, defender, chart, context) >= defender.CurrentHp;

    private static double ExpectedDamage(BattleCreature attacker, BattleCreature defender, BattleMove move, TypeChart chart,
        PhysicalFormulaInputs? physicalInputs = null, BattleActionFormulaInputs? actionInputs = null,
        PartyResourceFormulaInputs? resourceInputs = null,
        IReadOnlyList<BattleQueryModifier>? modifiers = null)
    {
        double eff = chart.Effectiveness(move.Type, defender.Types);
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
        int power = BattleQuery.ResolveInteger(BattleQueryId.BasePower, powerQuery.AuthoredBase, powerQuery.Modifiers);
        StatKind attackStat = physical ? StatKind.Atk : StatKind.Spa;
        StatKind defenseStat = physical ? StatKind.Def : StatKind.Spd;
        int a = BattleQuery.ResolveInteger(BattleQueryId.OffensiveStat,
            physical ? attacker.Stats.Atk : attacker.Stats.Spa,
            [new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply,
                BattleQuery.StatStageMultiplier(attacker.Stage(attackStat)), InsertionOrder: 0)]);
        int d = BattleQuery.ResolveInteger(BattleQueryId.DefensiveStat,
            physical ? defender.Stats.Def : defender.Stats.Spd,
            [new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply,
                BattleQuery.StatStageMultiplier(defender.Stage(defenseStat)), InsertionOrder: 0)]);
        double stab = TypeChart.Stab(move.Type, attacker.Types);
        bool burn = attacker.Status == PersistentStatus.Burn && physical && !powerQuery.IgnoreSourceBurnPenalty;
        int oneHit = BattleQuery.ResolveInteger(BattleQueryId.FinalDamage,
            DamageCalc.Compute(attacker.Level, power, a, d, eff, stab, crit: false, MidpointRoll, burn), modifiers);
        double hits = move.MultiHitMax >= 2 ? (move.MultiHitMin + move.MultiHitMax) / 2.0 : 1;
        double total = oneHit * hits;
        int floor = HpStatusFormulas.CannotKoFloor(move);
        return floor > 0 ? Math.Min(total, Math.Max(0, defender.CurrentHp - floor)) : total;
    }

    private static IReadOnlyList<BattleQueryModifier> WeatherDamageModifiers(
        SmartAiContext context, BattleMove move) => context.Conditions is null
        ? []
        : WeatherConditions.CollectDamageHooks(context.Conditions, move.Type.Slug, actionSequence: 0)
            .QueryModifiers(BattleQueryId.FinalDamage);

    private static BattleHookDispatchSnapshot? WeatherAccuracyHooks(SmartAiContext context, BattleMove move) =>
        context.Conditions is not null
        && move.SecondaryEffects.OfType<WeatherAccuracyEffect>().SingleOrDefault() is { } effect
            ? WeatherConditions.CollectAccuracyHooks(context.Conditions, effect, actionSequence: 0)
            : null;

    private static bool WeatherAllowsStatus(SmartAiContext context, PersistentStatus status) =>
        context.Conditions is null || !WeatherConditions.CollectStatusHooks(context.Conditions, status, 0)
            .Filters().Any(filter => filter is
                { Filter.Value: "status_attempt", Decision: BattleHookFilterDecision.Deny });

    private static IReadOnlyList<BattleQueryModifier> WeatherHealingModifiers(
        SmartAiContext context, HealEffect effect, int maxHp) => context.Conditions is null
        ? []
        : WeatherConditions.CollectHealingHooks(context.Conditions, effect, maxHp, actionSequence: 0)
            .QueryModifiers(BattleQueryId.Healing);

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

    private static double HpFraction(BattleCreature c) => c.CurrentHp / (double)c.MaxHp;

    private static int FirstUsableOrZero(BattleCreature attacker)
    {
        for (int i = 0; i < attacker.Moves.Count; i++)
            if (attacker.Moves[i].HasPp)
                return i;
        return 0;
    }
}
