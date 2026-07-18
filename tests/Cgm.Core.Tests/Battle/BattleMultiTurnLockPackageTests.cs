using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleMultiTurnLockPackageTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    [Fact]
    public void Compiler_ProducesTypedRampageAndPowerRampProfiles()
    {
        BattleMove rampage = Compile(Op("damage"), Op("multiTurnLock"));
        BattleMove rollout = Compile(Op("damage"), Op("multiTurnLock",
            ("minTurns", 5), ("maxTurns", 5), ("powerNum", 2), ("powerDen", 1),
            ("maxPowerStep", 4), ("endOnFailure", true), ("endEffect", "none")));

        Assert.Equal(new MultiTurnLockProfile(PowerStep: new Fraction(1, 1)), rampage.MultiTurnLockProfile);
        Assert.Equal(new MultiTurnLockProfile(5, 5, false, new Fraction(2, 1), 4, true,
            MultiTurnLockEndEffect.None), rollout.MultiTurnLockProfile);
    }

    [Fact]
    public void Compiler_RejectsMalformedOrRecursiveLockDefinitions()
    {
        Assert.Throws<ArgumentException>(() => Compile(Op("damage"), Op("multiTurnLock", ("minTurns", 0))));
        Assert.Throws<ArgumentException>(() => Compile(Op("damage"), Op("multiTurnLock", ("minTurns", 4), ("maxTurns", 3))));
        Assert.Throws<ArgumentException>(() => Compile(Op("damage"), Op("multiTurnLock", ("powerDen", 0))));
        Assert.Throws<ArgumentException>(() => Compile(Op("damage"), Op("multiTurnLock", ("unknown", true))));
        Assert.Throws<ArgumentException>(() => Compile(Op("damage"), Op("multiTurnLock"), Op("multiTurnLock")));
        Assert.Throws<ArgumentException>(() => Compile(Op("damage"), Op("multiTurnLock"), Op("chargeTurn")));
        Assert.Throws<ArgumentException>(() => new BattleMove(EntityId.Parse("move:invalid"), Normal,
            DamageClass.Physical, 30, 100, 10, 0, 0,
            multiTurnLockProfile: new MultiTurnLockProfile(5, 4)));
        Assert.Throws<ArgumentException>(() => new BattleMove(EntityId.Parse("move:overflow"), Normal,
            DamageClass.Physical, 30, 100, 10, 0, 0,
            multiTurnLockProfile: new MultiTurnLockProfile(4, 4, PowerStep: new Fraction(int.MaxValue, 1),
                MaxPowerStep: 3)));
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public void Rampage_DrawsDurationOnceAtStartAndPinsMinMax(bool maximum, int expected)
    {
        var rng = new DurationRng(maximum);
        BattleCreature source = Creature("source", 100, Rampage());
        var battle = Battle(source, Creature("target", 1, Inert()), rng);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Contains(events, e => e is MultiTurnLockStarted { Turns: var turns } && turns == expected);
        Assert.Equal(expected - 1, source.LockTurns);
        EffectTraceEntry trace = Assert.Single(battle.Trace, entry => entry.Kind == EffectTraceKind.LockDuration);
        Assert.True(trace.Performed);
        Assert.Equal(expected, trace.Value);
        Assert.Equal(1, rng.DurationCalls);
    }

    [Fact]
    public void FixedLock_RampsThroughCapWithoutDurationDraw()
    {
        BattleCreature source = Creature("source", 100, Rollout());
        var battle = Battle(source, Creature("target", 1, Inert()), new DurationRng(false));

        for (int i = 0; i < 5; i++)
            battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Equal([30, 60, 120, 240, 480], battle.QueryTrace
            .Where(entry => entry.Result.Query == BattleQueryId.BasePower && entry.SourceSlot.Side == BattleSide.Player)
            .Select(entry => entry.Result.FinalValue.ToInt32()).ToArray());
        Assert.False(source.IsLocked);
        Assert.False(source.IsConfused);
        Assert.DoesNotContain(battle.Trace, entry => entry.Kind == EffectTraceKind.LockDuration && entry.Performed);
    }

    [Fact]
    public void KeyedPowerBoost_ComposesWithRampAndClearsOnSwitch()
    {
        BattleMove boostMove = CompileStatus(Op("statStage", ("stat", "def"), ("delta", 1)),
            Op("multiTurnPowerBoost", ("key", "rollout"), ("num", 2), ("den", 1)));
        BattleCreature source = Creature("source", 100, boostMove, Rollout());
        BattleCreature reserve = Creature("reserve", 50, Inert());
        var battle = new BattleController([source, reserve], [Creature("target", 1, Inert())], Chart(), new DurationRng(false));

        battle.ResolveTurn(new UseMove(0), new Pass());
        battle.ResolveTurn(new UseMove(1), new Pass());

        Assert.Equal("rollout", source.MultiTurnPowerBoostKey);
        Assert.Equal(60, battle.QueryTrace.Last(entry => entry.Result.Query == BattleQueryId.BasePower)
            .Result.FinalValue.ToInt32());
        source.EndLock();
        battle.ResolveTurn(new Switch(1), new Pass());
        Assert.Null(source.MultiTurnPowerBoostKey);
    }

    [Fact]
    public void SelectedTarget_IsOwnedByLockAcrossForcedSubmissions()
    {
        BattleCreature source = Creature("source", 100, Rollout());
        BattleCreature enemy0 = Creature("enemy0", 1, Inert());
        BattleCreature enemy1 = Creature("enemy1", 1, Inert());
        var battle = new BattleController([source, Creature("ally", 50, Inert())], [enemy0, enemy1],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new DurationRng(false));
        BattleSlot first = new(BattleSide.Enemy, 0);
        BattleSlot second = new(BattleSide.Enemy, 1);

        battle.ResolveTurn(Actions(new UseMove(0), first));
        int enemy0Hp = enemy0.CurrentHp;
        int enemy1Hp = enemy1.CurrentHp;
        battle.ResolveTurn(Actions(new UseMove(0), second));

        Assert.True(enemy0.CurrentHp < enemy0Hp);
        Assert.Equal(enemy1Hp, enemy1.CurrentHp);
    }

    [Fact]
    public void FailureEndsPowerRampButRampageConsumesItsStoredTurn()
    {
        BattleCreature rolloutUser = Creature("rollout", 100, Rollout(accuracy: 1));
        var rolloutBattle = Battle(rolloutUser, Creature("target", 1, Inert()), new MissRng());
        IReadOnlyList<BattleEvent> rolloutEvents = rolloutBattle.ResolveTurn(new UseMove(0), new Pass());

        Assert.False(rolloutUser.IsLocked);
        Assert.Contains(rolloutEvents, e => e is MultiTurnLockEnded { Reason: MultiTurnLockEndReason.Failed });

        BattleCreature rampageUser = Creature("rampage", 100, Rampage(accuracy: 1));
        var rampageBattle = Battle(rampageUser, Creature("target2", 1, Inert()), new MissRng());
        rampageBattle.ResolveTurn(new UseMove(0), new Pass());
        Assert.True(rampageUser.IsLocked);
    }

    [Fact]
    public void PreventionEndsPowerRampBeforePpOrTargetDraws()
    {
        BattleMove move = Rollout();
        BattleCreature source = Creature("source", 100, move);
        var rng = new DurationRng(false);
        var battle = Battle(source, Creature("target", 1, Inert()), rng);
        battle.ResolveTurn(new UseMove(0), new Pass());
        source.SetStatus(PersistentStatus.Sleep, counter: 2);
        int pp = move.Pp;
        int traceCount = battle.Trace.Count;

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Pass(), new Pass());

        Assert.False(source.IsLocked);
        Assert.Equal(pp, move.Pp);
        Assert.Contains(events, e => e is MultiTurnLockEnded { Reason: MultiTurnLockEndReason.Failed });
        Assert.DoesNotContain(battle.Trace.Skip(traceCount), trace => trace.Kind is
            EffectTraceKind.TargetSelection or EffectTraceKind.Accuracy);
    }

    [Fact]
    public void HardQueuedSkipPrecedesAndTerminatesForcedRepeatWithoutRecursion()
    {
        BattleMove move = new(EntityId.Parse("move:skip_lock"), Normal, DamageClass.Physical,
            30, 100, 10, 0, 0, secondaryEffects: [new QueueActionGateEffect(1, QueueActionGateOwner.Creature)],
            multiTurnLockProfile: new MultiTurnLockProfile(2, 2, EndOnFailure: true,
                EndEffect: MultiTurnLockEndEffect.None));
        BattleCreature source = Creature("source", 100, move);
        var battle = Battle(source, Creature("target", 1, Inert()), new DurationRng(false));
        battle.ResolveTurn(new UseMove(0), new Pass());

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Contains(events, e => e is ActionSkipped { Slot.Side: BattleSide.Player });
        Assert.Contains(events, e => e is MultiTurnLockEnded { Reason: MultiTurnLockEndReason.Failed });
        Assert.DoesNotContain(events, e => e is MoveUsed { Slot.Side: BattleSide.Player });
        Assert.False(source.IsLocked);
    }

    [Fact]
    public void RepeatPpPolicyEndsVisiblyWhenNoPpRemains()
    {
        BattleMove move = new(EntityId.Parse("move:pp_lock"), Normal, DamageClass.Physical, 30, 100, 1, 0, 0,
            multiTurnLockProfile: new MultiTurnLockProfile(3, 3, true, EndEffect: MultiTurnLockEndEffect.None));
        BattleCreature source = Creature("source", 100, move);
        var battle = Battle(source, Creature("target", 1, Inert()), new DurationRng(false));
        battle.ResolveTurn(new UseMove(0), new Pass());

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.False(source.IsLocked);
        Assert.Contains(events, e => e is ActionInvalidated { Reason: ActionInvalidationReason.ResourceChanged });
        Assert.Contains(events, e => e is MultiTurnLockEnded { Reason: MultiTurnLockEndReason.NoPp });
        Assert.DoesNotContain(events, e => e is MoveUsed);
    }

    [Fact]
    public void ForcedSwitchAndFaintClearTheLockWithVisibleReasons()
    {
        BattleCreature source = Creature("source", 100, Rollout());
        BattleCreature reserve = Creature("reserve", 50, Inert());
        BattleMove force = new(EntityId.Parse("move:force"), Normal, DamageClass.Status, null, 100, 10, 0, 0,
            forcesSwitch: true);
        BattleCreature enemy = Creature("enemy", 1, force);
        var switchBattle = new BattleController([source, reserve], [enemy], Chart(), new DurationRng(false));

        IReadOnlyList<BattleEvent> switchEvents = switchBattle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Contains(switchEvents, e => e is MultiTurnLockEnded { Reason: MultiTurnLockEndReason.Switch });
        Assert.Same(reserve, switchBattle.Active(BattleSide.Player));

        BattleMove selfFaint = new(EntityId.Parse("move:self_faint"), Normal, DamageClass.Physical, 30, 100, 10, 0, 0,
            selfDestruct: true, multiTurnLockProfile: new MultiTurnLockProfile(3, 3, EndEffect: MultiTurnLockEndEffect.None));
        BattleCreature fainting = Creature("fainting", 100, selfFaint);
        fainting.SetMultiTurnPowerBoost("rollout", new Fraction(2, 1));
        var faintBattle = Battle(fainting, Creature("target", 1, Inert()), new DurationRng(false));
        IReadOnlyList<BattleEvent> faintEvents = faintBattle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Contains(faintEvents, e => e is MultiTurnLockEnded { Reason: MultiTurnLockEndReason.Faint });
        Assert.False(fainting.IsLocked);
        Assert.Null(fainting.MultiTurnPowerBoostKey);
    }

    [Fact]
    public void SmartAi_ExposesOnlyTheForcedRepeatCandidate()
    {
        BattleCreature source = Creature("source", 100, Rampage(), Inert());
        source.StartLock(0, 2);
        SmartAiDecision decision = SmartAi.ChooseAction(new SmartAiContext([source], 0,
            [Creature("player", 1, Inert())], 0, Chart(), new DurationRng(false)));

        Assert.Equal(new UseMove(0), decision.Action);
        Assert.Single(decision.Scores);
        Assert.Contains(decision.Scores[0].Components, component => component.Name == "forcedRepeat");
    }

    [Fact]
    public void RampageTermination_MatchesGolden()
    {
        BattleCreature source = Creature("source", 100, Rampage());
        var battle = Battle(source, Creature("target", 1, Inert()), new DurationRng(false));
        var events = new List<BattleEvent>();
        events.AddRange(battle.ResolveTurn(new UseMove(0), new Pass()));
        events.AddRange(battle.ResolveTurn(new Pass(), new Pass()));

        string actual = string.Join('\n', events.Select(Describe).Where(line => line is not null)
            .Concat(battle.Trace.Where(trace => trace.Kind is EffectTraceKind.LockDuration or EffectTraceKind.ConfusionDuration)
                .Select(trace => $"trace:{trace.Kind}:performed={trace.Performed}:draw={trace.DrawResult}:value={trace.Value}:bound={trace.DrawBound}:min={trace.DrawMinimum}"))!);

        Assert.Equal(Golden("multi-turn-lock-termination"), actual);
    }

    private static string? Describe(BattleEvent battleEvent) => battleEvent switch
    {
        MultiTurnLockStarted started => $"lock:start:{started.Move}:turns={started.Turns}",
        MultiTurnLockContinued continued => $"lock:continue:{continued.Move}:remaining={continued.TurnsRemaining}:step={continued.PowerStep}",
        MultiTurnLockEnded ended => $"lock:end:{ended.Move}:reason={ended.Reason}",
        MoveUsed used when used.Slot.Side == BattleSide.Player => $"move:{used.Move}",
        Confused confused when confused.Slot.Side == BattleSide.Player => "confused",
        _ => null,
    };

    private static string Golden(string name) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Battle", "Goldens", $"{name}.golden"))
        .Replace("\r\n", "\n").TrimEnd();

    private static BattleMove Rampage(int accuracy = 100) => new(EntityId.Parse("move:rampage"), Normal,
        DamageClass.Physical, 30, accuracy, 10, 0, 0, multiTurnLock: true);

    private static BattleMove Rollout(int accuracy = 100) => new(EntityId.Parse("move:rollout"), Normal,
        DamageClass.Physical, 30, accuracy, 10, 0, 0, target: MoveTarget.Selected,
        multiTurnLockProfile: new MultiTurnLockProfile(5, 5, false, new Fraction(2, 1), 4, true,
            MultiTurnLockEndEffect.None, "rollout"));

    private static BattleMove Inert() => new(EntityId.Parse("move:inert"), Normal,
        DamageClass.Status, null, null, 25, 0, 0);

    private static BattleCreature Creature(string id, int speed, params BattleMove[] moves) => new(
        EntityId.Parse($"species:{id}"), id, 50, [Normal], new Stats(9999, 100, 100, 100, 100, speed), moves);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleController Battle(BattleCreature source, BattleCreature target, IRng rng) =>
        new(source, target, Chart(), rng);

    private static BattleTurnActions Actions(BattleAction action, BattleSlot target) => new(BattleTopology.Doubles,
    [
        new BattleActionSubmission(new(BattleSide.Player, 0), action, new ActiveSlotSelection(target)),
        new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
        new BattleActionSubmission(new(BattleSide.Enemy, 0), new Pass()),
        new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
    ]);

    private static BattleMove Compile(params Effect[] effects) => MoveCompiler.ToBattleMove(new Move
    {
        Id = EntityId.Parse("move:compiled"), Name = "Compiled", Type = Normal,
        DamageClass = DamageClass.Physical, Power = 30, Accuracy = 100, Pp = 10,
        Target = MoveTarget.Selected, Effects = effects,
    });

    private static BattleMove CompileStatus(params Effect[] effects) => MoveCompiler.ToBattleMove(new Move
    {
        Id = EntityId.Parse("move:compiled_status"), Name = "Compiled Status", Type = Normal,
        DamageClass = DamageClass.Status, Accuracy = null, Pp = 10,
        Target = MoveTarget.User, Effects = effects,
    });

    private static Effect Op(string op, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Params = values.Length == 0 ? null : values.ToDictionary(value => value.Key,
            value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private sealed class DurationRng(bool maximum) : IRng
    {
        public int DurationCalls { get; private set; }
        public int Next(int maxExclusive) => 0;
        public int Next(int minInclusive, int maxExclusive)
        {
            if (minInclusive == 2 && maxExclusive == 4)
                DurationCalls++;
            return maximum ? maxExclusive - 1 : minInclusive;
        }
        public double NextDouble() => 0;
    }

    private sealed class MissRng : IRng
    {
        public int Next(int maxExclusive) => maxExclusive - 1;
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
        public double NextDouble() => 0;
    }
}
