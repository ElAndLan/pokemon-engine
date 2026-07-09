using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleItemTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Potion = EntityId.Parse("item:potion");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleMove Tackle(int power = 40) =>
        new(EntityId.Parse("move:tackle"), Normal, DamageClass.Physical, power, 100, 10, 0, 0);

    private static BattleCreature Creature(string slug = "a") =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [Normal],
            new Stats(100, 100, 100, 100, 100, 100), [Tackle()]);

    [Fact]
    public void BattleItem_HealsAndConsumesStock()
    {
        BattleCreature enemy = Creature("enemy");
        enemy.TakeDamage(80);
        var battle = new BattleController(Creature("player"), enemy, Chart(), new Rng(1));
        battle.SetBattleItemStock(BattleSide.Enemy, Potion, 1);

        var events = battle.ResolveTurn(new UseMove(0), new UseBattleItem(Potion, 0, 50));

        Assert.True(enemy.CurrentHp < 70);
        Assert.Contains(events, e => e is BattleItemUsed { Side: BattleSide.Enemy, Item: var item } && item == Potion);
        Assert.Contains(events, e => e is Healed { Side: BattleSide.Enemy, Amount: 50 });
        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(new UseMove(0), new UseBattleItem(Potion, 0, 50)));
    }

    [Fact]
    public void BattleItem_RejectsFullHpTarget()
    {
        var battle = new BattleController(Creature("player"), Creature("enemy"), Chart(), new Rng(1));
        battle.SetBattleItemStock(BattleSide.Enemy, Potion, 1);

        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(new UseMove(0), new UseBattleItem(Potion, 0, 50)));
    }

    [Fact]
    public void BattleItem_CanHealABenchedPartyMember()
    {
        var active = Creature("active");
        var reserve = Creature("reserve");
        reserve.TakeDamage(60); // 40/100 on the bench
        var battle = new BattleController([Creature("player")], [active, reserve], Chart(), new Rng(1));
        battle.SetBattleItemStock(BattleSide.Enemy, Potion, 1);

        var events = battle.ResolveTurn(new UseMove(0), new UseBattleItem(Potion, 1, 50));

        Assert.Equal(90, reserve.CurrentHp);                            // 40 + 50
        Assert.True(ReferenceEquals(battle.Active(BattleSide.Enemy), active)); // healed without switching in
        Assert.DoesNotContain(events, e => e is SwitchedIn { Side: BattleSide.Enemy });
        Assert.Contains(events, e => e is Healed { Side: BattleSide.Enemy, Amount: 50 });
    }

    [Fact]
    public void BattleItem_HealIsCappedToMissingHp()
    {
        var enemy = Creature("enemy");
        enemy.TakeDamage(20); // only 20 missing, item would heal 50
        var battle = new BattleController(Creature("player"), enemy, Chart(), new Rng(1));
        battle.SetBattleItemStock(BattleSide.Enemy, Potion, 1);

        var events = battle.ResolveTurn(new UseMove(0), new UseBattleItem(Potion, 0, 50));

        Assert.Contains(events, e => e is Healed { Side: BattleSide.Enemy, Amount: 20 }); // no overheal
    }
}
