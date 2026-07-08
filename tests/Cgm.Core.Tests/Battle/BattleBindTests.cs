using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>Partial-trap volatile (Bind/Wrap): residual chip + switch prevention for 4–5 turns (catalog §7.2).</summary>
public sealed class BattleBindTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 25, 0, 0);

    private static BattleMove BindMove() =>
        new(EntityId.Parse("move:wrap"), Normal, DamageClass.Physical, 15, 100, 25, 0, 0, binds: true);

    private static BattleCreature Fast(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:f"), "F", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 100), moves);

    private static BattleCreature Slow(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:s"), "S", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 1), moves);

    [Fact]
    public void Bind_TrapsTargetAndChipsEachTurn()
    {
        var player = Fast(400, BindMove());
        var enemy = Slow(800, Inert());
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));

        var events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(enemy.IsTrapped);
        Assert.Contains(events, e => e is Bound { Side: BattleSide.Enemy });
        Assert.Contains(events, e => e is BoundHurt { Side: BattleSide.Enemy }); // residual same turn it lands
    }

    [Fact]
    public void TrappedCreature_CannotSwitch()
    {
        var player = Fast(400, BindMove());
        var enemyA = Slow(800, Inert());
        var enemyB = Slow(800, Inert());
        var battle = new BattleController([player], [enemyA, enemyB], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // enemyA trapped
        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(new UseMove(0), new Switch(1)));
    }

    [Fact]
    public void Bind_ReleasesAfterDuration()
    {
        var player = Fast(400, BindMove(), Inert());
        var enemy = Slow(2000, Inert());
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // wrap lands (4–5 turns)
        int guard = 0;
        while (enemy.IsTrapped && guard++ < 8)
            battle.ResolveTurn(new UseMove(1), new UseMove(0)); // wait it out

        Assert.False(enemy.IsTrapped);
        Assert.InRange(guard, 3, 5); // released within the 4–5 turn window
    }

    [Fact]
    public void SwitchAllowedAgain_OnceTrapExpires()
    {
        var player = Fast(2000, Inert());
        var playerB = Fast(2000, Inert());
        var enemy = Slow(2000, BindMove(), Inert()); // binds once, then stays inert so it can't re-trap
        var battle = new BattleController([player, playerB], [enemy], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // player trapped by enemy's wrap
        Assert.True(player.IsTrapped);
        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(new Switch(1), new UseMove(1)));

        int guard = 0;
        while (player.IsTrapped && guard++ < 8)
            battle.ResolveTurn(new UseMove(0), new UseMove(1)); // enemy stays inert while the trap ticks down

        battle.ResolveTurn(new Switch(1), new UseMove(1)); // now legal — no throw
        Assert.Same(playerB, battle.Active(BattleSide.Player)); // switch went through
    }
}
