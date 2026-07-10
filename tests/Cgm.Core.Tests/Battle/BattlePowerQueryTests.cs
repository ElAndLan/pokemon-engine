using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattlePowerQueryTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Grass = EntityId.Parse("type:grass");

    [Fact]
    public void TargetHpThresholdPower_MultipliesPowerAtOrBelowThreshold()
    {
        Assert.Equal(130, EffectMath.TargetHpThresholdPower(65, currentHp: 50, maxHp: 100, 1, 2, 2, 1));
        Assert.Equal(65, EffectMath.TargetHpThresholdPower(65, currentHp: 51, maxHp: 100, 1, 2, 2, 1));
    }

    [Theory]
    [InlineData(150, 300, 300, 150)]
    [InlineData(150, 200, 300, 100)]
    [InlineData(150, 1, 300, 1)]
    [InlineData(150, 100, 0, 150)]
    public void HpRatioPower_ScalesPowerByHpRatio(int basePower, int currentHp, int maxHp, int expected)
    {
        Assert.Equal(expected, EffectMath.HpRatioPower(basePower, currentHp, maxHp));
    }

    [Fact]
    public void TargetHpThresholdPower_UsesModifiedPowerInDamageFormula()
    {
        var move = new BattleMove(EntityId.Parse("move:threshold_strike"), Grass, DamageClass.Special, 65, 100, 25, 0, 0,
            targetHpThresholdPower: new TargetHpThresholdPower(new Fraction(1, 2), new Fraction(2, 1)));
        var player = new BattleCreature(EntityId.Parse("species:a"), "A", 50, [Normal],
            new Stats(200, 100, 100, 100, 100, 100), [move]);
        var enemy = new BattleCreature(EntityId.Parse("species:b"), "B", 50, [Normal],
            new Stats(400, 100, 100, 100, 100, 1), [Inert()]);
        enemy.TakeDamage(200);

        new BattleController(player, enemy, Chart(), new FakeRng(ints: [0, 15], doubles: [0.99]))
            .ResolveTurn(new UseMove(0), new UseMove(0));

        int expected = DamageCalc.Compute(50, 130, 100, 100, effectiveness: 1.0, stab: 1.0, crit: false, roll: 100, burn: false);
        Assert.Equal(200 - expected, enemy.CurrentHp);
    }

    [Fact]
    public void HpRatioPower_UserSource_UsesUserHpRatioInDamageFormula()
    {
        var move = new BattleMove(EntityId.Parse("move:user_ratio_strike"), Grass, DamageClass.Special, 150, 100, 25, 0, 0,
            hpRatioPower: new HpRatioPower(HpRatioPowerSource.User));
        var player = new BattleCreature(EntityId.Parse("species:a"), "A", 50, [Normal],
            new Stats(300, 100, 100, 100, 100, 100), [move]);
        player.TakeDamage(100);
        var enemy = new BattleCreature(EntityId.Parse("species:b"), "B", 50, [Normal],
            new Stats(400, 100, 100, 100, 100, 1), [Inert()]);

        new BattleController(player, enemy, Chart(), new FakeRng(ints: [0, 15], doubles: [0.99]))
            .ResolveTurn(new UseMove(0), new UseMove(0));

        int expected = DamageCalc.Compute(50, 100, 100, 100, effectiveness: 1.0, stab: 1.0, crit: false, roll: 100, burn: false);
        Assert.Equal(400 - expected, enemy.CurrentHp);
    }

    [Fact]
    public void HpRatioPower_TargetSource_UsesTargetHpRatioInDamageFormula()
    {
        var move = new BattleMove(EntityId.Parse("move:target_ratio_strike"), Grass, DamageClass.Special, 120, 100, 25, 0, 0,
            hpRatioPower: new HpRatioPower(HpRatioPowerSource.Target));
        var player = new BattleCreature(EntityId.Parse("species:a"), "A", 50, [Normal],
            new Stats(300, 100, 100, 100, 100, 100), [move]);
        var enemy = new BattleCreature(EntityId.Parse("species:b"), "B", 50, [Normal],
            new Stats(400, 100, 100, 100, 100, 1), [Inert()]);
        enemy.TakeDamage(100);

        new BattleController(player, enemy, Chart(), new FakeRng(ints: [0, 15], doubles: [0.99]))
            .ResolveTurn(new UseMove(0), new UseMove(0));

        int expected = DamageCalc.Compute(50, 90, 100, 100, effectiveness: 1.0, stab: 1.0, crit: false, roll: 100, burn: false);
        Assert.Equal(300 - expected, enemy.CurrentHp);
    }

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 25, 0, 0);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }, new TypeDef { Id = Grass }]);
}
