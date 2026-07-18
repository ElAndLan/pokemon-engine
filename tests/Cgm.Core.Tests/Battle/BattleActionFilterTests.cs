using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleActionFilterTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly BattleSlot Player = new(BattleSide.Player, 0);
    private static readonly BattleSlot Enemy = new(BattleSide.Enemy, 0);

    [Fact]
    public void EveryMoveFilterUsesOneLegalityMatrix()
    {
        BattleMove attack = Move("shared", DamageClass.Physical);
        BattleMove status = Move("status", DamageClass.Status);
        BattleMove heal = Move("heal", DamageClass.Status, ActionFilterConditions.HealingTag);
        BattleMove sound = Move("sound", DamageClass.Special, ActionFilterConditions.SoundTag);
        BattleCreature actor = Creature(attack, status, heal, sound);
        actor.RecordMoveUse(attack.Move);
        BattleCreature source = Creature(Move("shared", DamageClass.Physical));

        AssertBlocked(ActionFilterConditions.Disable, 0, ActionLegalityReason.DisabledMove, counters: MoveCounter(0));
        AssertBlocked(ActionFilterConditions.Encore, 1, ActionLegalityReason.ForcedMove, counters: MoveCounter(0));
        AssertBlocked(ActionFilterConditions.Taunt, 1, ActionLegalityReason.StatusMoveBlocked);
        AssertBlocked(ActionFilterConditions.Torment, 0, ActionLegalityReason.RepeatedMoveBlocked);
        AssertBlocked(ActionFilterConditions.HealBlock, 2, ActionLegalityReason.MoveTagBlocked);
        AssertBlocked(ActionFilterConditions.SoundLock, 3, ActionLegalityReason.MoveTagBlocked);

        BattleConditionInstance imprison = Instance(ActionFilterConditions.Imprison,
            new BattleConditionOwner(BattleConditionScope.Creature, BattleSide.Enemy, Enemy, 0),
            new BattleConditionSource(Enemy, 0));
        ActionLegalityResult imprisoned = BattleActionLegality.Move(actor, 0, Player, 0, [imprison],
            _ => source);
        Assert.Equal(ActionLegalityReason.SourceKnownMoveBlocked, imprisoned.Reason);

        void AssertBlocked(BattleConditionDefinition definition, int index, ActionLegalityReason reason,
            IReadOnlyDictionary<string, int>? counters = null)
        {
            BattleConditionInstance condition = Instance(definition,
                new BattleConditionOwner(BattleConditionScope.Creature, BattleSide.Player, Player, 0),
                new BattleConditionSource(Enemy, 0), counters);
            ActionLegalityResult result = BattleActionLegality.Move(actor, index, Player, 0, [condition], _ => source);
            Assert.False(result.Allowed);
            Assert.Equal(reason, result.Reason);
            Assert.True(BattleActionLegality.Move(actor, index == 0 ? 3 : 0, Player, 0, [condition], _ => source).Allowed
                || definition == ActionFilterConditions.Encore);
        }
    }

    [Fact]
    public void PpAndOverlappingFiltersHaveStablePrecedence_AndItemsShareTheService()
    {
        BattleCreature actor = Creature(Move("empty", pp: 0), Move("status", DamageClass.Status), Move("other"));
        actor.SetChoiceLock(1);
        BattleConditionInstance taunt = Owned(ActionFilterConditions.Taunt);
        BattleConditionInstance item = Owned(ActionFilterConditions.ItemLock);

        Assert.Equal(ActionLegalityReason.NoPp,
            BattleActionLegality.Move(actor, 0, Player, 0, [taunt], _ => null).Reason);
        Assert.Equal(ActionLegalityReason.StatusMoveBlocked,
            BattleActionLegality.Move(actor, 1, Player, 0, [taunt], _ => null).Reason);
        Assert.Equal(ActionLegalityReason.ChoiceLock,
            BattleActionLegality.Move(actor, 2, Player, 0, [taunt], _ => null).Reason);
        Assert.Equal(ActionLegalityReason.ItemBlocked,
            BattleActionLegality.Item(Player, 0, [item]).Reason);
    }

    [Fact]
    public void NoLegalOrdinaryMoveExposesAndResolvesRulesetFallback()
    {
        BattleCreature player = Creature(Move("status_only", DamageClass.Status));
        BattleCreature enemy = Creature(Move("wait", DamageClass.Status));
        BattleController battle = Battle(player, enemy, new Rng(3));
        battle.ApplyActionFilterCondition(Player, Enemy, ActionFilterKind.BlockStatusMoves);

        Assert.Equal(new UseFallback(), Assert.Single(battle.LegalMoveActions(Player)));
        Assert.True(battle.CanSubmitAction(Player, new UseFallback()));
        Assert.False(battle.CanSubmitAction(Player, new UseMove(0)));

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseFallback(), new Pass());
        Assert.Contains(events, item => item is MoveUsed { Move.Slug: "fallback_action" });
        Assert.True(enemy.CurrentHp < enemy.MaxHp);
        Assert.True(player.CurrentHp < player.MaxHp);
        BattleDamageQueryResult query = Assert.Single(battle.DamageQueryTrace).Result;
        Assert.Equal(new BattleQueryValue(1), query.Effectiveness.FinalValue);
        Assert.Equal(new BattleQueryValue(1), query.Stab);
    }

    [Fact]
    public void SelectionFilterVisiblyTerminatesAForcedMultiTurnLock()
    {
        BattleMove locked = new(EntityId.Parse("move:locked"), Normal, DamageClass.Physical,
            30, 100, 10, 0, 0, multiTurnLockProfile: new MultiTurnLockProfile(3, 3));
        BattleCreature player = Creature(locked);
        BattleCreature enemy = Creature(Move("wait", DamageClass.Status));
        BattleController battle = Battle(player, enemy, new Rng(8));
        battle.ResolveTurn(new UseMove(0), new Pass());
        battle.ApplyActionFilterCondition(Player, Enemy, ActionFilterKind.DisableMove);

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new Pass(), new Pass());

        Assert.Contains(events, item => item is ActionBlocked { Reason: ActionLegalityReason.DisabledMove });
        Assert.Contains(events, item => item is MultiTurnLockEnded
            { Reason: MultiTurnLockEndReason.SelectionBlocked });
        Assert.False(player.IsLocked);
    }

    [Fact]
    public void TimedFiltersRefreshExpireAndClearOnSwitch()
    {
        BattleCreature player = Creature(Move("attack"));
        BattleCreature reserve = Creature(Move("reserve"));
        BattleCreature enemy = Creature(Move("wait", DamageClass.Status));
        BattleController battle = new([player, reserve], [enemy], Chart(), new Rng(2));

        battle.ApplyActionFilterCondition(Player, Enemy, ActionFilterKind.BlockStatusMoves, 2);
        battle.ApplyActionFilterCondition(Player, Enemy, ActionFilterKind.BlockStatusMoves, 3);
        Assert.Equal(3, Assert.Single(battle.ConditionSnapshot).RemainingDuration);
        battle.ResolveTurn(new Pass(), new Pass());
        Assert.Equal(2, Assert.Single(battle.ConditionSnapshot).RemainingDuration);
        battle.ResolveTurn(new Switch(1), new Pass());
        Assert.Empty(battle.ConditionSnapshot);
        Assert.Contains(battle.Log, item => item is ConditionRemoved { Reason: BattleConditionCleanupReason.Switch });
    }

    [Fact]
    public void InfatuationBlocksAtExecutionWithoutChangingSelectionLegality()
    {
        BattleCreature player = Creature(Move("attack"));
        BattleCreature enemy = Creature(Move("wait", DamageClass.Status));
        BattleController battle = Battle(player, enemy, new FakeRng(ints: [0]));
        battle.ApplyActionFilterCondition(Player, Enemy, ActionFilterKind.ActionBlockChance);

        Assert.True(battle.CanSubmitAction(Player, new UseMove(0)));
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Contains(events, item => item is ActionBlocked
            { Reason: ActionLegalityReason.ActionChanceBlocked, Condition.Value: "volatile:infatuation" });
        Assert.Equal(10, player.Moves[0].Pp);
    }

    [Fact]
    public void SmartAiUsesTheSameFilterAndFallbackResult()
    {
        BattleCreature enemy = Creature(Move("attack"), Move("status", DamageClass.Status));
        BattleCreature player = Creature(Move("wait", DamageClass.Status));
        BattleConditionInstance taunt = Instance(ActionFilterConditions.Taunt,
            new BattleConditionOwner(BattleConditionScope.Creature, BattleSide.Enemy, Enemy, 0),
            new BattleConditionSource(Player, 0));
        SmartAiDecision oneLegal = SmartAi.ChooseAction(new SmartAiContext([enemy], 0, [player], 0,
            Chart(), new Rng(4), Conditions: [taunt]));
        Assert.Equal(new UseMove(0), oneLegal.Action);

        enemy.Moves[0].UsePp();
        while (enemy.Moves[0].HasPp) enemy.Moves[0].UsePp();
        SmartAiDecision noneLegal = SmartAi.ChooseAction(new SmartAiContext([enemy], 0, [player], 0,
            Chart(), new Rng(4), Conditions: [taunt]));
        Assert.Equal(new UseFallback(), noneLegal.Action);
    }

    private static IReadOnlyDictionary<string, int> MoveCounter(int index) =>
        new Dictionary<string, int> { [ActionFilterConditions.MoveIndexCounter] = index };

    private static BattleConditionInstance Owned(BattleConditionDefinition definition) => Instance(definition,
        new BattleConditionOwner(BattleConditionScope.Creature, BattleSide.Player, Player, 0),
        new BattleConditionSource(Enemy, 0));

    private static BattleConditionInstance Instance(BattleConditionDefinition definition, BattleConditionOwner owner,
        BattleConditionSource source, IReadOnlyDictionary<string, int>? counters = null) =>
        new(0, definition, owner, source, 0, 0, definition.DefaultDuration, definition.Tags,
            counters ?? definition.InitialCounters, 1);

    private static BattleMove Move(string slug, DamageClass damageClass = DamageClass.Physical,
        string? tag = null, int pp = 10) => new(EntityId.Parse($"move:{slug}"), Normal, damageClass,
            damageClass == DamageClass.Status ? null : 30, 100, pp, 0, 0, tags: tag is null ? [] : [tag]);

    private static BattleCreature Creature(params BattleMove[] moves) => new(EntityId.Parse("species:test"), "Test", 50,
        [Normal], new Stats(200, 100, 100, 100, 100, 100), moves);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);
    private static BattleController Battle(BattleCreature player, BattleCreature enemy, IRng rng) =>
        new(player, enemy, Chart(), rng);
}
