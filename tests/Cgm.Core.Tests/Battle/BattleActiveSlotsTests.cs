using Cgm.Core.Battle;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleActiveSlotsTests
{
    [Fact]
    public void Assign_MapsEachDoublesSlotToItsPartyMember()
    {
        var active = new BattleActiveSlots(BattleTopology.Doubles);
        BattleSlot playerLeft = new(BattleSide.Player, 0);
        BattleSlot playerRight = new(BattleSide.Player, 1);
        BattleSlot enemyLeft = new(BattleSide.Enemy, 0);
        BattleSlot enemyRight = new(BattleSide.Enemy, 1);

        active.Assign(playerLeft, 2);
        active.Assign(playerRight, 4);
        active.Assign(enemyLeft, 1);
        active.Assign(enemyRight, 3);

        Assert.Equal(2, active.PartyIndex(playerLeft));
        Assert.Equal(4, active.PartyIndex(playerRight));
        Assert.Equal(1, active.PartyIndex(enemyLeft));
        Assert.Equal(3, active.PartyIndex(enemyRight));
    }

    [Fact]
    public void Assign_RejectsDuplicatePartyMemberOnOneSide()
    {
        var active = new BattleActiveSlots(BattleTopology.Doubles);
        active.Assign(new BattleSlot(BattleSide.Player, 0), 1);

        Assert.Throws<ArgumentException>(() =>
            active.Assign(new BattleSlot(BattleSide.Player, 1), 1));
    }

    [Fact]
    public void PartyIndex_RejectsUnassignedOrOutOfTopologySlots()
    {
        var active = new BattleActiveSlots(BattleTopology.Singles);

        Assert.Throws<InvalidOperationException>(() => active.PartyIndex(new BattleSlot(BattleSide.Player, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => active.Assign(new BattleSlot(BattleSide.Player, 1), 0));
    }
}
