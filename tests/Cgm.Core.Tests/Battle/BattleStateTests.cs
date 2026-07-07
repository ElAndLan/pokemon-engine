using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleCreatureTests
{
    private static BattleCreature Make(int hp = 100) =>
        new(EntityId.Parse("species:mon"), "Mon", 50, [EntityId.Parse("type:fire")],
            new Stats(hp, 50, 50, 50, 50, 50), [Move()]);

    private static BattleMove Move(int pp = 5) =>
        new(EntityId.Parse("move:ember"), EntityId.Parse("type:fire"), DamageClass.Special, 40, 100, pp, 0, 0);

    [Fact]
    public void StartsAtFullHp_NotFainted()
    {
        BattleCreature c = Make();
        Assert.Equal(100, c.CurrentHp);
        Assert.Equal(100, c.MaxHp);
        Assert.False(c.IsFainted);
    }

    [Fact]
    public void TakeDamage_ClampsAtZero_AndFaints()
    {
        BattleCreature c = Make();
        c.TakeDamage(30);
        Assert.Equal(70, c.CurrentHp);
        c.TakeDamage(999);
        Assert.Equal(0, c.CurrentHp);
        Assert.True(c.IsFainted);
    }

    [Fact]
    public void Heal_ClampsAtMax()
    {
        BattleCreature c = Make();
        c.TakeDamage(50);
        c.Heal(20);
        Assert.Equal(70, c.CurrentHp);
        c.Heal(999);
        Assert.Equal(100, c.CurrentHp);
    }

    [Fact]
    public void Move_PpDecrementsAndClampsAtZero()
    {
        BattleMove m = Move(pp: 2);
        Assert.True(m.HasPp);
        m.UsePp(); m.UsePp();
        Assert.False(m.HasPp);
        Assert.Equal(0, m.Pp);
        m.UsePp(); // no underflow
        Assert.Equal(0, m.Pp);
    }
}

public sealed class TurnOrderTests
{
    [Fact]
    public void HigherPriority_GoesFirst_RegardlessOfSpeed()
    {
        // A has +1 priority but far less speed → still first.
        Assert.True(TurnOrder.AFirst(1, 10, 0, 200, new FakeRng()));
        Assert.False(TurnOrder.AFirst(0, 200, 1, 10, new FakeRng()));
    }

    [Fact]
    public void EqualPriority_HigherSpeedGoesFirst()
    {
        Assert.True(TurnOrder.AFirst(0, 100, 0, 99, new FakeRng()));
        Assert.False(TurnOrder.AFirst(0, 99, 0, 100, new FakeRng()));
    }

    [Fact]
    public void SpeedTie_BrokenByRng()
    {
        Assert.True(TurnOrder.AFirst(0, 100, 0, 100, new FakeRng(ints: [0])));
        Assert.False(TurnOrder.AFirst(0, 100, 0, 100, new FakeRng(ints: [1])));
    }

    [Fact]
    public void SpeedTie_IsAboutFiftyFifty()
    {
        var rng = new Rng(5);
        int aFirst = Enumerable.Range(0, 10_000).Count(_ => TurnOrder.AFirst(0, 50, 0, 50, rng));
        Assert.InRange(aFirst, 4700, 5300);
    }
}
