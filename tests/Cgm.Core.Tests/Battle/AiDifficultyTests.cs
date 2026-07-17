using Cgm.Core.Battle;
using Cgm.Core.Model;
using Xunit.Abstractions;

namespace Cgm.Core.Tests.Battle;

/// <summary>Difficulty-tier strength measurement. Mirror matches (identical teams on both sides) isolate
/// AI skill: any deviation from a 50% win rate is the chooser, not the roster. The `Smart` (Advanced) tier
/// must meaningfully outplay `Basic` (Beginner). Sides alternate per seed to cancel first-mover bias.</summary>
public sealed class AiDifficultyTests
{
    private readonly ITestOutputHelper _out;
    public AiDifficultyTests(ITestOutputHelper output) => _out = output;

    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Water = EntityId.Parse("type:water");
    private static readonly EntityId Grass = EntityId.Parse("type:grass");
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    // Rock-paper-scissors coverage so switching/type reads matter.
    private static TypeChart Chart() => new(
    [
        new TypeDef { Id = Fire, DoubleDamageTo = [Grass] },
        new TypeDef { Id = Water, DoubleDamageTo = [Fire] },
        new TypeDef { Id = Grass, DoubleDamageTo = [Water] },
        new TypeDef { Id = Normal },
    ]);

    private static BattleMove Atk(EntityId type, int power, int priority = 0) =>
        new(EntityId.Parse("move:atk"), type, DamageClass.Physical, power, 100, 20, priority, 0);
    private static BattleMove Para() =>
        new(EntityId.Parse("move:para"), Normal, DamageClass.Status, null, null, 20, 0, 0,
            ailment: PersistentStatus.Paralysis, ailmentChance: 100);
    private static BattleMove SwordsDance() =>
        new(EntityId.Parse("move:sd"), Normal, DamageClass.Status, null, null, 20, 0, 0,
            stageEffect: new StageEffect(StatKind.Atk, 2, OnSelf: true, Chance: 100));
    private static BattleMove Recover() =>
        new(EntityId.Parse("move:recover"), Normal, DamageClass.Status, null, null, 10, 0, 0,
            heal: new Fraction(1, 2));
    private static BattleMove LayeredHazard() =>
        new(EntityId.Parse("move:layered_hazard"), Normal, DamageClass.Status, null, null, 20, 0, 0,
            target: MoveTarget.OpponentsField,
            secondaryEffects: [new SetEntryHazardEffect(EntryHazardConditions.LegacyLayeredDamage)]);
    private static BattleMove TypedHazard() =>
        new(EntityId.Parse("move:typed_hazard"), Normal, DamageClass.Status, null, null, 20, 0, 0,
            target: MoveTarget.OpponentsField,
            secondaryEffects: [new SetEntryHazardEffect(EntryHazardConditions.LegacyTypeScaledDamage)]);
    private static BattleMove Protect() =>
        new(EntityId.Parse("move:protect"), Normal, DamageClass.Status, null, null, 20, 0, 0,
            target: MoveTarget.User,
            secondaryEffects: [new ProtectEffect(ProtectionConditions.LegacyPersonal)]);
    private static BattleMove Roar() =>
        new(EntityId.Parse("move:roar"), Normal, DamageClass.Status, null, null, 20, 0, 0, forcesSwitch: true);

