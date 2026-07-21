using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Runtime.Engine;

/// <summary>Turns Core battle events into presentation text (ENGINE_RUNTIME_SPEC 16F). Every event
/// the engine can emit must have a line here: an unpresented event returns null so the completeness
/// test fails rather than the event silently vanishing from the message log.
///
/// Runtime never predicts or recomputes anything — each line describes an event Core already
/// decided. Wording is placeholder quality; final copy is content work, not engine work.</summary>
public static class BattleEventPresenter
{
    /// <summary>Shown when an event has no presentation, so a gap is visible in play as well as in
    /// tests rather than being dropped.</summary>
    public static string Unpresented(BattleEvent e) => $"[unpresented event: {e.GetType().Name}]";

    public static string Line(BattleEvent e) => Line(e, id => id.ToString());

    public static string Line(BattleEvent e, Func<EntityId, string> nameOf) =>
        TryLine(e, nameOf) ?? Unpresented(e);

    /// <summary>The presentation for an event, or null when the catalog does not cover it.</summary>
    public static string? TryLine(BattleEvent e, Func<EntityId, string> nameOf)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(nameOf);

        return e switch
        {
            // --- Actions -------------------------------------------------------------
            MoveUsed move => $"{move.Side} used {nameOf(move.Move)}",
            MoveCalled => "The move called another move",
            MoveCallFailed => "The called move failed",
            MoveMissed => "The attack missed",
            MoveFailed => "The move failed",
            ActionSkipped => "The action was skipped",
            ActionInvalidated => "The action was no longer valid",
            MoveBlocked => "The move was blocked",
            TargetRedirected => "The attack was redirected",
            PositionsSwapped => "Positions were swapped",

            // --- Damage and health ---------------------------------------------------
            DamageDealt damage => $"{damage.Target} took {damage.Amount} damage",
            Healed healed => $"{healed.Side} healed {healed.Amount}",
            Fainted fainted => $"{fainted.Side} fainted",
            StatusDamage => "Its status hurt it",
            ResidualDamage => "It was hurt by the field",
            HpFractionDamaged => "It lost a portion of its health",
            HpFormulaChanged => "Its health changed",
            HpCostPaid => "It paid health to act",
            Recoiled => "It was hurt by recoil",
            ContactDamaged => "Making contact hurt it",
            WeatherDamage => "The weather hurt it",

            // --- Status and stats ----------------------------------------------------
            StatusApplied status => $"{status.Side} became {status.Status}",
            StatusCured => "Its status was cured",
            StatStageChanged => "Its stats changed",
            CritBoosted => "Its critical-hit chance rose",
            Confused => "It became confused",
            ConfusionEnded => "It snapped out of confusion",
            HurtInConfusion => "It hurt itself in confusion",
            FullyParalyzed => "It is paralysed and cannot move",
            Flinched => "It flinched",
            StillAsleep => "It is fast asleep",
            WokeUp => "It woke up",
            Thawed => "It thawed out",
            StillFrozen => "It is frozen solid",

            // --- Switching and forms -------------------------------------------------
            SwitchedIn switched => $"{switched.Side} switched in",
            ReplacementRequested => "A replacement is needed",
            ForcedOut => "It was forced out",
            FormChanged form => form.FormId is null
                ? $"{form.Side} reverted form"
                : $"{form.Side} changed form to {form.FormId}",
            Transformed => "It transformed",
            TransformFailed => "The transformation failed",

            // --- Decoys --------------------------------------------------------------
            DecoyCreated => "A decoy appeared",
            DecoyCreationFailed => "The decoy could not be made",
            DecoyDamaged => "The decoy took the hit",
            DecoyBroken => "The decoy broke",
            DecoyBlocked => "The decoy blocked it",

            // --- Items ---------------------------------------------------------------
            BattleItemUsed item => $"{item.Side} used {nameOf(item.Item)}",
            HeldItemConsumed held => $"{held.Side} held item fired: {held.Op}",
            PpRestored => "Its PP was restored",

            // --- Field ---------------------------------------------------------------
            WeatherChanged weather => $"Weather changed to {weather.Weather}",
            WeatherEnded => "The weather cleared",
            TerrainChanged => "The terrain changed",
            TerrainEnded => "The terrain faded",
            TerrainHealed => "The terrain restored health",
            TerrainPriorityBlocked => "The terrain blocked a priority move",
            LeechSeeded => "It was seeded",
            LeechSapped => "Its health was sapped",

            // --- Binding and protection ----------------------------------------------
            Bound => "It was bound",
            BoundHurt => "The binding hurt it",
            BindReleased => "It was freed",
            Protected protectedEvent => $"{protectedEvent.Side} protected itself",
            ProtectFailed => "Its protection failed",

            // --- Multi-turn ----------------------------------------------------------
            Charging => "It is charging",
            ChargeReleased => "It unleashed the attack",
            ChargeCancelled => "The charge was cancelled",
            MultiTurnLockStarted => "It is locked into its move",
            MultiTurnLockContinued => "It continues its move",
            MultiTurnLockEnded => "Its move ended",

            // --- Capture -------------------------------------------------------------
            BallThrown => "A capture device was thrown",
            CaptureShakes => "The device shook",
            Captured => "It was caught",
            BrokeFree => "It broke free",

            // --- Overlays and mutation (Phase 15 mechanics) ---------------------------
            AbilityMutated => "Its ability changed",
            HeldItemMutated => "Its held item changed",
            TypesMutated => "Its type changed",
            DerivedStatMutated => "Its stats were rewritten",
            MetricMutated => "Its size changed",
            MoveTypeOverrideApplied => "The move changed type",
            TemporaryMoveReplacementApplied => "Its move was replaced",
            TemporaryMoveReplacementFailed => "The move replacement failed",

            // --- Conditions ----------------------------------------------------------
            ConditionApplied => "A condition took hold",
            ConditionRefreshed => "The condition was refreshed",
            ConditionStacked => "The condition intensified",
            ConditionReplaced => "The condition was replaced",
            ConditionRemoved => "The condition ended",
            ConditionExpired => "The condition wore off",
            ConditionTransferred => "The condition moved across",
            ConditionApplicationRejected => "The condition would not take hold",
            ConditionOperationRejected => "The condition change was refused",
            ConditionOperationNoOp => "The condition was unchanged",

            // --- Hazards -------------------------------------------------------------
            EntryHazardSet => "A hazard was set",
            EntryHazardTriggered => "The hazard struck",
            EntryHazardAbsorbed => "The hazard was absorbed",

            // --- Protection and blocking ---------------------------------------------
            ProtectionBlocked => "The attack was blocked",
            ProtectionContactDamaged => "Contact with the guard hurt it",
            ActionBlocked => "The action was blocked",
            SemiInvulnerableAvoided => "It was out of reach",

            // --- Ordering and delayed actions ----------------------------------------
            TurnOrderIntentApplied => "The turn order shifted",
            TurnOrderIntentFailed => "The turn order was unchanged",
            PairedActionPrepared => "A paired action was prepared",
            DelayedActionQueued => "An attack was set in motion",
            DelayedActionResolved => "The delayed attack landed",
            DelayedActionFailed => "The delayed attack failed",

            // --- Diagnostics ---------------------------------------------------------
            HookDispatchFailed => "An ability could not resolve",

            // --- Outcome -------------------------------------------------------------
            BattleEnded ended => $"{ended.Winner} won",

            _ => null,   // uncovered: the completeness test fails on this
        };
    }
}
