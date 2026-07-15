namespace Cgm.Core.Battle;

/// <summary>One move action's shared resolution state.</summary>
internal sealed class BattleActionContext(BattleMove move, BattleCreature source, BattleSlot sourceSlot, int traceAction)
{
    private readonly List<BattleTargetContext> _targets = [];

    public BattleMove Move { get; } = move;
    public BattleCreature Source { get; } = source;
    public BattleSlot SourceSlot { get; } = sourceSlot;
    public int TraceAction { get; } = traceAction;
    public BattleSide SourceSide => SourceSlot.Side;
    public IReadOnlyList<BattleTargetContext> Targets => _targets;
    public int TotalDamage { get; private set; }
    public bool Failed { get; private set; }

    public BattleTargetContext AddTarget(BattleCreature target, BattleSlot targetSlot)
    {
        var context = new BattleTargetContext(target, targetSlot);
        _targets.Add(context);
        return context;
    }

    internal void AddDamage(int amount) => TotalDamage += amount;
    internal void MarkFailed() => Failed = true;
}

/// <summary>One materialized target's state within an action.</summary>
internal sealed class BattleTargetContext(BattleCreature target, BattleSlot targetSlot)
{
    public BattleCreature Target { get; } = target;
    public BattleSlot TargetSlot { get; } = targetSlot;
    public BattleSide TargetSide => TargetSlot.Side;
    public int TotalDamage { get; private set; }

    internal void AddDamage(BattleActionContext action, int amount)
    {
        TotalDamage += amount;
        action.AddDamage(amount);
    }
}

/// <summary>Per-effect view of one action and one ordered target.</summary>
internal readonly record struct EffectContext(
    BattleActionContext Action,
    BattleTargetContext? TargetContext,
    BattleSide? ScopedTargetSide = null)
{
    public static EffectContext ForScopedAction(BattleActionContext action, BattleSide? scopedTargetSide) =>
        new(action, null, scopedTargetSide);
    public BattleMove Move => Action.Move;
    public BattleCreature Source => Action.Source;
    public BattleSlot SourceSlot => Action.SourceSlot;
    public BattleSide SourceSide => Action.SourceSide;
    public BattleCreature Target => TargetContext?.Target
        ?? throw new InvalidOperationException("This effect scope has no creature target.");
    public BattleSlot TargetSlot => TargetContext?.TargetSlot
        ?? throw new InvalidOperationException("This effect scope has no creature target.");
    public int TraceAction => Action.TraceAction;
    public BattleSide TargetSide => TargetContext?.TargetSide ?? ScopedTargetSide
        ?? throw new InvalidOperationException("This effect scope has no target side.");
    public int DamageDealt => TargetContext?.TotalDamage ?? 0;
    public int ActionDamageDealt => Action.TotalDamage;
}
