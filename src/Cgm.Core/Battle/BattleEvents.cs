using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattleSide { Player, Enemy }

/// <summary>An action a side submits for a turn (UI/AI produce these; the controller applies them).
/// Switch/item/run join in later battle layers.</summary>
public abstract record BattleAction;
public sealed record UseMove(int MoveIndex) : BattleAction;
public sealed record ActivateForm(string FormId, int MoveIndex) : BattleAction;
public sealed record Switch(int PartyIndex) : BattleAction;
public sealed record ThrowBall(double BallBonus, double StatusBonus) : BattleAction;
public sealed record UseBattleItem(EntityId Item, int TargetPartyIndex, int HealAmount) : BattleAction;
public sealed record Pass : BattleAction;

/// <summary>What happened during resolution — the stream the UI renders (BATTLE_SYSTEM_SPEC).
/// UI consumes these; it never reads or mutates state to infer what happened.</summary>
public abstract record BattleEvent;
public sealed record HookDispatchFailed(int ActionSequence, BattleConditionHook Checkpoint,
    BattleHookDispatchFailureKind Reason, int Limit) : BattleEvent;
public sealed record ConditionApplied(BattleConditionId Condition, BattleConditionScope Scope,
    BattleConditionOwner Owner, long Sequence) : BattleEvent;
public sealed record ConditionApplicationRejected(BattleConditionId Condition, BattleConditionScope Scope,
    BattleConditionOwner Owner, long Sequence, BattleConditionRejectionReason Reason) : BattleEvent;
public sealed record ConditionRefreshed(BattleConditionId Condition, BattleConditionScope Scope,
    BattleConditionOwner Owner, long Sequence, int? RemainingDuration) : BattleEvent;
public sealed record ConditionReplaced(BattleConditionId Condition, BattleConditionScope Scope,
    BattleConditionOwner Owner, long ReplacedSequence, long Sequence) : BattleEvent;
public sealed record ConditionStacked(BattleConditionId Condition, BattleConditionScope Scope,
    BattleConditionOwner Owner, long Sequence, int Stacks) : BattleEvent;
public sealed record ConditionExpired(BattleConditionId Condition, BattleConditionScope Scope,
    BattleConditionOwner Owner, long Sequence) : BattleEvent;
public sealed record ConditionRemoved(BattleConditionId Condition, BattleConditionScope Scope,
    BattleConditionOwner Owner, long Sequence, BattleConditionCleanupReason Reason) : BattleEvent;
public sealed record ConditionTransferred(BattleConditionId Condition, BattleConditionScope Scope,
    BattleConditionOwner PreviousOwner, BattleConditionOwner Owner, long Sequence) : BattleEvent;
