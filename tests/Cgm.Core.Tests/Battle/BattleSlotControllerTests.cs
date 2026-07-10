using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleSlotControllerTests
{
    [Fact]
    public void ActiveSlot_ResolvesTheSinglesPartyAssignment()
    {
        EntityId normal = EntityId.Parse("type:normal");
        BattleMove move = new(EntityId.Parse("move:test"), normal, DamageClass.Status, null, null, 10, 0, 0);
        BattleCreature player = new(EntityId.Parse("species:player"), "Player", 50, [normal], new Stats(100, 100, 100, 100, 100, 100), [move]);
        BattleCreature enemy = new(EntityId.Parse("species:enemy"), "Enemy", 50, [normal], new Stats(100, 100, 100, 100, 100, 100), [move]);
        var battle = new BattleController(player, enemy, new TypeChart([new TypeDef { Id = normal }]), new Rng(1));

        Assert.Equal(BattleTopology.Singles, battle.Topology);
        Assert.Same(player, battle.Active(new BattleSlot(BattleSide.Player, 0)));
        Assert.Equal(0, battle.ActiveIndex(new BattleSlot(BattleSide.Enemy, 0)));
    }
}
