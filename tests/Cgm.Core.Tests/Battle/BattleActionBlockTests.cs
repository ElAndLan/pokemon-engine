using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleActionBlockTests
{
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Grass = EntityId.Parse("type:grass");

    private static TypeChart Chart() => new([new TypeDef { Id = Fire }, new TypeDef { Id = Grass }]);

    private static BattleMove Weak() =>
        new(EntityId.Parse("move:m"), Fire, DamageClass.Special, 1, 100, 20, 0, 0);

    private static BattleCreature Creature(EntityId type) =>
        new(EntityId.Parse("species:x"), "X", 50, [type], new Stats(300, 100, 100, 100, 100, 100), [Weak()]);

    private static BattleController Battle(BattleCreature p, BattleCreature e, int seed = 1) =>
        new(p, e, Chart(), new Rng(seed));

    [Fact]
    public void Sleep_BlocksForCounterThenWakes()
    {
        var player = Creature(Fire);
        var enemy = Creature(Grass);
        player.SetStatus(PersistentStatus.Sleep, counter: 2);
        var b = Battle(player, enemy);

        var t1 = b.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Contains(t1, e => e is StillAsleep { Side: BattleSide.Player });
        Assert.DoesNotContain(t1, e => e is MoveUsed { Side: BattleSide.Player });

        var t2 = b.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Contains(t2, e => e is StillAsleep { Side: BattleSide.Player });

        var t3 = b.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Contains(t3, e => e is WokeUp { Side: BattleSide.Player });
        Assert.Contains(t3, e => e is MoveUsed { Side: BattleSide.Player }); // acts after waking
        Assert.Null(player.Status);
    }

    [Fact]
    public void Sleep_DoesNotConsumePpWhileBlocked()
    {
        var player = Creature(Fire);
        var enemy = Creature(Grass);
        player.SetStatus(PersistentStatus.Sleep, counter: 1);
        int pp = player.Moves[0].Pp;
        Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(pp, player.Moves[0].Pp); // no PP spent while asleep
    }

    [Fact]
    public void Paralysis_SometimesBlocks_SometimesActs()
    {
        // Across many single-turn battles, a paralyzed mover is both blocked and free at times.
        int blocked = 0, acted = 0;
        for (int seed = 0; seed < 200; seed++)
        {
            var player = Creature(Fire);
            var enemy = Creature(Grass);
            player.SetStatus(PersistentStatus.Paralysis);
            var events = Battle(player, enemy, seed).ResolveTurn(new UseMove(0), new UseMove(0));
            if (events.Any(e => e is FullyParalyzed { Side: BattleSide.Player })) blocked++;
            if (events.Any(e => e is MoveUsed { Side: BattleSide.Player })) acted++;
        }
        Assert.True(blocked > 0, "paralysis never blocked");
        Assert.True(acted > 0, "paralysis always blocked");
    }

    [Fact]
    public void Freeze_SometimesThaws_SometimesStaysFrozen()
    {
        int thawed = 0, frozen = 0;
        for (int seed = 0; seed < 200; seed++)
        {
            var player = Creature(Fire);
            var enemy = Creature(Grass);
            player.SetStatus(PersistentStatus.Freeze);
            var events = Battle(player, enemy, seed).ResolveTurn(new UseMove(0), new UseMove(0));
            if (events.Any(e => e is Thawed { Side: BattleSide.Player })) thawed++;
            if (events.Any(e => e is StillFrozen { Side: BattleSide.Player })) frozen++;
        }
        Assert.True(thawed > 0, "never thawed");
        Assert.True(frozen > 0, "never stayed frozen");
    }

    [Fact]
    public void NoStatus_NeverBlocked()
    {
        var events = Battle(Creature(Fire), Creature(Grass)).ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.DoesNotContain(events, e => e is FullyParalyzed or StillAsleep or StillFrozen);
        Assert.Contains(events, e => e is MoveUsed { Side: BattleSide.Player });
    }
}
