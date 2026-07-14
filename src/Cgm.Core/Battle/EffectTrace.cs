using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum EffectTraceKind
{
    StatusGate,
    FlinchGate,
    ConfusionGate,
    MoveGate,
    EffectChance,
    ContactChance,
    TurnOrderTie,
    TargetSelection,
    Accuracy,
    HitCount,
    Immunity,
    Critical,
    DamageRoll,
    Damage,
    LockDuration,
    TrapDuration,
    ConfusionDuration,
    Protect,
    ForceSwitchReserve,
    PositionSwap,
    Redirection,
    IntentEnqueued,
    IntentConsumed,
    IntentDeferred,
    IntentCancelled,
    IntentTransferred,
}

/// <summary>Deterministic resolver evidence; never drives simulation.</summary>
public sealed record EffectTraceEntry(
    int Turn,
    int ActionSequence,
    BattleSlot SourceSlot,
    BattleSlot? TargetSlot,
    EffectTraceKind Kind,
    bool Performed,
    double? DrawResult,
    int? Value,
    int EventStartIndex,
    int EventEndIndex)
{
    public double? DrawMinimum { get; init; }
    public double? DrawBound { get; init; }
    public long? IntentSequence { get; init; }
    public BattleIntentCheckpoint? IntentCheckpoint { get; init; }
    public BattleIntentPayloadKind? IntentPayload { get; init; }
    public EntityId? IntentSourceMove { get; init; }
}
