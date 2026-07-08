using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleAilmentTests
{
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Water = EntityId.Parse("type:water");

    private static TypeChart Chart() => new([new TypeDef { Id = Fire }, new TypeDef { Id = Water }]);

    private static BattleMove Burner(int chance) =>
        new(EntityId.Parse("move:ember"), Fire, DamageClass.Special, 40, 100, 25, 0, 0,
            ailment: PersistentStatus.Burn, ailmentChance: chance);

    private static BattleMove Plain() =>
        new(EntityId.Parse("move:m"), Water, DamageClass.Special, 1, 100, 25, 0, 0);

    private static BattleCreature Creature(EntityId type, int hp, params BattleMove[] moves) =>
        new(EntityId.Parse("species:x"), "X", 50, [type], new Stats(hp, 100, 100, 100, 100, 100), moves);

    private static BattleController Battle(BattleCreature p, BattleCreature e, int seed = 1) =>
        new(p, e, Chart(), new Rng(seed));

    [Fact]
    public void GuaranteedAilment_BurnsTarget()
    {
        var player = Creature(Water, 200, Burner(100));
        var enemy = Creature(Water, 200, Plain());
        var events = Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.Equal(PersistentStatus.Burn, enemy.Status);
        Assert.Contains(events, e => e is StatusApplied { Side: BattleSide.Enemy, Status: PersistentStatus.Burn });
    }

    [Fact]
    public void ZeroChance_NoAilment()
    {
        var player = Creature(Water, 200, Burner(0));
        var enemy = Creature(Water, 200, Plain());
        Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Null(enemy.Status);
    }

    [Fact]
    public void TypeImmuneTarget_NotBurned()
    {
        var player = Creature(Water, 200, Burner(100));
        var enemy = Creature(Fire, 200, Plain()); // fire-type immune to burn
        Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Null(enemy.Status);
    }

    [Fact]
    public void AlreadyStatused_NotReapplied()
    {
        var player = Creature(Water, 200, Burner(100));
        var enemy = Creature(Water, 200, Plain());
        enemy.SetStatus(PersistentStatus.Poison);
        Battle(player, enemy).ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal(PersistentStatus.Poison, enemy.Status); // unchanged
    }

    [Fact]
    public void BurnedAttacker_DealsLessPhysicalDamage()
    {
        BattleMove Phys() => new(EntityId.Parse("move:tackle"), Water, DamageClass.Physical, 60, 100, 25, 0, 0);

        var burned = Creature(Water, 200, Phys());
        burned.SetStatus(PersistentStatus.Burn);
        var target1 = Creature(Water, 500, Plain());
        var log1 = Battle(burned, target1).ResolveTurn(new UseMove(0), new UseMove(0));
        int burnedDmg = log1.OfType<DamageDealt>().First(d => d.Target == BattleSide.Enemy).Amount;

        var healthy = Creature(Water, 200, Phys());
        var target2 = Creature(Water, 500, Plain());
        var log2 = Battle(healthy, target2).ResolveTurn(new UseMove(0), new UseMove(0));
        int healthyDmg = log2.OfType<DamageDealt>().First(d => d.Target == BattleSide.Enemy).Amount;

        Assert.True(burnedDmg < healthyDmg);
    }

    [Fact]
    public void AilmentInfliction_IsDeterministic()
    {
        static PersistentStatus? Run()
        {
            var p = new BattleController(
                new BattleCreature(EntityId.Parse("species:p"), "P", 50, [EntityId.Parse("type:water")], new Stats(200, 100, 100, 100, 100, 100), [
                    new BattleMove(EntityId.Parse("move:e"), EntityId.Parse("type:fire"), DamageClass.Special, 40, 100, 25, 0, 0, PersistentStatus.Burn, 50)]),
                new BattleCreature(EntityId.Parse("species:e"), "E", 50, [EntityId.Parse("type:water")], new Stats(200, 100, 100, 100, 100, 100), [
                    new BattleMove(EntityId.Parse("move:m"), EntityId.Parse("type:water"), DamageClass.Special, 1, 100, 25, 0, 0)]),
                new TypeChart([new TypeDef { Id = EntityId.Parse("type:fire") }, new TypeDef { Id = EntityId.Parse("type:water") }]),
                new Rng(99));
            p.ResolveTurn(new UseMove(0), new UseMove(0));
            return p.Active(BattleSide.Enemy).Status;
        }
        Assert.Equal(Run(), Run());
    }
}
