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
}
