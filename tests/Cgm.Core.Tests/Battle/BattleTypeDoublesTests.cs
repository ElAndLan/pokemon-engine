using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleTypeDoublesTests
{
    private static readonly EntityId Fire = EntityId.Parse("type:fire");
    private static readonly EntityId Grass = EntityId.Parse("type:grass");

    [Fact]
    public void EntireFieldOverrideAffectsEveryActiveOwnerAndLaterSameTurnDamage()
    {
        BattleMove fieldOverride = new(EntityId.Parse("move:field_override"), Fire, DamageClass.Status,
            null, null, 10, 0, 0, target: MoveTarget.EntireField,
            secondaryEffects: [new MoveTypeOverrideEffect(BattleTypeSubject.AllActive, Grass, null, 1)]);
        BattleMove attack = new(EntityId.Parse("move:attack"), Fire, DamageClass.Special,
            40, 100, 10, 0, 0);
        BattleCreature source = Creature("source", 200, fieldOverride);
        BattleCreature ally = Creature("ally", 100, attack);
        BattleCreature target = Creature("target", 50, Wait("target_wait"));
        BattleCreature other = Creature("other", 25, Wait("other_wait"));
        var battle = new BattleController([source, ally], [target, other], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), new Rng(1));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new BattleSlot(BattleSide.Player, 0), new UseMove(0)),
            new BattleActionSubmission(new BattleSlot(BattleSide.Player, 1), new UseMove(0),
                new ActiveSlotSelection(new BattleSlot(BattleSide.Enemy, 0))),
            new BattleActionSubmission(new BattleSlot(BattleSide.Enemy, 0), new Pass()),
            new BattleActionSubmission(new BattleSlot(BattleSide.Enemy, 1), new Pass()),
        ]));

        Assert.Equal(
            [(BattleSide.Player, 0), (BattleSide.Player, 1), (BattleSide.Enemy, 0), (BattleSide.Enemy, 1)],
            events.OfType<MoveTypeOverrideApplied>().Select(item => (item.Side, item.PartyIndex)).ToArray());
        Assert.Equal(Grass, Assert.Single(battle.DamageQueryTrace).Result.Identity.EffectiveType);
        Assert.Empty(battle.Overlays.Snapshot());
        Assert.Equal(Fire, attack.Type);
    }

    private static BattleCreature Creature(string slug, int speed, BattleMove move) => new(
        EntityId.Parse($"species:{slug}"), slug, 50, [Fire], new Stats(300, 100, 100, 100, 100, speed), [move]);

    private static BattleMove Wait(string slug) => new(EntityId.Parse($"move:{slug}"), Fire,
        DamageClass.Status, null, null, 10, 0, 0);

    private static TypeChart Chart() => new([new TypeDef { Id = Fire }, new TypeDef { Id = Grass }]);
}
