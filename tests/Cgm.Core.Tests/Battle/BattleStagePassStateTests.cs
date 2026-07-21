using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleStagePassStateTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly StatKind[] Slots =
        [StatKind.Atk, StatKind.Def, StatKind.Spa, StatKind.Spd, StatKind.Spe, StatKind.Accuracy, StatKind.Evasion];

    [Fact]
    public void ConsumeUsesImmutableSnapshotInFixedOrderAndOnlyOnce()
    {
        var state = new BattleStagePassState();
        BattleCreature source = Creature("source");
        BattleCreature target = Creature("target");
        BattleOverlayOwner sourceOwner = Owner(BattleSide.Player, 1, 0);
        BattleOverlayOwner targetOwner = Owner(BattleSide.Player, 2, 0);
        int[] captured = [6, -6, 5, -5, 4, -4, 3];
        int[] targetBefore = [-1, -2, -3, -4, -5, -6, 0];
        SetStages(source, captured);
        SetStages(target, targetBefore);

        Assert.Equal(BattleStagePassFailure.None, state.Capture(sourceOwner, source));
        SetStages(source, [0, 0, 0, 0, 0, 0, 0]);
        BattleStagePassResult result = state.Consume(sourceOwner with { Slot = null }, targetOwner, target);

        Assert.True(result.Succeeded);
        Assert.Equal(Slots, result.Changes.Select(change => change.Stat));
        Assert.Equal(captured, Slots.Select(target.Stage));
        Assert.False(state.HasPending(sourceOwner));
        Assert.Equal(BattleStagePassFailure.Missing,
            state.Consume(sourceOwner, targetOwner, target).Failure);
    }

    [Fact]
    public void WhitelistTransfersStagesWithoutHpStatusPpOrVolatileState()
    {
        var state = new BattleStagePassState();
        BattleCreature source = Creature("whitelist_source", EntityId.Parse("type:other"),
            EntityId.Parse("ability:source"), EntityId.Parse("item:source"));
        BattleCreature target = Creature("whitelist_target");
        source.SetStage(StatKind.Atk, 3);
        source.TakeDamage(20);
        source.SetStatus(PersistentStatus.Burn);
        source.SetConfusion(3);
        source.RaiseCrit(2);
        source.SetSeeded(true);
        source.SetTrap(4);
        source.StartLock(0, 2);
        source.SetChoiceLock(0);
        source.Moves[0].UsePp();

        state.Capture(Owner(BattleSide.Player, 0, 0), source);
        BattleStagePassResult result = state.Consume(
            Owner(BattleSide.Player, 0, 0), Owner(BattleSide.Player, 1, 0), target);

        Assert.True(result.Succeeded);
        Assert.Equal(3, target.Stage(StatKind.Atk));
        Assert.Equal(target.MaxHp, target.CurrentHp);
        Assert.Null(target.Status);
        Assert.False(target.IsConfused);
        Assert.Equal(0, target.CritStageBonus);
        Assert.False(target.Seeded);
        Assert.False(target.IsTrapped);
        Assert.False(target.IsLocked);
        Assert.Null(target.ChoiceLockedMoveIndex);
        Assert.Equal(target.Moves[0].MaxPp, target.Moves[0].Pp);
        Assert.Equal(Normal, Assert.Single(target.Types));
        Assert.Null(target.Ability);
        Assert.Null(target.HeldItem);
    }

    [Fact]
    public void FailedCancelledFaintedAndBattleEndPathsDiscardWithoutTargetMutation()
    {
        var state = new BattleStagePassState();
        BattleCreature first = Creature("first");
        BattleCreature second = Creature("second");
        BattleCreature target = Creature("prospective");
        BattleOverlayOwner firstOwner = Owner(BattleSide.Player, 0, 0);
        BattleOverlayOwner secondOwner = Owner(BattleSide.Enemy, 0, 1);
        first.SetStage(StatKind.Atk, 2);
        second.SetStage(StatKind.Atk, -3);
        state.Capture(firstOwner, first);
        state.Capture(secondOwner, second);

        Assert.Equal(BattleStagePassFailure.SameCreature,
            state.Consume(firstOwner, firstOwner with { Slot = new(BattleSide.Player, 1) }, target).Failure);
        Assert.Equal(0, target.Stage(StatKind.Atk));
        Assert.False(state.HasPending(firstOwner));
        Assert.True(state.OwnerFainted(BattleSide.Enemy, 0));
        Assert.False(state.HasPending(secondOwner));

        state.Capture(firstOwner, first);
        Assert.True(state.Discard(firstOwner));
        Assert.Equal(BattleStagePassFailure.Missing,
            state.Consume(firstOwner, Owner(BattleSide.Player, 1, 0), target).Failure);

        state.Capture(firstOwner, first);
        state.Capture(secondOwner, second);
        Assert.Equal(2, state.EndBattle());
        Assert.False(state.HasPending(firstOwner));
        Assert.False(state.HasPending(secondOwner));

        target.TakeDamage(target.MaxHp);
        state.Capture(firstOwner, first);
        Assert.Equal(BattleStagePassFailure.TargetFainted,
            state.Consume(firstOwner, Owner(BattleSide.Player, 1, 0), target).Failure);
        Assert.False(state.HasPending(firstOwner));

        first.TakeDamage(first.MaxHp);
        Assert.Equal(BattleStagePassFailure.SourceFainted, state.Capture(firstOwner, first));
        Assert.False(state.HasPending(firstOwner));
    }

    [Fact]
    public void ControllerFaintLifecycleDiscardsPendingSnapshot()
    {
        BattleCreature player = Creature("controller_player");
        BattleCreature enemy = new(EntityId.Parse("species:controller_enemy"), "enemy", 50,
            [Normal], new Stats(100, 200, 10, 10, 10, 100),
            [new BattleMove(EntityId.Parse("move:decisive_hit"), Normal,
                DamageClass.Physical, 1000, null, 10, 0, 0)]);
        var battle = new BattleController(player, enemy,
            new TypeChart([new TypeDef { Id = Normal }]), new Rng(1));
        var owner = new BattleOverlayOwner(BattleSide.Player, 0, new(BattleSide.Player, 0));
        Assert.Equal(BattleStagePassFailure.None, battle.StagePasses.Capture(owner, player));

        battle.ResolveTurn(new Pass(), new UseMove(0));

        Assert.True(player.IsFainted);
        Assert.False(battle.StagePasses.HasPending(owner));
    }

    private static BattleOverlayOwner Owner(BattleSide side, int partyIndex, int position) =>
        new(side, partyIndex, new BattleSlot(side, position));

    private static BattleCreature Creature(string slug, EntityId? type = null,
        EntityId? ability = null, EntityId? heldItem = null) => new(EntityId.Parse($"species:{slug}"), slug, 50,
        [type ?? Normal], new Stats(100, 10, 10, 10, 10, 10),
        [new BattleMove(EntityId.Parse("move:neutral"), Normal, DamageClass.Status, null, null, 10, 0, 0)],
        ability: ability, heldItem: heldItem);

    private static void SetStages(BattleCreature creature, IReadOnlyList<int> values)
    {
        for (int i = 0; i < Slots.Length; i++)
            creature.SetStage(Slots[i], values[i]);
    }
}
