using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleCaptureTests
{
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Grass = EntityId.Parse("type:grass");

    private static TypeChart Chart() =>
        new([new TypeDef { Id = Fire, DoubleDamageTo = [Grass] }, new TypeDef { Id = Grass }]);

    private static BattleMove M(int power = 1) =>
        new(EntityId.Parse("move:m"), Fire, DamageClass.Special, power, 100, 10, 0, 0);

    private static BattleCreature Creature(EntityId type, int hp, int catchRate) =>
        new(EntityId.Parse("species:x"), "X", 50, [type], new Stats(hp, 50, 50, 50, 50, 50), [M()], catchRate);

    private static BattleController Wild(BattleCreature enemy, int seed = 1) =>
        new(Creature(Fire, 100, 45), enemy, Chart(), new Rng(seed), isWild: true);

    [Fact]
    public void GuaranteedCatch_EndsBattle_EnemyDoesNotAct()
    {
        // Master-ball-sized bonus → guaranteed catch.
        var enemy = Creature(Grass, hp: 100, catchRate: 255);
        BattleController b = Wild(enemy);

        IReadOnlyList<BattleEvent> events = b.ResolveTurn(new ThrowBall(255.0, 1.0), new UseMove(0));

        Assert.True(b.Captured);
        Assert.Equal(new BattleOutcome(BattleSide.Player), b.Outcome);
        Assert.Contains(events, e => e is BallThrown);
        Assert.Contains(events, e => e is CaptureShakes { Count: 4 });
        Assert.Contains(events, e => e is Captured { Side: BattleSide.Enemy });
        Assert.Contains(events, e => e is BattleEnded { Winner: BattleSide.Player });
        Assert.DoesNotContain(events, e => e is MoveUsed); // enemy never acted
    }

    [Fact]
    public void BreakFree_LetsEnemyAct()
    {
        // Tiny catch rate + full HP → effectively impossible; will break free.
        var enemy = Creature(Grass, hp: 100, catchRate: 1);
        BattleController b = Wild(enemy);

        IReadOnlyList<BattleEvent> events = b.ResolveTurn(new ThrowBall(1.0, 1.0), new UseMove(0));

        Assert.False(b.Captured);
        Assert.Null(b.Outcome);
        Assert.Contains(events, e => e is BrokeFree);
        Assert.Contains(events, e => e is MoveUsed { Side: BattleSide.Enemy }); // enemy attacked after
    }

    [Fact]
    public void CaptureInTrainerBattle_Throws()
    {
        var enemy = Creature(Grass, 100, 255);
        var b = new BattleController(Creature(Fire, 100, 45), enemy, Chart(), new Rng(1), isWild: false);
        Assert.Throws<ArgumentException>(() => b.ResolveTurn(new ThrowBall(255.0, 1.0), new UseMove(0)));
    }

    [Fact]
    public void Capture_IsDeterministic()
    {
        static IReadOnlyList<BattleEvent> Run()
        {
            var enemy = new BattleController(
                new BattleCreature(EntityId.Parse("species:p"), "P", 50, [EntityId.Parse("type:fire")], new Stats(100, 50, 50, 50, 50, 50), [
                    new BattleMove(EntityId.Parse("move:m"), EntityId.Parse("type:fire"), DamageClass.Special, 1, 100, 10, 0, 0)]),
                new BattleCreature(EntityId.Parse("species:e"), "E", 50, [EntityId.Parse("type:grass")], new Stats(50, 50, 50, 50, 50, 50), [
                    new BattleMove(EntityId.Parse("move:m"), EntityId.Parse("type:grass"), DamageClass.Special, 1, 100, 10, 0, 0)], 60),
                new TypeChart([new TypeDef { Id = EntityId.Parse("type:fire") }, new TypeDef { Id = EntityId.Parse("type:grass") }]),
                new Rng(2026), isWild: true);
            return enemy.ResolveTurn(new ThrowBall(1.5, 2.0), new UseMove(0));
        }
        Assert.Equal(Run(), Run());
    }
}
