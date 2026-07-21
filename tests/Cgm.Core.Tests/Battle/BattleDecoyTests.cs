using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleDecoyTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly BattleSlot PlayerSlot = new(BattleSide.Player, 0);

    [Fact]
    public void CompilerAdmitsOnlyTheClosedDecoyShape()
    {
        DecoyEffect effect = Assert.IsType<DecoyEffect>(Assert.Single(Compile("valid",
            DamageClass.Status, null, MoveTarget.User, Op("decoy")).SecondaryEffects));
        Assert.Equal(new Fraction(1, 4), effect.Fraction);
        Assert.Equal(new Fraction(1, 3), Assert.IsType<DecoyEffect>(Assert.Single(Compile("custom",
            DamageClass.Status, null, MoveTarget.User, Op("decoy", ("num", 1), ("den", 3)))
            .SecondaryEffects)).Fraction);

        Assert.Throws<ArgumentException>(() => Compile("chance", DamageClass.Status, null, MoveTarget.User,
            Op("decoy", 50)));
        Assert.Throws<ArgumentException>(() => Compile("damaging", DamageClass.Physical, 40, MoveTarget.User,
            Op("damage"), Op("decoy")));
        Assert.Throws<ArgumentException>(() => Compile("target", DamageClass.Status, null, MoveTarget.Selected,
            Op("decoy")));
        Assert.Throws<ArgumentException>(() => Compile("zero", DamageClass.Status, null, MoveTarget.User,
            Op("decoy", ("num", 0), ("den", 4))));
        Assert.Throws<ArgumentException>(() => Compile("whole", DamageClass.Status, null, MoveTarget.User,
            Op("decoy", ("num", 4), ("den", 4))));
        Assert.Throws<ArgumentException>(() => Compile("duplicate", DamageClass.Status, null, MoveTarget.User,
            Op("decoy"), Op("decoy")));
    }

    [Fact]
    public void CreationPaysExactCostAndDuplicateOrInsufficientAttemptsAreAtomic()
    {
        BattleMove create = CreateDecoy();
        BattleCreature player = Creature("creator", 100, 100, create);
        BattleController battle = Battle(player, Creature("idle", 100, 1, Wait()));

        IReadOnlyList<BattleEvent> created = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Equal(75, player.CurrentHp);
        Assert.Equal(new BattleDecoyState(25, 25), Decoy(battle, player));
        Assert.Contains(created, e => e == new HpCostPaid(PlayerSlot, 25));
        Assert.Contains(created, e => e == new DecoyCreated(PlayerSlot, 25));

        IReadOnlyList<BattleEvent> duplicate = battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Equal(75, player.CurrentHp);
        Assert.Equal(new BattleDecoyState(25, 25), Decoy(battle, player));
        Assert.Contains(duplicate, e => e == new DecoyCreationFailed(PlayerSlot,
            DecoyCreationFailure.AlreadyActive));

        BattleCreature low = Creature("low", 100, 100, create);
        low.TakeDamage(75);
        BattleController insufficient = Battle(low, Creature("idle_low", 100, 1, Wait()));
        IReadOnlyList<BattleEvent> failed = insufficient.ResolveTurn(new UseMove(0), new Pass());
        Assert.Equal(25, low.CurrentHp);
        Assert.Null(Decoy(insufficient, low));
        Assert.Contains(failed, e => e == new DecoyCreationFailed(PlayerSlot,
            DecoyCreationFailure.InsufficientHp));
        Assert.DoesNotContain(failed, e => e is HpCostPaid);
    }

    [Fact]
    public void BreakingHitDoesNotOverflowAndBlocksItsSecondaryWhileHistoryNamesTheDecoy()
    {
        BattleCreature player = Creature("protected", 100, 100, CreateDecoy());
        BattleMove hit = Compile("breaking_hit", DamageClass.Physical, null, MoveTarget.Selected,
            Op("fixedDamage", ("amount", 40)), Op("ailment", 100, ("ailment", "poison")));
        BattleController battle = Battle(player, Creature("attacker", 100, 1, hit));
        battle.ResolveTurn(new UseMove(0), new Pass());

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Pass(), new UseMove(0));

        Assert.Equal(75, player.CurrentHp);
        Assert.Null(player.Status);
        Assert.Null(Decoy(battle, player));
        Assert.Contains(events, e => e == new DecoyDamaged(PlayerSlot, 25, 0));
        Assert.Contains(events, e => e == new DecoyBroken(PlayerSlot));
        Assert.Contains(events, e => e is DecoyBlocked { Slot: { Side: BattleSide.Player } });
        BattleDamageRecord record = battle.ActionHistory.DamageSnapshot().Last();
        Assert.True(record.Substitute);
        Assert.False(record.Connected);
        Assert.Equal(BattleDamageFailure.Substitute, record.Failure);
        Assert.Equal(0, record.ActualHpRemoved);
        Assert.Equal(25, record.AppliedDamage);
    }

    [Fact]
    public void LaterMultiHitDamageReMaterializesAfterTheDecoyBreaks()
    {
        BattleMove tinyDecoy = Compile("tiny_decoy", DamageClass.Status, null, MoveTarget.User,
            Op("decoy", ("num", 1), ("den", 100)));
        BattleMove multi = Compile("multi", DamageClass.Physical, 100, MoveTarget.Selected,
            Op("damage"), Op("multiHit", ("min", 2), ("max", 2)));
        BattleCreature player = Creature("multi_target", 100, 100, tinyDecoy);
        BattleController battle = Battle(player, Creature("multi_source", 100, 1, multi));
        battle.ResolveTurn(new UseMove(0), new Pass());

        battle.ResolveTurn(new Pass(), new UseMove(0));

        BattleDamageRecord[] records = battle.ActionHistory.DamageSnapshot()
            .Where(record => record.Move == multi.Move).ToArray();
        Assert.Equal(2, records.Length);
        Assert.True(records[0].Substitute);
        Assert.False(records[1].Substitute);
        Assert.True(records[1].ActualHpRemoved > 0);
        Assert.True(player.CurrentHp < 99);
    }

    [Fact]
    public void StatusAndDamageBypassTagsUseTheSameEffectiveDecoyState()
    {
        BattleMove blocked = Compile("blocked_status", DamageClass.Status, null, MoveTarget.Selected,
            Op("ailment", 100, ("ailment", "poison")));
        BattleMove soundStatus = Compile("sound_status", DamageClass.Status, null, MoveTarget.Selected,
            Op("moveTags", ("tags", "sound")), Op("ailment", 100, ("ailment", "poison")));
        BattleMove soundDamage = Compile("sound_damage", DamageClass.Physical, null, MoveTarget.Selected,
            Op("moveTags", ("tags", "sound")), Op("fixedDamage", ("amount", 10)));
        BattleCreature player = Creature("bypass_target", 100, 100, CreateDecoy());
        BattleCreature enemy = Creature("bypass_source", 100, 1, blocked, soundStatus, soundDamage);
        BattleController battle = Battle(player, enemy);
        battle.ResolveTurn(new UseMove(0), new Pass());

        IReadOnlyList<BattleEvent> blockedEvents = battle.ResolveTurn(new Pass(), new UseMove(0));
        Assert.Null(player.Status);
        Assert.Contains(blockedEvents, e => e is DecoyBlocked);

        battle.ResolveTurn(new Pass(), new UseMove(1));
        Assert.Equal(PersistentStatus.Poison, player.Status);
        int before = player.CurrentHp;
        battle.ResolveTurn(new Pass(), new UseMove(2));
        Assert.True(player.CurrentHp < before);
        Assert.Equal(new BattleDecoyState(25, 25), Decoy(battle, player));
    }

    [Fact]
    public void SwitchCleanupAndSmartAiPreflightConsumeTheSharedOverlay()
    {
        BattleMove create = CreateDecoy();
        BattleCreature player = Creature("switch_owner", 100, 100, create);
        BattleCreature reserve = Creature("reserve", 100, 50, Wait());
        BattleController battle = new([player, reserve], [Creature("switch_enemy", 100, 1, Wait())],
            Chart(), new Rng(3));
        battle.ResolveTurn(new UseMove(0), new Pass());
        battle.ResolveTurn(new Switch(1), new Pass());

        Assert.Null(battle.Overlays.Resolve(new BattleOverlayOwner(BattleSide.Player, 0),
            PhysicalMetricFormulas.BaseEffectiveValues(player)).Values.Decoy);

        var overlays = new BattleOverlayStore();
        var enemyOwner = new BattleOverlayOwner(BattleSide.Enemy, 0, new BattleSlot(BattleSide.Enemy, 0));
        BattleCreature ai = Creature("ai", 100, 100, create, Compile("chip", DamageClass.Physical, 20,
            MoveTarget.Selected, Op("damage")));
        overlays.Apply(new BattleOverlayApplication(enemyOwner, new(), BattleOverlayLayer.FormOrSnapshot,
            new DecoyOverlay(new BattleDecoyState(25, 25)), 0, 0));
        SmartAiDecision decision = SmartAi.ChooseAction(new SmartAiContext([ai], 0,
            [Creature("ai_target", 100, 1, Wait())], 0, Chart(), new Rng(4), Overlays: overlays,
            Weights: new SmartAiWeights { NoiseFraction = 0 }));
        AiCandidateScore decoy = Assert.Single(decision.Scores, score => score.Action == new UseMove(0));
        Assert.Contains(decoy.Components, component => component.Name == "decoyUnavailable"
            && component.Value == -1_000_000);
        Assert.Equal(new UseMove(1), decision.Action);
    }

    [Fact]
    public void FaintAndBattleEndCleanupRemoveTheDecoyOverlay()
    {
        BattleMove bypass = Compile("lethal_bypass", DamageClass.Physical, null, MoveTarget.Selected,
            Op("moveTags", ("tags", "decoy_bypass")), Op("fixedDamage", ("amount", 100)));
        BattleCreature player = Creature("faint_owner", 100, 100, CreateDecoy());
        BattleController battle = Battle(player, Creature("faint_source", 100, 1, bypass));
        battle.ResolveTurn(new UseMove(0), new Pass());

        battle.ResolveTurn(new Pass(), new UseMove(0));

        Assert.True(player.IsFainted);
        Assert.Equal(BattleSide.Enemy, battle.Outcome?.Winner);
        Assert.Null(battle.Overlays.Resolve(new BattleOverlayOwner(BattleSide.Player, 0),
            PhysicalMetricFormulas.BaseEffectiveValues(player)).Values.Decoy);
    }

    [Fact]
    public void AbsorbedDamageFeedsDrainAndRecoilButDirectHealingIgnoresTheDecoy()
    {
        BattleMove absorb = Compile("absorb", DamageClass.Physical, null, MoveTarget.Selected,
            Op("fixedDamage", ("amount", 40)), Op("drain", ("num", 1), ("den", 2)),
            Op("recoil", ("num", 1), ("den", 4)));
        BattleMove heal = Compile("heal_target", DamageClass.Status, null, MoveTarget.Selected,
            Op("heal", ("recipient", "target"), ("num", 1), ("den", 10)));
        BattleCreature player = Creature("accounting_target", 100, 100, CreateDecoy());
        BattleCreature enemy = Creature("accounting_source", 100, 1, absorb, heal);
        enemy.TakeDamage(50);
        BattleController battle = Battle(player, enemy);
        battle.ResolveTurn(new UseMove(0), new Pass());

        battle.ResolveTurn(new Pass(), new UseMove(0));
        Assert.Equal(56, enemy.CurrentHp); // 25 absorbed -> heal 12, recoil 6.
        Assert.Equal(75, player.CurrentHp);

        battle.ResolveTurn(new UseMove(0), new Pass());
        int before = player.CurrentHp;
        IReadOnlyList<BattleEvent> healed = battle.ResolveTurn(new Pass(), new UseMove(1));
        Assert.Equal(before + 10, player.CurrentHp);
        Assert.Equal(new BattleDecoyState(25, 25), Decoy(battle, player));
        Assert.DoesNotContain(healed, e => e is DecoyBlocked);
    }

    [Fact]
    public void DoublesSpreadInterceptionIsIsolatedToTheOwningCreature()
    {
        BattleMove spread = Compile("spread", DamageClass.Physical, null, MoveTarget.AllOpponents,
            Op("fixedDamage", ("amount", 40)));
        BattleCreature enemy0 = Creature("spread_target_0", 100, 10, Wait());
        BattleCreature enemy1 = Creature("spread_target_1", 100, 10, Wait());
        var battle = new BattleController(
            [Creature("spread_source", 100, 100, spread), Creature("spread_ally", 100, 50, Wait())],
            [enemy0, enemy1], BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new Rng(5));
        BattleSlot protectedSlot = new(BattleSide.Enemy, 1);
        battle.Overlays.Apply(new BattleOverlayApplication(
            new BattleOverlayOwner(BattleSide.Enemy, 1, protectedSlot), new(Entity: spread.Move),
            BattleOverlayLayer.FormOrSnapshot, new DecoyOverlay(new BattleDecoyState(20, 20)), 0, 0,
            Cleanup: BattleOverlayCleanup.Switch | BattleOverlayCleanup.Faint | BattleOverlayCleanup.BattleEnd));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0)),
            new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 0), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
        ]));

        Assert.Equal(60, enemy0.CurrentHp);
        Assert.Equal(100, enemy1.CurrentHp);
        Assert.Contains(events, e => e == new DecoyDamaged(protectedSlot, 20, 0));
        BattleDamageRecord[] records = battle.ActionHistory.DamageSnapshot()
            .Where(record => record.Move == spread.Move).ToArray();
        Assert.Equal(2, records.Length);
        Assert.False(records[0].Substitute);
        Assert.True(records[1].Substitute);
    }

    [Fact]
    public void DelayedDamageUsesTheLiveDecoyAtItsDueCheckpoint()
    {
        BattleMove delayed = Compile("delayed", DamageClass.Special, 80, MoveTarget.Selected,
            Op("damage"), Op("delayedDamage", ("turns", 2)));
        BattleCreature player = Creature("delayed_target", 100, 100, CreateDecoy());
        BattleController battle = Battle(player, Creature("delayed_source", 100, 1, delayed));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        battle.ResolveTurn(new Pass(), new Pass());

        IReadOnlyList<BattleEvent> due = battle.ResolveTurn(new Pass(), new Pass());

        Assert.Equal(75, player.CurrentHp);
        Assert.Contains(due, e => e is DecoyDamaged { Slot.Side: BattleSide.Player });
        Assert.Contains(due, e => e is DelayedActionResolved { Payload: BattleIntentPayloadKind.DelayedDamage });
        BattleDamageRecord record = battle.ActionHistory.DamageSnapshot().Last();
        Assert.True(record.Substitute);
        Assert.Equal(BattleDamageFailure.Substitute, record.Failure);
    }

    [Fact]
    public void DecoyLifecycleMatchesDeterministicGolden()
    {
        static string Run()
        {
            BattleCreature player = Creature("golden_target", 100, 100, CreateDecoy());
            BattleMove hit = Compile("golden_hit", DamageClass.Physical, null, MoveTarget.Selected,
                Op("fixedDamage", ("amount", 40)), Op("ailment", 100, ("ailment", "poison")));
            BattleController battle = Battle(player, Creature("golden_source", 100, 1, hit));
            battle.ResolveTurn(new UseMove(0), new Pass());
            battle.ResolveTurn(new Pass(), new UseMove(0));
            return string.Join('\n', battle.Log.Select(EventRow).Concat(battle.Trace
                .Where(trace => trace.Kind == EffectTraceKind.Decoy)
                .Select(trace => $"trace:{trace.SourceSlot.Side}:{trace.TargetSlot?.Side}:"
                    + $"{trace.Performed}:{trace.Value}:{trace.EventStartIndex}-{trace.EventEndIndex}")));
        }

        string first = Run();
        Assert.Equal(first, Run());
        Assert.Equal(Golden("decoy-lifecycle"), first);
    }

    private static BattleController Battle(BattleCreature player, BattleCreature enemy) =>
        new(player, enemy, Chart(), new Rng(7));

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleDecoyState? Decoy(BattleController battle, BattleCreature creature) =>
        battle.Overlays.Resolve(new BattleOverlayOwner(BattleSide.Player, 0, PlayerSlot),
            PhysicalMetricFormulas.BaseEffectiveValues(creature)).Values.Decoy;

    private static BattleCreature Creature(string slug, int hp, int speed, params BattleMove[] moves) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
            new Stats(hp, 100, 100, 100, 100, speed), moves);

    private static BattleMove CreateDecoy() => Compile("create_decoy", DamageClass.Status, null,
        MoveTarget.User, Op("decoy"));

    private static BattleMove Wait() => new(EntityId.Parse("move:wait"), Normal,
        DamageClass.Status, null, null, 20, 0, 0);

    private static BattleMove Compile(string slug, DamageClass damageClass, int? power,
        MoveTarget target, params Effect[] effects) => MoveCompiler.ToBattleMove(new Move
    {
        Id = EntityId.Parse($"move:{slug}"), Name = slug, Type = Normal, DamageClass = damageClass,
        Power = power, Accuracy = 100, Pp = 20, Target = target, Effects = effects,
    });

    private static Effect Op(string op, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Params = values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static Effect Op(string op, int chance, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Chance = chance,
        Params = values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static string EventRow(BattleEvent battleEvent) => battleEvent switch
    {
        MoveUsed used => $"used:{used.Slot.Side}:{used.Move}",
        HpCostPaid cost => $"cost:{cost.Slot.Side}:{cost.Amount}",
        DecoyCreated created => $"created:{created.Slot.Side}:{created.Hp}",
        DecoyDamaged damaged => $"damaged:{damaged.Slot.Side}:{damaged.Amount}:{damaged.RemainingHp}",
        DecoyBroken broken => $"broken:{broken.Slot.Side}",
        DecoyBlocked blocked => $"blocked:{blocked.Slot.Side}",
        _ => $"event:{battleEvent.GetType().Name}",
    };

    private static string Golden(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden"))
        .Replace("\r\n", "\n").TrimEnd();
}
