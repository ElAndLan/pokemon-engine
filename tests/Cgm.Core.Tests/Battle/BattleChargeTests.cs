using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>Two-turn charge moves (Solar Beam-style, catalog §7.2 charge): charge turn 1, strike turn 2.</summary>
public sealed class BattleChargeTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 25, 0, 0);

    private static BattleMove SolarBeam() =>
        new(EntityId.Parse("move:solarbeam"), Normal, DamageClass.Special, 120, 100, 10, 0, 0, chargeTurn: true);

    private static BattleCreature Fast(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:f"), "F", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 100), moves);

    private static BattleCreature Slow(int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:s"), "S", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 1), moves);

    [Fact]
    public void ChargeTurn_DealsNoDamageAndLocksIn()
    {
        var player = Fast(300, SolarBeam());
        var enemy = Slow(2000, Inert());
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));

        var events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(2000, enemy.CurrentHp);          // no damage on the charge turn
        Assert.True(player.IsCharging);
        Assert.Equal(9, player.Moves[0].Pp);          // PP spent on the charge turn
        Assert.Contains(events, e => e is Charging { Side: BattleSide.Player });
    }

    [Fact]
    public void SecondTurn_FiresTheChargedMove()
    {
        var player = Fast(300, SolarBeam());
        var enemy = Slow(2000, Inert());
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // charge
        var events = battle.ResolveTurn(new UseMove(0), new UseMove(0)); // fire

        Assert.True(enemy.CurrentHp < 2000);        // damage dealt on turn 2
        Assert.False(player.IsCharging);            // charge consumed
        Assert.Equal(9, player.Moves[0].Pp);        // no extra PP spent on the fire turn
        Assert.Contains(events, e => e is DamageDealt { Target: BattleSide.Enemy });
    }

    [Fact]
    public void ChargingCreature_FiresEvenIfADifferentMoveIsSubmitted()
    {
        // The lock overrides the submitted action on the second turn.
        var player = Fast(300, SolarBeam(), Inert());
        var enemy = Slow(2000, Inert());
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // charge solar beam
        battle.ResolveTurn(new UseMove(1), new UseMove(0)); // submit inert — but solar beam fires

        Assert.True(enemy.CurrentHp < 2000);
        Assert.False(player.IsCharging);
    }

    [Fact]
    public void ChargingCreature_CannotSwitch()
    {
        var player = Fast(300, SolarBeam());
        var playerB = Fast(300, Inert());
        var enemy = Slow(2000, Inert());
        var battle = new BattleController([player, playerB], [enemy], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new UseMove(0)); // charging
        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(new Switch(1), new UseMove(0)));
    }
}
