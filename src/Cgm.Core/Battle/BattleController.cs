using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public sealed record BattleFieldInputs(
    string Ruleset = BattleRulesets.Gen4Like,
    Weather InitialWeather = Weather.None,
    int? InitialWeatherDuration = null,
    BattleEnvironment NaturalEnvironment = BattleEnvironment.Building,
    Terrain InitialTerrain = Terrain.None,
    int? InitialTerrainDuration = null);

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
    private sealed record MoveInvocation(BattleMove Caller, BattleMove Executed, BattleMove PpOwner,
        BattleActionSubmission Submission, IReadOnlyList<(EntityId Caller, EntityId Called)> Chain,
        MoveCallFailureReason? Failure);
    private readonly record struct DamageApplication(int AppliedDamage, int ActualHpRemoved);

    private readonly List<BattleCreature>[] _parties;
    private readonly BattleActiveSlots _activeSlots;
    private readonly Dictionary<EntityId, int>[] _itemStock = [[], []];
    private readonly bool[] _temporaryFormUsed = [false, false];
    private readonly TypeChart _chart;
    private readonly IRng _rng;
    private readonly IReadOnlyDictionary<EntityId, Item> _itemData;
    private readonly IReadOnlyDictionary<EntityId, BattleMove> _moveCatalog;
    private readonly List<BattleEvent> _log = [];
    private readonly List<EffectTraceEntry> _trace = [];
    private readonly List<BattleQueryTraceEntry> _queryTrace = [];
    private readonly List<BattleDamageQueryTraceEntry> _damageQueryTrace = [];
    private readonly List<RedirectCondition> _redirects = [];
    private readonly BattleIntentQueue _intentQueue = new();
    private readonly BattleOverlayStore _overlays = new();
    private readonly BattleItemState _items;
    private readonly BattleAbilityState _abilities;
    private readonly BattleCreatureTypeState _types;
    private readonly BattleActionHistory _actionHistory = new();
    private readonly BattleConditionStores _conditions =
        new(new BattleConditionRegistry([.. WeatherConditions.Definitions, .. TerrainConditions.Definitions,
            .. GroundedConditions.Definitions, .. FieldConditions.Definitions, .. SideConditions.Definitions,
            .. OneShotQueryConditions.Definitions, .. ActionFilterConditions.Definitions]));
    private readonly List<BattleConditionTraceEntry> _conditionTrace = [];
    private readonly List<BattleHookTraceEntry> _hookTrace = [];
    private readonly List<BattleSlot> _pendingReplacementSlots = [];
    private List<BattleScheduledAction>? _pendingMoveSchedule;
    private readonly HashSet<BattleSlot> _executedMoveSlots = [];
    private readonly Dictionary<BattleSlot, Fraction> _scheduledPowerBoosts = [];
    private readonly HashSet<BattleSlot> _repeatPendingSlots = [];
    private (BattleSlot Slot, Fraction Boost)? _currentPowerBoost;
    private readonly Dictionary<BattleSlot, (EntityId? Type, PairedActionSideEffect Effect)> _scheduledPairs = [];
    private (BattleSlot Slot, EntityId? Type, PairedActionSideEffect Effect)? _currentPair;
    private bool _dispatchingWeatherChange;
    private bool _dispatchingTerrainChange;
    private int _traceActionSequence;
    private readonly string _ruleset;
    private BattleEnvironment _naturalEnvironment;

    public BattleController(BattleCreature player, BattleCreature enemy, TypeChart chart, IRng rng,
        bool isWild = false, IEnumerable<Item>? itemData = null, BattleFieldInputs? fieldInputs = null,
        IEnumerable<BattleMove>? moveData = null, IEnumerable<Ability>? abilityData = null)
        : this([player], [enemy], chart, rng, isWild, itemData, fieldInputs, moveData, abilityData) { }

    public BattleController(IReadOnlyList<BattleCreature> playerParty, IReadOnlyList<BattleCreature> enemyParty,
        TypeChart chart, IRng rng, bool isWild = false, IEnumerable<Item>? itemData = null,
        BattleFieldInputs? fieldInputs = null, IEnumerable<BattleMove>? moveData = null,
        IEnumerable<Ability>? abilityData = null)
    {
        _parties = [[.. playerParty], [.. enemyParty]];
        _activeSlots = new BattleActiveSlots(BattleTopology.Singles);
        _activeSlots.Assign(new BattleSlot(BattleSide.Player, 0), 0);
        _activeSlots.Assign(new BattleSlot(BattleSide.Enemy, 0), 0);
        _chart = chart;
        _rng = rng;
        _itemData = (itemData ?? []).ToDictionary(item => item.Id);
        _items = new BattleItemState(_overlays, _itemData);
        _abilities = new BattleAbilityState(_overlays, (abilityData ?? []).ToDictionary(ability => ability.Id));
        // ponytail: no empty-type fallback — an emptying mutation fails WouldEmptyTypes (spec default);
        // pass a ruleset-defined typeless type here if one is ever authored.
        _types = new BattleCreatureTypeState(_overlays);
        _moveCatalog = BuildMoveCatalog(moveData);
        _ruleset = InitializeField(fieldInputs);
        TriggerInitialTerrainSeeds();
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
        IEnumerable<Item>? itemData = null,
        BattleFieldInputs? fieldInputs = null,
        IEnumerable<BattleMove>? moveData = null,
        IEnumerable<Ability>? abilityData = null)
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
        _items = new BattleItemState(_overlays, _itemData);
        _abilities = new BattleAbilityState(_overlays, (abilityData ?? []).ToDictionary(ability => ability.Id));
        // ponytail: no empty-type fallback — an emptying mutation fails WouldEmptyTypes (spec default);
        // pass a ruleset-defined typeless type here if one is ever authored.
        _types = new BattleCreatureTypeState(_overlays);
        _moveCatalog = BuildMoveCatalog(moveData);
        _ruleset = InitializeField(fieldInputs);
        TriggerInitialTerrainSeeds();
        IsWild = isWild;
    }

    public int Turn { get; private set; }
    public bool IsWild { get; }
    public bool Captured { get; private set; }
    public BattleOutcome? Outcome { get; private set; }
    public IReadOnlyList<BattleEvent> Log => _log;
    public IReadOnlyList<EffectTraceEntry> Trace => _trace;
    public IReadOnlyList<BattleQueryTraceEntry> QueryTrace => _queryTrace;
    public IReadOnlyList<BattleDamageQueryTraceEntry> DamageQueryTrace => _damageQueryTrace;
    public IReadOnlyList<BattleIntentDebugEntry> IntentQueueSnapshot => _intentQueue.DebugSnapshot();
    public IReadOnlyList<BattleSlot> PendingReplacementSlots => _pendingReplacementSlots;
    public BattleOverlayStore Overlays => _overlays;
    public BattleItemState Items => _items;
    public BattleAbilityState Abilities => _abilities;
    public BattleCreatureTypeState Types => _types;
    public BattleActionHistory ActionHistory => _actionHistory;
    public IReadOnlyList<BattleConditionInstance> ConditionSnapshot => _conditions.Snapshot();
    public IReadOnlyList<BattleConditionTraceEntry> ConditionTrace => _conditionTrace;
    public IReadOnlyList<BattleHookTraceEntry> HookTrace => _hookTrace;
    public Weather CurrentWeather => _conditions.Snapshot(BattleConditionScope.Weather)
        .Select(instance => WeatherConditions.For(instance.Definition.Id).Weather)
        .SingleOrDefault();
    public Terrain CurrentTerrain => _conditions.Snapshot(BattleConditionScope.Terrain)
        .Select(instance => TerrainConditions.For(instance.Definition.Id).Terrain)
        .SingleOrDefault();
    public BattleEnvironmentState Environment => BattleEnvironmentState.Resolve(_naturalEnvironment, CurrentTerrain);
    public BattleEnvironment NaturalEnvironment => Environment.Natural;
    public BattleEnvironment EffectiveEnvironment => Environment.Effective;
    public string Ruleset => _ruleset;

    private IReadOnlyDictionary<EntityId, BattleMove> BuildMoveCatalog(IEnumerable<BattleMove>? moveData)
    {
        Dictionary<EntityId, BattleMove> catalog = (moveData ?? [])
            .Select(move => move.WithPpPool(move.MaxPp, move.MaxPp))
            .ToDictionary(move => move.Move);
        foreach (BattleMove move in _parties.SelectMany(party => party).SelectMany(creature => creature.Moves))
            catalog.TryAdd(move.Move, move);
        return catalog;
    }

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

    public bool IsGrounded(BattleSlot slot)
    {
        BattleCreature creature = Active(slot);
        var owner = new BattleOverlayOwner(slot.Side, ActiveIndex(slot), slot);
        BattleQueryResult result = GroundedConditions.Query(creature,
            PhysicalMetricFormulas.EffectiveValues(creature, _overlays, owner).CreatureTypes,
            new BattleConditionOwner(BattleConditionScope.Creature, slot.Side, slot, ActiveIndex(slot)),
            ConditionSnapshot,
            new BattleQueryContext(slot, creature, Weather: CurrentWeather, Ruleset: Ruleset,
                Terrain: CurrentTerrain), suppressHeldItems: ItemsSuppressed,
            abilityHooks: AbilityHooks(slot));
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, _traceActionSequence, slot, slot, result));
        return result.FinalValue.ToInt32() == 1;
    }

    public void SetBattleItemStock(BattleSide side, EntityId item, int count) =>
        _itemStock[(int)side][item] = Math.Max(0, count);

    public IReadOnlyList<BattleAction> LegalMoveActions(BattleSlot slot)
    {
        if (!Topology.Contains(slot))
            throw new ArgumentException("Slot is outside this battle topology.", nameof(slot));
        BattleCreature active = Active(slot);
        if (active.ChargingMoveIndex is { } charge)
            return [new UseMove(charge)];
        if (active.IsLocked)
            return [new UseMove(active.LockedMoveIndex)];
        IReadOnlyList<int> legal = BattleActionLegality.LegalMoves(active, slot, ActiveIndex(slot),
            ConditionSnapshot, SourceCreature, ItemsSuppressed);
        return legal.Count == 0 ? [new UseFallback()] : legal.Select(index => (BattleAction)new UseMove(index)).ToArray();
    }

    public BattleConditionChangeSet ApplyActionFilterCondition(BattleSlot ownerSlot, BattleSlot sourceSlot,
        ActionFilterKind kind, int? duration = null, string? moveTag = null)
    {
        if (!Topology.Contains(ownerSlot) || !Topology.Contains(sourceSlot))
            throw new ArgumentException("Action-filter owner and source must be active battle slots.");
        BattleConditionDefinition definition = ActionFilterConditions.For(kind, moveTag);
        Dictionary<string, int>? counters = null;
        if (kind is ActionFilterKind.DisableMove or ActionFilterKind.ForceMove)
        {
            EntityId? last = Active(ownerSlot).LastMoveUsed;
            int index = last is null ? -1 : Active(ownerSlot).Moves.ToList().FindIndex(move => move.Move == last);
            if (index < 0)
                throw new ArgumentException("Move-specific action filters require an owner last-used move.");
            counters = new() { [ActionFilterConditions.MoveIndexCounter] = index };
        }
        var application = new BattleConditionApplication(definition.Id,
            new BattleConditionOwner(BattleConditionScope.Creature, ownerSlot.Side, ownerSlot, ActiveIndex(ownerSlot)),
            new BattleConditionSource(sourceSlot, ActiveIndex(sourceSlot)), Turn, _traceActionSequence, duration, counters);
        BattleConditionChangeSet changes = _conditions.Apply(application, definition);
        RecordConditionChanges(changes);
        return changes;
    }

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
        BattleActionSubmission resolvedPlayer = PreviewQueuedSubmission(EffectiveLockedSubmission(
            new BattleActionSubmission(new BattleSlot(BattleSide.Player, 0), playerAction)), intentPreview);
        BattleActionSubmission resolvedEnemy = PreviewQueuedSubmission(EffectiveLockedSubmission(
            new BattleActionSubmission(new BattleSlot(BattleSide.Enemy, 0), enemyAction)), intentPreview);
        BattleAction resolvedPlayerAction = resolvedPlayer.Action;
        BattleAction resolvedEnemyAction = resolvedEnemy.Action;
        Validate(BattleSide.Player, resolvedPlayerAction);
        Validate(BattleSide.Enemy, resolvedEnemyAction);
        ConsumePreviewedIntents(intentPreview);
        var actions = new BattleTurnActions(Topology,
        [
            resolvedPlayer,
            resolvedEnemy,
        ]);
        BeginActionHistory(actions.Actions.Select(submission => ActionPlan(submission)));
        PrepareProtectionChains(actions.Actions);

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
        BeginActionHistory(admitted.Select(action => ActionPlan(action.Submission,
            new BattleHistoryOwner(action.Submission.Source.Side, action.ActorPartyIndex,
                action.Submission.Source))));
        PrepareProtectionChains(admitted.Select(action => action.Submission));
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
            BattleActionSubmission effective = PreviewQueuedSubmission(EffectiveLockedSubmission(submission), intentPreview);
            Validate(effective.Source, effective.Action, effective.Selection);
            BattleCreature actor = Active(submission.Source);
            EntityId? moveId = MoveIndex(effective.Action) is { } moveIndex
                ? MoveAt(submission.Source, EffectiveMoveIndex(submission.Source, moveIndex)).Move
                : null;
            admitted.Add(new AdmittedAction(effective, ActiveIndex(submission.Source), moveId));
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
            AdvanceMultiTurnLock(slot, BattleActionResult.Prevented, null);
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

    private static BattleActionSubmission PreviewQueuedSubmission(BattleActionSubmission submitted,
        BattleIntentPreview preview)
    {
        BattleIntent[] due = preview.Entries
            .Where(intent => intent.Owner.LastKnownSlot == submitted.Source)
            .ToArray();
        if (due.Any(intent => intent.Payload is SkipActionIntent))
            return submitted with { Action = new Pass(), Selection = null, TargetPartySnapshot = null };
        BattleIntent? releaseIntent = due.SingleOrDefault(intent => intent.Payload is ReleaseMoveIntent);
        if (releaseIntent?.Payload is not ReleaseMoveIntent release)
            return submitted;
        BattleActionSelection? selection = releaseIntent.Target.Slot is { } target
            ? new ActiveSlotSelection(target) : null;
        return submitted with
        {
            Action = new UseMove(release.MoveIndex),
            Selection = selection,
            TargetPartySnapshot = releaseIntent.Target.SnapshotPartyIndex,
        };
    }

    private BattleActionSubmission EffectiveLockedSubmission(BattleActionSubmission submitted)
    {
        BattleCreature creature = Active(submitted.Source);
        return creature.IsLocked
            ? submitted with
            {
                Action = new UseMove(creature.LockedMoveIndex),
                Selection = creature.LockedSelection,
                TargetPartySnapshot = null,
            }
            : submitted;
    }

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
        // Flinch lasts only for a turn; Counter/Mirror Coat only see this turn's hits.
        foreach (BattleSlot slot in Topology.Slots)
        {
            BattleCreature active = Active(slot);
            active.ClearFlinch();
            active.ResetDamageTaken();
        }

        var scheduled = new List<BattleScheduledAction>();
        foreach (AdmittedAction action in actions)
        {
            if (MoveIndex(action.Submission.Action) is not { } moveIndex || !ActorIsCurrent(action))
                continue;
            int effective = EffectiveMoveIndex(action.Submission.Source, moveIndex);
            scheduled.Add(new BattleScheduledAction(action.Submission,
                EffectivePriority(action.Submission.Source, MoveAt(action.Submission.Source, effective), 0),
                Speed(action.Submission.Source)));
        }
        BeginMoveSchedule(scheduled);
        while (_pendingMoveSchedule!.Count > 0)
        {
            BattleScheduledAction scheduledAction = _pendingMoveSchedule[0];
            _pendingMoveSchedule.RemoveAt(0);
            BeginScheduledAction(scheduledAction.Submission.Source);
            AdmittedAction action = actions.Single(candidate => candidate.Submission == scheduledAction.Submission);
            if (!ActorIsCurrent(action))
            {
                InvalidateActor(action);
                continue;
            }

            int moveIndex = EffectiveMoveIndex(action.Submission.Source, MoveIndex(action.Submission.Action)!.Value);
            BattleCreature attacker = Active(action.Submission.Source);
            BattleMove move = MoveAt(action.Submission.Source, moveIndex);
            ActionLegalityResult lockedLegality = LockedMoveLegality(attacker, action.Submission.Source, moveIndex);
            if (!lockedLegality.Allowed)
            {
                _log.Add(new ActionBlocked(action.Submission.Source, lockedLegality.Reason,
                    lockedLegality.Condition!.Value));
                EndMultiTurnLock(action.Submission.Source, attacker, MultiTurnLockEndReason.SelectionBlocked);
                continue;
            }
            if (!move.HasPp && !attacker.IsCharging
                && (!attacker.IsLocked || move.MultiTurnLockProfile?.RepeatPaysPp == true))
            {
                _log.Add(new ActionInvalidated(action.Submission.Source, ActionInvalidationReason.ResourceChanged));
                EndMultiTurnLock(action.Submission.Source, attacker, MultiTurnLockEndReason.NoPp);
                continue;
            }

            int traceAction = ++_traceActionSequence;
            BattleHistoryOwner sourceOwner = HistoryOwner(action.Submission.Source);
            BattleActionAttemptId attempt = _actionHistory.BeginMove(traceAction, sourceOwner, move.Move);
            if (!CanAct(attacker, action.Submission.Source, traceAction))
            {
                CancelCharge(action.Submission.Source, attacker);
                ResetFailedProtection(attacker, move);
                _actionHistory.Complete(attempt, BattleActionResult.Prevented);
                AdvanceMultiTurnLock(action.Submission.Source, BattleActionResult.Prevented, traceAction);
                continue;
            }
            if (!PassesActionFilterGate(action.Submission.Source, traceAction))
            {
                _actionHistory.Complete(attempt, BattleActionResult.Prevented);
                AdvanceMultiTurnLock(action.Submission.Source, BattleActionResult.Prevented, traceAction);
                continue;
            }
            if (attacker.Flinched)
            {
                CancelCharge(action.Submission.Source, attacker);
                ResetFailedProtection(attacker, move);
                int start = _log.Count;
                _log.Add(new Flinched(action.Submission.Source));
                AddTrace(traceAction, action.Submission.Source, null, EffectTraceKind.FlinchGate, false, null, 0,
                    start, _log.Count);
                _actionHistory.Complete(attempt, BattleActionResult.Prevented);
                AdvanceMultiTurnLock(action.Submission.Source, BattleActionResult.Prevented, traceAction);
                continue;
            }
            AddTrace(traceAction, action.Submission.Source, null, EffectTraceKind.FlinchGate, false, null, 1,
                _log.Count, _log.Count);
            if (!PushesThroughConfusion(attacker, action.Submission.Source, traceAction))
            {
                CancelCharge(action.Submission.Source, attacker);
                ResetFailedProtection(attacker, move);
                _actionHistory.Complete(attempt, BattleActionResult.Prevented);
                AdvanceMultiTurnLock(action.Submission.Source, BattleActionResult.Prevented, traceAction);
                continue;
            }
            MoveInvocation invocation = ResolveMoveInvocation(action.Submission.Source, move,
                action.Submission, traceAction);
            if (invocation.Failure is { } callFailure)
            {
                _actionHistory.MarkStarted(attempt);
                invocation.PpOwner.UsePp();
                ApplyChoiceLock(attacker, moveIndex);
                _log.Add(new MoveUsed(action.Submission.Source, invocation.Caller.Move));
                foreach (((EntityId caller, EntityId called), int depth) in invocation.Chain.Select((edge, index) => (edge, index + 1)))
                    _log.Add(new MoveCalled(action.Submission.Source, caller, called, depth));
                _log.Add(new MoveCallFailed(action.Submission.Source, invocation.Caller.Move, callFailure));
                attacker.RecordMoveUse(invocation.Caller.Move);
                _actionHistory.Complete(attempt, BattleActionResult.Failed);
                AdvanceMultiTurnLock(action.Submission.Source, BattleActionResult.Failed, traceAction);
                continue;
            }
            move = invocation.Executed;
            if (move.Move != invocation.Caller.Move)
                _actionHistory.ReplacePendingMove(attempt, move.Move);
            BattleActionSubmission effectiveSubmission = invocation.Submission;
            BattleSlot? gateTarget = GateTarget(action.Submission.Source, move,
                effectiveSubmission.Selection);
            if (!PassesMoveGates(attacker, action.Submission.Source, move, traceAction,
                    MoveGateTiming.BeforeMove, gateTarget))
            {
                CancelCharge(action.Submission.Source, attacker);
                ResetFailedProtection(attacker, move);
                _actionHistory.Complete(attempt, BattleActionResult.Failed);
                AdvanceMultiTurnLock(action.Submission.Source, BattleActionResult.Failed, traceAction);
                continue;
            }
            if (TryPreparePairedAction(action.Submission.Source, attacker, moveIndex, move,
                    invocation, traceAction, attempt))
            {
                AdvanceMultiTurnLock(action.Submission.Source, BattleActionResult.Succeeded, traceAction);
                continue;
            }

            _actionHistory.MarkStarted(attempt);
            int ppBeforeSpend = invocation.PpOwner.Pp;
            if (!PrepareTimedMove(attacker, action.Submission.Source, move, moveIndex, traceAction,
                    effectiveSubmission, invocation.PpOwner))
            {
                _actionHistory.Complete(attempt, BattleActionResult.Succeeded);
                continue;
            }
            if (invocation.Chain.Count > 0)
            {
                _log.Add(new MoveUsed(action.Submission.Source, invocation.Caller.Move));
                foreach (((EntityId caller, EntityId called), int depth) in invocation.Chain.Select((edge, index) => (edge, index + 1)))
                    _log.Add(new MoveCalled(action.Submission.Source, caller, called, depth));
            }
            _log.Add(new MoveUsed(action.Submission.Source, move.Move));
            if (!PassesMoveGates(attacker, action.Submission.Source, move, traceAction,
                    MoveGateTiming.AfterMoveUsed, gateTarget))
            {
                attacker.RecordMoveUse(move.Move);
                ResetFailedProtection(attacker, move);
                _actionHistory.Complete(attempt, BattleActionResult.Failed);
                AdvanceMultiTurnLock(action.Submission.Source, BattleActionResult.Failed, traceAction);
                continue;
            }
            attacker.RecordMoveUse(move.Move);
            IReadOnlyList<BattleSlot>? targets = MaterializeLiveTargets(effectiveSubmission, move, traceAction,
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
            if (result == BattleActionResult.Connected && _currentPair is { } pair
                && pair.Slot == action.Submission.Source
                && pair.Effect != PairedActionSideEffect.None)
                ApplyPairedSideEffect(action.Submission.Source, pair.Effect, traceAction);
            _actionHistory.Complete(attempt, result, targetOwners);

            AdvanceMultiTurnLock(action.Submission.Source, result, traceAction);
            if (CheckEnd())
                break;
        }
        EndMoveSchedule();
    }

    private IReadOnlyList<BattleSlot>? MaterializeLiveTargets(BattleActionSubmission submission, BattleMove move, int traceAction,
        out BattleTargetScopeKind scopeKind, out BattleSide? scopeSide)
    {
        BattleSlot? selected = (submission.Selection as ActiveSlotSelection)?.Slot;
        MoveTarget effectiveTarget = EffectiveTarget(submission.Source, move);
        BattleTargetScope scope = BattleTargetResolver.ResolveScope(effectiveTarget, Topology, submission.Source, selected);
        scopeKind = scope.Kind;
        scopeSide = scope.Side;
        if (scope.Kind is BattleTargetScopeKind.Side or BattleTargetScopeKind.Field
            or BattleTargetScopeKind.FaintedParty or BattleTargetScopeKind.MoveReference)
        {
            return null;
        }

        List<BattleSlot> slots = scope.Slots.Where(IsLive).ToList();
        if (submission.TargetPartySnapshot is { } snapshot && selected is { } snapshottedSlot
            && ActiveIndex(snapshottedSlot) != snapshot)
            slots.Clear();
        else if ((effectiveTarget is MoveTarget.Selected or MoveTarget.SelectedPokemonMeFirst) && slots.Count == 0
            && selected is { Side: var side } && side != submission.Source.Side)
        {
            slots = Topology.SlotsFor(Opponent(submission.Source.Side)).Where(IsLive).ToList();
            if (slots.Count > 1)
                slots = [slots[0]];
        }
        if (IsRedirectable(effectiveTarget) && slots.Count > 0)
        {
            BattleSlot original = slots[0];
            DamageClass effectiveClass = EffectiveDamageClass(submission.Source, move);
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
                bool accepted = candidate.AcceptedClasses.Contains(effectiveClass)
                    && (candidate.AcceptedTags.Count == 0 || candidate.AcceptedTags.Overlaps(moveTags))
                    && !candidate.BypassClasses.Contains(effectiveClass)
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
        BattleMoveIdentityQueryResult moveIdentity = EffectiveMoveIdentity(sourceSlot, move, traceAction);
        EntityId moveType = moveIdentity.EffectiveType;
        var actionContext = new BattleActionContext(move, attacker, sourceSlot, traceAction);
        if (!TryItemPower(sourceSlot, move, out int? itemPower))
        {
            _log.Add(new MoveFailed(sourceSlot, move.Move, MoveFailureReason.FormulaInputUnavailable));
            foreach (BattleSlot targetSlot in targetSlots)
                RecordDamage(attempt, sourceOwner, HistoryOwner(targetSlot), move, DamageCause(move), 0,
                    false, BattleDamageFailure.NoQualifyingDamage, 0, default, critical: false,
                    effectiveType: moveType, effectiveClass: moveIdentity.EffectiveClass);
            return BattleActionResult.Failed;
        }
        var accurateTargets = new List<BattleTargetContext>(targetSlots.Count);
        bool priorityBlocked = false;
        bool sideProtected = false;
        foreach (BattleSlot targetSlot in targetSlots)
        {
            BattleCreature target = Active(targetSlot);
            if (!AllowsProtectionHit(sourceSlot, targetSlot, move, traceAction))
            {
                sideProtected = true;
                if (RecordsMoveDamage(move))
                    RecordDamage(attempt, sourceOwner, HistoryOwner(targetSlot), move, DamageCause(move),
                        0, false, BattleDamageFailure.Protected, 0, default, critical: false,
                        effectiveType: moveType, effectiveClass: moveIdentity.EffectiveClass);
                continue;
            }
            if (!TerrainAllowsPriorityHit(sourceSlot, targetSlot, move, traceAction))
            {
                priorityBlocked = true;
                if (RecordsMoveDamage(move))
                    RecordDamage(attempt, sourceOwner, HistoryOwner(targetSlot), move, DamageCause(move),
                        0, false, BattleDamageFailure.Blocked, 0, default, critical: false,
                        effectiveType: moveType, effectiveClass: moveIdentity.EffectiveClass);
                continue;
            }
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
                        effectiveType: moveType, effectiveClass: moveIdentity.EffectiveClass);
                continue;
            }
            accurateTargets.Add(targetContext);
        }

        if (accurateTargets.Count == 0)
        {
            if (move.Power is not null)
                ApplyCrashRecoil(attacker, sourceSlot, move);
            return sideProtected || priorityBlocked ? BattleActionResult.Failed : BattleActionResult.Missed;
        }

        if (move.SecondaryEffects.OfType<DelayedDamageEffect>().SingleOrDefault() is { } delayedDamage)
        {
            bool queued = true;
            foreach (BattleTargetContext target in accurateTargets)
                queued &= QueueDelayedDamage(actionContext, target, delayedDamage);
            return queued ? BattleActionResult.Succeeded : BattleActionResult.Failed;
        }

        if (move.Ohko || move.FixedDamage is not null || move.FixedDamageLevel)
        {
            foreach (BattleTargetContext targetContext in accurateTargets)
            {
                BattleDamageQueryResult query = ResolveDamageQuery(sourceSlot, targetContext.TargetSlot,
                    attacker, targetContext.Target, move, moveIdentity, EffectiveValues(sourceSlot),
                    EffectiveValues(targetContext.TargetSlot), targetSlots.Count, traceAction);
                double effectiveness = TypeChart.ToDouble(query.Effectiveness.FinalValue);
                int damage = effectiveness <= 0 ? 0
                    : move.Ohko ? targetContext.Target.CurrentHp
                    : move.FixedDamageLevel ? attacker.Level : move.FixedDamage!.Value;
                damage = TraceUnmodifiedFinalDamage(sourceSlot, targetContext.TargetSlot, attacker,
                    targetContext.Target, move, damage, traceAction, effectiveness > 0);
                AddTrace(traceAction, sourceSlot, targetContext.TargetSlot, EffectTraceKind.Immunity,
                    false, null, effectiveness <= 0 ? 0 : 1, _log.Count, _log.Count);
                DamageApplication applied = DealMoveDamage(targetContext.Target, targetContext.TargetSlot,
                    damage, effectiveness, crit: false, HpStatusFormulas.CannotKoFloor(move));
                targetContext.AddDamage(actionContext, applied.ActualHpRemoved);
                BattleDamageCause cause = move.Ohko ? BattleDamageCause.OneHitKnockout
                    : move.FixedDamageLevel ? BattleDamageCause.Level : BattleDamageCause.Fixed;
                RecordDamage(attempt, sourceOwner, HistoryOwner(targetContext.TargetSlot), move, cause,
                    1, true, effectiveness <= 0 ? BattleDamageFailure.Immune
                        : applied.ActualHpRemoved > 0 ? BattleDamageFailure.None : BattleDamageFailure.NoDamage,
                    damage, applied, critical: false, effectiveType: moveType,
                    effectiveClass: moveIdentity.EffectiveClass);
            }
            ApplyDoublesMoveEffects(actionContext, accurateTargets);
            return actionContext.TotalDamage > 0 ? BattleActionResult.Connected : BattleActionResult.Failed;
        }

        if (!HpStatusFormulas.HasBasePower(move))
        {
            ApplyDoublesMoveEffects(actionContext, accurateTargets);
            return actionContext.Failed ? BattleActionResult.Failed : BattleActionResult.Succeeded;
        }

        ApplyBeforeDamageSideRemovals(actionContext, accurateTargets);
        int? randomPower = SelectRandomPower(sourceSlot, move, traceAction);
        int? hitCountDraw = null;
        int hits = move.MultiHitMax >= 2 ? EffectMath.HitCount(_rng, move.MultiHitMin, move.MultiHitMax, out hitCountDraw) : 1;
        AddTrace(traceAction, sourceSlot, null, EffectTraceKind.HitCount, hitCountDraw is not null, hitCountDraw, hits, _log.Count, _log.Count,
            hitCountDraw is not null ? HitCountDrawBound(move) : null);
        foreach (BattleTargetContext targetContext in accurateTargets)
        {
            for (int hit = 0; hit < hits && !targetContext.Target.IsFainted; hit++)
            {
                (int damage, bool crit, double effectiveness) = ComputeHit(
                    sourceSlot, targetContext.TargetSlot, attacker, targetContext.Target, move, moveIdentity, move.Power ?? 1,
                    targetSlots.Count, ppBeforeSpend, itemPower, randomPower, traceAction,
                    out double? critDraw, out int? damageRollDraw);
                AddTrace(traceAction, sourceSlot, targetContext.TargetSlot, EffectTraceKind.Immunity,
                    false, null, effectiveness <= 0 ? 0 : 1, _log.Count, _log.Count);
                if (effectiveness > 0)
                {
                    AddTrace(traceAction, sourceSlot, targetContext.TargetSlot, EffectTraceKind.Critical,
                        critDraw is not null, critDraw, crit ? 1 : 0, _log.Count, _log.Count,
                        critDraw is not null ? 1 : null);
                    AddTrace(traceAction, sourceSlot, targetContext.TargetSlot, EffectTraceKind.DamageRoll, true,
                        damageRollDraw, damageRollDraw is { } roll ? roll + 85 : null, _log.Count, _log.Count, 16);
                }
                DamageApplication applied = DealMoveDamage(targetContext.Target, targetContext.TargetSlot, damage,
                    effectiveness, crit,
                    HpStatusFormulas.CannotKoFloor(move));
                targetContext.Target.RecordDamageTaken(moveIdentity.EffectiveClass, applied.ActualHpRemoved);
                targetContext.AddDamage(actionContext, applied.ActualHpRemoved);
                RecordDamage(attempt, sourceOwner, HistoryOwner(targetContext.TargetSlot), move,
                    BattleDamageCause.Standard, hit + 1, attempted: true, effectiveness <= 0
                        ? BattleDamageFailure.Immune
                        : applied.ActualHpRemoved > 0 ? BattleDamageFailure.None : BattleDamageFailure.NoDamage,
                    damage, applied, crit, effectiveType: moveType, effectiveClass: moveIdentity.EffectiveClass);
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
            foreach (MoveEffect effect in actionContext.Move.SecondaryEffects.Where(effect =>
                IsTargetScoped(effect) && effect is not RemoveSideConditionEffect))
                ApplyEffect(context, effect);
            ApplyContactEffects(actionContext.Move, actionContext.SourceSlot, actionContext.Source, targetContext.TargetSlot,
                actionContext.TraceAction);
        }

        foreach (RemoveSideConditionEffect effect in actionContext.Move.SecondaryEffects
            .OfType<RemoveSideConditionEffect>().Where(effect =>
                effect.Side == SideConditionTarget.Target && effect.Timing == SideConditionTiming.AfterHit))
        {
            foreach (BattleTargetContext targetContext in targetContexts
                .DistinctBy(target => target.TargetSide))
                ApplyEffect(new EffectContext(actionContext, targetContext), effect);
        }

        var actionEffectContext = new EffectContext(actionContext, targetContexts[0]);
        foreach (MoveEffect effect in actionContext.Move.SecondaryEffects.Where(effect => !IsTargetScoped(effect)))
            if (!ApplyEffect(actionEffectContext, effect))
                break;
    }

    private static bool IsTargetScoped(MoveEffect effect) => effect switch
    {
        AilmentEffect or ConfusionEffect or FlinchEffect or LeechSeedEffect or BindEffect or ForceSwitchEffect
            or PositionSwapEffect or TurnOrderIntentEffect => true,
        StatChangeEffect { OnSelf: false } or StatChangeAllEffect { OnSelf: false } or StatInvertEffect { OnSelf: false } => true,
        StatResetEffect { Scope: not StageEffectScope.Self } => true,
        StatCopyEffect { From: StageEffectScope.Target } or StatCopyEffect { To: StageEffectScope.Target } or StatSwapEffect => true,
        StatStealEffect or RandomStatRaiseEffect { OnSelf: false } or DerivedStatSwapEffect
            or DerivedStatSplitEffect => true,
        HealEffect { Recipient: HpFractionRecipient.Target } or HpFractionEffect { Recipient: HpFractionRecipient.Target }
            or HpEqualizeEffect or OneShotQueryEffect { Query: OneShotQuery.Accuracy } => true,
        GroundedStateEffect { Scope: GroundedStateScope.Target } => true,
        ApplyActionFilterEffect { Owner: SideConditionTarget.Target } => true,
        RemoveSideConditionEffect { Side: SideConditionTarget.Target, Timing: SideConditionTiming.AfterHit } => true,
        RemoveConditionEffect { Owner: SideConditionTarget.Target }
            or RemoveConditionEffect { Selector.Source: BattleConditionSourceTarget.Target }
            or TransferConditionEffect or SwapConditionEffect => true,
        ItemMutationEffect { Subject: BattleItemSubject.Target }
            or ItemMutationEffect { Operation: BattleItemOperation.Give or BattleItemOperation.Steal or BattleItemOperation.Swap }
            => true,
        TypeMutationEffect { Subject: BattleTypeSubject.Target } => true,
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
                _log.Count, _log.Count, bound),
            reverseSpeed: FieldConditions.Active(ConditionSnapshot, BattleFieldCondition.TrickRoom));

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
            && MoveAt(action.Submission.Source, EffectiveMoveIndex(action.Submission.Source, moveIndex)).Move == action.MoveId;
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
                BattleMove move = MoveAt(slot, EffectiveMoveIndex(slot, use.MoveIndex));
                ValidateSelectionMoveGates(slot, move);
                ValidateSelection(slot, move.Target, selection);
                break;

            case UseFallback:
                if (BattleActionLegality.LegalMoves(Active(slot), slot, ActiveIndex(slot), ConditionSnapshot,
                        SourceCreature, ItemsSuppressed).Count != 0)
                    throw new ArgumentException($"{side} may use the fallback only when no ordinary move is legal.");
                BattleMove fallback = MoveAt(slot, -1);
                ValidateSelection(slot, fallback.Target, selection);
                break;

            case ActivateForm form:
                ValidateMoveUse(slot, form.MoveIndex);
                BattleMove formMove = Active(slot).Moves[EffectiveMoveIndex(slot, form.MoveIndex)];
                ValidateSelectionMoveGates(slot, formMove);
                ValidateSelection(slot, formMove.Target, selection);
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
                ActionLegalityResult itemLegality = BattleActionLegality.Item(slot, ActiveIndex(slot), ConditionSnapshot);
                if (!itemLegality.Allowed)
                    throw new ArgumentException($"{side} battle items are blocked by {itemLegality.Condition}.");
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

    private void ValidateSelectionMoveGates(BattleSlot slot, BattleMove move)
    {
        BattleHistoryOwner source = HistoryOwner(slot);
        if (!BattleActionGates.SourceHistoryAllows(move, Active(slot),
                _actionHistory.PreviousActionFailed(source), MoveGateTiming.Selection))
            throw new ArgumentException($"{slot.Side} move '{move.Move}' fails its selection-time action gate.");
    }

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
        if (!c.IsCharging && !c.IsLocked)
        {
            ActionLegalityResult result = BattleActionLegality.Move(c, moveIndex, slot, ActiveIndex(slot),
                ConditionSnapshot, SourceCreature, ItemsSuppressed);
            if (!result.Allowed)
                throw new ArgumentException($"{side} move {moveIndex} is not legal: {result.Reason}.");
        }
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
        CancelCharge(slot, outgoing);
        EndMultiTurnLock(slot, outgoing, MultiTurnLockEndReason.Switch);
        outgoing.ResetStages();
        outgoing.ClearVolatiles();
        TraceCleanup(_intentQueue.OwnerSwitched(slot.Side, outgoingPartyIndex, null));
        RecordConditionChanges(_conditions.OwnerSwitched(slot.Side, outgoingPartyIndex, null,
            Turn, _traceActionSequence));
        RecordConditionChanges(_conditions.SourceLeft(slot.Side, outgoingPartyIndex,
            BattleConditionCleanupReason.Switch, Turn, _traceActionSequence));
        _overlays.OwnerSwitched(slot.Side, outgoingPartyIndex, null, Turn, _traceActionSequence);
        _activeSlots.Assign(slot, index);
        _overlays.OwnerSwitched(slot.Side, index, slot, Turn, _traceActionSequence);
        _log.Add(new SwitchedIn(slot, index));
        OnSwitchIn(slot);
        ResolveSwitchInIntents(slot);
    }

    private void ResolveMoves(BattleTurnActions actions)
    {
        // Flinch lasts only for a turn; conditions own protection duration.
        foreach (BattleSlot slot in actions.Topology.Slots)
        {
            BattleCreature active = Active(slot);
            active.ClearFlinch();
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
                EffectivePriority(submission.Source, MoveAt(submission.Source, moveIndex), 0),
                Speed(submission.Source)));
        }

        BeginMoveSchedule(scheduled);
        while (_pendingMoveSchedule!.Count > 0)
        {
            BattleScheduledAction scheduledAction = _pendingMoveSchedule[0];
            _pendingMoveSchedule.RemoveAt(0);
            BeginScheduledAction(scheduledAction.Submission.Source);
            int submittedIndex = MoveIndex(scheduledAction.Submission.Action)!.Value;
            BattleSlot source = scheduledAction.Submission.Source;
            int traceBefore = _traceActionSequence;
            ResolveMove(source, EffectiveMoveIndex(source, submittedIndex), scheduledAction.Submission);
            int? traceAction = _traceActionSequence > traceBefore ? _traceActionSequence : null;
            BattleActionResult result = traceAction is { } actionSequence
                ? _actionHistory.Snapshot().Last(attempt => attempt.Id.ActionSequence == actionSequence).Result
                : BattleActionResult.Failed;
            AdvanceMultiTurnLock(source, result, traceAction);
            if (CheckEnd())
                break;
        }
        EndMoveSchedule();
    }

    private void BeginMoveSchedule(IReadOnlyList<BattleScheduledAction> scheduled)
    {
        _pendingMoveSchedule = [.. OrderActions(scheduled)];
        _executedMoveSlots.Clear();
        _scheduledPowerBoosts.Clear();
        _repeatPendingSlots.Clear();
        _scheduledPairs.Clear();
        _currentPowerBoost = null;
        _currentPair = null;
    }

    private void BeginScheduledAction(BattleSlot source)
    {
        _executedMoveSlots.Add(source);
        _currentPowerBoost = _scheduledPowerBoosts.Remove(source, out Fraction boost)
            ? (source, boost) : null;
        _currentPair = _scheduledPairs.Remove(source, out var pair)
            ? (source, pair.Type, pair.Effect) : null;
    }

    private void EndMoveSchedule()
    {
        _pendingMoveSchedule = null;
        _executedMoveSlots.Clear();
        _scheduledPowerBoosts.Clear();
        _repeatPendingSlots.Clear();
        _scheduledPairs.Clear();
        _currentPowerBoost = null;
        _currentPair = null;
    }

    private static int? MoveIndex(BattleAction action) => action switch
    {
        UseMove move => move.MoveIndex,
        ActivateForm form => form.MoveIndex,
        UseFallback => -1,
        _ => null,
    };

    private void BeginActionHistory(IEnumerable<BattleActionPlan> plans) =>
        _actionHistory.BeginTurn(Turn, plans);

    private BattleActionPlan ActionPlan(BattleActionSubmission submission,
        BattleHistoryOwner? capturedOwner = null)
    {
        int? moveIndex = MoveIndex(submission.Action);
        return new BattleActionPlan(capturedOwner ?? HistoryOwner(submission.Source),
            moveIndex is not null ? BattlePlannedActionKind.Move
                : submission.Action is Switch ? BattlePlannedActionKind.Switch : BattlePlannedActionKind.Other,
            moveIndex is { } index
                ? MoveAt(submission.Source, EffectiveMoveIndex(submission.Source, index)).DamageClass
                : null);
    }

    private BattleHistoryOwner HistoryOwner(BattleSlot slot) =>
        new(slot.Side, ActiveIndex(slot), slot);

    /// <summary>Applies the compiled lock's success/failure policy after one forced execution.</summary>
    private void AdvanceMultiTurnLock(BattleSlot slot, BattleActionResult result, int? traceAction)
    {
        BattleCreature c = Active(slot);
        if (!c.IsLocked)
            return;
        BattleMove move = c.Moves[c.LockedMoveIndex];
        MultiTurnLockProfile profile = move.MultiTurnLockProfile!;
        if (profile.EndOnFailure && result != BattleActionResult.Connected)
        {
            EndMultiTurnLock(slot, c, MultiTurnLockEndReason.Failed);
            return;
        }
        c.AdvanceLock(result == BattleActionResult.Connected && c.LockPowerStep < profile.MaxPowerStep);
        if (c.IsLocked)
        {
            _log.Add(new MultiTurnLockContinued(slot, move.Move, c.LockTurns, c.LockPowerStep));
            return;
        }
        c.EndLock();
        _log.Add(new MultiTurnLockEnded(slot, move.Move, MultiTurnLockEndReason.Completed));
        ApplyMultiTurnEndEffect(slot, c, profile, traceAction);
    }

    private void EndMultiTurnLock(BattleSlot slot, BattleCreature creature, MultiTurnLockEndReason reason)
    {
        if (!creature.IsLocked)
            return;
        EntityId move = creature.Moves[creature.LockedMoveIndex].Move;
        creature.EndLock();
        _log.Add(new MultiTurnLockEnded(slot, move, reason));
    }

    private void ApplyMultiTurnEndEffect(BattleSlot slot, BattleCreature creature,
        MultiTurnLockProfile profile, int? traceAction)
    {
        if (profile.EndEffect != MultiTurnLockEndEffect.Confusion || creature.IsFainted || creature.IsConfused)
            return;
        int eventStart = _log.Count;
        int duration = VolatileEffects.ConfusionDuration(_rng, out int draw);
        creature.SetConfusion(duration);
        _log.Add(new Confused(slot));
        if (traceAction is { } action)
            AddTrace(action, slot, null, EffectTraceKind.ConfusionDuration, true, draw, duration,
                eventStart, _log.Count, 5, 1);
    }

    private int Speed(BattleSide side)
    {
        return Speed(new BattleSlot(side, 0));
    }

    private int Speed(BattleSlot slot)
    {
        BattleCreature c = Active(slot);
        BattleHookDispatchSnapshot side = SideConditions.CollectSpeedHooks(ConditionSnapshot, slot.Side, 0);
        _hookTrace.AddRange(side.Trace);
        BattleQueryResult result = PhysicalMetricFormulas.SpeedQuery(c, _overlays,
            new BattleOverlayOwner(slot.Side, ActiveIndex(slot), slot),
            side.QueryModifiers(BattleQueryId.Speed),
            new BattleQueryContext(slot, c, Weather: CurrentWeather, Ruleset: Ruleset, Terrain: CurrentTerrain));
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, 0, slot, null, result));
        return result.FinalValue.ToInt32();
    }

    private int EffectivePriority(BattleSlot slot, BattleMove move, int traceAction)
    {
        BattleCreature creature = Active(slot);
        var modifiers = new List<BattleQueryModifier>();
        if (move.SecondaryEffects.OfType<TerrainMoveEffect>().SingleOrDefault() is { } terrainEffect)
        {
            BattleHookDispatchSnapshot terrain = TerrainConditions.CollectMovePriorityHooks(
                ConditionSnapshot, terrainEffect, IsGrounded(slot), traceAction);
            _hookTrace.AddRange(terrain.Trace);
            modifiers.AddRange(terrain.QueryModifiers(BattleQueryId.Priority));
        }
        BattleQueryResult result = BattleActionQueries.Priority(move, modifiers,
            new BattleQueryContext(slot, creature, Weather: CurrentWeather, Ruleset: Ruleset,
                Terrain: CurrentTerrain));
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction, slot, null, result));
        return result.FinalValue.ToInt32();
    }

    private bool TerrainAllowsPriorityHit(BattleSlot sourceSlot, BattleSlot targetSlot,
        BattleMove move, int traceAction)
    {
        if (sourceSlot.Side == targetSlot.Side || !TerrainConditions.CanBlockPriorityTarget(move.Target))
            return true;
        BattleHookDispatchSnapshot terrain = TerrainConditions.CollectPriorityHooks(
            ConditionSnapshot, EffectivePriority(sourceSlot, move, traceAction), IsGrounded(targetSlot), traceAction);
        _hookTrace.AddRange(terrain.Trace);
        bool allowed = !terrain.Filters().Any(filter => filter is
            { Filter.Value: "priority_hit", Decision: BattleHookFilterDecision.Deny });
        if (!allowed)
            _log.Add(new TerrainPriorityBlocked(sourceSlot, targetSlot));
        return allowed;
    }

    private bool AllowsProtectionHit(BattleSlot sourceSlot, BattleSlot targetSlot,
        BattleMove move, int traceAction)
    {
        int effectivePriority = SideConditions.Active(ConditionSnapshot, targetSlot.Side,
            BattleSideCondition.PriorityProtection)
            ? EffectivePriority(sourceSlot, move, traceAction)
            : move.Priority;
        BattleHookDispatchSnapshot protection = SideConditions.CollectProtectionHooks(
            ConditionSnapshot, sourceSlot, targetSlot, move, effectivePriority, Ruleset,
            BypassesProtection(sourceSlot, move)
                || BypassesSideConditions(sourceSlot, move, "side_protection"), traceAction);
        _hookTrace.AddRange(protection.Trace);
        bool allowed = !protection.Filters().Any(filter => filter is
            { Filter.Value: "side_protection", Decision: BattleHookFilterDecision.Deny });
        if (allowed)
        {
            BattleConditionOwner owner = new(BattleConditionScope.Creature, targetSlot.Side,
                targetSlot, ActiveIndex(targetSlot));
            BattleConditionInstance? personal = ProtectionConditions.Active(ConditionSnapshot, owner);
            BattleHookDispatchSnapshot personalProtection = ProtectionConditions.CollectHooks(
                ConditionSnapshot, owner, sourceSlot, targetSlot, move,
                BypassesProtection(sourceSlot, move), traceAction);
            _hookTrace.AddRange(personalProtection.Trace);
            if (!personalProtection.Filters().Any(filter => filter is
                    { Filter.Value: "personal_protection", Decision: BattleHookFilterDecision.Deny })
                || personal?.Definition.Protection is not { } profile)
                return true;

            int personalStart = _log.Count;
            _log.Add(new MoveBlocked(sourceSlot));
            _log.Add(new ProtectionBlocked(sourceSlot, targetSlot, personal.Definition.Id));
            int performed = move.MakesContact
                ? ApplyProtectionContact(profile, sourceSlot, targetSlot, traceAction) : 0;
            _trace.Add(new EffectTraceEntry(Turn, traceAction, sourceSlot, targetSlot,
                EffectTraceKind.ProtectionBlock, performed > 0, null, performed,
                personalStart, _log.Count) { Condition = personal.Definition.Id });
            return false;
        }

        int start = _log.Count;
        _log.Add(new MoveBlocked(sourceSlot));
        AddTrace(traceAction, sourceSlot, targetSlot, EffectTraceKind.SideProtection,
            false, null, 0, start, _log.Count);
        return false;
    }

    private bool ApplyProtection(EffectContext ctx, ProtectionProfile profile)
    {
        profile = ProtectionConditions.Validate(profile);
        int start = _log.Count;
        double chance = ProtectionConditions.SuccessChance(profile, ctx.Source.ProtectChain, Ruleset);
        if (!ProtectionConditions.Succeeds(profile, ctx.Source.ProtectChain, Ruleset, _rng, out double? draw))
        {
            ctx.Source.ResetProtectChain();
            _log.Add(new ProtectFailed(ctx.SourceSlot));
            ctx.Action.MarkFailed();
            TraceProtectionAttempt(ctx, profile.Id, draw, chance, false, start);
            return false;
        }

        BattleConditionDefinition definition;
        BattleConditionOwner owner;
        if (profile.Scope == ProtectionScope.Personal)
        {
            definition = ProtectionConditions.Definition(profile);
            owner = new BattleConditionOwner(BattleConditionScope.Creature, ctx.SourceSide,
                ctx.SourceSlot, ActiveIndex(ctx.SourceSlot));
        }
        else
        {
            BattleSideCondition sideCondition = profile.Filter switch
            {
                ProtectionFilter.Priority => BattleSideCondition.PriorityProtection,
                ProtectionFilter.MultiTarget => BattleSideCondition.MultiTargetProtection,
                _ => throw new ArgumentOutOfRangeException(nameof(profile), profile.Filter,
                    "Side protection requires a side filter."),
            };
            definition = SideConditions.For(sideCondition);
            owner = SideConditions.Owner(ctx.SourceSide);
        }

        BattleConditionChangeSet changes = profile.Scope == ProtectionScope.Personal
            ? _conditions.Apply(new BattleConditionApplication(definition.Id, owner,
                new BattleConditionSource(ctx.SourceSlot, ActiveIndex(ctx.SourceSlot)), Turn,
                ctx.TraceAction), definition)
            : _conditions.Apply(new BattleConditionApplication(definition.Id, owner,
                new BattleConditionSource(ctx.SourceSlot, ActiveIndex(ctx.SourceSlot)), Turn,
                ctx.TraceAction));
        RecordConditionChanges(changes);
        if (changes.Events.OfType<ConditionApplicationRejected>().Any())
        {
            ctx.Source.ResetProtectChain();
            _log.Add(new ProtectFailed(ctx.SourceSlot));
            ctx.Action.MarkFailed();
            TraceProtectionAttempt(ctx, definition.Id, draw, chance, false, start);
            return false;
        }

        if (ProtectionConditions.UsesChain(profile, Ruleset))
            ctx.Source.AdvanceProtectChain();
        _log.Add(new Protected(ctx.SourceSlot));
        TraceProtectionAttempt(ctx, definition.Id, draw, chance, true, start);
        return true;
    }

    private void TraceProtectionAttempt(EffectContext ctx, BattleConditionId condition,
        double? draw, double chance, bool succeeded, int start) =>
        _trace.Add(new EffectTraceEntry(Turn, ctx.TraceAction, ctx.SourceSlot, null,
            EffectTraceKind.Protect, draw is not null, draw, succeeded ? 1 : 0, start, _log.Count)
        {
            Condition = condition,
            DrawBound = 1,
            DrawMinimum = 0,
            ResolvedChance = chance,
        });

    private int ApplyProtectionContact(ProtectionProfile profile, BattleSlot sourceSlot,
        BattleSlot protectingSlot, int traceAction)
    {
        BattleCreature source = Active(sourceSlot);
        int performed = 0;
        foreach (ProtectionContactEffect effect in profile.ContactEffects)
        {
            if (source.IsFainted)
                break;
            switch (effect)
            {
                case ProtectionContactDamage damage:
                    int amount = EffectMath.FractionOfMaxHp(source.MaxHp, damage.Fraction);
                    Sap(source, sourceSlot, amount,
                        actual => new ProtectionContactDamaged(sourceSlot, profile.Id, actual));
                    performed++;
                    break;
                case ProtectionContactStatus status when CanApplyProtectionStatus(
                    protectingSlot, sourceSlot, status.Status, traceAction):
                    source.SetStatus(status.Status);
                    _log.Add(new StatusApplied(sourceSlot, status.Status));
                    performed++;
                    break;
                case ProtectionContactStage stage when AllowsSideStageDrop(
                    protectingSlot, sourceSlot, null, traceAction):
                    int before = source.Stage(stage.Stat);
                    source.ChangeStage(stage.Stat, stage.Delta);
                    int delta = source.Stage(stage.Stat) - before;
                    if (delta != 0)
                    {
                        _log.Add(new StatStageChanged(sourceSlot, stage.Stat, delta));
                        performed++;
                    }
                    break;
            }
        }
        return performed;
    }

    private bool CanApplyProtectionStatus(BattleSlot protectingSlot, BattleSlot sourceSlot,
        PersistentStatus status, int traceAction)
    {
        BattleCreature source = Active(sourceSlot);
        if (!StatusEffects.CanApplyStatus(source.Status)
            || StatusEffects.TypeImmuneToStatus(status, EffectiveTypes(sourceSlot))
            || BlocksStatus(sourceSlot, status))
            return false;
        BattleHookDispatchSnapshot weather = WeatherConditions.CollectStatusHooks(
            ConditionSnapshot, status, traceAction);
        _hookTrace.AddRange(weather.Trace);
        if (weather.Filters().Any(filter => filter is
            { Filter.Value: "status_attempt", Decision: BattleHookFilterDecision.Deny }))
            return false;
        BattleHookDispatchSnapshot terrain = TerrainConditions.CollectStatusHooks(
            ConditionSnapshot, status, IsGrounded(sourceSlot), traceAction);
        _hookTrace.AddRange(terrain.Trace);
        return !terrain.Filters().Any(filter => filter is
                { Filter.Value: "status_attempt", Decision: BattleHookFilterDecision.Deny })
            && AllowsSideStatus(protectingSlot, sourceSlot, null, confusion: false, traceAction);
    }

    private bool ResolveAccuracy(BattleSlot sourceSlot, BattleSlot targetSlot, BattleCreature source,
        BattleCreature target, BattleMove move, int traceAction, out int? draw)
    {
        if (target.SemiInvulnerableState is { } state)
        {
            bool canHit = move.SecondaryEffects.OfType<SemiInvulnerableHitEffect>()
                .Any(effect => effect.States.Contains(state));
            int start = _log.Count;
            if (!canHit)
                _log.Add(new SemiInvulnerableAvoided(sourceSlot, targetSlot, state));
            AddTrace(traceAction, sourceSlot, targetSlot, EffectTraceKind.SemiInvulnerability,
                false, null, canHit ? 1 : 0, start, _log.Count);
            if (!canHit)
            {
                draw = null;
                return false;
            }
        }
        int authored = move.Ohko ? EffectMath.OhkoAccuracy(source.Level, target.Level) : move.Accuracy ?? 100;
        WeatherAccuracyEffect? weatherEffect = move.SecondaryEffects.OfType<WeatherAccuracyEffect>().SingleOrDefault();
        BattleHookDispatchSnapshot? weather = weatherEffect is null ? null
            : WeatherConditions.CollectAccuracyHooks(_conditions.Snapshot(), weatherEffect, traceAction);
        if (weather is not null)
            _hookTrace.AddRange(weather.Trace);
        bool weatherBypass = weather?.Filters().Any(filter => filter is
            { Filter.Value: "accuracy_bypass", Decision: BattleHookFilterDecision.Allow }) == true;
        bool baseBypass = move.BypassAccuracy || (!move.Ohko && move.Accuracy is null) || weatherBypass;
        var modifiers = weather?.QueryModifiers(BattleQueryId.Accuracy).ToList() ?? [];
        BattleHookDispatchSnapshot field = FieldConditions.CollectAccuracyHooks(ConditionSnapshot, traceAction);
        _hookTrace.AddRange(field.Trace);
        modifiers.AddRange(field.QueryModifiers(BattleQueryId.Accuracy));
        BattleConditionOwner targetOwner = new(BattleConditionScope.Creature, targetSlot.Side,
            targetSlot, ActiveIndex(targetSlot));
        BattleConditionInstance? guarantee = OneShotQueryConditions.FindAccuracy(ConditionSnapshot,
            targetOwner, new BattleConditionSource(sourceSlot, ActiveIndex(sourceSlot)));
        BattleAccuracyQueryResult accuracy = BattleActionQueries.Accuracy(move, authored, source, target,
            baseBypass, guarantee is not null, modifiers,
            new BattleQueryContext(sourceSlot, source, targetSlot, target, CurrentWeather, Ruleset, CurrentTerrain));
        BattleQueryResult result = accuracy.Query;
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction, sourceSlot, targetSlot, result));

        if (guarantee is not null && result.FinalValue == new BattleQueryValue(100))
            RecordConditionChanges(_conditions.Remove(guarantee.Definition.Id, guarantee.Owner, Turn, traceAction));
        if (accuracy.Bypass)
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

    private BattleMove MoveAt(BattleSlot slot, int moveIndex) => moveIndex >= 0
        ? Active(slot).Moves[moveIndex]
        : BattleFallbackRules.Compile(Ruleset, Active(slot).Moves[0].Type);

    private void LogMoveCallChain(BattleSlot source,
        IReadOnlyList<(EntityId Caller, EntityId Called)> chain)
    {
        foreach (((EntityId caller, EntityId called), int depth) in
                 chain.Select((edge, index) => (edge, index + 1)))
            _log.Add(new MoveCalled(source, caller, called, depth));
    }

    private bool ApplyTurnOrderIntent(EffectContext context, TurnOrderIntentProfile profile)
    {
        int eventStart = _log.Count;
        BattleSlot target = context.TargetSlot;
        int pendingIndex = _pendingMoveSchedule?.FindIndex(action => action.Submission.Source == target) ?? -1;
        bool applied = pendingIndex >= 0 && !_executedMoveSlots.Contains(target);
        if (applied)
        {
            BattleScheduledAction pending = _pendingMoveSchedule![pendingIndex];
            switch (profile.Kind)
            {
                case TurnOrderIntentKind.ActNext:
                    _pendingMoveSchedule.RemoveAt(pendingIndex);
                    _pendingMoveSchedule.Insert(0, pending);
                    break;
                case TurnOrderIntentKind.ActLast:
                    _pendingMoveSchedule.RemoveAt(pendingIndex);
                    _pendingMoveSchedule.Add(pending);
                    break;
                case TurnOrderIntentKind.BoostPower:
                    _scheduledPowerBoosts[target] = profile.EffectivePowerMultiplier;
                    break;
                case TurnOrderIntentKind.RepeatPending:
                    if (!_repeatPendingSlots.Add(target))
                        applied = false;
                    else
                        _pendingMoveSchedule.Insert(pendingIndex + 1, pending);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile), profile.Kind,
                        "Unknown turn-order intent.");
            }
        }
        _log.Add(applied
            ? new TurnOrderIntentApplied(context.SourceSlot, target, profile.Kind)
            : new TurnOrderIntentFailed(context.SourceSlot, target, profile.Kind));
        AddTrace(context.TraceAction, context.SourceSlot, target, EffectTraceKind.TurnOrderIntent,
            applied, null, applied ? 1 : 0, eventStart, _log.Count);
        return applied;
    }

    private bool TryPreparePairedAction(BattleSlot source, BattleCreature attacker, int moveIndex,
        BattleMove move, MoveInvocation invocation, int traceAction, BattleActionAttemptId attempt)
    {
        PairedActionProfile? profile = move.SecondaryEffects.OfType<PairedActionEffect>()
            .SingleOrDefault()?.Profile;
        if (profile is null || _pendingMoveSchedule is null)
            return false;
        int partnerIndex = _pendingMoveSchedule.FindIndex(candidate =>
            candidate.Submission.Source.Side == source.Side && candidate.Submission.Source != source
            && MoveIndex(candidate.Submission.Action) is not null);
        if (partnerIndex < 0)
            return false;
        BattleScheduledAction scheduledPartner = _pendingMoveSchedule[partnerIndex];
        BattleSlot partnerSlot = scheduledPartner.Submission.Source;
        int partnerMoveIndex = EffectiveMoveIndex(partnerSlot, MoveIndex(scheduledPartner.Submission.Action)!.Value);
        BattleMove partnerMove = MoveAt(partnerSlot, partnerMoveIndex);
        PairedActionProfile? partnerProfile = partnerMove.SecondaryEffects.OfType<PairedActionEffect>()
            .SingleOrDefault()?.Profile;
        PairedActionOption? option = profile.Options.SingleOrDefault(candidate =>
            candidate.Partner == partnerProfile?.Member);
        bool reciprocal = partnerProfile is not null && partnerProfile.Key == profile.Key
            && partnerProfile.Mode == profile.Mode
            && partnerProfile.Options.Any(candidate => candidate.Partner == profile.Member);
        if (option is null || !reciprocal)
            return false;

        _pendingMoveSchedule.RemoveAt(partnerIndex);
        _pendingMoveSchedule.Insert(0, scheduledPartner);
        _scheduledPowerBoosts[partnerSlot] = profile.PowerMultiplier;
        _scheduledPairs[partnerSlot] = (option.Type, option.SideEffect);
        int eventStart = _log.Count;
        _log.Add(new PairedActionPrepared(source, partnerSlot, profile.Key, profile.Member,
            partnerProfile!.Member, profile.Mode));
        AddTrace(traceAction, source, partnerSlot, EffectTraceKind.PairedAction, true, null,
            (int)profile.Mode + 1, eventStart, _log.Count);
        if (profile.Mode == PairedActionMode.FollowUp)
            return false;

        _actionHistory.MarkStarted(attempt);
        invocation.PpOwner.UsePp();
        ApplyChoiceLock(attacker, moveIndex);
        if (invocation.Chain.Count > 0)
        {
            _log.Add(new MoveUsed(source, invocation.Caller.Move));
            LogMoveCallChain(source, invocation.Chain);
        }
        _log.Add(new MoveUsed(source, move.Move));
        attacker.RecordMoveUse(move.Move);
        _actionHistory.Complete(attempt, BattleActionResult.Succeeded);
        return true;
    }

    private void ApplyPairedSideEffect(BattleSlot source, PairedActionSideEffect effect, int traceAction)
    {
        BattleSideCondition condition = effect switch
        {
            PairedActionSideEffect.SpeedReduction => BattleSideCondition.SpeedReduction,
            PairedActionSideEffect.ResidualDamage => BattleSideCondition.ResidualDamage,
            PairedActionSideEffect.SecondaryChanceBoost => BattleSideCondition.SecondaryChanceBoost,
            _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, "Unknown paired side effect."),
        };
        BattleSide owner = effect == PairedActionSideEffect.SecondaryChanceBoost
            ? source.Side : Opponent(source.Side);
        BattleConditionDefinition definition = SideConditions.For(condition);
        RecordConditionChanges(_conditions.Apply(new BattleConditionApplication(
            definition.Id, SideConditions.Owner(owner),
            new BattleConditionSource(source, ActiveIndex(source)), Turn, traceAction,
            definition.DefaultDuration)));
    }

    private BattleCreature? SourceCreature(BattleConditionSource source)
    {
        if (source.Slot is not { } slot || source.PartyIndex is not { } partyIndex
            || !Topology.Contains(slot) || partyIndex < 0 || partyIndex >= _parties[(int)slot.Side].Count)
            return null;
        return _parties[(int)slot.Side][partyIndex];
    }

    private MoveInvocation ResolveMoveInvocation(BattleSlot sourceSlot, BattleMove caller,
        BattleActionSubmission submission, int traceAction)
    {
        BattleMove current = caller;
        BattleMove ppOwner = caller;
        var chain = new List<(EntityId Caller, EntityId Called)>();
        for (int depth = 1; depth <= MoveReferenceResolver.MaximumDepth; depth++)
        {
            CallMoveEffect? call = current.SecondaryEffects.OfType<CallMoveEffect>().SingleOrDefault();
            if (call is null)
            {
                if (chain.Count == 0)
                    return new MoveInvocation(caller, current, ppOwner, submission, chain, null);
                BattleActionSubmission revalidated = RevalidateCalledSelection(submission, current);
                return new MoveInvocation(caller, current, ppOwner, revalidated, chain,
                    CalledTargetUnavailable(current, revalidated) ? MoveCallFailureReason.TargetUnavailable : null);
            }
            MoveReferenceCandidate[] candidates = MoveReferenceCandidates(sourceSlot, submission, call.Profile).ToArray();
            MoveReferenceCandidate? selected = MoveReferenceResolver.Select(candidates,
                call.Profile.ExcludedTags, call.Profile.PpOwner == CalledMovePpOwner.Called,
                _rng, out int? draw, out int count);
            AddTrace(traceAction, sourceSlot, selected?.OwnerSlot, EffectTraceKind.MoveSelection,
                draw is not null, draw, selected?.MoveIndex, _log.Count, _log.Count,
                draw is not null ? count : null);
            if (selected is null)
                return new MoveInvocation(caller, current, ppOwner, submission, chain,
                    MoveCallFailureReason.EmptyPool);
            if (chain.Count == 0 && call.Profile.PpOwner == CalledMovePpOwner.Called)
                ppOwner = selected.Move;
            chain.Add((current.Move, selected.Move.Move));
            current = selected.Move;
        }
        if (current.SecondaryEffects.OfType<CallMoveEffect>().Any())
            return new MoveInvocation(caller, current, ppOwner, submission, chain,
                MoveCallFailureReason.DepthExceeded);
        BattleActionSubmission finalSubmission = RevalidateCalledSelection(submission, current);
        return new MoveInvocation(caller, current, ppOwner, finalSubmission, chain,
            CalledTargetUnavailable(current, finalSubmission) ? MoveCallFailureReason.TargetUnavailable : null);
    }

    private bool CalledTargetUnavailable(BattleMove move, BattleActionSubmission submission) =>
        Topology.ActiveSlotsPerSide > 1
        && move.Target is (MoveTarget.Selected or MoveTarget.SelectedPokemonMeFirst
            or MoveTarget.Ally or MoveTarget.UserOrAlly)
        && submission.Selection is not ActiveSlotSelection;

    private IEnumerable<MoveReferenceCandidate> MoveReferenceCandidates(BattleSlot sourceSlot,
        BattleActionSubmission submission, CallMoveProfile profile)
    {
        BattleCreature source = Active(sourceSlot);
        BattleSlot? targetSlot = ReferenceTarget(sourceSlot, submission.Selection);
        BattleCreature? target = targetSlot is { } slot ? Active(slot) : null;
        IEnumerable<MoveReferenceCandidate> Known(BattleCreature creature, BattleSlot? ownerSlot, int partyIndex) =>
            creature.Moves.Select((move, index) => new MoveReferenceCandidate(move, ownerSlot, partyIndex, index));
        return profile.Selector switch
        {
            MoveReferenceSelector.UserKnown => Known(source, sourceSlot, ActiveIndex(sourceSlot)),
            MoveReferenceSelector.TargetKnown when target is not null =>
                Known(target, targetSlot, ActiveIndex(targetSlot!.Value)),
            MoveReferenceSelector.UserLastUsed => Known(source, sourceSlot, ActiveIndex(sourceSlot))
                .Where(candidate => candidate.Move.Move == source.LastMoveUsed),
            MoveReferenceSelector.TargetLastUsed when target is not null =>
                Known(target, targetSlot, ActiveIndex(targetSlot!.Value))
                    .Where(candidate => candidate.Move.Move == target.LastMoveUsed),
            MoveReferenceSelector.PartyKnown => _parties[(int)sourceSlot.Side]
                .SelectMany((creature, partyIndex) => partyIndex == ActiveIndex(sourceSlot)
                    ? [] : Known(creature, ActiveSlotForParty(sourceSlot.Side, partyIndex), partyIndex)),
            MoveReferenceSelector.AuthoredPool => profile.AuthoredPool
                .Where(_moveCatalog.ContainsKey)
                .Select(id => new MoveReferenceCandidate(_moveCatalog[id], null, -1, -1)),
            MoveReferenceSelector.EnvironmentPool when profile.EnvironmentPool.TryGetValue(
                Environment.Effective, out EntityId selected) =>
                _moveCatalog.TryGetValue(selected, out BattleMove? environmentMove)
                    ? [new MoveReferenceCandidate(environmentMove, null, -1, -1)] : [],
            MoveReferenceSelector.ExplicitReference when submission.Selection is MoveReferenceSelection reference =>
                [new MoveReferenceCandidate(Active(reference.Slot).Moves[reference.MoveIndex], reference.Slot,
                    ActiveIndex(reference.Slot), reference.MoveIndex)],
            _ => [],
        };
    }

    private BattleSlot? ReferenceTarget(BattleSlot source, BattleActionSelection? selection)
    {
        if (selection is ActiveSlotSelection active && Topology.Contains(active.Slot) && IsLive(active.Slot))
            return active.Slot;
        return Topology.SlotsFor(Opponent(source.Side)).Where(IsLive)
            .Select(slot => (BattleSlot?)slot).FirstOrDefault();
    }

    private BattleSlot? ActiveSlotForParty(BattleSide side, int partyIndex) => Topology.SlotsFor(side)
        .Where(slot => ActiveIndex(slot) == partyIndex).Select(slot => (BattleSlot?)slot).FirstOrDefault();

    private BattleActionSubmission RevalidateCalledSelection(BattleActionSubmission submission, BattleMove move)
    {
        if (Topology.ActiveSlotsPerSide == 1)
            return submission with { Selection = null, TargetPartySnapshot = null };
        BattleSlot source = submission.Source;
        BattleSlot? selected = (submission.Selection as ActiveSlotSelection)?.Slot;
        bool valid = selected is { } slot && Topology.Contains(slot) && IsLive(slot) && move.Target switch
        {
            MoveTarget.Selected or MoveTarget.SelectedPokemonMeFirst => slot.Side != source.Side,
            MoveTarget.Ally => slot.Side == source.Side && slot != source,
            MoveTarget.UserOrAlly => slot.Side == source.Side,
            _ => false,
        };
        if (!valid)
        {
            selected = move.Target switch
            {
                MoveTarget.Selected or MoveTarget.SelectedPokemonMeFirst =>
                    Topology.SlotsFor(Opponent(source.Side)).Where(IsLive)
                        .Select(slot => (BattleSlot?)slot).FirstOrDefault(),
                MoveTarget.Ally => Topology.SlotsFor(source.Side).Where(slot => slot != source && IsLive(slot))
                    .Select(slot => (BattleSlot?)slot).FirstOrDefault(),
                MoveTarget.UserOrAlly => source,
                _ => null,
            };
        }
        return submission with
        {
            Selection = selected is { } target ? new ActiveSlotSelection(target) : null,
            TargetPartySnapshot = null,
        };
    }

    private bool PrepareTimedMove(BattleCreature attacker, BattleSlot sourceSlot, BattleMove move,
        int moveIndex, int traceAction, BattleActionSubmission? submission = null,
        BattleMove? ppOwner = null)
    {
        bool firing = attacker.IsCharging;
        bool requiresCharge = move.Charge is not null;
        if (requiresCharge && !firing
            && move.SecondaryEffects.OfType<WeatherMoveEffect>().SingleOrDefault() is { } weatherEffect)
        {
            BattleHookDispatchSnapshot weather = WeatherConditions.CollectChargeHooks(
                ConditionSnapshot, weatherEffect, traceAction);
            _hookTrace.AddRange(weather.Trace);
            requiresCharge = !weather.Filters().Any(filter => filter is
                { Filter.Value: "charge_required", Decision: BattleHookFilterDecision.Deny });
        }
        if (move.Charge is not null && !firing)
            ApplyChargeStartEffects(attacker, sourceSlot, move);
        if (requiresCharge && !firing)
        {
            int eventStart = _log.Count;
            (ppOwner ?? move).UsePp();
            if (moveIndex >= 0)
                ApplyChoiceLock(attacker, moveIndex);
            attacker.RecordMoveUse(move.Move);
            ChargeMoveEffect charge = move.Charge!;
            BattleIntent intent = _intentQueue.Enqueue(new BattleIntentRequest(
                Turn + 1,
                BattleIntentCheckpoint.PreAction,
                new BattleIntentOwner(BattleIntentOwnerScope.Creature, sourceSlot.Side, sourceSlot,
                    ActiveIndex(sourceSlot), BattleIntentSwitchPolicy.Cancel, BattleIntentFaintPolicy.Cancel),
                ChargeTarget(sourceSlot, move, submission?.Selection, charge.TargetPolicy),
                new ReleaseMoveIntent(moveIndex, charge.State),
                move.Move,
                traceAction,
                Ruleset));
            AddIntentTrace(intent, EffectTraceKind.IntentEnqueued, true, _log.Count, _log.Count);
            attacker.StartCharging(moveIndex, charge.State);
            _log.Add(new Charging(sourceSlot, move.Move));
            AddTrace(traceAction, sourceSlot, null, EffectTraceKind.Charge, true, null,
                charge.State is { } state ? (int)state + 1 : 0, eventStart, _log.Count);
            return false;
        }
        if (firing)
        {
            attacker.StopCharging();
            _log.Add(new ChargeReleased(sourceSlot, move.Move));
            AddTrace(traceAction, sourceSlot, null, EffectTraceKind.Charge, false, null, 1,
                _log.Count - 1, _log.Count);
        }

        bool continuingLock = attacker.IsLocked;
        if (move.MultiTurnLockProfile is { } lockProfile && !continuingLock)
        {
            bool drawsDuration = lockProfile.MinTurns != lockProfile.MaxTurns;
            int duration = drawsDuration ? _rng.Next(lockProfile.MinTurns, lockProfile.MaxTurns + 1) : lockProfile.MinTurns;
            attacker.StartLock(moveIndex, duration, submission?.Selection);
            _log.Add(new MultiTurnLockStarted(sourceSlot, move.Move, duration));
            AddTrace(traceAction, sourceSlot, null, EffectTraceKind.LockDuration, drawsDuration,
                drawsDuration ? duration : null, duration, _log.Count - 1, _log.Count,
                drawsDuration ? lockProfile.MaxTurns + 1 : null, lockProfile.MinTurns);
        }

        if (!firing && (!continuingLock || move.MultiTurnLockProfile?.RepeatPaysPp == true))
            (ppOwner ?? move).UsePp();
        if (!continuingLock && moveIndex >= 0)
            ApplyChoiceLock(attacker, moveIndex);
        return true;
    }

    private void ApplyChargeStartEffects(BattleCreature creature, BattleSlot slot, BattleMove move)
    {
        foreach (ChargeStartStatEffect stat in move.SecondaryEffects.OfType<ChargeStartStatEffect>())
        {
            int before = creature.Stage(stat.Stat);
            creature.ChangeStage(stat.Stat, stat.Delta);
            int applied = creature.Stage(stat.Stat) - before;
            if (applied != 0)
                _log.Add(new StatStageChanged(slot, stat.Stat, applied));
        }
    }

    private BattleIntentTarget ChargeTarget(BattleSlot sourceSlot, BattleMove move,
        BattleActionSelection? selection, BattleIntentTargetPolicy policy)
    {
        if (move.Target == MoveTarget.User)
            return new BattleIntentTarget(BattleIntentTargetPolicy.Source);
        if (move.Target is MoveTarget.UserAndAllies or MoveTarget.AllAllies or MoveTarget.UsersField)
            return new BattleIntentTarget(BattleIntentTargetPolicy.Side, Side: sourceSlot.Side);
        if (move.Target is MoveTarget.AllOpponents or MoveTarget.OpponentsField)
            return new BattleIntentTarget(BattleIntentTargetPolicy.Side, Side: Opponent(sourceSlot.Side));
        if (move.Target is MoveTarget.RandomOpponent)
            return new BattleIntentTarget(BattleIntentTargetPolicy.Side, Side: Opponent(sourceSlot.Side));
        if (move.Target is MoveTarget.AllOtherPokemon or MoveTarget.AllPokemon or MoveTarget.EntireField)
            return new BattleIntentTarget(BattleIntentTargetPolicy.Field);

        BattleSlot target = selection is ActiveSlotSelection active
            ? active.Slot
            : new BattleSlot(Opponent(sourceSlot.Side), 0);
        return policy switch
        {
            BattleIntentTargetPolicy.LiveSlot => new(policy, target, Side: target.Side),
            BattleIntentTargetPolicy.SnapshotSlot => new(policy, target, ActiveIndex(target), target.Side),
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy,
                "Charge target policy must be live-slot or snapshot-slot."),
        };
    }

    private void CancelCharge(BattleSlot slot, BattleCreature creature)
    {
        if (creature.ChargingMoveIndex is not { } moveIndex)
            return;
        EntityId move = creature.Moves[moveIndex].Move;
        creature.StopCharging();
        _log.Add(new ChargeCancelled(slot, move));
    }

    private void ResolveMove(BattleSlot sourceSlot, int moveIndex,
        BattleActionSubmission? submission = null)
    {
        BattleSide side = sourceSlot.Side;
        BattleCreature attacker = Active(sourceSlot);
        if (attacker.IsFainted)
            return;

        BattleMove move = MoveAt(sourceSlot, moveIndex);
        ActionLegalityResult lockedLegality = LockedMoveLegality(attacker, sourceSlot, moveIndex);
        if (!lockedLegality.Allowed)
        {
            _log.Add(new ActionBlocked(sourceSlot, lockedLegality.Reason, lockedLegality.Condition!.Value));
            EndMultiTurnLock(sourceSlot, attacker, MultiTurnLockEndReason.SelectionBlocked);
            return;
        }
        if (!move.HasPp && attacker.IsLocked && move.MultiTurnLockProfile?.RepeatPaysPp == true)
        {
            _log.Add(new ActionInvalidated(sourceSlot, ActionInvalidationReason.ResourceChanged));
            EndMultiTurnLock(sourceSlot, attacker, MultiTurnLockEndReason.NoPp);
            return;
        }
        int traceAction = ++_traceActionSequence;
        BattleHistoryOwner sourceOwner = HistoryOwner(sourceSlot);
        BattleActionAttemptId attempt = _actionHistory.BeginMove(traceAction, sourceOwner, move.Move);
        if (!CanAct(attacker, sourceSlot, traceAction))
        {
            CancelCharge(sourceSlot, attacker);
            ResetFailedProtection(attacker, move);
            _actionHistory.Complete(attempt, BattleActionResult.Prevented);
            return; // frozen/asleep/fully-paralyzed — no PP spent, no move
        }
        if (!PassesActionFilterGate(sourceSlot, traceAction))
        {
            _actionHistory.Complete(attempt, BattleActionResult.Prevented);
            return;
        }

        if (attacker.Flinched)
        {
            CancelCharge(sourceSlot, attacker);
            ResetFailedProtection(attacker, move);
            int start = _log.Count;
            _log.Add(new Flinched(sourceSlot));
            AddTrace(traceAction, sourceSlot, null, EffectTraceKind.FlinchGate, false, null, 0, start, _log.Count);
            _actionHistory.Complete(attempt, BattleActionResult.Prevented);
            return; // flinch costs the turn but no PP
        }
        AddTrace(traceAction, sourceSlot, null, EffectTraceKind.FlinchGate, false, null, 1, _log.Count, _log.Count);

        if (!PushesThroughConfusion(attacker, sourceSlot, traceAction))
        {
            CancelCharge(sourceSlot, attacker);
            ResetFailedProtection(attacker, move);
            _actionHistory.Complete(attempt, BattleActionResult.Prevented);
            return; // hurt itself in confusion — no PP, no move
        }

        BattleActionSubmission submitted = submission!;
        MoveInvocation invocation = ResolveMoveInvocation(sourceSlot, move, submitted, traceAction);
        if (invocation.Failure is { } callFailure)
        {
            _actionHistory.MarkStarted(attempt);
            invocation.PpOwner.UsePp();
            ApplyChoiceLock(attacker, moveIndex);
            _log.Add(new MoveUsed(sourceSlot, invocation.Caller.Move));
            LogMoveCallChain(sourceSlot, invocation.Chain);
            _log.Add(new MoveCallFailed(sourceSlot, invocation.Caller.Move, callFailure));
            attacker.RecordMoveUse(invocation.Caller.Move);
            _actionHistory.Complete(attempt, BattleActionResult.Failed);
            return;
        }
        move = invocation.Executed;
        if (move.Move != invocation.Caller.Move)
            _actionHistory.ReplacePendingMove(attempt, move.Move);
        BattleActionSubmission effectiveSubmission = invocation.Submission;
        BattleSlot? gateTarget = GateTarget(sourceSlot, move, effectiveSubmission.Selection);
        if (!PassesMoveGates(attacker, sourceSlot, move, traceAction,
                MoveGateTiming.BeforeMove, gateTarget))
        {
            CancelCharge(sourceSlot, attacker);
            ResetFailedProtection(attacker, move);
            _actionHistory.Complete(attempt, BattleActionResult.Failed);
            return;
        }
        BattleSide targetSide = BattleTargetResolver.IsSinglesActiveCreatureTarget(move.Target)
            ? BattleTargetResolver.ResolveSinglesActiveCreatureSide(move.Target, side)
            : Opponent(side);
        BattleSlot targetSlot = new(targetSide, 0);
        BattleCreature target = Active(targetSlot);
        bool snapshotUnavailable = effectiveSubmission.TargetPartySnapshot is { } snapshot
            && ActiveIndex(targetSlot) != snapshot;
        if (target.IsFainted || snapshotUnavailable)
        {
            if (attacker.IsCharging)
                PrepareTimedMove(attacker, sourceSlot, move, moveIndex, traceAction,
                    effectiveSubmission, invocation.PpOwner);
            if (snapshotUnavailable)
                _log.Add(new MoveFailed(sourceSlot, move.Move, MoveFailureReason.TargetUnavailable));
            _actionHistory.Complete(attempt, BattleActionResult.Failed);
            return;
        }
        BattleHistoryOwner targetOwner = HistoryOwner(targetSlot);

        // Two-turn move: turn 1 charges (PP spent now, no damage); turn 2 fires as a normal hit.
        _actionHistory.MarkStarted(attempt);
        int ppBeforeSpend = invocation.PpOwner.Pp;
        if (!PrepareTimedMove(attacker, sourceSlot, move, moveIndex, traceAction,
                effectiveSubmission, invocation.PpOwner))
        {
            _actionHistory.Complete(attempt, BattleActionResult.Succeeded, [targetOwner]);
            return;
        }

        if (invocation.Chain.Count > 0)
        {
            _log.Add(new MoveUsed(sourceSlot, invocation.Caller.Move));
            LogMoveCallChain(sourceSlot, invocation.Chain);
        }
        _log.Add(new MoveUsed(sourceSlot, move.Move));
        if (!PassesMoveGates(attacker, sourceSlot, move, traceAction,
                MoveGateTiming.AfterMoveUsed, gateTarget))
        {
            attacker.RecordMoveUse(move.Move);
            ResetFailedProtection(attacker, move);
            _actionHistory.Complete(attempt, BattleActionResult.Failed, [targetOwner]);
            return;
        }
        attacker.RecordMoveUse(move.Move);
        BattleMoveIdentityQueryResult moveIdentity = EffectiveMoveIdentity(sourceSlot, move, traceAction);
        EntityId moveType = moveIdentity.EffectiveType;

        var actionContext = new BattleActionContext(move, attacker, sourceSlot, traceAction);
        BattleTargetContext targetContext = actionContext.AddTarget(target, targetSlot);

        if (!TryItemPower(sourceSlot, move, out int? itemPower))
        {
            _log.Add(new MoveFailed(sourceSlot, move.Move, MoveFailureReason.FormulaInputUnavailable));
            RecordDamage(attempt, sourceOwner, targetOwner, move, DamageCause(move), 0,
                false, BattleDamageFailure.NoQualifyingDamage, 0, default, critical: false,
                effectiveType: moveType, effectiveClass: moveIdentity.EffectiveClass);
            _actionHistory.Complete(attempt, BattleActionResult.Failed, [targetOwner]);
            return;
        }

        if (!AllowsProtectionHit(sourceSlot, targetSlot, move, traceAction))
        {
            if (RecordsMoveDamage(move))
                RecordDamage(attempt, sourceOwner, targetOwner, move, DamageCause(move), 0,
                    false, BattleDamageFailure.Protected, 0, default, critical: false,
                    effectiveType: moveType, effectiveClass: moveIdentity.EffectiveClass);
            ApplyCrashRecoil(attacker, sourceSlot, move);
            _actionHistory.Complete(attempt, BattleActionResult.Failed, [targetOwner]);
            return;
        }

        if (!TerrainAllowsPriorityHit(sourceSlot, targetSlot, move, traceAction))
        {
            if (RecordsMoveDamage(move))
                RecordDamage(attempt, sourceOwner, targetOwner, move, DamageCause(move), 0,
                    false, BattleDamageFailure.Blocked, 0, default, critical: false,
                    effectiveType: moveType, effectiveClass: moveIdentity.EffectiveClass);
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
                    effectiveType: moveType, effectiveClass: moveIdentity.EffectiveClass);
            ApplyCrashRecoil(attacker, sourceSlot, move);
            _actionHistory.Complete(attempt, BattleActionResult.Missed, [targetOwner]);
            return;
        }

        if (move.SecondaryEffects.OfType<DelayedDamageEffect>().SingleOrDefault() is { } delayedDamage)
        {
            bool queued = QueueDelayedDamage(actionContext, targetContext, delayedDamage);
            _actionHistory.Complete(attempt,
                queued ? BattleActionResult.Succeeded : BattleActionResult.Failed, [targetOwner]);
            return;
        }

        ApplyBeforeDamageSideRemovals(actionContext, [targetContext]);
        int? randomPower = SelectRandomPower(sourceSlot, move, traceAction);

        if (move.CounterCategory is { } counterCat)
        {
            // Counter/Mirror Coat: return 2× the damage of that category taken this turn (no draw).
            int received = counterCat == DamageClass.Physical ? attacker.PhysicalDamageTaken : attacker.SpecialDamageTaken;
            if (received > 0)
            {
                int dmg = TraceUnmodifiedFinalDamage(sourceSlot, targetSlot, attacker, target,
                    move, checked(received * 2), traceAction);
                DamageApplication applied = DealMoveDamage(target, targetSlot, dmg, 1.0, crit: false,
                    HpStatusFormulas.CannotKoFloor(move));
                targetContext.AddDamage(actionContext, applied.ActualHpRemoved);
                RecordDamage(attempt, sourceOwner, targetOwner, move, BattleDamageCause.Counter, 1, true,
                    applied.ActualHpRemoved > 0 ? BattleDamageFailure.None : BattleDamageFailure.NoDamage,
                    dmg, applied, critical: false, effectiveType: moveType,
                    effectiveClass: moveIdentity.EffectiveClass);
            }
            else
            {
                _log.Add(new MoveMissed(sourceSlot, move.Move)); // nothing to counter → fizzles
                RecordDamage(attempt, sourceOwner, targetOwner, move, BattleDamageCause.Counter, 0, false,
                    BattleDamageFailure.NoQualifyingDamage, 0, default, critical: false,
                    effectiveType: moveType, effectiveClass: moveIdentity.EffectiveClass);
            }
        }
        else if (move.Ohko || move.FixedDamage is not null || move.FixedDamageLevel)
        {
            // Formula-bypassing hit: no crit/STAB/roll (no RNG draws), but type immunity still voids it.
            BattleDamageQueryResult damageQuery = ResolveDamageQuery(sourceSlot, targetSlot, attacker,
                target, move, moveIdentity, EffectiveValues(sourceSlot), EffectiveValues(targetSlot), 1, traceAction);
            double eff = TypeChart.ToDouble(damageQuery.Effectiveness.FinalValue);
            int dmg = eff <= 0 ? 0
                : move.Ohko ? target.CurrentHp
                : move.FixedDamageLevel ? attacker.Level
                : move.FixedDamage!.Value;
            dmg = TraceUnmodifiedFinalDamage(sourceSlot, targetSlot, attacker, target, move, dmg,
                traceAction, eff > 0);
            DamageApplication applied = DealMoveDamage(target, targetSlot, dmg, eff, crit: false,
                HpStatusFormulas.CannotKoFloor(move));
            targetContext.AddDamage(actionContext, applied.ActualHpRemoved);
            BattleDamageCause cause = move.Ohko ? BattleDamageCause.OneHitKnockout
                : move.FixedDamageLevel ? BattleDamageCause.Level : BattleDamageCause.Fixed;
            RecordDamage(attempt, sourceOwner, targetOwner, move, cause, 1, true,
                eff <= 0 ? BattleDamageFailure.Immune
                    : applied.ActualHpRemoved > 0 ? BattleDamageFailure.None : BattleDamageFailure.NoDamage,
                dmg, applied, critical: false, effectiveType: moveType,
                effectiveClass: moveIdentity.EffectiveClass);
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
                (int dmg, bool crit, double eff) = ComputeHit(sourceSlot, targetSlot, attacker, target, move, moveIdentity, power, 1,
                    ppBeforeSpend, itemPower, randomPower, traceAction,
                    out double? critDraw, out int? damageRollDraw);
                AddTrace(traceAction, sourceSlot, targetSlot, EffectTraceKind.Immunity, false, null,
                    eff <= 0 ? 0 : 1, _log.Count, _log.Count);
                if (eff > 0)
                {
                    AddTrace(traceAction, sourceSlot, targetSlot, EffectTraceKind.Critical,
                        critDraw is not null, critDraw, crit ? 1 : 0, _log.Count, _log.Count,
                        critDraw is not null ? 1 : null);
                    AddTrace(traceAction, sourceSlot, targetSlot, EffectTraceKind.DamageRoll, true,
                        damageRollDraw, damageRollDraw is { } roll ? roll + 85 : null, _log.Count, _log.Count, 16);
                }
                DamageApplication applied = DealMoveDamage(target, targetSlot, dmg, eff, crit,
                    HpStatusFormulas.CannotKoFloor(move));
                target.RecordDamageTaken(moveIdentity.EffectiveClass, applied.ActualHpRemoved); // for Counter/Mirror Coat
                targetContext.AddDamage(actionContext, applied.ActualHpRemoved);
                RecordDamage(attempt, sourceOwner, targetOwner, move, BattleDamageCause.Standard, h + 1, true,
                    eff <= 0 ? BattleDamageFailure.Immune
                        : applied.ActualHpRemoved > 0 ? BattleDamageFailure.None : BattleDamageFailure.NoDamage,
                    dmg, applied, crit, effectiveType: moveType, effectiveClass: moveIdentity.EffectiveClass);
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
        BattleCreature source, BattleCreature target, BattleMove move, int damage, int traceAction,
        bool applyMoveModifiers = true)
    {
        var context = new BattleQueryContext(sourceSlot, source, targetSlot, target,
            CurrentWeather, Ruleset, CurrentTerrain);
        BattleQueryResult result = applyMoveModifiers
            ? BattleActionQueries.FinalDamage(move, damage, null, context)
            : BattleQuery.Evaluate(BattleQueryId.FinalDamage, new BattleQueryValue(damage), [], context);
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
            case StatStealEffect steal: ApplyStatSteal(ctx, steal); break;
            case RandomStatRaiseEffect raise: ApplyRandomStatRaise(ctx, raise); break;
            case DerivedStatSwapEffect swap: ApplyDerivedStatSwap(ctx, swap); break;
            case DerivedStatSplitEffect split: ApplyDerivedStatSplit(ctx, split); break;
            case StatInvertEffect i: ApplyStatInvert(ctx, i); break;
            case ConfusionEffect confusion: ApplyConfusion(ctx, confusion); break;
            case FlinchEffect f: ApplyFlinch(ctx, f); break;
            case LeechSeedEffect: ApplyLeechSeed(ctx); break;
            case BindEffect: ApplyBind(ctx); break;
            case ProtectEffect protect:
                return ApplyProtection(ctx, protect.Profile);
            case ForceSwitchEffect: ForceSwitch(ctx); break;
            case PositionSwapEffect: SwapPositions(ctx); break;
            case CallMoveEffect:
                break; // consumed before ordinary effect resolution.
            case PairedActionEffect:
                break; // consumed by current-turn scheduling.
            case TurnOrderIntentEffect order:
                return ApplyTurnOrderIntent(ctx, order.Profile);
            case ItemRequireEffect:
                break; // evaluated before PP and accuracy in PassesMoveGates.
            case ItemMutationEffect item:
                return ApplyItemMutation(ctx, item);
            case AbilityMutationEffect ability:
                return ApplyAbilityMutation(ctx, ability);
            case TypeMutationEffect type:
                return ApplyTypeMutation(ctx, type);
            case RedirectEffect redirect: _redirects.Add(new RedirectCondition(ctx.SourceSlot, redirect.Priority,
                redirect.AcceptedClasses, redirect.BypassClasses, redirect.AcceptedTags, redirect.BypassTags)); break;
            case DrainEffect d when ctx.ActionDamageDealt > 0:
                Heal(ctx.Source, ctx.SourceSlot,
                    EffectMath.DrainHeal(ctx.ActionDamageDealt, d.Fraction.Num, d.Fraction.Den), ctx.Move);
                break;
            case HealEffect h:
                (BattleCreature healRecipient, BattleSlot healSlot) = FractionRecipient(ctx, h.Recipient);
                if (!healRecipient.IsFainted)
                {
                    BattleHookDispatchSnapshot weather = WeatherConditions.CollectHealingHooks(
                        ConditionSnapshot, h, healRecipient.MaxHp, ctx.TraceAction);
                    _hookTrace.AddRange(weather.Trace);
                    BattleHookDispatchSnapshot terrain = TerrainConditions.CollectHealingHooks(
                        ConditionSnapshot, h, healRecipient.MaxHp, ctx.TraceAction);
                    _hookTrace.AddRange(terrain.Trace);
                    Heal(healRecipient, healSlot,
                        EffectMath.HealAmount(healRecipient.MaxHp, h.Fraction.Num, h.Fraction.Den),
                        ctx.Move,
                        [.. weather.QueryModifiers(BattleQueryId.Healing),
                         .. terrain.QueryModifiers(BattleQueryId.Healing)]);
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
                or WeatherAccuracyEffect or MoveQueryModifierEffect or AccuracyQueryEffect:
                break; // evaluated in ComputeHit before DamageCalc.
            case OneShotQueryEffect query:
                ApplyOneShotQuery(ctx, query);
                break;
            case DelayedDamageEffect:
                break; // queued before the ordinary damage pipeline.
            case DelayedHealEffect delayedHeal:
                return QueueDelayedHeal(ctx, delayedHeal);
            case DelayedStatusEffect delayedStatus:
                return QueueDelayedStatus(ctx, delayedStatus);
            case ReplacementRestoreEffect replacement:
                return QueueReplacementRestore(ctx, replacement);
            case RecoilEffect r when ctx.ActionDamageDealt > 0:
                Sap(ctx.Source, ctx.SourceSlot, EffectMath.RecoilDamage(ctx.ActionDamageDealt, r.Fraction.Num, r.Fraction.Den),
                    amt => new Recoiled(ctx.SourceSlot, amt));
                break;
            case CritBoostEffect cb:
                ctx.Source.RaiseCrit(cb.Stages);
                _log.Add(new CritBoosted(ctx.SourceSlot));
                break;
            case MultiTurnPowerBoostEffect boost:
                ctx.Source.SetMultiTurnPowerBoost(boost.Key, boost.Multiplier);
                break;
            case SelfDestructEffect when !ctx.Source.IsFainted:
                ctx.Source.TakeDamage(ctx.Source.MaxHp);
                RecordFaint(ctx.SourceSlot);
                break;
            case SetEntryHazardEffect hazard:
                return ApplyEntryHazard(ctx, hazard.Hazard);
            case SetWeatherEffect w when w.Weather != CurrentWeather:
                SetWeather(w.Weather, WeatherConditions.DefaultTurns, ctx.SourceSlot);
                break;
            case SetTerrainEffect t when t.Terrain != CurrentTerrain:
                SetTerrain(t.Terrain, TerrainConditions.DefaultTurns, ctx.SourceSlot);
                break;
            case GroundedStateEffect grounded:
                ApplyGroundedState(ctx, grounded);
                break;
            case SetFieldConditionEffect field:
                ApplyFieldCondition(ctx, field);
                break;
            case SetSideConditionEffect side:
                return ApplySideCondition(ctx, side);
            case ApplyActionFilterEffect filter:
                return ApplyActionFilter(ctx, filter);
            case RemoveSideConditionEffect { Timing: SideConditionTiming.AfterHit } remove:
                RemoveSideConditions(ctx, remove);
                break;
            case RemoveConditionEffect remove:
                return ApplyConditionRemove(ctx, remove);
            case TransferConditionEffect transfer:
                return ApplyConditionTransfer(ctx, transfer);
            case SwapConditionEffect swap:
                return ApplyConditionSwap(ctx, swap);
            case SideConditionBypassEffect or ProtectionBypassEffect or RemoveSideConditionEffect
                or DamageStatQueryEffect or DamageClassQueryEffect or EffectivenessQueryEffect:
                break;
            case RemoveTerrainEffect:
                ClearTerrain();
                break;
            case TerrainMoveEffect or TerrainGateEffect:
                break;
            case MoveGateEffect or FieldMoveGateEffect or SemiInvulnerableHitEffect
                or ChargeStartStatEffect:
                break; // evaluated before PP/RNG in PassesMoveGates.
            case QueueActionGateEffect gate when gate.Owner == QueueActionGateOwner.Creature
                && ctx.ActionDamageDealt <= 0:
                break;
            case QueueActionGateEffect gate:
                QueueActionGate(ctx, gate);
                break;
        }
        return true;
    }

    private bool ApplyActionFilter(EffectContext ctx, ApplyActionFilterEffect effect)
    {
        BattleSlot owner = effect.Owner == SideConditionTarget.Source ? ctx.SourceSlot : ctx.TargetSlot;
        try
        {
            ApplyActionFilterCondition(owner, ctx.SourceSlot, effect.Filter, effect.Duration, effect.MoveTag);
            return true;
        }
        catch (ArgumentException)
        {
            ctx.Action.MarkFailed();
            return false;
        }
    }

    private bool ApplyItemMutation(EffectContext ctx, ItemMutationEffect effect)
    {
        bool transfer = effect.Operation is BattleItemOperation.Give or BattleItemOperation.Steal
            or BattleItemOperation.Swap;
        BattleSlot primarySlot = effect.Subject == BattleItemSubject.Target && !transfer
            ? ctx.TargetSlot : ctx.SourceSlot;
        BattleCreature primary = effect.Subject == BattleItemSubject.Target && !transfer
            ? ctx.Target : ctx.Source;
        BattleSlot secondarySlot = transfer ? ctx.TargetSlot : primarySlot;
        BattleCreature secondary = transfer ? ctx.Target : primary;
        BattleOverlayOwner primaryOwner = OverlayOwner(primarySlot);
        BattleOverlayOwner secondaryOwner = OverlayOwner(secondarySlot);
        int start = _log.Count;
        BattleItemMutationResult result = _items.Mutate(effect.Operation,
            primaryOwner, PhysicalMetricFormulas.BaseEffectiveValues(primary), primary.IsFainted,
            secondaryOwner, PhysicalMetricFormulas.BaseEffectiveValues(secondary), secondary.IsFainted,
            Turn, ctx.TraceAction, effect.Cause, effect.Duration, StickyItemProtection);
        if (result.Succeeded)
        {
            foreach ((BattleOverlayOwner owner, EntityId? before, EntityId? after) in result.Changes)
            {
                _parties[(int)owner.Side][owner.PartyIndex].ResetConsumedHeldEffects();
                _log.Add(new HeldItemMutated(owner.Side, owner.PartyIndex, before, after,
                    effect.Operation, effect.Cause));
            }
            if (effect.Operation == BattleItemOperation.Consume)
                _log.Add(new HeldItemConsumed(primarySlot, effect.Cause));
        }
        else
        {
            ctx.Action.MarkFailed();
        }
        _trace.Add(new EffectTraceEntry(Turn, ctx.TraceAction, ctx.SourceSlot,
            ctx.TargetContext?.TargetSlot, EffectTraceKind.HeldItemMutation, result.Succeeded, null,
            result.Succeeded ? result.Changes.Count : -(int)result.Failure, start, _log.Count));
        return result.Succeeded;
    }

    private bool StickyItemProtection(BattleOverlayOwner owner, BattleItemOperation operation)
    {
        return AbilityHooks(owner).SelectMany(hook => hook.Effects)
            .Any(effect => effect.Op == "itemMutationGuard"
                && BattleItemState.Operations(effect).Contains(operation));
    }

    private bool ApplyTypeMutation(EffectContext ctx, TypeMutationEffect effect)
    {
        (BattleOverlayOwner Owner, BattleEffectiveValues Base, bool Fainted) Party(BattleTypeSubject which)
        {
            BattleSlot slot = which == BattleTypeSubject.User ? ctx.SourceSlot : ctx.TargetSlot;
            BattleCreature creature = which == BattleTypeSubject.User ? ctx.Source : ctx.Target;
            return (OverlayOwner(slot), PhysicalMetricFormulas.BaseEffectiveValues(creature), creature.IsFainted);
        }

        var subject = Party(effect.Subject);
        (BattleOverlayOwner, BattleEffectiveValues, bool)? source = effect.Source is { } src ? Party(src) : null;
        int start = _log.Count;
        BattleTypeMutationResult result = _types.Mutate(effect.Operation, effect.Types, subject, source,
            Turn, ctx.TraceAction);
        if (result.Succeeded)
            _log.Add(new CreatureTypesMutated(subject.Owner.Side, subject.Owner.PartyIndex,
                result.Before, result.After, effect.Operation));
        else
            ctx.Action.MarkFailed();
        _trace.Add(new EffectTraceEntry(Turn, ctx.TraceAction, ctx.SourceSlot, ctx.TargetSlot,
            EffectTraceKind.CreatureTypesMutation, result.Succeeded, null,
            result.Succeeded ? result.After.Count : -(int)result.Failure, start, _log.Count));
        return result.Succeeded;
    }

    private bool ApplyAbilityMutation(EffectContext ctx, AbilityMutationEffect effect)
    {
        BattleOverlayOwner user = OverlayOwner(ctx.SourceSlot);
        BattleOverlayOwner target = OverlayOwner(ctx.TargetSlot);
        var allies = Topology.Slots.Where(slot => slot.Side == ctx.SourceSlot.Side && slot != ctx.SourceSlot
                && IsLive(slot))
            .Select(slot =>
            {
                BattleCreature creature = Active(slot);
                return (OverlayOwner(slot), PhysicalMetricFormulas.BaseEffectiveValues(creature), creature.IsFainted);
            }).ToArray();
        int start = _log.Count;
        BattleAbilityMutationResult result = _abilities.Mutate(effect.Operation, effect.Subject, effect.Source,
            effect.Ability, user, PhysicalMetricFormulas.BaseEffectiveValues(ctx.Source), ctx.Source.IsFainted,
            target, PhysicalMetricFormulas.BaseEffectiveValues(ctx.Target), ctx.Target.IsFainted, allies,
            Turn, ctx.TraceAction);
        if (result.Succeeded)
            foreach ((BattleOverlayOwner owner, EntityId? before, EntityId? after) in result.Changes)
                _log.Add(new AbilityMutated(owner.Side, owner.PartyIndex, before, after, effect.Operation));
        else
            ctx.Action.MarkFailed();
        _trace.Add(new EffectTraceEntry(Turn, ctx.TraceAction, ctx.SourceSlot, ctx.TargetSlot,
            EffectTraceKind.AbilityMutation, result.Succeeded, null,
            result.Succeeded ? result.Changes.Count : -(int)result.Failure, start, _log.Count));
        return result.Succeeded;
    }

    private IReadOnlyList<AbilityHook> AbilityHooks(BattleOverlayOwner owner)
    {
        BattleCreature creature = _parties[(int)owner.Side][owner.PartyIndex];
        return _abilities.Hooks(owner, PhysicalMetricFormulas.BaseEffectiveValues(creature), creature.AbilityHooks);
    }

    private IReadOnlyList<AbilityHook> AbilityHooks(BattleSlot slot) => AbilityHooks(OverlayOwner(slot));

    private BattleOverlayOwner OverlayOwner(BattleSlot slot) =>
        new(slot.Side, ActiveIndex(slot), slot);

    private bool ApplyConditionRemove(EffectContext ctx, RemoveConditionEffect effect)
    {
        BattleConditionSource userSource = ConditionSource(ctx.SourceSlot, ctx.Source);
        BattleConditionSource targetSource = ctx.TargetContext is null
            ? userSource : ConditionSource(ctx.TargetSlot, ctx.Target);
        BattleConditionMutationResult result = _conditions.RemoveSelected(effect.Selector,
            ConditionOwner(ctx, effect.Selector.Scope, effect.Owner), userSource, targetSource,
            Turn, ctx.TraceAction);
        return RecordConditionMutation(ctx, BattleConditionOperation.Remove,
            EffectTraceKind.ConditionRemoval, effect.Selector.Scope, result);
    }

    private bool ApplyConditionTransfer(EffectContext ctx, TransferConditionEffect effect)
    {
        BattleConditionSource userSource = ConditionSource(ctx.SourceSlot, ctx.Source);
        BattleConditionSource targetSource = ConditionSource(ctx.TargetSlot, ctx.Target);
        BattleConditionMutationResult result = _conditions.TransferSelected(effect.Selector,
            ConditionOwner(ctx, effect.Selector.Scope, effect.From),
            ConditionOwner(ctx, effect.Selector.Scope, effect.To),
            userSource, targetSource, effect.ResetDuration, effect.ResetCounters,
            Turn, ctx.TraceAction);
        return RecordConditionMutation(ctx, BattleConditionOperation.Transfer,
            EffectTraceKind.ConditionTransfer, effect.Selector.Scope, result);
    }

    private bool ApplyConditionSwap(EffectContext ctx, SwapConditionEffect effect)
    {
        BattleConditionSource userSource = ConditionSource(ctx.SourceSlot, ctx.Source);
        BattleConditionSource targetSource = ConditionSource(ctx.TargetSlot, ctx.Target);
        BattleConditionMutationResult result = _conditions.SwapSelected(effect.Selector,
            ConditionOwner(ctx, effect.Selector.Scope, SideConditionTarget.Source),
            ConditionOwner(ctx, effect.Selector.Scope, SideConditionTarget.Target),
            userSource, targetSource, effect.ResetDuration, effect.ResetCounters,
            Turn, ctx.TraceAction);
        return RecordConditionMutation(ctx, BattleConditionOperation.Swap,
            EffectTraceKind.ConditionSwap, effect.Selector.Scope, result);
    }

    private bool RecordConditionMutation(EffectContext ctx, BattleConditionOperation operation,
        EffectTraceKind traceKind, BattleConditionScope scope, BattleConditionMutationResult result)
    {
        int start = _log.Count;
        RecordConditionChanges(result.Changes);
        if (result.Outcome == BattleConditionMutationOutcome.NoMatch)
            _log.Add(new ConditionOperationNoOp(operation, scope));
        else if (result.Outcome == BattleConditionMutationOutcome.Conflict)
        {
            _log.Add(new ConditionOperationRejected(operation, scope));
            ctx.Action.MarkFailed();
        }
        _trace.Add(new EffectTraceEntry(Turn, ctx.TraceAction, ctx.SourceSlot,
            ctx.TargetContext?.TargetSlot, traceKind,
            result.Outcome == BattleConditionMutationOutcome.Applied, null,
            result.Changes.Affected.Count, start, _log.Count));
        return result.Outcome != BattleConditionMutationOutcome.Conflict;
    }

    private BattleConditionOwner ConditionOwner(EffectContext ctx, BattleConditionScope scope,
        SideConditionTarget target)
    {
        if (scope is BattleConditionScope.Field or BattleConditionScope.Weather
            or BattleConditionScope.Terrain or BattleConditionScope.Room)
            return new BattleConditionOwner(scope);
        BattleSlot slot = target == SideConditionTarget.Source ? ctx.SourceSlot : ctx.TargetSlot;
        BattleCreature creature = target == SideConditionTarget.Source ? ctx.Source : ctx.Target;
        return scope switch
        {
            BattleConditionScope.Side => new(scope, slot.Side),
            BattleConditionScope.Slot => new(scope, slot.Side, slot),
            BattleConditionScope.Creature => new(scope, slot.Side, slot, PartyIndex(slot.Side, creature)),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported condition owner scope."),
        };
    }

    private BattleConditionSource ConditionSource(BattleSlot slot, BattleCreature creature) =>
        new(slot, PartyIndex(slot.Side, creature));

    private int PartyIndex(BattleSide side, BattleCreature creature)
    {
        int index = _parties[(int)side].IndexOf(creature);
        return index >= 0 ? index : throw new InvalidOperationException("Battle creature is not in its side party.");
    }

    private void ApplyGroundedState(EffectContext ctx, GroundedStateEffect effect)
    {
        BattleConditionDefinition definition = GroundedConditions.For(effect.State, effect.Scope);
        BattleConditionOwner owner = effect.Scope == GroundedStateScope.Field
            ? new BattleConditionOwner(BattleConditionScope.Field)
            : new BattleConditionOwner(BattleConditionScope.Creature, ctx.TargetSlot.Side, ctx.TargetSlot,
                ActiveIndex(ctx.TargetSlot));
        RecordConditionChanges(_conditions.Apply(new BattleConditionApplication(
            definition.Id,
            owner,
            new BattleConditionSource(ctx.SourceSlot, ActiveIndex(ctx.SourceSlot)),
            Turn,
            ctx.TraceAction,
            effect.Duration)));
    }

    private void ApplyOneShotQuery(EffectContext ctx, OneShotQueryEffect effect)
    {
        if (effect.Duration is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(effect), "One-shot query duration must be in 1..8.");
        BattleConditionDefinition definition = OneShotQueryConditions.For(effect.Query);
        BattleSlot ownerSlot = effect.Query == OneShotQuery.Accuracy ? ctx.TargetSlot : ctx.SourceSlot;
        BattleConditionOwner owner = new(BattleConditionScope.Creature, ownerSlot.Side, ownerSlot,
            ActiveIndex(ownerSlot));
        RecordConditionChanges(_conditions.Apply(new BattleConditionApplication(
            definition.Id, owner,
            new BattleConditionSource(ctx.SourceSlot, ActiveIndex(ctx.SourceSlot)),
            Turn, ctx.TraceAction, effect.Duration), definition));
    }

    private void ApplyFieldCondition(EffectContext ctx, SetFieldConditionEffect effect)
    {
        BattleConditionDefinition definition = FieldConditions.For(effect.Condition, Ruleset);
        BattleConditionOwner owner = definition.Scope == BattleConditionScope.Room
            ? FieldConditions.RoomOwner : FieldConditions.FieldOwner;
        bool toggles = effect.Condition is BattleFieldCondition.TrickRoom
            or BattleFieldCondition.WonderRoom or BattleFieldCondition.MagicRoom;
        if (toggles && FieldConditions.Active(ConditionSnapshot, effect.Condition))
        {
            RecordConditionChanges(_conditions.Remove(definition.Id, owner, Turn, ctx.TraceAction));
            return;
        }
        RecordConditionChanges(_conditions.Apply(new BattleConditionApplication(definition.Id, owner,
            new BattleConditionSource(ctx.SourceSlot, ActiveIndex(ctx.SourceSlot)), Turn, ctx.TraceAction,
            definition.DurationCheckpoint is null ? null : effect.Duration)));
    }

    private bool ApplySideCondition(EffectContext ctx, SetSideConditionEffect effect)
    {
        if (effect.Condition == BattleSideCondition.AllDamageScreen
            && (_ruleset != BattleRulesets.ModernReference || CurrentWeather != Weather.Snow))
        {
            _log.Add(new MoveFailed(ctx.SourceSlot, ctx.Move.Move, MoveFailureReason.ConditionRequirementNotMet));
            ctx.Action.MarkFailed();
            return false;
        }

        BattleSide ownerSide = effect.Side == SideConditionTarget.Source ? ctx.SourceSide : ctx.TargetSide;
        BattleConditionDefinition definition = SideConditions.For(effect.Condition);
        int extension = definition.Tags.Contains("screen") ? SideConditionDurationExtension(ctx.SourceSlot) : 0;
        BattleConditionChangeSet changes = _conditions.Apply(new BattleConditionApplication(
            definition.Id, SideConditions.Owner(ownerSide),
            new BattleConditionSource(ctx.SourceSlot, ActiveIndex(ctx.SourceSlot)), Turn, ctx.TraceAction,
            effect.Duration + extension));
        RecordConditionChanges(changes);
        if (!changes.Events.OfType<ConditionApplicationRejected>().Any())
            return true;

        _log.Add(new MoveFailed(ctx.SourceSlot, ctx.Move.Move, MoveFailureReason.ConditionAlreadyActive));
        ctx.Action.MarkFailed();
        return false;
    }

    private void ApplyBeforeDamageSideRemovals(BattleActionContext action,
        IReadOnlyList<BattleTargetContext> targets)
    {
        foreach (RemoveSideConditionEffect effect in action.Move.SecondaryEffects
            .OfType<RemoveSideConditionEffect>().Where(effect => effect.Timing == SideConditionTiming.BeforeDamage))
        {
            IEnumerable<BattleSide> sides = effect.Side == SideConditionTarget.Source
                ? [action.SourceSide]
                : targets.Select(target => target.TargetSide).Distinct();
            foreach (BattleSide side in sides)
                RemoveSideConditions(action, side, effect.Tag,
                    targets.FirstOrDefault(target => target.TargetSide == side));
        }
    }

    private void RemoveSideConditions(EffectContext ctx, RemoveSideConditionEffect effect) =>
        RemoveSideConditions(ctx.Action,
            effect.Side == SideConditionTarget.Source ? ctx.SourceSide : ctx.TargetSide,
            effect.Tag, ctx.TargetContext);

    private void RemoveSideConditions(BattleActionContext action, BattleSide side, string tag,
        BattleTargetContext? target)
    {
        int start = _log.Count;
        BattleConditionChangeSet changes = _conditions.RemoveTagged(BattleConditionScope.Side,
            SideConditions.Owner(side), tag, Turn, action.TraceAction);
        RecordConditionChanges(changes);
        AddTrace(action.TraceAction, action.SourceSlot, target?.TargetSlot, EffectTraceKind.ConditionRemoval,
            false, null, changes.Affected.Count, start, _log.Count);
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
            Heal(recipient, slot, amount, ctx.Move);
            return;
        }

        int calculated = amount;
        BattleDamageQueryResult? damageQuery = effect.Recipient == HpFractionRecipient.Target
            ? ResolveEffectDamageQuery(ctx) : null;
        double effectiveness = damageQuery is null
            ? 1.0 : TypeChart.ToDouble(damageQuery.Effectiveness.FinalValue);
        if (damageQuery is not null && effectiveness <= 0)
            amount = 0;
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
                effectiveness <= 0 ? BattleDamageFailure.Immune
                    : actual > 0 ? BattleDamageFailure.None : BattleDamageFailure.NoDamage,
                calculated, new DamageApplication(amount, actual), critical: false,
                effectiveType: damageQuery!.Identity.EffectiveType,
                effectiveClass: damageQuery.Identity.EffectiveClass);
        }
    }

    private void ApplyHpEqualize(EffectContext ctx, HpEqualizeEffect effect)
    {
        int eventStart = _log.Count;
        int sourceBefore = ctx.Source.CurrentHp, targetBefore = ctx.Target.CurrentHp;
        if (effect.Mode == HpEqualizeMode.MatchSource)
        {
            BattleDamageQueryResult damageQuery = ResolveEffectDamageQuery(ctx);
            double effectiveness = TypeChart.ToDouble(damageQuery.Effectiveness.FinalValue);
            if (targetBefore <= sourceBefore || effectiveness <= 0)
            {
                BattleHistoryOwner targetOwner = HistoryOwner(ctx.TargetSlot);
                RecordDamage(new BattleActionAttemptId(Turn, ctx.TraceAction), HistoryOwner(ctx.SourceSlot),
                    targetOwner, ctx.Move, BattleDamageCause.HpFormula,
                    NextDamageHitNumber(ctx.TraceAction, targetOwner), true,
                    effectiveness <= 0 ? BattleDamageFailure.Immune : BattleDamageFailure.NoDamage,
                    0, default, critical: false, effectiveType: damageQuery.Identity.EffectiveType,
                    effectiveClass: damageQuery.Identity.EffectiveClass);
                AddTrace(ctx.TraceAction, ctx.SourceSlot, ctx.TargetSlot, EffectTraceKind.HpFormula, false,
                    null, targetBefore, eventStart, eventStart);
                return;
            }
            int calculated = targetBefore - sourceBefore;
            DamageApplication applied = DealMoveDamage(ctx.Target, ctx.TargetSlot, calculated, effectiveness,
                crit: false);
            ctx.Target.RecordDamageTaken(damageQuery.Identity.EffectiveClass, applied.ActualHpRemoved);
            ctx.TargetContext!.AddDamage(ctx.Action, applied.ActualHpRemoved);
            BattleHistoryOwner target = HistoryOwner(ctx.TargetSlot);
            RecordDamage(new BattleActionAttemptId(Turn, ctx.TraceAction), HistoryOwner(ctx.SourceSlot), target,
                ctx.Move, BattleDamageCause.HpFormula, NextDamageHitNumber(ctx.TraceAction, target), true,
                applied.ActualHpRemoved > 0 ? BattleDamageFailure.None : BattleDamageFailure.NoDamage,
                calculated, applied, critical: false, effectiveType: damageQuery.Identity.EffectiveType,
                effectiveClass: damageQuery.Identity.EffectiveClass);
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

    private BattleDamageQueryResult ResolveEffectDamageQuery(EffectContext ctx)
    {
        BattleMoveIdentityQueryResult identity = EffectiveMoveIdentity(ctx.SourceSlot, ctx.Move, ctx.TraceAction);
        return ResolveDamageQuery(ctx.SourceSlot, ctx.TargetSlot, ctx.Source, ctx.Target, ctx.Move,
            identity, EffectiveValues(ctx.SourceSlot), EffectiveValues(ctx.TargetSlot),
            Math.Max(1, ctx.Action.Targets.Count), ctx.TraceAction);
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

    private bool PassesMoveGates(BattleCreature creature, BattleSlot slot, BattleMove move,
        int traceAction, MoveGateTiming timing, BattleSlot? targetSlot)
    {
        if (timing == MoveGateTiming.BeforeMove)
        {
            foreach (ItemRequireEffect requirement in move.SecondaryEffects.OfType<ItemRequireEffect>())
            {
                BattleSlot requirementSlot = requirement.Subject == BattleItemSubject.User
                    ? slot : targetSlot ?? new BattleSlot(Opponent(slot.Side), 0);
                bool passed = IsLive(requirementSlot) && _items.Meets(OverlayOwner(requirementSlot),
                    PhysicalMetricFormulas.BaseEffectiveValues(Active(requirementSlot)), requirement.Requirement);
                int start = _log.Count;
                if (!passed)
                    _log.Add(new MoveFailed(slot, move.Move, MoveFailureReason.ItemRequirementNotMet));
                AddTrace(traceAction, slot, requirement.Subject == BattleItemSubject.Target ? requirementSlot : null,
                    EffectTraceKind.MoveGate, false, null, passed ? 1 : 0, start, _log.Count);
                if (!passed)
                    return false;
            }
            foreach (FieldMoveGateEffect gate in move.SecondaryEffects.OfType<FieldMoveGateEffect>())
            {
                bool passed = !FieldConditions.Active(ConditionSnapshot, gate.Condition);
                int start = _log.Count;
                if (!passed)
                    _log.Add(new MoveFailed(slot, move.Move, MoveFailureReason.FieldConditionBlocked));
                AddTrace(traceAction, slot, null, EffectTraceKind.MoveGate, false, null, passed ? 1 : 0,
                    start, _log.Count);
                if (!passed)
                    return false;
            }
            if (move.SecondaryEffects.OfType<TerrainGateEffect>().Any())
            {
                bool passed = CurrentTerrain != Terrain.None;
                int start = _log.Count;
                if (!passed)
                    _log.Add(new MoveFailed(slot, move.Move, MoveFailureReason.TerrainRequired));
                AddTrace(traceAction, slot, null, EffectTraceKind.MoveGate, false, null, passed ? 1 : 0,
                    start, _log.Count);
                if (!passed)
                    return false;
            }
        }

        BattleHistoryOwner sourceOwner = HistoryOwner(slot);
        BattleActionFormulaInputs? history = targetSlot is { } target
            ? _actionHistory.PowerInputs(sourceOwner, HistoryOwner(target), move.Move) : null;
        foreach (MoveGateEffect gate in move.SecondaryEffects.OfType<MoveGateEffect>()
            .Where(gate => gate.Timing == timing))
        {
            bool matchingDamage = gate.Kind == MoveGateKind.DamageReceived
                && _actionHistory.DamageTo(sourceOwner, Turn).Any(record => record.ActualHpRemoved > 0
                    && (gate.DamageClass is null || record.DamageClass == gate.DamageClass));
            MoveFailureReason? failure = BattleActionGates.Failure(move, creature, gate,
                new BattleMoveGateInputs(
                    _actionHistory.PreviousActionFailed(sourceOwner),
                    history?.SourceBeforeTarget ?? false,
                    history?.SourceAfterTarget ?? false,
                    targetSlot is { } plannedTarget
                        ? _actionHistory.PlannedMoveClass(HistoryOwner(plannedTarget)) : null,
                    matchingDamage));
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

    private BattleSlot? GateTarget(BattleSlot source, BattleMove move,
        BattleActionSelection? selection)
    {
        bool needsTarget = move.SecondaryEffects.OfType<MoveGateEffect>().Any(gate => gate.Kind is
                MoveGateKind.SourceBeforeTarget or MoveGateKind.SourceAfterTarget
                or MoveGateKind.TargetAction)
            || move.SecondaryEffects.OfType<ItemRequireEffect>().Any(requirement =>
                requirement.Subject == BattleItemSubject.Target);
        if (!needsTarget)
            return null;
        if (Topology.ActiveSlotsPerSide == 1)
            return new BattleSlot(Opponent(source.Side), 0);
        return selection is ActiveSlotSelection active
            ? active.Slot
            : throw new ArgumentException("Target-relative action gates require an active-slot selection.",
                nameof(selection));
    }

    private void QueueActionGate(EffectContext ctx, QueueActionGateEffect gate)
    {
        if (gate.Turns <= 0 || !Enum.IsDefined(gate.Owner))
            throw new ArgumentOutOfRangeException(nameof(gate),
                "Queued action gates require positive turns and a defined owner.");
        if (Outcome is not null)
            return;
        BattleIntentOwner owner = gate.Owner switch
        {
            QueueActionGateOwner.Slot => new BattleIntentOwner(BattleIntentOwnerScope.Slot,
                ctx.SourceSide, ctx.SourceSlot, null, BattleIntentSwitchPolicy.StaySlot,
                BattleIntentFaintPolicy.Persist),
            QueueActionGateOwner.Creature => new BattleIntentOwner(BattleIntentOwnerScope.Creature,
                ctx.SourceSide, ctx.SourceSlot, ActiveIndex(ctx.SourceSlot),
                BattleIntentSwitchPolicy.Cancel, BattleIntentFaintPolicy.Cancel),
            _ => throw new ArgumentOutOfRangeException(nameof(gate), gate.Owner,
                "Unknown queued action-gate owner."),
        };
        BattleIntent intent = _intentQueue.Enqueue(new BattleIntentRequest(
            Turn + gate.Turns,
            BattleIntentCheckpoint.PreAction,
            owner,
            new BattleIntentTarget(BattleIntentTargetPolicy.Source),
            new SkipActionIntent(),
            ctx.Move.Move,
            ctx.TraceAction,
            Ruleset));
        AddIntentTrace(intent, EffectTraceKind.IntentEnqueued, true, _log.Count, _log.Count);
    }

    private bool QueueDelayedDamage(BattleActionContext action, BattleTargetContext target,
        DelayedDamageEffect effect)
    {
        if (effect.UniquePerSlot && IntentQueueSnapshot.Any(intent =>
                intent.Payload == BattleIntentPayloadKind.DelayedDamage
                && intent.TargetSlot == target.TargetSlot))
        {
            _log.Add(new DelayedActionFailed(target.TargetSlot, action.Move.Move,
                BattleIntentPayloadKind.DelayedDamage, DelayedActionFailureReason.SlotOccupied));
            action.MarkFailed();
            return false;
        }

        DelayedDamageSnapshot snapshot = SnapshotDelayedDamage(action, target);
        BattleIntent intent = EnqueueDelayed(action, target.TargetSlot, effect.Turns,
            BattleIntentTargetPolicy.LiveSlot,
            new DelayedDamageIntent(snapshot, action.SourceSlot, ActiveIndex(action.SourceSlot),
                effect.SourceRequired));
        RecordDelayedEnqueue(intent, action.SourceSlot, target.TargetSlot);
        return true;
    }

    private DelayedDamageSnapshot SnapshotDelayedDamage(BattleActionContext action,
        BattleTargetContext target)
    {
        BattleMove move = action.Move;
        BattleMoveIdentityQueryResult identity = EffectiveMoveIdentity(action.SourceSlot, move,
            action.TraceAction);
        BattleEffectiveValues sourceValues = EffectiveValues(action.SourceSlot);
        StatKind offensiveStat = identity.EffectiveClass == DamageClass.Physical
            ? StatKind.Atk : StatKind.Spa;
        var context = new BattleQueryContext(action.SourceSlot, action.Source,
            target.TargetSlot, target.Target, CurrentWeather, Ruleset, CurrentTerrain);
        BattleQueryResult offensive = BattleQuery.Evaluate(BattleQueryId.OffensiveStat,
            new BattleQueryValue(StatValue(sourceValues.Stats, offensiveStat)),
            [
                new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply,
                    BattleQuery.StatStageMultiplier(action.Source.Stage(offensiveStat)), InsertionOrder: 0),
                .. StatHookModifiers(action.SourceSlot, target.TargetSlot, action.SourceSlot,
                    offensiveStat, BattleQueryId.OffensiveStat),
            ], context);
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, action.TraceAction,
            action.SourceSlot, target.TargetSlot, offensive));

        BattleHookDispatchSnapshot field = FieldConditions.CollectBasePowerHooks(
            ConditionSnapshot, identity.EffectiveType.Slug, Ruleset, action.TraceAction);
        _hookTrace.AddRange(field.Trace);
        BattleQueryResult power = BattleQuery.Evaluate(BattleQueryId.BasePower,
            new BattleQueryValue(move.Power!.Value), field.QueryModifiers(BattleQueryId.BasePower), context);
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, action.TraceAction,
            action.SourceSlot, target.TargetSlot, power));

        return new DelayedDamageSnapshot(action.Source.Level, power.FinalValue.ToInt32(),
            offensive.FinalValue.ToInt32(), identity.EffectiveType, identity.EffectiveClass,
            sourceValues.CreatureTypes.Contains(identity.EffectiveType),
            action.Source.Status == PersistentStatus.Burn && identity.EffectiveClass == DamageClass.Physical,
            IsGrounded(action.SourceSlot), BattleActionQueries.MoveModifiers(move, BattleQueryId.FinalDamage));
    }

    private bool QueueDelayedHeal(EffectContext ctx, DelayedHealEffect effect)
    {
        int basis = effect.Basis == DelayedHealBasis.SourceMaxHp ? ctx.Source.MaxHp : ctx.Target.MaxHp;
        int amount = EffectMath.HealAmount(basis, effect.Fraction.Num, effect.Fraction.Den);
        BattleIntent intent = EnqueueDelayed(ctx.Action, ctx.TargetSlot, effect.Turns,
            effect.TargetPolicy, new DelayedHealIntent(amount, ctx.SourceSlot,
                ActiveIndex(ctx.SourceSlot), effect.SourceRequired,
                BattleActionQueries.MoveModifiers(ctx.Move, BattleQueryId.Healing)));
        RecordDelayedEnqueue(intent, ctx.SourceSlot, ctx.TargetSlot);
        return true;
    }

    private bool QueueDelayedStatus(EffectContext ctx, DelayedStatusEffect effect)
    {
        BattleIntent intent = EnqueueDelayed(ctx.Action, ctx.TargetSlot, effect.Turns,
            effect.TargetPolicy, new DelayedStatusIntent(effect.Status, ctx.SourceSlot,
                ActiveIndex(ctx.SourceSlot), effect.SourceRequired));
        RecordDelayedEnqueue(intent, ctx.SourceSlot, ctx.TargetSlot);
        return true;
    }

    private bool QueueReplacementRestore(EffectContext ctx, ReplacementRestoreEffect effect)
    {
        bool hasReserve = _parties[(int)ctx.SourceSide]
            .Select((creature, index) => (creature, index))
            .Any(candidate => !candidate.creature.IsFainted
                && !_activeSlots.IsActive(ctx.SourceSide, candidate.index));
        if (!hasReserve)
        {
            _log.Add(new DelayedActionFailed(ctx.SourceSlot, ctx.Move.Move,
                BattleIntentPayloadKind.ReplacementRestore, DelayedActionFailureReason.NoReserve));
            ctx.Action.MarkFailed();
            return false;
        }

        BattleIntent intent = _intentQueue.Enqueue(new BattleIntentRequest(
            Turn,
            BattleIntentCheckpoint.SwitchIn,
            new BattleIntentOwner(BattleIntentOwnerScope.Slot, ctx.SourceSide, ctx.SourceSlot,
                null, BattleIntentSwitchPolicy.StaySlot, BattleIntentFaintPolicy.Persist),
            new BattleIntentTarget(BattleIntentTargetPolicy.LiveSlot, ctx.SourceSlot,
                Side: ctx.SourceSide),
            new ReplacementRestoreIntent(effect.RestoreHp, effect.CureStatus, effect.RestorePp),
            ctx.Move.Move,
            ctx.TraceAction,
            Ruleset));
        RecordDelayedEnqueue(intent, ctx.SourceSlot, ctx.SourceSlot);
        return true;
    }

    private BattleIntent EnqueueDelayed(BattleActionContext action, BattleSlot targetSlot, int turns,
        BattleIntentTargetPolicy policy, BattleIntentPayload payload)
    {
        BattleIntentTarget target = policy == BattleIntentTargetPolicy.SnapshotSlot
            ? new BattleIntentTarget(policy, targetSlot, ActiveIndex(targetSlot), targetSlot.Side)
            : new BattleIntentTarget(policy, targetSlot, Side: targetSlot.Side);
        return _intentQueue.Enqueue(new BattleIntentRequest(
            Turn + turns,
            BattleIntentCheckpoint.TurnEnd,
            new BattleIntentOwner(BattleIntentOwnerScope.Field, action.SourceSide),
            target,
            payload,
            action.Move.Move,
            action.TraceAction,
            Ruleset));
    }

    private void RecordDelayedEnqueue(BattleIntent intent, BattleSlot source, BattleSlot target)
    {
        int start = _log.Count;
        _log.Add(new DelayedActionQueued(source, target, intent.SourceMove,
            intent.Payload.Kind, intent.DueTurn));
        AddIntentTrace(intent, EffectTraceKind.IntentEnqueued, true, start, _log.Count);
        AddTrace(intent.SourceActionSequence, source, target, EffectTraceKind.DelayedAction,
            false, null, 1, start, _log.Count);
    }

    private void ResolveTurnEndIntents()
    {
        BattleIntentPreview preview = _intentQueue.Preview(Turn, BattleIntentCheckpoint.TurnEnd);
        IReadOnlyList<BattleIntent> consumed = _intentQueue.Consume(preview);
        foreach (BattleIntent intent in consumed)
        {
            int start = _log.Count;
            int traceAction = ++_traceActionSequence;
            switch (intent.Payload)
            {
                case DelayedDamageIntent damage:
                    ResolveDelayedDamage(intent, damage, traceAction);
                    break;
                case DelayedHealIntent heal:
                    ResolveDelayedHeal(intent, heal, traceAction);
                    break;
                case DelayedStatusIntent status:
                    ResolveDelayedStatus(intent, status, traceAction);
                    break;
                default:
                    throw new InvalidOperationException($"Payload '{intent.Payload.Kind}' cannot run at TurnEnd.");
            }
            AddIntentTrace(intent, EffectTraceKind.IntentConsumed, true, start, _log.Count);
            AddTrace(traceAction, intent.Owner.LastKnownSlot ?? new BattleSlot(intent.Owner.Side, 0),
                intent.Target.Slot, EffectTraceKind.DelayedAction, false, null, 1, start, _log.Count);
        }
        foreach (BattleIntent intent in _intentQueue.Complete(preview))
            AddIntentTrace(intent, EffectTraceKind.IntentDeferred, false, _log.Count, _log.Count);
    }

    private void ResolveDelayedDamage(BattleIntent intent, DelayedDamageIntent payload,
        int traceAction)
    {
        BattleIntentResolvedTarget? resolved = ResolveIntentTarget(intent);
        if (resolved?.Slot is not { } targetSlot)
        {
            DelayedFailure(intent, null, DelayedActionFailureReason.TargetUnavailable);
            return;
        }
        if (!SourceAvailable(payload.SourceSlot, payload.SourcePartyIndex, payload.SourceRequired))
        {
            DelayedFailure(intent, targetSlot, DelayedActionFailureReason.SourceUnavailable);
            return;
        }

        BattleCreature target = Active(targetSlot);
        DelayedDamageSnapshot snapshot = payload.Snapshot;
        var context = new BattleQueryContext(payload.SourceSlot, null, targetSlot, target,
            CurrentWeather, intent.Ruleset, CurrentTerrain);
        BattleQueryValue authoredEffectiveness = new(1);
        foreach (EntityId targetType in EffectiveTypes(targetSlot))
            authoredEffectiveness = TypeChart.Multiply(authoredEffectiveness,
                _chart.SingleValue(snapshot.MoveType, targetType));
        BattleQueryResult effectivenessResult = BattleQuery.Evaluate(BattleQueryId.Effectiveness,
            authoredEffectiveness, null, context);
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction,
            payload.SourceSlot, targetSlot, effectivenessResult));
        double effectiveness = TypeChart.ToDouble(effectivenessResult.FinalValue);
        BattleHistoryOwner sourceOwner = new(payload.SourceSlot.Side, payload.SourcePartyIndex,
            payload.SourceSlot);
        BattleHistoryOwner targetOwner = HistoryOwner(targetSlot);
        BattleActionAttemptId attempt = _actionHistory.BeginMove(traceAction, sourceOwner,
            intent.SourceMove);
        _actionHistory.MarkStarted(attempt);
        if (effectiveness <= 0)
        {
            DamageApplication immune = DealMoveDamage(target, targetSlot, 0, effectiveness,
                crit: false, applySurvival: false);
            RecordDelayedDamage(intent, attempt, sourceOwner, targetOwner, payload,
                0, immune, BattleDamageFailure.Immune);
            _actionHistory.Complete(attempt, BattleActionResult.Failed, [targetOwner]);
            DelayedFailure(intent, targetSlot, DelayedActionFailureReason.Immune);
            return;
        }

        StatKind defensiveStat = snapshot.DamageClass == DamageClass.Physical
            ? StatKind.Def : StatKind.Spd;
        defensiveStat = FieldConditions.DefensiveStat(ConditionSnapshot, defensiveStat);
        BattleEffectiveValues targetValues = EffectiveValues(targetSlot);
        BattleQueryResult defense = BattleQuery.Evaluate(BattleQueryId.DefensiveStat,
            new BattleQueryValue(StatValue(targetValues.Stats, defensiveStat)),
            [
                new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply,
                    BattleQuery.StatStageMultiplier(target.Stage(defensiveStat)), InsertionOrder: 0),
                .. StatHookModifiers(payload.SourceSlot, targetSlot, targetSlot,
                    defensiveStat, BattleQueryId.DefensiveStat),
            ], context);
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction,
            payload.SourceSlot, targetSlot, defense));
        int roll = BattleRolls.DamageRoll(_rng, out int damageRollDraw);
        AddTrace(traceAction, payload.SourceSlot, targetSlot, EffectTraceKind.DamageRoll,
            true, damageRollDraw, roll, _log.Count, _log.Count, 16);
        int calculated = DamageCalc.Compute(snapshot.Level, snapshot.Power, snapshot.OffensiveStat,
            defense.FinalValue.ToInt32(), effectiveness, snapshot.Stab ? 1.5 : 1,
            crit: false, roll, snapshot.Burn);
        IReadOnlyList<BattleQueryModifier> hooks = DelayedDamageModifiers(snapshot, targetSlot,
            traceAction);
        BattleQueryResult finalDamage = BattleQuery.Evaluate(BattleQueryId.FinalDamage,
            new BattleQueryValue(calculated), [.. snapshot.FinalDamageModifiers, .. hooks], context);
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction,
            payload.SourceSlot, targetSlot, finalDamage));
        calculated = finalDamage.FinalValue.ToInt32();
        DamageApplication applied = DealMoveDamage(target, targetSlot, calculated, effectiveness,
            crit: false, applySurvival: false);
        target.RecordDamageTaken(snapshot.DamageClass, applied.ActualHpRemoved);
        RecordDelayedDamage(intent, attempt, sourceOwner, targetOwner, payload, calculated,
            applied, applied.ActualHpRemoved > 0 ? BattleDamageFailure.None : BattleDamageFailure.NoDamage);
        _actionHistory.Complete(attempt, applied.ActualHpRemoved > 0
            ? BattleActionResult.Connected : BattleActionResult.Failed, [targetOwner]);
        _log.Add(new DelayedActionResolved(targetSlot, intent.SourceMove, intent.Payload.Kind));
    }

    private IReadOnlyList<BattleQueryModifier> DelayedDamageModifiers(
        DelayedDamageSnapshot snapshot, BattleSlot targetSlot, int traceAction)
    {
        var modifiers = new List<BattleQueryModifier>();
        BattleHookDispatchSnapshot weather = WeatherConditions.CollectDamageHooks(
            ConditionSnapshot, snapshot.MoveType.Slug, traceAction);
        _hookTrace.AddRange(weather.Trace);
        Append(weather.QueryModifiers(BattleQueryId.FinalDamage));
        BattleHookDispatchSnapshot terrain = TerrainConditions.CollectDamageHooks(
            ConditionSnapshot, snapshot.MoveType.Slug, snapshot.SourceGrounded,
            IsGrounded(targetSlot), traceAction);
        _hookTrace.AddRange(terrain.Trace);
        Append(terrain.QueryModifiers(BattleQueryId.FinalDamage));
        BattleHookDispatchSnapshot side = SideConditions.CollectDamageHooks(ConditionSnapshot,
            targetSlot.Side, snapshot.DamageClass, Topology.ActiveSlotsPerSide, critical: false,
            bypass: false, traceAction);
        _hookTrace.AddRange(side.Trace);
        Append(side.QueryModifiers(BattleQueryId.FinalDamage));
        return modifiers;

        void Append(IEnumerable<BattleQueryModifier> incoming)
        {
            foreach (BattleQueryModifier modifier in incoming)
                modifiers.Add(modifier with { InsertionOrder = modifiers.Count });
        }
    }

    private void RecordDelayedDamage(BattleIntent intent, BattleActionAttemptId attempt,
        BattleHistoryOwner source, BattleHistoryOwner target, DelayedDamageIntent payload,
        int calculated, DamageApplication damage, BattleDamageFailure failure)
    {
        bool connected = damage.ActualHpRemoved > 0;
        _actionHistory.RecordDamage(new BattleDamageRecord(
            attempt, source, target, intent.SourceMove,
            payload.Snapshot.DamageClass, payload.Snapshot.MoveType, BattleDamageCause.Standard,
            1, true, connected, failure, calculated, damage.AppliedDamage, damage.ActualHpRemoved,
            false, false, false, connected && Active(target.Slot).IsFainted));
    }

    private void ResolveDelayedHeal(BattleIntent intent, DelayedHealIntent payload, int traceAction)
    {
        BattleIntentResolvedTarget? resolved = ResolveIntentTarget(intent);
        if (resolved?.Slot is not { } targetSlot)
        {
            DelayedFailure(intent, null, DelayedActionFailureReason.TargetUnavailable);
            return;
        }
        if (!SourceAvailable(payload.SourceSlot, payload.SourcePartyIndex, payload.SourceRequired))
        {
            DelayedFailure(intent, targetSlot, DelayedActionFailureReason.SourceUnavailable);
            return;
        }

        BattleCreature target = Active(targetSlot);
        var context = new BattleQueryContext(payload.SourceSlot, null, targetSlot, target,
            CurrentWeather, intent.Ruleset, CurrentTerrain);
        BattleQueryResult healing = BattleQuery.Evaluate(BattleQueryId.Healing,
            new BattleQueryValue(payload.Amount), payload.Modifiers, context);
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction,
            payload.SourceSlot, targetSlot, healing));
        int amount = healing.FinalValue.ToInt32();
        if (amount <= 0)
        {
            DelayedFailure(intent, targetSlot, DelayedActionFailureReason.HealingBlocked);
            return;
        }
        int before = target.CurrentHp;
        target.Heal(amount);
        if (target.CurrentHp > before)
            _log.Add(new Healed(targetSlot, target.CurrentHp - before));
        _log.Add(new DelayedActionResolved(targetSlot, intent.SourceMove, intent.Payload.Kind));
    }

    private void ResolveDelayedStatus(BattleIntent intent, DelayedStatusIntent payload,
        int traceAction)
    {
        BattleIntentResolvedTarget? resolved = ResolveIntentTarget(intent);
        if (resolved?.Slot is not { } targetSlot)
        {
            DelayedFailure(intent, null, DelayedActionFailureReason.TargetUnavailable);
            return;
        }
        if (!SourceAvailable(payload.SourceSlot, payload.SourcePartyIndex, payload.SourceRequired))
        {
            DelayedFailure(intent, targetSlot, DelayedActionFailureReason.SourceUnavailable);
            return;
        }

        BattleCreature target = Active(targetSlot);
        bool eligible = StatusEffects.CanApplyStatus(target.Status)
            && !StatusEffects.TypeImmuneToStatus(payload.Status, EffectiveTypes(targetSlot))
            && !BlocksStatus(targetSlot, payload.Status);
        if (eligible)
        {
            BattleHookDispatchSnapshot weather = WeatherConditions.CollectStatusHooks(
                ConditionSnapshot, payload.Status, traceAction);
            _hookTrace.AddRange(weather.Trace);
            eligible = !weather.Filters().Any(filter => filter is
                { Filter.Value: "status_attempt", Decision: BattleHookFilterDecision.Deny });
        }
        if (eligible)
        {
            BattleHookDispatchSnapshot terrain = TerrainConditions.CollectStatusHooks(
                ConditionSnapshot, payload.Status, IsGrounded(targetSlot), traceAction);
            _hookTrace.AddRange(terrain.Trace);
            eligible = !terrain.Filters().Any(filter => filter is
                { Filter.Value: "status_attempt", Decision: BattleHookFilterDecision.Deny });
        }
        if (eligible)
            eligible = AllowsSideStatus(payload.SourceSlot, targetSlot, null,
                confusion: false, traceAction);
        if (!eligible)
        {
            DelayedFailure(intent, targetSlot, DelayedActionFailureReason.StatusBlocked);
            return;
        }
        target.SetStatus(payload.Status);
        _log.Add(new StatusApplied(targetSlot, payload.Status));
        _log.Add(new DelayedActionResolved(targetSlot, intent.SourceMove, intent.Payload.Kind));
    }

    private void ResolveSwitchInIntents(BattleSlot slot)
    {
        BattleIntentPreview preview = _intentQueue.PreviewTarget(Turn,
            BattleIntentCheckpoint.SwitchIn, slot);
        var consumedSequences = new HashSet<long>();
        var eventRanges = new Dictionary<long, (int Start, int End)>();
        foreach (BattleIntent intent in preview.Entries)
        {
            if (intent.Payload is not ReplacementRestoreIntent restore)
                throw new InvalidOperationException($"Payload '{intent.Payload.Kind}' cannot run at SwitchIn.");
            BattleCreature target = Active(slot);
            bool needsRestore = !target.IsFainted
                && (restore.RestoreHp && target.CurrentHp < target.MaxHp
                    || restore.CureStatus && target.Status is not null
                    || restore.RestorePp && target.Moves.Any(move => move.Pp < move.MaxPp));
            if (!needsRestore)
            {
                AddIntentTrace(intent, EffectTraceKind.IntentDeferred, false, _log.Count, _log.Count);
                continue;
            }

            int start = _log.Count;
            if (restore.RestoreHp && target.CurrentHp < target.MaxHp)
            {
                int before = target.CurrentHp;
                target.Heal(target.MaxHp);
                _log.Add(new Healed(slot, target.CurrentHp - before));
            }
            if (restore.CureStatus && target.Status is { } status)
            {
                target.ClearStatus();
                _log.Add(new StatusCured(slot, status));
            }
            if (restore.RestorePp)
            {
                int restored = target.Moves.Sum(move => move.RestorePp());
                if (restored > 0)
                    _log.Add(new PpRestored(slot, restored));
            }
            _log.Add(new DelayedActionResolved(slot, intent.SourceMove, intent.Payload.Kind));
            consumedSequences.Add(intent.Sequence);
            eventRanges.Add(intent.Sequence, (start, _log.Count));
        }

        foreach (BattleIntent intent in _intentQueue.Consume(preview, consumedSequences))
        {
            (int Start, int End) range = eventRanges[intent.Sequence];
            AddIntentTrace(intent, EffectTraceKind.IntentConsumed, true, range.Start, range.End);
            AddTrace(++_traceActionSequence, slot, slot, EffectTraceKind.DelayedAction,
                false, null, 1, range.Start, range.End);
        }
        foreach (BattleIntent intent in _intentQueue.Complete(preview))
            AddIntentTrace(intent, EffectTraceKind.IntentDeferred, false, _log.Count, _log.Count);
    }

    private BattleIntentResolvedTarget? ResolveIntentTarget(BattleIntent intent) =>
        _intentQueue.ResolveTarget(intent,
            slot => Topology.Contains(slot) && !Active(slot).IsFainted ? ActiveIndex(slot) : null,
            (side, partyIndex) => Topology.SlotsFor(side)
                .Where(slot => ActiveIndex(slot) == partyIndex && !Active(slot).IsFainted)
                .Select(slot => (BattleSlot?)slot)
                .FirstOrDefault());

    private bool SourceAvailable(BattleSlot sourceSlot, int sourcePartyIndex, bool required) =>
        !required || Topology.SlotsFor(sourceSlot.Side)
            .Any(slot => ActiveIndex(slot) == sourcePartyIndex && !Active(slot).IsFainted);

    private void DelayedFailure(BattleIntent intent, BattleSlot? target,
        DelayedActionFailureReason reason) =>
        _log.Add(new DelayedActionFailed(target, intent.SourceMove, intent.Payload.Kind, reason));

    /// <summary>Runs permanent side-owned entry hazards in condition sequence after ordinary switch-in hooks.
    /// The batch draws no RNG; a faint stops later hazards for that creature but does not interrupt another slot.</summary>
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
        TriggerTerrainSeed(slot);

        foreach (BattleConditionInstance condition in EntryHazardConditions.Active(ConditionSnapshot, side))
        {
            if (c.IsFainted)
                break;
            TriggerEntryHazard(slot, condition);
        }
    }

    private bool ApplyEntryHazard(EffectContext context, EntryHazardProfile profile)
    {
        BattleConditionDefinition definition = EntryHazardConditions.Definition(profile);
        BattleConditionChangeSet changes = _conditions.Apply(new BattleConditionApplication(
            definition.Id, SideConditions.Owner(context.TargetSide),
            new BattleConditionSource(context.SourceSlot, ActiveIndex(context.SourceSlot)),
            Turn, context.TraceAction), definition);
        RecordConditionChanges(changes);
        if (changes.Events.OfType<ConditionApplicationRejected>().Any())
        {
            _log.Add(new MoveFailed(context.SourceSlot, context.Move.Move, MoveFailureReason.ConditionAlreadyActive));
            context.Action.MarkFailed();
            return false;
        }

        BattleConditionInstance instance = changes.Affected.Single();
        _log.Add(new EntryHazardSet(context.TargetSide, definition.Id, instance.StackCount, instance.Source));
        return true;
    }

    private void TriggerEntryHazard(BattleSlot slot, BattleConditionInstance condition)
    {
        EntryHazardProfile profile = condition.Definition.EntryHazard
            ?? throw new InvalidOperationException("Entry-hazard hook requires an entry-hazard profile.");
        BattleCreature creature = Active(slot);
        int start = _log.Count;
        BattleSlot sourceSlot = condition.Source.Slot ?? new BattleSlot(Opponent(slot.Side), 0);
        bool grounded = !profile.GroundedOnly || IsGrounded(slot);
        int value = 0;
        bool performed = false;

        if (grounded && profile.AbsorbTypes.Overlaps(EffectiveTypes(slot)))
        {
            RecordConditionChanges(_conditions.Remove(condition.Definition.Id, condition.Owner,
                Turn, _traceActionSequence));
            _log.Add(new EntryHazardAbsorbed(slot, condition.Definition.Id, condition.Source));
            performed = true;
        }
        else if (grounded)
        {
            switch (profile.Kind)
            {
                case EntryHazardKind.Damage:
                    double effectiveness = profile.DamageType is { } type
                        ? _chart.Effectiveness(type, EffectiveTypes(slot)) : 1;
                    value = EntryHazardConditions.Damage(profile, condition.StackCount, creature.MaxHp, effectiveness);
                    if (value > 0)
                    {
                        Sap(creature, slot, value, amount => new EntryHazardTriggered(slot,
                            condition.Definition.Id, condition.Source, profile.Kind, amount));
                        performed = true;
                    }
                    else
                    {
                        _log.Add(new EntryHazardTriggered(slot, condition.Definition.Id,
                            condition.Source, profile.Kind, 0));
                    }
                    break;
                case EntryHazardKind.Status:
                    PersistentStatus status = EntryHazardConditions.StatusFor(profile, condition.StackCount);
                    if (CanApplyEntryHazardStatus(sourceSlot, slot, status))
                    {
                        creature.SetStatus(status);
                        _log.Add(new StatusApplied(slot, status));
                        value = (int)status;
                        performed = true;
                    }
                    _log.Add(new EntryHazardTriggered(slot, condition.Definition.Id,
                        condition.Source, profile.Kind, performed ? value : 0));
                    break;
                case EntryHazardKind.Stage:
                    StatKind stat = profile.Stat!.Value;
                    if (profile.StageDelta >= 0 || AllowsEntryHazardStageDrop(sourceSlot.Side, slot.Side))
                    {
                        int before = creature.Stage(stat);
                        creature.ChangeStage(stat, profile.StageDelta);
                        value = creature.Stage(stat) - before;
                        if (value != 0)
                            _log.Add(new StatStageChanged(slot, stat, value));
                        performed = value != 0;
                    }
                    _log.Add(new EntryHazardTriggered(slot, condition.Definition.Id,
                        condition.Source, profile.Kind, value));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(profile), profile.Kind, "Unknown entry-hazard kind.");
            }
        }

        _trace.Add(new EffectTraceEntry(Turn, _traceActionSequence, sourceSlot, slot,
            EffectTraceKind.EntryHazard, performed, null, value, start, _log.Count)
        {
            Condition = condition.Definition.Id,
        });
    }

    private bool CanApplyEntryHazardStatus(BattleSlot sourceSlot, BattleSlot targetSlot, PersistentStatus status)
    {
        BattleCreature target = Active(targetSlot);
        bool eligible = StatusEffects.CanApplyStatus(target.Status)
            && !StatusEffects.TypeImmuneToStatus(status, EffectiveTypes(targetSlot))
            && !BlocksStatus(targetSlot, status);
        if (!eligible)
            return false;
        BattleHookDispatchSnapshot weather = WeatherConditions.CollectStatusHooks(
            ConditionSnapshot, status, _traceActionSequence);
        _hookTrace.AddRange(weather.Trace);
        if (weather.Filters().Any(filter => filter is
            { Filter.Value: "status_attempt", Decision: BattleHookFilterDecision.Deny }))
            return false;
        BattleHookDispatchSnapshot terrain = TerrainConditions.CollectStatusHooks(
            ConditionSnapshot, status, IsGrounded(targetSlot), _traceActionSequence);
        _hookTrace.AddRange(terrain.Trace);
        if (terrain.Filters().Any(filter => filter is
            { Filter.Value: "status_attempt", Decision: BattleHookFilterDecision.Deny }))
            return false;
        BattleHookDispatchSnapshot side = SideConditions.CollectStatusHooks(ConditionSnapshot,
            sourceSlot.Side, targetSlot.Side, bypass: false, actionSequence: _traceActionSequence);
        _hookTrace.AddRange(side.Trace);
        return !side.Filters().Any(item => item is
            { Filter.Value: "status_attempt", Decision: BattleHookFilterDecision.Deny });
    }

    private bool AllowsEntryHazardStageDrop(BattleSide source, BattleSide target)
    {
        BattleHookDispatchSnapshot side = SideConditions.CollectStageDropHooks(ConditionSnapshot,
            source, target, bypass: false, actionSequence: _traceActionSequence);
        _hookTrace.AddRange(side.Trace);
        return !side.Filters().Any(item => item is
            { Filter.Value: "stage_drop_attempt", Decision: BattleHookFilterDecision.Deny });
    }

    private IReadOnlyList<EntityId> EffectiveTypes(BattleSlot slot) => PhysicalMetricFormulas.EffectiveValues(
        Active(slot), _overlays, new BattleOverlayOwner(slot.Side, ActiveIndex(slot), slot)).CreatureTypes;

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
            || e is StatSwapEffect or StatStealEffect or DerivedStatSwapEffect or DerivedStatSplitEffect
            || (e is StatInvertEffect i && !i.OnSelf)
            || (e is RandomStatRaiseEffect rr && !rr.OnSelf)));

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
        if (EffectiveTypes(ctx.TargetSlot).Contains(ctx.Move.Type)) // a seed of its own type can't take hold (grass immune to grass Leech Seed)
            return;
        ctx.Target.SetSeeded(true);
        _log.Add(new LeechSeeded(ctx.TargetSlot));
    }

    private void ApplyAilment(EffectContext ctx, AilmentEffect effect)
    {
        int start = _log.Count;
        bool eligible = !ctx.Target.IsFainted
            && StatusEffects.CanApplyStatus(ctx.Target.Status)
            && !StatusEffects.TypeImmuneToStatus(effect.Status, EffectiveTypes(ctx.TargetSlot))
            && !BlocksStatus(ctx.TargetSlot, effect.Status);
        if (eligible)
        {
            BattleHookDispatchSnapshot weather = WeatherConditions.CollectStatusHooks(
                ConditionSnapshot, effect.Status, ctx.TraceAction);
            _hookTrace.AddRange(weather.Trace);
            eligible = !weather.Filters().Any(filter => filter is
                { Filter.Value: "status_attempt", Decision: BattleHookFilterDecision.Deny });
            if (eligible)
            {
                BattleHookDispatchSnapshot terrain = TerrainConditions.CollectStatusHooks(
                    ConditionSnapshot, effect.Status, IsGrounded(ctx.TargetSlot), ctx.TraceAction);
                _hookTrace.AddRange(terrain.Trace);
                eligible = !terrain.Filters().Any(filter => filter is
                    { Filter.Value: "status_attempt", Decision: BattleHookFilterDecision.Deny });
            }
            if (eligible)
                eligible = AllowsSideStatus(ctx.SourceSlot, ctx.TargetSlot, ctx.Move, confusion: false,
                    ctx.TraceAction);
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
        bool eligible = !recipient.IsFainted && (effect.Delta >= 0
            || AllowsSideStageDrop(ctx.SourceSlot, recipientSlot, ctx.Move, ctx.TraceAction));
        EffectChanceResult chance = CheckEffectChance(EffectiveEffectChance(ctx, effect), eligible);
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
        bool eligible = !recipient.IsFainted && (effect.Delta >= 0
            || AllowsSideStageDrop(ctx.SourceSlot, recipientSlot, ctx.Move, ctx.TraceAction));
        EffectChanceResult chance = CheckEffectChance(EffectiveEffectChance(ctx, effect), eligible);
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

    private void ApplyStatSteal(EffectContext ctx, StatStealEffect effect)
    {
        int start = _log.Count;
        EffectChanceResult chance = CheckEffectChance(EffectiveEffectChance(ctx, effect),
            !ctx.Source.IsFainted && !ctx.Target.IsFainted);
        if (!chance.Passed)
        {
            TraceEffectChance(ctx, chance, start);
            return;
        }
        (IReadOnlyDictionary<StatKind, int> user, IReadOnlyDictionary<StatKind, int> target) =
            BattleStageMutation.Steal(StageSnapshot(ctx.Source), StageSnapshot(ctx.Target));
        foreach (StatKind stat in AllStageSlots)
        {
            SetStage(ctx.Target, ctx.TargetSlot, stat, target[stat]);
            SetStage(ctx.Source, ctx.SourceSlot, stat, user[stat]);
        }
        TraceEffectChance(ctx, chance, start);
    }

    private void ApplyRandomStatRaise(EffectContext ctx, RandomStatRaiseEffect effect)
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
        (StatKind? chosen, IReadOnlyDictionary<StatKind, int> result) =
            BattleStageMutation.RandomRaise(StageSnapshot(recipient), effect.Delta, _rng);
        if (chosen is { } stat)
            SetStage(recipient, recipientSlot, stat, result[stat]);
        TraceEffectChance(ctx, chance, start);
    }

    private static IReadOnlyDictionary<StatKind, int> StageSnapshot(BattleCreature creature) =>
        AllStageSlots.ToDictionary(stat => stat, creature.Stage);

    private void ApplyDerivedStatSwap(EffectContext ctx, DerivedStatSwapEffect effect)
    {
        int start = _log.Count;
        EffectChanceResult chance = CheckEffectChance(EffectiveEffectChance(ctx, effect),
            !ctx.Source.IsFainted && !ctx.Target.IsFainted);
        if (!chance.Passed)
        {
            TraceEffectChance(ctx, chance, start);
            return;
        }
        int userValue = DerivedStat(ctx.SourceSlot, effect.Stat);
        int targetValue = DerivedStat(ctx.TargetSlot, effect.Stat);
        if (userValue != targetValue)
        {
            // ponytail: a fresh additive delta per side computed from the pre-swap snapshot; each side's
            // effective stat becomes the other's. Composes with any prior deltas via the additive layer.
            ApplyDerivedStatDelta(ctx.SourceSlot, StatDelta(effect.Stat, targetValue - userValue),
                "derived_swap_speed", ctx.TraceAction);
            ApplyDerivedStatDelta(ctx.TargetSlot, StatDelta(effect.Stat, userValue - targetValue),
                "derived_swap_speed", ctx.TraceAction);
            _log.Add(new DerivedStatMutated(ctx.SourceSlot.Side, ActiveIndex(ctx.SourceSlot), effect.Stat,
                userValue, targetValue));
            _log.Add(new DerivedStatMutated(ctx.TargetSlot.Side, ActiveIndex(ctx.TargetSlot), effect.Stat,
                targetValue, userValue));
        }
        TraceEffectChance(ctx, chance, start);
    }

    private void ApplyDerivedStatSplit(EffectContext ctx, DerivedStatSplitEffect effect)
    {
        int start = _log.Count;
        EffectChanceResult chance = CheckEffectChance(EffectiveEffectChance(ctx, effect),
            !ctx.Source.IsFainted && !ctx.Target.IsFainted);
        if (!chance.Passed)
        {
            TraceEffectChance(ctx, chance, start);
            return;
        }
        (StatKind a, StatKind b) = effect.Group == DerivedStatGroup.Offense
            ? (StatKind.Atk, StatKind.Spa) : (StatKind.Def, StatKind.Spd);
        string key = effect.Group == DerivedStatGroup.Offense ? "derived_split_offense" : "derived_split_defense";
        SplitStat(ctx, a, key);
        SplitStat(ctx, b, key);
        TraceEffectChance(ctx, chance, start);
    }

    // Averages one derived stat across user and target (floor); both effective values become the average.
    // The overlay key is per-stat so a two-stat split keeps both contributions instead of overwriting.
    private void SplitStat(EffectContext ctx, StatKind stat, string groupKey)
    {
        string key = $"{groupKey}_{stat.ToString().ToLowerInvariant()}";
        int userValue = DerivedStat(ctx.SourceSlot, stat);
        int targetValue = DerivedStat(ctx.TargetSlot, stat);
        int average = (userValue + targetValue) / 2; // stats are positive, so this floors
        if (average != userValue)
        {
            ApplyDerivedStatDelta(ctx.SourceSlot, StatDelta(stat, average - userValue), key, ctx.TraceAction);
            _log.Add(new DerivedStatMutated(ctx.SourceSlot.Side, ActiveIndex(ctx.SourceSlot), stat, userValue, average));
        }
        if (average != targetValue)
        {
            ApplyDerivedStatDelta(ctx.TargetSlot, StatDelta(stat, average - targetValue), key, ctx.TraceAction);
            _log.Add(new DerivedStatMutated(ctx.TargetSlot.Side, ActiveIndex(ctx.TargetSlot), stat, targetValue, average));
        }
    }

    private int DerivedStat(BattleSlot slot, StatKind stat)
    {
        Stats stats = EffectiveValues(slot).Stats;
        return stat switch
        {
            StatKind.Atk => stats.Atk,
            StatKind.Def => stats.Def,
            StatKind.Spa => stats.Spa,
            StatKind.Spd => stats.Spd,
            StatKind.Spe => stats.Spe,
            _ => throw new ArgumentException($"Derived-stat mutation does not support {stat}."),
        };
    }

    private static Stats StatDelta(StatKind stat, int delta) => stat switch
    {
        StatKind.Atk => new Stats(0, delta, 0, 0, 0, 0),
        StatKind.Def => new Stats(0, 0, delta, 0, 0, 0),
        StatKind.Spa => new Stats(0, 0, 0, delta, 0, 0),
        StatKind.Spd => new Stats(0, 0, 0, 0, delta, 0),
        StatKind.Spe => new Stats(0, 0, 0, 0, 0, delta),
        _ => throw new ArgumentException($"Derived-stat mutation does not support {stat}."),
    };

    private void ApplyDerivedStatDelta(BattleSlot slot, Stats delta, string key, int actionSequence) =>
        _overlays.Apply(new BattleOverlayApplication(OverlayOwner(slot), new BattleOverlaySource(),
            BattleOverlayLayer.Additive, new StatDeltaOverlay(key, delta), Turn, actionSequence,
            Cleanup: BattleOverlayCleanup.Switch | BattleOverlayCleanup.Faint | BattleOverlayCleanup.BattleEnd));

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
        BattleHookDispatchSnapshot? side = ctx.Move.DamageClass == DamageClass.Status ? null
            : SideConditions.CollectSecondaryChanceHooks(ConditionSnapshot, ctx.SourceSide, ctx.TraceAction);
        if (effect.ChanceFormula is null && (side is null || side.Invocations.Count == 0))
            return effect.Chance;
        BattleQueryResult calculated = HpStatusFormulas.SecondaryChanceQuery(effect, ctx.Source, ctx.Target,
            side?.QueryModifiers(BattleQueryId.SecondaryChance));
        if (side is not null)
            _hookTrace.AddRange(side.Trace);
        BattleQueryResult result = calculated with
        {
            Inputs = new BattleQueryInputs(ctx.SourceSlot, ctx.TargetSlot, CurrentWeather,
                calculated.Inputs.Ruleset, CurrentTerrain),
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
        bool eligible = !ctx.Target.IsFainted && !ctx.Target.IsConfused;
        if (eligible)
        {
            BattleHookDispatchSnapshot terrain = TerrainConditions.CollectConfusionHooks(
                ConditionSnapshot, IsGrounded(ctx.TargetSlot), ctx.TraceAction);
            _hookTrace.AddRange(terrain.Trace);
            eligible = !terrain.Filters().Any(filter => filter is
                { Filter.Value: "confusion_attempt", Decision: BattleHookFilterDecision.Deny });
        }
        if (eligible)
            eligible = AllowsSideStatus(ctx.SourceSlot, ctx.TargetSlot, ctx.Move, confusion: true,
                ctx.TraceAction);
        EffectChanceResult chance = CheckEffectChance(EffectiveEffectChance(ctx, effect), eligible);
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
            string statName = Str(effect, "stat");
            int statDelta = Int(effect, "delta") ?? 0;
            if (statusName.Length > 0)
            {
                PersistentStatus status = Parse<PersistentStatus>(statusName);
                eligible = eligible
                    && StatusEffects.CanApplyStatus(source.Status)
                    && !StatusEffects.TypeImmuneToStatus(status, EffectiveTypes(sourceSlot))
                    && !BlocksStatus(sourceSlot, status)
                    && AllowsSideStatus(targetSlot, sourceSlot, null, confusion: false, traceAction);
            }
            else if (statName.Length > 0 && statDelta < 0)
                eligible = eligible && AllowsSideStageDrop(targetSlot, sourceSlot, null, traceAction);
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

            if (statName.Length > 0)
            {
                StatKind stat = Parse<StatKind>(statName);
                source.ChangeStage(stat, statDelta);
                _log.Add(new StatStageChanged(sourceSlot, stat, statDelta));
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

    private void Heal(BattleCreature c, BattleSlot slot, int amount, BattleMove? move = null,
        IReadOnlyList<BattleQueryModifier>? modifiers = null)
    {
        var context = new BattleQueryContext(slot, c, slot, c, CurrentWeather, Ruleset, CurrentTerrain);
        BattleQueryResult result = move is null
            ? BattleQuery.Evaluate(BattleQueryId.Healing, new BattleQueryValue(amount), modifiers, context)
            : BattleActionQueries.Healing(move, amount, modifiers, context);
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
        int hpFloor = 0, bool applySurvival = true)
    {
        int before = target.CurrentHp;
        if (applySurvival)
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
        bool substitute = false, EntityId? effectiveType = null, DamageClass? effectiveClass = null)
    {
        bool connected = damage.ActualHpRemoved > 0 && !substitute;
        _actionHistory.RecordDamage(new BattleDamageRecord(
            attempt, source, target, move.Move, effectiveClass ?? move.DamageClass, effectiveType ?? move.Type, cause, hitNumber,
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
        CancelCharge(slot, Active(slot));
        EndMultiTurnLock(slot, Active(slot), MultiTurnLockEndReason.Faint);
        Active(slot).ClearMultiTurnPowerBoost();
        TraceCancelled(_intentQueue.OwnerFainted(slot.Side, ActiveIndex(slot)));
        _log.Add(new Fainted(slot));
        _actionHistory.RecordFaint(HistoryOwner(slot));
    }

    private int ApplySurviveFromFull(BattleCreature target, BattleSlot targetSlot, int amount)
    {
        if (amount < target.CurrentHp || target.CurrentHp != target.MaxHp || target.HasConsumedHeldEffect("surviveFromFull"))
            return amount;
        if (!HeldEffects(target).Any(e => e.Op == "surviveFromFull"))
            return amount;

        if (!TryConsumeHeldItem(targetSlot, "surviveFromFull"))
            return amount;
        ReevaluateConditionForms();
        return Math.Max(0, target.CurrentHp - 1);
    }

    private bool TryItemPower(BattleSlot sourceSlot, BattleMove move, out int? power)
    {
        power = null;
        if (move.SecondaryEffects.OfType<ItemDataPowerEffect>().SingleOrDefault() is not { Field: ItemPowerField.FlingPower })
            return true;

        BattleCreature source = Active(sourceSlot);
        EntityId? heldItem = ItemsSuppressed ? null : PhysicalMetricFormulas.EffectiveValues(source, _overlays,
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

    private BattleDamageQueryResult ResolveDamageQuery(BattleSlot sourceSlot, BattleSlot targetSlot,
        BattleCreature source, BattleCreature target, BattleMove move,
        BattleMoveIdentityQueryResult identity, BattleEffectiveValues sourceValues,
        BattleEffectiveValues targetValues, int snapshottedLiveTargets, int traceAction)
    {
        var context = new BattleQueryContext(sourceSlot, source, targetSlot, target,
            CurrentWeather, Ruleset, CurrentTerrain);
        BattleDamageQueryResult result = BattleDamageQueries.Resolve(move, identity, source, target,
            sourceValues, targetValues, _chart, snapshottedLiveTargets, context);
        _damageQueryTrace.Add(new BattleDamageQueryTraceEntry(Turn, traceAction,
            sourceSlot, targetSlot, result));
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction, sourceSlot, targetSlot,
            result.Effectiveness));
        return result;
    }

    private (int Dmg, bool Crit, double Eff) ComputeHit(
        BattleSlot sourceSlot,
        BattleSlot targetSlot,
        BattleCreature attacker,
        BattleCreature target,
        BattleMove move,
        BattleMoveIdentityQueryResult moveIdentity,
        int power,
        int snapshottedLiveTargets,
        int ppBeforeSpend,
        int? itemPower,
        int? randomPower,
        int traceAction,
        out double? critDraw,
        out int? damageRollDraw)
    {
        EntityId moveType = moveIdentity.EffectiveType;
        critDraw = null;
        damageRollDraw = null;
        PhysicalFormulaInputs? physicalInputs = null;
        if (PhysicalMetricFormulas.HasPowerFormula(move))
        {
            BattleHookDispatchSnapshot sourceSpeed = SideConditions.CollectSpeedHooks(
                ConditionSnapshot, sourceSlot.Side, traceAction);
            BattleHookDispatchSnapshot targetSpeed = SideConditions.CollectSpeedHooks(
                ConditionSnapshot, targetSlot.Side, traceAction);
            _hookTrace.AddRange(sourceSpeed.Trace);
            _hookTrace.AddRange(targetSpeed.Trace);
            physicalInputs = PhysicalMetricFormulas.Inputs(attacker, target, _overlays,
                new BattleOverlayOwner(sourceSlot.Side, ActiveIndex(sourceSlot), sourceSlot),
                new BattleOverlayOwner(targetSlot.Side, ActiveIndex(targetSlot), targetSlot),
                sourceSpeed.QueryModifiers(BattleQueryId.Speed), targetSpeed.QueryModifiers(BattleQueryId.Speed));
        }
        BattleActionFormulaInputs? actionInputs = ActionHistoryFormulas.HasPowerFormula(move)
            ? _actionHistory.PowerInputs(HistoryOwner(sourceSlot), HistoryOwner(targetSlot), move.Move)
            : null;
        PartyResourceFormulaInputs? resourceInputs = PartyResourceFormulas.HasPowerFormula(move)
            ? PartyResourceFormulas.Inputs(Party(sourceSlot.Side), attacker, target,
                ppBeforeSpend, move.Pp, itemPower, randomPower)
            : null;
        HpStatusPowerQuery powerQuery = HpStatusFormulas.PowerQuery(move, attacker, target, physicalInputs,
            actionInputs, resourceInputs, IsPersonallyProtected(sourceSlot), IsPersonallyProtected(targetSlot));
        var powerModifiers = powerQuery.Modifiers.ToList();
        if (_currentPowerBoost is { } scheduledBoost && scheduledBoost.Slot == sourceSlot)
        {
            powerModifiers.Add(new BattleQueryModifier(BattleQueryStage.SourceTargetState,
                BattleQueryOperation.Multiply,
                new BattleQueryValue(scheduledBoost.Boost.Num, scheduledBoost.Boost.Den),
                InsertionOrder: powerModifiers.Count));
        }
        if (attacker.IsLocked && attacker.Moves[attacker.LockedMoveIndex].Move == move.Move
            && move.MultiTurnLockProfile is { MaxPowerStep: > 0 } lockProfile
            && attacker.LockPowerStep > 0)
        {
            Fraction multiplier = MultiTurnPowerMultiplier(lockProfile, attacker.LockPowerStep);
            powerModifiers.Add(new BattleQueryModifier(BattleQueryStage.SourceTargetState,
                BattleQueryOperation.Multiply, new BattleQueryValue(multiplier.Num, multiplier.Den),
                InsertionOrder: powerModifiers.Count));
        }
        if (move.MultiTurnLockProfile?.PowerBoostKey is { } boostKey
            && attacker.MultiTurnPowerBoostKey == boostKey)
        {
            Fraction boost = attacker.MultiTurnPowerBoost;
            powerModifiers.Add(new BattleQueryModifier(BattleQueryStage.SourceTargetState,
                BattleQueryOperation.Multiply, new BattleQueryValue(boost.Num, boost.Den),
                InsertionOrder: powerModifiers.Count));
        }
        if (move.SecondaryEffects.OfType<WeatherMoveEffect>().SingleOrDefault() is { } weatherEffect)
        {
            BattleHookDispatchSnapshot weather = WeatherConditions.CollectBasePowerHooks(
                ConditionSnapshot, weatherEffect, traceAction);
            _hookTrace.AddRange(weather.Trace);
            powerModifiers.AddRange(weather.QueryModifiers(BattleQueryId.BasePower)
                .Select(modifier => modifier with { InsertionOrder = powerModifiers.Count }));
        }
        if (target.SemiInvulnerableState is { } semiState
            && move.SecondaryEffects.OfType<SemiInvulnerableHitEffect>()
                .SingleOrDefault(effect => effect.States.Contains(semiState))?.PowerMultiplier is { } semiPower)
        {
            powerModifiers.Add(new BattleQueryModifier(BattleQueryStage.SourceTargetState,
                BattleQueryOperation.Multiply, new BattleQueryValue(semiPower.Num, semiPower.Den),
                InsertionOrder: powerModifiers.Count));
        }
        BattleHookDispatchSnapshot fieldPower = FieldConditions.CollectBasePowerHooks(
            ConditionSnapshot, moveType.Slug, Ruleset, traceAction);
        _hookTrace.AddRange(fieldPower.Trace);
        powerModifiers.AddRange(fieldPower.QueryModifiers(BattleQueryId.BasePower)
            .Select(modifier => modifier with { InsertionOrder = powerModifiers.Count }));
        if (move.SecondaryEffects.OfType<TerrainMoveEffect>().SingleOrDefault() is { } terrainEffect)
        {
            BattleHookDispatchSnapshot terrain = TerrainConditions.CollectBasePowerHooks(
                ConditionSnapshot, terrainEffect, IsGrounded(sourceSlot), IsGrounded(targetSlot), traceAction);
            _hookTrace.AddRange(terrain.Trace);
            powerModifiers.AddRange(terrain.QueryModifiers(BattleQueryId.BasePower)
                .Select(modifier => modifier with { InsertionOrder = powerModifiers.Count }));
        }
        BattleQueryResult powerResult = BattleQuery.Evaluate(BattleQueryId.BasePower,
            new BattleQueryValue(powerQuery.AuthoredBase), powerModifiers,
            new BattleQueryContext(sourceSlot, attacker, targetSlot, target, CurrentWeather, Ruleset, CurrentTerrain));
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction, sourceSlot, targetSlot, powerResult));
        power = powerResult.FinalValue.ToInt32();

        BattleEffectiveValues sourceValues = EffectiveValues(sourceSlot);
        BattleEffectiveValues targetValues = EffectiveValues(targetSlot);
        var queryContext = new BattleQueryContext(sourceSlot, attacker, targetSlot, target,
            CurrentWeather, Ruleset, CurrentTerrain);
        BattleDamageQueryResult damageQuery = ResolveDamageQuery(sourceSlot, targetSlot, attacker,
            target, move, moveIdentity, sourceValues, targetValues, snapshottedLiveTargets, traceAction);
        double eff = TypeChart.ToDouble(damageQuery.Effectiveness.FinalValue);
        if (eff <= 0)
            return (0, false, eff);

        bool physical = moveIdentity.EffectiveClass == DamageClass.Physical;
        BattleHookDispatchSnapshot critical = SideConditions.CollectCriticalHooks(
            ConditionSnapshot, sourceSlot.Side, targetSlot.Side, traceAction);
        _hookTrace.AddRange(critical.Trace);
        BattleConditionOwner sourceOwner = new(BattleConditionScope.Creature, sourceSlot.Side,
            sourceSlot, ActiveIndex(sourceSlot));
        BattleConditionInstance? criticalGuarantee = OneShotQueryConditions.FindCritical(
            ConditionSnapshot, sourceOwner);
        BattleQueryResult criticalResult = BattleActionQueries.CriticalChance(move,
            move.CritStage + attacker.CritStageBonus, criticalGuarantee is not null,
            critical.QueryModifiers(BattleQueryId.CriticalChance),
            new BattleQueryContext(sourceSlot, attacker, targetSlot, target, CurrentWeather, Ruleset, CurrentTerrain));
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction, sourceSlot, targetSlot, criticalResult));
        bool guaranteedCritical = criticalGuarantee is not null
            && criticalResult.FinalValue == new BattleQueryValue(1);
        bool crit;
        if (guaranteedCritical)
        {
            crit = true;
            RecordConditionChanges(_conditions.Remove(criticalGuarantee!.Definition.Id,
                criticalGuarantee.Owner, Turn, traceAction));
        }
        else
        {
            crit = BattleRolls.IsCrit(criticalResult.FinalValue, _rng, out double critRoll);
            critDraw = critRoll;
        }
        int roll = BattleRolls.DamageRoll(_rng, out int randomRoll);
        damageRollDraw = randomRoll;

        DamageStatSelector offensive = damageQuery.Offensive with
        {
            Stat = FieldConditions.DefensiveStat(ConditionSnapshot, damageQuery.Offensive.Stat),
        };
        DamageStatSelector defensive = damageQuery.Defensive with
        {
            Stat = FieldConditions.DefensiveStat(ConditionSnapshot, damageQuery.Defensive.Stat),
        };
        BattleCreature offensiveOwner = BattleDamageQueries.Owner(offensive, attacker, target);
        BattleCreature defensiveOwner = BattleDamageQueries.Owner(defensive, attacker, target);
        BattleEffectiveValues offensiveValues = BattleDamageQueries.Owner(offensive, sourceValues, targetValues);
        BattleEffectiveValues defensiveValues = BattleDamageQueries.Owner(defensive, sourceValues, targetValues);
        BattleSlot offensiveSlot = BattleDamageQueries.Owner(offensive, sourceSlot, targetSlot);
        BattleSlot defensiveSlot = BattleDamageQueries.Owner(defensive, sourceSlot, targetSlot);
        int aStage = offensiveOwner.Stage(offensive.Stat);
        int dStage = defensiveOwner.Stage(defensive.Stat);
        if (crit)
        {
            aStage = Math.Max(0, aStage);
            dStage = Math.Min(0, dStage);
        }

        List<BattleQueryModifier> attackModifiers =
        [
            new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply, BattleQuery.StatStageMultiplier(aStage), InsertionOrder: 0),
            .. StatHookModifiers(sourceSlot, targetSlot, offensiveSlot, offensive.Stat, BattleQueryId.OffensiveStat),
        ];
        List<BattleQueryModifier> defenseModifiers =
        [
            new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply, BattleQuery.StatStageMultiplier(dStage), InsertionOrder: 0),
            .. StatHookModifiers(sourceSlot, targetSlot, defensiveSlot, defensive.Stat, BattleQueryId.DefensiveStat),
        ];
        BattleQueryResult attackResult = BattleQuery.Evaluate(BattleQueryId.OffensiveStat,
            new BattleQueryValue(StatValue(offensiveValues.Stats, offensive.Stat)), attackModifiers, queryContext);
        BattleQueryResult defenseResult = BattleQuery.Evaluate(BattleQueryId.DefensiveStat,
            new BattleQueryValue(StatValue(defensiveValues.Stats, defensive.Stat)), defenseModifiers, queryContext);
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction, sourceSlot, targetSlot, attackResult));
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction, sourceSlot, targetSlot, defenseResult));
        int a = attackResult.FinalValue.ToInt32();
        int d = defenseResult.FinalValue.ToInt32();
        double stab = TypeChart.ToDouble(damageQuery.Stab);
        bool burn = attacker.Status == PersistentStatus.Burn && physical && !powerQuery.IgnoreSourceBurnPenalty;

        int dmg = DamageCalc.Compute(attacker.Level, power, a, d, eff, stab, crit, roll, burn, snapshottedLiveTargets);
        BattleQueryResult damageResult = BattleActionQueries.FinalDamage(move, dmg,
            DamageHookModifiers(move, moveIdentity.EffectiveClass, moveType, sourceSlot, targetSlot, crit),
            queryContext);
        _queryTrace.Add(new BattleQueryTraceEntry(Turn, traceAction, sourceSlot, targetSlot, damageResult));
        return (damageResult.FinalValue.ToInt32(), crit, eff);
    }

    private static Fraction MultiTurnPowerMultiplier(MultiTurnLockProfile profile, int step)
    {
        Fraction perStep = profile.EffectivePowerStep;
        int numerator = 1;
        int denominator = 1;
        for (int i = 0; i < Math.Min(step, profile.MaxPowerStep); i++)
        {
            numerator = checked(numerator * perStep.Num);
            denominator = checked(denominator * perStep.Den);
        }
        return new Fraction(numerator, denominator);
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
            foreach (AbilityHook hook in AbilityHooks(slot))
            {
                Effect[] effects = hook.Effects.Where(effect => effect.Op is not
                    ("itemMutationGuard" or "abilityMutationGuard")).ToArray();
                if (effects.Length > 0)
                    yield return new BattleHookSource(slot, BattleHookSourceKind.Ability, hook.Hook, effects);
            }
            foreach (var group in HeldEffects(c).Select(e => (Hook: HeldHook(e), Effect: e))
                .Where(x => x.Hook is not null).GroupBy(x => x.Hook!.Value))
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
            else if (invocation.Effect.Op == "terrainSummon")
            {
                int turns = Int(invocation.Effect, "duration") ?? TerrainConditions.DefaultTurns;
                SetTerrain(Parse<Terrain>(Str(invocation.Effect, "terrain")),
                    turns + TerrainDurationExtension(invocation.Slot), invocation.Slot);
            }
            else if (invocation.Effect.Op == "terrainSeed")
                TriggerTerrainSeed(invocation.Slot, invocation.Effect);
        }
    }

    private void TriggerInitialTerrainSeeds()
    {
        if (CurrentTerrain == Terrain.None)
            return;
        foreach (BattleSlot slot in Topology.Slots)
            TriggerTerrainSeed(slot);
    }

    private void TriggerTerrainSeed(BattleSlot slot)
    {
        foreach (Effect effect in HeldEffects(Active(slot)).Where(effect => effect.Op == "terrainSeed"))
            TriggerTerrainSeed(slot, effect);
    }

    private void TriggerTerrainSeed(BattleSlot slot, Effect effect)
    {
        BattleCreature holder = Active(slot);
        if (holder.IsFainted || holder.HasConsumedHeldEffect(effect.Op)
            || Parse<Terrain>(Str(effect, "terrain")) != CurrentTerrain)
            return;

        StatKind stat = Parse<StatKind>(Str(effect, "stat"));
        if (holder.Stage(stat) >= StatStages.Max)
            return;

        if (!TryConsumeHeldItem(slot, effect.Op))
            return;
        holder.ChangeStage(stat, 1);
        _log.Add(new StatStageChanged(slot, stat, 1));
        ReevaluateConditionForms();
    }

    private int WeatherDurationExtension(BattleSlot slot) =>
        HeldEffects(Active(slot))
            .Where(e => e.Op == "weatherDurationExtend")
            .Sum(e => Int(e, "turns") ?? 0);

    private int TerrainDurationExtension(BattleSlot slot) =>
        HeldEffects(Active(slot))
            .Where(e => e.Op == "terrainDurationExtend")
            .Sum(e => Int(e, "turns") ?? 0);

    private int SideConditionDurationExtension(BattleSlot slot) =>
        HeldEffects(Active(slot))
            .Where(e => e.Op == "sideConditionDurationExtend"
                && string.Equals(Str(e, "tag"), "screen", StringComparison.Ordinal))
            .Sum(e => Int(e, "turns") ?? 0);

    private void ApplyChoiceLock(BattleCreature creature, int moveIndex)
    {
        if (HeldEffects(creature).Any(e => e.Op == "choiceLock"))
            creature.SetChoiceLock(moveIndex);
    }

    private bool ItemsSuppressed => FieldConditions.Active(ConditionSnapshot, BattleFieldCondition.MagicRoom);
    private IReadOnlyList<Effect> HeldEffects(BattleCreature creature)
    {
        if (ItemsSuppressed)
            return [];
        BattleSlot? slot = Topology.Slots.Where(candidate => ReferenceEquals(Active(candidate), creature))
            .Select(candidate => (BattleSlot?)candidate).FirstOrDefault();
        if (slot is not { } activeSlot)
            return [];
        EntityId? effective = EffectiveValues(activeSlot).HeldItem;
        if (effective is { } item && _itemData.TryGetValue(item, out Item? definition))
            return definition.BattleEffects;
        return effective == creature.HeldItem ? creature.HeldItemBattleEffects : [];
    }

    private IReadOnlyList<BattleQueryModifier> DamageHookModifiers(
        BattleMove move, DamageClass damageClass, EntityId moveType,
        BattleSlot sourceSlot, BattleSlot targetSlot, bool critical)
    {
        var modifiers = new List<BattleQueryModifier>();
        int insertion = 0;

        BattleHookDispatchSnapshot weather = WeatherConditions.CollectDamageHooks(
            ConditionSnapshot, moveType.Slug, _traceActionSequence);
        _hookTrace.AddRange(weather.Trace);
        modifiers.AddRange(weather.QueryModifiers(BattleQueryId.FinalDamage)
            .Select(modifier => modifier with { InsertionOrder = insertion++ }));
        BattleHookDispatchSnapshot terrain = TerrainConditions.CollectDamageHooks(
            ConditionSnapshot, moveType.Slug, IsGrounded(sourceSlot), IsGrounded(targetSlot),
            _traceActionSequence);
        _hookTrace.AddRange(terrain.Trace);
        modifiers.AddRange(terrain.QueryModifiers(BattleQueryId.FinalDamage)
            .Select(modifier => modifier with { InsertionOrder = insertion++ }));
        BattleHookDispatchSnapshot side = SideConditions.CollectDamageHooks(ConditionSnapshot,
            targetSlot.Side, damageClass, Topology.ActiveSlotsPerSide, critical,
            BypassesSideConditions(sourceSlot, move, "screen"), _traceActionSequence);
        _hookTrace.AddRange(side.Trace);
        modifiers.AddRange(side.QueryModifiers(BattleQueryId.FinalDamage)
            .Select(modifier => modifier with { InsertionOrder = insertion++ }));

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
                if (!Enum.TryParse(Str(effect, "damageClass"), ignoreCase: true, out DamageClass parsedClass)
                    || parsedClass != damageClass)
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

        return modifiers;
    }

    private bool AllowsSideStatus(BattleSlot sourceSlot, BattleSlot targetSlot, BattleMove? move,
        bool confusion, int traceAction)
    {
        BattleHookDispatchSnapshot side = confusion
            ? SideConditions.CollectConfusionHooks(ConditionSnapshot, sourceSlot.Side, targetSlot.Side,
                BypassesSideConditions(sourceSlot, move, "status_guard"), traceAction)
            : SideConditions.CollectStatusHooks(ConditionSnapshot, sourceSlot.Side, targetSlot.Side,
                BypassesSideConditions(sourceSlot, move, "status_guard"), traceAction);
        _hookTrace.AddRange(side.Trace);
        string filter = confusion ? "confusion_attempt" : "status_attempt";
        return !side.Filters().Any(item => item is
            { Filter.Value: var value, Decision: BattleHookFilterDecision.Deny } && value == filter);
    }

    private bool AllowsSideStageDrop(BattleSlot sourceSlot, BattleSlot targetSlot, BattleMove? move,
        int traceAction)
    {
        BattleHookDispatchSnapshot side = SideConditions.CollectStageDropHooks(ConditionSnapshot,
            sourceSlot.Side, targetSlot.Side, BypassesSideConditions(sourceSlot, move, "stage_guard"), traceAction);
        _hookTrace.AddRange(side.Trace);
        return !side.Filters().Any(item => item is
            { Filter.Value: "stage_drop_attempt", Decision: BattleHookFilterDecision.Deny });
    }

    private bool BypassesSideConditions(BattleSlot sourceSlot, BattleMove? move, string tag) =>
        move?.SecondaryEffects.OfType<SideConditionBypassEffect>().Any(effect => effect.Tag == tag) == true
        || (tag is "screen" or "side_protection")
            && move?.SecondaryEffects.OfType<RemoveSideConditionEffect>().Any(effect =>
                (effect.Tag == tag || tag == "screen" && effect.Tag == "barrier")
                && effect.Side == SideConditionTarget.Target
                && effect.Timing == SideConditionTiming.BeforeDamage) == true
        || AbilityHooks(sourceSlot).SelectMany(hook => hook.Effects).Any(effect =>
            effect.Op == "sideConditionBypass"
            && string.Equals(Str(effect, "tag"), tag, StringComparison.Ordinal));

    private bool BypassesProtection(BattleSlot sourceSlot, BattleMove move) =>
        move.SecondaryEffects.OfType<ProtectionBypassEffect>().Any()
        || AbilityHooks(sourceSlot).SelectMany(hook => hook.Effects)
            .Any(effect => effect.Op == "protectionBypass");

    private bool IsPersonallyProtected(BattleSlot slot) => ProtectionConditions.Active(
        ConditionSnapshot, new BattleConditionOwner(BattleConditionScope.Creature,
            slot.Side, slot, ActiveIndex(slot))) is not null;

    private void PrepareProtectionChains(IEnumerable<BattleActionSubmission> submissions)
    {
        foreach (BattleActionSubmission submission in submissions)
        {
            BattleCreature creature = Active(submission.Source);
            ProtectionProfile? profile = MoveIndex(submission.Action) is { } moveIndex
                ? MoveAt(submission.Source, EffectiveMoveIndex(submission.Source, moveIndex))
                    .SecondaryEffects.OfType<ProtectEffect>().SingleOrDefault()?.Profile
                : null;
            if (profile is null || !ProtectionConditions.UsesChain(profile, Ruleset))
                creature.ResetProtectChain();
        }
    }

    private static void ResetFailedProtection(BattleCreature creature, BattleMove move)
    {
        if (move.SecondaryEffects.OfType<ProtectEffect>().Any())
            creature.ResetProtectChain();
    }

    private BattleMoveIdentityQueryResult EffectiveMoveIdentity(BattleSlot sourceSlot, BattleMove move,
        int traceAction)
    {
        EntityId? conditionType = null;
        if (move.SecondaryEffects.OfType<WeatherMoveEffect>().SingleOrDefault() is { } weatherEffect)
        {
            BattleHookDispatchSnapshot weather = WeatherConditions.CollectMoveTypeHooks(
                ConditionSnapshot, weatherEffect, traceAction);
            _hookTrace.AddRange(weather.Trace);
            conditionType = weather.MoveTypes().SingleOrDefault() is { } weatherType && weatherType != default
                ? weatherType : null;
        }
        else if (move.SecondaryEffects.OfType<TerrainMoveEffect>().SingleOrDefault() is { } terrainEffect)
        {
            BattleHookDispatchSnapshot terrain = TerrainConditions.CollectMoveTypeHooks(
                ConditionSnapshot, terrainEffect, IsGrounded(sourceSlot), traceAction);
            _hookTrace.AddRange(terrain.Trace);
            conditionType = terrain.MoveTypes().SingleOrDefault() is { } terrainType && terrainType != default
                ? terrainType : null;
        }
        if (_currentPair is { } pair && pair.Slot == sourceSlot && pair.Type is { } pairedType)
            conditionType = pairedType;
        BattleCreature source = Active(sourceSlot);
        int slot = MoveSlot(source, move, allowUnowned: true);
        return slot >= 0
            ? BattleDamageQueries.Identity(move, slot, source, EffectiveValues(sourceSlot), Environment, conditionType)
            : new BattleMoveIdentityQueryResult(move.Type, conditionType ?? move.Type,
                move.DamageClass, move.DamageClass, Environment.Natural, Environment.Effective);
    }

    private EntityId EffectiveMoveType(BattleSlot sourceSlot, BattleMove move, int traceAction) =>
        EffectiveMoveIdentity(sourceSlot, move, traceAction).EffectiveType;

    private DamageClass EffectiveDamageClass(BattleSlot sourceSlot, BattleMove move)
    {
        BattleCreature source = Active(sourceSlot);
        int slot = MoveSlot(source, move, allowUnowned: true);
        return slot >= 0 ? BattleDamageQueries.Identity(move, slot, source,
            EffectiveValues(sourceSlot), Environment).EffectiveClass : move.DamageClass;
    }

    private BattleEffectiveValues EffectiveValues(BattleSlot slot) => PhysicalMetricFormulas.EffectiveValues(
        Active(slot), _overlays, new BattleOverlayOwner(slot.Side, ActiveIndex(slot), slot));

    private static int MoveSlot(BattleCreature source, BattleMove move, bool allowUnowned = false)
    {
        for (int index = 0; index < source.Moves.Count; index++)
            if (ReferenceEquals(source.Moves[index], move))
                return index;
        return move.IsFallback || allowUnowned ? -1
            : throw new InvalidOperationException("The resolved move is not owned by its source creature.");
    }

    private MoveTarget EffectiveTarget(BattleSlot sourceSlot, BattleMove move) =>
        move.Target == MoveTarget.Selected
        && move.SecondaryEffects.OfType<TerrainMoveEffect>().SingleOrDefault() is { } effect
        && TerrainConditions.Spreads(ConditionSnapshot, effect, IsGrounded(sourceSlot))
            ? MoveTarget.AllOpponents
            : move.Target;

    private IReadOnlyList<BattleQueryModifier> StatHookModifiers(
        BattleSlot sourceSlot, BattleSlot targetSlot, BattleSlot statOwner, StatKind stat, BattleQueryId query)
    {
        var modifiers = new List<BattleQueryModifier>();
        BattleHookDispatchSnapshot weather = WeatherConditions.CollectStatHooks(
            ConditionSnapshot, EffectiveTypes(statOwner), stat, query, Ruleset, _traceActionSequence);
        _hookTrace.AddRange(weather.Trace);
        modifiers.AddRange(weather.QueryModifiers(query));
        int insertion = modifiers.Count;
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

    private string InitializeField(BattleFieldInputs? supplied)
    {
        BattleFieldInputs inputs = supplied ?? new BattleFieldInputs();
        if (!BattleRulesets.IsSupported(inputs.Ruleset))
            throw new ArgumentException("Unknown battle ruleset profile.", nameof(supplied));
        if (!TerrainConditions.IsNaturalEnvironment(inputs.NaturalEnvironment))
            throw new ArgumentOutOfRangeException(nameof(supplied), "Unknown or terrain-only natural battle environment.");
        if (!Enum.IsDefined(inputs.InitialWeather))
            throw new ArgumentOutOfRangeException(nameof(supplied), "Unknown initial weather.");
        if (!Enum.IsDefined(inputs.InitialTerrain))
            throw new ArgumentOutOfRangeException(nameof(supplied), "Unknown initial terrain.");
        if (inputs.InitialWeatherDuration is <= 0)
            throw new ArgumentOutOfRangeException(nameof(supplied), "Initial weather duration must be positive.");
        if (inputs.InitialTerrainDuration is <= 0)
            throw new ArgumentOutOfRangeException(nameof(supplied), "Initial terrain duration must be positive.");
        if (inputs.InitialTerrain == Terrain.None && inputs.InitialTerrainDuration is not null)
            throw new ArgumentException("Clear initial terrain cannot have a duration.", nameof(supplied));
        if (!TerrainConditions.Supports(inputs.InitialTerrain, inputs.Ruleset))
            throw new ArgumentException("Initial terrain is incompatible with the battle ruleset.", nameof(supplied));

        _naturalEnvironment = inputs.NaturalEnvironment;
        if (inputs.InitialWeather == Weather.None)
        {
            if (inputs.InitialWeatherDuration is not null)
                throw new ArgumentException("Clear initial weather cannot have a duration.", nameof(supplied));
        }
        else if (!WeatherConditions.Supports(inputs.InitialWeather, inputs.Ruleset))
            throw new ArgumentException("Initial weather is incompatible with the battle ruleset.", nameof(supplied));
        else
        {
            WeatherDef definition = WeatherConditions.For(inputs.InitialWeather);
            RecordConditionChanges(_conditions.Apply(new BattleConditionApplication(
                definition.Definition!.Id,
                WeatherConditions.FieldOwner,
                new BattleConditionSource(),
                Turn,
                ActionSequence: 0,
                inputs.InitialWeatherDuration)));
            _log.Add(new WeatherChanged(inputs.InitialWeather));
            ReevaluateConditionForms();
        }

        if (inputs.InitialTerrain != Terrain.None)
        {
            TerrainDef definition = TerrainConditions.For(inputs.InitialTerrain);
            RecordConditionChanges(_conditions.Apply(new BattleConditionApplication(
                definition.Definition!.Id,
                TerrainConditions.FieldOwner,
                new BattleConditionSource(),
                Turn,
                ActionSequence: 0,
                inputs.InitialTerrainDuration)));
            _log.Add(new TerrainChanged(inputs.InitialTerrain));
        }
        return inputs.Ruleset;
    }

    private void SetWeather(Weather weather, int turns, BattleSlot sourceSlot)
    {
        if (weather == Weather.None || weather == CurrentWeather)
            return;
        if (!WeatherConditions.Supports(weather, Ruleset))
            throw new InvalidOperationException($"Weather '{weather}' is incompatible with ruleset '{Ruleset}'.");
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

    private void SetTerrain(Terrain terrain, int turns, BattleSlot sourceSlot)
    {
        if (terrain == Terrain.None || terrain == CurrentTerrain)
            return;
        if (!TerrainConditions.Supports(terrain, Ruleset))
            throw new InvalidOperationException($"Terrain '{terrain}' is incompatible with ruleset '{Ruleset}'.");
        TerrainDef definition = TerrainConditions.For(terrain);
        RecordConditionChanges(_conditions.Apply(new BattleConditionApplication(
            definition.Definition!.Id,
            TerrainConditions.FieldOwner,
            new BattleConditionSource(sourceSlot, ActiveIndex(sourceSlot)),
            Turn,
            _traceActionSequence,
            Math.Max(1, turns))));
        _log.Add(new TerrainChanged(terrain));
        if (_dispatchingTerrainChange)
            return;

        _dispatchingTerrainChange = true;
        try
        {
            ApplyHookInvocations(BattleHookDispatcher.TerrainChange(sourceSlot.Side, HookSources()));
        }
        finally
        {
            _dispatchingTerrainChange = false;
        }
    }

    private void ClearTerrain()
    {
        if (CurrentTerrain == Terrain.None)
            return;
        Terrain terrain = CurrentTerrain;
        RecordConditionChanges(_conditions.Remove(TerrainConditions.For(terrain).Definition!.Id,
            TerrainConditions.FieldOwner, Turn, _traceActionSequence));
        _log.Add(new TerrainEnded(terrain));
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
        "terrainSeed" => AbilityHookPoint.OnTerrainChange,
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

    private bool PassesActionFilterGate(BattleSlot slot, int traceAction)
    {
        BattleConditionInstance? condition = ConditionSnapshot.FirstOrDefault(instance =>
            instance.Owner.Scope == BattleConditionScope.Creature
            && instance.Owner.Side == slot.Side && instance.Owner.PartyIndex == ActiveIndex(slot)
            && instance.Definition.ActionFilter?.Kind == ActionFilterKind.ActionBlockChance);
        if (condition?.Definition.ActionFilter is not { } filter)
            return true;
        int draw = _rng.Next(100);
        bool allowed = draw >= filter.BlockChance;
        if (!allowed)
            _log.Add(new ActionBlocked(slot, ActionLegalityReason.ActionChanceBlocked, condition.Definition.Id));
        AddTrace(traceAction, slot, null, EffectTraceKind.MoveGate, true, draw, allowed ? 1 : 0,
            _log.Count - (allowed ? 0 : 1), _log.Count, 100);
        return allowed;
    }

    private ActionLegalityResult LockedMoveLegality(BattleCreature creature, BattleSlot slot, int moveIndex) =>
        !creature.IsLocked ? new ActionLegalityResult(true)
            : BattleActionLegality.Move(creature, moveIndex, slot, ActiveIndex(slot), ConditionSnapshot,
                SourceCreature, ItemsSuppressed, ignorePp: true, ignoreChoice: true);

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
        ResolveTurnEndIntents();

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
        SideConditionsTurnEnd();
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
            if (!TryConsumeHeldItem(slot, effect.Op))
                return;
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

            if (!TryConsumeHeldItem(slot, effect.Op))
                return;
            Heal(c, slot, Math.Min(amount, c.MaxHp - c.CurrentHp));
            ReevaluateConditionForms();
        }
    }

    private bool TryConsumeHeldItem(BattleSlot slot, string op)
    {
        BattleCreature creature = Active(slot);
        BattleOverlayOwner owner = OverlayOwner(slot);
        EntityId? item = EffectiveValues(slot).HeldItem;
        if (item is null || !_itemData.ContainsKey(item.Value))
        {
            creature.ConsumeHeldEffect(op);
            _log.Add(new HeldItemConsumed(slot, op));
            return true;
        }

        BattleItemMutationResult result = _items.Mutate(BattleItemOperation.Consume,
            owner, PhysicalMetricFormulas.BaseEffectiveValues(creature), creature.IsFainted,
            owner, PhysicalMetricFormulas.BaseEffectiveValues(creature), creature.IsFainted,
            Turn, _traceActionSequence, op.ToLowerInvariant());
        if (!result.Succeeded)
            return false;
        creature.ConsumeHeldEffect(op);
        _log.Add(new HeldItemMutated(owner.Side, owner.PartyIndex, item, null,
            BattleItemOperation.Consume, op.ToLowerInvariant()));
        _log.Add(new HeldItemConsumed(slot, op));
        return true;
    }

    private static int FractionAmount(BattleCreature c, Effect effect)
    {
        int num = Int(effect, "num") ?? 1;
        int den = Int(effect, "den") ?? 16;
        return Math.Max(1, c.MaxHp * num / den);
    }

    private void SideConditionsTurnEnd()
    {
        foreach (BattleSlot slot in Topology.Slots)
        {
            BattleCreature creature = Active(slot);
            if (creature.IsFainted
                || !SideConditions.Active(ConditionSnapshot, slot.Side, BattleSideCondition.ResidualDamage))
                continue;
            var owner = new BattleOverlayOwner(slot.Side, ActiveIndex(slot), slot);
            if (PhysicalMetricFormulas.EffectiveValues(creature, _overlays, owner).CreatureTypes
                .Any(type => type.Slug == "fire"))
                continue;
            Sap(creature, slot, Math.Max(1, creature.MaxHp / 8), amount => new ResidualDamage(slot, amount));
        }
    }

    /// <summary>Field-condition end-turn hooks run in topology order before their shared duration
    /// checkpoint completes. Draws no RNG.</summary>
    private void FieldConditionsTurnEnd()
    {
        Weather weather = CurrentWeather;
        if (weather != Weather.None)
        {
            WeatherDef def = WeatherConditions.For(weather);
            if (def.Definition!.Hooks.Contains(BattleConditionHook.TurnEnd))
            {
                foreach (BattleSlot slot in Topology.Slots)
                {
                    BattleCreature c = Active(slot);
                    if (c.IsFainted || EffectiveTypes(slot).Any(t => def.ResidualImmuneTypes.Contains(t.Slug)))
                        continue;
                    Sap(c, slot, Math.Max(1, c.MaxHp / def.ResidualDenominator), amt => new WeatherDamage(slot, amt));
                }
            }
        }

        Terrain terrain = CurrentTerrain;
        if (terrain == Terrain.Grassy)
        {
            foreach (BattleSlot slot in Topology.Slots)
            {
                BattleCreature creature = Active(slot);
                if (creature.IsFainted || creature.CurrentHp == creature.MaxHp || !IsGrounded(slot))
                    continue;
                int before = creature.CurrentHp;
                creature.Heal(Math.Max(1, creature.MaxHp / TerrainConditions.For(terrain).HealingDenominator));
                _log.Add(new TerrainHealed(slot, creature.CurrentHp - before));
            }
        }

        BattleConditionChangeSet completion = _conditions.CompleteCheckpoint(
            BattleIntentCheckpoint.TurnEnd, Turn, _traceActionSequence);
        _overlays.CompleteCheckpoint(BattleIntentCheckpoint.TurnEnd, Turn, _traceActionSequence);
        RecordConditionChanges(completion);
        if (weather != Weather.None && completion.Events.OfType<ConditionExpired>()
            .Any(expired => expired.Scope == BattleConditionScope.Weather))
        {
            _log.Add(new WeatherEnded(weather));
            ReevaluateConditionForms();
        }
        if (terrain != Terrain.None && completion.Events.OfType<ConditionExpired>()
            .Any(expired => expired.Scope == BattleConditionScope.Terrain))
            _log.Add(new TerrainEnded(terrain));
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
            RecordConditionChanges(_conditions.OwnerFainted(slot.Side, ActiveIndex(slot), Turn,
                _traceActionSequence));
            RecordConditionChanges(_conditions.SourceLeft(slot.Side, ActiveIndex(slot),
                BattleConditionCleanupReason.Faint, Turn, _traceActionSequence));
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
        foreach (BattleSlot slot in Topology.Slots)
            CancelCharge(slot, Active(slot));
        TraceCancelled(_intentQueue.EndBattle());
        _overlays.EndBattle(Turn, _traceActionSequence);
        _items.EndBattle();
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
