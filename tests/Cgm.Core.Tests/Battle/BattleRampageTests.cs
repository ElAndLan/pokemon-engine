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
    public void Thrash_LockedCreatureCannotSwitch()
    {
        var player = Fast(300, Thrash());
        var playerB = Fast(300, Inert());
        var enemy = Slow(9999, Inert());
        var battle = new BattleController([player, playerB], [enemy], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(new Switch(1), new UseMove(0)));
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
}
