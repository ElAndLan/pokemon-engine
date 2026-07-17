using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleActionHistoryFormulaTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly BattleSlot Player0 = new(BattleSide.Player, 0);
    private static readonly BattleSlot Player1 = new(BattleSide.Player, 1);
    private static readonly BattleSlot Enemy0 = new(BattleSide.Enemy, 0);
    private static readonly EntityId Chain = EntityId.Parse("move:chain");
    private static readonly EntityId Other = EntityId.Parse("move:other");

    [Theory]
    [InlineData(0, 40)]
    [InlineData(1, 80)]
    [InlineData(2, 160)]
    [InlineData(99, 160)]
    public void ExponentialFormula_CoversFirstRepeatAndCap(int prior, int expected) =>
        Assert.Equal(expected, ActionHistoryFormulas.ConsecutivePower(40, prior,
            ConsecutivePowerMode.Exponential, 2, 160));

    [Theory]
    [InlineData(0, 40)]
    [InlineData(1, 80)]
    [InlineData(4, 200)]
    [InlineData(99, 200)]
    public void LinearFormula_CoversFirstRepeatAndCap(int prior, int expected) =>
        Assert.Equal(expected, ActionHistoryFormulas.ConsecutivePower(40, prior,
            ConsecutivePowerMode.Linear, 40, 200));

    [Fact]
    public void Formula_RejectsInvalidInputsAndHandlesSaturatedCounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionHistoryFormulas.ConsecutivePower(0, 0,
            ConsecutivePowerMode.Linear, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionHistoryFormulas.ConsecutivePower(1, -1,
            ConsecutivePowerMode.Linear, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionHistoryFormulas.ConsecutivePower(2, 0,
            ConsecutivePowerMode.Linear, 1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ActionHistoryFormulas.ConsecutivePower(1, 0,
            (ConsecutivePowerMode)99, 1, 1));
        Assert.Equal(1, ActionHistoryFormulas.ConsecutivePower(1, int.MaxValue,
            ConsecutivePowerMode.Exponential, 1, int.MaxValue));
        Assert.Equal(int.MaxValue, ActionHistoryFormulas.ConsecutivePower(1, int.MaxValue,
            ConsecutivePowerMode.Linear, int.MaxValue, int.MaxValue));
    }

    [Fact]
    public void CreatureHistory_ResetsOnFailureDifferentActionSwitchAndFaint()
    {
        var history = new BattleActionHistory();
        BattleHistoryOwner source = Owner(BattleSide.Player, 0, Player0);
        BattleHistoryOwner target = Owner(BattleSide.Enemy, 0, Enemy0);

        history.BeginTurn(0, Plans(source, target));
        Complete(history, 1, source, Chain, BattleActionResult.Connected, target);
        Assert.Equal(1, history.PowerInputs(source, target, Chain).PriorCreatureConnections);
        Complete(history, 2, source, Chain, BattleActionResult.Missed, target);
        Assert.Equal(0, history.PowerInputs(source, target, Chain).PriorCreatureConnections);

        history.BeginTurn(1, Plans(source, target));
        Complete(history, 1, source, Chain, BattleActionResult.Connected, target);
        Complete(history, 2, source, Other, BattleActionResult.Connected, target);
        Assert.Equal(0, history.PowerInputs(source, target, Chain).PriorCreatureConnections);

        history.RecordSwitch(source, Owner(BattleSide.Player, 1, Player0));
        Assert.Equal(0, history.PowerInputs(source, target, Other).PriorCreatureConnections);
        BattleActionAttemptId faintedAttempt = history.BeginMove(3, source, Chain);
        history.MarkStarted(faintedAttempt);
        history.RecordFaint(source);
        history.Complete(faintedAttempt, BattleActionResult.Connected, [target]);
        Assert.Equal(0, history.PowerInputs(source, target, Chain).PriorCreatureConnections);
        Assert.False(history.PowerInputs(source, target, Chain).PreviousActionFailed);
    }

    [Fact]
    public void SideHistory_CountsOneAttemptedTurnAcrossAlliesAndAgesAfterGap()
    {
        var history = new BattleActionHistory();
        BattleHistoryOwner first = Owner(BattleSide.Player, 0, Player0);
        BattleHistoryOwner ally = Owner(BattleSide.Player, 1, Player1);
        BattleHistoryOwner target = Owner(BattleSide.Enemy, 0, Enemy0);
        history.BeginTurn(0, [.. Plans(first, target), new(ally, BattlePlannedActionKind.Move)]);

        BattleActionAttemptId a = history.BeginMove(1, first, Chain);
        history.MarkStarted(a);
        Assert.Equal(0, history.PowerInputs(ally, target, Chain).PriorSideAttemptedTurns);
        Complete(history, 2, ally, Chain, BattleActionResult.Connected, target);
        history.Complete(a, BattleActionResult.Connected, [target]);

        history.BeginTurn(1, Plans(first, target));
        Assert.Equal(1, history.PowerInputs(first, target, Chain).PriorSideAttemptedTurns);
        Complete(history, 1, first, Chain, BattleActionResult.Connected, target);
        history.BeginTurn(3, Plans(first, target));
        Assert.Equal(0, history.PowerInputs(first, target, Chain).PriorSideAttemptedTurns);
    }

    [Fact]
    public void OrderInputs_DistinguishPendingCompletedPassSwitchAndTiePreview()
    {
        var history = new BattleActionHistory();
        BattleHistoryOwner source = Owner(BattleSide.Player, 0, Player0);
        BattleHistoryOwner target = Owner(BattleSide.Enemy, 0, Enemy0);
        history.BeginTurn(0, Plans(source, target));
        Assert.True(history.PowerInputs(source, target, Chain).SourceBeforeTarget);
        Complete(history, 1, target, Other, BattleActionResult.Succeeded, source);
        Assert.True(history.PowerInputs(source, target, Chain).SourceAfterTarget);
        BattleHistoryOwner incoming = Owner(BattleSide.Enemy, 1, Enemy0);
        history.RecordSwitch(target, incoming);
        BattleActionFormulaInputs switched = history.PowerInputs(source, incoming, Chain);
        Assert.True(switched.SourceBeforeTarget);
        Assert.False(switched.SourceAfterTarget);
        Assert.Equal((false, false), (history.PreviewPowerInputs(source, target, Chain, false, false).SourceBeforeTarget,
            history.PreviewPowerInputs(source, target, Chain, false, false).SourceAfterTarget));

        history.EndBattle();
        history.BeginTurn(0, [new(source, BattlePlannedActionKind.Move), new(target, BattlePlannedActionKind.Other)]);
        Assert.False(history.PowerInputs(source, target, Chain).SourceBeforeTarget);
        Assert.False(history.PowerInputs(source, target, Chain).SourceAfterTarget);
    }

    [Fact]
    public void FailureAndAllyFaintInputs_AreOwnerIsolatedAndRetainedForExactlyOneTurn()
    {
        var history = new BattleActionHistory();
        BattleHistoryOwner source = Owner(BattleSide.Player, 0, Player0);
        BattleHistoryOwner ally = Owner(BattleSide.Player, 1, Player1);
        BattleHistoryOwner target = Owner(BattleSide.Enemy, 0, Enemy0);
        history.BeginTurn(0, [.. Plans(source, target), new(ally, BattlePlannedActionKind.Move)]);
        Complete(history, 1, source, Chain, BattleActionResult.Failed, target);
        history.RecordFaint(ally);
        history.BeginTurn(1, Plans(source, target));
        BattleActionFormulaInputs inputs = history.PowerInputs(source, target, Chain);
        Assert.True(inputs.PreviousActionFailed);
        Assert.True(inputs.AllyFaintedPreviousTurn);
        Assert.False(history.PowerInputs(target, source, Chain).PreviousActionFailed);

        Complete(history, 1, source, Chain, BattleActionResult.Succeeded, target);
        history.BeginTurn(2, Plans(source, target));
        inputs = history.PowerInputs(source, target, Chain);
        Assert.False(inputs.PreviousActionFailed);
        Assert.False(inputs.AllyFaintedPreviousTurn);
    }

    [Fact]
    public void Snapshot_IsTypedBoundedAndRequiresCompletedUniqueAttempts()
    {
        var history = new BattleActionHistory();
        BattleHistoryOwner source = Owner(BattleSide.Player, 0, Player0);
        BattleHistoryOwner target = Owner(BattleSide.Enemy, 0, Enemy0);
        history.BeginTurn(0, Plans(source, target));
        BattleActionAttemptId pending = history.BeginMove(1, source, Chain);
        Assert.Throws<InvalidOperationException>(() => history.BeginTurn(1, Plans(source, target)));
        Assert.Throws<ArgumentException>(() => history.Complete(pending, BattleActionResult.Connected));
        history.MarkStarted(pending);
        Assert.Throws<InvalidOperationException>(() => history.MarkStarted(pending));
        history.Complete(pending, BattleActionResult.Connected, [target]);
        Assert.Throws<ArgumentException>(() => history.BeginMove(1, source, Chain));
        history.BeginTurn(1, Plans(source, target));
        Complete(history, 1, source, Other, BattleActionResult.Missed, target);
        history.BeginTurn(2, Plans(source, target));
        Complete(history, 1, source, Chain, BattleActionResult.Connected, target);

        BattleActionAttempt[] snapshot = [.. history.Snapshot()];
        Assert.Equal([1, 2], snapshot.Select(attempt => attempt.Id.Turn));
        Assert.Equal([BattleActionResult.Missed, BattleActionResult.Connected], snapshot.Select(attempt => attempt.Result));
        Assert.All(snapshot, attempt => Assert.True(attempt.Started));
        Assert.Throws<ArgumentException>(() => history.BeginTurn(3,
            [new(source, BattlePlannedActionKind.Move), new(source, BattlePlannedActionKind.Move)]));
    }

    [Fact]
    public void Compiler_ProducesBothTypedOpsAndRejectsMalformedVariants()
    {
        BattleMove move = Compile(40,
            Op("consecutivePower", ("scope", "creatureConnected"), ("mode", "exponential"), ("step", 2), ("cap", 160)),
            Op("historyPower", ("condition", "sourceAfterTarget"), ("multiplierNum", 2), ("multiplierDen", 1)));
        Assert.Contains(new ConsecutivePowerEffect(ConsecutivePowerScope.CreatureConnected,
            ConsecutivePowerMode.Exponential, 2, 160), move.SecondaryEffects);
        Assert.Contains(new HistoryPowerEffect(HistoryPowerCondition.SourceAfterTarget, new Fraction(2, 1)),
            move.SecondaryEffects);

        Assert.Throws<ArgumentException>(() => Compile(null,
            Op("consecutivePower", ("scope", "creatureConnected"), ("mode", "linear"), ("step", 1), ("cap", 40))));
        Assert.Throws<ArgumentException>(() => Compile(40,
            Op("consecutivePower", ("scope", "creatureConnected"), ("mode", "linear"), ("step", 0), ("cap", 40))));
        Assert.Throws<ArgumentException>(() => Compile(40,
            Op("historyPower", ("condition", "unknown"), ("multiplierNum", 2), ("multiplierDen", 1))));
        Assert.Throws<ArgumentException>(() => Compile(40,
            Op("consecutivePower", ("scope", "creatureConnected"), ("mode", "linear"), ("step", 1), ("cap", 40))
                with { Chance = 50 }));
        Assert.Throws<ArgumentException>(() => Compile(null,
            Op("historyPower", ("condition", "sourceBeforeTarget"), ("multiplierNum", 2), ("multiplierDen", 1))));
        Assert.Throws<ArgumentException>(() => Compile(40,
            Op("historyPower", ("condition", "sourceBeforeTarget"), ("multiplierNum", -1), ("multiplierDen", 1))));
        Assert.Throws<ArgumentException>(() => Compile(40,
            Op("historyPower", ("condition", "sourceBeforeTarget"), ("multiplierNum", 2), ("multiplierDen", 1)),
            Op("historyPower", ("condition", "sourceAfterTarget"), ("multiplierNum", 2), ("multiplierDen", 1))));
        Assert.Throws<ArgumentOutOfRangeException>(() => Compile(40,
            Op("historyPower", ("condition", "sourceBeforeTarget"), ("multiplierNum", 1), ("multiplierDen", 0))));
        Effect chance = Op("historyPower", ("condition", "sourceBeforeTarget"),
            ("multiplierNum", 2), ("multiplierDen", 1)) with { Chance = 50 };
        Assert.Throws<ArgumentException>(() => Compile(40, chance));
    }

    [Fact]
    public void Resolver_AppliesCreatureChainAndResetsAfterMiss()
    {
        BattleMove chain = Compile(40,
            Op("consecutivePower", ("scope", "creatureConnected"), ("mode", "exponential"), ("step", 2), ("cap", 160)));
        BattleCreature player = Creature("player", 2000, 100, chain);
        BattleCreature enemy = Creature("enemy", 2000, 50, Inert());
        var battle = new BattleController(player, enemy, Chart(), new TestRng());

        battle.ResolveTurn(new UseMove(0), new Pass());
        battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Equal([40, 80], battle.QueryTrace.Where(entry => entry.Result.Query == BattleQueryId.BasePower)
            .Select(entry => entry.Result.FinalValue.ToInt32()));
        Assert.Equal([BattleActionResult.Connected, BattleActionResult.Connected],
            battle.ActionHistory.Snapshot().Where(attempt => attempt.Source.Side == BattleSide.Player)
                .Select(attempt => attempt.Result));

        BattleMove miss = CompileWithAccuracy(40, 1,
            Op("consecutivePower", ("scope", "creatureConnected"), ("mode", "exponential"), ("step", 2), ("cap", 160)));
        BattleCreature missUser = Creature("missuser", 2000, 100, miss, chain);
        battle = new BattleController(missUser, Creature("target", 2000, 50, Inert()), Chart(), new TestRng());
        battle.ResolveTurn(new UseMove(0), new Pass());
        battle.ResolveTurn(new UseMove(1), new Pass());
        Assert.Equal(40, Assert.Single(battle.QueryTrace,
            entry => entry.Result.Query == BattleQueryId.BasePower).Result.FinalValue.ToInt32());
    }

    [Fact]
    public void Resolver_AppliesActionOrderAndPreviousFailureWithoutExtraFormulaRng()
    {
        BattleMove after = Compile(50,
            Op("historyPower", ("condition", "sourceAfterTarget"), ("multiplierNum", 2), ("multiplierDen", 1)));
        var rng = new TestRng();
        var battle = new BattleController(Creature("slow", 2000, 50, after),
            Creature("fast", 2000, 100, Inert()), Chart(), rng);
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(100, Assert.Single(battle.QueryTrace,
            entry => entry.Result.Query == BattleQueryId.BasePower).Result.FinalValue.ToInt32());
        Assert.Equal(1, rng.DoubleDraws);

        BattleMove previousFailed = Compile(50,
            Op("historyPower", ("condition", "previousActionFailed"), ("multiplierNum", 2), ("multiplierDen", 1)));
        battle = new BattleController(Creature("user", 2000, 100, CompileWithAccuracy(50, 1,
                Op("historyPower", ("condition", "sourceAfterTarget"), ("multiplierNum", 2), ("multiplierDen", 1))),
                previousFailed),
            Creature("other", 2000, 50, Inert()), Chart(), new TestRng());
        battle.ResolveTurn(new UseMove(0), new Pass());
        battle.ResolveTurn(new UseMove(1), new Pass());
        Assert.Equal(100, Assert.Single(battle.QueryTrace,
            entry => entry.Result.Query == BattleQueryId.BasePower).Result.FinalValue.ToInt32());
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 50)]
    public void Resolver_TiedSpeedUsesActualSeededOrder(int tieDraw, int expectedPower)
    {
        BattleMove before = Compile(50,
            Op("historyPower", ("condition", "sourceBeforeTarget"), ("multiplierNum", 2), ("multiplierDen", 1)));
        var battle = new BattleController(Creature("tiesource", 2000, 100, before),
            Creature("tietarget", 2000, 100, Inert()), Chart(), new TieRng(tieDraw));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(expectedPower, Assert.Single(battle.QueryTrace,
            entry => entry.Result.Query == BattleQueryId.BasePower).Result.FinalValue.ToInt32());
    }

    [Fact]
    public void PowerQuery_CoversBeforeTargetAndRejectsUnknownHistoryCondition()
    {
        BattleMove before = Compile(50,
            Op("historyPower", ("condition", "sourceBeforeTarget"), ("multiplierNum", 2), ("multiplierDen", 1)));
        BattleCreature source = Creature("sourcequery", 1000, 100, before);
        BattleCreature target = Creature("targetquery", 1000, 50, Inert());
        BattleActionFormulaInputs inputs = new(0, 0, true, false, false, false);
        HpStatusPowerQuery query = HpStatusFormulas.PowerQuery(before, source, target, actionInputs: inputs);
        Assert.Equal(100, BattleQuery.ResolveInteger(BattleQueryId.BasePower, query.AuthoredBase, query.Modifiers));

        var invalid = new BattleMove(Chain, Normal, DamageClass.Special, 50, null, 20, 0, 0,
            secondaryEffects: [new HistoryPowerEffect((HistoryPowerCondition)999, new Fraction(2, 1))]);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            HpStatusFormulas.PowerQuery(invalid, source, target, actionInputs: inputs));
    }

    [Fact]
    public void Doubles_HistoryClosesFlinchedAndAllMissedAttempts()
    {
        BattleMove flinch = new(EntityId.Parse("move:flinch"), Normal, DamageClass.Special,
            40, null, 20, 0, 0, flinchChance: 100, target: MoveTarget.Selected);
        BattleMove miss = CompileWithAccuracy(40, 1,
            Op("historyPower", ("condition", "sourceBeforeTarget"), ("multiplierNum", 2), ("multiplierDen", 1)));
        var battle = new BattleController(
            [Creature("flincher", 2000, 200, flinch), Creature("miss", 2000, 190, miss)],
            [Creature("flinched", 2000, 100, Inert()), Creature("targetmiss", 2000, 90, Inert())],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new TestRng());

        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new(new(BattleSide.Player, 0), new UseMove(0), new ActiveSlotSelection(new(BattleSide.Enemy, 0))),
            new(new(BattleSide.Player, 1), new UseMove(0), new ActiveSlotSelection(new(BattleSide.Enemy, 1))),
            new(new(BattleSide.Enemy, 0), new UseMove(0), new ActiveSlotSelection(new(BattleSide.Player, 0))),
            new(new(BattleSide.Enemy, 1), new Pass()),
        ]));

        Assert.Contains(battle.ActionHistory.Snapshot(), attempt => attempt.Result == BattleActionResult.Prevented);
        Assert.Contains(battle.ActionHistory.Snapshot(), attempt => attempt.Result == BattleActionResult.Missed);
    }

    [Fact]
    public void Resolver_ClassifiesMoveGateAndProtectFailureAsFailed()
    {
        BattleMove gated = Compile(40,
            Op("historyPower", ("condition", "previousActionFailed"), ("multiplierNum", 2), ("multiplierDen", 1)),
            Op("moveGate", ("kind", "notPreviousMove")));
        var battle = new BattleController(Creature("gated", 2000, 100, gated),
            Creature("gateother", 2000, 50, Inert()), Chart(), new TestRng());
        battle.ResolveTurn(new UseMove(0), new Pass());
        battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Equal(BattleActionResult.Failed, Assert.Single(battle.ActionHistory.Snapshot(),
            attempt => attempt.Id.Turn == 1 && attempt.Source.Side == BattleSide.Player).Result);

        BattleMove protect = new(EntityId.Parse("move:protect"), Normal, DamageClass.Status,
            null, null, 20, 4, 0, isProtect: true);
        battle = new BattleController(Creature("protector", 2000, 100, protect),
            Creature("protectother", 2000, 50, Inert()), Chart(), new TestRng());
        battle.ResolveTurn(new UseMove(0), new Pass());
        battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Equal(BattleActionResult.Failed, Assert.Single(battle.ActionHistory.Snapshot(),
            attempt => attempt.Id.Turn == 1 && attempt.Source.Side == BattleSide.Player).Result);
    }

    [Fact]
    public void Doubles_SideChainSharesPriorTurnsWithoutSameTurnDoubleCounting()
    {
        BattleMove echoed = Compile(40,
            Op("consecutivePower", ("scope", "sideAttemptedTurns"), ("mode", "linear"), ("step", 40), ("cap", 200)));
        var battle = new BattleController(
            [Creature("p0", 3000, 100, echoed), Creature("p1", 3000, 90, echoed)],
            [Creature("e0", 3000, 20, Inert()), Creature("e1", 3000, 10, Inert())],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new TestRng());

        battle.ResolveTurn(DoublesMoves());
        battle.ResolveTurn(DoublesMoves());

        Assert.Equal([40, 40, 80, 80], battle.QueryTrace
            .Where(entry => entry.Result.Query == BattleQueryId.BasePower)
            .Select(entry => entry.Result.FinalValue.ToInt32()));
        Assert.Equal(4, battle.ActionHistory.Snapshot().Count(attempt => attempt.Source.Side == BattleSide.Player));
    }

    [Fact]
    public void ReplacementEntryHazardFaint_PowersSurvivingAllyOnNextTurn()
    {
        BattleMove spikes = new(EntityId.Parse("move:layered_hazard"), Normal, DamageClass.Status,
            null, null, 20, 0, 0, target: MoveTarget.OpponentsField,
            secondaryEffects: [new SetEntryHazardEffect(EntryHazardConditions.LegacyLayeredDamage)]);
        BattleMove ko = new(EntityId.Parse("move:ko"), Normal, DamageClass.Special,
            300, null, 20, 0, 0, target: MoveTarget.Selected);
        BattleMove retaliate = Compile(50,
            Op("historyPower", ("condition", "allyFaintedPreviousTurn"), ("multiplierNum", 2), ("multiplierDen", 1)));
        var battle = new BattleController(
            [Creature("hazard", 1000, 200, spikes), Creature("striker", 1000, 190, ko)],
            [Creature("victim", 1, 10, Inert()), Creature("survivor", 1000, 20, retaliate),
                Creature("frail", 1, 5, Inert()), Creature("reserve", 1000, 5, Inert())],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new TestRng());
        BattleSlot enemy0 = new(BattleSide.Enemy, 0);

        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new(new(BattleSide.Player, 0), new UseMove(0)),
            new(new(BattleSide.Player, 1), new UseMove(0), new ActiveSlotSelection(enemy0)),
            new(enemy0, new Pass()),
            new(new(BattleSide.Enemy, 1), new Pass()),
        ]));
        battle.ResolveReplacements([new(enemy0, 2)]);
        battle.ResolveReplacements([new(enemy0, 3)]);
        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new(new(BattleSide.Player, 0), new Pass()),
            new(new(BattleSide.Player, 1), new Pass()),
            new(enemy0, new Pass()),
            new(new(BattleSide.Enemy, 1), new UseMove(0), new ActiveSlotSelection(new(BattleSide.Player, 0))),
        ]));

        Assert.Equal(100, Assert.Single(battle.QueryTrace,
            entry => entry.Turn == 1 && entry.Result.Query == BattleQueryId.BasePower).Result.FinalValue.ToInt32());
    }

    [Fact]
    public void SmartAi_UsesVisibleSpeedAndHistoryButNotPrivatePlans()
    {
        BattleMove formula = Compile(50,
            Op("historyPower", ("condition", "sourceAfterTarget"), ("multiplierNum", 2), ("multiplierDen", 1)));
        BattleMove fixedMove = new(EntityId.Parse("move:fixed"), Normal, DamageClass.Special, 75, null, 20, 0, 0);
        BattleCreature enemy = Creature("enemyai", 1000, 50, formula, fixedMove);
        BattleCreature player = Creature("playerai", 1000, 100, Inert());
        var history = new BattleActionHistory();
        history.BeginTurn(0, [new(Owner(BattleSide.Enemy, 0, Enemy0), BattlePlannedActionKind.Move),
            new(Owner(BattleSide.Player, 0, Player0), BattlePlannedActionKind.Other)]);

        SmartAiDecision decision = SmartAi.ChooseAction(new SmartAiContext([enemy], 0, [player], 0,
            Chart(), new TestRng(), Weights: new SmartAiWeights { NoiseFraction = 0 }, ActionHistory: history));

        Assert.Equal(0, Assert.IsType<UseMove>(decision.Action).MoveIndex);
        Assert.True(decision.Scores[0].Score > decision.Scores[1].Score);
    }

    private static BattleHistoryOwner Owner(BattleSide side, int party, BattleSlot slot) => new(side, party, slot);

    private static BattleActionPlan[] Plans(BattleHistoryOwner source, BattleHistoryOwner target) =>
        [new(source, BattlePlannedActionKind.Move), new(target, BattlePlannedActionKind.Move)];

    private static BattleTurnActions DoublesMoves() => new(BattleTopology.Doubles,
    [
        new(new(BattleSide.Player, 0), new UseMove(0), new ActiveSlotSelection(new(BattleSide.Enemy, 0))),
        new(new(BattleSide.Player, 1), new UseMove(0), new ActiveSlotSelection(new(BattleSide.Enemy, 1))),
        new(new(BattleSide.Enemy, 0), new Pass()),
        new(new(BattleSide.Enemy, 1), new Pass()),
    ]);

    private static void Complete(BattleActionHistory history, int sequence, BattleHistoryOwner source, EntityId move,
        BattleActionResult result, BattleHistoryOwner target)
    {
        BattleActionAttemptId id = history.BeginMove(sequence, source, move);
        if (result is BattleActionResult.Missed or BattleActionResult.Succeeded or BattleActionResult.Connected)
            history.MarkStarted(id);
        history.Complete(id, result, [target]);
    }

    private static BattleMove Compile(int? power, params Effect[] effects) => MoveCompiler.ToBattleMove(new Move
    {
        Id = Chain, Name = "Chain", Type = Normal, DamageClass = DamageClass.Special,
        Power = power, Accuracy = null, Pp = 30, Target = MoveTarget.Selected, Effects = effects,
    });

    private static BattleMove CompileWithAccuracy(int power, int accuracy, params Effect[] effects) =>
        MoveCompiler.ToBattleMove(new Move
        {
            Id = Chain, Name = "Chain", Type = Normal, DamageClass = DamageClass.Special,
            Power = power, Accuracy = accuracy, Pp = 30, Target = MoveTarget.Selected, Effects = effects,
        });

    private static Effect Op(string op, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Params = values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static BattleMove Inert() =>
        new(Other, Normal, DamageClass.Status, null, null, 30, 0, 0);

    private static BattleCreature Creature(string slug, int hp, int speed, params BattleMove[] moves) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
            new Stats(hp, 100, 100, 100, 100, speed), moves);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private sealed class TestRng : IRng
    {
        public int DoubleDraws { get; private set; }
        public int Next(int maxExclusive) => maxExclusive switch { 16 => 15, 100 => 99, _ => 0 };
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
        public double NextDouble()
        {
            DoubleDraws++;
            return 0.99;
        }
    }

    private sealed class TieRng(int tieDraw) : IRng
    {
        public int Next(int maxExclusive) => maxExclusive switch { 2 => tieDraw, 16 => 15, _ => 0 };
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
        public double NextDouble() => 0.99;
    }
}
