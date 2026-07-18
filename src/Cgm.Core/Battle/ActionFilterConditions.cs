using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum ActionFilterKind
{
    DisableMove,
    ForceMove,
    BlockStatusMoves,
    BlockLastMove,
    BlockKnownBySource,
    BlockMoveTag,
    BlockItems,
    ActionBlockChance,
}

public sealed record ActionFilterProfile(ActionFilterKind Kind, string? MoveTag = null, int BlockChance = 0);

public enum ActionLegalityReason
{
    None,
    NoPp,
    ChoiceLock,
    DisabledMove,
    ForcedMove,
    StatusMoveBlocked,
    RepeatedMoveBlocked,
    SourceKnownMoveBlocked,
    MoveTagBlocked,
    ItemBlocked,
    ActionChanceBlocked,
}

public sealed record ActionLegalityResult(bool Allowed, ActionLegalityReason Reason = ActionLegalityReason.None,
    BattleConditionId? Condition = null);

public static class ActionFilterConditions
{
    public const string MoveIndexCounter = "move_index";
    public const string HealingTag = "healing";
    public const string SoundTag = "sound";

    public static readonly BattleConditionDefinition Disable = Timed("disable", ActionFilterKind.DisableMove, 4,
        refresh: false);
    public static readonly BattleConditionDefinition Encore = Timed("encore", ActionFilterKind.ForceMove, 3,
        refresh: false);
    public static readonly BattleConditionDefinition Taunt = Timed("taunt", ActionFilterKind.BlockStatusMoves, 3);
    public static readonly BattleConditionDefinition Torment = Persistent("torment", ActionFilterKind.BlockLastMove);
    public static readonly BattleConditionDefinition Imprison = Persistent("imprison", ActionFilterKind.BlockKnownBySource,
        sourceBound: true);
    public static readonly BattleConditionDefinition HealBlock = Timed("heal_block", ActionFilterKind.BlockMoveTag, 5,
        HealingTag);
    public static readonly BattleConditionDefinition ItemLock = Timed("item_lock", ActionFilterKind.BlockItems, 5);
    public static readonly BattleConditionDefinition SoundLock = Timed("sound_lock", ActionFilterKind.BlockMoveTag, 2,
        SoundTag);
    public static readonly BattleConditionDefinition Infatuation = Persistent("infatuation",
        ActionFilterKind.ActionBlockChance, sourceBound: true, chance: 50);

    public static IReadOnlyList<BattleConditionDefinition> Definitions { get; } =
        [Disable, Encore, Taunt, Torment, Imprison, HealBlock, ItemLock, SoundLock, Infatuation];

    public static BattleConditionDefinition For(ActionFilterKind kind, string? moveTag = null) => kind switch
    {
        ActionFilterKind.DisableMove => Disable,
        ActionFilterKind.ForceMove => Encore,
        ActionFilterKind.BlockStatusMoves => Taunt,
        ActionFilterKind.BlockLastMove => Torment,
        ActionFilterKind.BlockKnownBySource => Imprison,
        ActionFilterKind.BlockItems => ItemLock,
        ActionFilterKind.ActionBlockChance => Infatuation,
        ActionFilterKind.BlockMoveTag when moveTag == HealingTag => HealBlock,
        ActionFilterKind.BlockMoveTag when moveTag == SoundTag => SoundLock,
        _ => throw new ArgumentException("Unknown action-filter row.", nameof(kind)),
    };

    private static BattleConditionDefinition Timed(string slug, ActionFilterKind kind, int duration,
        string? tag = null, bool refresh = true) => Definition(slug, kind, tag, duration, false, 0, refresh);

    private static BattleConditionDefinition Persistent(string slug, ActionFilterKind kind,
        string? tag = null, bool sourceBound = false, int chance = 0) =>
        Definition(slug, kind, tag, null, sourceBound, chance, false);

    private static BattleConditionDefinition Definition(string slug, ActionFilterKind kind, string? tag,
        int? duration, bool sourceBound, int chance, bool refresh) => new()
    {
        Id = new BattleConditionId($"volatile:{slug}"),
        Scope = BattleConditionScope.Creature,
        Hooks = kind == ActionFilterKind.ActionBlockChance
            ? [BattleConditionHook.BeforeMove]
            : kind == ActionFilterKind.BlockItems
                ? [BattleConditionHook.ActionSelection]
                : [BattleConditionHook.ActionSelection, BattleConditionHook.MoveAvailability],
        DefaultDuration = duration,
        DurationCheckpoint = duration is null ? null : BattleIntentCheckpoint.TurnEnd,
        Tags = sourceBound ? ["action_filter", "source_bound"] : ["action_filter"],
        StackingKey = slug,
        StackingPolicy = refresh ? BattleConditionStackingPolicy.Refresh : BattleConditionStackingPolicy.Reject,
        SwitchPolicy = BattleConditionSwitchPolicy.Remove,
        FaintPolicy = BattleConditionFaintPolicy.Remove,
        ActionFilter = new ActionFilterProfile(kind, tag, chance),
    };
}