public sealed record MoveUsed(BattleSlot Slot, EntityId Move) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public MoveUsed(BattleSide side, EntityId move) : this(new BattleSlot(side, 0), move) { }
}
public sealed record MoveMissed(BattleSlot Slot, EntityId Move, BattleSlot? TargetSlot = null) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public MoveMissed(BattleSide side, EntityId move) : this(new BattleSlot(side, 0), move) { }
}
public enum MoveFailureReason { FirstActionOnly, CannotRepeat, TargetUnavailable, FormulaInputUnavailable, TerrainRequired, FieldConditionBlocked, ConditionRequirementNotMet, ConditionAlreadyActive }
public sealed record MoveFailed(BattleSlot Slot, EntityId Move, MoveFailureReason Reason) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public MoveFailed(BattleSide side, EntityId move, MoveFailureReason reason) : this(new BattleSlot(side, 0), move, reason) { }
}
public sealed record ActionSkipped(BattleSlot Slot) : BattleEvent;
public enum ActionInvalidationReason { ActorChanged, ActorFainted, MoveChanged, ResourceChanged, TargetStateChanged }
public sealed record ActionInvalidated(BattleSlot Slot, ActionInvalidationReason Reason) : BattleEvent;
public sealed record DamageDealt(BattleSlot Slot, int Amount, double Effectiveness, bool Crit) : BattleEvent
{
    public BattleSide Target => Slot.Side;
    public DamageDealt(BattleSide target, int amount, double effectiveness, bool crit)
        : this(new BattleSlot(target, 0), amount, effectiveness, crit) { }
}
public sealed record Fainted(BattleSlot Slot) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public Fainted(BattleSide side) : this(new BattleSlot(side, 0)) { }
}
public sealed record SwitchedIn(BattleSlot Slot, int PartyIndex) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public SwitchedIn(BattleSide side, int partyIndex) : this(new BattleSlot(side, 0), partyIndex) { }
}
public sealed record ReplacementRequested(BattleSlot Slot) : BattleEvent;
public sealed record PositionsSwapped(BattleSlot SourceSlot, BattleSlot TargetSlot) : BattleEvent;
public sealed record TargetRedirected(BattleSlot SourceSlot, BattleSlot OriginalTargetSlot, BattleSlot RedirectedTargetSlot) : BattleEvent;
public sealed record FormChanged(BattleSlot Slot, string? FormId) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public FormChanged(BattleSide side, string? formId) : this(new BattleSlot(side, 0), formId) { }
}
public sealed record StatusApplied(BattleSlot Slot, PersistentStatus Status) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public StatusApplied(BattleSide side, PersistentStatus status) : this(new BattleSlot(side, 0), status) { }
}
public sealed record StatusCured(BattleSlot Slot, PersistentStatus Status) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public StatusCured(BattleSide side, PersistentStatus status) : this(new BattleSlot(side, 0), status) { }
}
public sealed record StatStageChanged(BattleSlot Slot, StatKind Stat, int Delta) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public StatStageChanged(BattleSide side, StatKind stat, int delta) : this(new BattleSlot(side, 0), stat, delta) { }
}
public sealed record StatusDamage(BattleSlot Slot, int Amount) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public StatusDamage(BattleSide side, int amount) : this(new BattleSlot(side, 0), amount) { }
}
public sealed record ResidualDamage(BattleSlot Slot, int Amount) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public ResidualDamage(BattleSide side, int amount) : this(new BattleSlot(side, 0), amount) { }
}
public sealed record Healed(BattleSide Side, int Amount, BattleSlot? Slot = null) : BattleEvent
{
    public Healed(BattleSlot slot, int amount) : this(slot.Side, amount, slot) { }
}
public sealed record HpFractionDamaged(BattleSlot Slot, int Amount) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public HpFractionDamaged(BattleSide side, int amount) : this(new BattleSlot(side, 0), amount) { }
}
public sealed record HpFormulaChanged(BattleSlot Slot, int Before, int After, HpEqualizeMode Formula) : BattleEvent;
public sealed record HpCostPaid(BattleSlot Slot, int Amount) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public HpCostPaid(BattleSide side, int amount) : this(new BattleSlot(side, 0), amount) { }
}
public sealed record BattleItemUsed(BattleSlot Slot, EntityId Item, int TargetPartyIndex) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public BattleItemUsed(BattleSide side, EntityId item, int targetPartyIndex) : this(new BattleSlot(side, 0), item, targetPartyIndex) { }
}
public sealed record HeldItemConsumed(BattleSlot Slot, string Op) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public HeldItemConsumed(BattleSide side, string op) : this(new BattleSlot(side, 0), op) { }
}
public sealed record Recoiled(BattleSlot Slot, int Amount) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public Recoiled(BattleSide side, int amount) : this(new BattleSlot(side, 0), amount) { }
}
public sealed record ContactDamaged(BattleSlot Slot, int Amount) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public ContactDamaged(BattleSide side, int amount) : this(new BattleSlot(side, 0), amount) { }
}
public sealed record CritBoosted(BattleSlot Slot) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public CritBoosted(BattleSide side) : this(new BattleSlot(side, 0)) { }
}
public sealed record LeechSeeded(BattleSlot Slot) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public LeechSeeded(BattleSide side) : this(new BattleSlot(side, 0)) { }
}
public sealed record LeechSapped(BattleSlot Slot, int Amount) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public LeechSapped(BattleSide side, int amount) : this(new BattleSlot(side, 0), amount) { }
}
public sealed record EntryHazardSet(BattleSide Side, BattleConditionId Condition, int Layers,
    BattleConditionSource Source) : BattleEvent;
public sealed record EntryHazardTriggered(BattleSlot Slot, BattleConditionId Condition,
    BattleConditionSource Source, EntryHazardKind Kind, int Value) : BattleEvent;
