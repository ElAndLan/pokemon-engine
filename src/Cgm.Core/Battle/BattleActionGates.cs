using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public sealed record BattleMoveGateInputs(
    bool PreviousActionFailed = false,
    bool SourceBeforeTarget = false,
    bool SourceAfterTarget = false,
    DamageClass? TargetPlannedMoveClass = null,
    bool MatchingDamageReceived = false);

public static class BattleActionGates
{
    public static MoveFailureReason? Failure(BattleMove move, BattleCreature source,
        MoveGateEffect gate, BattleMoveGateInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(inputs);
        Validate(gate);
        return gate.Kind switch
        {
            MoveGateKind.FirstAction when source.ActionsSinceSwitch > 0 => MoveFailureReason.FirstActionOnly,
            MoveGateKind.NotPreviousMove when source.LastMoveUsed == move.Move => MoveFailureReason.CannotRepeat,
            MoveGateKind.PreviousActionFailed when !inputs.PreviousActionFailed => MoveFailureReason.PreviousActionRequired,
            MoveGateKind.SourceBeforeTarget when !inputs.SourceBeforeTarget => MoveFailureReason.ActionOrderRequirementNotMet,
            MoveGateKind.SourceAfterTarget when !inputs.SourceAfterTarget => MoveFailureReason.ActionOrderRequirementNotMet,
            MoveGateKind.TargetAction when !Matches(inputs.TargetPlannedMoveClass, gate.TargetClass!.Value)
                => MoveFailureReason.TargetActionRequirementNotMet,
            MoveGateKind.DamageReceived when gate.DamageMode == MoveGateDamageMode.Forbid
                && inputs.MatchingDamageReceived => MoveFailureReason.InterruptedByDamage,
            MoveGateKind.DamageReceived when gate.DamageMode == MoveGateDamageMode.Require
                && !inputs.MatchingDamageReceived => MoveFailureReason.DamageRequired,
            _ => null,
        };
    }

    public static bool SourceHistoryAllows(BattleMove move, BattleCreature source,
        bool previousActionFailed, MoveGateTiming? timing = null) =>
        move.SecondaryEffects.OfType<MoveGateEffect>()
            .Where(gate => timing is null || gate.Timing == timing)
            .Where(gate => gate.Kind is MoveGateKind.FirstAction or MoveGateKind.NotPreviousMove
                or MoveGateKind.PreviousActionFailed)
            .All(gate => Failure(move, source, gate,
                new BattleMoveGateInputs(PreviousActionFailed: previousActionFailed)) is null);

    public static void Validate(MoveGateEffect gate)
    {
        ArgumentNullException.ThrowIfNull(gate);
        if (!Enum.IsDefined(gate.Kind) || !Enum.IsDefined(gate.Timing))
            throw new ArgumentOutOfRangeException(nameof(gate), "Action-gate kind and timing must be defined.");
        if ((gate.TargetClass is { } targetClass && !Enum.IsDefined(targetClass))
            || (gate.DamageMode is { } damageMode && !Enum.IsDefined(damageMode))
            || (gate.DamageClass is { } damageClass
                && (!Enum.IsDefined(damageClass) || damageClass == DamageClass.Status)))
            throw new ArgumentOutOfRangeException(nameof(gate), "Action-gate filters must be defined damaging values.");
        if ((gate.Kind == MoveGateKind.TargetAction) != (gate.TargetClass is not null)
            || gate.Kind == MoveGateKind.TargetAction && (gate.DamageMode is not null || gate.DamageClass is not null))
            throw new ArgumentException("targetAction requires only targetClass.", nameof(gate));
        if ((gate.Kind == MoveGateKind.DamageReceived) != (gate.DamageMode is not null)
            || gate.Kind != MoveGateKind.DamageReceived && gate.DamageClass is not null
            || gate.Kind == MoveGateKind.DamageReceived && gate.TargetClass is not null)
            throw new ArgumentException("damageReceived requires damageMode and accepts only damageClass.", nameof(gate));
        if (gate.Timing == MoveGateTiming.Selection && gate.Kind is not
            (MoveGateKind.FirstAction or MoveGateKind.NotPreviousMove or MoveGateKind.PreviousActionFailed))
            throw new ArgumentException("Selection timing admits only source-history gates.", nameof(gate));
    }

    private static bool Matches(DamageClass? planned, MoveGateTargetClass required) => required switch
    {
        MoveGateTargetClass.AnyMove => planned is not null,
        MoveGateTargetClass.DamagingMove => planned is DamageClass.Physical or DamageClass.Special,
        MoveGateTargetClass.StatusMove => planned == DamageClass.Status,
        _ => throw new ArgumentOutOfRangeException(nameof(required)),
    };
}
