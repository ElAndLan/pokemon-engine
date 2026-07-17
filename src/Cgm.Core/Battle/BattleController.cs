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
    private sealed record AdmittedAction(BattleActionSubmission Submission, int ActorPartyIndex, EntityId? MoveId);
    private readonly record struct DamageApplication(int AppliedDamage, int ActualHpRemoved);

    private readonly List<BattleCreature>[] _parties;
    private readonly BattleActiveSlots _activeSlots;
    private readonly Dictionary<EntityId, int>[] _itemStock = [[], []];
    private readonly bool[] _temporaryFormUsed = [false, false];
    private readonly int[] _spikeLayers = [0, 0];    // entry-hazard side condition (catalog §7.3), per side
    private readonly bool[] _stealthRock = [false, false]; // type-scaled entry hazard, per side
    private readonly TypeChart _chart;
    private readonly IRng _rng;
    private readonly IReadOnlyDictionary<EntityId, Item> _itemData;
    private readonly List<BattleEvent> _log = [];
    private readonly List<EffectTraceEntry> _trace = [];
    private readonly List<BattleQueryTraceEntry> _queryTrace = [];
    private readonly List<RedirectCondition> _redirects = [];
    private readonly BattleIntentQueue _intentQueue = new();
    private readonly BattleOverlayStore _overlays = new();
    private readonly BattleActionHistory _actionHistory = new();
    private readonly BattleConditionStores _conditions =
        new(new BattleConditionRegistry(WeatherConditions.Definitions));
    private readonly List<BattleConditionTraceEntry> _conditionTrace = [];
    private readonly List<BattleHookTraceEntry> _hookTrace = [];
    private readonly List<BattleSlot> _pendingReplacementSlots = [];
    private bool _dispatchingWeatherChange;
    private int _traceActionSequence;

    public BattleController(BattleCreature player, BattleCreature enemy, TypeChart chart, IRng rng,
        bool isWild = false, IEnumerable<Item>? itemData = null)
        : this([player], [enemy], chart, rng, isWild, itemData) { }

    public BattleController(IReadOnlyList<BattleCreature> playerParty, IReadOnlyList<BattleCreature> enemyParty,
        TypeChart chart, IRng rng, bool isWild = false, IEnumerable<Item>? itemData = null)
    {
        _parties = [[.. playerParty], [.. enemyParty]];
        _activeSlots = new BattleActiveSlots(BattleTopology.Singles);
        _activeSlots.Assign(new BattleSlot(BattleSide.Player, 0), 0);
        _activeSlots.Assign(new BattleSlot(BattleSide.Enemy, 0), 0);
        _chart = chart;
        _rng = rng;
        _itemData = (itemData ?? []).ToDictionary(item => item.Id);
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
        bool isWild = false,
        IEnumerable<Item>? itemData = null)
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
        _itemData = (itemData ?? []).ToDictionary(item => item.Id);
        IsWild = isWild;
    }

    public int Turn { get; private set; }
    public bool IsWild { get; }
    public bool Captured { get; private set; }
    public BattleOutcome? Outcome { get; private set; }
    public IReadOnlyList<BattleEvent> Log => _log;
    public IReadOnlyList<EffectTraceEntry> Trace => _trace;
    public IReadOnlyList<BattleQueryTraceEntry> QueryTrace => _queryTrace;
    public IReadOnlyList<BattleIntentDebugEntry> IntentQueueSnapshot => _intentQueue.DebugSnapshot();
    public IReadOnlyList<BattleSlot> PendingReplacementSlots => _pendingReplacementSlots;
    public BattleOverlayStore Overlays => _overlays;
    public BattleActionHistory ActionHistory => _actionHistory;
    public IReadOnlyList<BattleConditionInstance> ConditionSnapshot => _conditions.Snapshot();
    public IReadOnlyList<BattleConditionTraceEntry> ConditionTrace => _conditionTrace;
    public IReadOnlyList<BattleHookTraceEntry> HookTrace => _hookTrace;
    public Weather CurrentWeather => _conditions.Snapshot(BattleConditionScope.Weather)
        .Select(instance => WeatherConditions.For(instance.Definition.Id).Weather)
        .SingleOrDefault();

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
        if (Outcome is not null || _pendingReplacementSlots.Count > 0)
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
        => CanSubmitAction(slot, action, null);

    public bool CanSubmitAction(BattleSlot slot, BattleAction action, BattleActionSelection? selection)
    {
        if (Outcome is not null || _pendingReplacementSlots.Count > 0 || !Topology.Contains(slot))
            return false;

        try
        {
            Validate(slot, action, selection);
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
        if (_pendingReplacementSlots.Count > 0)
            throw new InvalidOperationException("Replacement selections must be resolved before the next turn.");

        int start = _log.Count;
        _traceActionSequence = 0;
        BattleIntentPreview intentPreview = _intentQueue.Preview(Turn, BattleIntentCheckpoint.PreAction);
        BattleAction resolvedPlayerAction = PreviewQueuedActionGate(new BattleSlot(BattleSide.Player, 0), playerAction, intentPreview);
        BattleAction resolvedEnemyAction = PreviewQueuedActionGate(new BattleSlot(BattleSide.Enemy, 0), enemyAction, intentPreview);
        Validate(BattleSide.Player, resolvedPlayerAction);
        Validate(BattleSide.Enemy, resolvedEnemyAction);
        ConsumePreviewedIntents(intentPreview);
        var actions = new BattleTurnActions(Topology,
        [
            new BattleActionSubmission(new BattleSlot(BattleSide.Player, 0), resolvedPlayerAction),
            new BattleActionSubmission(new BattleSlot(BattleSide.Enemy, 0), resolvedEnemyAction),
        ]);
        BeginActionHistory(actions.Actions.Select(submission =>
            new BattleActionPlan(HistoryOwner(submission.Source), PlanKind(submission.Action))));

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

        // 6. End-of-turn residuals, then request choices for fillable fainted slots.
        if (Outcome is null)
        {
            EndOfTurn();
            CheckEnd();
        }

        Turn++;
        RevertFaintedBattleForms();
        if (Outcome is null)
            RequestReplacements();
        return _log.GetRange(start, _log.Count - start);
    }

    /// <summary>Admits and resolves one complete slot-addressed turn.</summary>
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
        _traceActionSequence = 0;
        if (_pendingReplacementSlots.Count > 0)
            throw new InvalidOperationException("Fillable fainted slots require replacement selections before the next turn.");
        BattleIntentPreview intentPreview = _intentQueue.Preview(Turn, BattleIntentCheckpoint.PreAction);
        IReadOnlyList<AdmittedAction> admitted = Admit(submitted, intentPreview);
        ConsumePreviewedIntents(intentPreview);
        BeginActionHistory(admitted.Select(action => new BattleActionPlan(
            new BattleHistoryOwner(action.Submission.Source.Side, action.ActorPartyIndex, action.Submission.Source),
            PlanKind(action.Submission.Action))));
        ResolveSwitchPhase(admitted);
        ResolveItemPhase(admitted);
        ResolveFormPhase(admitted);
        ResolveDoublesMoveScheduling(admitted);
        if (Outcome is null)
        {
            EndOfTurn();
            CheckEnd();
        }
        Turn++;
        RevertFaintedBattleForms();
        if (Outcome is null)
            RequestReplacements();
        return _log.GetRange(start, _log.Count - start);
    }

    public IReadOnlyList<BattleEvent> ResolveReplacements(IReadOnlyList<BattleReplacementSelection> selections)
    {
        ArgumentNullException.ThrowIfNull(selections);
        if (Outcome is not null)
            throw new InvalidOperationException("The battle is already over.");

        if (_pendingReplacementSlots.Count == 0)
            throw new InvalidOperationException("No replacement selections are pending.");
        BattleSlot[] pending = [.. _pendingReplacementSlots];
        if (selections.Count != pending.Length || selections.Select(selection => selection.Slot).Distinct().Count() != selections.Count)
            throw new ArgumentException("Replacement selections must name every pending slot exactly once.", nameof(selections));

        foreach (BattleReplacementSelection selection in selections)
        {
            if (!pending.Contains(selection.Slot))
                throw new ArgumentException("Replacement selection is not pending.", nameof(selections));
            if (selection.PartyIndex < 0 || selection.PartyIndex >= _parties[(int)selection.Slot.Side].Count
                || _parties[(int)selection.Slot.Side][selection.PartyIndex].IsFainted
                || _activeSlots.IsActive(selection.Slot.Side, selection.PartyIndex))
                throw new ArgumentException("Replacement selection must name a healthy non-active party member.", nameof(selections));
        }
        foreach (IGrouping<BattleSide, BattleReplacementSelection> side in selections.GroupBy(selection => selection.Slot.Side))
            if (side.Select(selection => selection.PartyIndex).Distinct().Count() != side.Count())
                throw new ArgumentException("A party member cannot fill two replacement slots.", nameof(selections));

        int start = _log.Count;
        _pendingReplacementSlots.Clear();
        foreach (BattleSlot slot in pending)
            SwitchTo(slot, selections.Single(selection => selection.Slot == slot).PartyIndex);
        RevertFaintedBattleForms();
        CheckEnd();
        if (Outcome is null)
            RequestReplacements();
        return _log.GetRange(start, _log.Count - start);
    }

    private IReadOnlyList<AdmittedAction> Admit(BattleTurnActions submitted, BattleIntentPreview intentPreview)
    {
        BattleSlot[] expected = Topology.Slots.Where(IsLive).ToArray();
        if (!submitted.Actions.Select(action => action.Source).SequenceEqual(expected))
            throw new ArgumentException("Every occupied living slot must submit exactly one action.", nameof(submitted));
        var admitted = new List<AdmittedAction>(submitted.Actions.Count);
        foreach (BattleActionSubmission submission in submitted.Actions)
        {
            BattleAction effective = PreviewQueuedActionGate(submission.Source, submission.Action, intentPreview);
            Validate(submission.Source, effective, submission.Selection);
            BattleCreature actor = Active(submission.Source);
            EntityId? moveId = MoveIndex(effective) is { } moveIndex
                ? actor.Moves[EffectiveMoveIndex(submission.Source, moveIndex)].Move
                : null;
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

    private void ConsumePreviewedIntents(BattleIntentPreview preview)
    {
        IReadOnlyList<BattleIntent> consumed = _intentQueue.Consume(preview);
        var eventRanges = new Dictionary<BattleSlot, (int Start, int End)>();
        foreach (BattleSlot slot in Topology.Slots)
        {
            if (!consumed.Any(intent => intent.Payload is SkipActionIntent && intent.Owner.LastKnownSlot == slot))
                continue;
            int start = _log.Count;
            _log.Add(new ActionSkipped(slot));
            eventRanges.Add(slot, (start, _log.Count));
        }

        var tracedSlots = new HashSet<BattleSlot>();
        foreach (BattleIntent intent in consumed)
        {
            BattleSlot slot = intent.Owner.LastKnownSlot ?? new BattleSlot(intent.Owner.Side, 0);
            (int Start, int End) range = tracedSlots.Add(slot) && eventRanges.TryGetValue(slot, out var emitted)
                ? emitted
                : (_log.Count, _log.Count);
            AddIntentTrace(intent, EffectTraceKind.IntentConsumed, true, range.Start, range.End);
        }
        foreach (BattleIntent intent in _intentQueue.Complete(preview))
            AddIntentTrace(intent, EffectTraceKind.IntentDeferred, false, _log.Count, _log.Count);
    }

    private static BattleAction PreviewQueuedActionGate(BattleSlot slot, BattleAction action, BattleIntentPreview preview) =>
        preview.Entries.Any(intent => intent.Payload is SkipActionIntent && intent.Owner.LastKnownSlot == slot)
            ? new Pass()
            : action;

    private void ResolveSwitchPhase(IReadOnlyList<AdmittedAction> actions)
    {
        var scheduled = actions.Where(action => action.Submission.Action is Switch)
            .Select(action => new BattleScheduledAction(action.Submission, 0, Speed(action.Submission.Source)))
            .ToList();
        foreach (BattleScheduledAction scheduledAction in OrderActions(scheduled))
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
        foreach (BattleScheduledAction scheduledAction in OrderActions(scheduled))
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
        _redirects.Clear();
        // Flinch and Protect last only for a turn; Counter/Mirror Coat only see this turn's hits.
        foreach (BattleSlot slot in Topology.Slots)
        {
            BattleCreature active = Active(slot);
            active.ClearFlinch();
            active.ClearProtected();
            active.ResetDamageTaken();
        }

        var scheduled = new List<BattleScheduledAction>();
        foreach (AdmittedAction action in actions)
        {
            if (MoveIndex(action.Submission.Action) is not { } moveIndex || !ActorIsCurrent(action))
                continue;
            int effective = EffectiveMoveIndex(action.Submission.Source, moveIndex);
            scheduled.Add(new BattleScheduledAction(action.Submission, Active(action.Submission.Source).Moves[effective].Priority,
                Speed(action.Submission.Source)));
        }
        foreach (BattleScheduledAction scheduledAction in OrderActions(scheduled))
        {
            AdmittedAction action = actions.Single(candidate => candidate.Submission == scheduledAction.Submission);
            if (!ActorIsCurrent(action))
            {
                InvalidateActor(action);
                continue;
            }

            int moveIndex = EffectiveMoveIndex(action.Submission.Source, MoveIndex(action.Submission.Action)!.Value);
            BattleCreature attacker = Active(action.Submission.Source);
            BattleMove move = attacker.Moves[moveIndex];
            if (!move.HasPp && !attacker.IsCharging && !attacker.IsLocked)
            {
                _log.Add(new ActionInvalidated(action.Submission.Source, ActionInvalidationReason.ResourceChanged));
                continue;
            }

            int traceAction = ++_traceActionSequence;
            BattleHistoryOwner sourceOwner = HistoryOwner(action.Submission.Source);
            BattleActionAttemptId attempt = _actionHistory.BeginMove(traceAction, sourceOwner, move.Move);
            if (!CanAct(attacker, action.Submission.Source, traceAction))
            {
                _actionHistory.Complete(attempt, BattleActionResult.Prevented);
                TickRampageLock(action.Submission.Source, traceAction);
                continue;
            }
            if (attacker.Flinched)
            {
                int start = _log.Count;
                _log.Add(new Flinched(action.Submission.Source));
                AddTrace(traceAction, action.Submission.Source, null, EffectTraceKind.FlinchGate, false, null, 0,
                    start, _log.Count);
                _actionHistory.Complete(attempt, BattleActionResult.Prevented);
                TickRampageLock(action.Submission.Source, traceAction);
                continue;
            }
            AddTrace(traceAction, action.Submission.Source, null, EffectTraceKind.FlinchGate, false, null, 1,
                _log.Count, _log.Count);
            if (!PushesThroughConfusion(attacker, action.Submission.Source, traceAction))
            {
                _actionHistory.Complete(attempt, BattleActionResult.Prevented);
                TickRampageLock(action.Submission.Source, traceAction);
                continue;
            }
            if (!PassesMoveGates(attacker, action.Submission.Source, move, traceAction))
            {
                _actionHistory.Complete(attempt, BattleActionResult.Failed);
                TickRampageLock(action.Submission.Source, traceAction);
                continue;
            }

            if (!move.IsProtect)
                attacker.ResetProtectChain();
            _actionHistory.MarkStarted(attempt);
            int ppBeforeSpend = move.Pp;
            if (!PrepareTimedMove(attacker, action.Submission.Source, move, moveIndex, traceAction))
            {
                _actionHistory.Complete(attempt, BattleActionResult.Succeeded);
                continue;
            }
            _log.Add(new MoveUsed(action.Submission.Source, move.Move));
            attacker.RecordMoveUse(move.Move);
            IReadOnlyList<BattleSlot>? targets = MaterializeLiveTargets(action.Submission, move, traceAction,
                out BattleTargetScopeKind scopeKind, out BattleSide? scopeSide);
            BattleActionResult result;
            BattleHistoryOwner[] targetOwners = targets?.Select(HistoryOwner).ToArray() ?? [];
            if (targets is { Count: 0 })
            {
                _log.Add(new MoveFailed(action.Submission.Source, move.Move, MoveFailureReason.TargetUnavailable));
                result = BattleActionResult.Failed;
            }
            else if (targets is { } materializedTargets)
                result = ResolveDoublesMove(action.Submission.Source, sourceOwner, move, materializedTargets,
                    ppBeforeSpend, traceAction, attempt);
            else if (scopeKind is BattleTargetScopeKind.Side or BattleTargetScopeKind.Field)
                result = ResolveDoublesScopedMove(action.Submission.Source, move, scopeSide, traceAction);
            else
            {
                result = BattleActionResult.Failed;
            }
            _actionHistory.Complete(attempt, result, targetOwners);

            TickRampageLock(action.Submission.Source, traceAction);

            if (CheckEnd())
                break;
        }
    }

    private IReadOnlyList<BattleSlot>? MaterializeLiveTargets(BattleActionSubmission submission, BattleMove move, int traceAction,
        out BattleTargetScopeKind scopeKind, out BattleSide? scopeSide)
    {
        BattleSlot? selected = (submission.Selection as ActiveSlotSelection)?.Slot;
        BattleTargetScope scope = BattleTargetResolver.ResolveScope(move.Target, Topology, submission.Source, selected);
        scopeKind = scope.Kind;
        scopeSide = scope.Side;
        if (scope.Kind is BattleTargetScopeKind.Side or BattleTargetScopeKind.Field
            or BattleTargetScopeKind.FaintedParty or BattleTargetScopeKind.MoveReference)
        {
            return null;
        }

        List<BattleSlot> slots = scope.Slots.Where(IsLive).ToList();
        if ((move.Target is MoveTarget.Selected or MoveTarget.SelectedPokemonMeFirst) && slots.Count == 0
            && selected is { Side: var side } && side != submission.Source.Side)
        {
            slots = Topology.SlotsFor(Opponent(submission.Source.Side)).Where(IsLive).ToList();
            if (slots.Count > 1)
                slots = [slots[0]];
        }
        if (IsRedirectable(move.Target) && slots.Count > 0)
        {
            BattleSlot original = slots[0];
            HashSet<string> moveTags = MoveTags(move);
            RedirectCondition[] candidates = _redirects
                .Where(condition => condition.Slot.Side == original.Side
                    && IsLive(condition.Slot)
                    && (scope.Selection == BattleTargetSelection.RandomOpponent || condition.Slot != original))
                .OrderByDescending(condition => condition.Priority)
                .ThenByDescending(condition => Speed(condition.Slot))
                .ThenBy(condition => (int)condition.Slot.Side)
                .ThenBy(condition => condition.Slot.Position)
                .ToArray();
            foreach (RedirectCondition candidate in candidates)
            {
                bool accepted = candidate.AcceptedClasses.Contains(move.DamageClass)
                    && (candidate.AcceptedTags.Count == 0 || candidate.AcceptedTags.Overlaps(moveTags))
                    && !candidate.BypassClasses.Contains(move.DamageClass)
                    && !candidate.BypassTags.Overlaps(moveTags);
                if (!accepted)
                {
                    AddTrace(traceAction, submission.Source, candidate.Slot, EffectTraceKind.Redirection, false, null,
                        0, _log.Count, _log.Count);
                    continue;
                }
                int start = _log.Count;
                slots = [candidate.Slot];
                _log.Add(new TargetRedirected(submission.Source, original, candidate.Slot));
                AddTrace(traceAction, submission.Source, candidate.Slot, EffectTraceKind.Redirection, false, null,
                    candidate.Priority, start, _log.Count);
                break;
            }
        }

        if (scope.Selection == BattleTargetSelection.RandomOpponent)
        {
            int candidateCount = slots.Count;
            int? draw = null;
            if (slots.Count > 1)
            {
                draw = _rng.Next(slots.Count);
                slots = [slots[draw.Value]];
            }
            AddTrace(traceAction, submission.Source, slots.Count == 1 ? slots[0] : null, EffectTraceKind.TargetSelection,
                draw is not null, draw, candidateCount, _log.Count, _log.Count,
                draw is not null ? candidateCount : null);
        }

        return slots;
    }

    private BattleActionResult ResolveDoublesScopedMove(BattleSlot sourceSlot, BattleMove move,
        BattleSide? scopeSide, int traceAction)
    {
        var actionContext = new BattleActionContext(move, Active(sourceSlot), sourceSlot, traceAction);
        EffectContext context = EffectContext.ForScopedAction(actionContext, scopeSide);
        foreach (MoveEffect effect in move.SecondaryEffects.Where(effect => !IsTargetScoped(effect)))
            if (!ApplyEffect(context, effect))
                break;
        return actionContext.Failed ? BattleActionResult.Failed : BattleActionResult.Succeeded;
    }

    private BattleActionResult ResolveDoublesMove(BattleSlot sourceSlot, BattleHistoryOwner sourceOwner,
        BattleMove move, IReadOnlyList<BattleSlot> targetSlots, int ppBeforeSpend, int traceAction,
        BattleActionAttemptId attempt)
    {
        BattleCreature attacker = Active(sourceSlot);
        EntityId moveType = EffectiveMoveType(move, traceAction);
        var actionContext = new BattleActionContext(move, attacker, sourceSlot, traceAction);
        if (!TryItemPower(sourceSlot, move, out int? itemPower))
        {
            _log.Add(new MoveFailed(sourceSlot, move.Move, MoveFailureReason.FormulaInputUnavailable));
            foreach (BattleSlot targetSlot in targetSlots)
                RecordDamage(attempt, sourceOwner, HistoryOwner(targetSlot), move, DamageCause(move), 0,
                    false, BattleDamageFailure.NoQualifyingDamage, 0, default, critical: false,
                    effectiveType: moveType);
            return BattleActionResult.Failed;
        }
        var accurateTargets = new List<BattleTargetContext>(targetSlots.Count);
        foreach (BattleSlot targetSlot in targetSlots)
        {
            BattleCreature target = Active(targetSlot);
            BattleTargetContext targetContext = actionContext.AddTarget(target, targetSlot);
            bool hit = ResolveAccuracy(sourceSlot, targetSlot, attacker, target, move, traceAction, out int? accuracyDraw);
            AddTrace(traceAction, sourceSlot, targetSlot, EffectTraceKind.Accuracy, accuracyDraw is not null,
                accuracyDraw, hit ? 1 : 0, _log.Count, _log.Count, accuracyDraw is not null ? 100 : null);
            if (!hit)
            {
                _log.Add(new MoveMissed(sourceSlot, move.Move, targetSlot));
                if (RecordsMoveDamage(move))
                    RecordDamage(attempt, sourceOwner, HistoryOwner(targetSlot), move, DamageCause(move),
                        0, false, BattleDamageFailure.Missed, 0, default, critical: false,
                        effectiveType: moveType);
                continue;
            }
            accurateTargets.Add(targetContext);
        }

        if (accurateTargets.Count == 0)
        {
            if (move.Power is not null)
                ApplyCrashRecoil(attacker, sourceSlot, move);
            return BattleActionResult.Missed;
        }

        if (!HpStatusFormulas.HasBasePower(move))
        {
            ApplyDoublesMoveEffects(actionContext, accurateTargets);
            return actionContext.Failed ? BattleActionResult.Failed : BattleActionResult.Succeeded;
        }

        int? randomPower = SelectRandomPower(sourceSlot, move, traceAction);
        int? hitCountDraw = null;
        int hits = move.MultiHitMax >= 2 ? EffectMath.HitCount(_rng, move.MultiHitMin, move.MultiHitMax, out hitCountDraw) : 1;
        AddTrace(traceAction, sourceSlot, null, EffectTraceKind.HitCount, hitCountDraw is not null, hitCountDraw, hits, _log.Count, _log.Count,
            hitCountDraw is not null ? HitCountDrawBound(move) : null);
        foreach (BattleTargetContext targetContext in accurateTargets)
        {
            double effectiveness = _chart.Effectiveness(moveType, targetContext.Target.Types);
            if (effectiveness <= 0)
            {
                AddTrace(traceAction, sourceSlot, targetContext.TargetSlot, EffectTraceKind.Immunity, false, null, 0, _log.Count, _log.Count);
                DamageApplication damage = DealMoveDamage(targetContext.Target, targetContext.TargetSlot, 0,
                    effectiveness, crit: false);
                targetContext.AddDamage(actionContext, damage.ActualHpRemoved);
                RecordDamage(attempt, sourceOwner, HistoryOwner(targetContext.TargetSlot), move,
                    BattleDamageCause.Standard, 1, attempted: true, BattleDamageFailure.Immune, 0, damage,
                    critical: false, effectiveType: moveType);
                continue;
            }

            for (int hit = 0; hit < hits && !targetContext.Target.IsFainted; hit++)
            {
                AddTrace(traceAction, sourceSlot, targetContext.TargetSlot, EffectTraceKind.Immunity, false, null, 1, _log.Count, _log.Count);
                (int damage, bool crit, _) = ComputeHit(
                    sourceSlot, targetContext.TargetSlot, attacker, targetContext.Target, move, moveType, move.Power ?? 1,
                    targetSlots.Count, ppBeforeSpend, itemPower, randomPower, traceAction,
                    out double? critDraw, out int? damageRollDraw);
                AddTrace(traceAction, sourceSlot, targetContext.TargetSlot, EffectTraceKind.Critical, true,
                    critDraw, crit ? 1 : 0, _log.Count, _log.Count, 1);
                AddTrace(traceAction, sourceSlot, targetContext.TargetSlot, EffectTraceKind.DamageRoll, true,
                    damageRollDraw, damageRollDraw is { } roll ? roll + 85 : null, _log.Count, _log.Count, 16);
                DamageApplication applied = DealMoveDamage(targetContext.Target, targetContext.TargetSlot, damage,
                    effectiveness, crit,
                    HpStatusFormulas.CannotKoFloor(move));
                targetContext.Target.RecordDamageTaken(move.DamageClass, applied.ActualHpRemoved);
                targetContext.AddDamage(actionContext, applied.ActualHpRemoved);
                RecordDamage(attempt, sourceOwner, HistoryOwner(targetContext.TargetSlot), move,
                    BattleDamageCause.Standard, hit + 1, attempted: true,
                    applied.ActualHpRemoved > 0 ? BattleDamageFailure.None : BattleDamageFailure.NoDamage,
                    damage, applied, crit, effectiveType: moveType);
                AddTrace(traceAction, sourceSlot, targetContext.TargetSlot, EffectTraceKind.Damage, false, null,
                    applied.ActualHpRemoved,
                    _log.Count - 1, _log.Count);
            }
        }

        if (actionContext.TotalDamage == 0)
            ApplyCrashRecoil(attacker, sourceSlot, move);

        ApplyDoublesMoveEffects(actionContext, accurateTargets);
        return actionContext.TotalDamage > 0 ? BattleActionResult.Connected : BattleActionResult.Failed;
    }

    private void ApplyDoublesMoveEffects(BattleActionContext actionContext, IReadOnlyList<BattleTargetContext> targetContexts)
    {
        foreach (BattleTargetContext targetContext in targetContexts)
        {
            var context = new EffectContext(actionContext, targetContext);
            foreach (MoveEffect effect in actionContext.Move.SecondaryEffects.Where(IsTargetScoped))
                ApplyEffect(context, effect);
            ApplyContactEffects(actionContext.Move, actionContext.SourceSlot, actionContext.Source, targetContext.TargetSlot,
                actionContext.TraceAction);
        }

        var actionEffectContext = new EffectContext(actionContext, targetContexts[0]);
        foreach (MoveEffect effect in actionContext.Move.SecondaryEffects.Where(effect => !IsTargetScoped(effect)))
            if (!ApplyEffect(actionEffectContext, effect))
                break;
    }

    private static bool IsTargetScoped(MoveEffect effect) => effect switch
    {
        AilmentEffect or ConfusionEffect or FlinchEffect or LeechSeedEffect or BindEffect or ForceSwitchEffect or PositionSwapEffect => true,
        StatChangeEffect { OnSelf: false } or StatChangeAllEffect { OnSelf: false } or StatInvertEffect { OnSelf: false } => true,
        StatResetEffect { Scope: not StageEffectScope.Self } => true,
        StatCopyEffect { From: StageEffectScope.Target } or StatCopyEffect { To: StageEffectScope.Target } or StatSwapEffect => true,
        HealEffect { Recipient: HpFractionRecipient.Target } or HpFractionEffect { Recipient: HpFractionRecipient.Target }
            or HpEqualizeEffect => true,
        _ => false,
    };

    private static bool IsRedirectable(MoveTarget target) =>
        target is MoveTarget.Selected or MoveTarget.SelectedPokemonMeFirst or MoveTarget.RandomOpponent;

    private static double HitCountDrawBound(BattleMove move) =>
        move.MultiHitMin == 2 && move.MultiHitMax == 5 ? 8 : move.MultiHitMax - move.MultiHitMin + 1;

    private void AddTrace(int action, BattleSlot source, BattleSlot? target, EffectTraceKind kind, bool performed,
        double? draw, int? value, int eventStart, int eventEnd, double? drawBound = null, double? drawMinimum = null) =>
        _trace.Add(new EffectTraceEntry(Turn, action, source, target, kind, performed, draw, value, eventStart, eventEnd)
        {
            DrawMinimum = drawMinimum,
            DrawBound = drawBound,
        });

    private void AddIntentTrace(BattleIntent intent, EffectTraceKind kind, bool performed, int eventStart, int eventEnd)
    {
        BattleSlot source = intent.Owner.LastKnownSlot ?? new BattleSlot(intent.Owner.Side, 0);
        _trace.Add(new EffectTraceEntry(Turn, intent.SourceActionSequence, source, intent.Target.Slot, kind,
            performed, null, performed ? 1 : 0, eventStart, eventEnd)
        {
            IntentSequence = intent.Sequence,
            IntentCheckpoint = intent.Checkpoint,
            IntentPayload = intent.Payload.Kind,
            IntentSourceMove = intent.SourceMove,
        });
    }

    private void TraceCleanup(BattleIntentCleanupResult cleanup)
    {
        foreach (BattleIntent intent in cleanup.Cancelled)
            AddIntentTrace(intent, EffectTraceKind.IntentCancelled, false, _log.Count, _log.Count);
        foreach (BattleIntent intent in cleanup.Transferred)
            AddIntentTrace(intent, EffectTraceKind.IntentTransferred, true, _log.Count, _log.Count);
    }

    private void TraceCancelled(IEnumerable<BattleIntent> cancelled)
    {
        foreach (BattleIntent intent in cancelled)
            AddIntentTrace(intent, EffectTraceKind.IntentCancelled, false, _log.Count, _log.Count);
    }

    private IReadOnlyList<BattleScheduledAction> OrderActions(IReadOnlyList<BattleScheduledAction> scheduled) =>
        BattleTurnOrder.Order(scheduled, _rng, (action, draw, bound, selectedIndex) =>
            AddTrace(0, action.Submission.Source, null, EffectTraceKind.TurnOrderTie, true, draw, selectedIndex,
                _log.Count, _log.Count, bound));

    private bool IsLive(BattleSlot slot) => !Active(slot).IsFainted;

    private bool ActorIsCurrent(AdmittedAction action)
    {
        if (ActiveIndex(action.Submission.Source) != action.ActorPartyIndex)
            return false;
        BattleCreature actor = Active(action.Submission.Source);
        if (actor.IsFainted)
            return false;
        return action.MoveId is null || MoveIndex(action.Submission.Action) is { } moveIndex
            && moveIndex < actor.Moves.Count
            && actor.Moves[EffectiveMoveIndex(action.Submission.Source, moveIndex)].Move == action.MoveId;
    }

    private void InvalidateActor(AdmittedAction action)
    {
        ActionInvalidationReason reason = ActiveIndex(action.Submission.Source) != action.ActorPartyIndex
            ? ActionInvalidationReason.ActorChanged
            : Active(action.Submission.Source).IsFainted ? ActionInvalidationReason.ActorFainted
            : ActionInvalidationReason.MoveChanged;
        _log.Add(new ActionInvalidated(action.Submission.Source, reason));
    }

    private void Validate(BattleSide side, BattleAction action) => Validate(new BattleSlot(side, 0), action, null);

    private void Validate(BattleSlot slot, BattleAction action, BattleActionSelection? selection = null)
    {
        if (!Topology.Contains(slot))
            throw new ArgumentException($"Slot {slot} is outside this battle topology.", nameof(slot));
        BattleSide side = slot.Side;
        switch (action)
        {
            case UseMove use:
                ValidateMoveUse(slot, use.MoveIndex);
                ValidateSelection(slot, Active(slot).Moves[EffectiveMoveIndex(slot, use.MoveIndex)].Target, selection);
                break;

            case ActivateForm form:
                ValidateMoveUse(slot, form.MoveIndex);
                ValidateSelection(slot, Active(slot).Moves[EffectiveMoveIndex(slot, form.MoveIndex)].Target, selection);
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

    private void ValidateSelection(BattleSlot source, MoveTarget target, BattleActionSelection? selection)
    {
        bool requiresActive = Topology.ActiveSlotsPerSide > 1
            && (target is MoveTarget.Selected or MoveTarget.SelectedPokemonMeFirst or MoveTarget.Ally);
        if (target == MoveTarget.UserOrAlly && Topology.ActiveSlotsPerSide > 1)
            requiresActive = true;
        if (requiresActive && selection is not ActiveSlotSelection)
            throw new ArgumentException($"Move target '{target}' requires an active-slot selection.", nameof(selection));
        if (target == MoveTarget.FaintingPokemon && selection is not PartyMemberSelection)
            throw new ArgumentException("Fainting-pokemon targets require a party-member selection.", nameof(selection));
        if (target == MoveTarget.SpecificMove && selection is not MoveReferenceSelection)
            throw new ArgumentException("Specific-move targets require a move-reference selection.", nameof(selection));
        if (selection is null)
            return;

        switch (selection)
        {
            case ActiveSlotSelection active:
                if (!Topology.Contains(active.Slot))
                    throw new ArgumentException("Selected active slot is outside the battle topology.", nameof(selection));
                if (target is not (MoveTarget.Selected or MoveTarget.SelectedPokemonMeFirst or MoveTarget.Ally or MoveTarget.UserOrAlly))
                    throw new ArgumentException($"Move target '{target}' does not accept an active-slot selection.", nameof(selection));
                if (active.Slot == source && target != MoveTarget.UserOrAlly)
                    throw new ArgumentException("A selected active target cannot be the source slot.", nameof(selection));
                if (target == MoveTarget.SelectedPokemonMeFirst && active.Slot.Side == source.Side)
                    throw new ArgumentException("Selected-pokemon-me-first targets must be opposing slots.", nameof(selection));
                if (target == MoveTarget.Ally && active.Slot.Side != source.Side)
                    throw new ArgumentException("Ally targets must be own-side slots.", nameof(selection));
                if (target == MoveTarget.UserOrAlly && active.Slot.Side != source.Side)
                    throw new ArgumentException("User-or-ally targets must be own-side slots.", nameof(selection));
                break;
            case PartyMemberSelection party:
                if (target != MoveTarget.FaintingPokemon || party.Side != source.Side
                    || party.PartyIndex < 0 || party.PartyIndex >= _parties[(int)party.Side].Count
                    || !_parties[(int)party.Side][party.PartyIndex].IsFainted)
                    throw new ArgumentException("Fainting-pokemon selection must name a fainted member on the source side.", nameof(selection));
                break;
            case MoveReferenceSelection move:
                if (target != MoveTarget.SpecificMove || !Topology.Contains(move.Slot)
                    || move.MoveIndex < 0 || move.MoveIndex >= Active(move.Slot).Moves.Count)
                    throw new ArgumentException("Move-reference selection is invalid.", nameof(selection));
                break;
            default:
                throw new ArgumentException("Unknown action selection.", nameof(selection));
        }
    }

    private void ValidateMoveUse(BattleSlot slot, int moveIndex)
    {
        BattleCreature c = Active(slot);
        BattleSide side = slot.Side;
        if (moveIndex < 0 || moveIndex >= c.Moves.Count)
            throw new ArgumentException($"{side} move index {moveIndex} out of range.");
        if (!c.IsCharging && !c.IsLocked && c.ChoiceLockedMoveIndex is { } locked && moveIndex != locked)
            throw new ArgumentException($"{side} is locked into move {locked}.");
        if (!c.Moves[moveIndex].HasPp && !c.IsCharging && !c.IsLocked)
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
        => SwitchTo(new BattleSlot(side, 0), index);

    private void SwitchTo(BattleSlot slot, int index)
    {
        int outgoingPartyIndex = ActiveIndex(slot);
        _actionHistory.RecordSwitch(
            new BattleHistoryOwner(slot.Side, outgoingPartyIndex, slot),
            new BattleHistoryOwner(slot.Side, index, slot));
        BattleCreature outgoing = Active(slot);
        outgoing.ResetStages();
        outgoing.ClearVolatiles();
        TraceCleanup(_intentQueue.OwnerSwitched(slot.Side, outgoingPartyIndex, null));
        _overlays.OwnerSwitched(slot.Side, outgoingPartyIndex, null, Turn, _traceActionSequence);
        _activeSlots.Assign(slot, index);
        _overlays.OwnerSwitched(slot.Side, index, slot, Turn, _traceActionSequence);
        _log.Add(new SwitchedIn(slot, index));
        OnSwitchIn(slot);
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

            int moveIndex = EffectiveMoveIndex(submission.Source, submittedIndex);
            scheduled.Add(new BattleScheduledAction(
                submission,
                Active(submission.Source).Moves[moveIndex].Priority,
                Speed(submission.Source)));
        }

        foreach (BattleScheduledAction scheduledAction in OrderActions(scheduled))
        {
            int submittedIndex = MoveIndex(scheduledAction.Submission.Action)!.Value;
            BattleSlot source = scheduledAction.Submission.Source;
            int traceBefore = _traceActionSequence;
            ResolveMove(source, EffectiveMoveIndex(source, submittedIndex));
            TickRampageLock(source, _traceActionSequence > traceBefore ? _traceActionSequence : null);
            if (CheckEnd())
                break;
        }
    }

    private static int? MoveIndex(BattleAction action) => action switch
    {
        UseMove move => move.MoveIndex,
        ActivateForm form => form.MoveIndex,
        _ => null,
    };

    private void BeginActionHistory(IEnumerable<BattleActionPlan> plans) =>
        _actionHistory.BeginTurn(Turn, plans);

    private static BattlePlannedActionKind PlanKind(BattleAction action) => MoveIndex(action) is not null
        ? BattlePlannedActionKind.Move
        : action is Switch ? BattlePlannedActionKind.Switch : BattlePlannedActionKind.Other;

    private BattleHistoryOwner HistoryOwner(BattleSlot slot) =>
        new(slot.Side, ActiveIndex(slot), slot);

    /// <summary>Counts a rampage (Thrash/Outrage) down after its move resolved — whether it hit, missed,
    /// or was blocked. When the lock ends, the user confuses itself.</summary>
    private void TickRampageLock(BattleSlot slot, int? traceAction)
    {
        BattleCreature c = Active(slot);
        if (!c.IsLocked)
            return;
        c.TickLock();
        if (!c.IsLocked && !c.IsFainted && !c.IsConfused)
        {
            int eventStart = _log.Count;
            int duration = VolatileEffects.ConfusionDuration(_rng, out int draw);
            c.SetConfusion(duration);
            _log.Add(new Confused(slot));
            if (traceAction is { } action)
                AddTrace(action, slot, null, EffectTraceKind.ConfusionDuration, true, draw, duration,
                    eventStart, _log.Count, 5, 1);
        }
    }

    private int Speed(BattleSide side)
    {
        return Speed(new BattleSlot(side, 0));
    }

    private int Speed(BattleSlot slot)
    {
        BattleCreature c = Active(slot);
        BattleQueryResult result = PhysicalMetricFormulas.SpeedQuery(c, _overlays,
            new BattleOverlayOwner(slot.Side, ActiveIndex(slot), slot),
            new BattleQueryContext(slot, c, Weather: CurrentWeather));
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, 0, slot, null, result));
        return result.FinalValue.ToInt32();
    }

    private bool ResolveAccuracy(BattleSlot sourceSlot, BattleSlot targetSlot, BattleCreature source,
        BattleCreature target, BattleMove move, int traceAction, out int? draw)
    {
        int authored = move.Ohko ? EffectMath.OhkoAccuracy(source.Level, target.Level) : move.Accuracy ?? 100;
        WeatherAccuracyEffect? weatherEffect = move.SecondaryEffects.OfType<WeatherAccuracyEffect>().SingleOrDefault();
        BattleHookDispatchSnapshot? weather = weatherEffect is null ? null
            : WeatherConditions.CollectAccuracyHooks(_conditions.Snapshot(), weatherEffect, traceAction);
        if (weather is not null)
            _hookTrace.AddRange(weather.Trace);
        bool weatherBypass = weather?.Filters().Any(filter => filter is
            { Filter.Value: "accuracy_bypass", Decision: BattleHookFilterDecision.Allow }) == true;
        bool alwaysHits = move.BypassAccuracy || (!move.Ohko && move.Accuracy is null) || weatherBypass;
        var modifiers = weather?.QueryModifiers(BattleQueryId.Accuracy).ToList() ?? [];
        if (!alwaysHits)
        {
            modifiers.Add(new BattleQueryModifier(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply,
                BattleQuery.AccuracyStageMultiplier(source.Stage(StatKind.Accuracy), target.Stage(StatKind.Evasion)),
                InsertionOrder: modifiers.Count));
        }
        BattleQueryResult result = BattleQuery.Evaluate(BattleQueryId.Accuracy, new BattleQueryValue(authored), modifiers,
            new BattleQueryContext(sourceSlot, source, targetSlot, target, CurrentWeather));
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction, sourceSlot, targetSlot, result));

        if (alwaysHits)
        {
            draw = null;
            return true;
        }
        return BattleRolls.Hits(result.FinalValue.ToInt32(), 0, 0, _rng, out draw);
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

    private bool PrepareTimedMove(BattleCreature attacker, BattleSlot sourceSlot, BattleMove move,
        int moveIndex, int traceAction)
    {
        bool firing = attacker.IsCharging;
        bool requiresCharge = move.ChargeTurn;
        if (requiresCharge && !firing
            && move.SecondaryEffects.OfType<WeatherMoveEffect>().SingleOrDefault() is { } weatherEffect)
        {
            BattleHookDispatchSnapshot weather = WeatherConditions.CollectChargeHooks(
                ConditionSnapshot, weatherEffect, traceAction);
            _hookTrace.AddRange(weather.Trace);
            requiresCharge = !weather.Filters().Any(filter => filter is
                { Filter.Value: "charge_required", Decision: BattleHookFilterDecision.Deny });
        }
        if (requiresCharge && !firing)
        {
            move.UsePp();
            ApplyChoiceLock(attacker, moveIndex);
            attacker.RecordMoveUse(move.Move);
            attacker.StartCharging(moveIndex);
            _log.Add(new Charging(sourceSlot, move.Move));
            return false;
        }
        if (firing)
            attacker.StopCharging();

        bool continuingLock = attacker.IsLocked;
        if (move.MultiTurnLock && !continuingLock)
        {
            int duration = _rng.Next(2, 4);
            attacker.StartLock(moveIndex, duration);
            AddTrace(traceAction, sourceSlot, null, EffectTraceKind.LockDuration, true, duration, duration,
                _log.Count, _log.Count, 4, 2);
        }

        if (!firing && !continuingLock)
            move.UsePp();
        if (!continuingLock)
            ApplyChoiceLock(attacker, moveIndex);
        return true;
    }

    private void ResolveMove(BattleSlot sourceSlot, int moveIndex)
    {
        BattleSide side = sourceSlot.Side;
        BattleCreature attacker = Active(sourceSlot);
        if (attacker.IsFainted)
            return;

        int traceAction = ++_traceActionSequence;
        BattleMove move = attacker.Moves[moveIndex];
        BattleHistoryOwner sourceOwner = HistoryOwner(sourceSlot);
        BattleActionAttemptId attempt = _actionHistory.BeginMove(traceAction, sourceOwner, move.Move);
        if (!CanAct(attacker, sourceSlot, traceAction))
        {
            _actionHistory.Complete(attempt, BattleActionResult.Prevented);
            return; // frozen/asleep/fully-paralyzed — no PP spent, no move
        }

        if (attacker.Flinched)
        {
            int start = _log.Count;
            _log.Add(new Flinched(sourceSlot));
            AddTrace(traceAction, sourceSlot, null, EffectTraceKind.FlinchGate, false, null, 0, start, _log.Count);
            _actionHistory.Complete(attempt, BattleActionResult.Prevented);
            return; // flinch costs the turn but no PP
        }
        AddTrace(traceAction, sourceSlot, null, EffectTraceKind.FlinchGate, false, null, 1, _log.Count, _log.Count);

        if (!PushesThroughConfusion(attacker, sourceSlot, traceAction))
        {
            _actionHistory.Complete(attempt, BattleActionResult.Prevented);
            return; // hurt itself in confusion — no PP, no move
        }

        if (!PassesMoveGates(attacker, sourceSlot, move, traceAction))
        {
            _actionHistory.Complete(attempt, BattleActionResult.Failed);
            return;
        }
        BattleSide targetSide = BattleTargetResolver.IsSinglesActiveCreatureTarget(move.Target)
            ? BattleTargetResolver.ResolveSinglesActiveCreatureSide(move.Target, side)
            : Opponent(side);
        BattleSlot targetSlot = new(targetSide, 0);
        BattleCreature target = Active(targetSlot);
        if (target.IsFainted)
        {
            _actionHistory.Complete(attempt, BattleActionResult.Failed);
            return;
        }
        BattleHistoryOwner targetOwner = HistoryOwner(targetSlot);

        if (!move.IsProtect)
            attacker.ResetProtectChain(); // any non-protect move breaks the protect chain

        // Two-turn move: turn 1 charges (PP spent now, no damage); turn 2 fires as a normal hit.
        _actionHistory.MarkStarted(attempt);
        int ppBeforeSpend = move.Pp;
        if (!PrepareTimedMove(attacker, sourceSlot, move, moveIndex, traceAction))
        {
            _actionHistory.Complete(attempt, BattleActionResult.Succeeded, [targetOwner]);
            return;
        }

        _log.Add(new MoveUsed(sourceSlot, move.Move));
        attacker.RecordMoveUse(move.Move);
        EntityId moveType = EffectiveMoveType(move, traceAction);

        var actionContext = new BattleActionContext(move, attacker, sourceSlot, traceAction);
        BattleTargetContext targetContext = actionContext.AddTarget(target, targetSlot);

        if (!TryItemPower(sourceSlot, move, out int? itemPower))
        {
            _log.Add(new MoveFailed(sourceSlot, move.Move, MoveFailureReason.FormulaInputUnavailable));
            RecordDamage(attempt, sourceOwner, targetOwner, move, DamageCause(move), 0,
                false, BattleDamageFailure.NoQualifyingDamage, 0, default, critical: false,
                effectiveType: moveType);
            _actionHistory.Complete(attempt, BattleActionResult.Failed, [targetOwner]);
            return;
        }

        // Protect: a move aimed at a shielded target is blocked outright (PP already spent).
        if (target.Protected && TargetsOpponent(move))
        {
            _log.Add(new MoveBlocked(sourceSlot));
            if (RecordsMoveDamage(move))
                RecordDamage(attempt, sourceOwner, targetOwner, move, DamageCause(move), 0,
                    false, BattleDamageFailure.Protected, 0, default, critical: false,
                    effectiveType: moveType);
            ApplyCrashRecoil(attacker, sourceSlot, move);
            _actionHistory.Complete(attempt, BattleActionResult.Failed, [targetOwner]);
            return;
        }

        // OHKO uses a level-scaled accuracy in place of the move's own; accuracyBypass sure-hits.
        bool hit = ResolveAccuracy(sourceSlot, targetSlot, attacker, target, move, traceAction, out int? accuracyDraw);
        AddTrace(traceAction, sourceSlot, targetSlot, EffectTraceKind.Accuracy, accuracyDraw is not null,
            accuracyDraw, hit ? 1 : 0, _log.Count, _log.Count, accuracyDraw is not null ? 100 : null);
        if (!hit)
        {
            _log.Add(new MoveMissed(sourceSlot, move.Move, targetSlot));
            if (RecordsMoveDamage(move))
                RecordDamage(attempt, sourceOwner, targetOwner, move, DamageCause(move), 0,
                    false, BattleDamageFailure.Missed, 0, default, critical: false,
                    effectiveType: moveType);
            ApplyCrashRecoil(attacker, sourceSlot, move);
            _actionHistory.Complete(attempt, BattleActionResult.Missed, [targetOwner]);
            return;
        }

        int? randomPower = SelectRandomPower(sourceSlot, move, traceAction);

        if (move.CounterCategory is { } counterCat)
        {
            // Counter/Mirror Coat: return 2× the damage of that category taken this turn (no draw).
            int received = counterCat == DamageClass.Physical ? attacker.PhysicalDamageTaken : attacker.SpecialDamageTaken;
            if (received > 0)
            {
                int dmg = TraceUnmodifiedFinalDamage(sourceSlot, targetSlot, attacker, target,
                    checked(received * 2), traceAction);
                DamageApplication applied = DealMoveDamage(target, targetSlot, dmg, 1.0, crit: false,
                    HpStatusFormulas.CannotKoFloor(move));
                targetContext.AddDamage(actionContext, applied.ActualHpRemoved);
                RecordDamage(attempt, sourceOwner, targetOwner, move, BattleDamageCause.Counter, 1, true,
                    applied.ActualHpRemoved > 0 ? BattleDamageFailure.None : BattleDamageFailure.NoDamage,
                    dmg, applied, critical: false);
            }
            else
            {
                _log.Add(new MoveMissed(sourceSlot, move.Move)); // nothing to counter → fizzles
                RecordDamage(attempt, sourceOwner, targetOwner, move, BattleDamageCause.Counter, 0, false,
                    BattleDamageFailure.NoQualifyingDamage, 0, default, critical: false);
            }
        }
        else if (move.Ohko || move.FixedDamage is not null || move.FixedDamageLevel)
        {
            // Formula-bypassing hit: no crit/STAB/roll (no RNG draws), but type immunity still voids it.
            double eff = _chart.Effectiveness(moveType, target.Types);
            int dmg = eff <= 0 ? 0
                : move.Ohko ? target.CurrentHp
                : move.FixedDamageLevel ? attacker.Level
                : move.FixedDamage!.Value;
            dmg = TraceUnmodifiedFinalDamage(sourceSlot, targetSlot, attacker, target, dmg, traceAction);
            DamageApplication applied = DealMoveDamage(target, targetSlot, dmg, eff, crit: false,
                HpStatusFormulas.CannotKoFloor(move));
            targetContext.AddDamage(actionContext, applied.ActualHpRemoved);
            BattleDamageCause cause = move.Ohko ? BattleDamageCause.OneHitKnockout
                : move.FixedDamageLevel ? BattleDamageCause.Level : BattleDamageCause.Fixed;
            RecordDamage(attempt, sourceOwner, targetOwner, move, cause, 1, true,
                eff <= 0 ? BattleDamageFailure.Immune
                    : applied.ActualHpRemoved > 0 ? BattleDamageFailure.None : BattleDamageFailure.NoDamage,
                dmg, applied, critical: false);
        }
        else if (HpStatusFormulas.HasBasePower(move))
        {
            int power = move.Power ?? 1;
            // Single-hit is a 1-iteration loop, so the crit→roll draw order is identical to before;
            // HitCount only draws for actual multi-hit moves. Each hit rolls crit/damage independently.
            int? hitCountDraw = null;
            int hits = move.MultiHitMax >= 2 ? EffectMath.HitCount(_rng, move.MultiHitMin, move.MultiHitMax, out hitCountDraw) : 1;
            AddTrace(traceAction, sourceSlot, null, EffectTraceKind.HitCount, hitCountDraw is not null,
                hitCountDraw, hits, _log.Count, _log.Count, hitCountDraw is not null ? HitCountDrawBound(move) : null);
            for (int h = 0; h < hits && !target.IsFainted; h++)
            {
                double effectiveness = _chart.Effectiveness(moveType, target.Types);
                AddTrace(traceAction, sourceSlot, targetSlot, EffectTraceKind.Immunity, false, null,
                    effectiveness <= 0 ? 0 : 1, _log.Count, _log.Count);
                (int dmg, bool crit, double eff) = ComputeHit(sourceSlot, targetSlot, attacker, target, move, moveType, power, 1,
                    ppBeforeSpend, itemPower, randomPower, traceAction,
                    out double? critDraw, out int? damageRollDraw);
                if (effectiveness > 0)
                {
                    AddTrace(traceAction, sourceSlot, targetSlot, EffectTraceKind.Critical, true,
                        critDraw, crit ? 1 : 0, _log.Count, _log.Count, 1);
                    AddTrace(traceAction, sourceSlot, targetSlot, EffectTraceKind.DamageRoll, true,
                        damageRollDraw, damageRollDraw is { } roll ? roll + 85 : null, _log.Count, _log.Count, 16);
                }
                DamageApplication applied = DealMoveDamage(target, targetSlot, dmg, eff, crit,
                    HpStatusFormulas.CannotKoFloor(move));
                target.RecordDamageTaken(move.DamageClass, applied.ActualHpRemoved); // for Counter/Mirror Coat
                targetContext.AddDamage(actionContext, applied.ActualHpRemoved);
                RecordDamage(attempt, sourceOwner, targetOwner, move, BattleDamageCause.Standard, h + 1, true,
                    eff <= 0 ? BattleDamageFailure.Immune
                        : applied.ActualHpRemoved > 0 ? BattleDamageFailure.None : BattleDamageFailure.NoDamage,
                    dmg, applied, crit, effectiveType: moveType);
                AddTrace(traceAction, sourceSlot, targetSlot, EffectTraceKind.Damage, false, null,
                    applied.ActualHpRemoved,
                    _log.Count - 1, _log.Count);
            }
        }

        if (actionContext.TotalDamage == 0)
            ApplyCrashRecoil(attacker, sourceSlot, move);

        // Effect-list-driven resolution (EFFECT_TYPES_CATALOG): iterate the move's ordered effects and
        // dispatch each to a shared primitive. Order matches the historical pipeline (target secondaries,
        // then leech/drain/heal/recoil/crit/faint), so the RNG draw order is unchanged.
        var ctx = new EffectContext(actionContext, targetContext);
        foreach (MoveEffect effect in move.SecondaryEffects)
            if (!ApplyEffect(ctx, effect))
                break;
        ApplyContactEffects(move, sourceSlot, attacker, targetSlot, traceAction);
        bool damaging = HpStatusFormulas.HasBasePower(move) || move.CounterCategory is not null
            || move.Ohko || move.FixedDamage is not null || move.FixedDamageLevel;
        _actionHistory.Complete(attempt,
            actionContext.Failed ? BattleActionResult.Failed
            : actionContext.TotalDamage > 0 ? BattleActionResult.Connected
            : damaging
                ? BattleActionResult.Failed
                : BattleActionResult.Succeeded,
            [targetOwner]);
    }

    private int TraceUnmodifiedFinalDamage(BattleSlot sourceSlot, BattleSlot targetSlot,
        BattleCreature source, BattleCreature target, int damage, int traceAction)
    {
        BattleQueryResult result = BattleQuery.Evaluate(BattleQueryId.FinalDamage, new BattleQueryValue(damage),
            context: new BattleQueryContext(sourceSlot, source, targetSlot, target, CurrentWeather));
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction, sourceSlot, targetSlot, result));
        return result.FinalValue.ToInt32();
    }

    /// <summary>Confusion pre-move check: counts down, may snap out or force a self-hit.
    /// Returns false only when the creature hurt itself and loses the turn.</summary>
    private bool PushesThroughConfusion(BattleCreature c, BattleSlot sourceSlot, int traceAction)
    {
        BattleSide side = sourceSlot.Side;
        if (!c.IsConfused)
        {
            AddTrace(traceAction, sourceSlot, null, EffectTraceKind.ConfusionGate, false, null, 1, _log.Count, _log.Count);
            return true;
        }

        c.TickConfusion();
        if (!c.IsConfused)
        {
            int start = _log.Count;
            _log.Add(new ConfusionEnded(sourceSlot));
            AddTrace(traceAction, sourceSlot, null, EffectTraceKind.ConfusionGate, false, null, 1, start, _log.Count);
            return true; // snapped out this turn — acts freely
        }

        bool hitsSelf = VolatileEffects.HitsSelfInConfusion(_rng, out double draw);
        if (!hitsSelf)
        {
            AddTrace(traceAction, sourceSlot, null, EffectTraceKind.ConfusionGate, true, draw, 1,
                _log.Count, _log.Count, 1);
            return true;
        }

        int eventStart = _log.Count;
        int dmg = VolatileEffects.ConfusionSelfDamage(c.Level, c.Stats.Atk, c.Stats.Def);
        c.TakeDamage(dmg);
        _log.Add(new HurtInConfusion(sourceSlot, dmg));
        if (c.IsFainted)
            RecordFaint(sourceSlot);
        AddTrace(traceAction, sourceSlot, null, EffectTraceKind.ConfusionGate, true, draw, 0,
            eventStart, _log.Count, 1);
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
            case HpCostEffect h:
                bool paid = ApplyHpCost(ctx, h);
                if (!paid)
                    ctx.Action.MarkFailed();
                return paid;
            case StatResetEffect r: ApplyStatReset(ctx, r); break;
            case StatCopyEffect copy: ApplyStatCopy(ctx, copy); break;
            case StatSwapEffect s: ApplyStatSwap(ctx, s); break;
            case StatInvertEffect i: ApplyStatInvert(ctx, i); break;
            case ConfusionEffect confusion: ApplyConfusion(ctx, confusion); break;
            case FlinchEffect f: ApplyFlinch(ctx, f); break;
            case LeechSeedEffect: ApplyLeechSeed(ctx); break;
            case BindEffect: ApplyBind(ctx); break;
            case ProtectEffect:
                int protectStart = _log.Count;
                if (VolatileEffects.ProtectSucceeds(ctx.Source.ProtectChain, _rng, out double protectDraw))
                {
                    ctx.Source.SetProtected();
                    _log.Add(new Protected(ctx.SourceSlot));
                    AddTrace(ctx.TraceAction, ctx.SourceSlot, null, EffectTraceKind.Protect, true, protectDraw, 1,
                        protectStart, _log.Count, 1);
                }
                else
                {
                    ctx.Source.ResetProtectChain();
                    _log.Add(new ProtectFailed(ctx.SourceSlot));
                    ctx.Action.MarkFailed();
                    AddTrace(ctx.TraceAction, ctx.SourceSlot, null, EffectTraceKind.Protect, true, protectDraw, 0,
                        protectStart, _log.Count, 1);
                }
                break;
            case ForceSwitchEffect: ForceSwitch(ctx); break;
            case PositionSwapEffect: SwapPositions(ctx); break;
            case RedirectEffect redirect: _redirects.Add(new RedirectCondition(ctx.SourceSlot, redirect.Priority,
                redirect.AcceptedClasses, redirect.BypassClasses, redirect.AcceptedTags, redirect.BypassTags)); break;
            case DrainEffect d when ctx.ActionDamageDealt > 0:
                Heal(ctx.Source, ctx.SourceSlot, EffectMath.DrainHeal(ctx.ActionDamageDealt, d.Fraction.Num, d.Fraction.Den));
                break;
            case HealEffect h:
                (BattleCreature healRecipient, BattleSlot healSlot) = FractionRecipient(ctx, h.Recipient);
                if (!healRecipient.IsFainted)
                {
                    BattleHookDispatchSnapshot weather = WeatherConditions.CollectHealingHooks(
                        ConditionSnapshot, h, healRecipient.MaxHp, ctx.TraceAction);
                    _hookTrace.AddRange(weather.Trace);
                    Heal(healRecipient, healSlot,
                        EffectMath.HealAmount(healRecipient.MaxHp, h.Fraction.Num, h.Fraction.Den),
                        weather.QueryModifiers(BattleQueryId.Healing));
                }
                break;
            case HpFractionEffect h:
                ApplyHpFraction(ctx, h);
                break;
            case HpEqualizeEffect equalize:
                ApplyHpEqualize(ctx, equalize);
                break;
            case StatusPowerEffect or StatusCountPowerEffect or CannotKoEffect or SpeedRatioPowerEffect
                or MetricBandPowerEffect or MetricRatioPowerEffect or ConsecutivePowerEffect or HistoryPowerEffect
                or WeatherAccuracyEffect:
                break; // evaluated in ComputeHit before DamageCalc.
            case RecoilEffect r when ctx.ActionDamageDealt > 0:
                Sap(ctx.Source, ctx.SourceSlot, EffectMath.RecoilDamage(ctx.ActionDamageDealt, r.Fraction.Num, r.Fraction.Den),
                    amt => new Recoiled(ctx.SourceSlot, amt));
                break;
            case CritBoostEffect cb:
                ctx.Source.RaiseCrit(cb.Stages);
                _log.Add(new CritBoosted(ctx.SourceSlot));
                break;
            case SelfDestructEffect when !ctx.Source.IsFainted:
                ctx.Source.TakeDamage(ctx.Source.MaxHp);
                RecordFaint(ctx.SourceSlot);
                break;
            case EntryHazardEffect:
                int layers = _spikeLayers[(int)ctx.TargetSide] = Math.Min(3, _spikeLayers[(int)ctx.TargetSide] + 1);
                _log.Add(new HazardSet(ctx.TargetSide, layers));
                break;
            case StealthRockEffect when !_stealthRock[(int)ctx.TargetSide]:
                _stealthRock[(int)ctx.TargetSide] = true;
                _log.Add(new StealthRockSet(ctx.TargetSide));
                break;
            case SetWeatherEffect w when w.Weather != CurrentWeather:
                SetWeather(w.Weather, WeatherConditions.DefaultTurns, ctx.SourceSlot);
                break;
            case MoveGateEffect:
                break; // evaluated before PP/RNG in PassesMoveGates.
            case QueueActionGateEffect gate:
                QueueActionGate(ctx, gate.Turns);
                break;
        }
        return true;
    }

    private void SwapPositions(EffectContext ctx)
    {
        int eventStart = _log.Count;
        if (ctx.SourceSlot.Side != ctx.TargetSlot.Side || ctx.SourceSlot == ctx.TargetSlot)
            return;

        int sourcePartyIndex = ActiveIndex(ctx.SourceSlot);
        int targetPartyIndex = ActiveIndex(ctx.TargetSlot);
        _activeSlots.Swap(ctx.SourceSlot, ctx.TargetSlot);
        TraceCleanup(_intentQueue.OwnerSwitched(ctx.SourceSide, sourcePartyIndex, ctx.TargetSlot));
        TraceCleanup(_intentQueue.OwnerSwitched(ctx.TargetSide, targetPartyIndex, ctx.SourceSlot));
        _log.Add(new PositionsSwapped(ctx.SourceSlot, ctx.TargetSlot));
        AddTrace(ctx.TraceAction, ctx.SourceSlot, ctx.TargetSlot, EffectTraceKind.PositionSwap, false,
            null, 1, eventStart, _log.Count);
    }

    private static HashSet<string> MoveTags(BattleMove move)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal) { move.DamageClass == DamageClass.Status ? "status" : "damaging" };
        if (move.MakesContact) tags.Add("contact");
        return tags;
    }

    private sealed record RedirectCondition(BattleSlot Slot, int Priority,
        IReadOnlySet<DamageClass> AcceptedClasses, IReadOnlySet<DamageClass> BypassClasses,
        IReadOnlySet<string> AcceptedTags, IReadOnlySet<string> BypassTags);

    private void ApplyHpFraction(EffectContext ctx, HpFractionEffect effect)
    {
        (BattleCreature recipient, BattleSlot slot) = FractionRecipient(ctx, effect.Recipient);
        if (recipient.IsFainted)
            return;

        int amount = EffectMath.HpFractionAmount(recipient.CurrentHp, recipient.MaxHp, effect.Basis, effect.Fraction);
        if (effect.Operation == HpFractionOperation.Heal)
        {
            Heal(recipient, slot, amount);
            return;
        }

        int calculated = amount;
        int floor = HpStatusFormulas.CannotKoFloor(ctx.Move);
        if (floor > 0)
            amount = Math.Min(amount, Math.Max(0, recipient.CurrentHp - floor));
        int before = recipient.CurrentHp;
        Sap(recipient, slot, amount, damaged => new HpFractionDamaged(slot, damaged));
        if (effect.Recipient == HpFractionRecipient.Target)
        {
            int actual = before - recipient.CurrentHp;
            ctx.TargetContext!.AddDamage(ctx.Action, actual);
            BattleHistoryOwner target = HistoryOwner(ctx.TargetSlot);
            RecordDamage(new BattleActionAttemptId(Turn, ctx.TraceAction), HistoryOwner(ctx.SourceSlot), target,
                ctx.Move, BattleDamageCause.HpFormula, NextDamageHitNumber(ctx.TraceAction, target), true,
                actual > 0 ? BattleDamageFailure.None : BattleDamageFailure.NoDamage,
                calculated, new DamageApplication(amount, actual), critical: false);
        }
    }

    private void ApplyHpEqualize(EffectContext ctx, HpEqualizeEffect effect)
    {
        int eventStart = _log.Count;
        int sourceBefore = ctx.Source.CurrentHp, targetBefore = ctx.Target.CurrentHp;
        if (effect.Mode == HpEqualizeMode.MatchSource)
        {
            double effectiveness = _chart.Effectiveness(ctx.Move.Type, ctx.Target.Types);
            if (targetBefore <= sourceBefore || effectiveness <= 0)
            {
                BattleHistoryOwner targetOwner = HistoryOwner(ctx.TargetSlot);
                RecordDamage(new BattleActionAttemptId(Turn, ctx.TraceAction), HistoryOwner(ctx.SourceSlot),
                    targetOwner, ctx.Move, BattleDamageCause.HpFormula,
                    NextDamageHitNumber(ctx.TraceAction, targetOwner), true,
                    effectiveness <= 0 ? BattleDamageFailure.Immune : BattleDamageFailure.NoDamage,
                    0, default, critical: false);
                AddTrace(ctx.TraceAction, ctx.SourceSlot, ctx.TargetSlot, EffectTraceKind.HpFormula, false,
                    null, targetBefore, eventStart, eventStart);
                return;
            }
            int calculated = targetBefore - sourceBefore;
            DamageApplication applied = DealMoveDamage(ctx.Target, ctx.TargetSlot, calculated, 1.0, crit: false);
            ctx.Target.RecordDamageTaken(ctx.Move.DamageClass, applied.ActualHpRemoved);
            ctx.TargetContext!.AddDamage(ctx.Action, applied.ActualHpRemoved);
            BattleHistoryOwner target = HistoryOwner(ctx.TargetSlot);
            RecordDamage(new BattleActionAttemptId(Turn, ctx.TraceAction), HistoryOwner(ctx.SourceSlot), target,
                ctx.Move, BattleDamageCause.HpFormula, NextDamageHitNumber(ctx.TraceAction, target), true,
                applied.ActualHpRemoved > 0 ? BattleDamageFailure.None : BattleDamageFailure.NoDamage,
                calculated, applied, critical: false);
            _log.Add(new HpFormulaChanged(ctx.TargetSlot, targetBefore, ctx.Target.CurrentHp, effect.Mode));
            AddTrace(ctx.TraceAction, ctx.SourceSlot, ctx.TargetSlot, EffectTraceKind.HpFormula, false,
                null, ctx.Target.CurrentHp, eventStart, _log.Count);
            return;
        }

        int average = (int)(((long)sourceBefore + targetBefore) / 2);
        SetFormulaHp(ctx.Source, ctx.SourceSlot, average, effect.Mode);
        SetFormulaHp(ctx.Target, ctx.TargetSlot, average, effect.Mode);
        AddTrace(ctx.TraceAction, ctx.SourceSlot, ctx.TargetSlot, EffectTraceKind.HpFormula, false,
            null, ctx.Target.CurrentHp, eventStart, _log.Count);
    }

    private void SetFormulaHp(BattleCreature creature, BattleSlot slot, int value, HpEqualizeMode formula)
    {
        int before = creature.CurrentHp;
        int after = Math.Clamp(value, 0, creature.MaxHp);
        if (after < before) creature.TakeDamage(before - after);
        else if (after > before) creature.Heal(after - before);
        if (after != before)
            _log.Add(new HpFormulaChanged(slot, before, after, formula));
    }

    private static (BattleCreature Creature, BattleSlot Slot) FractionRecipient(
        EffectContext ctx,
        HpFractionRecipient recipient) => recipient switch
    {
        HpFractionRecipient.Self => (ctx.Source, ctx.SourceSlot),
        HpFractionRecipient.Target => (ctx.Target, ctx.TargetSlot),
        _ => throw new ArgumentOutOfRangeException(nameof(recipient), recipient, "Unknown HP-fraction recipient."),
    };

    private bool PassesMoveGates(BattleCreature creature, BattleSlot slot, BattleMove move, int traceAction)
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
                int start = _log.Count;
                _log.Add(new MoveFailed(slot, move.Move, reason));
                AddTrace(traceAction, slot, null, EffectTraceKind.MoveGate, false, null, 0, start, _log.Count);
                return false;
            }
            AddTrace(traceAction, slot, null, EffectTraceKind.MoveGate, false, null, 1, _log.Count, _log.Count);
        }
        return true;
    }

    private void QueueActionGate(EffectContext ctx, int turns)
    {
        if (Outcome is not null)
            return;
        BattleIntent intent = _intentQueue.Enqueue(new BattleIntentRequest(
            Turn + turns,
            BattleIntentCheckpoint.PreAction,
            new BattleIntentOwner(BattleIntentOwnerScope.Slot, ctx.SourceSide, ctx.SourceSlot, null,
                BattleIntentSwitchPolicy.StaySlot, BattleIntentFaintPolicy.Persist),
            new BattleIntentTarget(BattleIntentTargetPolicy.Source),
            new SkipActionIntent(),
            ctx.Move.Move,
            ctx.TraceAction));
        AddIntentTrace(intent, EffectTraceKind.IntentEnqueued, true, _log.Count, _log.Count);
    }

    private static readonly EntityId RockType = EntityId.Parse("type:rock");

    /// <summary>on_switch_in hook (catalog §7.3): a creature entering a side with entry hazards takes
    /// damage — Stealth Rock (type-scaled) before Spikes (layer-scaled). Draws no RNG. Battle-start
    /// actives never trigger it (no hazards yet).</summary>
    private void OnSwitchIn(BattleSide side) => OnSwitchIn(new BattleSlot(side, 0));

    private void OnSwitchIn(BattleSlot slot)
    {
        BattleSide side = slot.Side;
        BattleCreature c = Active(slot);
        if (c.IsFainted)
            return;

        ReevaluateConditionForm(slot);
        ApplyHookInvocations(Topology.ActiveSlotsPerSide == 1
            ? BattleHookDispatcher.SwitchIn(side, HookSources())
            : BattleHookDispatcher.SwitchIn(slot, HookSources()));

        if (_stealthRock[(int)side])
        {
            double eff = _chart.Effectiveness(RockType, c.Types);
            Sap(c, slot, EffectMath.TypeScaledHazardDamage(c.MaxHp, eff), amt => new HurtByHazard(slot, amt));
        }
        if (!c.IsFainted && _spikeLayers[(int)side] > 0)
            Sap(c, slot, EffectMath.HazardDamage(c.MaxHp, _spikeLayers[(int)side]), amt => new HurtByHazard(slot, amt));
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
    private void ForceSwitch(EffectContext ctx)
    {
        int eventStart = _log.Count;
        BattleSide side = ctx.TargetSide;
        if (ctx.Target.IsFainted)
        {
            AddTrace(ctx.TraceAction, ctx.SourceSlot, ctx.TargetSlot, EffectTraceKind.ForceSwitchReserve, false,
                null, 0, eventStart, eventStart);
            return;
        }

        if (IsWild && side == BattleSide.Enemy)
        {
            _log.Add(new ForcedOut(ctx.TargetSlot));
            EndBattle(Opponent(side)); // scared the wild creature off
            AddTrace(ctx.TraceAction, ctx.SourceSlot, ctx.TargetSlot, EffectTraceKind.ForceSwitchReserve, false,
                null, 0, eventStart, _log.Count);
            return;
        }

        var reserves = new List<int>();
        for (int i = 0; i < _parties[(int)side].Count; i++)
            if (!_activeSlots.IsActive(side, i) && !_parties[(int)side][i].IsFainted)
                reserves.Add(i);
        if (reserves.Count == 0)
        {
            AddTrace(ctx.TraceAction, ctx.SourceSlot, ctx.TargetSlot, EffectTraceKind.ForceSwitchReserve, false,
                null, 0, eventStart, eventStart);
            return; // nothing to drag out
        }

        _log.Add(new ForcedOut(ctx.TargetSlot));
        int? draw = reserves.Count > 1 ? _rng.Next(reserves.Count) : null;
        int selected = reserves[draw ?? 0];
        SwitchTo(ctx.TargetSlot, selected);
        AddTrace(ctx.TraceAction, ctx.SourceSlot, ctx.TargetSlot, EffectTraceKind.ForceSwitchReserve,
            draw is not null, draw, selected, eventStart, _log.Count, draw is not null ? reserves.Count : null);
    }

    private void ApplyLeechSeed(EffectContext ctx)
    {
        if (ctx.Target.IsFainted || ctx.Target.Seeded)
            return;
        if (ctx.Target.Types.Contains(ctx.Move.Type)) // a seed of its own type can't take hold (grass immune to grass Leech Seed)
            return;
        ctx.Target.SetSeeded(true);
        _log.Add(new LeechSeeded(ctx.TargetSlot));
    }

    private void ApplyAilment(EffectContext ctx, AilmentEffect effect)
    {
        int start = _log.Count;
        bool eligible = !ctx.Target.IsFainted
            && StatusEffects.CanApplyStatus(ctx.Target.Status)
            && !StatusEffects.TypeImmuneToStatus(effect.Status, ctx.Target.Types)
            && !BlocksStatus(ctx.TargetSlot, effect.Status);
        if (eligible)
        {
            BattleHookDispatchSnapshot weather = WeatherConditions.CollectStatusHooks(
                ConditionSnapshot, effect.Status, ctx.TraceAction);
            _hookTrace.AddRange(weather.Trace);
            eligible = !weather.Filters().Any(filter => filter is
                { Filter.Value: "status_attempt", Decision: BattleHookFilterDecision.Deny });
        }
        EffectChanceResult chance = CheckEffectChance(EffectiveEffectChance(ctx, effect), eligible);
        if (chance.Passed)
        {
            ctx.Target.SetStatus(effect.Status);
            _log.Add(new StatusApplied(ctx.TargetSlot, effect.Status));
        }
        TraceEffectChance(ctx, chance, start);
    }

    private void ApplyStatChange(EffectContext ctx, StatChangeEffect effect)
    {
        BattleCreature recipient = effect.OnSelf ? ctx.Source : ctx.Target;
        BattleSlot recipientSlot = effect.OnSelf ? ctx.SourceSlot : ctx.TargetSlot;
        int start = _log.Count;
        EffectChanceResult chance = CheckEffectChance(EffectiveEffectChance(ctx, effect), !recipient.IsFainted);
        if (!chance.Passed)
        {
            TraceEffectChance(ctx, chance, start);
            return;
        }
        recipient.ChangeStage(effect.Stat, effect.Delta);
        _log.Add(new StatStageChanged(recipientSlot, effect.Stat, effect.Delta));
        TraceEffectChance(ctx, chance, start);
    }

    private static readonly StatKind[] AllStageStats =
        [StatKind.Atk, StatKind.Def, StatKind.Spa, StatKind.Spd, StatKind.Spe];
    private static readonly StatKind[] AllStageSlots =
        [StatKind.Atk, StatKind.Def, StatKind.Spa, StatKind.Spd, StatKind.Spe, StatKind.Accuracy, StatKind.Evasion];
    private static readonly StatKind[] OffenseStageStats = [StatKind.Atk, StatKind.Spa];
    private static readonly StatKind[] DefenseStageStats = [StatKind.Def, StatKind.Spd];

    private void ApplyStatChangeAll(EffectContext ctx, StatChangeAllEffect effect)
    {
        BattleCreature recipient = effect.OnSelf ? ctx.Source : ctx.Target;
        BattleSlot recipientSlot = effect.OnSelf ? ctx.SourceSlot : ctx.TargetSlot;
        int start = _log.Count;
        EffectChanceResult chance = CheckEffectChance(EffectiveEffectChance(ctx, effect), !recipient.IsFainted);
        if (!chance.Passed)
        {
            TraceEffectChance(ctx, chance, start);
            return;
        }
        foreach (StatKind stat in AllStageStats)
        {
            recipient.ChangeStage(stat, effect.Delta);
            _log.Add(new StatStageChanged(recipientSlot, stat, effect.Delta));
        }
        TraceEffectChance(ctx, chance, start);
    }

    private bool ApplyHpCost(EffectContext ctx, HpCostEffect effect)
    {
        int amount = Math.Max(1, ctx.Source.MaxHp * effect.Fraction.Num / effect.Fraction.Den);
        if (!effect.AllowFaint && ctx.Source.CurrentHp <= amount)
            return false;
        Sap(ctx.Source, ctx.SourceSlot, amount, amt => new HpCostPaid(ctx.SourceSlot, amt));
        return !ctx.Source.IsFainted;
    }

    private void ApplyStatReset(EffectContext ctx, StatResetEffect effect)
    {
        (BattleCreature Creature, BattleSlot Slot)[] recipients = StageRecipients(ctx, effect.Scope)
            .Where(recipient => !recipient.Creature.IsFainted).ToArray();
        int start = _log.Count;
        EffectChanceResult chance = CheckEffectChance(EffectiveEffectChance(ctx, effect), recipients.Length > 0);
        if (!chance.Passed)
        {
            TraceEffectChance(ctx, chance, start);
            return;
        }
        foreach ((BattleCreature creature, BattleSlot slot) in recipients)
            foreach (StatKind stat in AllStageSlots)
                SetStage(creature, slot, stat, 0);
        TraceEffectChance(ctx, chance, start);
    }

    private void ApplyStatCopy(EffectContext ctx, StatCopyEffect effect)
    {
        (BattleCreature from, _) = StageRecipient(ctx, effect.From);
        (BattleCreature to, BattleSlot toSlot) = StageRecipient(ctx, effect.To);
        int start = _log.Count;
        EffectChanceResult chance = CheckEffectChance(EffectiveEffectChance(ctx, effect), !from.IsFainted && !to.IsFainted);
        if (!chance.Passed)
        {
            TraceEffectChance(ctx, chance, start);
            return;
        }
        foreach (StatKind stat in AllStageSlots)
            SetStage(to, toSlot, stat, from.Stage(stat));
        TraceEffectChance(ctx, chance, start);
    }

    private void ApplyStatSwap(EffectContext ctx, StatSwapEffect effect)
    {
        int start = _log.Count;
        EffectChanceResult chance = CheckEffectChance(EffectiveEffectChance(ctx, effect), !ctx.Source.IsFainted && !ctx.Target.IsFainted);
        if (!chance.Passed)
        {
            TraceEffectChance(ctx, chance, start);
            return;
        }
        foreach (StatKind stat in SwapStats(effect.Group))
        {
            int sourceStage = ctx.Source.Stage(stat);
            int targetStage = ctx.Target.Stage(stat);
            SetStage(ctx.Source, ctx.SourceSlot, stat, targetStage);
            SetStage(ctx.Target, ctx.TargetSlot, stat, sourceStage);
        }
        TraceEffectChance(ctx, chance, start);
    }

    private void ApplyStatInvert(EffectContext ctx, StatInvertEffect effect)
    {
        BattleCreature recipient = effect.OnSelf ? ctx.Source : ctx.Target;
        BattleSlot recipientSlot = effect.OnSelf ? ctx.SourceSlot : ctx.TargetSlot;
        int start = _log.Count;
        EffectChanceResult chance = CheckEffectChance(EffectiveEffectChance(ctx, effect), !recipient.IsFainted);
        if (!chance.Passed)
        {
            TraceEffectChance(ctx, chance, start);
            return;
        }
        foreach (StatKind stat in AllStageSlots)
            SetStage(recipient, recipientSlot, stat, -recipient.Stage(stat));
        TraceEffectChance(ctx, chance, start);
    }

    private void SetStage(BattleCreature creature, BattleSlot slot, StatKind stat, int value)
    {
        if (creature.IsFainted)
            return;
        int before = creature.Stage(stat);
        creature.SetStage(stat, value);
        int delta = creature.Stage(stat) - before;
        if (delta != 0)
            _log.Add(new StatStageChanged(slot, stat, delta));
    }

    private readonly record struct EffectChanceResult(bool Passed, bool Performed, int? Draw);

    private int EffectiveEffectChance(EffectContext ctx, MoveEffect effect)
    {
        if (effect.ChanceFormula is null)
            return effect.Chance;
        BattleQueryResult calculated = HpStatusFormulas.SecondaryChanceQuery(effect, ctx.Source, ctx.Target);
        BattleQueryResult result = calculated with
        {
            Inputs = new BattleQueryInputs(ctx.SourceSlot, ctx.TargetSlot, CurrentWeather, calculated.Inputs.Ruleset),
        };
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, ctx.TraceAction, ctx.SourceSlot, ctx.TargetSlot, result));
        return result.FinalValue.ToInt32();
    }

    private EffectChanceResult CheckEffectChance(int chance, bool eligible)
    {
        if (!eligible || chance <= 0)
            return new(false, false, null);
        if (chance >= 100)
            return new(true, false, null);
        int draw = _rng.Next(100);
        return new(draw < chance, true, draw);
    }

    private void TraceEffectChance(EffectContext ctx, EffectChanceResult chance, int eventStart) =>
        TraceChance(ctx.TraceAction, ctx.SourceSlot, ctx.TargetSlot, EffectTraceKind.EffectChance, chance, eventStart);

    private void TraceChance(int action, BattleSlot source, BattleSlot target, EffectTraceKind kind,
        EffectChanceResult chance, int eventStart) =>
        AddTrace(action, source, target, kind, chance.Performed,
            chance.Draw, chance.Passed ? 1 : 0, eventStart, _log.Count, chance.Performed ? 100 : null);

    private static IEnumerable<(BattleCreature Creature, BattleSlot Slot)> StageRecipients(EffectContext ctx, StageEffectScope scope)
    {
        if (scope is StageEffectScope.Self or StageEffectScope.Both)
            yield return (ctx.Source, ctx.SourceSlot);
        if (scope is StageEffectScope.Target or StageEffectScope.Both)
            yield return (ctx.Target, ctx.TargetSlot);
    }

    private static (BattleCreature Creature, BattleSlot Slot) StageRecipient(EffectContext ctx, StageEffectScope scope) =>
        scope switch
        {
            StageEffectScope.Self => (ctx.Source, ctx.SourceSlot),
            StageEffectScope.Target => (ctx.Target, ctx.TargetSlot),
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
        int start = _log.Count;
        EffectChanceResult chance = CheckEffectChance(EffectiveEffectChance(ctx, effect), !ctx.Target.IsFainted && !ctx.Target.IsConfused);
        TraceEffectChance(ctx, chance, start);
        if (!chance.Passed)
            return;

        int durationStart = _log.Count;
        int duration = VolatileEffects.ConfusionDuration(_rng, out int draw);
        ctx.Target.SetConfusion(duration);
        _log.Add(new Confused(ctx.TargetSlot));
        AddTrace(ctx.TraceAction, ctx.SourceSlot, ctx.TargetSlot, EffectTraceKind.ConfusionDuration, true,
            draw, duration, durationStart, _log.Count, 5, 1);
    }

    private void ApplyBind(EffectContext ctx)
    {
        int eventStart = _log.Count;
        if (ctx.Target.IsFainted || ctx.Target.IsTrapped)
        {
            AddTrace(ctx.TraceAction, ctx.SourceSlot, ctx.TargetSlot, EffectTraceKind.TrapDuration, false,
                null, 0, eventStart, eventStart);
            return;
        }

        int duration = _rng.Next(4, 6);
        ctx.Target.SetTrap(duration);
        _log.Add(new Bound(ctx.TargetSlot));
        AddTrace(ctx.TraceAction, ctx.SourceSlot, ctx.TargetSlot, EffectTraceKind.TrapDuration, true,
            duration, duration, eventStart, _log.Count, 6, 4);
    }

    private void ApplyFlinch(EffectContext ctx, FlinchEffect effect)
    {
        // Flinch only bites if the target hasn't acted yet — resolution order gives us that for free.
        int start = _log.Count;
        EffectChanceResult chance = CheckEffectChance(EffectiveEffectChance(ctx, effect), !ctx.Target.IsFainted);
        if (chance.Passed)
            ctx.Target.SetFlinch();
        TraceEffectChance(ctx, chance, start);
    }

    private void ApplyContactEffects(BattleMove move, BattleSlot sourceSlot, BattleCreature source, BattleSlot targetSlot,
        int traceAction)
    {
        if (!move.MakesContact || source.IsFainted)
            return;

        foreach (BattleHookInvocation invocation in BattleHookDispatcher.ContactReceived(targetSlot, HookSources()))
        {
            Effect effect = invocation.Effect;
            if (effect.Op != "contactChanceEffect")
                continue;

            int start = _log.Count;
            int chance = effect.Chance ?? 100;
            bool eligible = !source.IsFainted;
            string statusName = Str(effect, "status");
            if (statusName.Length > 0)
            {
                PersistentStatus status = Parse<PersistentStatus>(statusName);
                eligible = eligible
                    && StatusEffects.CanApplyStatus(source.Status)
                    && !StatusEffects.TypeImmuneToStatus(status, source.Types)
                    && !BlocksStatus(sourceSlot, status);
            }
            EffectChanceResult result = CheckEffectChance(chance, eligible);
            if (!result.Passed)
            {
                TraceChance(traceAction, sourceSlot, targetSlot, EffectTraceKind.ContactChance, result, start);
                continue;
            }

            if (Int(effect, "damage") is { } damage)
            {
                Sap(source, sourceSlot, damage, amount => new ContactDamaged(sourceSlot, amount));
                TraceChance(traceAction, sourceSlot, targetSlot, EffectTraceKind.ContactChance, result, start);
                continue;
            }

            if (statusName.Length > 0)
            {
                PersistentStatus status = Parse<PersistentStatus>(statusName);
                source.SetStatus(status);
                _log.Add(new StatusApplied(sourceSlot, status));
                TraceChance(traceAction, sourceSlot, targetSlot, EffectTraceKind.ContactChance, result, start);
                continue;
            }

            string statName = Str(effect, "stat");
            if (statName.Length > 0)
            {
                StatKind stat = Parse<StatKind>(statName);
                int delta = Int(effect, "delta") ?? 0;
                source.ChangeStage(stat, delta);
                _log.Add(new StatStageChanged(sourceSlot, stat, delta));
            }
            TraceChance(traceAction, sourceSlot, targetSlot, EffectTraceKind.ContactChance, result, start);
        }
    }

    // ---- Reusable effect primitives (shared by every op that heals or saps HP) ----

    /// <summary>Restore HP and log it. No-op if the creature is full or the amount is ≤0. Used by
    /// drain, healFraction, and Leech Seed's beneficiary.</summary>
    private void Heal(BattleCreature c, BattleSide side, int amount)
    {
        amount = BattleQuery.ResolveInteger(BattleQueryId.Healing, amount);
        if (amount <= 0 || c.CurrentHp >= c.MaxHp)
            return;
        int before = c.CurrentHp;
        c.Heal(amount);
        _log.Add(new Healed(side, c.CurrentHp - before));
    }

    private void Heal(BattleCreature c, BattleSlot slot, int amount,
        IReadOnlyList<BattleQueryModifier>? modifiers = null)
    {
        BattleQueryResult result = BattleQuery.Evaluate(BattleQueryId.Healing, new BattleQueryValue(amount),
            modifiers,
            context: new BattleQueryContext(slot, c, slot, c, CurrentWeather));
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, _traceActionSequence, slot, slot, result));
        amount = result.FinalValue.ToInt32();
        if (amount <= 0 || c.CurrentHp >= c.MaxHp)
            return;
        int before = c.CurrentHp;
        c.Heal(amount);
        _log.Add(new Healed(slot, c.CurrentHp - before));
    }

    /// <summary>Deal non-move HP loss (recoil/crash/leech), log it via the supplied event factory, and
    /// record a faint. Used by recoil, crash-on-miss, and Leech Seed's victim.</summary>
    private void Sap(BattleCreature c, BattleSlot slot, int amount, Func<int, BattleEvent> lossEvent)
    {
        if (amount <= 0)
            return;
        c.TakeDamage(amount);
        _log.Add(lossEvent(amount));
        if (c.IsFainted)
            RecordFaint(slot);
    }

    private void ApplyCrashRecoil(BattleCreature attacker, BattleSlot slot, BattleMove move)
    {
        if (move.Recoil is not { } crash || !move.RecoilOnMiss)
            return;
        Sap(attacker, slot, EffectMath.CrashDamage(attacker.MaxHp, crash.Num, crash.Den), amt => new Recoiled(slot, amt));
    }

    /// <summary>Sap HP from a victim and heal it to a beneficiary — the shared "drain life" primitive
    /// behind Leech Seed (Absorb/Mega Drain use <see cref="Heal"/> against damage already dealt).</summary>
    private void DrainLife(BattleCreature victim, BattleSlot victimSlot, BattleCreature beneficiary,
        BattleSlot beneficiarySlot, int amount)
    {
        Sap(victim, victimSlot, amount, amt => new LeechSapped(victimSlot, amt));
        Heal(beneficiary, beneficiarySlot, amount);
    }

    /// <summary>One hit of a damaging move — draws crit then damage roll (fixed order), applies
    /// crit's stat-stage ignore rule and burn. Returned <c>eff</c> feeds the DamageDealt event.</summary>
    private DamageApplication DealMoveDamage(BattleCreature target, BattleSlot targetSlot, int amount,
        double effectiveness, bool crit,
        int hpFloor = 0)
    {
        int before = target.CurrentHp;
        amount = ApplySurviveFromFull(target, targetSlot, amount);
        if (hpFloor > 0)
            amount = Math.Min(amount, Math.Max(0, target.CurrentHp - hpFloor));
        target.TakeDamage(amount);
        int dealt = before - target.CurrentHp;
        _log.Add(new DamageDealt(targetSlot, dealt, effectiveness, crit));
        if (target.IsFainted)
            RecordFaint(targetSlot);
        return new DamageApplication(amount, dealt);
    }

    private void RecordDamage(BattleActionAttemptId attempt, BattleHistoryOwner source,
        BattleHistoryOwner target, BattleMove move, BattleDamageCause cause, int hitNumber, bool attempted,
        BattleDamageFailure failure, int calculatedDamage, DamageApplication damage, bool critical,
        bool substitute = false, EntityId? effectiveType = null)
    {
        bool connected = damage.ActualHpRemoved > 0 && !substitute;
        _actionHistory.RecordDamage(new BattleDamageRecord(
            attempt, source, target, move.Move, move.DamageClass, effectiveType ?? move.Type, cause, hitNumber,
            attempted, connected, failure, calculatedDamage, damage.AppliedDamage, damage.ActualHpRemoved,
            critical, move.MakesContact, substitute, connected && Active(target.Slot).IsFainted));
    }

    private static bool RecordsMoveDamage(BattleMove move) =>
        HpStatusFormulas.HasBasePower(move) || move.CounterCategory is not null || move.Ohko
        || move.FixedDamage is not null || move.FixedDamageLevel
        || move.SecondaryEffects.Any(effect => effect is HpFractionEffect
            { Recipient: HpFractionRecipient.Target, Operation: HpFractionOperation.Damage }
            or HpEqualizeEffect { Mode: HpEqualizeMode.MatchSource });

    private static BattleDamageCause DamageCause(BattleMove move) =>
        HpStatusFormulas.HasBasePower(move) ? BattleDamageCause.Standard
        : move.CounterCategory is not null ? BattleDamageCause.Counter
        : move.Ohko ? BattleDamageCause.OneHitKnockout
        : move.FixedDamageLevel ? BattleDamageCause.Level
        : move.FixedDamage is not null ? BattleDamageCause.Fixed
        : BattleDamageCause.HpFormula;

    private int NextDamageHitNumber(int actionSequence, BattleHistoryOwner target) =>
        _actionHistory.DamageSnapshot()
            .Where(record => record.Attempt == new BattleActionAttemptId(Turn, actionSequence)
                && record.Target.Side == target.Side && record.Target.PartyIndex == target.PartyIndex)
            .Select(record => record.HitNumber)
            .DefaultIfEmpty()
            .Max() + 1;

    private void RecordFaint(BattleSlot slot)
    {
        _log.Add(new Fainted(slot));
        _actionHistory.RecordFaint(HistoryOwner(slot));
    }

    private int ApplySurviveFromFull(BattleCreature target, BattleSlot targetSlot, int amount)
    {
        if (amount < target.CurrentHp || target.CurrentHp != target.MaxHp || target.HasConsumedHeldEffect("surviveFromFull"))
            return amount;
        if (!target.HeldItemBattleEffects.Any(e => e.Op == "surviveFromFull"))
            return amount;

        target.ConsumeHeldEffect("surviveFromFull");
        _log.Add(new HeldItemConsumed(targetSlot, "surviveFromFull"));
        ReevaluateConditionForms();
        return Math.Max(0, target.CurrentHp - 1);
    }

    private bool TryItemPower(BattleSlot sourceSlot, BattleMove move, out int? power)
    {
        power = null;
        if (move.SecondaryEffects.OfType<ItemDataPowerEffect>().SingleOrDefault() is not { Field: ItemPowerField.FlingPower })
            return true;

        BattleCreature source = Active(sourceSlot);
        EntityId? heldItem = PhysicalMetricFormulas.EffectiveValues(source, _overlays,
            new BattleOverlayOwner(sourceSlot.Side, ActiveIndex(sourceSlot), sourceSlot)).HeldItem;
        if (heldItem is not { } itemId || !_itemData.TryGetValue(itemId, out Item? item) || item.FlingPower is not > 0)
            return false;

        power = item.FlingPower;
        return true;
    }

    private int? SelectRandomPower(BattleSlot sourceSlot, BattleMove move, int traceAction)
    {
        if (move.SecondaryEffects.OfType<RandomTablePowerEffect>().SingleOrDefault() is not { } table)
            return null;

        int power = PartyResourceFormulas.SelectWeightedPower(table.Entries, _rng, out int? draw, out int totalWeight);
        AddTrace(traceAction, sourceSlot, null, EffectTraceKind.PowerTable, draw is not null, draw, power,
            _log.Count, _log.Count, totalWeight);
        return power;
    }

    private (int Dmg, bool Crit, double Eff) ComputeHit(
        BattleSlot sourceSlot,
        BattleSlot targetSlot,
        BattleCreature attacker,
        BattleCreature target,
        BattleMove move,
        EntityId moveType,
        int power,
        int snapshottedLiveTargets,
        int ppBeforeSpend,
        int? itemPower,
        int? randomPower,
        int traceAction,
        out double? critDraw,
        out int? damageRollDraw)
    {
        critDraw = null;
        damageRollDraw = null;
        PhysicalFormulaInputs? physicalInputs = PhysicalMetricFormulas.HasPowerFormula(move)
            ? PhysicalMetricFormulas.Inputs(attacker, target, _overlays,
                new BattleOverlayOwner(sourceSlot.Side, ActiveIndex(sourceSlot), sourceSlot),
                new BattleOverlayOwner(targetSlot.Side, ActiveIndex(targetSlot), targetSlot))
            : null;
        BattleActionFormulaInputs? actionInputs = ActionHistoryFormulas.HasPowerFormula(move)
            ? _actionHistory.PowerInputs(HistoryOwner(sourceSlot), HistoryOwner(targetSlot), move.Move)
            : null;
        PartyResourceFormulaInputs? resourceInputs = PartyResourceFormulas.HasPowerFormula(move)
            ? PartyResourceFormulas.Inputs(Party(sourceSlot.Side), attacker, target,
                ppBeforeSpend, move.Pp, itemPower, randomPower)
            : null;
        HpStatusPowerQuery powerQuery = HpStatusFormulas.PowerQuery(move, attacker, target, physicalInputs,
            actionInputs, resourceInputs);
        var powerModifiers = powerQuery.Modifiers.ToList();
        if (move.SecondaryEffects.OfType<WeatherMoveEffect>().SingleOrDefault() is { } weatherEffect)
        {
            BattleHookDispatchSnapshot weather = WeatherConditions.CollectBasePowerHooks(
                ConditionSnapshot, weatherEffect, traceAction);
            _hookTrace.AddRange(weather.Trace);
            powerModifiers.AddRange(weather.QueryModifiers(BattleQueryId.BasePower)
                .Select(modifier => modifier with { InsertionOrder = powerModifiers.Count }));
        }
        BattleQueryResult powerResult = BattleQuery.Evaluate(BattleQueryId.BasePower,
            new BattleQueryValue(powerQuery.AuthoredBase), powerModifiers,
            new BattleQueryContext(sourceSlot, attacker, targetSlot, target, CurrentWeather));
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction, sourceSlot, targetSlot, powerResult));
        power = powerResult.FinalValue.ToInt32();

        double eff = _chart.Effectiveness(moveType, target.Types);
        if (eff <= 0)
            return (0, false, eff);

        bool physical = move.DamageClass == DamageClass.Physical;
        bool crit = BattleRolls.IsCrit(move.CritStage + attacker.CritStageBonus, _rng, out double critRoll);
        critDraw = critRoll;
        int roll = BattleRolls.DamageRoll(_rng, out int randomRoll);
        damageRollDraw = randomRoll;

        StatKind offStat = move.OffensiveStatOverride ?? (physical ? StatKind.Atk : StatKind.Spa);
        StatKind defStat = move.DefensiveStatOverride ?? (physical ? StatKind.Def : StatKind.Spd);
        int aStage = attacker.Stage(offStat);
        int dStage = target.Stage(defStat);
        if (crit)
        {
            aStage = Math.Max(0, aStage);
            dStage = Math.Min(0, dStage);
        }

        List<BattleQueryModifier> attackModifiers =
        [
            new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply, BattleQuery.StatStageMultiplier(aStage), InsertionOrder: 0),
            .. StatHookModifiers(sourceSlot, targetSlot, sourceSlot, offStat),
        ];
        List<BattleQueryModifier> defenseModifiers =
        [
            new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply, BattleQuery.StatStageMultiplier(dStage), InsertionOrder: 0),
            .. StatHookModifiers(sourceSlot, targetSlot, targetSlot, defStat),
        ];
        BattleQueryResult attackResult = BattleQuery.Evaluate(BattleQueryId.OffensiveStat,
            new BattleQueryValue(StatValue(attacker.Stats, offStat)), attackModifiers,
            new BattleQueryContext(sourceSlot, attacker, targetSlot, target, CurrentWeather));
        BattleQueryResult defenseResult = BattleQuery.Evaluate(BattleQueryId.DefensiveStat,
            new BattleQueryValue(StatValue(target.Stats, defStat)), defenseModifiers,
            new BattleQueryContext(sourceSlot, attacker, targetSlot, target, CurrentWeather));
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction, sourceSlot, targetSlot, attackResult));
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction, sourceSlot, targetSlot, defenseResult));
        int a = attackResult.FinalValue.ToInt32();
        int d = defenseResult.FinalValue.ToInt32();
        double stab = TypeChart.Stab(moveType, attacker.Types);
        bool burn = attacker.Status == PersistentStatus.Burn && physical && !powerQuery.IgnoreSourceBurnPenalty;

        int dmg = DamageCalc.Compute(attacker.Level, power, a, d, eff, stab, crit, roll, burn, snapshottedLiveTargets);
        BattleQueryResult damageResult = BattleQuery.Evaluate(BattleQueryId.FinalDamage, new BattleQueryValue(dmg),
            DamageHookModifiers(move, moveType, sourceSlot, targetSlot),
            new BattleQueryContext(sourceSlot, attacker, targetSlot, target, CurrentWeather));
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction, sourceSlot, targetSlot, damageResult));
        return (damageResult.FinalValue.ToInt32(), crit, eff);
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
        foreach (BattleSlot slot in Topology.Slots)
        {
            BattleCreature c = Active(slot);
            foreach (AbilityHook hook in c.AbilityHooks)
                yield return new BattleHookSource(slot, BattleHookSourceKind.Ability, hook.Hook, hook.Effects);
            foreach (var group in c.HeldItemBattleEffects.Select(e => (Hook: HeldHook(e), Effect: e)).Where(x => x.Hook is not null).GroupBy(x => x.Hook!.Value))
                yield return new BattleHookSource(slot, BattleHookSourceKind.HeldItem, group.Key, group.Select(x => x.Effect).ToList());
        }
    }

    private void ApplyHookInvocations(IEnumerable<BattleHookInvocation> invocations)
    {
        foreach (BattleHookInvocation invocation in invocations)
        {
            if (invocation.Effect.Op == "weatherSummon")
            {
                int turns = Int(invocation.Effect, "duration") ?? WeatherConditions.DefaultTurns;
                SetWeather(Parse<Weather>(Str(invocation.Effect, "weather")),
                    turns + WeatherDurationExtension(invocation.Slot), invocation.Slot);
            }
        }
    }

    private int WeatherDurationExtension(BattleSlot slot) =>
        Active(slot).HeldItemBattleEffects
            .Where(e => e.Op == "weatherDurationExtend")
            .Sum(e => Int(e, "turns") ?? 0);

    private static void ApplyChoiceLock(BattleCreature creature, int moveIndex)
    {
        if (creature.HeldItemBattleEffects.Any(e => e.Op == "choiceLock"))
            creature.SetChoiceLock(moveIndex);
    }

    private IReadOnlyList<BattleQueryModifier> DamageHookModifiers(
        BattleMove move, EntityId moveType, BattleSlot sourceSlot, BattleSlot targetSlot)
    {
        var modifiers = new List<BattleQueryModifier>();
        int insertion = 0;
        foreach (BattleHookInvocation invocation in BattleHookDispatcher.Damage(sourceSlot, targetSlot, HookSources()))
        {
            Effect effect = invocation.Effect;
            if (effect.Op is "typeDamageModify" or "typeDamageBoost")
            {
                if (!string.Equals(Str(effect, "type"), moveType.Slug, StringComparison.OrdinalIgnoreCase))
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
            modifiers.Add(new BattleQueryModifier(BattleQueryStage.Hooks, BattleQueryOperation.Multiply,
                new BattleQueryValue(Int(effect, "multiplierPercent") ?? 100, 100),
                Int(effect, "priority") ?? 0, QueryOwner(invocation, sourceSlot, targetSlot), insertion++));
        }

        BattleHookDispatchSnapshot weather = WeatherConditions.CollectDamageHooks(
            ConditionSnapshot, moveType.Slug, _traceActionSequence);
        _hookTrace.AddRange(weather.Trace);
        modifiers.AddRange(weather.QueryModifiers(BattleQueryId.FinalDamage)
            .Select(modifier => modifier with { InsertionOrder = insertion++ }));
        return modifiers;
    }

    private EntityId EffectiveMoveType(BattleMove move, int traceAction)
    {
        if (move.SecondaryEffects.OfType<WeatherMoveEffect>().SingleOrDefault() is not { } effect)
            return move.Type;
        BattleHookDispatchSnapshot weather = WeatherConditions.CollectMoveTypeHooks(
            ConditionSnapshot, effect, traceAction);
        _hookTrace.AddRange(weather.Trace);
        return weather.MoveTypes().SingleOrDefault() is { } type && type != default ? type : move.Type;
    }

    private IReadOnlyList<BattleQueryModifier> StatHookModifiers(
        BattleSlot sourceSlot, BattleSlot targetSlot, BattleSlot statOwner, StatKind stat)
    {
        var modifiers = new List<BattleQueryModifier>();
        int insertion = 0;
        foreach (BattleHookInvocation invocation in BattleHookDispatcher.Damage(sourceSlot, targetSlot, HookSources()))
        {
            Effect effect = invocation.Effect;
            if (invocation.Slot != statOwner || effect.Op != "statModify")
                continue;
            if (!string.Equals(Str(effect, "stat"), StatSlug(stat), StringComparison.OrdinalIgnoreCase))
                continue;

            int priority = Int(effect, "priority") ?? 0;
            BattleQueryOwnerScope owner = QueryOwner(invocation, sourceSlot, targetSlot);
            if (Int(effect, "add") is { } add)
                modifiers.Add(new BattleQueryModifier(BattleQueryStage.Hooks, BattleQueryOperation.Add,
                    new BattleQueryValue(add), priority, owner, insertion++));
            if (Int(effect, "multiplierPercent") is { } percent)
                modifiers.Add(new BattleQueryModifier(BattleQueryStage.Hooks, BattleQueryOperation.Multiply,
                    new BattleQueryValue(percent, 100), priority, owner, insertion++));
        }
        return modifiers;
    }

    private static BattleQueryOwnerScope QueryOwner(
        BattleHookInvocation invocation, BattleSlot sourceSlot, BattleSlot targetSlot) =>
        invocation.Kind == BattleHookSourceKind.Field ? BattleQueryOwnerScope.Field
        : invocation.Slot == sourceSlot ? BattleQueryOwnerScope.Source
        : invocation.Slot == targetSlot ? BattleQueryOwnerScope.Target
        : throw new InvalidOperationException("A damage query hook must belong to its source, target, or field.");

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

    private bool BlocksStatus(BattleSlot target, PersistentStatus status) =>
        BattleHookDispatcher.StatusAttempt(target, HookSources())
            .Any(i => i.Effect.Op == "statusImmunity"
                && string.Equals(Str(i.Effect, "status"), status.ToString(), StringComparison.OrdinalIgnoreCase));

    private void SetWeather(Weather weather, int turns, BattleSlot sourceSlot)
    {
        if (weather == Weather.None || weather == CurrentWeather)
            return;
        WeatherDef definition = WeatherConditions.For(weather);
        RecordConditionChanges(_conditions.Apply(new BattleConditionApplication(
            definition.Definition!.Id,
            WeatherConditions.FieldOwner,
            new BattleConditionSource(sourceSlot, ActiveIndex(sourceSlot)),
            Turn,
            _traceActionSequence,
            Math.Max(1, turns))));
        _log.Add(new WeatherChanged(weather));
        ReevaluateConditionForms();
        if (_dispatchingWeatherChange)
            return;

        _dispatchingWeatherChange = true;
        try
        {
            ApplyHookInvocations(BattleHookDispatcher.WeatherChange(sourceSlot.Side, HookSources()));
        }
        finally
        {
            _dispatchingWeatherChange = false;
        }
    }

    private void RecordConditionChanges(BattleConditionChangeSet changes)
    {
        _log.AddRange(changes.Events);
        _conditionTrace.AddRange(changes.Trace);
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

    private bool CanAct(BattleCreature c, BattleSlot sourceSlot, int traceAction)
    {
        BattleSide side = sourceSlot.Side;
        switch (c.Status)
        {
            case PersistentStatus.Freeze:
                int thawStart = _log.Count;
                if (StatusEffects.Thaws(_rng, out double thawDraw))
                {
                    c.ClearStatus();
                    _log.Add(new Thawed(sourceSlot));
                    AddTrace(traceAction, sourceSlot, null, EffectTraceKind.StatusGate, true, thawDraw, 1,
                        thawStart, _log.Count, 1);
                    return true;
                }
                _log.Add(new StillFrozen(sourceSlot));
                AddTrace(traceAction, sourceSlot, null, EffectTraceKind.StatusGate, true, thawDraw, 0,
                    thawStart, _log.Count, 1);
                return false;

            case PersistentStatus.Sleep:
                int sleepStart = _log.Count;
                if (c.StatusCounter > 0)
                {
                    c.TickSleep();
                    _log.Add(new StillAsleep(sourceSlot));
                    AddTrace(traceAction, sourceSlot, null, EffectTraceKind.StatusGate, false, null, 0,
                        sleepStart, _log.Count);
                    return false;
                }
                c.ClearStatus();
                _log.Add(new WokeUp(sourceSlot));
                AddTrace(traceAction, sourceSlot, null, EffectTraceKind.StatusGate, false, null, 1,
                    sleepStart, _log.Count);
                return true;

            case PersistentStatus.Paralysis:
                int paralysisStart = _log.Count;
                if (StatusEffects.FullyParalyzed(_rng, out double paralysisDraw))
                {
                    _log.Add(new FullyParalyzed(sourceSlot));
                    AddTrace(traceAction, sourceSlot, null, EffectTraceKind.StatusGate, true, paralysisDraw, 0,
                        paralysisStart, _log.Count, 1);
                    return false;
                }
                AddTrace(traceAction, sourceSlot, null, EffectTraceKind.StatusGate, true, paralysisDraw, 1,
                    paralysisStart, _log.Count, 1);
                return true;

            default:
                AddTrace(traceAction, sourceSlot, null, EffectTraceKind.StatusGate, false, null, 1,
                    _log.Count, _log.Count);
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
        foreach (BattleSlot slot in Topology.Slots)
        {
            BattleSide side = slot.Side;
            BattleCreature c = Active(slot);
            if (c.IsFainted || c.Status is not { } status)
                continue;

            int dmg = StatusEffects.ResidualDamage(status, c.MaxHp, c.StatusCounter);
            if (dmg > 0)
            {
                c.TakeDamage(dmg);
                _log.Add(new StatusDamage(slot, dmg));
                if (c.IsFainted)
                    RecordFaint(slot);
            }
            c.AdvanceToxic();
        }

        // Leech Seed: each seeded active is sapped and the opposing active recovers that HP.
        foreach (BattleSlot slot in Topology.Slots)
        {
            BattleCreature c = Active(slot);
            BattleSlot? recipientSlot = Topology.SlotsFor(Opponent(slot.Side)).FirstOrDefault(IsLive);
            if (!c.IsFainted && c.Seeded && recipientSlot is { } recipient)
                DrainLife(c, slot, Active(recipient), recipient, Math.Max(1, c.MaxHp / 8));
        }

        // Partial trap (Bind/Wrap/…): residual chip while trapped, then count down and release.
        foreach (BattleSlot slot in Topology.Slots)
        {
            BattleCreature c = Active(slot);
            if (c.IsFainted || !c.IsTrapped)
                continue;
            Sap(c, slot, Math.Max(1, c.MaxHp / 8), amt => new BoundHurt(slot, amt));
            c.TickTrap();
            if (!c.IsTrapped)
                _log.Add(new BindReleased(slot));
        }

        ApplyEndOfTurnHooks();
        FieldConditionsTurnEnd();
        TickTimedForms();
    }

    private void ApplyEndOfTurnHooks()
    {
        foreach (BattleSlot slot in Topology.Slots)
            foreach (BattleHookInvocation invocation in Topology.ActiveSlotsPerSide == 1
                ? BattleHookDispatcher.EndOfTurn(slot.Side, HookSources())
                : BattleHookDispatcher.EndOfTurn(slot, HookSources()))
                ApplyEndOfTurnHook(slot, invocation.Effect);
    }

    private void ApplyEndOfTurnHook(BattleSlot slot, Effect effect)
    {
        BattleSide side = slot.Side;
        BattleCreature c = Active(slot);
        if (c.IsFainted)
            return;

        if (effect.Op == "residualHeal")
        {
            Heal(c, slot, FractionAmount(c, effect));
            return;
        }

        if (effect.Op == "residualDamage")
        {
            Sap(c, slot, FractionAmount(c, effect), amt => new ResidualDamage(slot, amt));
            return;
        }

        if (effect.Op == "statusCure" && !c.HasConsumedHeldEffect(effect.Op) && c.Status is { } status
            && string.Equals(Str(effect, "status"), status.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            c.ConsumeHeldEffect(effect.Op);
            _log.Add(new HeldItemConsumed(slot, effect.Op));
            c.ClearStatus();
            _log.Add(new StatusCured(slot, status));
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
            _log.Add(new HeldItemConsumed(slot, effect.Op));
            Heal(c, slot, Math.Min(amount, c.MaxHp - c.CurrentHp));
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
    private void FieldConditionsTurnEnd()
    {
        Weather weather = CurrentWeather;
        if (weather == Weather.None)
            return;

        WeatherDef def = WeatherConditions.For(weather);
        if (def.Definition!.Hooks.Contains(BattleConditionHook.TurnEnd))
        {
            foreach (BattleSlot slot in Topology.Slots)
            {
                BattleCreature c = Active(slot);
                if (c.IsFainted || c.Types.Any(t => def.ResidualImmuneTypes.Contains(t.Slug)))
                    continue;
                Sap(c, slot, Math.Max(1, c.MaxHp / def.ResidualDenominator), amt => new WeatherDamage(slot, amt));
            }
        }

        BattleConditionChangeSet completion = _conditions.CompleteCheckpoint(
            BattleIntentCheckpoint.TurnEnd, Turn, _traceActionSequence);
        RecordConditionChanges(completion);
        if (completion.Events.OfType<ConditionExpired>().Any())
        {
            _log.Add(new WeatherEnded(weather));
            ReevaluateConditionForms();
        }
    }

    private void ReevaluateConditionForms()
    {
        foreach (BattleSlot slot in Topology.Slots)
            ReevaluateConditionForm(slot);
    }

    private void ReevaluateConditionForm(BattleSide side)
        => ReevaluateConditionForm(new BattleSlot(side, 0));

    private void ReevaluateConditionForm(BattleSlot slot)
    {
        (bool changed, string? formId) = Active(slot).ReevaluateConditionForm(CurrentWeather);
        if (changed)
            _log.Add(new FormChanged(slot, formId));
    }

    private void TickTimedForms()
    {
        foreach (BattleSlot slot in Topology.Slots)
            if (Active(slot).TickTimedForm())
            {
                _log.Add(new FormChanged(slot, null));
                ReevaluateConditionForm(slot);
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

    private void RequestReplacements()
    {
        foreach (BattleSlot slot in Topology.Slots.Where(slot => !IsLive(slot)))
        {
            TraceCancelled(_intentQueue.OwnerFainted(slot.Side, ActiveIndex(slot)));
            _overlays.OwnerFainted(slot.Side, ActiveIndex(slot), Turn, _traceActionSequence);
        }
        _pendingReplacementSlots.Clear();
        foreach (BattleSide side in (ReadOnlySpan<BattleSide>)[BattleSide.Player, BattleSide.Enemy])
        {
            int reserves = _parties[(int)side]
                .Select((creature, partyIndex) => (creature, partyIndex))
                .Count(candidate => !candidate.creature.IsFainted && !_activeSlots.IsActive(side, candidate.partyIndex));
            foreach (BattleSlot slot in Topology.SlotsFor(side))
            {
                if (reserves == 0)
                    break;
                if (IsLive(slot))
                    continue;
                _pendingReplacementSlots.Add(slot);
                reserves--;
            }
        }

        foreach (BattleSlot slot in _pendingReplacementSlots)
            _log.Add(new ReplacementRequested(slot));
    }

    private bool CheckEnd()
    {
        if (Outcome is not null)
            return true;

        bool playerDown = _parties[0].All(c => c.IsFainted);
        bool enemyDown = _parties[1].All(c => c.IsFainted);
        if (!playerDown && !enemyDown)
            return false;

        EndBattle(playerDown == enemyDown ? null : playerDown ? BattleSide.Enemy : BattleSide.Player);
        return true;
    }

    private void EndBattle(BattleSide? winner)
    {
        Outcome = new BattleOutcome(winner);
        _pendingReplacementSlots.Clear();
        TraceCancelled(_intentQueue.EndBattle());
        _overlays.EndBattle(Turn, _traceActionSequence);
        RecordConditionChanges(_conditions.EndBattle(Turn, _traceActionSequence));
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
