using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleTargetSelectionTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 25, 0, 0);

    private static BattleCreature Creature(int hp, int speed, params BattleMove[] moves) =>
        new(EntityId.Parse("species:target_helper"), "Target Helper", 50, [Normal],
            new Stats(hp, 100, 100, 100, 100, speed), moves);

    [Fact]
    public void UserTargetDamage_HitsUser()
    {
        var selfHit = new BattleMove(EntityId.Parse("move:self_hit"), Normal, DamageClass.Physical, 60, 100, 25, 0, 0,
            target: MoveTarget.User);
        var player = Creature(200, 100, selfHit);
        var enemy = Creature(200, 1, Inert());

        new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(player.CurrentHp < 200);
        Assert.Equal(200, enemy.CurrentHp);
    }

    [Fact]
    public void AllOpponentsDamageLower_UsesActiveOpponentInSingles()
    {
        var spreadDrop = new BattleMove(EntityId.Parse("move:spread_drop"), Normal, DamageClass.Physical, 40, 100, 25, 0, 0,
            stageEffect: new StageEffect(StatKind.Spe, -1, OnSelf: false, Chance: 100),
            target: MoveTarget.AllOpponents);
        var player = Creature(200, 100, spreadDrop);
        var enemy = Creature(200, 1, Inert());

        IReadOnlyList<BattleEvent> events =
            new BattleController(player, enemy, Chart(), new Rng(1)).ResolveTurn(new UseMove(0), new UseMove(0));

        Assert.True(enemy.CurrentHp < 200);
        Assert.Equal(-1, enemy.Stage(StatKind.Spe));
        Assert.Contains(events, e => e is StatStageChanged { Side: BattleSide.Enemy, Stat: StatKind.Spe, Delta: -1 });
    }
}