    private static BattleCreature Mon(string slug, EntityId type, params BattleMove[] moves) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [type],
            new Stats(150, 120, 95, 120, 95, 100), moves);

    // Fresh mutable roster per call; both sides use an identical copy (a mirror match). Movesets now span
    // hazards, priority, protect, force-switch, setup, status and recovery so the AI's full toolkit engages.
    private static BattleCreature[] Team() =>
    [
        Mon("f", Fire,   Atk(Fire, 80),   Atk(Normal, 55), LayeredHazard(), Atk(Normal, 40, priority: 1)),
        Mon("w", Water,  Atk(Water, 80),  Atk(Normal, 55), SwordsDance(), Protect()),
        Mon("g", Grass,  Atk(Grass, 80),  Atk(Normal, 55), Para(),        Recover()),
        Mon("n", Normal, Atk(Normal, 80), Atk(Fire, 60),   Roar(),        TypedHazard()),
    ];

    private const int TurnCap = 3000;

    private static BattleSide Simulate(int seed, AiProfile playerAi, AiProfile enemyAi,
        SmartAiWeights? playerW, SmartAiWeights? enemyW = null)
    {
        BattleCreature[] player = Team(), enemy = Team();
        TypeChart chart = Chart();
        var battle = new BattleController(player, enemy, chart, new Rng(seed));
        var rngP = new Rng(seed * 2 + 1);
        var rngE = new Rng(seed * 2 + 2);

        int turns = 0;
        while (battle.Outcome is null)
        {
            Assert.True(++turns <= TurnCap, "battle failed to terminate");
            int pa = Array.FindIndex(player, c => ReferenceEquals(c, battle.Active(BattleSide.Player)));
            int ea = Array.FindIndex(enemy, c => ReferenceEquals(c, battle.Active(BattleSide.Enemy)));
            BattleAction p = TrainerAi.ChooseAction(playerAi, new SmartAiContext(player, pa, enemy, ea, chart, rngP,
                Turn: turns, Weights: playerAi == AiProfile.Smart ? playerW : null,
                Conditions: battle.ConditionSnapshot));
            BattleAction e = TrainerAi.ChooseAction(enemyAi, new SmartAiContext(enemy, ea, player, pa, chart, rngE,
                Turn: turns, Weights: enemyAi == AiProfile.Smart ? enemyW : null,
                Conditions: battle.ConditionSnapshot));
            battle.ResolveTurn(p, e);
            if (battle.PendingReplacementSlots.Count > 0)
                battle.ResolveReplacements(battle.PendingReplacementSlots.Select(slot => new BattleReplacementSelection(
                    slot, Array.FindIndex(slot.Side == BattleSide.Player ? player : enemy,
                        creature => !creature.IsFainted && !ReferenceEquals(creature, battle.Active(slot))))).ToArray());
        }
        Assert.True(battle.Outcome.Winner.HasValue);
        return battle.Outcome.Winner.Value;
    }

    /// <summary>Measures Smart's mirror-match win rate vs Basic over many seeds and prints it.</summary>
    private double SmartWinRateVsBasic(int games, SmartAiWeights? smartWeights = null)
    {
        int smartWins = 0;
        for (int s = 0; s < games; s++)
        {
            bool smartIsPlayer = s % 2 == 0;
            BattleSide smartSide = smartIsPlayer ? BattleSide.Player : BattleSide.Enemy;
            BattleSide winner = smartIsPlayer
                ? Simulate(s, AiProfile.Smart, AiProfile.Basic, playerW: smartWeights, enemyW: null)
                : Simulate(s, AiProfile.Basic, AiProfile.Smart, playerW: null, enemyW: smartWeights);
            if (winner == smartSide) smartWins++;
        }
        double rate = smartWins / (double)games;
        _out.WriteLine($"Smart vs Basic (mirror, {games} games): Smart won {smartWins} ({rate:P1})");
        return rate;
    }

    [Fact]
    public void SmartTier_OutplaysBasic_InMirrorMatches()
    {
        double rate = SmartWinRateVsBasic(400); // 400 seeds: robust, not overfit to a lucky 200-set
        // Regression floor for the Advanced tier vs Beginner in a mirror match with the FULL toolkit.
        // Progression: 53.5% → (noise 0.10→0.05) 58.8% → enriched teams 52.5% → (SwitchThreshold 35→50,
        // stop marginal switches into hazards/priority) 69.0% @400. Validated both vs Basic and in self-play
        // (threshold 50 beats threshold 35 ~83%). Floor below 69% with headroom.
        Assert.True(rate > 0.64, $"Smart mirror win rate {rate:P1} — Advanced tier regressed (expected ~69%).");
    }

    [Fact]
    public void WinRateMeasurement_IsDeterministic()
    {
        Assert.Equal(SmartWinRateVsBasic(60), SmartWinRateVsBasic(60));
    }

    [Fact]
    public void SmartVsSmart_IsRoughlyBalanced()
    {
        // Fairness/symmetry sanity: mirror teams + equal tiers should be near 50-50 (no structural
        // side bias in the engine or chooser). Sides differ only by RNG streams.
        int playerWins = 0, games = 400;
        for (int s = 0; s < games; s++)
            if (Simulate(s, AiProfile.Smart, AiProfile.Smart, null) == BattleSide.Player)
                playerWins++;
        double rate = playerWins / (double)games;
        _out.WriteLine($"Smart-vs-Smart player-side win rate: {rate:P1}");
        Assert.InRange(rate, 0.40, 0.60);
    }
}
