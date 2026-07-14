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
    public void Compiler_RejectsInvalidActionGateParameters()
    {
        Assert.Throws<ArgumentException>(() => Compile(Op("moveGate")));
        Assert.Throws<ArgumentException>(() => Compile(Op("moveGate", 50, ("kind", "firstAction"))));
        Assert.Throws<ArgumentException>(() => Compile(Op("moveGate", null, ("kind", "never"))));
        Assert.Throws<ArgumentException>(() => Compile(Op("queueActionGate", 50)));
        Assert.Throws<ArgumentException>(() => Compile(Op("queueActionGate", null, ("turns", 0))));
        Assert.Throws<ArgumentException>(() => Compile(Op("queueActionGate", null, ("turns", 1), ("extra", 1))));
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
}
