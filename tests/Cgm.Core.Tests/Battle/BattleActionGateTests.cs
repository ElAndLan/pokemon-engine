using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleActionGateTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static Effect Op(string op, int? chance = null, params (string Key, object Value)[] values) =>
        new()
        {
            Op = op,
            Chance = chance,
            Params = values.Length == 0 ? null : values.ToDictionary(
                value => value.Key,
                value => JsonSerializer.SerializeToElement(value.Value)),
        };

    private static BattleMove Compile(params Effect[] effects) => MoveCompiler.ToBattleMove(new Move
    {
        Id = EntityId.Parse("move:gated"),
        Name = "Gated",
        Type = Normal,
        DamageClass = DamageClass.Physical,
        Power = 40,
        Accuracy = 100,
        Pp = 10,
        Effects = effects,
    });

    private static BattleMove Compile(DamageClass damageClass, MoveTarget target,
        params Effect[] effects) => MoveCompiler.ToBattleMove(new Move
    {
        Id = EntityId.Parse("move:gated"),
        Name = "Gated",
        Type = Normal,
        DamageClass = damageClass,
        Power = damageClass == DamageClass.Status ? null : 40,
        Accuracy = 100,
        Pp = 10,
        Target = target,
        Effects = effects,
    });

    private static BattleCreature Creature(string id, int speed, params BattleMove[] moves) =>
        new(EntityId.Parse($"species:{id}"), id, 50, [Normal], new Stats(200, 100, 100, 100, 100, speed), moves);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 20, 0, 0);

    private static BattleController Battle(BattleCreature player, BattleCreature enemy, BattleCreature? reserve = null) =>
        new(reserve is null ? [player] : [player, reserve], [enemy], new TypeChart([new TypeDef { Id = Normal }]), new Rng(1));

    [Fact]
    public void Compiler_CompilesQueuedAndPreMoveGateOps()
    {
        BattleMove move = Compile(
            Op("damage"),
            Op("moveGate", null, ("kind", "firstAction")),
            Op("moveGate", null, ("kind", "notPreviousMove")),
            Op("queueActionGate", null, ("turns", 2)));

        Assert.Contains(move.SecondaryEffects, effect => effect is MoveGateEffect { Kind: MoveGateKind.FirstAction });
        Assert.Contains(move.SecondaryEffects, effect => effect is MoveGateEffect { Kind: MoveGateKind.NotPreviousMove });
        Assert.Contains(move.SecondaryEffects, effect => effect is QueueActionGateEffect { Turns: 2 });
    }

    [Fact]
    public void Compiler_CompilesCompleteActionGateAndRechargeVocabulary()
    {
        BattleMove move = Compile(
            Op("damage"),
            Op("moveGate", null, ("kind", "previousActionFailed"), ("timing", "selection")),
            Op("moveGate", null, ("kind", "sourceBeforeTarget")),
            Op("moveGate", null, ("kind", "targetAction"), ("targetClass", "damagingMove")),
            Op("moveGate", null, ("kind", "damageReceived"), ("timing", "afterMoveUsed"),
                ("damageMode", "forbid"), ("damageClass", "physical")),
            Op("recharge", null, ("turns", 2)));

        Assert.Contains(move.SecondaryEffects, effect => effect is MoveGateEffect
            { Kind: MoveGateKind.PreviousActionFailed, Timing: MoveGateTiming.Selection });
        Assert.Contains(move.SecondaryEffects, effect => effect is MoveGateEffect
            { Kind: MoveGateKind.TargetAction, TargetClass: MoveGateTargetClass.DamagingMove });
        Assert.Contains(move.SecondaryEffects, effect => effect is MoveGateEffect
            { Kind: MoveGateKind.DamageReceived, Timing: MoveGateTiming.AfterMoveUsed,
              DamageMode: MoveGateDamageMode.Forbid, DamageClass: DamageClass.Physical });
        Assert.Contains(move.SecondaryEffects, effect => effect is QueueActionGateEffect
            { Turns: 2, Owner: QueueActionGateOwner.Creature });
    }

    [Fact]
    public void Compiler_RejectsInvalidActionGateParameters()
    {
        Assert.Throws<ArgumentException>(() => Compile(Op("moveGate")));
        Assert.Throws<ArgumentException>(() => Compile(Op("moveGate", 50, ("kind", "firstAction"))));
        Assert.Throws<ArgumentException>(() => Compile(Op("moveGate", null, ("kind", "never"))));
        Assert.Throws<ArgumentException>(() => Compile(Op("queueActionGate", 50)));
        Assert.Throws<ArgumentException>(() => Compile(Op("queueActionGate", null, ("turns", 0))));
        Assert.Throws<ArgumentException>(() => Compile(Op("queueActionGate", null, ("turns", 1), ("extra", 1))));
        Assert.Throws<ArgumentException>(() => Compile(Op("moveGate", null, ("kind", "targetAction"))));
        Assert.Throws<ArgumentException>(() => Compile(Op("moveGate", null, ("kind", "firstAction"),
            ("targetClass", "anyMove"))));
        Assert.ThrowsAny<ArgumentException>(() => Compile(Op("moveGate", null, ("kind", "damageReceived"),
            ("damageMode", "require"), ("damageClass", "status"))));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleActionGates.Validate(new MoveGateEffect(
            MoveGateKind.DamageReceived, DamageMode: MoveGateDamageMode.Require,
            DamageClass: (DamageClass)99)));
        Assert.Throws<ArgumentException>(() => Compile(Op("moveGate", null, ("kind", "targetAction"),
            ("timing", "selection"), ("targetClass", "anyMove"))));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Physical, MoveTarget.AllOpponents,
            Op("damage"), Op("moveGate", null, ("kind", "sourceAfterTarget"))));
        Assert.Throws<ArgumentException>(() => Compile(Op("moveGate", null, ("kind", "firstAction")),
            Op("moveGate", null, ("kind", "firstAction"))));
        Assert.Throws<ArgumentException>(() => Compile(DamageClass.Status, MoveTarget.User,
            Op("recharge")));
    }

    [Fact]
    public void SelectionGate_RejectsTheWholeTurnAtomically()
    {
        BattleMove ordinary = Move("ordinary", DamageClass.Physical, 40);
        BattleMove gated = Move("selection_gate", DamageClass.Physical, 40,
            new MoveGateEffect(MoveGateKind.FirstAction, MoveGateTiming.Selection));
        BattleCreature player = Creature("player", 100, ordinary, gated);
        BattleCreature enemy = Creature("enemy", 1, Inert());
        var rng = new CountingRng();
        var battle = new BattleController(player, enemy,
            new TypeChart([new TypeDef { Id = Normal }]), rng);
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        int turn = battle.Turn, events = battle.Log.Count, calls = rng.Calls;

        Assert.False(battle.CanSubmitAction(BattleSide.Player, new UseMove(1)));
        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(new UseMove(1), new UseMove(0)));

        Assert.Equal(turn, battle.Turn);
        Assert.Equal(events, battle.Log.Count);
        Assert.Equal(calls, rng.Calls);
        Assert.Equal(10, gated.Pp);
    }

    [Fact]
    public void BeforeAndAfterMoveUsedGatesHaveDistinctPpAndEventBoundaries()
    {
        BattleMove before = Move("before_gate", DamageClass.Physical, 40,
            new MoveGateEffect(MoveGateKind.PreviousActionFailed));
        BattleCreature beforeSource = Creature("before_source", 100, before);
        var beforeBattle = Battle(beforeSource, Creature("before_target", 1, Inert()));

        IReadOnlyList<BattleEvent> beforeEvents = beforeBattle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(10, before.Pp);
        Assert.DoesNotContain(beforeEvents, item => item is MoveUsed { Side: BattleSide.Player });
        Assert.Contains(beforeEvents, item => item is MoveFailed
            { Side: BattleSide.Player, Reason: MoveFailureReason.PreviousActionRequired });

        BattleMove after = Move("after_gate", DamageClass.Physical, 40,
            new MoveGateEffect(MoveGateKind.PreviousActionFailed, MoveGateTiming.AfterMoveUsed));
        BattleCreature afterSource = Creature("after_source", 100, after);
        var afterBattle = Battle(afterSource, Creature("after_target", 1, Inert()));

        IReadOnlyList<BattleEvent> afterEvents = afterBattle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(9, after.Pp);
        List<BattleEvent> ordered = afterEvents.ToList();
        Assert.True(ordered.FindIndex(item => item is MoveUsed { Side: BattleSide.Player })
            < ordered.FindIndex(item => item is MoveFailed
                { Side: BattleSide.Player, Reason: MoveFailureReason.PreviousActionRequired }));
        Assert.Equal(after.Move, afterSource.LastMoveUsed);
    }

    [Fact]
    public void PreviousFailureGate_UsesTheImmediatelyPreviousSourceResult()
    {
        BattleMove gated = Move("previous_failure", DamageClass.Physical, 40,
            new MoveGateEffect(MoveGateKind.PreviousActionFailed));
        BattleCreature source = Creature("source", 100, gated);
        BattleCreature target = Creature("target", 1, Inert());
        BattleController battle = Battle(source, target);

        IReadOnlyList<BattleEvent> first = battle.ResolveTurn(new UseMove(0), new UseMove(0));
        IReadOnlyList<BattleEvent> second = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Contains(first, item => item is MoveFailed
            { Side: BattleSide.Player, Reason: MoveFailureReason.PreviousActionRequired });
        Assert.Contains(second, item => item is DamageDealt { Slot.Side: BattleSide.Enemy, Amount: > 0 });
        Assert.Equal(9, gated.Pp);
    }

    [Fact]
    public void TargetOrderAndActionClassGatesUseTheSnapshottedTurnPlan()
    {
        BattleMove gated = Move("target_plan", DamageClass.Physical, 40,
            new MoveGateEffect(MoveGateKind.SourceAfterTarget),
            new MoveGateEffect(MoveGateKind.TargetAction, TargetClass: MoveGateTargetClass.DamagingMove));
        BattleMove damage = Move("target_damage", DamageClass.Special, 20);
        BattleMove status = Move("target_status", DamageClass.Status, null);

        BattleCreature source = Creature("source", 1, gated);
        BattleCreature target = Creature("target", 100, damage, status);
        BattleController battle = Battle(source, target);
        IReadOnlyList<BattleEvent> passed = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Contains(passed, item => item is DamageDealt { Slot.Side: BattleSide.Enemy, Amount: > 0 });

        BattleCreature secondSource = Creature("second_source", 1,
            Move("target_plan_two", DamageClass.Physical, 40,
                new MoveGateEffect(MoveGateKind.SourceAfterTarget),
                new MoveGateEffect(MoveGateKind.TargetAction,
                    TargetClass: MoveGateTargetClass.DamagingMove)));
        BattleController failedBattle = Battle(secondSource,
            Creature("second_target", 100, damage, status));
        IReadOnlyList<BattleEvent> failed = failedBattle.ResolveTurn(new UseMove(0), new UseMove(1));

        Assert.Contains(failed, item => item is MoveFailed
            { Side: BattleSide.Player, Reason: MoveFailureReason.TargetActionRequirementNotMet });
    }

    [Theory]
    [InlineData(MoveGateTargetClass.AnyMove, DamageClass.Status, true)]
    [InlineData(MoveGateTargetClass.DamagingMove, DamageClass.Physical, true)]
    [InlineData(MoveGateTargetClass.DamagingMove, DamageClass.Status, false)]
    [InlineData(MoveGateTargetClass.StatusMove, DamageClass.Status, true)]
    [InlineData(MoveGateTargetClass.StatusMove, DamageClass.Special, false)]
    public void TargetActionGate_CoversTheClosedActionClassTable(
        MoveGateTargetClass required, DamageClass planned, bool passes)
    {
        BattleMove move = Move("target_class", DamageClass.Physical, 40,
            new MoveGateEffect(MoveGateKind.TargetAction, TargetClass: required));
        MoveFailureReason? failure = BattleActionGates.Failure(move,
            Creature("source", 100, move), move.SecondaryEffects.OfType<MoveGateEffect>().Single(),
            new BattleMoveGateInputs(TargetPlannedMoveClass: planned));

        Assert.Equal(passes, failure is null);
    }

    [Fact]
    public void SourceBeforeTargetGate_PassesOnlyWhileTargetActionIsPending()
    {
        BattleMove gated = Move("before_target", DamageClass.Physical, 40,
            new MoveGateEffect(MoveGateKind.SourceBeforeTarget));
        BattleCreature source = Creature("source", 100, gated);
        BattleController battle = Battle(source,
            Creature("target", 1, Move("target_move", DamageClass.Physical, 20)));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Contains(events, item => item is DamageDealt { Slot.Side: BattleSide.Enemy, Amount: > 0 });
        Assert.DoesNotContain(events, item => item is MoveFailed { Side: BattleSide.Player });
    }

    [Theory]
    [InlineData(MoveGateDamageMode.Forbid, DamageClass.Physical, MoveFailureReason.InterruptedByDamage)]
    [InlineData(MoveGateDamageMode.Require, DamageClass.Special, MoveFailureReason.DamageRequired)]
    public void DamageReceivedGate_UsesPriorCurrentTurnDamageAndClass(
        MoveGateDamageMode mode, DamageClass incomingClass, MoveFailureReason? expectedFailure)
    {
        BattleMove gated = Move("damage_gate", DamageClass.Physical, 40,
            new MoveGateEffect(MoveGateKind.DamageReceived, MoveGateTiming.AfterMoveUsed,
                DamageMode: mode, DamageClass: DamageClass.Physical));
        BattleMove incoming = Move("incoming", incomingClass, 20);
        BattleCreature source = Creature("source", 1, gated);
        BattleController battle = Battle(source, Creature("target", 100, incoming));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(9, gated.Pp);
        Assert.Contains(events, item => item is MoveUsed { Side: BattleSide.Player });
        Assert.Contains(events, item => item is MoveFailed { Side: BattleSide.Player, Reason: var reason }
            && reason == expectedFailure);
    }

    [Fact]
    public void RequiredDamageGate_PassesAfterMatchingDamage()
    {
        BattleMove gated = Move("required_damage", DamageClass.Physical, 40,
            new MoveGateEffect(MoveGateKind.DamageReceived, DamageMode: MoveGateDamageMode.Require,
                DamageClass: DamageClass.Physical));
        BattleCreature source = Creature("source", 1, gated);
        BattleController battle = Battle(source,
            Creature("target", 100, Move("incoming", DamageClass.Physical, 20)));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Contains(events, item => item is DamageDealt { Slot.Side: BattleSide.Enemy, Amount: > 0 });
        Assert.DoesNotContain(events, item => item is MoveFailed { Side: BattleSide.Player });
    }

    [Theory]
    [InlineData(MoveGateDamageMode.Require, true, true)]
    [InlineData(MoveGateDamageMode.Require, false, false)]
    [InlineData(MoveGateDamageMode.Forbid, true, false)]
    [InlineData(MoveGateDamageMode.Forbid, false, true)]
    public void DamageReceivedGate_CoversRequiredAndForbiddenMatchingRows(
        MoveGateDamageMode mode, bool matchingDamage, bool passes)
    {
        BattleMove move = Move("damage_table", DamageClass.Physical, 40,
            new MoveGateEffect(MoveGateKind.DamageReceived, DamageMode: mode));
        MoveFailureReason? failure = BattleActionGates.Failure(move,
            Creature("source", 100, move), move.SecondaryEffects.OfType<MoveGateEffect>().Single(),
            new BattleMoveGateInputs(MatchingDamageReceived: matchingDamage));

        Assert.Equal(passes, failure is null);
    }

    [Fact]
    public void QueueActionGate_SkipsEveryActionOnItsDueTurnWithoutSpendingPp()
    {
        BattleMove gated = Compile(Op("damage"), Op("queueActionGate"));
        BattleCreature player = Creature("player", 100, gated);
        BattleCreature reserve = Creature("reserve", 50, Inert());
        BattleCreature enemy = Creature("enemy", 1, Inert());
        BattleController battle = Battle(player, enemy, reserve);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        IReadOnlyList<BattleEvent> skipped = battle.ResolveTurn(new Switch(1), new UseMove(0));
        IReadOnlyList<BattleEvent> resumed = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Contains(skipped, eventItem => eventItem is ActionSkipped { Slot: { Side: BattleSide.Player, Position: 0 } });
        Assert.DoesNotContain(skipped, eventItem => eventItem is SwitchedIn { Side: BattleSide.Player });
        Assert.Equal(8, player.Moves[0].Pp);
        Assert.Contains(resumed, eventItem => eventItem is MoveUsed { Side: BattleSide.Player });
    }

    [Fact]
    public void QueueActionGate_RemainsPendingWhenWholeTurnAdmissionFails()
    {
        BattleMove gated = Compile(Op("damage"), Op("queueActionGate"));
        BattleCreature player = Creature("player", 100, gated);
        BattleCreature enemy = Creature("enemy", 1, Inert());
        var rng = new CountingRng();
        var battle = new BattleController(player, enemy, new TypeChart([new TypeDef { Id = Normal }]), rng);
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        int rngCalls = rng.Calls;

        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(new UseMove(0), new UseMove(1)));

        Assert.Single(battle.IntentQueueSnapshot);
        Assert.Equal(9, player.Moves[0].Pp);
        Assert.Equal(rngCalls, rng.Calls);
        IReadOnlyList<BattleEvent> skipped = battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Single(skipped.OfType<ActionSkipped>());
        Assert.Empty(battle.IntentQueueSnapshot);
        Assert.Equal(9, player.Moves[0].Pp);
        Assert.Equal(rngCalls, rng.Calls);
    }

    [Fact]
    public void MultipleDueGatesConsumeInSequenceButEmitOneSkipPerSlot()
    {
        BattleMove gated = Compile(Op("damage"), Op("queueActionGate"), Op("queueActionGate"));
        BattleCreature player = Creature("player", 100, gated);
        BattleController battle = Battle(player, Creature("enemy", 1, Inert()));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Single(events.OfType<ActionSkipped>());
        EffectTraceEntry[] consumed = battle.Trace.Where(entry => entry.Kind == EffectTraceKind.IntentConsumed).ToArray();
        Assert.Equal(2, consumed.Length);
        Assert.True(consumed[0].IntentSequence < consumed[1].IntentSequence);
        Assert.All(consumed, entry =>
        {
            Assert.Equal(BattleIntentCheckpoint.PreAction, entry.IntentCheckpoint);
            Assert.Equal(BattleIntentPayloadKind.SkipAction, entry.IntentPayload);
            Assert.Equal(EntityId.Parse("move:gated"), entry.IntentSourceMove);
            Assert.Null(entry.DrawResult);
        });
    }

    [Fact]
    public void QueueActionGate_ReplaysWithIdenticalEventsTraceAndState()
    {
        static (string[] Events, EffectTraceEntry[] Trace, BattleIntentDebugEntry[] Queue, int Pp) Replay()
        {
            BattleMove gated = Compile(Op("damage"), Op("queueActionGate"));
            BattleCreature player = Creature("player", 100, gated);
            BattleController battle = Battle(player, Creature("enemy", 1, Inert()));
            battle.ResolveTurn(new UseMove(0), new UseMove(0));
            battle.ResolveTurn(new UseMove(0), new UseMove(0));
            return (battle.Log.Select(entry => $"{entry.GetType().Name}:{entry}").ToArray(),
                battle.Trace.ToArray(), battle.IntentQueueSnapshot.ToArray(), player.Moves[0].Pp);
        }

        var first = Replay();
        var second = Replay();

        Assert.Equal(first.Events, second.Events);
        Assert.Equal(first.Trace, second.Trace);
        Assert.Equal(first.Queue, second.Queue);
        Assert.Equal(first.Pp, second.Pp);
    }

    [Fact]
    public void Recharge_BlocksEverySubmittedActionKindWithoutPpOrRng()
    {
        BattleAction[] blockedActions =
        [
            new UseMove(0),
            new Switch(1),
            new UseBattleItem(EntityId.Parse("item:unused"), 0, 10),
            new ActivateForm("unused", 0),
            new Pass(),
        ];

        foreach (BattleAction blocked in blockedActions)
        {
            BattleMove recharge = Move("recharge", DamageClass.Physical, 80,
                new QueueActionGateEffect(1, QueueActionGateOwner.Creature));
            BattleCreature source = Creature("source", 100, recharge);
            BattleCreature reserve = Creature("reserve", 50, Inert());
            var rng = new CountingRng();
            var battle = new BattleController([source, reserve], [Creature("target", 1, Inert())],
                new TypeChart([new TypeDef { Id = Normal }]), rng);
            battle.ResolveTurn(new UseMove(0), new Pass());
            int calls = rng.Calls;

            IReadOnlyList<BattleEvent> events = battle.ResolveTurn(blocked, new Pass());

            Assert.Single(events.OfType<ActionSkipped>());
            Assert.Equal(9, recharge.Pp);
            Assert.Equal(calls, rng.Calls);
            Assert.Empty(battle.IntentQueueSnapshot);
            Assert.DoesNotContain(events, item => item is SwitchedIn or BattleItemUsed or MoveUsed);
        }
    }

    [Fact]
    public void Recharge_QueuesOnlyAfterPositiveDirectDamage()
    {
        BattleMove recharge = new(EntityId.Parse("move:missed_recharge"), Normal,
            DamageClass.Physical, 80, 1, 10, 0, 0,
            secondaryEffects: [new QueueActionGateEffect(1, QueueActionGateOwner.Creature)]);
        BattleCreature source = Creature("source", 100, recharge);
        var battle = new BattleController(source, Creature("target", 1, Inert()),
            new TypeChart([new TypeDef { Id = Normal }]), new FakeRng(ints: [99]));

        battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Empty(battle.IntentQueueSnapshot);
    }

    [Fact]
    public void Recharge_DoesNotQueueAfterTypeImmunity()
    {
        EntityId ghost = EntityId.Parse("type:ghost");
        BattleMove recharge = Move("immune_recharge", DamageClass.Physical, 80,
            new QueueActionGateEffect(1, QueueActionGateOwner.Creature));
        BattleCreature source = Creature("source", 100, recharge);
        BattleCreature target = new(EntityId.Parse("species:immune_target"), "immune_target", 50,
            [ghost], new Stats(200, 100, 100, 100, 100, 1), [Inert()]);
        var battle = new BattleController(source, target,
            new TypeChart([new TypeDef { Id = Normal, NoDamageTo = [ghost] }, new TypeDef { Id = ghost }]),
            new Rng(1));

        battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Empty(battle.IntentQueueSnapshot);
    }

    [Fact]
    public void Recharge_DoesNotQueueAfterProtection()
    {
        BattleMove recharge = Move("protected_recharge", DamageClass.Physical, 80,
            new QueueActionGateEffect(1, QueueActionGateOwner.Creature));
        BattleMove protect = new(EntityId.Parse("move:protect"), Normal, DamageClass.Status,
            null, null, 10, 4, 0, target: MoveTarget.User,
            secondaryEffects: [new ProtectEffect(ProtectionConditions.LegacyPersonal)]);
        var battle = Battle(Creature("source", 100, recharge), Creature("target", 1, protect));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Empty(battle.IntentQueueSnapshot);
    }

    [Fact]
    public void Recharge_CancelsWhenItsCreatureSwitchesBeforeTheDueTurn()
    {
        BattleMove recharge = Move("delayed_recharge", DamageClass.Physical, 80,
            new QueueActionGateEffect(2, QueueActionGateOwner.Creature));
        BattleMove reserveMove = Move("reserve_move", DamageClass.Physical, 40);
        BattleCreature source = Creature("source", 100, recharge);
        BattleCreature reserve = Creature("reserve", 50, reserveMove);
        var battle = new BattleController([source, reserve], [Creature("target", 1, Inert())],
            new TypeChart([new TypeDef { Id = Normal }]), new Rng(1));
        battle.ResolveTurn(new UseMove(0), new Pass());

        battle.ResolveTurn(new Switch(1), new Pass());
        IReadOnlyList<BattleEvent> third = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Contains(battle.Trace, item => item.Kind == EffectTraceKind.IntentCancelled);
        Assert.DoesNotContain(third, item => item is ActionSkipped);
        Assert.Contains(third, item => item is MoveUsed { Move: var move } && move == reserveMove.Move);
    }

    [Fact]
    public void Recharge_CancelsWhenItsCreatureFaintsBeforeTheDueTurn()
    {
        BattleMove recharge = Move("faint_recharge", DamageClass.Physical, 80,
            new QueueActionGateEffect(2, QueueActionGateOwner.Creature));
        BattleMove reserveMove = Move("reserve_move", DamageClass.Physical, 40);
        BattleMove knockout = Move("knockout", DamageClass.Physical, 1000);
        BattleCreature source = Creature("source", 100, recharge);
        BattleCreature reserve = Creature("reserve", 50, reserveMove);
        BattleCreature enemy = Creature("enemy", 200, Inert(), knockout);
        var battle = new BattleController([source, reserve], [enemy],
            new TypeChart([new TypeDef { Id = Normal }]), new Rng(1));
        battle.ResolveTurn(new UseMove(0), new Pass());

        battle.ResolveTurn(new Pass(), new UseMove(1));
        battle.ResolveReplacements([new BattleReplacementSelection(
            new BattleSlot(BattleSide.Player, 0), 1)]);
        IReadOnlyList<BattleEvent> third = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Contains(battle.Trace, item => item.Kind == EffectTraceKind.IntentCancelled);
        Assert.DoesNotContain(third, item => item is ActionSkipped);
        Assert.Contains(third, item => item is MoveUsed { Move: var move } && move == reserveMove.Move);
    }

    [Fact]
    public void Recharge_IsCreatureAndSlotIsolatedInDoubles()
    {
        BattleMove recharge = Move("doubles_recharge", DamageClass.Physical, 80,
            new QueueActionGateEffect(1, QueueActionGateOwner.Creature));
        BattleMove allyMove = Move("ally_move", DamageClass.Physical, 40);
        BattleCreature player0 = Creature("player_zero", 100, recharge);
        BattleCreature player1 = Creature("player_one", 90, allyMove);
        var battle = new BattleController([player0, player1],
            [Creature("enemy_zero", 20, Inert()), Creature("enemy_one", 10, Inert())],
            BattleTopology.Doubles, [0, 1], [0, 1], new TypeChart([new TypeDef { Id = Normal }]),
            new Rng(1));
        BattleSlot p0 = new(BattleSide.Player, 0), p1 = new(BattleSide.Player, 1);
        BattleSlot e0 = new(BattleSide.Enemy, 0);
        battle.ResolveTurn(Doubles(
            new(p0, new UseMove(0), new ActiveSlotSelection(e0)),
            new(p1, new Pass())));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Doubles(
            new(p0, new UseMove(0), new ActiveSlotSelection(e0)),
            new(p1, new UseMove(0), new ActiveSlotSelection(e0))));

        Assert.Contains(events, item => item is ActionSkipped { Slot: var slot } && slot == p0);
        Assert.DoesNotContain(events, item => item is ActionSkipped { Slot: var slot } && slot == p1);
        Assert.Contains(events, item => item is MoveUsed { Slot: var slot, Move: var move }
            && slot == p1 && move == allyMove.Move);
    }

    [Fact]
    public void SmartAi_ExcludesKnownFailingSourceHistoryGateWithoutReadingTargetAction()
    {
        BattleMove gated = Move("ai_gated", DamageClass.Physical, 100,
            new MoveGateEffect(MoveGateKind.NotPreviousMove, MoveGateTiming.Selection));
        BattleMove legal = Move("ai_legal", DamageClass.Physical, 40);
        BattleMove targetPlan = Move("ai_target_plan", DamageClass.Physical, 120,
            new MoveGateEffect(MoveGateKind.TargetAction,
                TargetClass: MoveGateTargetClass.DamagingMove));
        BattleCreature source = Creature("source", 100, gated, legal, targetPlan);
        source.RecordMoveUse(gated.Move);

        SmartAiDecision decision = SmartAi.ChooseAction(new SmartAiContext([source], 0,
            [Creature("target", 1, Inert())], 0, new TypeChart([new TypeDef { Id = Normal }]),
            new Rng(1), Weights: new SmartAiWeights { NoiseFraction = 0 }));

        Assert.Equal(new UseMove(2), decision.Action);
        Assert.DoesNotContain(decision.Scores, score => score.Action == new UseMove(0));
        Assert.Contains(decision.Scores, score => score.Action == new UseMove(2));
    }

    [Fact]
    public void ActionGateFamily_MatchesDeterministicGolden()
    {
        static string Run()
        {
            BattleMove recharge = Move("golden_recharge", DamageClass.Physical, 80,
                new QueueActionGateEffect(1, QueueActionGateOwner.Creature));
            BattleCreature source = Creature("golden_source", 100, recharge);
            var battle = new BattleController(source, Creature("golden_target", 1, Inert()),
                new TypeChart([new TypeDef { Id = Normal }]), new Rng(7));
            battle.ResolveTurn(new UseMove(0), new Pass());
            BattleIntentDebugEntry intent = Assert.Single(battle.IntentQueueSnapshot);
            battle.ResolveTurn(new Switch(99), new Pass());

            return string.Join('\n',
            [
                $"intent:{intent.OwnerScope}:{intent.Checkpoint}:{intent.Payload}:due={intent.DueTurn}",
                .. battle.Log.Select(item => item switch
                {
                    MoveUsed used => $"event:move:{used.Slot}:{used.Move}",
                    DamageDealt damage => $"event:damage:{damage.Slot}:{damage.Amount}",
                    ActionSkipped skipped => $"event:skip:{skipped.Slot}",
                    _ => $"event:{item.GetType().Name}",
                }),
                .. battle.Trace.Where(item => item.Kind is EffectTraceKind.IntentEnqueued
                        or EffectTraceKind.IntentConsumed)
                    .Select(item => $"trace:{item.Kind}:{item.IntentSequence}:{item.IntentPayload}"),
                $"pp:{recharge.Pp}",
            ]);
        }

        string first = Run();
        Assert.Equal(first, Run());
        Assert.Equal(Golden("action-gate"), first);
    }

    [Theory]
    [InlineData("firstAction", MoveFailureReason.FirstActionOnly)]
    [InlineData("notPreviousMove", MoveFailureReason.CannotRepeat)]
    public void MoveGate_FailsBeforePpOrDamage(string kind, MoveFailureReason reason)
    {
        BattleMove gated = Compile(Op("damage"), Op("moveGate", null, ("kind", kind)));
        BattleCreature player = Creature("player", 100, gated);
        BattleCreature enemy = Creature("enemy", 1, Inert());
        BattleController battle = Battle(player, enemy);

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        int enemyHp = enemy.CurrentHp;
        IReadOnlyList<BattleEvent> failed = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Contains(failed, eventItem => eventItem is MoveFailed { Side: BattleSide.Player, Reason: var actual } && actual == reason);
        Assert.Equal(enemyHp, enemy.CurrentHp);
        Assert.Equal(9, player.Moves[0].Pp);
    }

    private sealed class CountingRng : IRng
    {
        public int Calls { get; private set; }

        public int Next(int maxExclusive)
        {
            Calls++;
            return 0;
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            Calls++;
            return minInclusive;
        }

        public double NextDouble()
        {
            Calls++;
            return 0.99;
        }
    }

    private static BattleMove Move(string slug, DamageClass damageClass, int? power,
        params MoveEffect[] effects) => new(EntityId.Parse($"move:{slug}"), Normal, damageClass,
        power, null, 10, 0, 0, secondaryEffects: effects);

    private static BattleTurnActions Doubles(params BattleActionSubmission[] player) => new(
        BattleTopology.Doubles,
        [
            .. player,
            new(new BattleSlot(BattleSide.Enemy, 0), new Pass()),
            new(new BattleSlot(BattleSide.Enemy, 1), new Pass()),
        ]);

    private static string Golden(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden"))
        .Replace("\r\n", "\n").TrimEnd();
}
