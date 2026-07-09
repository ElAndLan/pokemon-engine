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
    bool OwnStealthRock = false);

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
                scores.Add(ScoreMove(new UseMove(i), active, target, context.PlayerParty, context.Chart, weights, context.Rng));

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
        IReadOnlyList<BattleCreature> playerParty, TypeChart chart, SmartAiWeights weights, IRng rng)
    {
        BattleMove move = attacker.Moves[action.MoveIndex];
        var c = new List<AiScoreComponent>();

        double damage = ExpectedDamage(attacker, defender, move, chart);
        double accuracy = move.BypassAccuracy ? 1 : (move.Ohko ? EffectMath.OhkoAccuracy(attacker.Level, defender.Level) : move.Accuracy ?? 100) / 100.0;
        c.Add(new("damage", damage * accuracy));
        if (damage >= defender.CurrentHp && damage > 0)
            c.Add(new("ko", weights.KoBonus * accuracy));

        if (move.Ailment is { } ailment && move.AilmentChance > 0
            && StatusEffects.CanApplyStatus(defender.Status)
            && !StatusEffects.TypeImmuneToStatus(ailment, defender.Types))
            c.Add(new("status", weights.StatusValue * HpFraction(defender) * move.AilmentChance / 100.0));

        if (move.StageEffect is { OnSelf: true, Delta: > 0 } setup && !ThreatensKo(defender, attacker, chart))
            c.Add(new("setup", weights.SetupValue * setup.Delta));

        if ((move.SetsSpikes || move.SetsStealthRock) && playerParty.Count(p => !p.IsFainted) > 1)
            c.Add(new("hazard", weights.HazardValue * (playerParty.Count(p => !p.IsFainted) - 1)));

        if (move.IsProtect && ThreatensKo(defender, attacker, chart))
            c.Add(new("protect", weights.ProtectValue / Math.Pow(2, attacker.ProtectChain)));

        if (move.ForcesSwitch && HasDangerousBoost(defender))
            c.Add(new("forceSwitch", weights.ForceSwitchValue));

        if (move.Heal is { } heal && attacker.CurrentHp < attacker.MaxHp / 2)
            c.Add(new("recovery", EffectMath.HealAmount(attacker.MaxHp, heal.Num, heal.Den)));

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
            double offenseGain = BestDamage(incoming, player, context.Chart) - BestDamage(active, player, context.Chart);
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

    private static double BestDamage(BattleCreature attacker, BattleCreature defender, TypeChart chart) =>
        attacker.Moves.Where(m => m.HasPp).Select(m => ExpectedDamage(attacker, defender, m, chart)).DefaultIfEmpty(0).Max();

    private static double PredictedDamage(BattleCreature attacker, BattleCreature defender, SmartAiContext context)
    {
        if (context.Memory?.RepeatedPlayerMoveCount >= 2
            && context.Memory.LastPlayerMove is { } last
            && attacker.Moves.FirstOrDefault(m => m.Move == last && m.HasPp) is { } repeated)
            return ExpectedDamage(attacker, defender, repeated, context.Chart);

        return BestDamage(attacker, defender, context.Chart);
    }

    private static bool ThreatensKo(BattleCreature attacker, BattleCreature defender, TypeChart chart) =>
        BestDamage(attacker, defender, chart) >= defender.CurrentHp;

    private static double ExpectedDamage(BattleCreature attacker, BattleCreature defender, BattleMove move, TypeChart chart)
    {
        double eff = chart.Effectiveness(move.Type, defender.Types);
        if (eff <= 0)
            return 0;

        if (move.Ohko)
            return attacker.Level >= defender.Level ? defender.CurrentHp : 0;
        if (move.FixedDamageLevel)
            return attacker.Level;
        if (move.FixedDamage is int fixedDamage)
            return fixedDamage;
        if (move.Power is not int power)
            return 0;

        bool physical = move.DamageClass == DamageClass.Physical;
        int a = (int)((physical ? attacker.Stats.Atk : attacker.Stats.Spa)
            * StatStages.Multiplier(attacker.Stage(physical ? StatKind.Atk : StatKind.Spa)));
        int d = Math.Max(1, (int)((physical ? defender.Stats.Def : defender.Stats.Spd)
            * StatStages.Multiplier(defender.Stage(physical ? StatKind.Def : StatKind.Spd))));
        double stab = TypeChart.Stab(move.Type, attacker.Types);
        bool burn = attacker.Status == PersistentStatus.Burn && physical;
        int oneHit = DamageCalc.Compute(attacker.Level, power, a, d, eff, stab, crit: false, MidpointRoll, burn);
        double hits = move.MultiHitMax >= 2 ? (move.MultiHitMin + move.MultiHitMax) / 2.0 : 1;
        return oneHit * hits;
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

    private static double HpFraction(BattleCreature c) => c.CurrentHp / (double)c.MaxHp;

    private static int FirstUsableOrZero(BattleCreature attacker)
    {
        for (int i = 0; i < attacker.Moves.Count; i++)
            if (attacker.Moves[i].HasPp)
                return i;
        return 0;
    }
}
