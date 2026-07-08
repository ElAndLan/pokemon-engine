using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleStatusTurnTests
{
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Grass = EntityId.Parse("type:grass");

    private static TypeChart Chart() => new([new TypeDef { Id = Fire }, new TypeDef { Id = Grass }]);

    private static BattleMove Weak() =>
        new(EntityId.Parse("move:m"), Fire, DamageClass.Special, 1, 100, 10, 0, 0);

    private static BattleCreature Creature(EntityId type, int hp) =>
        new(EntityId.Parse("species:x"), "X", 50, [type], new Stats(hp, 50, 50, 50, 50, 50), [Weak()]);

    [Fact]
    public void Burn_DamagesTargetAtEndOfTurn()
    {
        var player = Creature(Fire, 80);
        var enemy = Creature(Grass, 200);
        player.SetStatus(PersistentStatus.Burn);
        var b = new BattleController(player, enemy, Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = b.ResolveTurn(new UseMove(0), new UseMove(0));

        // 80/8 = 10 residual on the player.
        Assert.Contains(events, e => e is StatusDamage { Side: BattleSide.Player, Amount: 10 });
        Assert.True(player.CurrentHp <= 70);
    }

    [Fact]
    public void Toxic_RampsAcrossTurns()
    {
        var player = Creature(Fire, 160);
        var enemy = Creature(Grass, 200);
        player.SetStatus(PersistentStatus.Toxic); // counter starts at 1
        var b = new BattleController(player, enemy, Chart(), new Rng(1));

        var t1 = b.ResolveTurn(new UseMove(0), new UseMove(0)); // 160*1/16 = 10
        var t2 = b.ResolveTurn(new UseMove(0), new UseMove(0)); // 160*2/16 = 20

        Assert.Contains(t1, e => e is StatusDamage { Side: BattleSide.Player, Amount: 10 });
        Assert.Contains(t2, e => e is StatusDamage { Side: BattleSide.Player, Amount: 20 });
    }

    [Fact]
    public void ResidualCanFaint_AndEndBattle()
    {
        var player = Creature(Fire, 8); // 8/8 = 1 residual, but low HP after enemy hit
        var enemy = Creature(Grass, 200);
        player.SetStatus(PersistentStatus.Poison);
        player.TakeDamage(7); // 1 HP left
        var b = new BattleController(player, enemy, Chart(), new Rng(1));

        var events = b.ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(player.IsFainted);
        Assert.Contains(events, e => e is Fainted { Side: BattleSide.Player });
        Assert.Equal(new BattleOutcome(BattleSide.Enemy), b.Outcome);
    }

    [Fact]
    public void NoStatus_NoResidual()
    {
        var player = Creature(Fire, 100);
        var enemy = Creature(Grass, 200);
        var events = new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.DoesNotContain(events, e => e is StatusDamage);
    }
}
