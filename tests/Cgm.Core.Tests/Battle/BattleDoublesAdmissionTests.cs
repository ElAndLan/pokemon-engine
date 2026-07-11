using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleDoublesAdmissionTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Potion = EntityId.Parse("item:potion");

    [Fact]
    public void Construction_RequiresTwoUniqueLivingAssignmentsPerSide()
    {
        BattleCreature fainted = Creature("fainted");
        fainted.TakeDamage(fainted.MaxHp);

        Assert.Throws<ArgumentException>(() => Battle([0, 0], [0, 1]));
        Assert.Throws<ArgumentException>(() => Battle([0], [0, 1]));
        Assert.Throws<ArgumentException>(() => new BattleController(
            [Creature("p0"), fainted], [Creature("e0"), Creature("e1")], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), new FakeRng()));
    }

    [Fact]
    public void SideOnlyActiveAndActionApis_RejectDoubles()
    {
        BattleController battle = Battle([0, 1], [0, 1]);

        Assert.Throws<InvalidOperationException>(() => battle.Active(BattleSide.Player));
        Assert.Throws<InvalidOperationException>(() => battle.CanSubmitAction(BattleSide.Player, new Pass()));
        Assert.Throws<InvalidOperationException>(() => battle.ResolveTurn(new Pass(), new Pass()));
    }

    [Fact]
    public void AdmissionRejectsCollectiveConflictsWithoutMutationOrRng()
    {
        BattleController battle = Battle([0, 1], [0, 1]);
        battle.SetBattleItemStock(BattleSide.Player, Potion, 1);
        battle.Party(BattleSide.Player)[0].TakeDamage(30);
        battle.Party(BattleSide.Player)[1].TakeDamage(30);
        int hp0 = battle.Party(BattleSide.Player)[0].CurrentHp;
        int hp1 = battle.Party(BattleSide.Player)[1].CurrentHp;

        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(Actions(
            new(new(BattleSide.Player, 0), new UseBattleItem(Potion, 0, 20)),
            new(new(BattleSide.Player, 1), new UseBattleItem(Potion, 1, 20)),
            new(new(BattleSide.Enemy, 0), new Pass()),
            new(new(BattleSide.Enemy, 1), new Pass()))));

        Assert.Equal(hp0, battle.Party(BattleSide.Player)[0].CurrentHp);
        Assert.Equal(hp1, battle.Party(BattleSide.Player)[1].CurrentHp);
        Assert.Empty(battle.Log);
        Assert.Equal(0, battle.Turn);
    }

    [Fact]
    public void AdmissionRejectsDuplicateReserveAndDoublesCapture()
    {
        BattleController battle = Battle([0, 1], [0, 1], isWild: true);

        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(Actions(
            new(new(BattleSide.Player, 0), new Switch(2)),
            new(new(BattleSide.Player, 1), new Switch(2)),
            new(new(BattleSide.Enemy, 0), new Pass()),
            new(new(BattleSide.Enemy, 1), new Pass()))));
        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(Actions(
            new(new(BattleSide.Player, 0), new ThrowBall(1, 1)),
            new(new(BattleSide.Player, 1), new Pass()),
            new(new(BattleSide.Enemy, 0), new Pass()),
            new(new(BattleSide.Enemy, 1), new Pass()))));

        Assert.Equal(0, battle.Turn);
        Assert.Empty(battle.Log);
    }

    [Fact]
    public void SwitchesResolveBySpeedAndEmitExactSlots()
    {
        BattleController battle = Battle([0, 1], [0, 1], playerSpeeds: [10, 90]);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(
            new(new(BattleSide.Player, 0), new Switch(2)),
            new(new(BattleSide.Player, 1), new Switch(3)),
            new(new(BattleSide.Enemy, 0), new Pass()),
            new(new(BattleSide.Enemy, 1), new Pass())));

        SwitchedIn[] switches = events.OfType<SwitchedIn>().ToArray();
        Assert.Equal([new BattleSlot(BattleSide.Player, 1), new BattleSlot(BattleSide.Player, 0)], switches.Select(item => item.Slot));
        Assert.Equal(2, battle.ActiveIndex(new BattleSlot(BattleSide.Player, 0)));
        Assert.Equal(3, battle.ActiveIndex(new BattleSlot(BattleSide.Player, 1)));
    }

    [Fact]
    public void SlotActionApiAcceptsOccupiedSlotAndRejectsInvalidMove()
    {
        BattleController battle = Battle([0, 1], [0, 1]);

        Assert.True(battle.CanSubmitAction(new BattleSlot(BattleSide.Player, 1), new UseMove(0)));
        Assert.False(battle.CanSubmitAction(new BattleSlot(BattleSide.Player, 1), new UseMove(1)));
    }

    [Fact]
    public void SwitchPhasePrecedesItemPhaseAndItemTieUsesTheSharedShuffle()
    {
        BattleController battle = Battle([0, 1], [0, 1], rngInts: [0]);
        battle.Party(BattleSide.Player)[1].TakeDamage(40);
        battle.Party(BattleSide.Enemy)[0].TakeDamage(40);
        battle.SetBattleItemStock(BattleSide.Player, Potion, 1);
        battle.SetBattleItemStock(BattleSide.Enemy, Potion, 1);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(Actions(
            new(new(BattleSide.Player, 0), new Switch(2)),
            new(new(BattleSide.Player, 1), new UseBattleItem(Potion, 1, 20)),
            new(new(BattleSide.Enemy, 0), new UseBattleItem(Potion, 0, 20)),
            new(new(BattleSide.Enemy, 1), new Pass())));

        Assert.True(Array.FindIndex(events.ToArray(), item => item is SwitchedIn)
            < Array.FindIndex(events.ToArray(), item => item is BattleItemUsed));
        Assert.Equal(20, battle.Party(BattleSide.Player)[1].CurrentHp - 60);
        Assert.Equal(20, battle.Party(BattleSide.Enemy)[0].CurrentHp - 60);
    }

    private static BattleController Battle(int[] playerActive, int[] enemyActive, int[]? playerSpeeds = null,
        bool isWild = false, int[]? rngInts = null) =>
        new([Creature("p0", playerSpeeds?[0] ?? 50), Creature("p1", playerSpeeds?[1] ?? 50), Creature("p2"), Creature("p3")],
            [Creature("e0"), Creature("e1"), Creature("e2"), Creature("e3")], BattleTopology.Doubles,
            playerActive, enemyActive, Chart(), new FakeRng(rngInts), isWild);

    private static BattleTurnActions Actions(params BattleActionSubmission[] submissions) =>
        new(BattleTopology.Doubles, submissions);

    private static BattleCreature Creature(string slug, int speed = 50) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [Normal], new Stats(100, 100, 100, 100, 100, speed),
            [new BattleMove(EntityId.Parse("move:wait"), Normal, DamageClass.Status, null, null, 10, 0, 0)]);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);
}
