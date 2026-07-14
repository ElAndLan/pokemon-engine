using Cgm.Core.Battle;
using Cgm.Core.Model;
using System.Text.Json;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleDoublesDamageTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Ghost = EntityId.Parse("type:ghost");

    [Fact]
    public void SpreadDamage_UsesTheSnapshottedTwoTargetModifier()
    {
        BattleController battle = Battle(new BattleMove(EntityId.Parse("move:spread_probe"), Normal,
            DamageClass.Special, 100, 100, 10, 0, 0, target: MoveTarget.AllOpponents),
            new FakeRng(ints: [0, 0, 15, 15], doubles: [0.99, 0.99]));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(new UseMove(0)));

        DamageDealt[] damage = events.OfType<DamageDealt>().Where(item => item.Target == BattleSide.Enemy).ToArray();
        Assert.Equal([new BattleSlot(BattleSide.Enemy, 0), new BattleSlot(BattleSide.Enemy, 1)], damage.Select(item => item.Slot));
        Assert.Equal([51, 51], damage.Select(item => item.Amount));
    }

    [Fact]
    public void SpreadDamage_WithOneLiveTargetHasNoSpreadReduction()
    {
        BattleController battle = Battle(new BattleMove(EntityId.Parse("move:single_probe"), Normal,
            DamageClass.Special, 100, 100, 10, 0, 0, target: MoveTarget.AllOpponents),
            new FakeRng(ints: [0, 15], doubles: [0.99]));
        battle.Active(new BattleSlot(BattleSide.Enemy, 1)).TakeDamage(200);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0)),
            new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 0), new Pass()),
        ]));

        DamageDealt damage = Assert.Single(events.OfType<DamageDealt>(), item => item.Target == BattleSide.Enemy);
        Assert.Equal(new BattleSlot(BattleSide.Enemy, 0), damage.Slot);
        Assert.Equal(69, damage.Amount);
    }

    [Fact]
    public void SpreadDamage_RollsAccuracyPerTargetAndSkipsHitDrawsForImmuneTarget()
    {
        var rng = new CountingRng(0, 0, 15);
        BattleController battle = Battle(new BattleMove(EntityId.Parse("move:immunity_probe"), Normal,
            DamageClass.Special, 100, 50, 10, 0, 0, target: MoveTarget.AllOpponents), rng,
            Creature("e0", Ghost), Creature("e1", Normal));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(new UseMove(0)));

        DamageDealt[] damage = events.OfType<DamageDealt>().Where(item => item.Target == BattleSide.Enemy).ToArray();
        Assert.Equal([0, 51], damage.Select(item => item.Amount));
        Assert.Equal(3, rng.IntCalls); // accuracy ×2, then one non-immune damage roll
        Assert.Equal(1, rng.DoubleCalls);
    }

    [Fact]
    public void SpreadDamage_AllowsOneTargetToMissWithoutCancellingOtherTargets()
    {
        BattleController battle = Battle(new BattleMove(EntityId.Parse("move:accuracy_probe"), Normal,
            DamageClass.Special, 100, 50, 10, 0, 0, target: MoveTarget.AllOpponents),
            new FakeRng(ints: [0, 99, 15], doubles: [0.99]));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(new UseMove(0)));

        DamageDealt damage = Assert.Single(events.OfType<DamageDealt>(), item => item.Target == BattleSide.Enemy);
        Assert.Equal(new BattleSlot(BattleSide.Enemy, 0), damage.Slot);
        Assert.Contains(events, item => item is MoveMissed { TargetSlot: { Side: BattleSide.Enemy, Position: 1 } });
    }

    [Fact]
    public void SpreadMultiHit_UsesOneActionHitCountInTargetThenHitOrder()
    {
        var rng = new CountingRng(0, 0, 6, 15, 15, 15, 15, 15, 15, 15, 15);
        BattleController battle = Battle(new BattleMove(EntityId.Parse("move:multi_probe"), Normal,
            DamageClass.Special, 25, 100, 10, 0, 0, multiHitMin: 2, multiHitMax: 5,
            target: MoveTarget.AllOpponents), rng);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(new UseMove(0)));

        DamageDealt[] damage = events.OfType<DamageDealt>().Where(item => item.Target == BattleSide.Enemy).ToArray();
        Assert.Equal(
        [
            new BattleSlot(BattleSide.Enemy, 0), new BattleSlot(BattleSide.Enemy, 0),
            new BattleSlot(BattleSide.Enemy, 0), new BattleSlot(BattleSide.Enemy, 0),
            new BattleSlot(BattleSide.Enemy, 1), new BattleSlot(BattleSide.Enemy, 1),
            new BattleSlot(BattleSide.Enemy, 1), new BattleSlot(BattleSide.Enemy, 1),
        ], damage.Select(item => item.Slot));
        Assert.Equal(11, rng.IntCalls); // accuracy ×2, hit count once, then 8 damage rolls
        Assert.Equal(8, rng.DoubleCalls);
    }

    [Fact]
    public void SpreadTargetEffectsPrecedeOncePerActionDrainAndRecoil()
    {
        BattleMove move = new(EntityId.Parse("move:aggregate_probe"), Normal, DamageClass.Special, 100, 100, 10, 0, 0,
            target: MoveTarget.AllOpponents,
            secondaryEffects:
            [
                new StatChangeEffect(StatKind.Atk, -1, OnSelf: false),
                new DrainEffect(new Fraction(1, 2)),
                new RecoilEffect(new Fraction(1, 4)),
            ]);
        BattleController battle = Battle(move, new FakeRng(ints: [0, 0, 15, 15], doubles: [0.99, 0.99]));
        battle.Active(new BattleSlot(BattleSide.Player, 0)).TakeDamage(150);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(new UseMove(0)));

        Assert.Equal(-1, battle.Active(new BattleSlot(BattleSide.Enemy, 0)).Stage(StatKind.Atk));
        Assert.Equal(-1, battle.Active(new BattleSlot(BattleSide.Enemy, 1)).Stage(StatKind.Atk));
        Assert.Equal(76, battle.Active(new BattleSlot(BattleSide.Player, 0)).CurrentHp);
        Type[] order = events.Where(item => item is DamageDealt or StatStageChanged or Healed or Recoiled)
            .Select(item => item.GetType()).ToArray();
        Assert.Equal(
        [
            typeof(DamageDealt), typeof(DamageDealt),
            typeof(StatStageChanged), typeof(StatStageChanged),
            typeof(Healed), typeof(Recoiled),
        ], order);
    }

    [Fact]
    public void SpreadContactConsequencesUseTheContactedTargetSlot()
    {
        BattleMove move = new(EntityId.Parse("move:contact_probe"), Normal, DamageClass.Physical, 100, 100, 10, 0, 0,
            makesContact: true, target: MoveTarget.AllOpponents);
        AbilityHook contactHook = new()
        {
            Hook = AbilityHookPoint.OnContactReceived,
            Effects =
            [
                new Effect
                {
                    Op = "contactChanceEffect",
                    Chance = 100,
                    Params = Params(("stat", "atk"), ("delta", -1)),
                },
            ],
        };
        BattleController battle = Battle(move, new FakeRng(ints: [0, 0, 15, 15], doubles: [0.99, 0.99]),
            Creature("e0", Normal, abilityHooks: [contactHook]), Creature("e1", Normal));

        battle.ResolveTurn(Actions(new UseMove(0)));

        Assert.Equal(-1, battle.Active(new BattleSlot(BattleSide.Player, 0)).Stage(StatKind.Atk));
        EffectTraceEntry chance = Assert.Single(battle.Trace, entry => entry.Kind == EffectTraceKind.ContactChance);
        Assert.Equal(new BattleSlot(BattleSide.Enemy, 0), chance.TargetSlot);
        Assert.False(chance.Performed);
        Assert.Null(chance.DrawResult);
        Assert.Equal(1, chance.Value);
        Assert.True(chance.EventEndIndex > chance.EventStartIndex);
    }

    [Fact]
    public void ContactChanceTraceRecordsEachContactedHookDraw()
    {
        BattleMove move = new(EntityId.Parse("move:contact_chance_trace_probe"), Normal, DamageClass.Physical, 100, 100, 10, 0, 0,
            makesContact: true, target: MoveTarget.AllOpponents);
        AbilityHook hook = new()
        {
            Hook = AbilityHookPoint.OnContactReceived,
            Effects = [new Effect { Op = "contactChanceEffect", Chance = 50, Params = Params(("stat", "atk"), ("delta", -1)) }],
        };
        BattleController battle = Battle(move, new FakeRng(ints: [0, 0, 15, 15, 10, 90], doubles: [0.99, 0.99]),
            Creature("e0", Normal, abilityHooks: [hook]), Creature("e1", Normal, abilityHooks: [hook]));

        battle.ResolveTurn(Actions(new UseMove(0)));

        EffectTraceEntry[] chances = battle.Trace.Where(entry => entry.Kind == EffectTraceKind.ContactChance).ToArray();
        Assert.Equal([new BattleSlot(BattleSide.Enemy, 0), new BattleSlot(BattleSide.Enemy, 1)], chances.Select(entry => entry.TargetSlot));
        Assert.Equal([10d, 90d], chances.Select(entry => entry.DrawResult));
        Assert.All(chances, entry =>
        {
            Assert.True(entry.Performed);
            Assert.Equal(100d, entry.DrawBound);
        });
        Assert.Equal([1, 0], chances.Select(entry => entry.Value));
        Assert.Equal(-1, battle.Active(new BattleSlot(BattleSide.Player, 0)).Stage(StatKind.Atk));
    }

    [Fact]
    public void TiedDoublesActionsTraceFisherYatesDraw()
    {
        BattleMove p0Move = new(EntityId.Parse("move:tie_probe_a"), Normal, DamageClass.Status, null, null, 10, 0, 0,
            target: MoveTarget.User);
        BattleMove p1Move = new(EntityId.Parse("move:tie_probe_b"), Normal, DamageClass.Status, null, null, 10, 0, 0,
            target: MoveTarget.User);
        var battle = new BattleController(
            [Creature("p0", Normal, p0Move), Creature("p1", Normal, p1Move)],
            [Creature("e0", Normal), Creature("e1", Normal)], BattleTopology.Doubles, [0, 1], [0, 1], Chart(),
            new FakeRng(ints: [0]));
        var actions = new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0)),
            new BattleActionSubmission(new(BattleSide.Player, 1), new UseMove(0)),
            new BattleActionSubmission(new(BattleSide.Enemy, 0), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
        ]);

        battle.ResolveTurn(actions);

        EffectTraceEntry tie = Assert.Single(battle.Trace, entry => entry.Kind == EffectTraceKind.TurnOrderTie);
        Assert.Equal(0, tie.ActionSequence);
        Assert.Equal(new BattleSlot(BattleSide.Player, 1), tie.SourceSlot);
        Assert.True(tie.Performed);
        Assert.Equal(0d, tie.DrawResult);
        Assert.Equal(2d, tie.DrawBound);
        Assert.Equal(1, tie.Value);
    }

    [Fact]
    public void ContactDamageCanFaintSourceAfterSnapshottedSpreadDamage()
    {
        BattleMove move = new(EntityId.Parse("move:contact_faint_probe"), Normal, DamageClass.Physical, 100, 100, 10, 0, 0,
            makesContact: true, target: MoveTarget.AllOpponents);
        AbilityHook contactHook = new()
        {
            Hook = AbilityHookPoint.OnContactReceived,
            Effects = [new Effect { Op = "contactChanceEffect", Chance = 100, Params = Params(("damage", 1)) }],
        };
        BattleCreature source = new(EntityId.Parse("species:fragile"), "fragile", 50, [Normal],
            new Stats(1, 100, 100, 100, 100, 100), [move]);
        var battle = new BattleController([source, Creature("p1", Normal)],
            [Creature("e0", Normal, abilityHooks: [contactHook]), Creature("e1", Normal)], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), new FakeRng(ints: [0, 0, 15, 15], doubles: [0.99, 0.99]));

        battle.ResolveTurn(Actions(new UseMove(0)));

        Assert.True(source.IsFainted);
        Assert.All([battle.Active(new BattleSlot(BattleSide.Enemy, 0)), battle.Active(new BattleSlot(BattleSide.Enemy, 1))],
            target => Assert.True(target.CurrentHp < target.MaxHp));
        Assert.Contains(battle.Log, item => item is ContactDamaged { Slot: { Side: BattleSide.Player, Position: 0 }, Amount: 1 });
    }

    [Fact]
    public void SpreadDamage_RecordsDeterministicAccuracyAndDamageTrace()
    {
        BattleController battle = Battle(new BattleMove(EntityId.Parse("move:trace_probe"), Normal,
            DamageClass.Special, 100, 100, 10, 0, 0, target: MoveTarget.AllOpponents),
            new FakeRng(ints: [0, 0, 15, 15], doubles: [0.99, 0.99]));

        battle.ResolveTurn(Actions(new UseMove(0)));

        Assert.Equal([EffectTraceKind.StatusGate, EffectTraceKind.FlinchGate, EffectTraceKind.ConfusionGate,
            EffectTraceKind.Accuracy, EffectTraceKind.Accuracy, EffectTraceKind.HitCount,
            EffectTraceKind.Immunity, EffectTraceKind.Critical, EffectTraceKind.DamageRoll, EffectTraceKind.Damage,
            EffectTraceKind.Immunity, EffectTraceKind.Critical, EffectTraceKind.DamageRoll, EffectTraceKind.Damage],
            battle.Trace.Select(entry => entry.Kind));
        Assert.Equal([0d, 0d], battle.Trace.Where(entry => entry.Kind == EffectTraceKind.Accuracy)
            .Select(entry => entry.DrawResult));
        Assert.Equal([100d, 100d], battle.Trace.Where(entry => entry.Kind == EffectTraceKind.Accuracy)
            .Select(entry => entry.DrawBound));
        Assert.Equal([0.99d, 0.99d], battle.Trace.Where(entry => entry.Kind == EffectTraceKind.Critical)
            .Select(entry => entry.DrawResult));
        Assert.All(battle.Trace.Where(entry => entry.Kind == EffectTraceKind.Critical), entry => Assert.Equal(1d, entry.DrawBound));
        Assert.Equal([15d, 15d], battle.Trace.Where(entry => entry.Kind == EffectTraceKind.DamageRoll)
            .Select(entry => entry.DrawResult));
        Assert.All(battle.Trace.Where(entry => entry.Kind == EffectTraceKind.DamageRoll), entry => Assert.Equal(16d, entry.DrawBound));
        Assert.All(battle.Trace.Where(entry => entry.Kind == EffectTraceKind.Damage), entry =>
            Assert.True(entry.EventEndIndex > entry.EventStartIndex));
    }

    [Fact]
    public void DoublesMoveGateStopsBeforePpTargetOrRng()
    {
        BattleMove move = new(EntityId.Parse("move:gated_probe"), Normal, DamageClass.Special, 100, 100, 10, 0, 0,
            target: MoveTarget.AllOpponents, secondaryEffects: [new MoveGateEffect(MoveGateKind.NotPreviousMove)]);
        var rng = new CountingRng();
        BattleController battle = Battle(move, rng);
        BattleCreature source = battle.Active(new BattleSlot(BattleSide.Player, 0));
        source.RecordMoveUse(move.Move);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(new UseMove(0)));

        Assert.Contains(events, item => item is MoveFailed
        {
            Slot: { Side: BattleSide.Player, Position: 0 }, Reason: MoveFailureReason.CannotRepeat,
        });
        Assert.Equal(10, move.Pp);
        Assert.DoesNotContain(events, item => item is MoveUsed or DamageDealt);
        Assert.Equal([EffectTraceKind.StatusGate, EffectTraceKind.FlinchGate, EffectTraceKind.ConfusionGate,
            EffectTraceKind.MoveGate], battle.Trace.Select(entry => entry.Kind));
        Assert.Equal(0, battle.Trace[^1].Value);
        Assert.True(battle.Trace[^1].EventEndIndex > battle.Trace[^1].EventStartIndex);
        Assert.Equal(0, rng.IntCalls);
        Assert.Equal(0, rng.DoubleCalls);
    }

    [Fact]
    public void SourceGateTraceRecordsStatusDrawsAndVolatileSkips()
    {
        BattleController paralysis = Battle(new BattleMove(EntityId.Parse("move:status_trace_probe"), Normal,
            DamageClass.Special, 100, 100, 10, 0, 0, target: MoveTarget.AllOpponents),
            new FakeRng(doubles: [0]));
        paralysis.Active(new BattleSlot(BattleSide.Player, 0)).SetStatus(PersistentStatus.Paralysis);

        paralysis.ResolveTurn(Actions(new UseMove(0)));

        EffectTraceEntry blockedByParalysis = Assert.Single(paralysis.Trace);
        Assert.Equal(EffectTraceKind.StatusGate, blockedByParalysis.Kind);
        Assert.True(blockedByParalysis.Performed);
        Assert.Equal(0d, blockedByParalysis.DrawResult);
        Assert.Equal(1d, blockedByParalysis.DrawBound);
        Assert.Equal(0, blockedByParalysis.Value);
        Assert.True(blockedByParalysis.EventEndIndex > blockedByParalysis.EventStartIndex);

        BattleController sleep = Battle(new BattleMove(EntityId.Parse("move:sleep_trace_probe"), Normal,
            DamageClass.Special, 100, 100, 10, 0, 0, target: MoveTarget.AllOpponents), new FakeRng());
        sleep.Active(new BattleSlot(BattleSide.Player, 0)).SetStatus(PersistentStatus.Sleep, counter: 1);

        sleep.ResolveTurn(Actions(new UseMove(0)));

        EffectTraceEntry blockedBySleep = Assert.Single(sleep.Trace);
        Assert.Equal(EffectTraceKind.StatusGate, blockedBySleep.Kind);
        Assert.False(blockedBySleep.Performed);
        Assert.Null(blockedBySleep.DrawResult);
        Assert.Null(blockedBySleep.DrawBound);
        Assert.Equal(0, blockedBySleep.Value);
    }

    [Fact]
    public void SourceGateTraceRecordsConfusionDraw()
    {
        BattleController battle = Battle(new BattleMove(EntityId.Parse("move:confusion_trace_probe"), Normal,
            DamageClass.Special, 100, 100, 10, 0, 0, target: MoveTarget.AllOpponents), new FakeRng(doubles: [0]));
        battle.Active(new BattleSlot(BattleSide.Player, 0)).SetConfusion(2);

        battle.ResolveTurn(Actions(new UseMove(0)));

        EffectTraceEntry confusion = Assert.Single(battle.Trace, entry => entry.Kind == EffectTraceKind.ConfusionGate);
        Assert.True(confusion.Performed);
        Assert.Equal(0d, confusion.DrawResult);
        Assert.Equal(1d, confusion.DrawBound);
        Assert.Equal(0, confusion.Value);
        Assert.True(confusion.EventEndIndex > confusion.EventStartIndex);
    }

    [Fact]
    public void SpreadSecondaryChanceTracesEachEligibleTarget()
    {
        BattleMove move = new(EntityId.Parse("move:secondary_trace_probe"), Normal, DamageClass.Special, 100, 100, 10, 0, 0,
            target: MoveTarget.AllOpponents,
            secondaryEffects: [new StatChangeEffect(StatKind.Atk, -1, OnSelf: false) { Chance = 50 }]);
        BattleController battle = Battle(move, new FakeRng(ints: [0, 0, 15, 15, 10, 90], doubles: [0.99, 0.99]));

        battle.ResolveTurn(Actions(new UseMove(0)));

        EffectTraceEntry[] chances = battle.Trace.Where(entry => entry.Kind == EffectTraceKind.EffectChance).ToArray();
        Assert.Equal([new BattleSlot(BattleSide.Enemy, 0), new BattleSlot(BattleSide.Enemy, 1)], chances.Select(entry => entry.TargetSlot));
        Assert.All(chances, entry =>
        {
            Assert.True(entry.Performed);
            Assert.Equal(100d, entry.DrawBound);
        });
        Assert.Equal([10d, 90d], chances.Select(entry => entry.DrawResult));
        Assert.Equal([1, 0], chances.Select(entry => entry.Value));
        Assert.Equal(-1, battle.Active(new BattleSlot(BattleSide.Enemy, 0)).Stage(StatKind.Atk));
        Assert.Equal(0, battle.Active(new BattleSlot(BattleSide.Enemy, 1)).Stage(StatKind.Atk));
    }

    [Fact]
    public void FaintedTargetSecondaryTraceSkipsItsChanceDraw()
    {
        BattleMove move = new(EntityId.Parse("move:fainted_secondary_trace_probe"), Normal, DamageClass.Special, 1_000, 100, 10, 0, 0,
            target: MoveTarget.Selected,
            secondaryEffects: [new AilmentEffect(PersistentStatus.Poison) { Chance = 50 }]);
        BattleController battle = Battle(move, new FakeRng(ints: [0, 15], doubles: [0.99]));

        battle.ResolveTurn(Actions(new UseMove(0), new ActiveSlotSelection(new BattleSlot(BattleSide.Enemy, 0))));

        EffectTraceEntry chance = Assert.Single(battle.Trace, entry => entry.Kind == EffectTraceKind.EffectChance);
        Assert.Equal(new BattleSlot(BattleSide.Enemy, 0), chance.TargetSlot);
        Assert.False(chance.Performed);
        Assert.Null(chance.DrawResult);
        Assert.Null(chance.DrawBound);
        Assert.Equal(0, chance.Value);
        Assert.True(battle.Active(new BattleSlot(BattleSide.Enemy, 0)).IsFainted);
    }

    [Fact]
    public void AlwaysHitSpread_TraceMarksAccuracyAsNoDraw()
    {
        BattleController battle = Battle(new BattleMove(EntityId.Parse("move:always_hit_probe"), Normal,
            DamageClass.Special, 100, null, 10, 0, 0, target: MoveTarget.AllOpponents),
            new FakeRng(ints: [15, 15], doubles: [0.99, 0.99]));

        battle.ResolveTurn(Actions(new UseMove(0)));

        EffectTraceEntry[] accuracy = battle.Trace.Where(entry => entry.Kind == EffectTraceKind.Accuracy).ToArray();
        Assert.Equal(2, accuracy.Length);
        Assert.All(accuracy, entry =>
        {
            Assert.False(entry.Performed);
            Assert.Null(entry.DrawResult);
        });
    }

    [Fact]
    public void StatusProtect_UsesDoublesEffectPathAndTracesItsDraw()
    {
        BattleMove protect = new(EntityId.Parse("move:doubles_protect_probe"), Normal, DamageClass.Status,
            null, null, 10, 4, 0, isProtect: true, target: MoveTarget.User);
        BattleController battle = Battle(protect, new FakeRng(doubles: [0d]));

        battle.ResolveTurn(Actions(new UseMove(0)));

        Assert.True(battle.Active(new BattleSlot(BattleSide.Player, 0)).Protected);
        EffectTraceEntry trace = Assert.Single(battle.Trace, entry => entry.Kind == EffectTraceKind.Protect);
        Assert.Equal(new BattleSlot(BattleSide.Player, 0), trace.SourceSlot);
        Assert.Null(trace.TargetSlot);
        Assert.True(trace.Performed);
        Assert.Equal(0d, trace.DrawResult);
        Assert.Equal(1d, trace.DrawBound);
        Assert.Equal(1, trace.Value);
        Assert.True(trace.EventEndIndex > trace.EventStartIndex);
    }

    [Fact]
    public void StatusForceSwitch_UsesDoublesTargetSlotAndTracesReserveDraw()
    {
        BattleMove forceSwitch = new(EntityId.Parse("move:doubles_force_switch_probe"), Normal, DamageClass.Status,
            null, null, 10, 0, 0, forcesSwitch: true, target: MoveTarget.Selected);
        BattleCreature enemy0 = Creature("e0", Normal);
        BattleCreature enemy1 = Creature("e1", Normal);
        BattleCreature enemy2 = Creature("e2", Normal);
        BattleCreature enemy3 = Creature("e3", Normal);
        BattleController battle = new([Creature("p0", Normal, forceSwitch), Creature("p1", Normal)],
            [enemy0, enemy1, enemy2, enemy3], BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new FakeRng(ints: [1]));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(
            Actions(new UseMove(0), new ActiveSlotSelection(new BattleSlot(BattleSide.Enemy, 1))));

        Assert.Same(enemy0, battle.Active(new BattleSlot(BattleSide.Enemy, 0)));
        Assert.Same(enemy3, battle.Active(new BattleSlot(BattleSide.Enemy, 1)));
        Assert.Contains(events, entry => entry is ForcedOut { Side: BattleSide.Enemy });
        EffectTraceEntry trace = Assert.Single(battle.Trace, entry => entry.Kind == EffectTraceKind.ForceSwitchReserve);
        Assert.Equal(new BattleSlot(BattleSide.Player, 0), trace.SourceSlot);
        Assert.Equal(new BattleSlot(BattleSide.Enemy, 1), trace.TargetSlot);
        Assert.True(trace.Performed);
        Assert.Equal(1d, trace.DrawResult);
        Assert.Equal(2d, trace.DrawBound);
        Assert.Equal(3, trace.Value);
        Assert.True(trace.EventEndIndex > trace.EventStartIndex);
    }

    [Fact]
    public void StatusSideScope_ResolvesItsActionEffectOnceWithoutTargetAccuracy()
    {
        BattleMove spikes = new(EntityId.Parse("move:doubles_spikes_probe"), Normal, DamageClass.Status,
            null, null, 10, 0, 0, setsSpikes: true, target: MoveTarget.OpponentsField);
        BattleController battle = Battle(spikes, new FakeRng());

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(new UseMove(0)));

        Assert.Contains(events, entry => entry is HazardSet { Side: BattleSide.Enemy, Layers: 1 });
        Assert.DoesNotContain(battle.Trace, entry => entry.Kind == EffectTraceKind.Accuracy);
    }

    [Fact]
    public void StatusFieldScope_ResolvesItsActionEffectOnceWithoutTargetAccuracy()
    {
        BattleMove weather = new(EntityId.Parse("move:doubles_weather_probe"), Normal, DamageClass.Status,
            null, null, 10, 0, 0, setsWeather: Weather.Rain, target: MoveTarget.EntireField);
        BattleController battle = Battle(weather, new FakeRng());

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(new UseMove(0)));

        Assert.Single(events, entry => entry is WeatherChanged { Weather: Weather.Rain });
        Assert.DoesNotContain(battle.Trace, entry => entry.Kind == EffectTraceKind.Accuracy);
    }

    [Fact]
    public void ActionScopedQueueGate_UsesTheDoublesSourceSlot()
    {
        BattleMove gate = new(EntityId.Parse("move:doubles_queue_gate_probe"), Normal, DamageClass.Status,
            null, null, 10, 0, 0, target: MoveTarget.User, secondaryEffects: [new QueueActionGateEffect(1)]);
        BattleController battle = new([Creature("p0", Normal), Creature("p1", Normal, gate)],
            [Creature("e0", Normal), Creature("e1", Normal)], BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new FakeRng());

        battle.ResolveTurn(Actions(new Pass(), new UseMove(0)));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(new Pass(), new UseMove(0)));

        Assert.Contains(events, entry => entry is ActionSkipped { Slot: { Side: BattleSide.Player, Position: 1 } });
        Assert.DoesNotContain(events, entry => entry is MoveUsed { Slot: { Side: BattleSide.Player, Position: 1 } });
    }

    [Fact]
    public void PositionSwap_AtomicallyExchangesAlliedSlotsAndPreservesCreatureState()
    {
        BattleMove swap = new(EntityId.Parse("move:position_swap_probe"), Normal, DamageClass.Status,
            null, null, 10, 0, 0, target: MoveTarget.Ally, secondaryEffects: [new PositionSwapEffect()]);
        BattleCreature player0 = Creature("p0", Normal, swap);
        BattleCreature player1 = Creature("p1", Normal);
        player0.SetStage(StatKind.Atk, 2);
        BattleController battle = new([player0, player1], [Creature("e0", Normal), Creature("e1", Normal)],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new FakeRng());

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(
            Actions(new UseMove(0), new ActiveSlotSelection(new BattleSlot(BattleSide.Player, 1))));

        Assert.Same(player1, battle.Active(new BattleSlot(BattleSide.Player, 0)));
        Assert.Same(player0, battle.Active(new BattleSlot(BattleSide.Player, 1)));
        Assert.Equal(2, battle.Active(new BattleSlot(BattleSide.Player, 1)).Stage(StatKind.Atk));
        Assert.Contains(events, entry => entry is PositionsSwapped
        {
            SourceSlot: { Side: BattleSide.Player, Position: 0 },
            TargetSlot: { Side: BattleSide.Player, Position: 1 },
        });
        EffectTraceEntry trace = Assert.Single(battle.Trace, entry => entry.Kind == EffectTraceKind.PositionSwap);
        Assert.False(trace.Performed);
        Assert.Null(trace.DrawResult);
        Assert.True(trace.EventEndIndex > trace.EventStartIndex);
    }

    [Fact]
    public void Redirect_ReplacesSelectedOpponentBeforeDamage()
    {
        BattleMove hit = new(EntityId.Parse("move:redirect_hit_probe"), Normal, DamageClass.Special,
            100, null, 10, 0, 0, target: MoveTarget.Selected);
        BattleMove redirect = new(EntityId.Parse("move:redirect_probe"), Normal, DamageClass.Status,
            null, null, 10, 1, 0, target: MoveTarget.User,
            secondaryEffects: [new RedirectEffect(1, new HashSet<DamageClass> { DamageClass.Special }, new HashSet<DamageClass>(),
                new HashSet<string> { "damaging" }, new HashSet<string>())]);
        BattleCreature enemy0 = Creature("e0", Normal, redirect);
        BattleCreature enemy1 = Creature("e1", Normal);
        BattleController battle = new([Creature("p0", Normal, hit), Creature("p1", Normal)], [enemy0, enemy1],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new FakeRng(ints: [15], doubles: [0.99]));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0), new ActiveSlotSelection(new(BattleSide.Enemy, 1))),
            new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 0), new UseMove(0)),
            new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
        ]));

        Assert.True(enemy0.CurrentHp < enemy0.MaxHp);
        Assert.Equal(enemy1.MaxHp, enemy1.CurrentHp);
        Assert.Contains(events, entry => entry is TargetRedirected
        {
            OriginalTargetSlot: { Side: BattleSide.Enemy, Position: 1 },
            RedirectedTargetSlot: { Side: BattleSide.Enemy, Position: 0 },
        });
    }

    [Fact]
    public void Redirect_HigherPriorityWinsWithoutTieDraw()
    {
        BattleMove hit = new(EntityId.Parse("move:redirect_priority_hit"), Normal, DamageClass.Special, 100, null, 10, 0, 0, target: MoveTarget.Selected);
        BattleMove low = new(EntityId.Parse("move:redirect_low"), Normal, DamageClass.Status, null, null, 10, 1, 0, target: MoveTarget.User,
            secondaryEffects: [new RedirectEffect(1, new HashSet<DamageClass> { DamageClass.Special }, new HashSet<DamageClass>(), new HashSet<string>(), new HashSet<string>())]);
        BattleMove high = new(EntityId.Parse("move:redirect_high"), Normal, DamageClass.Status, null, null, 10, 2, 0, target: MoveTarget.User,
            secondaryEffects: [new RedirectEffect(2, new HashSet<DamageClass> { DamageClass.Special }, new HashSet<DamageClass>(), new HashSet<string>(), new HashSet<string>())]);
        BattleCreature e0 = Creature("e0", Normal, low);
        BattleCreature e1 = Creature("e1", Normal, high);
        BattleController battle = new([Creature("p0", Normal, hit), Creature("p1", Normal)], [e0, e1], BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new FakeRng(ints: [15], doubles: [0.99]));

        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0), new ActiveSlotSelection(new BattleSlot(BattleSide.Enemy, 0))), new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()), new BattleActionSubmission(new(BattleSide.Enemy, 0), new UseMove(0)), new BattleActionSubmission(new(BattleSide.Enemy, 1), new UseMove(0))]));

        Assert.Equal(e0.MaxHp, e0.CurrentHp);
        Assert.True(e1.CurrentHp < e1.MaxHp);
    }

    [Fact]
    public void Redirect_BypassedClassLeavesSelectedTarget()
    {
        BattleMove hit = new(EntityId.Parse("move:redirect_bypass_hit"), Normal, DamageClass.Special, 100, null, 10, 0, 0, target: MoveTarget.Selected);
        BattleMove redirect = new(EntityId.Parse("move:redirect_bypass"), Normal, DamageClass.Status, null, null, 10, 1, 0, target: MoveTarget.User,
            secondaryEffects: [new RedirectEffect(1, new HashSet<DamageClass> { DamageClass.Special }, new HashSet<DamageClass> { DamageClass.Special }, new HashSet<string>(), new HashSet<string>())]);
        BattleCreature e0 = Creature("e0", Normal, redirect);
        BattleCreature e1 = Creature("e1", Normal);
        BattleController battle = new([Creature("p0", Normal, hit), Creature("p1", Normal)], [e0, e1], BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new FakeRng(ints: [15], doubles: [0.99]));

        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0), new ActiveSlotSelection(new BattleSlot(BattleSide.Enemy, 1))), new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()), new BattleActionSubmission(new(BattleSide.Enemy, 0), new UseMove(0)), new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass())]));

        Assert.Equal(e0.MaxHp, e0.CurrentHp);
        Assert.True(e1.CurrentHp < e1.MaxHp);
        Assert.Contains(battle.Trace, entry => entry is { Kind: EffectTraceKind.Redirection, Performed: false, Value: 0 });
    }

    [Fact]
    public void Redirect_UsesTagFiltersAndBypassTags()
    {
        BattleMove contactHit = new(EntityId.Parse("move:redirect_tag_hit"), Normal, DamageClass.Physical, 100, null, 10, 0, 0,
            makesContact: true, target: MoveTarget.Selected);
        BattleMove acceptsContact = new(EntityId.Parse("move:redirect_accepts_contact"), Normal, DamageClass.Status, null, null, 10, 1, 0,
            target: MoveTarget.User, secondaryEffects: [new RedirectEffect(1,
                new HashSet<DamageClass> { DamageClass.Physical }, new HashSet<DamageClass>(),
                new HashSet<string> { "contact" }, new HashSet<string>())]);
        BattleMove bypassesContact = new(EntityId.Parse("move:redirect_bypasses_contact"), Normal, DamageClass.Status, null, null, 10, 1, 0,
            target: MoveTarget.User, secondaryEffects: [new RedirectEffect(1,
                new HashSet<DamageClass> { DamageClass.Physical }, new HashSet<DamageClass>(),
                new HashSet<string> { "contact" }, new HashSet<string> { "contact" })]);

        BattleCreature redirected = Creature("redirected", Normal, acceptsContact);
        BattleCreature selected = Creature("selected", Normal);
        BattleController acceptedBattle = new([Creature("p0", Normal, contactHit), Creature("p1", Normal)], [redirected, selected],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new FakeRng(ints: [15], doubles: [0.99]));
        acceptedBattle.ResolveTurn(RedirectActions(new UseMove(0), new BattleSlot(BattleSide.Enemy, 1)));

        Assert.True(redirected.CurrentHp < redirected.MaxHp);
        Assert.Equal(selected.MaxHp, selected.CurrentHp);

        BattleCreature bypassRedirector = Creature("bypass_redirector", Normal, bypassesContact);
        BattleCreature bypassSelected = Creature("bypass_selected", Normal);
        BattleController bypassedBattle = new([Creature("p2", Normal, contactHit), Creature("p3", Normal)], [bypassRedirector, bypassSelected],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new FakeRng(ints: [15], doubles: [0.99]));
        bypassedBattle.ResolveTurn(RedirectActions(new UseMove(0), new BattleSlot(BattleSide.Enemy, 1)));

        Assert.Equal(bypassRedirector.MaxHp, bypassRedirector.CurrentHp);
        Assert.True(bypassSelected.CurrentHp < bypassSelected.MaxHp);
        Assert.Contains(bypassedBattle.Trace, entry => entry is { Kind: EffectTraceKind.Redirection, Performed: false, Value: 0 });
    }

    [Fact]
    public void Redirect_UsesSpeedThenTopologyWithoutRedirectTieDraw()
    {
        BattleMove hit = new(EntityId.Parse("move:redirect_speed_hit"), Normal, DamageClass.Special, 100, null, 10, 0, 0, target: MoveTarget.Selected);
        BattleMove redirect0 = new(EntityId.Parse("move:redirect_speed_0"), Normal, DamageClass.Status, null, null, 10, 2, 0,
            target: MoveTarget.User, secondaryEffects: [new RedirectEffect(1, new HashSet<DamageClass> { DamageClass.Special }, new HashSet<DamageClass>(), new HashSet<string>(), new HashSet<string>())]);
        BattleMove redirect1 = new(EntityId.Parse("move:redirect_speed_1"), Normal, DamageClass.Status, null, null, 10, 1, 0,
            target: MoveTarget.User, secondaryEffects: [new RedirectEffect(1, new HashSet<DamageClass> { DamageClass.Special }, new HashSet<DamageClass>(), new HashSet<string>(), new HashSet<string>())]);
        BattleCreature slow = Creature("slow_redirector", Normal, redirect0, speed: 20);
        BattleCreature fast = Creature("fast_redirector", Normal, redirect1, speed: 80);
        BattleController speedBattle = new([Creature("p0", Normal, hit), Creature("p1", Normal)], [slow, fast],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new FakeRng(ints: [15], doubles: [0.99]));
        speedBattle.ResolveTurn(RedirectActions(new UseMove(0), new BattleSlot(BattleSide.Enemy, 0), enemySecondAction: new UseMove(0)));

        Assert.Equal(slow.MaxHp, slow.CurrentHp);
        Assert.True(fast.CurrentHp < fast.MaxHp);
        Assert.DoesNotContain(speedBattle.Trace, entry => entry.Kind == EffectTraceKind.TurnOrderTie);

        BattleCreature first = Creature("first_redirector", Normal, redirect0, speed: 50);
        BattleCreature second = Creature("second_redirector", Normal, redirect1, speed: 50);
        BattleController topologyBattle = new([Creature("p2", Normal, hit), Creature("p3", Normal)], [first, second],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new FakeRng(ints: [15], doubles: [0.99]));
        topologyBattle.ResolveTurn(RedirectActions(new UseMove(0), new BattleSlot(BattleSide.Enemy, 1), enemySecondAction: new UseMove(0)));

        Assert.True(first.CurrentHp < first.MaxHp);
        Assert.Equal(second.MaxHp, second.CurrentHp);
        Assert.DoesNotContain(topologyBattle.Trace, entry => entry.Kind == EffectTraceKind.TurnOrderTie);
    }

    [Fact]
    public void Redirect_FixesRandomOpponentWithoutTargetDraw()
    {
        BattleMove randomStatus = new(EntityId.Parse("move:redirect_random_status"), Normal, DamageClass.Status,
            null, null, 10, 0, 0, target: MoveTarget.RandomOpponent,
            secondaryEffects: [new AilmentEffect(PersistentStatus.Burn)]);
        BattleMove redirect = new(EntityId.Parse("move:redirect_random"), Normal, DamageClass.Status, null, null, 10, 1, 0,
            target: MoveTarget.User, secondaryEffects: [new RedirectEffect(1,
                new HashSet<DamageClass> { DamageClass.Status }, new HashSet<DamageClass>(),
                new HashSet<string> { "status" }, new HashSet<string>())]);
        BattleCreature redirector = Creature("random_redirector", Normal, redirect);
        BattleCreature other = Creature("random_other", Normal);
        BattleController battle = new([Creature("p0", Normal, randomStatus), Creature("p1", Normal)], [redirector, other],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new FakeRng());

        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0)),
            new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 0), new UseMove(0)),
            new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
        ]));

        Assert.Equal(PersistentStatus.Burn, redirector.Status);
        Assert.Null(other.Status);
        Assert.Contains(battle.Trace, entry => entry is { Kind: EffectTraceKind.TargetSelection, Performed: false, Value: 1 });
    }

    [Fact]
    public void Redirect_DoesNotApplyToSpreadAllyOrFaintedRedirector()
    {
        BattleMove spread = new(EntityId.Parse("move:redirect_spread"), Normal, DamageClass.Special, 100, null, 10, 0, 0, target: MoveTarget.AllOpponents);
        BattleMove ally = new(EntityId.Parse("move:redirect_ally"), Normal, DamageClass.Status, null, null, 10, 0, 0, target: MoveTarget.Ally,
            secondaryEffects: [new HealEffect(new Fraction(1, 2), HpFractionRecipient.Target)]);
        BattleMove redirect = new(EntityId.Parse("move:redirect_scope"), Normal, DamageClass.Status, null, null, 10, 2, 0, target: MoveTarget.User,
            secondaryEffects: [new RedirectEffect(1, new HashSet<DamageClass> { DamageClass.Special }, new HashSet<DamageClass>(), new HashSet<string>(), new HashSet<string>())]);
        BattleCreature enemy0 = Creature("scope_redirector", Normal, redirect);
        BattleCreature enemy1 = Creature("scope_target", Normal);
        BattleController spreadBattle = new([Creature("p0", Normal, spread), Creature("p1", Normal, ally)], [enemy0, enemy1],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new FakeRng(ints: [0, 0, 0, 15, 15], doubles: [0.99, 0.99]));
        spreadBattle.Active(new BattleSlot(BattleSide.Player, 1)).TakeDamage(100);
        spreadBattle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0)),
            new BattleActionSubmission(new(BattleSide.Player, 1), new UseMove(0), new ActiveSlotSelection(new(BattleSide.Player, 0))),
            new BattleActionSubmission(new(BattleSide.Enemy, 0), new UseMove(0)),
            new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
        ]));

        Assert.True(enemy0.CurrentHp < enemy0.MaxHp);
        Assert.True(enemy1.CurrentHp < enemy1.MaxHp);
        Assert.Equal(spreadBattle.Active(new BattleSlot(BattleSide.Player, 0)).MaxHp,
            spreadBattle.Active(new BattleSlot(BattleSide.Player, 0)).CurrentHp);
        Assert.DoesNotContain(spreadBattle.Log, entry => entry is TargetRedirected);

        BattleMove side = new(EntityId.Parse("move:redirect_side"), Normal, DamageClass.Status, null, null, 10, 0, 0,
            target: MoveTarget.OpponentsField, secondaryEffects: [new EntryHazardEffect()]);
        BattleMove field = new(EntityId.Parse("move:redirect_field"), Normal, DamageClass.Status, null, null, 10, -1, 0,
            target: MoveTarget.EntireField, secondaryEffects: [new SetWeatherEffect(Weather.Rain)]);
        BattleController scopedBattle = new([Creature("p2", Normal, side), Creature("p3", Normal, field)],
            [Creature("scope_redirector_2", Normal, redirect), Creature("scope_target_2", Normal)],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new FakeRng());
        scopedBattle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0)),
            new BattleActionSubmission(new(BattleSide.Player, 1), new UseMove(0)),
            new BattleActionSubmission(new(BattleSide.Enemy, 0), new UseMove(0)),
            new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
        ]));

        Assert.Contains(scopedBattle.Log, entry => entry is HazardSet { Side: BattleSide.Enemy });
        Assert.Contains(scopedBattle.Log, entry => entry is WeatherChanged { Weather: Weather.Rain });
        Assert.DoesNotContain(scopedBattle.Log, entry => entry is TargetRedirected);

        BattleMove killer = new(EntityId.Parse("move:redirect_fainted_killer"), Normal, DamageClass.Special, 100, null, 10, 1, 0, target: MoveTarget.Selected);
        BattleMove hit = new(EntityId.Parse("move:redirect_fainted_hit"), Normal, DamageClass.Special, 100, null, 10, 0, 0, target: MoveTarget.Selected);
        BattleCreature faintedRedirector = Creature("fainted_redirector", Normal, redirect);
        BattleCreature selected = Creature("fainted_selected", Normal);
        BattleController faintedBattle = new([Creature("p2", Normal, killer), Creature("p3", Normal, hit)], [faintedRedirector, selected],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new FakeRng(ints: [15, 15], doubles: [0.99, 0.99]));
        faintedRedirector.TakeDamage(150);
        faintedBattle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), new UseMove(0), new ActiveSlotSelection(new(BattleSide.Enemy, 0))),
            new BattleActionSubmission(new(BattleSide.Player, 1), new UseMove(0), new ActiveSlotSelection(new(BattleSide.Enemy, 1))),
            new BattleActionSubmission(new(BattleSide.Enemy, 0), new UseMove(0)),
            new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
        ]));

        Assert.True(selected.CurrentHp < selected.MaxHp);
        Assert.DoesNotContain(faintedBattle.Log, entry => entry is TargetRedirected);
    }

    private static BattleController Battle(BattleMove move, IRng rng, BattleCreature? enemy0 = null, BattleCreature? enemy1 = null) =>
        new([Creature("p0", Normal, move), Creature("p1", Normal)], [enemy0 ?? Creature("e0", Normal), enemy1 ?? Creature("e1", Normal)],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), rng);

    private static BattleTurnActions RedirectActions(BattleAction playerAction, BattleSlot selectedTarget,
        BattleAction? enemySecondAction = null) => new(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new(BattleSide.Player, 0), playerAction, new ActiveSlotSelection(selectedTarget)),
            new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 0), new UseMove(0)),
            new BattleActionSubmission(new(BattleSide.Enemy, 1), enemySecondAction ?? new Pass()),
        ]);

    private static BattleTurnActions Actions(BattleAction action, BattleActionSelection? selection = null) => new(BattleTopology.Doubles,
    [
        new BattleActionSubmission(new(BattleSide.Player, 0), action, selection),
        new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
        new BattleActionSubmission(new(BattleSide.Enemy, 0), new Pass()),
        new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
    ]);

    private static BattleTurnActions Actions(BattleAction player0, BattleAction player1) => new(BattleTopology.Doubles,
    [
        new BattleActionSubmission(new(BattleSide.Player, 0), player0),
        new BattleActionSubmission(new(BattleSide.Player, 1), player1),
        new BattleActionSubmission(new(BattleSide.Enemy, 0), new Pass()),
        new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
    ]);

    private static BattleCreature Creature(string slug, EntityId type, BattleMove? move = null,
        IReadOnlyList<AbilityHook>? abilityHooks = null, int speed = 100) => new(
        EntityId.Parse($"species:{slug}"), slug, 50, [type], new Stats(200, 100, 100, 100, 100, speed),
        [move ?? new BattleMove(EntityId.Parse("move:wait"), Normal, DamageClass.Status, null, null, 10, 0, 0)],
        abilityHooks: abilityHooks);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal, NoDamageTo = [Ghost] }, new TypeDef { Id = Ghost }]);

    private static IReadOnlyDictionary<string, JsonElement> Params(params (string Key, object Value)[] values) =>
        values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value));

    private sealed class CountingRng(params int[] values) : IRng
    {
        private readonly Queue<int> _values = new(values);
        public int IntCalls { get; private set; }
        public int DoubleCalls { get; private set; }

        public int Next(int maxExclusive)
        {
            IntCalls++;
            return _values.Dequeue();
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            IntCalls++;
            return _values.Dequeue();
        }

        public double NextDouble()
        {
            DoubleCalls++;
            return 0.99;
        }
    }
}
