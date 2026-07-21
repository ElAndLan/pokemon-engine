using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleTypeOverlayCleanupControllerTests
{
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Grass = EntityId.Parse("type:grass");

    private static TypeChart Chart() => new([new TypeDef { Id = Fire }, new TypeDef { Id = Grass }]);

    private static BattleCreature Creature(EntityId type, int hp, int speed, params BattleMove[] moves) =>
        new(EntityId.Parse("species:test"), "Test", 50, [type],
            new Stats(hp, 100, 100, 100, 100, speed), moves);

    private static BattleMove OverlayMove() => new(EntityId.Parse("move:overlay"), Fire, DamageClass.Status,
        null, 100, 10, 0, 0, secondaryEffects:
        [
            new TypeMutationEffect(BattleTypeOperation.Replace, BattleTypeSubject.User,
                BattleTypeSource.Fixed, Grass),
        ]);

    private static BattleEffectiveValues Values(BattleCreature creature) =>
        PhysicalMetricFormulas.BaseEffectiveValues(creature);

    [Fact]
    public void SwitchCleanupRevertsCreatureTypesAndMoveTypeRulesForOutgoingOwners()
    {
        BattleCreature player = Creature(Fire, 200, 100, OverlayMove(), OverlayMove());
        BattleCreature playerReserve = Creature(Fire, 200, 50, OverlayMove());
        BattleCreature enemy = Creature(Fire, 200, 10, OverlayMove(), OverlayMove());
        BattleCreature enemyReserve = Creature(Fire, 200, 5, OverlayMove());
        var battle = new BattleController([player, playerReserve], [enemy, enemyReserve], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new Pass());
        battle.Overlays.Apply(new BattleOverlayApplication(
            new BattleOverlayOwner(BattleSide.Enemy, 0, new BattleSlot(BattleSide.Enemy, 0)),
            new BattleOverlaySource(new BattleSlot(BattleSide.Player, 0), 0),
            BattleOverlayLayer.Additive, new MoveTypeRuleOverlay("controller_cleanup", new(Grass)),
            battle.Turn, 0, Cleanup: BattleOverlayCleanup.Switch | BattleOverlayCleanup.Faint
                | BattleOverlayCleanup.BattleEnd));
        Assert.Equal([Grass], battle.Overlays.Resolve(
            new BattleOverlayOwner(BattleSide.Player, 0, new BattleSlot(BattleSide.Player, 0)), Values(player)).Values.CreatureTypes);
        Assert.Single(battle.Overlays.Resolve(
            new BattleOverlayOwner(BattleSide.Enemy, 0, new BattleSlot(BattleSide.Enemy, 0)), Values(enemy)).Values.MoveTypeRules);

        battle.ResolveTurn(new Switch(1), new Pass());
        Assert.Equal([Fire], battle.Overlays.Resolve(
            new BattleOverlayOwner(BattleSide.Player, 0, new BattleSlot(BattleSide.Player, 0)), Values(player)).Values.CreatureTypes);

        battle.ResolveTurn(new Pass(), new Switch(1));
        Assert.Empty(battle.Overlays.Resolve(
            new BattleOverlayOwner(BattleSide.Enemy, 0, new BattleSlot(BattleSide.Enemy, 0)), Values(enemy)).Values.MoveTypeRules);
    }

    [Fact]
    public void FaintCleanupRevertsTypeOverlaysBeforeReplacement()
    {
        BattleCreature player = Creature(Fire, 40, 100, OverlayMove());
        BattleCreature reserve = Creature(Fire, 200, 50, OverlayMove());
        BattleMove knockout = new(EntityId.Parse("move:knockout"), Fire, DamageClass.Special, 500, 100, 10, 0, 0);
        BattleCreature enemy = Creature(Fire, 200, 10, knockout);
        var battle = new BattleController([player, reserve], [enemy], Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new Pass());
        battle.ResolveTurn(new Pass(), new UseMove(0));

        Assert.True(player.IsFainted);
        Assert.Equal([Fire], battle.Overlays.Resolve(
            new BattleOverlayOwner(BattleSide.Player, 0, new BattleSlot(BattleSide.Player, 0)), Values(player)).Values.CreatureTypes);
    }

    [Fact]
    public void BattleEndCleanupRemovesAllTypeOverlays()
    {
        BattleCreature player = Creature(Fire, 40, 100, OverlayMove());
        BattleMove knockout = new(EntityId.Parse("move:knockout"), Fire, DamageClass.Special, 500, 100, 10, 0, 0);
        BattleCreature enemy = Creature(Fire, 200, 10, knockout);
        var battle = new BattleController(player, enemy, Chart(), new Rng(1));

        battle.ResolveTurn(new UseMove(0), new Pass());
        battle.ResolveTurn(new Pass(), new UseMove(0));

        Assert.NotNull(battle.Outcome);
        Assert.Empty(battle.Overlays.Snapshot());
    }
}
