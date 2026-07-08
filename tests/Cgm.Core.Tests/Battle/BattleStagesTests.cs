using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleStagesTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleMove Phys() =>
        new(EntityId.Parse("move:tackle"), Normal, DamageClass.Physical, 60, 100, 25, 0, 0);

    private static BattleCreature Creature(int hp = 500) =>
        new(EntityId.Parse("species:x"), "X", 50, [Normal], new Stats(hp, 100, 100, 100, 100, 100), [Phys()]);

    // Resolves one turn (fixed seed) and returns the damage the enemy took.
    private static int EnemyDamage(BattleController b) =>
        b.ResolveTurn(new UseMove(0), new UseMove(0)).OfType<DamageDealt>().First(d => d.Target == BattleSide.Enemy).Amount;

    [Fact]
    public void RaisedAttack_IncreasesDamage()
    {
        var boosted = Creature();
        boosted.ChangeStage(StatKind.Atk, 2); // +2 → ×2.0
        int withBoost = EnemyDamage(new BattleController(boosted, Creature(), Chart(), new Rng(1)));
        int baseline = EnemyDamage(new BattleController(Creature(), Creature(), Chart(), new Rng(1)));
        Assert.True(withBoost > baseline);
    }

    [Fact]
    public void RaisedDefense_ReducesDamage()
    {
        var defender = Creature();
        defender.ChangeStage(StatKind.Def, 2);
        int vsBoosted = EnemyDamage(new BattleController(Creature(), defender, Chart(), new Rng(1)));
        int baseline = EnemyDamage(new BattleController(Creature(), Creature(), Chart(), new Rng(1)));
        Assert.True(vsBoosted < baseline);
    }

    [Fact]
    public void ChangeStage_ClampsAndReads()
    {
        var c = Creature();
        c.ChangeStage(StatKind.Spe, 10);
        Assert.Equal(6, c.Stage(StatKind.Spe)); // clamped to +6
        c.ResetStages();
        Assert.Equal(0, c.Stage(StatKind.Spe));
    }

    [Fact]
    public void HpStage_IsInvalid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Creature().Stage(StatKind.Hp));
    }

    [Fact]
    public void NeutralStages_MatchUnmodifiedDamage()
    {
        // Two default creatures → stage application at 0 must not change the result.
        int a = EnemyDamage(new BattleController(Creature(), Creature(), Chart(), new Rng(5)));
        int b = EnemyDamage(new BattleController(Creature(), Creature(), Chart(), new Rng(5)));
        Assert.Equal(a, b);
        Assert.True(a > 0);
    }
}