public static class BattleActionLegality
{
    public static ActionLegalityResult Move(BattleCreature actor, int moveIndex, BattleSlot slot, int partyIndex,
        IReadOnlyList<BattleConditionInstance> conditions,
        Func<BattleConditionSource, BattleCreature?> sourceCreature,
        bool suppressHeldItems = false,
        bool ignorePp = false,
        bool ignoreChoice = false)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(conditions);
        ArgumentNullException.ThrowIfNull(sourceCreature);
        if (moveIndex < 0 || moveIndex >= actor.Moves.Count)
            throw new ArgumentOutOfRangeException(nameof(moveIndex));
        BattleMove move = actor.Moves[moveIndex];
        if (!ignorePp && !move.HasPp)
            return Block(ActionLegalityReason.NoPp);
        if (!ignoreChoice && !suppressHeldItems && actor.ChoiceLockedMoveIndex is { } choice && choice != moveIndex)
            return Block(ActionLegalityReason.ChoiceLock);

        BattleConditionInstance[] owned = conditions.Where(condition =>
            condition.Owner.Scope == BattleConditionScope.Creature
            && condition.Owner.Side == slot.Side && condition.Owner.PartyIndex == partyIndex
            && condition.Definition.ActionFilter is not null).ToArray();
        foreach (BattleConditionInstance condition in owned)
        {
            ActionFilterProfile filter = condition.Definition.ActionFilter!;
            bool blocked = filter.Kind switch
            {
                ActionFilterKind.DisableMove => StoredMove(condition) == moveIndex,
                ActionFilterKind.ForceMove => StoredMove(condition) != moveIndex,
                ActionFilterKind.BlockStatusMoves => move.DamageClass == DamageClass.Status,
                ActionFilterKind.BlockLastMove => actor.LastMoveUsed == move.Move,
                ActionFilterKind.BlockMoveTag => filter.MoveTag is { } tag && move.Tags.Contains(tag),
                _ => false,
            };
            if (blocked)
                return Block(Reason(filter.Kind), condition.Definition.Id);
        }

        foreach (BattleConditionInstance condition in conditions.Where(condition =>
            condition.Definition.ActionFilter?.Kind == ActionFilterKind.BlockKnownBySource
            && condition.Owner.Side != slot.Side))
        {
            BattleCreature? source = sourceCreature(condition.Source);
            if (source is not null && source.Moves.Any(known => known.Move == move.Move))
                return Block(ActionLegalityReason.SourceKnownMoveBlocked, condition.Definition.Id);
        }
        return new ActionLegalityResult(true);
    }

    public static ActionLegalityResult Item(BattleSlot slot, int partyIndex,
        IReadOnlyList<BattleConditionInstance> conditions)
    {
        BattleConditionInstance? blocked = conditions.FirstOrDefault(condition =>
            condition.Owner.Scope == BattleConditionScope.Creature
            && condition.Owner.Side == slot.Side && condition.Owner.PartyIndex == partyIndex
            && condition.Definition.ActionFilter?.Kind == ActionFilterKind.BlockItems);
        return blocked is null ? new ActionLegalityResult(true)
            : Block(ActionLegalityReason.ItemBlocked, blocked.Definition.Id);
    }

    public static IReadOnlyList<int> LegalMoves(BattleCreature actor, BattleSlot slot, int partyIndex,
        IReadOnlyList<BattleConditionInstance> conditions, Func<BattleConditionSource, BattleCreature?> sourceCreature,
        bool suppressHeldItems = false) => Enumerable.Range(0, actor.Moves.Count)
        .Where(index => Move(actor, index, slot, partyIndex, conditions, sourceCreature, suppressHeldItems).Allowed)
        .ToArray();

    private static int StoredMove(BattleConditionInstance condition) =>
        condition.Counters.TryGetValue(ActionFilterConditions.MoveIndexCounter, out int index) ? index : -1;

    private static ActionLegalityReason Reason(ActionFilterKind kind) => kind switch
    {
        ActionFilterKind.DisableMove => ActionLegalityReason.DisabledMove,
        ActionFilterKind.ForceMove => ActionLegalityReason.ForcedMove,
        ActionFilterKind.BlockStatusMoves => ActionLegalityReason.StatusMoveBlocked,
        ActionFilterKind.BlockLastMove => ActionLegalityReason.RepeatedMoveBlocked,
        ActionFilterKind.BlockMoveTag => ActionLegalityReason.MoveTagBlocked,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static ActionLegalityResult Block(ActionLegalityReason reason, BattleConditionId? condition = null) =>
        new(false, reason, condition);
}
