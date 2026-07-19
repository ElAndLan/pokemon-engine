using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

/// <summary>15G-3 revenge consumer (Metal Burst/Comeuppance): return a multiple of the damage taken
/// this turn, of any class, to the target.</summary>
public sealed class BattleRevengeTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    [Fact]
    public void MetalBurstReturnsOneAndAHalfTimesTheDamageTakenThisTurn()
    {
        BattleMove metalBurst = MetalBurst();
        BattleMove strike = new(EntityId.Parse("move:strike"), Normal, DamageClass.Physical, 60, 100, 10, 0, 0,
            target: MoveTarget.Selected);
        BattleCreature user = Creature("user", 1, metalBurst);      // slower -> takes the hit, then revenges
        BattleCreature enemy = Creature("enemy", 100, strike);

        var battle = new BattleController(user, enemy, Chart(), new FakeRng(ints: [0, 100, 0, 0], doubles: [0.99, 0.99]));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        int userTook = events.OfType<DamageDealt>().First(e => e.Target == BattleSide.Player).Amount;
        int enemyTook = events.OfType<DamageDealt>().First(e => e.Target == BattleSide.Enemy).Amount;
        Assert.True(userTook > 0);
        Assert.Equal(userTook * 3 / 2, enemyTook);
    }

    [Fact]
    public void MetalBurstFizzlesWhenTheUserTookNoDamage()
    {
        BattleCreature user = Creature("user", 100, MetalBurst()); // faster -> nothing to return
        BattleCreature enemy = Creature("enemy", 1, Inert());

        var battle = new BattleController(user, enemy, Chart(), new FakeRng(ints: [0, 0]));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.DoesNotContain(events, e => e is DamageDealt { Target: BattleSide.Enemy });
        Assert.Contains(events, e => e is MoveMissed);
    }

    private static BattleMove MetalBurst() =>
        new(EntityId.Parse("move:metal_burst"), Normal, DamageClass.Physical, null, 100, 10, 0, 0,
            target: MoveTarget.Selected, secondaryEffects: [new RevengeDamageEffect(new Fraction(3, 2))]);

    private static BattleCreature Creature(string slug, int spe, BattleMove move) => new(
        EntityId.Parse($"species:{slug}"), slug, 50, [Normal], new Stats(1000, 100, 100, 100, 100, spe), [move]);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 20, 0, 0);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);
}
