using Cgm.Core.Model;

namespace Cgm.Core.Battle;

/// <summary>
/// Battle turn engine (v0–v4): parties per side with one active each, action validation, ordering
/// by priority/effective-speed, move resolution through the formula layer with a fixed RNG draw
/// order, statuses/stat-stages, capture, switching, and end-on-party-wipe. Deterministic given the
/// seed + actions (golden replays). UI submits <see cref="BattleAction"/>s and consumes the
/// <see cref="BattleEvent"/> stream; it never mutates state directly.
/// </summary>
public sealed class BattleController
{
    private sealed record QueuedActionGate(BattleSlot Slot, int DueTurn);
    private sealed record AdmittedAction(BattleActionSubmission Submission, int ActorPartyIndex, EntityId? MoveId);

    private readonly List<BattleCreature>[] _parties;
    private readonly BattleActiveSlots _activeSlots;
    private readonly Dictionary<EntityId, int>[] _itemStock = [[], []];
    private readonly bool[] _temporaryFormUsed = [false, false];
    private readonly int[] _spikeLayers = [0, 0];    // entry-hazard side condition (catalog §7.3), per side
    private readonly bool[] _stealthRock = [false, false]; // type-scaled entry hazard, per side
    private Weather _weather = Weather.None;          // field condition (catalog §7.6)
    private int _weatherTurns;
    private readonly TypeChart _chart;
    private readonly IRng _rng;
    private readonly List<BattleEvent> _log = [];
    private readonly List<QueuedActionGate> _queuedActionGates = [];
    private bool _dispatchingWeatherChange;

    public BattleController(BattleCreature player, BattleCreature enemy, TypeChart chart, IRng rng, bool isWild = false)
        : this([player], [enemy], chart, rng, isWild) { }

    public BattleController(IReadOnlyList<BattleCreature> playerParty, IReadOnlyList<BattleCreature> enemyParty,
        TypeChart chart, IRng rng, bool isWild = false)
    {
        _parties = [[.. playerParty], [.. enemyParty]];
        _activeSlots = new BattleActiveSlots(BattleTopology.Singles);
        _activeSlots.Assign(new BattleSlot(BattleSide.Player, 0), 0);
        _activeSlots.Assign(new BattleSlot(BattleSide.Enemy, 0), 0);
        _chart = chart;
        _rng = rng;
        IsWild = isWild;
    }

    /// <summary>Creates a slot-addressed battle. The supplied active assignments are party indices, not slots.</summary>
    public BattleController(
        IReadOnlyList<BattleCreature> playerParty,
        IReadOnlyList<BattleCreature> enemyParty,
        BattleTopology topology,
        IReadOnlyList<int> playerActivePartyIndexes,
        IReadOnlyList<int> enemyActivePartyIndexes,
        TypeChart chart,
        IRng rng,
        bool isWild = false)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(playerParty);
        ArgumentNullException.ThrowIfNull(enemyParty);
        ArgumentNullException.ThrowIfNull(playerActivePartyIndexes);
        ArgumentNullException.ThrowIfNull(enemyActivePartyIndexes);
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(rng);
        if (playerActivePartyIndexes.Count != topology.ActiveSlotsPerSide
            || enemyActivePartyIndexes.Count != topology.ActiveSlotsPerSide)
            throw new ArgumentException("Every battle-start slot requires one party assignment.");

