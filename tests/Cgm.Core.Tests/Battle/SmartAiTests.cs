using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class SmartAiTests
{
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Grass = EntityId.Parse("type:grass");
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Ground = EntityId.Parse("type:ground");
    private static readonly EntityId Flying = EntityId.Parse("type:flying");
    private static readonly EntityId Potion = EntityId.Parse("item:potion");
    private static readonly EntityId SuperPotion = EntityId.Parse("item:superpotion");

    private static TypeChart Chart() => new(
    [
        new TypeDef { Id = Fire, DoubleDamageTo = [Grass] },
        new TypeDef { Id = Ground, NoDamageTo = [Flying] },
        new TypeDef { Id = Grass }, new TypeDef { Id = Normal }, new TypeDef { Id = Flying },
    ]);

    private static BattleMove Damage(EntityId type, int power, int? accuracy = 100, int pp = 10, string slug = "m") =>
        new(EntityId.Parse($"move:{slug}"), type, DamageClass.Physical, power, accuracy, pp, 0, 0);

    private static BattleMove Paralyze() =>
        new(EntityId.Parse("move:twave"), Normal, DamageClass.Status, null, null, 25, 0, 0,
            ailment: PersistentStatus.Paralysis, ailmentChance: 100);

    private static BattleMove SwordsDance() =>
        new(EntityId.Parse("move:sd"), Normal, DamageClass.Status, null, null, 25, 0, 0,
            stageEffect: new StageEffect(StatKind.Atk, 2, OnSelf: true, Chance: 100));

    private static BattleMove Roar() =>
        new(EntityId.Parse("move:roar"), Normal, DamageClass.Status, null, null, 20, 0, 0, forcesSwitch: true);

    private static BattleMove Protect() =>
        new(EntityId.Parse("move:protect"), Normal, DamageClass.Status, null, null, 10, 0, 0, isProtect: true);

    private static BattleMove Spikes() =>
        new(EntityId.Parse("move:spikes"), Normal, DamageClass.Status, null, null, 20, 0, 0, setsSpikes: true);

    private static BattleMove Explosion() =>
        new(EntityId.Parse("move:boom"), Normal, DamageClass.Physical, 200, 100, 5, 0, 0, selfDestruct: true);

    private static SmartAiContext Ctx(BattleCreature[] enemy, BattleCreature[] player, int turn = 0,
        SmartAiMemory? memory = null, double switchThreshold = 35) =>
        new(enemy, 0, player, 0, Chart(), new Rng(1), Turn: turn, Memory: memory,
            Weights: new SmartAiWeights { NoiseFraction = 0, SwitchThreshold = switchThreshold });

    private static BattleCreature Attacker(params BattleMove[] moves) =>
        new(EntityId.Parse("species:a"), "A", 50, [Fire], new Stats(100, 100, 100, 100, 100, 100), moves);

    private static BattleCreature Defender(EntityId type, int hp = 200, int curHp = -1)
    {
        var c = new BattleCreature(EntityId.Parse("species:d"), "D", 50, [type],
            new Stats(hp, 100, 100, 100, 100, 100), [Damage(Normal, 40)]);
        if (curHp >= 0)
            c.TakeDamage(hp - curHp);
        return c;
    }

    [Fact]
    public void PicksSuperEffectiveOverNeutral()
    {
        var atk = Attacker(Damage(Normal, 60), Damage(Fire, 60)); // index 1 super vs grass
        Assert.Equal(1, SmartAi.ChooseMove(atk, Defender(Grass), Chart(), new Rng(1)));
    }

    [Fact]
    public void NeverChoosesImmuneMove()
    {
        // index 0 = ground (0× vs flying), index 1 = normal (neutral) → must pick 1.
        var atk = Attacker(Damage(Ground, 120), Damage(Normal, 40));
        Assert.Equal(1, SmartAi.ChooseMove(atk, Defender(Flying), Chart(), new Rng(1)));
    }

    [Fact]
    public void PrefersTheGuaranteedKo_WeightedByAccuracy()
    {
        // Target is nearly dead. Both moves are lethal, but one is 100% and the other 50% accurate.
        var atk = Attacker(Damage(Normal, 40, accuracy: 100), Damage(Normal, 40, accuracy: 50));
        var target = Defender(Normal, hp: 200, curHp: 5);
        Assert.Equal(0, SmartAi.ChooseMove(atk, target, Chart(), new Rng(1))); // the accurate KO
    }

    [Fact]
    public void ValuesStatusOnAHealthyTarget()
    {
        // Full-HP target: paralysis is worth more than a tiny chip.
        var atk = Attacker(Damage(Normal, 10), Paralyze());
        Assert.Equal(1, SmartAi.ChooseMove(atk, Defender(Normal, hp: 200), Chart(), new Rng(2)));
    }

    [Fact]
    public void PrefersTheKillOverStatus_WhenTargetIsLow()
    {
        // Near-dead target: the lethal chip beats inflicting status.
        var atk = Attacker(Damage(Normal, 10), Paralyze());
        var target = Defender(Normal, hp: 200, curHp: 1);
        Assert.Equal(0, SmartAi.ChooseMove(atk, target, Chart(), new Rng(2)));
    }

    [Fact]
    public void ValuesSetup_WhenNoStrongAttack()
    {
        // Only a very weak attack vs a setup move on a healthy target → set up.
        var atk = Attacker(Damage(Normal, 5), SwordsDance());
        Assert.Equal(1, SmartAi.ChooseMove(atk, Defender(Normal, hp: 300), Chart(), new Rng(3)));
    }

    [Fact]
    public void Deterministic_ForSameSeed()
    {
        var atk = Attacker(Damage(Fire, 60), Damage(Normal, 60));
        var target = Defender(Grass);
        Assert.Equal(
            SmartAi.ChooseMove(atk, target, Chart(), new Rng(7)),
            SmartAi.ChooseMove(atk, target, Chart(), new Rng(7)));
    }

    [Fact]
    public void SkipsNoPpMoves()
    {
        var atk = Attacker(Damage(Fire, 120, pp: 0), Damage(Fire, 40)); // strongest is out of PP
        Assert.Equal(1, SmartAi.ChooseMove(atk, Defender(Grass), Chart(), new Rng(1)));
    }

    [Fact]
    public void ProfileDispatch_UsesSmartChooser()
    {
        var atk = Attacker(Damage(Normal, 10), Paralyze());
        var action = TrainerAi.ChooseAction(AiProfile.Smart,
            new SmartAiContext([atk], 0, [Defender(Normal, hp: 200)], 0, Chart(), new Rng(2)));

        Assert.Equal(new UseMove(1), action);
    }

    [Fact]
    public void ScoreTable_ExposesNamedComponents()
    {
        var atk = Attacker(Damage(Normal, 10), Paralyze());
        var decision = SmartAi.ChooseAction(new SmartAiContext([atk], 0, [Defender(Normal, hp: 200)], 0,
            Chart(), new Rng(2), Weights: new SmartAiWeights { NoiseFraction = 0 }));

        var statusScore = Assert.Single(decision.Scores, s => s.Action == new UseMove(1));
        Assert.Contains(statusScore.Components, c => c.Name == "status" && c.Value > 0);
    }

    [Fact]
    public void FormulaPower_WithNoAuthoredPower_IsScoredFromTheCurrentTargetSnapshot()
    {
        var formula = new BattleMove(EntityId.Parse("move:formula"), Normal, DamageClass.Physical,
            null, 100, 10, 0, 0,
            hpBandPower: new HpBandPower(HpRatioPowerSource.Target, 64,
                [new HpPowerBand(32, 200), new HpPowerBand(64, 20)]));
        var attacker = Attacker(Damage(Normal, 5, slug: "weak"), formula);

        Assert.Equal(1, SmartAi.ChooseMove(attacker, Defender(Normal, curHp: 80), Chart(), new Rng(1)));
    }

    [Fact]
    public void HpFractionDamage_WithNoAuthoredPower_IsScoredAsDirectDamage()
    {
        var fraction = new BattleMove(EntityId.Parse("move:fraction"), Normal, DamageClass.Physical,
            null, 100, 10, 0, 0, secondaryEffects:
            [new HpFractionEffect(HpFractionRecipient.Target, HpFractionOperation.Damage,
                HpFractionBasis.CurrentHp, new Fraction(1, 2)), new CannotKoEffect(1)]);
        var attacker = Attacker(Damage(Normal, 5, slug: "weak"), fraction);

        Assert.Equal(1, SmartAi.ChooseMove(attacker, Defender(Normal), Chart(), new Rng(1)));
    }

    [Fact]
    public void HpEqualizeMatchSource_IsScoredAsExactDamage()
    {
        var equalize = new BattleMove(EntityId.Parse("move:equalize"), Normal, DamageClass.Status,
            null, 100, 10, 0, 0, secondaryEffects: [new HpEqualizeEffect(HpEqualizeMode.MatchSource)]);
        BattleCreature attacker = Attacker(Damage(Normal, 5, slug: "weak"), equalize);
        attacker.TakeDamage(80);

        Assert.Equal(1, SmartAi.ChooseMove(attacker, Defender(Normal), Chart(), new Rng(1)));
    }

    [Fact]
    public void StatusChanceFormula_ChangesSmartAiStatusValue()
    {
        var conditional = new BattleMove(EntityId.Parse("move:conditional"), Normal, DamageClass.Status,
            null, 100, 10, 0, 0, ailment: PersistentStatus.Paralysis, ailmentChance: 40,
            secondaryEffects: [new AilmentEffect(PersistentStatus.Paralysis)
            {
                Chance = 40,
                ChanceFormula = new StatusChanceFormula(StatusPowerSubject.User, PersistentStatus.Burn,
                    null, new Fraction(2, 1)),
            }]);
        var plain = new BattleMove(EntityId.Parse("move:plain"), Normal, DamageClass.Status,
            null, 100, 10, 0, 0, ailment: PersistentStatus.Paralysis, ailmentChance: 60);
        BattleCreature attacker = Attacker(plain, conditional);
        attacker.SetStatus(PersistentStatus.Burn);

        SmartAiDecision decision = SmartAi.ChooseAction(Ctx([attacker], [Defender(Normal)]));

        Assert.Equal(new UseMove(1), decision.Action);
        double conditionalValue = decision.Scores.Single(score => score.Action == new UseMove(1))
            .Components.Single(component => component.Name == "status").Value;
        double plainValue = decision.Scores.Single(score => score.Action == new UseMove(0))
            .Components.Single(component => component.Name == "status").Value;
        Assert.Equal(48, conditionalValue);
        Assert.Equal(36, plainValue);
    }

    [Fact]
    public void LegacyAilmentMetadata_RemainsSafeWhenTypedEffectsAreExplicitlyEmpty()
    {
        var legacy = new BattleMove(EntityId.Parse("move:legacy"), Normal, DamageClass.Status,
            null, 100, 10, 0, 0, ailment: PersistentStatus.Paralysis, ailmentChance: 60,
            secondaryEffects: []);
        BattleCreature attacker = Attacker(legacy);

        AiCandidateScore score = Assert.Single(SmartAi.ChooseAction(Ctx([attacker], [Defender(Normal)])).Scores);

        Assert.Equal(36, score.Components.Single(component => component.Name == "status").Value);
    }

    [Fact]
    public void Memory_RecordsSeenAndRepeatedPlayerMoves()
    {
        var memory = new SmartAiMemory();
        var player = Defender(Normal);

        memory.ObservePlayerAction(new UseMove(0), player);
        memory.ObservePlayerAction(new UseMove(0), player);

        Assert.Contains(player.Moves[0].Move, memory.SeenPlayerMoves);
        Assert.Equal(2, memory.RepeatedPlayerMoveCount);
    }

    [Fact]
    public void SmartAi_CanVoluntarilySwitch_WhenReserveIsMuchBetter()
    {
        var active = new BattleCreature(EntityId.Parse("species:bad"), "Bad", 50, [Grass],
            new Stats(120, 80, 80, 80, 80, 80), [Damage(Normal, 5)]);
        var reserve = new BattleCreature(EntityId.Parse("species:good"), "Good", 50, [Fire],
            new Stats(120, 100, 100, 100, 100, 100), [Damage(Fire, 80)]);
        var player = new BattleCreature(EntityId.Parse("species:p"), "P", 50, [Grass],
            new Stats(120, 100, 100, 100, 100, 100), [Damage(Fire, 100)]);

        var decision = SmartAi.ChooseAction(new SmartAiContext([active, reserve], 0, [player], 0, Chart(), new Rng(1),
            Weights: new SmartAiWeights { NoiseFraction = 0, SwitchThreshold = 0 }));

        Assert.Equal(new Switch(1), decision.Action);
    }

    [Fact]
    public void SwitchScoring_SubtractsExpectedSwitchInHazardDamage()
    {
        // A clearly-good pivot is available; entry hazards on the AI's own side must reduce the switch's
        // value by exactly the expected switch-in damage (stealth rock + 3 spikes layers).
        BattleCreature Active() => new(EntityId.Parse("species:bad"), "Bad", 50, [Grass],
            new Stats(120, 80, 80, 80, 80, 80), [Damage(Normal, 5)]);
        BattleCreature Reserve() => new(EntityId.Parse("species:good"), "Good", 50, [Fire],
            new Stats(120, 100, 100, 100, 100, 100), [Damage(Fire, 80)]);
        BattleCreature Player() => new(EntityId.Parse("species:p"), "P", 50, [Grass],
            new Stats(120, 100, 100, 100, 100, 100), [Damage(Fire, 100)]);
        var weights = new SmartAiWeights { NoiseFraction = 0, SwitchThreshold = 0 };

        AiCandidateScore SwitchScore(bool hazards) => Assert.Single(
            SmartAi.ChooseAction(new SmartAiContext([Active(), Reserve()], 0, [Player()], 0, Chart(), new Rng(1),
                Weights: weights, OwnStealthRock: hazards, OwnSpikeLayers: hazards ? 3 : 0)).Scores,
            s => s.Action is Switch);

        AiCandidateScore clean = SwitchScore(false);
        AiCandidateScore hazed = SwitchScore(true);
        double hazComp = hazed.Components.Single(c => c.Name == "switchInHazard").Value;

        Assert.Equal(0, clean.Components.Single(c => c.Name == "switchInHazard").Value); // no hazards → no penalty
        Assert.True(hazComp < 0);                                                        // hazards → penalty
        Assert.Equal(clean.Score + hazComp, hazed.Score, 3);                             // score drops by exactly it
    }

    [Fact]
    public void SwitchThreshold_AppliesToRelativeGain_NotBestMoveScore()
    {
        var active = new BattleCreature(EntityId.Parse("species:active"), "Active", 50, [Fire],
            new Stats(120, 100, 100, 100, 100, 100), [Damage(Fire, 60)]);
        var reserve = new BattleCreature(EntityId.Parse("species:reserve"), "Reserve", 50, [Fire],
            new Stats(120, 150, 100, 100, 100, 100), [Damage(Fire, 80)]);
        var player = new BattleCreature(EntityId.Parse("species:player"), "Player", 50, [Grass],
            new Stats(120, 100, 100, 100, 100, 100), [Damage(Normal, 10)]);

        var decision = SmartAi.ChooseAction(new SmartAiContext([active, reserve], 0, [player], 0,
            Chart(), new Rng(1), Weights: new SmartAiWeights { NoiseFraction = 0, SwitchThreshold = 35 }));

        Assert.Equal(new Switch(1), decision.Action);
    }

    [Fact]
    public void SwitchPrediction_UsesRepeatedSeenMoveInsteadOfStrongestMove()
    {
        var active = new BattleCreature(EntityId.Parse("species:active"), "Active", 50, [Fire],
            new Stats(120, 100, 60, 100, 100, 100), [Damage(Fire, 40)]);
        var reserve = new BattleCreature(EntityId.Parse("species:reserve"), "Reserve", 50, [Flying],
            new Stats(120, 100, 100, 100, 100, 100), [Damage(Normal, 40)]);
        var player = new BattleCreature(EntityId.Parse("species:player"), "Player", 50, [Grass],
            new Stats(120, 100, 100, 100, 100, 100), [Damage(Normal, 10, slug: "chip"), Damage(Ground, 120, slug: "quake")]);

        var memory = new SmartAiMemory();
        memory.ObservePlayerAction(new UseMove(0), player);
        memory.ObservePlayerAction(new UseMove(0), player);

        var weights = new SmartAiWeights { NoiseFraction = 0, SwitchThreshold = 0 };
        var withoutMemory = SmartAi.ChooseAction(new SmartAiContext([active, reserve], 0, [player], 0,
            Chart(), new Rng(1), Weights: weights));
        var withMemory = SmartAi.ChooseAction(new SmartAiContext([active, reserve], 0, [player], 0,
            Chart(), new Rng(1), Memory: memory, Weights: weights));

        Assert.Equal(new Switch(1), withoutMemory.Action);
        Assert.Equal(new UseMove(0), withMemory.Action);
    }

    [Fact]
    public void SmartAi_RespectsSwitchCooldown()
    {
        var memory = new SmartAiMemory();
        memory.MarkVoluntarySwitch(4);
        var active = new BattleCreature(EntityId.Parse("species:bad"), "Bad", 50, [Grass],
            new Stats(120, 80, 80, 80, 80, 80), [Damage(Normal, 5)]);
        var reserve = new BattleCreature(EntityId.Parse("species:good"), "Good", 50, [Fire],
            new Stats(120, 100, 100, 100, 100, 100), [Damage(Fire, 80)]);
        var player = new BattleCreature(EntityId.Parse("species:p"), "P", 50, [Grass],
            new Stats(120, 100, 100, 100, 100, 100), [Damage(Fire, 100)]);

        var decision = SmartAi.ChooseAction(new SmartAiContext([active, reserve], 0, [player], 0, Chart(), new Rng(1),
            Turn: 6, Memory: memory, Weights: new SmartAiWeights { NoiseFraction = 0, SwitchThreshold = 0 }));

        Assert.IsType<UseMove>(decision.Action);
    }

    [Fact]
    public void SmartAi_UsesHealingItemBelowThreshold()
    {
        var atk = Attacker(Damage(Normal, 10));
        atk.TakeDamage(90);

        var decision = SmartAi.ChooseAction(new SmartAiContext([atk], 0, [Defender(Normal)], 0, Chart(), new Rng(1),
            Items: [new TrainerBattleItem(Potion, Count: 1, HealAmount: 50)],
            Weights: new SmartAiWeights { NoiseFraction = 0 }));

        Assert.Equal(new UseBattleItem(Potion, 0, 50), decision.Action);
        Assert.Contains(decision.Scores, s => s.Action == decision.Action
            && s.Components.Any(c => c.Name == "itemHeal" && c.Value == 50));
    }

    [Fact]
    public void SmartAi_DoesNotUseHealingItemAboveThreshold()
    {
        var atk = Attacker(Damage(Normal, 10));
        atk.TakeDamage(40);

        var decision = SmartAi.ChooseAction(new SmartAiContext([atk], 0, [Defender(Normal)], 0, Chart(), new Rng(1),
            Items: [new TrainerBattleItem(Potion, Count: 1, HealAmount: 50)],
            Weights: new SmartAiWeights { NoiseFraction = 0 }));

        Assert.IsType<UseMove>(decision.Action);
    }

    [Fact]
    public void PrefersKo_OverHealingItem_WhenLowHp()
    {
        // Below the heal threshold, but a guaranteed KO is on the board: attack, don't waste a heal.
        var atk = Attacker(Damage(Normal, 40));
        atk.TakeDamage(90); // 10/100
        var target = Defender(Normal, hp: 200, curHp: 5); // the chip move is lethal

        var decision = SmartAi.ChooseAction(new SmartAiContext([atk], 0, [target], 0, Chart(), new Rng(1),
            Items: [new TrainerBattleItem(Potion, Count: 1, HealAmount: 50)],
            Weights: new SmartAiWeights { NoiseFraction = 0 }));

        Assert.Equal(new UseMove(0), decision.Action);
    }

    [Fact]
    public void PicksTheStrongerHealingItem_WhenBelowThreshold()
    {
        // Two heals available and no worthwhile attack: use the one that restores more.
        var atk = Attacker(Damage(Normal, 5));
        atk.TakeDamage(85); // 15/100 → 85 missing, below threshold
        var target = Defender(Normal, hp: 300);

        var decision = SmartAi.ChooseAction(new SmartAiContext([atk], 0, [target], 0, Chart(), new Rng(1),
            Items: [new TrainerBattleItem(Potion, Count: 1, HealAmount: 30),
                    new TrainerBattleItem(SuperPotion, Count: 1, HealAmount: 80)],
            Weights: new SmartAiWeights { NoiseFraction = 0 }));

        Assert.Equal(new UseBattleItem(SuperPotion, 0, 80), decision.Action);
    }

    [Fact]
    public void FallsBackToFirstMove_WhenAllMovesOutOfPp()
    {
        // No usable move and no reserve: don't crash, pick move 0 (the controller resolves Struggle).
        var atk = Attacker(Damage(Normal, 40, pp: 0), Damage(Fire, 60, pp: 0));
        var decision = SmartAi.ChooseAction(Ctx([atk], [Defender(Grass)]));
        Assert.Equal(new UseMove(0), decision.Action);
    }

    [Fact]
    public void ValuesForceSwitch_AgainstDangerouslyBoostedTarget()
    {
        // Player is +2 Atk. Roar (force-out) is worth more than a chip attack.
        var atk = Attacker(Damage(Normal, 10), Roar());
        var target = Defender(Normal, hp: 300);
        target.ChangeStage(StatKind.Atk, 2);
        Assert.Equal(new UseMove(1), SmartAi.ChooseAction(Ctx([atk], [target])).Action);
    }

    [Fact]
    public void IgnoresForceSwitch_WhenTargetNotBoosted()
    {
        // No boost to punish: Roar has no value, the chip attack wins.
        var atk = Attacker(Damage(Normal, 10), Roar());
        Assert.Equal(new UseMove(0), SmartAi.ChooseAction(Ctx([atk], [Defender(Normal, hp: 300)])).Action);
    }

    [Fact]
    public void ValuesProtect_WhenThreatenedWithAKo()
    {
        // Attacker is at 1 HP facing a lethal move: Protect beats a weak swing.
        var atk = new BattleCreature(EntityId.Parse("species:frail"), "F", 50, [Fire],
            new Stats(1, 100, 100, 100, 100, 100), [Damage(Normal, 10), Protect()]);
        var target = Defender(Normal, hp: 200); // its 40-power move KOs a 1-HP target
        Assert.Equal(new UseMove(1), SmartAi.ChooseAction(Ctx([atk], [target])).Action);
    }

    [Fact]
    public void IgnoresProtect_WhenNotThreatened()
    {
        // Full-HP attacker not in KO range: no reason to Protect, attack instead.
        var atk = Attacker(Damage(Normal, 30), Protect());
        Assert.Equal(new UseMove(0), SmartAi.ChooseAction(Ctx([atk], [Defender(Normal, hp: 300)])).Action);
    }

    [Fact]
    public void AvoidsSelfDestruct_WhenItWouldNotKo()
    {
        // Huge-HP user: the self-KO cost dwarfs the extra damage, so use the normal move.
        var atk = new BattleCreature(EntityId.Parse("species:bulk"), "B", 50, [Fire],
            new Stats(400, 100, 100, 100, 100, 100), [Explosion(), Damage(Normal, 60)]);
        Assert.Equal(new UseMove(1), SmartAi.ChooseAction(Ctx([atk], [Defender(Normal, hp: 300)])).Action);
    }

    [Fact]
    public void ValuesHazards_OnlyWhenReservesRemain()
    {
        var atk = Attacker(Damage(Normal, 5), Spikes());
        var lone = new[] { Defender(Normal, hp: 300) };
        var withReserve = new[] { Defender(Normal, hp: 300), Defender(Normal, hp: 300) };

        Assert.Equal(new UseMove(0), SmartAi.ChooseAction(Ctx([atk], lone)).Action);      // no reserve → attack
        Assert.Equal(new UseMove(1), SmartAi.ChooseAction(Ctx([atk], withReserve)).Action); // reserve → lay Spikes
    }

    [Fact]
    public void TrappedCreature_DoesNotSwitch_EvenWithABetterReserve()
    {
        var active = new BattleCreature(EntityId.Parse("species:bad"), "Bad", 50, [Grass],
            new Stats(120, 80, 80, 80, 80, 80), [Damage(Normal, 5)]);
        var reserve = new BattleCreature(EntityId.Parse("species:good"), "Good", 50, [Fire],
            new Stats(120, 100, 100, 100, 100, 100), [Damage(Fire, 80)]);
        var player = new BattleCreature(EntityId.Parse("species:p"), "P", 50, [Grass],
            new Stats(120, 100, 100, 100, 100, 100), [Damage(Fire, 100)]);
        active.SetTrap(3);

        var decision = SmartAi.ChooseAction(Ctx([active, reserve], [player], switchThreshold: 0));
        Assert.IsType<UseMove>(decision.Action);
    }

    [Fact]
    public void DoesNotSwitchToAFaintedReserve()
    {
        var active = new BattleCreature(EntityId.Parse("species:bad"), "Bad", 50, [Grass],
            new Stats(120, 80, 80, 80, 80, 80), [Damage(Normal, 5)]);
        var reserve = new BattleCreature(EntityId.Parse("species:ko"), "KO", 50, [Fire],
            new Stats(120, 100, 100, 100, 100, 100), [Damage(Fire, 80)]);
        var player = new BattleCreature(EntityId.Parse("species:p"), "P", 50, [Grass],
            new Stats(120, 100, 100, 100, 100, 100), [Damage(Fire, 100)]);
        reserve.TakeDamage(reserve.MaxHp);

        var decision = SmartAi.ChooseAction(Ctx([active, reserve], [player], switchThreshold: 0));
        Assert.IsType<UseMove>(decision.Action);
    }
}
