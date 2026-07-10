using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleDamageStatOverrideTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 25, 0, 0);

    private static BattleCreature Creature(string slug, Stats stats, params BattleMove[] moves) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [Normal], stats, moves);

    private static int DamageFrom(BattleMove move, BattleCreature player, BattleCreature enemy)
    {
        var events = new BattleController(player, enemy, Chart(),
            new FakeRng(ints: [0, 100], doubles: [0.99])).ResolveTurn(new UseMove(0), new UseMove(0));
        return events.OfType<DamageDealt>().Single(e => e.Target == BattleSide.Enemy).Amount;
    }

    [Fact]
    public void OffensiveStatOverride_UsesNamedUserStat()
    {
        var ordinary = new BattleMove(EntityId.Parse("move:plain_strike"), Normal, DamageClass.Physical, 80, 100, 25, 0, 0);
        var guarded = new BattleMove(EntityId.Parse("move:guarded_strike"), Normal, DamageClass.Physical, 80, 100, 25, 0, 0,
            offensiveStatOverride: StatKind.Def);

        int ordinaryDamage = DamageFrom(ordinary,
            Creature("a", new Stats(200, Atk: 30, Def: 160, Spa: 30, Spd: 30, Spe: 100), ordinary),
            Creature("b", new Stats(300, 100, 100, 100, 100, 1), Inert()));
        int overrideDamage = DamageFrom(guarded,
            Creature("a", new Stats(200, Atk: 30, Def: 160, Spa: 30, Spd: 30, Spe: 100), guarded),
            Creature("b", new Stats(300, 100, 100, 100, 100, 1), Inert()));

        Assert.True(overrideDamage > ordinaryDamage);
    }

    [Fact]
    public void DefensiveStatOverride_UsesNamedTargetStat()
    {
        var mindStrike = new BattleMove(EntityId.Parse("move:mind_strike"), Normal, DamageClass.Special, 80, 100, 25, 0, 0,
            defensiveStatOverride: StatKind.Def);
        var player = Creature("a", new Stats(200, 100, 100, 160, 100, 100), mindStrike);
        var enemy = Creature("b", new Stats(300, 100, Def: 40, Spa: 100, Spd: 160, Spe: 1), Inert());

        int damage = DamageFrom(mindStrike, player, enemy);

        Assert.True(damage > 50);
    }

    [Fact]
    public void OffensiveStatOverride_UsesThatStatsStage()
    {
        var guarded = new BattleMove(EntityId.Parse("move:guarded_strike"), Normal, DamageClass.Physical, 80, 100, 25, 0, 0,
            offensiveStatOverride: StatKind.Def);
        var boosted = Creature("a", new Stats(200, Atk: 30, Def: 100, Spa: 30, Spd: 30, Spe: 100), guarded);
        boosted.ChangeStage(StatKind.Def, 2);

        int normalDamage = DamageFrom(guarded,
            Creature("a", new Stats(200, Atk: 30, Def: 100, Spa: 30, Spd: 30, Spe: 100), guarded),
            Creature("b", new Stats(300, 100, 100, 100, 100, 1), Inert()));
        int boostedDamage = DamageFrom(guarded, boosted,
            Creature("b", new Stats(300, 100, 100, 100, 100, 1), Inert()));

        Assert.True(boostedDamage > normalDamage);
    }
}