        _parties = [[.. playerParty], [.. enemyParty]];
        _activeSlots = new BattleActiveSlots(topology);
        AssignInitial(BattleSide.Player, playerActivePartyIndexes);
        AssignInitial(BattleSide.Enemy, enemyActivePartyIndexes);
        _chart = chart;
        _rng = rng;
        IsWild = isWild;
    }

    public int Turn { get; private set; }
    public bool IsWild { get; }
    public bool Captured { get; private set; }
    public BattleOutcome? Outcome { get; private set; }
    public IReadOnlyList<BattleEvent> Log => _log;

    public BattleTopology Topology => _activeSlots.Topology;
    public BattleCreature Active(BattleSide side)
    {
        RequireSinglesAdapter();
        return Active(new BattleSlot(side, 0));
    }
    public BattleCreature Active(BattleSlot slot) => _parties[(int)slot.Side][_activeSlots.PartyIndex(slot)];
    public int ActiveIndex(BattleSide side)
    {
        RequireSinglesAdapter();
        return ActiveIndex(new BattleSlot(side, 0));
    }
    public int ActiveIndex(BattleSlot slot) => _activeSlots.PartyIndex(slot);
    public IReadOnlyList<BattleCreature> Party(BattleSide side) => _parties[(int)side];

    /// <summary>Entry-hazard state on a side (for AI switch valuation / UI). A creature switching in here
    /// takes stealth-rock then spikes damage — see <see cref="OnSwitchIn"/>.</summary>
    public int SpikeLayers(BattleSide side) => _spikeLayers[(int)side];
    public bool HasStealthRock(BattleSide side) => _stealthRock[(int)side];

    public void SetBattleItemStock(BattleSide side, EntityId item, int count) =>
        _itemStock[(int)side][item] = Math.Max(0, count);

    public bool CanSubmitAction(BattleSide side, BattleAction action)
    {
        RequireSinglesAdapter();
        if (Outcome is not null)
            return false;

        try
        {
            Validate(side, action);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public bool CanSubmitAction(BattleSlot slot, BattleAction action)
    {
        if (Outcome is not null || !Topology.Contains(slot))
            return false;

        try
        {
            Validate(slot, action);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public IReadOnlyList<BattleEvent> ResolveTurn(BattleAction playerAction, BattleAction enemyAction)
    {
        RequireSinglesAdapter();
        if (Outcome is not null)
            throw new InvalidOperationException("The battle is already over.");

        int start = _log.Count;
        BattleAction resolvedPlayerAction = ApplyQueuedActionGates(new BattleSlot(BattleSide.Player, 0), playerAction);
        BattleAction resolvedEnemyAction = ApplyQueuedActionGates(new BattleSlot(BattleSide.Enemy, 0), enemyAction);
        Validate(BattleSide.Player, resolvedPlayerAction);
        Validate(BattleSide.Enemy, resolvedEnemyAction);
        var actions = new BattleTurnActions(Topology,
        [
            new BattleActionSubmission(new BattleSlot(BattleSide.Player, 0), resolvedPlayerAction),
            new BattleActionSubmission(new BattleSlot(BattleSide.Enemy, 0), resolvedEnemyAction),
        ]);

        // 1. Switches happen before anything else.
        ApplySwitch(BattleSide.Player, resolvedPlayerAction);
        ApplySwitch(BattleSide.Enemy, resolvedEnemyAction);

        // 2. Battle form activation happens before that side's selected move.
        ApplyFormActivation(BattleSide.Player, resolvedPlayerAction);
        ApplyFormActivation(BattleSide.Enemy, resolvedEnemyAction);

        // 3. Capture (wild, player only).
        if (resolvedPlayerAction is ThrowBall ball)
            ResolveCapture(ball);

        // 4. Battle items resolve before moves, after switches/capture.
        ApplyBattleItem(BattleSide.Player, resolvedPlayerAction);
        ApplyBattleItem(BattleSide.Enemy, resolvedEnemyAction);

        // 5. Moves, ordered by priority then effective speed.
        if (Outcome is null)
            ResolveMoves(actions);

        // 6. End-of-turn residuals, then auto-replace any fainted actives with reserves.
        if (Outcome is null)
        {
            EndOfTurn();
            AutoReplaceFainted();
        }

        Turn++;
        RevertFaintedBattleForms();
        CheckEnd();
        return _log.GetRange(start, _log.Count - start);
    }

    /// <summary>
    /// Admits and resolves a complete slot-addressed turn. Doubles move target materialization is
    /// intentionally owned by 15B-3; this package owns construction, admission, phase ordering,
    /// collective conflicts, and actor identity through the move scheduling checkpoint.
    /// </summary>
    public IReadOnlyList<BattleEvent> ResolveTurn(BattleTurnActions submitted)
    {
        ArgumentNullException.ThrowIfNull(submitted);
        if (Outcome is not null)
            throw new InvalidOperationException("The battle is already over.");
        if (submitted.Topology.ActiveSlotsPerSide != Topology.ActiveSlotsPerSide)
            throw new ArgumentException("Submitted actions use a different battle topology.", nameof(submitted));
        if (Topology.ActiveSlotsPerSide == 1)
        {
            return ResolveTurn(
                submitted.For(new BattleSlot(BattleSide.Player, 0)).Action,
                submitted.For(new BattleSlot(BattleSide.Enemy, 0)).Action);
        }

        int start = _log.Count;
        IReadOnlyList<AdmittedAction> admitted = Admit(submitted);
        ConsumePreviewedActionGates(admitted);
        ResolveSwitchPhase(admitted);
        ResolveItemPhase(admitted);
        ResolveFormPhase(admitted);
        ResolveDoublesMoveScheduling(admitted);
        Turn++;
        return _log.GetRange(start, _log.Count - start);
    }

    private IReadOnlyList<AdmittedAction> Admit(BattleTurnActions submitted)
    {
        var admitted = new List<AdmittedAction>(submitted.Actions.Count);
        foreach (BattleActionSubmission submission in submitted.Actions)
        {
            BattleAction effective = PreviewQueuedActionGate(submission.Source, submission.Action);
            Validate(submission.Source, effective);
            BattleCreature actor = Active(submission.Source);
            EntityId? moveId = MoveIndex(effective) is { } moveIndex ? actor.Moves[moveIndex].Move : null;
            admitted.Add(new AdmittedAction(submission with { Action = effective }, ActiveIndex(submission.Source), moveId));
        }

        foreach (IGrouping<BattleSide, AdmittedAction> side in admitted.GroupBy(action => action.Submission.Source.Side))
        {
            int forms = side.Count(action => action.Submission.Action is ActivateForm form
                && Active(action.Submission.Source).CanActivateTemporaryForm(form.FormId,
                    item => _itemStock[(int)side.Key].TryGetValue(item, out int count) && count > 0));
            if (forms > 1)
                throw new ArgumentException($"{side.Key} cannot activate more than one temporary form in a turn.");

            foreach (IGrouping<EntityId, UseBattleItem> items in side
                .Select(action => action.Submission.Action).OfType<UseBattleItem>().GroupBy(item => item.Item))
            {
                int stock = _itemStock[(int)side.Key].TryGetValue(items.Key, out int count) ? count : 0;
                if (items.Count() > stock)
                    throw new ArgumentException($"{side.Key} requested more '{items.Key}' than its available stock.");
            }

            int[] switches = side.Select(action => action.Submission.Action).OfType<Switch>().Select(action => action.PartyIndex).ToArray();
            if (switches.Distinct().Count() != switches.Length)
                throw new ArgumentException($"{side.Key} cannot switch two slots to the same reserve.");
        }
        return admitted;
    }

    private void ConsumePreviewedActionGates(IEnumerable<AdmittedAction> actions)
    {
        foreach (AdmittedAction action in actions)
        {
            if (action.Submission.Action is not Pass)
                continue;
            int removed = _queuedActionGates.RemoveAll(gate => gate.Slot == action.Submission.Source && gate.DueTurn <= Turn);
            if (removed > 0)
                _log.Add(new ActionSkipped(action.Submission.Source));
        }
    }

    private BattleAction PreviewQueuedActionGate(BattleSlot slot, BattleAction action) =>
        _queuedActionGates.Any(gate => gate.Slot == slot && gate.DueTurn <= Turn) ? new Pass() : action;

    private void ResolveSwitchPhase(IReadOnlyList<AdmittedAction> actions)
    {
        var scheduled = actions.Where(action => action.Submission.Action is Switch)
            .Select(action => new BattleScheduledAction(action.Submission, 0, Speed(action.Submission.Source)))
            .ToList();
        foreach (BattleScheduledAction scheduledAction in BattleTurnOrder.Order(scheduled, _rng))
        {
            AdmittedAction action = actions.Single(candidate => candidate.Submission == scheduledAction.Submission);
            if (!ActorIsCurrent(action))
            {
                InvalidateActor(action);
                continue;
            }
            SwitchTo(action.Submission.Source, ((Switch)action.Submission.Action).PartyIndex);
        }
    }

    private void ResolveItemPhase(IReadOnlyList<AdmittedAction> actions)
    {
        var scheduled = actions.Where(action => action.Submission.Action is UseBattleItem)
            .Select(action => new BattleScheduledAction(action.Submission, 0, Speed(action.Submission.Source)))
            .ToList();
        foreach (BattleScheduledAction scheduledAction in BattleTurnOrder.Order(scheduled, _rng))
        {
            AdmittedAction action = actions.Single(candidate => candidate.Submission == scheduledAction.Submission);
            if (!ActorIsCurrent(action))
            {
                InvalidateActor(action);
                continue;
            }
            UseBattleItem item = (UseBattleItem)action.Submission.Action;
            if (!_itemStock[(int)action.Submission.Source.Side].TryGetValue(item.Item, out int stock) || stock <= 0)
            {
                _log.Add(new ActionInvalidated(action.Submission.Source, ActionInvalidationReason.ResourceChanged));
                continue;
            }
            BattleCreature target = _parties[(int)action.Submission.Source.Side][item.TargetPartyIndex];
            if (target.IsFainted || target.CurrentHp >= target.MaxHp)
            {
                _log.Add(new ActionInvalidated(action.Submission.Source, ActionInvalidationReason.TargetStateChanged));
                continue;
            }
            _itemStock[(int)action.Submission.Source.Side][item.Item]--;
            _log.Add(new BattleItemUsed(action.Submission.Source, item.Item, item.TargetPartyIndex));
            Heal(target, action.Submission.Source.Side, Math.Min(item.HealAmount, target.MaxHp - target.CurrentHp));
        }
    }

    private void ResolveFormPhase(IReadOnlyList<AdmittedAction> actions)
    {
        foreach (AdmittedAction action in actions.Where(action => action.Submission.Action is ActivateForm)
            .OrderBy(action => action.Submission.Source))
        {
            if (!ActorIsCurrent(action))
            {
                InvalidateActor(action);
                continue;
            }
            ActivateForm form = (ActivateForm)action.Submission.Action;
            BattleCreature creature = Active(action.Submission.Source);
            if (creature.CanActivateTimedForm(form.FormId))
                creature.ActivateTimedForm(form.FormId);
            else
            {
                creature.ActivateTemporaryForm(form.FormId);
                _temporaryFormUsed[(int)action.Submission.Source.Side] = true;
            }
            _log.Add(new FormChanged(action.Submission.Source, form.FormId));
        }
    }

    private void ResolveDoublesMoveScheduling(IReadOnlyList<AdmittedAction> actions)
    {
        var scheduled = new List<BattleScheduledAction>();
        foreach (AdmittedAction action in actions)
        {
            if (MoveIndex(action.Submission.Action) is not { } moveIndex || !ActorIsCurrent(action))
                continue;
            int effective = EffectiveMoveIndex(action.Submission.Source, moveIndex);
            scheduled.Add(new BattleScheduledAction(action.Submission, Active(action.Submission.Source).Moves[effective].Priority,
                Speed(action.Submission.Source)));
        }
        foreach (BattleScheduledAction scheduledAction in BattleTurnOrder.Order(scheduled, _rng))
        {
            AdmittedAction action = actions.Single(candidate => candidate.Submission == scheduledAction.Submission);
            if (!ActorIsCurrent(action))
                InvalidateActor(action);
        }
    }

    private bool ActorIsCurrent(AdmittedAction action)
    {
        if (ActiveIndex(action.Submission.Source) != action.ActorPartyIndex)
            return false;
        BattleCreature actor = Active(action.Submission.Source);
        if (actor.IsFainted)
            return false;
        return action.MoveId is null || MoveIndex(action.Submission.Action) is { } moveIndex
            && moveIndex < actor.Moves.Count && actor.Moves[moveIndex].Move == action.MoveId;
    }

    private void InvalidateActor(AdmittedAction action)
    {
        ActionInvalidationReason reason = ActiveIndex(action.Submission.Source) != action.ActorPartyIndex
            ? ActionInvalidationReason.ActorChanged
            : Active(action.Submission.Source).IsFainted ? ActionInvalidationReason.ActorFainted
            : ActionInvalidationReason.MoveChanged;
        _log.Add(new ActionInvalidated(action.Submission.Source, reason));
    }

    private void Validate(BattleSide side, BattleAction action) => Validate(new BattleSlot(side, 0), action);

    private void Validate(BattleSlot slot, BattleAction action)
    {
        if (!Topology.Contains(slot))
            throw new ArgumentException($"Slot {slot} is outside this battle topology.", nameof(slot));
        BattleSide side = slot.Side;
        switch (action)
        {
            case UseMove use:
                ValidateMoveUse(slot, use.MoveIndex);
                break;

            case ActivateForm form:
                ValidateMoveUse(slot, form.MoveIndex);
                BattleCreature active = Active(slot);
                bool temporary = !_temporaryFormUsed[(int)side]
                    && active.CanActivateTemporaryForm(form.FormId, item => _itemStock[(int)side].TryGetValue(item, out int count) && count > 0);
                if (!temporary && !active.CanActivateTimedForm(form.FormId))
                    throw new ArgumentException($"{side} cannot activate form '{form.FormId}'.");
                break;

            case Switch sw:
                List<BattleCreature> party = _parties[(int)side];
                if (sw.PartyIndex < 0 || sw.PartyIndex >= party.Count)
                    throw new ArgumentException($"{side} switch index {sw.PartyIndex} out of range.");
                if (_activeSlots.IsActive(side, sw.PartyIndex))
                    throw new ArgumentException($"{side} is already on party member {sw.PartyIndex}.");
                if (party[sw.PartyIndex].IsFainted)
                    throw new ArgumentException($"{side} cannot switch to a fainted member.");
                if (Active(slot).IsTrapped)
                    throw new ArgumentException($"{side} is trapped and cannot switch.");
                if (Active(slot).IsCharging)
                    throw new ArgumentException($"{side} is charging a move and cannot switch.");
                if (Active(slot).IsLocked)
                    throw new ArgumentException($"{side} is locked into a move and cannot switch.");
                break;

            case ThrowBall when side != BattleSide.Player:
                throw new ArgumentException("Only the player can throw a ball.");
            case ThrowBall when !IsWild:
                throw new ArgumentException("Cannot capture in a trainer battle.");
            case ThrowBall when Topology.ActiveSlotsPerSide != 1:
                throw new ArgumentException("Capture is not available in doubles.");
            case ThrowBall:
                break;

            case UseBattleItem item:
                if (item.HealAmount <= 0)
                    throw new ArgumentException("Battle item heal amount must be positive.");
                if (!_itemStock[(int)side].TryGetValue(item.Item, out int count) || count <= 0)
                    throw new ArgumentException($"{side} has no stock for item '{item.Item}'.");
                List<BattleCreature> itemParty = _parties[(int)side];
                if (item.TargetPartyIndex < 0 || item.TargetPartyIndex >= itemParty.Count)
                    throw new ArgumentException($"{side} item target {item.TargetPartyIndex} out of range.");
                BattleCreature target = itemParty[item.TargetPartyIndex];
                if (target.IsFainted)
                    throw new ArgumentException($"{side} cannot use a healing item on a fainted creature.");
                if (target.CurrentHp >= target.MaxHp)
                    throw new ArgumentException($"{side} cannot use a healing item at full HP.");
                break;

            case Pass:
                break;

            default:
                throw new ArgumentException($"Unsupported action for {side}.");
        }
    }

    private void ValidateMoveUse(BattleSide side, int moveIndex) => ValidateMoveUse(new BattleSlot(side, 0), moveIndex);

    private void ValidateMoveUse(BattleSlot slot, int moveIndex)
    {
        BattleCreature c = Active(slot);
        BattleSide side = slot.Side;
        if (moveIndex < 0 || moveIndex >= c.Moves.Count)
            throw new ArgumentException($"{side} move index {moveIndex} out of range.");
        if (!c.IsCharging && !c.IsLocked && c.ChoiceLockedMoveIndex is { } locked && moveIndex != locked)
            throw new ArgumentException($"{side} is locked into move {locked}.");
        if (!c.Moves[moveIndex].HasPp)
            throw new ArgumentException($"{side} move {moveIndex} has no PP.");
    }

    private void ApplySwitch(BattleSide side, BattleAction action)
    {
        if (action is Switch sw)
            SwitchTo(side, sw.PartyIndex);
    }

    private void ApplyBattleItem(BattleSide side, BattleAction action)
    {
        if (action is not UseBattleItem item)
            return;

        _itemStock[(int)side][item.Item]--;
        BattleCreature target = _parties[(int)side][item.TargetPartyIndex];
        _log.Add(new BattleItemUsed(side, item.Item, item.TargetPartyIndex));
        Heal(target, side, Math.Min(item.HealAmount, target.MaxHp - target.CurrentHp));
    }

    private void ApplyFormActivation(BattleSide side, BattleAction action)
    {
        if (action is not ActivateForm form)
            return;

        BattleCreature active = Active(side);
        if (active.CanActivateTimedForm(form.FormId))
        {
            active.ActivateTimedForm(form.FormId);
        }
        else
        {
            active.ActivateTemporaryForm(form.FormId);
            _temporaryFormUsed[(int)side] = true;
        }
        _log.Add(new FormChanged(side, form.FormId));
    }

    /// <summary>Brings a side's party member into play — the shared path for voluntary, forced (Roar),
    /// and faint-replacement switches: outgoing loses stat stages + volatiles, then the on_switch_in
    /// hazards fire on the incoming creature.</summary>
    private void SwitchTo(BattleSide side, int index)
    {
        BattleCreature outgoing = Active(side);
        outgoing.ResetStages();     // stat stages don't carry across a switch
        outgoing.ClearVolatiles();  // confusion/flinch/trap/etc. clear on switch-out
        _activeSlots.Assign(new BattleSlot(side, 0), index);
        _log.Add(new SwitchedIn(side, index));
        OnSwitchIn(side);
    }

    private void SwitchTo(BattleSlot slot, int index)
    {
        BattleCreature outgoing = Active(slot);
        outgoing.ResetStages();
        outgoing.ClearVolatiles();
        _activeSlots.Assign(slot, index);
        _log.Add(new SwitchedIn(slot, index));
        // ponytail: multi-slot entry-hook dispatch belongs to the scoped-hook package; this package
        // only establishes the atomic slot switch and preserves the singles hook path.
        if (Topology.ActiveSlotsPerSide == 1)
            OnSwitchIn(slot.Side);
    }

    private void ResolveMoves(BattleTurnActions actions)
    {
        // Flinch and Protect last only for a turn; clear last turn's before this turn's moves resolve.
        foreach (BattleSlot slot in actions.Topology.Slots)
        {
            BattleCreature active = Active(slot);
            active.ClearFlinch();
            active.ClearProtected();
            active.ResetDamageTaken(); // Counter/Mirror Coat only see this turn's hits
        }

        var scheduled = new List<BattleScheduledAction>();
        foreach (BattleActionSubmission submission in actions.Actions)
        {
            if (MoveIndex(submission.Action) is not { } submittedIndex)
                continue;

            int moveIndex = EffectiveMoveIndex(submission.Source.Side, submittedIndex);
            scheduled.Add(new BattleScheduledAction(
                submission,
                Active(submission.Source).Moves[moveIndex].Priority,
                Speed(submission.Source.Side)));
        }

        foreach (BattleScheduledAction scheduledAction in BattleTurnOrder.Order(scheduled, _rng))
        {
            BattleSide side = scheduledAction.Submission.Source.Side;
            int submittedIndex = MoveIndex(scheduledAction.Submission.Action)!.Value;
            ResolveMove(side, EffectiveMoveIndex(side, submittedIndex));
            TickRampageLock(side);
        }
    }

    private static int? MoveIndex(BattleAction action) => action switch
    {
        UseMove move => move.MoveIndex,
        ActivateForm form => form.MoveIndex,
        _ => null,
    };

    /// <summary>Counts a rampage (Thrash/Outrage) down after its move resolved — whether it hit, missed,
    /// or was blocked. When the lock ends, the user confuses itself.</summary>
    private void TickRampageLock(BattleSide side)
    {
        BattleCreature c = Active(side);
        if (!c.IsLocked)
            return;
        c.TickLock();
        if (!c.IsLocked && !c.IsFainted && !c.IsConfused)
        {
            c.SetConfusion(VolatileEffects.ConfusionDuration(_rng));
            _log.Add(new Confused(side));
        }
    }

    private int Speed(BattleSide side)
    {
        BattleCreature c = Active(side);
        double m = StatusEffects.SpeedMultiplier(c.Status) * StatStages.Multiplier(c.Stage(StatKind.Spe));
        return (int)(c.Stats.Spe * m);
    }

    private int Speed(BattleSlot slot)
    {
        BattleCreature c = Active(slot);
        double m = StatusEffects.SpeedMultiplier(c.Status) * StatStages.Multiplier(c.Stage(StatKind.Spe));
        return (int)(c.Stats.Spe * m);
    }

    /// <summary>A charging or rampaging creature is locked into its move; otherwise the submitted index stands.</summary>
    private int EffectiveMoveIndex(BattleSide side, int submitted)
    {
        BattleCreature c = Active(side);
        if (c.IsCharging) return c.ChargingMoveIndex!.Value;
        if (c.IsLocked) return c.LockedMoveIndex;
        return submitted;
    }

    private int EffectiveMoveIndex(BattleSlot slot, int submitted)
    {
        BattleCreature c = Active(slot);
        if (c.IsCharging) return c.ChargingMoveIndex!.Value;
        if (c.IsLocked) return c.LockedMoveIndex;
        return submitted;
    }

    private void ResolveMove(BattleSide side, int moveIndex)
    {
        BattleCreature attacker = Active(side);
        if (attacker.IsFainted)
            return;

        if (!CanAct(attacker, side))
            return; // frozen/asleep/fully-paralyzed — no PP spent, no move

        if (attacker.Flinched)
        {
            _log.Add(new Flinched(side));
            return; // flinch costs the turn but no PP
        }

        if (!PushesThroughConfusion(attacker, side))
            return; // hurt itself in confusion — no PP, no move

        BattleMove move = attacker.Moves[moveIndex];
        if (!PassesMoveGates(attacker, side, move))
            return;
        BattleSide targetSide = BattleTargetResolver.IsSinglesActiveCreatureTarget(move.Target)
            ? BattleTargetResolver.ResolveSinglesActiveCreatureSide(move.Target, side)
            : Opponent(side);
        BattleCreature target = Active(targetSide);
        if (target.IsFainted)
            return;

        if (!move.IsProtect)
            attacker.ResetProtectChain(); // any non-protect move breaks the protect chain

        // Two-turn move: turn 1 charges (PP spent now, no damage); turn 2 fires as a normal hit.
        bool firing = attacker.IsCharging;
        if (move.ChargeTurn && !firing)
        {
            move.UsePp();
            ApplyChoiceLock(attacker, moveIndex);
            attacker.RecordMoveUse(move.Move);
            attacker.StartCharging(moveIndex);
            _log.Add(new Charging(side, move.Move));
            return;
        }
        if (firing)
            attacker.StopCharging();

        // Thrash/Outrage: first use locks the creature in for 2–3 turns; the lock ticks after resolution.
        bool continuingLock = attacker.IsLocked;
        if (move.MultiTurnLock && !continuingLock)
            attacker.StartLock(moveIndex, _rng.Next(2, 4));

        // PP is spent only on the first turn — not while firing a charge or continuing a rampage lock.
        if (!firing && !continuingLock)
            move.UsePp();
        if (!continuingLock)
            ApplyChoiceLock(attacker, moveIndex);
        _log.Add(new MoveUsed(side, move.Move));
        attacker.RecordMoveUse(move.Move);

        // Protect: a move aimed at a shielded target is blocked outright (PP already spent).
        if (target.Protected && TargetsOpponent(move))
        {
            _log.Add(new MoveBlocked(side));
            ApplyCrashRecoil(attacker, side, move);
            return;
        }

        // OHKO uses a level-scaled accuracy in place of the move's own; accuracyBypass sure-hits.
        int? accuracy = move.Ohko ? EffectMath.OhkoAccuracy(attacker.Level, target.Level) : move.Accuracy;
        if (!move.BypassAccuracy && !BattleRolls.Hits(
                accuracy,
                attacker.Stage(StatKind.Accuracy),
                target.Stage(StatKind.Evasion),
                _rng))
        {
            _log.Add(new MoveMissed(side, move.Move));
            ApplyCrashRecoil(attacker, side, move);
            return;
        }

        int damageDealt = 0;
        if (move.CounterCategory is { } counterCat)
        {
            // Counter/Mirror Coat: return 2× the damage of that category taken this turn (no draw).
            int received = counterCat == DamageClass.Physical ? attacker.PhysicalDamageTaken : attacker.SpecialDamageTaken;
            if (received > 0)
            {
                int dmg = received * 2;
                damageDealt = DealMoveDamage(target, targetSide, dmg, 1.0, crit: false);
            }
            else
            {
                _log.Add(new MoveMissed(side, move.Move)); // nothing to counter → fizzles
            }
        }
        else if (move.Ohko || move.FixedDamage is not null || move.FixedDamageLevel)
        {
            // Formula-bypassing hit: no crit/STAB/roll (no RNG draws), but type immunity still voids it.
            double eff = _chart.Effectiveness(move.Type, target.Types);
            int dmg = eff <= 0 ? 0
                : move.Ohko ? target.CurrentHp
                : move.FixedDamageLevel ? attacker.Level
                : move.FixedDamage!.Value;
            damageDealt = DealMoveDamage(target, targetSide, dmg, eff, crit: false);
        }
        else if (move.Power is int power)
        {
            // Single-hit is a 1-iteration loop, so the crit→roll draw order is identical to before;
            // HitCount only draws for actual multi-hit moves. Each hit rolls crit/damage independently.
            int hits = move.MultiHitMax >= 2 ? EffectMath.HitCount(_rng, move.MultiHitMin, move.MultiHitMax) : 1;
            for (int h = 0; h < hits && !target.IsFainted; h++)
            {
                (int dmg, bool crit, double eff) = ComputeHit(side, attacker, target, move, power);
                int dealt = DealMoveDamage(target, targetSide, dmg, eff, crit);
                target.RecordDamageTaken(move.DamageClass, dealt); // for Counter/Mirror Coat
                damageDealt += dealt;
            }
        }

        if (damageDealt == 0)
            ApplyCrashRecoil(attacker, side, move);

        // Effect-list-driven resolution (EFFECT_TYPES_CATALOG): iterate the move's ordered effects and
        // dispatch each to a shared primitive. Order matches the historical pipeline (target secondaries,
        // then leech/drain/heal/recoil/crit/faint), so the RNG draw order is unchanged.
        var ctx = new EffectContext(move, attacker, side, target, targetSide, damageDealt);
        foreach (MoveEffect effect in move.SecondaryEffects)
            if (!ApplyEffect(ctx, effect))
                break;
        ApplyContactEffects(move, side, attacker, targetSide);
    }

    /// <summary>Confusion pre-move check: counts down, may snap out or force a self-hit.
    /// Returns false only when the creature hurt itself and loses the turn.</summary>
    private bool PushesThroughConfusion(BattleCreature c, BattleSide side)
    {
        if (!c.IsConfused)
            return true;

        c.TickConfusion();
        if (!c.IsConfused)
        {
            _log.Add(new ConfusionEnded(side));
            return true; // snapped out this turn — acts freely
        }

        if (!VolatileEffects.HitsSelfInConfusion(_rng))
            return true;

        int dmg = VolatileEffects.ConfusionSelfDamage(c.Level, c.Stats.Atk, c.Stats.Def);
        c.TakeDamage(dmg);
        _log.Add(new HurtInConfusion(side, dmg));
        if (c.IsFainted)
            _log.Add(new Fainted(side));
        return false;
    }

    /// <summary>Dispatches one compiled effect to its shared primitive (ResolvePrimitive,
    /// EFFECT_TYPES_CATALOG §4.1). Each primitive keeps the historical chance-roll semantics so draw
    /// order is preserved.</summary>
    private bool ApplyEffect(EffectContext ctx, MoveEffect effect)
    {
        switch (effect)
        {
            case AilmentEffect a: ApplyAilment(ctx, a); break;
            case StatChangeEffect s: ApplyStatChange(ctx, s); break;
            case StatChangeAllEffect s: ApplyStatChangeAll(ctx, s); break;
            case HpCostEffect h: return ApplyHpCost(ctx, h);
            case StatResetEffect r: ApplyStatReset(ctx, r); break;
            case StatCopyEffect copy: ApplyStatCopy(ctx, copy); break;
            case StatSwapEffect s: ApplyStatSwap(ctx, s); break;
            case StatInvertEffect i: ApplyStatInvert(ctx, i); break;
            case ConfusionEffect confusion: ApplyConfusion(ctx, confusion); break;
            case FlinchEffect f: ApplyFlinch(ctx, f); break;
            case LeechSeedEffect: ApplyLeechSeed(ctx); break;
            case BindEffect when !ctx.Target.IsFainted && !ctx.Target.IsTrapped:
                ctx.Target.SetTrap(_rng.Next(4, 6)); // partial trap lasts 4–5 turns
                _log.Add(new Bound(ctx.TargetSide));
                break;
            case ProtectEffect:
                if (VolatileEffects.ProtectSucceeds(ctx.Source.ProtectChain, _rng))
                {
                    ctx.Source.SetProtected();
                    _log.Add(new Protected(ctx.SourceSide));
                }
                else
                {
                    ctx.Source.ResetProtectChain();
                    _log.Add(new ProtectFailed(ctx.SourceSide));
                }
                break;
            case ForceSwitchEffect when !ctx.Target.IsFainted:
                ForceSwitch(ctx.TargetSide);
                break;
            case DrainEffect d when ctx.DamageDealt > 0:
                Heal(ctx.Source, ctx.SourceSide, EffectMath.DrainHeal(ctx.DamageDealt, d.Fraction.Num, d.Fraction.Den));
                break;
            case HealEffect h:
                (BattleCreature healRecipient, BattleSide healSide) = FractionRecipient(ctx, h.Recipient);
                if (!healRecipient.IsFainted)
                    Heal(healRecipient, healSide, EffectMath.HealAmount(healRecipient.MaxHp, h.Fraction.Num, h.Fraction.Den));
                break;
            case HpFractionEffect h:
                ApplyHpFraction(ctx, h);
                break;
            case StatusPowerEffect:
                break; // evaluated in ComputeHit before DamageCalc.
            case RecoilEffect r when ctx.DamageDealt > 0:
                Sap(ctx.Source, ctx.SourceSide, EffectMath.RecoilDamage(ctx.DamageDealt, r.Fraction.Num, r.Fraction.Den),
                    amt => new Recoiled(ctx.SourceSide, amt));
                break;
            case CritBoostEffect cb:
                ctx.Source.RaiseCrit(cb.Stages);
                _log.Add(new CritBoosted(ctx.SourceSide));
                break;
            case SelfDestructEffect when !ctx.Source.IsFainted:
                ctx.Source.TakeDamage(ctx.Source.MaxHp);
                _log.Add(new Fainted(ctx.SourceSide));
                break;
            case EntryHazardEffect:
                int layers = _spikeLayers[(int)ctx.TargetSide] = Math.Min(3, _spikeLayers[(int)ctx.TargetSide] + 1);
                _log.Add(new HazardSet(ctx.TargetSide, layers));
                break;
            case StealthRockEffect when !_stealthRock[(int)ctx.TargetSide]:
                _stealthRock[(int)ctx.TargetSide] = true;
                _log.Add(new StealthRockSet(ctx.TargetSide));
                break;
            case SetWeatherEffect w when w.Weather != _weather:
                SetWeather(w.Weather, WeatherConditions.DefaultTurns, ctx.SourceSide);
                break;
            case MoveGateEffect:
                break; // evaluated before PP/RNG in PassesMoveGates.
            case QueueActionGateEffect gate:
                QueueActionGate(new BattleSlot(ctx.SourceSide, 0), gate.Turns);
                break;
        }
        return true;
    }

    private void ApplyHpFraction(EffectContext ctx, HpFractionEffect effect)
    {
        (BattleCreature recipient, BattleSide side) = FractionRecipient(ctx, effect.Recipient);
        if (recipient.IsFainted)
            return;

        int amount = EffectMath.HpFractionAmount(recipient.CurrentHp, recipient.MaxHp, effect.Basis, effect.Fraction);
        if (effect.Operation == HpFractionOperation.Heal)
        {
            Heal(recipient, side, amount);
            return;
        }

        Sap(recipient, side, amount, damaged => new HpFractionDamaged(side, damaged));
    }

    private static (BattleCreature Creature, BattleSide Side) FractionRecipient(
        EffectContext ctx,
        HpFractionRecipient recipient) => recipient switch
    {
        HpFractionRecipient.Self => (ctx.Source, ctx.SourceSide),
        HpFractionRecipient.Target => (ctx.Target, ctx.TargetSide),
        _ => throw new ArgumentOutOfRangeException(nameof(recipient), recipient, "Unknown HP-fraction recipient."),
    };

    private bool PassesMoveGates(BattleCreature creature, BattleSide side, BattleMove move)
    {
        foreach (MoveGateEffect gate in move.SecondaryEffects.OfType<MoveGateEffect>())
        {
            MoveFailureReason? failure = gate.Kind switch
            {
                MoveGateKind.FirstAction when creature.ActionsSinceSwitch > 0 => MoveFailureReason.FirstActionOnly,
                MoveGateKind.NotPreviousMove when creature.LastMoveUsed == move.Move => MoveFailureReason.CannotRepeat,
                _ => null,
            };
            if (failure is { } reason)
            {
                _log.Add(new MoveFailed(side, move.Move, reason));
                return false;
            }
        }
        return true;
    }

    private void QueueActionGate(BattleSlot slot, int turns) =>
        _queuedActionGates.Add(new QueuedActionGate(slot, Turn + turns));

    private BattleAction ApplyQueuedActionGates(BattleSlot slot, BattleAction action)
    {
        int removed = _queuedActionGates.RemoveAll(gate => gate.Slot == slot && gate.DueTurn <= Turn);
        if (removed == 0)
            return action;

        _log.Add(new ActionSkipped(slot));
        return new Pass();
    }

    private static readonly EntityId RockType = EntityId.Parse("type:rock");

    /// <summary>on_switch_in hook (catalog §7.3): a creature entering a side with entry hazards takes
    /// damage — Stealth Rock (type-scaled) before Spikes (layer-scaled). Draws no RNG. Battle-start
    /// actives never trigger it (no hazards yet).</summary>
    private void OnSwitchIn(BattleSide side)
    {
        BattleCreature c = Active(side);
        if (c.IsFainted)
            return;

        ReevaluateConditionForm(side);
        ApplyHookInvocations(BattleHookDispatcher.SwitchIn(side, HookSources()));

        if (_stealthRock[(int)side])
        {
            double eff = _chart.Effectiveness(RockType, c.Types);
            Sap(c, side, EffectMath.TypeScaledHazardDamage(c.MaxHp, eff), amt => new HurtByHazard(side, amt));
        }
        if (!c.IsFainted && _spikeLayers[(int)side] > 0)
            Sap(c, side, EffectMath.HazardDamage(c.MaxHp, _spikeLayers[(int)side]), amt => new HurtByHazard(side, amt));
    }

    /// <summary>Whether a move acts on the opposing creature (so Protect can block it). Damage or any
    /// target-directed secondary counts; self-buffs, heals, weather, and side hazards do not.</summary>
    private static bool TargetsOpponent(BattleMove move) =>
        move.Target is MoveTarget.Selected or MoveTarget.AllOpponents or MoveTarget.AllOtherPokemon
        && (move.Power.HasValue || move.SecondaryEffects.Any(e =>
            e is AilmentEffect or ConfusionEffect or FlinchEffect or LeechSeedEffect or BindEffect or ForceSwitchEffect
            || (e is StatChangeEffect s && !s.OnSelf)
            || (e is StatChangeAllEffect a && !a.OnSelf)
            || (e is StatResetEffect r && r.Scope != StageEffectScope.Self)
            || (e is StatCopyEffect c && (c.From == StageEffectScope.Target || c.To == StageEffectScope.Target))
            || e is StatSwapEffect
            || (e is StatInvertEffect i && !i.OnSelf)));

    /// <summary>Roar/Whirlwind: a wild target flees (battle ends); a trainer's is dragged out to a random
    /// healthy reserve. No reserve → no effect.</summary>
    private void ForceSwitch(BattleSide side)
    {
        if (IsWild && side == BattleSide.Enemy)
        {
            _log.Add(new ForcedOut(side));
            EndBattle(Opponent(side)); // scared the wild creature off
            return;
        }

        var reserves = new List<int>();
        for (int i = 0; i < _parties[(int)side].Count; i++)
            if (!_activeSlots.IsActive(side, i) && !_parties[(int)side][i].IsFainted)
                reserves.Add(i);
        if (reserves.Count == 0)
            return; // nothing to drag out

        _log.Add(new ForcedOut(side));
        SwitchTo(side, reserves[_rng.Next(reserves.Count)]);
    }

    private void ApplyLeechSeed(EffectContext ctx)
    {
        if (ctx.Target.IsFainted || ctx.Target.Seeded)
            return;
        if (ctx.Target.Types.Contains(ctx.Move.Type)) // a seed of its own type can't take hold (grass immune to grass Leech Seed)
            return;
        ctx.Target.SetSeeded(true);
        _log.Add(new LeechSeeded(ctx.TargetSide));
    }

    private void ApplyAilment(EffectContext ctx, AilmentEffect effect)
    {
        if (ctx.Target.IsFainted)
            return;
        if (!StatusEffects.CanApplyStatus(ctx.Target.Status) || StatusEffects.TypeImmuneToStatus(effect.Status, ctx.Target.Types))
            return;
        if (BlocksStatus(ctx.TargetSide, effect.Status))
            return;
        if (_rng.Next(100) < effect.Chance)
        {
            ctx.Target.SetStatus(effect.Status);
            _log.Add(new StatusApplied(ctx.TargetSide, effect.Status));
        }
    }

    private void ApplyStatChange(EffectContext ctx, StatChangeEffect effect)
    {
        if (effect.Chance < 100 && _rng.Next(100) >= effect.Chance)
            return;
        BattleCreature recipient = effect.OnSelf ? ctx.Source : ctx.Target;
        BattleSide recipientSide = effect.OnSelf ? ctx.SourceSide : ctx.TargetSide;
        if (recipient.IsFainted)
            return;
        recipient.ChangeStage(effect.Stat, effect.Delta);
        _log.Add(new StatStageChanged(recipientSide, effect.Stat, effect.Delta));
    }

    private static readonly StatKind[] AllStageStats =
        [StatKind.Atk, StatKind.Def, StatKind.Spa, StatKind.Spd, StatKind.Spe];
    private static readonly StatKind[] AllStageSlots =
        [StatKind.Atk, StatKind.Def, StatKind.Spa, StatKind.Spd, StatKind.Spe, StatKind.Accuracy, StatKind.Evasion];
    private static readonly StatKind[] OffenseStageStats = [StatKind.Atk, StatKind.Spa];
    private static readonly StatKind[] DefenseStageStats = [StatKind.Def, StatKind.Spd];

    private void ApplyStatChangeAll(EffectContext ctx, StatChangeAllEffect effect)
    {
        if (effect.Chance < 100 && _rng.Next(100) >= effect.Chance)
            return;
        BattleCreature recipient = effect.OnSelf ? ctx.Source : ctx.Target;
        BattleSide recipientSide = effect.OnSelf ? ctx.SourceSide : ctx.TargetSide;
        if (recipient.IsFainted)
            return;
        foreach (StatKind stat in AllStageStats)
        {
            recipient.ChangeStage(stat, effect.Delta);
            _log.Add(new StatStageChanged(recipientSide, stat, effect.Delta));
        }
    }

    private bool ApplyHpCost(EffectContext ctx, HpCostEffect effect)
    {
        int amount = Math.Max(1, ctx.Source.MaxHp * effect.Fraction.Num / effect.Fraction.Den);
        if (!effect.AllowFaint && ctx.Source.CurrentHp <= amount)
            return false;
        Sap(ctx.Source, ctx.SourceSide, amount, amt => new HpCostPaid(ctx.SourceSide, amt));
        return !ctx.Source.IsFainted;
    }

    private void ApplyStatReset(EffectContext ctx, StatResetEffect effect)
    {
        if (!ChancePasses(effect.Chance))
            return;
        foreach ((BattleCreature creature, BattleSide side) in StageRecipients(ctx, effect.Scope))
            foreach (StatKind stat in AllStageSlots)
                SetStage(creature, side, stat, 0);
    }

    private void ApplyStatCopy(EffectContext ctx, StatCopyEffect effect)
    {
        if (!ChancePasses(effect.Chance))
            return;
        (BattleCreature from, _) = StageRecipient(ctx, effect.From);
        (BattleCreature to, BattleSide toSide) = StageRecipient(ctx, effect.To);
        foreach (StatKind stat in AllStageSlots)
            SetStage(to, toSide, stat, from.Stage(stat));
    }

    private void ApplyStatSwap(EffectContext ctx, StatSwapEffect effect)
    {
        if (!ChancePasses(effect.Chance))
            return;
        foreach (StatKind stat in SwapStats(effect.Group))
        {
            int sourceStage = ctx.Source.Stage(stat);
            int targetStage = ctx.Target.Stage(stat);
            SetStage(ctx.Source, ctx.SourceSide, stat, targetStage);
            SetStage(ctx.Target, ctx.TargetSide, stat, sourceStage);
        }
    }

    private void ApplyStatInvert(EffectContext ctx, StatInvertEffect effect)
    {
        if (!ChancePasses(effect.Chance))
            return;
        BattleCreature recipient = effect.OnSelf ? ctx.Source : ctx.Target;
        BattleSide recipientSide = effect.OnSelf ? ctx.SourceSide : ctx.TargetSide;
        foreach (StatKind stat in AllStageSlots)
            SetStage(recipient, recipientSide, stat, -recipient.Stage(stat));
    }

    private void SetStage(BattleCreature creature, BattleSide side, StatKind stat, int value)
    {
        if (creature.IsFainted)
            return;
        int before = creature.Stage(stat);
        creature.SetStage(stat, value);
        int delta = creature.Stage(stat) - before;
        if (delta != 0)
            _log.Add(new StatStageChanged(side, stat, delta));
    }

    private bool ChancePasses(int chance) => chance >= 100 || _rng.Next(100) < chance;

    private static IEnumerable<(BattleCreature Creature, BattleSide Side)> StageRecipients(EffectContext ctx, StageEffectScope scope)
    {
        if (scope is StageEffectScope.Self or StageEffectScope.Both)
            yield return (ctx.Source, ctx.SourceSide);
        if (scope is StageEffectScope.Target or StageEffectScope.Both)
            yield return (ctx.Target, ctx.TargetSide);
    }

    private static (BattleCreature Creature, BattleSide Side) StageRecipient(EffectContext ctx, StageEffectScope scope) =>
        scope switch
        {
            StageEffectScope.Self => (ctx.Source, ctx.SourceSide),
            StageEffectScope.Target => (ctx.Target, ctx.TargetSide),
            _ => throw new ArgumentException("Stage copy endpoints must be self or target."),
        };

    private static IReadOnlyList<StatKind> SwapStats(StageSwapGroup group) => group switch
    {
        StageSwapGroup.All => AllStageSlots,
        StageSwapGroup.Offense => OffenseStageStats,
        StageSwapGroup.Defense => DefenseStageStats,
        _ => throw new ArgumentException($"Unknown stage swap group '{group}'."),
    };

    private void ApplyConfusion(EffectContext ctx, ConfusionEffect effect)
    {
        if (ctx.Target.IsFainted || ctx.Target.IsConfused)
            return;
        if (_rng.Next(100) < effect.Chance)
        {
            ctx.Target.SetConfusion(VolatileEffects.ConfusionDuration(_rng));
            _log.Add(new Confused(ctx.TargetSide));
        }
    }

    private void ApplyFlinch(EffectContext ctx, FlinchEffect effect)
    {
        // Flinch only bites if the target hasn't acted yet — resolution order gives us that for free.
        if (!ctx.Target.IsFainted && VolatileEffects.Flinches(effect.Chance, _rng))
            ctx.Target.SetFlinch();
    }

    private void ApplyContactEffects(BattleMove move, BattleSide sourceSide, BattleCreature source, BattleSide targetSide)
    {
        if (!move.MakesContact || source.IsFainted)
            return;

        foreach (BattleHookInvocation invocation in BattleHookDispatcher.ContactReceived(targetSide, HookSources()))
        {
            Effect effect = invocation.Effect;
            if (effect.Op != "contactChanceEffect" || _rng.Next(100) >= (effect.Chance ?? 100))
                continue;

            string statusName = Str(effect, "status");
            if (statusName.Length > 0)
            {
                PersistentStatus status = Parse<PersistentStatus>(statusName);
                if (StatusEffects.CanApplyStatus(source.Status)
                    && !StatusEffects.TypeImmuneToStatus(status, source.Types)
                    && !BlocksStatus(sourceSide, status))
                {
                    source.SetStatus(status);
                    _log.Add(new StatusApplied(sourceSide, status));
                }
                continue;
            }

            string statName = Str(effect, "stat");
            if (statName.Length > 0)
            {
                StatKind stat = Parse<StatKind>(statName);
                int delta = Int(effect, "delta") ?? 0;
                source.ChangeStage(stat, delta);
                _log.Add(new StatStageChanged(sourceSide, stat, delta));
            }
        }
    }

    // ---- Reusable effect primitives (shared by every op that heals or saps HP) ----

    /// <summary>Restore HP and log it. No-op if the creature is full or the amount is ≤0. Used by
    /// drain, healFraction, and Leech Seed's beneficiary.</summary>
    private void Heal(BattleCreature c, BattleSide side, int amount)
    {
        if (amount <= 0 || c.CurrentHp >= c.MaxHp)
            return;
        int before = c.CurrentHp;
        c.Heal(amount);
        _log.Add(new Healed(side, c.CurrentHp - before));
    }

    /// <summary>Deal non-move HP loss (recoil/crash/leech), log it via the supplied event factory, and
    /// record a faint. Used by recoil, crash-on-miss, and Leech Seed's victim.</summary>
    private void Sap(BattleCreature c, BattleSide side, int amount, Func<int, BattleEvent> lossEvent)
    {
        if (amount <= 0)
            return;
        c.TakeDamage(amount);
        _log.Add(lossEvent(amount));
        if (c.IsFainted)
            _log.Add(new Fainted(side));
    }

    private void ApplyCrashRecoil(BattleCreature attacker, BattleSide side, BattleMove move)
    {
        if (move.Recoil is not { } crash || !move.RecoilOnMiss)
            return;
        Sap(attacker, side, EffectMath.CrashDamage(attacker.MaxHp, crash.Num, crash.Den), amt => new Recoiled(side, amt));
    }

    /// <summary>Sap HP from a victim and heal it to a beneficiary — the shared "drain life" primitive
    /// behind Leech Seed (Absorb/Mega Drain use <see cref="Heal"/> against damage already dealt).</summary>
    private void DrainLife(BattleCreature victim, BattleSide victimSide, BattleCreature beneficiary, BattleSide beneficiarySide, int amount)
    {
        Sap(victim, victimSide, amount, amt => new LeechSapped(victimSide, amt));
        Heal(beneficiary, beneficiarySide, amount);
    }

    /// <summary>One hit of a damaging move — draws crit then damage roll (fixed order), applies
    /// crit's stat-stage ignore rule and burn. Returned <c>eff</c> feeds the DamageDealt event.</summary>
    private int DealMoveDamage(BattleCreature target, BattleSide targetSide, int amount, double effectiveness, bool crit)
    {
        int before = target.CurrentHp;
        amount = ApplySurviveFromFull(target, targetSide, amount);
        target.TakeDamage(amount);
        int dealt = before - target.CurrentHp;
        _log.Add(new DamageDealt(targetSide, dealt, effectiveness, crit));
        if (target.IsFainted)
            _log.Add(new Fainted(targetSide));
        return dealt;
    }

    private int ApplySurviveFromFull(BattleCreature target, BattleSide targetSide, int amount)
    {
        if (amount < target.CurrentHp || target.CurrentHp != target.MaxHp || target.HasConsumedHeldEffect("surviveFromFull"))
            return amount;
        if (!target.HeldItemBattleEffects.Any(e => e.Op == "surviveFromFull"))
            return amount;

        target.ConsumeHeldEffect("surviveFromFull");
        _log.Add(new HeldItemConsumed(targetSide, "surviveFromFull"));
        ReevaluateConditionForms();
        return Math.Max(0, target.CurrentHp - 1);
    }

    private (int Dmg, bool Crit, double Eff) ComputeHit(BattleSide side, BattleCreature attacker, BattleCreature target, BattleMove move, int power)
    {
        if (move.TargetHpThresholdPower is { } hpPower)
            power = EffectMath.TargetHpThresholdPower(
                power,
                target.CurrentHp,
                target.MaxHp,
                hpPower.Threshold.Num,
                hpPower.Threshold.Den,
                hpPower.Multiplier.Num,
                hpPower.Multiplier.Den);
        if (move.HpRatioPower is { } ratioPower)
        {
            BattleCreature ratioSource = ratioPower.Source == HpRatioPowerSource.User ? attacker : target;
            power = EffectMath.HpRatioPower(power, ratioSource.CurrentHp, ratioSource.MaxHp);
        }

        bool ignoreSourceBurnPenalty = false;
        if (move.SecondaryEffects.OfType<StatusPowerEffect>().SingleOrDefault() is { } statusPower)
        {
            BattleCreature statusSubject = statusPower.Subject == StatusPowerSubject.User ? attacker : target;
            bool conditionMet = statusSubject.Status is { } status
                && (statusPower.Status is null || statusPower.Status == status);
            power = EffectMath.StatusPower(power, conditionMet, statusPower.Multiplier);
            ignoreSourceBurnPenalty = conditionMet && statusPower.IgnoreSourceBurnPenalty;
        }

        bool physical = move.DamageClass == DamageClass.Physical;
        bool crit = BattleRolls.IsCrit(move.CritStage + attacker.CritStageBonus, _rng);
        int roll = BattleRolls.DamageRoll(_rng);

        StatKind offStat = move.OffensiveStatOverride ?? (physical ? StatKind.Atk : StatKind.Spa);
        StatKind defStat = move.DefensiveStatOverride ?? (physical ? StatKind.Def : StatKind.Spd);
        int aStage = attacker.Stage(offStat);
        int dStage = target.Stage(defStat);
        if (crit)
        {
            aStage = Math.Max(0, aStage);
            dStage = Math.Min(0, dStage);
        }

        int a = ModifiedBattleStat(side, side, offStat,
            (int)(StatValue(attacker.Stats, offStat) * StatStages.Multiplier(aStage)));
        int d = ModifiedBattleStat(side, Opponent(side), defStat,
            Math.Max(1, (int)(StatValue(target.Stats, defStat) * StatStages.Multiplier(dStage))));
        double eff = _chart.Effectiveness(move.Type, target.Types);
        double stab = TypeChart.Stab(move.Type, attacker.Types);
        bool burn = attacker.Status == PersistentStatus.Burn && physical && !ignoreSourceBurnPenalty;

        int dmg = DamageCalc.Compute(attacker.Level, power, a, d, eff, stab, crit, roll, burn);
        double hooks = HookDamageMultiplier(move, side);
        dmg = (int)(dmg * hooks * WeatherConditions.DamageMultiplier(_weather, move.Type.Slug)); // on_damage_query modifiers
        return (dmg, crit, eff);
    }

    private static int StatValue(Stats stats, StatKind stat) => stat switch
    {
        StatKind.Atk => stats.Atk,
        StatKind.Def => stats.Def,
        StatKind.Spa => stats.Spa,
        StatKind.Spd => stats.Spd,
        _ => throw new ArgumentException($"Stat {stat} is not valid for damage calculation."),
    };

    private IEnumerable<BattleHookSource> HookSources()
    {
        foreach (BattleSide side in new[] { BattleSide.Player, BattleSide.Enemy })
        {
            BattleCreature c = Active(side);
            foreach (AbilityHook hook in c.AbilityHooks)
                yield return new BattleHookSource(side, BattleHookSourceKind.Ability, hook.Hook, hook.Effects);
            foreach (var group in c.HeldItemBattleEffects.Select(e => (Hook: HeldHook(e), Effect: e)).Where(x => x.Hook is not null).GroupBy(x => x.Hook!.Value))
                yield return new BattleHookSource(side, BattleHookSourceKind.HeldItem, group.Key, group.Select(x => x.Effect).ToList());
        }
    }

    private void ApplyHookInvocations(IEnumerable<BattleHookInvocation> invocations)
    {
        foreach (BattleHookInvocation invocation in invocations)
        {
            if (invocation.Effect.Op == "weatherSummon")
            {
                int turns = Int(invocation.Effect, "duration") ?? WeatherConditions.DefaultTurns;
                SetWeather(Parse<Weather>(Str(invocation.Effect, "weather")), turns + WeatherDurationExtension(invocation.Side), invocation.Side);
            }
        }
    }

    private int WeatherDurationExtension(BattleSide side) =>
        Active(side).HeldItemBattleEffects
            .Where(e => e.Op == "weatherDurationExtend")
            .Sum(e => Int(e, "turns") ?? 0);

    private static void ApplyChoiceLock(BattleCreature creature, int moveIndex)
    {
        if (creature.HeldItemBattleEffects.Any(e => e.Op == "choiceLock"))
            creature.SetChoiceLock(moveIndex);
    }

    private double HookDamageMultiplier(BattleMove move, BattleSide side)
    {
        double multiplier = 1.0;
        foreach (BattleHookInvocation invocation in BattleHookDispatcher.Damage(side, HookSources()))
        {
            Effect effect = invocation.Effect;
            if (effect.Op is "typeDamageModify" or "typeDamageBoost")
            {
                if (!string.Equals(Str(effect, "type"), move.Type.Slug, StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            else if (effect.Op == "choiceLock")
            {
                if (!Enum.TryParse(Str(effect, "damageClass"), ignoreCase: true, out DamageClass damageClass)
                    || damageClass != move.DamageClass)
                    continue;
            }
            else
            {
                continue;
            }
            multiplier *= (Int(effect, "multiplierPercent") ?? 100) / 100.0;
        }
        return multiplier;
    }

    private int ModifiedBattleStat(BattleSide attackerSide, BattleSide statOwnerSide, StatKind stat, int value)
    {
        double multiplier = 1.0;
        int add = 0;
        foreach (BattleHookInvocation invocation in BattleHookDispatcher.Damage(attackerSide, HookSources()))
        {
            Effect effect = invocation.Effect;
            if (invocation.Side != statOwnerSide || effect.Op != "statModify")
                continue;
            if (!string.Equals(Str(effect, "stat"), StatSlug(stat), StringComparison.OrdinalIgnoreCase))
                continue;

            multiplier *= (Int(effect, "multiplierPercent") ?? 100) / 100.0;
            add += Int(effect, "add") ?? 0;
        }
        return Math.Max(1, (int)(value * multiplier) + add);
    }

    private static string StatSlug(StatKind stat) => stat switch
    {
        StatKind.Atk => "atk",
        StatKind.Def => "def",
        StatKind.Spa => "spa",
        StatKind.Spd => "spd",
        StatKind.Spe => "spe",
        StatKind.Accuracy => "accuracy",
        StatKind.Evasion => "evasion",
        _ => stat.ToString().ToLowerInvariant(),
    };

    private bool BlocksStatus(BattleSide target, PersistentStatus status) =>
        BattleHookDispatcher.StatusAttempt(target, HookSources())
            .Any(i => i.Effect.Op == "statusImmunity"
                && string.Equals(Str(i.Effect, "status"), status.ToString(), StringComparison.OrdinalIgnoreCase));

    private void SetWeather(Weather weather, int turns, BattleSide sourceSide)
    {
        if (weather == Weather.None)
            return;
        bool changed = _weather != weather;
        _weather = weather;
        _weatherTurns = Math.Max(1, turns);
        if (!changed)
            return;
        _log.Add(new WeatherChanged(weather));
        ReevaluateConditionForms();
        if (_dispatchingWeatherChange)
            return;

        _dispatchingWeatherChange = true;
        try
        {
            ApplyHookInvocations(BattleHookDispatcher.WeatherChange(sourceSide, HookSources()));
        }
        finally
        {
            _dispatchingWeatherChange = false;
        }
    }

    private static AbilityHookPoint? HeldHook(Effect effect) => effect.Op switch
    {
        "typeDamageBoost" => AbilityHookPoint.OnModifyOutgoingDamage,
        "choiceLock" => AbilityHookPoint.OnModifyOutgoingDamage,
        "thresholdHeal" => AbilityHookPoint.OnEndOfTurn,
        "statusCure" => AbilityHookPoint.OnEndOfTurn,
        "residualHeal" => AbilityHookPoint.OnEndOfTurn,
        _ => null,
    };

    private static string Str(Effect effect, string key) =>
        effect.Params is not null && effect.Params.TryGetValue(key, out var value)
            ? value.GetString() ?? ""
            : "";

    private static int? Int(Effect effect, string key) =>
        effect.Params is not null && effect.Params.TryGetValue(key, out var value) && value.TryGetInt32(out int n)
            ? n
            : null;

    private static T Parse<T>(string value) where T : struct, Enum =>
        Enum.TryParse(value, ignoreCase: true, out T parsed)
            ? parsed
            : throw new ArgumentException($"Unknown {typeof(T).Name} '{value}'.");

    private bool CanAct(BattleCreature c, BattleSide side)
    {
        switch (c.Status)
        {
            case PersistentStatus.Freeze:
                if (StatusEffects.Thaws(_rng)) { c.ClearStatus(); _log.Add(new Thawed(side)); return true; }
                _log.Add(new StillFrozen(side));
                return false;

            case PersistentStatus.Sleep:
                if (c.StatusCounter > 0) { c.TickSleep(); _log.Add(new StillAsleep(side)); return false; }
                c.ClearStatus();
                _log.Add(new WokeUp(side));
                return true;

            case PersistentStatus.Paralysis:
                if (StatusEffects.FullyParalyzed(_rng)) { _log.Add(new FullyParalyzed(side)); return false; }
                return true;

            default:
                return true;
        }
    }

    private void ResolveCapture(ThrowBall ball)
    {
        BattleCreature target = Active(BattleSide.Enemy);
        _log.Add(new BallThrown());

        CaptureResult result = CaptureCalc.Attempt(
            target.MaxHp, target.CurrentHp, target.CatchRate, ball.BallBonus, ball.StatusBonus, _rng);
        _log.Add(new CaptureShakes(result.Shakes));

        if (result.Caught)
        {
            Captured = true;
            _log.Add(new Captured(BattleSide.Enemy));
            EndBattle(BattleSide.Player);
        }
        else
        {
            _log.Add(new BrokeFree());
        }
    }

    private void EndOfTurn()
    {
        foreach (BattleSide side in (ReadOnlySpan<BattleSide>)[BattleSide.Player, BattleSide.Enemy])
        {
            BattleCreature c = Active(side);
            if (c.IsFainted || c.Status is not { } status)
                continue;

            int dmg = StatusEffects.ResidualDamage(status, c.MaxHp, c.StatusCounter);
            if (dmg > 0)
            {
                c.TakeDamage(dmg);
                _log.Add(new StatusDamage(side, dmg));
                if (c.IsFainted)
                    _log.Add(new Fainted(side));
            }
            c.AdvanceToxic();
        }

        // Leech Seed: each seeded active is sapped and the opposing active recovers that HP.
        foreach (BattleSide side in (ReadOnlySpan<BattleSide>)[BattleSide.Player, BattleSide.Enemy])
        {
            BattleCreature c = Active(side);
            if (!c.IsFainted && c.Seeded)
                DrainLife(c, side, Active(Opponent(side)), Opponent(side), Math.Max(1, c.MaxHp / 8));
        }

        // Partial trap (Bind/Wrap/…): residual chip while trapped, then count down and release.
        foreach (BattleSide side in (ReadOnlySpan<BattleSide>)[BattleSide.Player, BattleSide.Enemy])
        {
            BattleCreature c = Active(side);
            if (c.IsFainted || !c.IsTrapped)
                continue;
            Sap(c, side, Math.Max(1, c.MaxHp / 8), amt => new BoundHurt(side, amt));
            c.TickTrap();
            if (!c.IsTrapped)
                _log.Add(new BindReleased(side));
        }

        ApplyEndOfTurnHooks();
        WeatherTurnEnd();
        TickTimedForms();
    }

    private void ApplyEndOfTurnHooks()
    {
        foreach (BattleSide side in (ReadOnlySpan<BattleSide>)[BattleSide.Player, BattleSide.Enemy])
            foreach (BattleHookInvocation invocation in BattleHookDispatcher.EndOfTurn(side, HookSources()))
                ApplyEndOfTurnHook(side, invocation.Effect);
    }

    private void ApplyEndOfTurnHook(BattleSide side, Effect effect)
    {
        BattleCreature c = Active(side);
        if (c.IsFainted)
            return;

        if (effect.Op == "residualHeal")
        {
            Heal(c, side, FractionAmount(c, effect));
            return;
        }

        if (effect.Op == "residualDamage")
        {
            Sap(c, side, FractionAmount(c, effect), amt => new ResidualDamage(side, amt));
            return;
        }

        if (effect.Op == "statusCure" && !c.HasConsumedHeldEffect(effect.Op) && c.Status is { } status
            && string.Equals(Str(effect, "status"), status.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            c.ConsumeHeldEffect(effect.Op);
            _log.Add(new HeldItemConsumed(side, effect.Op));
            c.ClearStatus();
            _log.Add(new StatusCured(side, status));
            ReevaluateConditionForms();
            return;
        }

        if (effect.Op == "thresholdHeal" && !c.HasConsumedHeldEffect(effect.Op))
        {
            int threshold = Int(effect, "thresholdPercent") ?? 50;
            if (c.CurrentHp * 100 > c.MaxHp * threshold)
                return;

            int amount = Int(effect, "healAmount")
                ?? (Int(effect, "healFractionPercent") is { } percent ? Math.Max(1, c.MaxHp * percent / 100) : 0);
            if (amount <= 0 || c.CurrentHp >= c.MaxHp)
                return;

            c.ConsumeHeldEffect(effect.Op);
            _log.Add(new HeldItemConsumed(side, effect.Op));
            Heal(c, side, Math.Min(amount, c.MaxHp - c.CurrentHp));
            ReevaluateConditionForms();
        }
    }

    private static int FractionAmount(BattleCreature c, Effect effect)
    {
        int num = Int(effect, "num") ?? 1;
        int den = Int(effect, "den") ?? 16;
        return Math.Max(1, c.MaxHp * num / den);
    }

    /// <summary>on_turn_end for the weather field condition: residual chip to non-immune actives, then
    /// the duration counts down and expires. Draws no RNG.</summary>
    private void WeatherTurnEnd()
    {
        if (_weather == Weather.None)
            return;

        WeatherDef def = WeatherConditions.For(_weather);
        if (def.ResidualDenominator > 0)
        {
            foreach (BattleSide side in (ReadOnlySpan<BattleSide>)[BattleSide.Player, BattleSide.Enemy])
            {
                BattleCreature c = Active(side);
                if (c.IsFainted || c.Types.Any(t => def.ResidualImmuneTypes.Contains(t.Slug)))
                    continue;
                Sap(c, side, Math.Max(1, c.MaxHp / def.ResidualDenominator), amt => new WeatherDamage(side, amt));
            }
        }

        if (--_weatherTurns <= 0)
        {
            _log.Add(new WeatherEnded(_weather));
            _weather = Weather.None;
            ReevaluateConditionForms();
        }
    }

    private void ReevaluateConditionForms()
    {
        foreach (BattleSide side in (ReadOnlySpan<BattleSide>)[BattleSide.Player, BattleSide.Enemy])
            ReevaluateConditionForm(side);
    }

    private void ReevaluateConditionForm(BattleSide side)
    {
        (bool changed, string? formId) = Active(side).ReevaluateConditionForm(_weather);
        if (changed)
            _log.Add(new FormChanged(side, formId));
    }

    private void TickTimedForms()
    {
        foreach (BattleSide side in (ReadOnlySpan<BattleSide>)[BattleSide.Player, BattleSide.Enemy])
            if (Active(side).TickTimedForm())
            {
                _log.Add(new FormChanged(side, null));
                ReevaluateConditionForm(side);
            }
    }

    private void RevertFaintedBattleForms()
    {
        foreach (BattleSide side in (ReadOnlySpan<BattleSide>)[BattleSide.Player, BattleSide.Enemy])
            foreach (BattleCreature creature in _parties[(int)side])
                if (creature.RevertActiveBattleFormIfFainted())
                    _log.Add(new FormChanged(side, null));
    }

    private void RevertBattleEndForms()
    {
        foreach (BattleSide side in (ReadOnlySpan<BattleSide>)[BattleSide.Player, BattleSide.Enemy])
            foreach (BattleCreature creature in _parties[(int)side])
                if (creature.RevertActiveBattleForm())
                    _log.Add(new FormChanged(side, null));
    }

    /// <summary>After a faint, auto-switch a side to its first healthy reserve.</summary>
    // ponytail: auto-picks the first reserve; a UI-driven forced-switch choice is a later refinement.
    private void AutoReplaceFainted()
    {
        foreach (BattleSide side in (ReadOnlySpan<BattleSide>)[BattleSide.Player, BattleSide.Enemy])
        {
            if (!Active(side).IsFainted)
                continue;
            int next = _parties[(int)side].FindIndex(c => !c.IsFainted);
            if (next >= 0)
                SwitchTo(side, next);
        }
    }

    private void CheckEnd()
    {
        if (Outcome is not null)
            return;

        bool playerDown = _parties[0].All(c => c.IsFainted);
        bool enemyDown = _parties[1].All(c => c.IsFainted);
        if (!playerDown && !enemyDown)
            return;

        BattleSide winner = playerDown ? BattleSide.Enemy : BattleSide.Player;
        EndBattle(winner);
    }

    private void EndBattle(BattleSide winner)
    {
        Outcome = new BattleOutcome(winner);
        RevertBattleEndForms();
        _log.Add(new BattleEnded(winner));
    }

    private void AssignInitial(BattleSide side, IReadOnlyList<int> partyIndexes)
    {
        foreach ((int partyIndex, int position) in partyIndexes.Select((index, position) => (index, position)))
        {
            if (partyIndex < 0 || partyIndex >= _parties[(int)side].Count)
                throw new ArgumentOutOfRangeException(nameof(partyIndexes), $"{side} party index {partyIndex} is out of range.");
            if (_parties[(int)side][partyIndex].IsFainted)
                throw new ArgumentException($"{side} cannot start with a fainted party member.", nameof(partyIndexes));
            _activeSlots.Assign(new BattleSlot(side, position), partyIndex);
        }
    }

    private void RequireSinglesAdapter()
    {
        if (Topology.ActiveSlotsPerSide != 1)
            throw new InvalidOperationException("This side-only API is a singles adapter. Use the slot-addressed API in doubles.");
    }

    private static BattleSide Opponent(BattleSide s) => s == BattleSide.Player ? BattleSide.Enemy : BattleSide.Player;
}
