using Cgm.Core.Battle;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleTurnActionsTests
{
    [Fact]
    public void Constructor_NormalizesDoublesActionsToTopologyOrder()
    {
        var actions = new BattleTurnActions(BattleTopology.Doubles,
        [
            new BattleActionSubmission(new BattleSlot(BattleSide.Enemy, 1), new UseMove(0)),
            new BattleActionSubmission(new BattleSlot(BattleSide.Player, 1), new UseMove(0)),
            new BattleActionSubmission(new BattleSlot(BattleSide.Enemy, 0), new UseMove(0)),
            new BattleActionSubmission(new BattleSlot(BattleSide.Player, 0), new UseMove(0)),
        ]);

        Assert.Equal(BattleTopology.Doubles.Slots, actions.Actions.Select(action => action.Source));
    }

    [Fact]
    public void Constructor_RejectsMissingDuplicateAndOutOfTopologySubmissions()
    {
        BattleSlot player = new(BattleSide.Player, 0);
        BattleSlot enemy = new(BattleSide.Enemy, 0);

        Assert.Throws<ArgumentException>(() => new BattleTurnActions(BattleTopology.Singles,
        [
            new BattleActionSubmission(player, new UseMove(0)),
            new BattleActionSubmission(player, new UseMove(0)),
        ]));
        Assert.Throws<ArgumentException>(() => new BattleTurnActions(BattleTopology.Singles,
        [
            new BattleActionSubmission(player, new UseMove(0)),
            new BattleActionSubmission(enemy, new UseMove(0), new BattleSlot(BattleSide.Player, 1)),
        ]));
    }

    [Fact]
    public void Order_UsesPriorityThenSpeedThenStableRngTieShuffle()
    {
        BattleActionSubmission player = new(new BattleSlot(BattleSide.Player, 0), new UseMove(0));
        BattleActionSubmission enemy = new(new BattleSlot(BattleSide.Enemy, 0), new UseMove(0));
        BattleActionSubmission playerTwo = new(new BattleSlot(BattleSide.Player, 1), new UseMove(0));

        IReadOnlyList<BattleScheduledAction> ordered = BattleTurnOrder.Order(
        [
            new BattleScheduledAction(player, 0, 80),
            new BattleScheduledAction(enemy, 1, 1),
            new BattleScheduledAction(playerTwo, 0, 80),
        ],
        new FakeRng(ints: [0]));

        Assert.Equal(enemy, ordered[0].Submission);
        Assert.Equal(player, ordered[1].Submission);
        Assert.Equal(playerTwo, ordered[2].Submission);
    }
}
