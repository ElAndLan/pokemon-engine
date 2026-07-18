using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>Thrash/Outrage rampage lock (catalog §9.3 outrage_family): forced for 2–3 turns, then self-confuse.</summary>
public sealed class BattleRampageTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 25, 0, 0);

    private static BattleMove Thrash() =>
        new(EntityId.Parse("move:thrash"), Normal, DamageClass.Physical, 40, 100, 10, 0, 0, multiTurnLock: true);

    private static BattleCreature Fast(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:f"), "F", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 100), moves);

    private static BattleCreature Slow(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:s"), "S", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 1), moves);

    [Fact]
    public void Thrash_HitsAndLocksTheUserIn()
    {
        var player = Fast(300, Thrash());
        var enemy = Slow(9999, Inert());
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(enemy.CurrentHp < 9999); // hit turn 1
        Assert.True(player.IsLocked);
        Assert.Equal(9, player.Moves[0].Pp); // PP spent once
    }

    [Fact]
    public void Thrash_SpendsNoExtraPp_WhileLocked()
    {
        var player = Fast(300, Thrash());
        var enemy = Slow(9999, Inert());
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // turn 1 (PP 10→9)
        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // locked continuation → no PP

        Assert.Equal(9, player.Moves[0].Pp);
    }

    [Fact]
    public void Thrash_ForcedEvenIfAnotherMoveSubmitted()
    {
        var player = Fast(300, Thrash(), Inert());
        var enemy = Slow(9999, Inert());
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // thrash starts
        int before = enemy.CurrentHp;
        battle.ResolveTurn(new UseMove(1), new UseMove(0)); // submit inert — thrash fires anyway

        Assert.True(enemy.CurrentHp < before);
    }

    [Fact]
    public void Thrash_ReplacesSubmittedSwitchWithForcedMove()
    {
        var player = Fast(300, Thrash());
        var playerB = Fast(300, Inert());
        var enemy = Slow(9999, Inert());
        var battle = new BattleController([player, playerB], [enemy], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        int before = enemy.CurrentHp;
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Switch(1), new UseMove(0));

        Assert.True(enemy.CurrentHp < before);
        Assert.Same(player, battle.Active(BattleSide.Player));
        Assert.Contains(events, e => e is MoveUsed { Move: var move } && move == player.Moves[0].Move);
    }

    [Fact]
    public void Thrash_SelfConfusesWhenTheRampageEnds()
    {
        var player = Fast(9999, Thrash());
        var enemy = Slow(9999, Inert());
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // turn 1 starts the rampage
        int turns = 1;
        while (player.IsLocked && turns < 5)
        {
            battle.ResolveTurn(new UseMove(0), new UseMove(0));
            turns++;
        }

        Assert.False(player.IsLocked);
        Assert.True(player.IsConfused); // rampage ends in self-confusion
        Assert.InRange(turns, 2, 3);     // locked for 2–3 turns total
    }

    [Fact]
    public void RampageLock_ContinuesInDoublesAfterSpendingItsLastPp()
    {
        BattleMove rampage = new(EntityId.Parse("move:rampage"), Normal, DamageClass.Physical,
            40, 100, 1, 0, 0, target: MoveTarget.RandomOpponent, multiTurnLock: true);
        var player = Fast(9999, rampage, Inert());
        var enemy0 = Slow(9999, Inert());
        var enemy1 = Slow(9999, Inert());
        var battle = new BattleController([player, Fast(9999, Inert())], [enemy0, enemy1],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(), new Rng(1));

        battle.ResolveTurn(DoublesActions(new UseMove(0)));
        int hpAfterFirst = enemy0.CurrentHp + enemy1.CurrentHp;
        IReadOnlyList<BattleEvent> second = battle.ResolveTurn(DoublesActions(new UseMove(1)));

        Assert.Equal(0, rampage.Pp);
        Assert.True(enemy0.CurrentHp + enemy1.CurrentHp < hpAfterFirst);
        Assert.Contains(second, e => e is MoveUsed { Move: var move } && move == rampage.Move);
    }

    private static BattleTurnActions DoublesActions(BattleAction playerAction) => new(BattleTopology.Doubles,
    [
        new BattleActionSubmission(new(BattleSide.Player, 0), playerAction),
        new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
        new BattleActionSubmission(new(BattleSide.Enemy, 0), new Pass()),
        new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
    ]);
}