public sealed record EntryHazardAbsorbed(BattleSlot Slot, BattleConditionId Condition,
    BattleConditionSource Source) : BattleEvent;
public sealed record WeatherChanged(Weather Weather) : BattleEvent;
public sealed record WeatherEnded(Weather Weather) : BattleEvent;
public sealed record WeatherDamage(BattleSlot Slot, int Amount) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public WeatherDamage(BattleSide side, int amount) : this(new BattleSlot(side, 0), amount) { }
}
public sealed record TerrainChanged(Terrain Terrain) : BattleEvent;
public sealed record TerrainEnded(Terrain Terrain) : BattleEvent;
public sealed record TerrainHealed(BattleSlot Slot, int Amount) : BattleEvent;
public sealed record TerrainPriorityBlocked(BattleSlot Source, BattleSlot Target) : BattleEvent;
public sealed record Bound(BattleSlot Slot) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public Bound(BattleSide side) : this(new BattleSlot(side, 0)) { }
}
public sealed record BoundHurt(BattleSlot Slot, int Amount) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public BoundHurt(BattleSide side, int amount) : this(new BattleSlot(side, 0), amount) { }
}
public sealed record BindReleased(BattleSlot Slot) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public BindReleased(BattleSide side) : this(new BattleSlot(side, 0)) { }
}
public sealed record Protected(BattleSlot Slot) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public Protected(BattleSide side) : this(new BattleSlot(side, 0)) { }
}
public sealed record ProtectFailed(BattleSlot Slot) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public ProtectFailed(BattleSide side) : this(new BattleSlot(side, 0)) { }
}
public sealed record MoveBlocked(BattleSlot Slot) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public MoveBlocked(BattleSide side) : this(new BattleSlot(side, 0)) { }
}
public sealed record ProtectionBlocked(BattleSlot Source, BattleSlot Target,
    BattleConditionId Condition) : BattleEvent;
public sealed record ProtectionContactDamaged(BattleSlot Slot, BattleConditionId Condition,
    int Amount) : BattleEvent;
public sealed record ForcedOut(BattleSlot Slot) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public ForcedOut(BattleSide side) : this(new BattleSlot(side, 0)) { }
}
public sealed record Charging(BattleSlot Slot, EntityId Move) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public Charging(BattleSide side, EntityId move) : this(new BattleSlot(side, 0), move) { }
}
public sealed record FullyParalyzed(BattleSlot Slot) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public FullyParalyzed(BattleSide side) : this(new BattleSlot(side, 0)) { }
}
public sealed record Confused(BattleSlot Slot) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public Confused(BattleSide side) : this(new BattleSlot(side, 0)) { }
}
public sealed record ConfusionEnded(BattleSlot Slot) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public ConfusionEnded(BattleSide side) : this(new BattleSlot(side, 0)) { }
}
public sealed record HurtInConfusion(BattleSlot Slot, int Amount) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public HurtInConfusion(BattleSide side, int amount) : this(new BattleSlot(side, 0), amount) { }
}
public sealed record Flinched(BattleSlot Slot) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public Flinched(BattleSide side) : this(new BattleSlot(side, 0)) { }
}
public sealed record StillAsleep(BattleSlot Slot) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public StillAsleep(BattleSide side) : this(new BattleSlot(side, 0)) { }
}
public sealed record WokeUp(BattleSlot Slot) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public WokeUp(BattleSide side) : this(new BattleSlot(side, 0)) { }
}
public sealed record Thawed(BattleSlot Slot) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public Thawed(BattleSide side) : this(new BattleSlot(side, 0)) { }
}
public sealed record StillFrozen(BattleSlot Slot) : BattleEvent
{
    public BattleSide Side => Slot.Side;
    public StillFrozen(BattleSide side) : this(new BattleSlot(side, 0)) { }
}
public sealed record BallThrown : BattleEvent;
public sealed record CaptureShakes(int Count) : BattleEvent;
public sealed record Captured(BattleSide Side) : BattleEvent;
public sealed record BrokeFree : BattleEvent;
public sealed record BattleEnded(BattleSide? Winner) : BattleEvent;

public sealed record BattleOutcome(BattleSide? Winner)
{
    public bool IsDraw => Winner is null;
    public BattleOutcome(BattleSide winner) : this((BattleSide?)winner) { }
}
