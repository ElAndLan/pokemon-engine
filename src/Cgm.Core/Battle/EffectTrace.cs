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
}
