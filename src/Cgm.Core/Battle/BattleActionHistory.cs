using Cgm.Core.Model;

namespace Cgm.Core.Battle;

public enum BattlePlannedActionKind { Move, Switch, Other }
public enum BattleActionResult { Pending, Prevented, Failed, Missed, Succeeded, Connected }
public enum ConsecutivePowerScope { CreatureConnected, SideAttemptedTurns }
public enum ConsecutivePowerMode { Exponential, Linear }
public enum HistoryPowerCondition
{
    SourceBeforeTarget,
    SourceAfterTarget,
    PreviousActionFailed,
    AllyFaintedPreviousTurn,
}
public enum BattleDamageCause { Standard, Fixed, Level, OneHitKnockout, Counter, HpFormula }
public enum BattleDamageFailure
{
    None,
    Missed,
    Protected,
    Blocked,
    Immune,
    NoDamage,
    NoQualifyingDamage,
    Substitute,
}

public readonly record struct BattleHistoryOwner(BattleSide Side, int PartyIndex, BattleSlot Slot);
public readonly record struct BattleActionAttemptId(int Turn, int ActionSequence);
public sealed record BattleActionPlan(
    BattleHistoryOwner Owner,
    BattlePlannedActionKind Kind,
    DamageClass? MoveClass = null);
public sealed record BattleActionAttempt(
    BattleActionAttemptId Id,
    BattleHistoryOwner Source,
    EntityId Move,
    bool Started,
    BattleActionResult Result,
    IReadOnlyList<BattleHistoryOwner> Targets);
public sealed record BattleActionFormulaInputs(
    int PriorCreatureConnections,
    int PriorSideAttemptedTurns,
    bool SourceBeforeTarget,
    bool SourceAfterTarget,
    bool PreviousActionFailed,
    bool AllyFaintedPreviousTurn);
public sealed record BattleDamageRecord(
    BattleActionAttemptId Attempt,
    BattleHistoryOwner Source,
    BattleHistoryOwner Target,
    EntityId Move,
    DamageClass DamageClass,
    EntityId DamageType,
    BattleDamageCause Cause,
    int HitNumber,
    bool Attempted,
    bool Connected,
    BattleDamageFailure Failure,
    int CalculatedDamage,
    int AppliedDamage,
    int ActualHpRemoved,
    bool Critical,
    bool Contact,
    bool Substitute,
    bool FaintedTarget);

public sealed class BattleActionHistory
{
    private sealed record Pending(BattleHistoryOwner Source, EntityId Move, bool Started = false);
    private sealed record Streak(EntityId Move, int Count, int LastTurn);

    private readonly List<BattleActionAttempt> _attempts = [];
    private readonly List<BattleDamageRecord> _damage = [];
    private readonly Dictionary<BattleActionAttemptId, Pending> _pending = [];
    private readonly Dictionary<(BattleSide Side, int Party), BattleActionPlan> _plans = [];
    private readonly HashSet<(BattleSide Side, int Party)> _completed = [];
    private readonly HashSet<(BattleSide Side, int Party)> _switched = [];
    private readonly HashSet<(BattleSide Side, int Party)> _faintedOwners = [];
    private readonly Dictionary<(BattleSide Side, int Party), Streak> _creatureStreaks = [];
    private readonly Dictionary<(BattleSide Side, EntityId Move), Streak> _sideStreaks = [];
    private readonly Dictionary<(BattleSide Side, int Party), BattleActionResult> _lastResults = [];
    private readonly Dictionary<(BattleSide Side, int Party), EntityId> _lastSuccessfulMoves = [];
    private readonly HashSet<(int Turn, BattleSide Side)> _faints = [];
    private int _currentTurn = -1;

    public void BeginTurn(int turn, IEnumerable<BattleActionPlan> plans)
    {
        ArgumentNullException.ThrowIfNull(plans);
        if (turn < 0 || turn <= _currentTurn)
            throw new ArgumentOutOfRangeException(nameof(turn), "History turns must be nonnegative and increasing.");
        if (_pending.Count != 0)
            throw new InvalidOperationException("Every action attempt must complete before the next turn.");

        BattleActionPlan[] captured = plans.ToArray();
        foreach (BattleActionPlan plan in captured)
        {
            ValidateOwner(plan.Owner);
            if (!Enum.IsDefined(plan.Kind)
                || (plan.MoveClass is { } moveClass && !Enum.IsDefined(moveClass))
                || plan.Kind != BattlePlannedActionKind.Move && plan.MoveClass is not null)
                throw new ArgumentException("Only move plans may carry a damage class.", nameof(plans));
        }
        if (captured.Select(plan => Key(plan.Owner)).Distinct().Count() != captured.Length
            || captured.Select(plan => plan.Owner.Slot).Distinct().Count() != captured.Length)
            throw new ArgumentException("Turn plans require unique actors and slots.", nameof(plans));

        _currentTurn = turn;
        _plans.Clear();
        _completed.Clear();
        _switched.Clear();
        _faintedOwners.Clear();
        foreach (BattleActionPlan plan in captured)
        {
            _plans.Add(Key(plan.Owner), plan);
            if (plan.Kind != BattlePlannedActionKind.Move)
                ClearCreature(plan.Owner);
        }
        _attempts.RemoveAll(attempt => attempt.Id.Turn < turn - 1);
        _damage.RemoveAll(record => record.Attempt.Turn < turn - 1);
        _faints.RemoveWhere(faint => faint.Turn < turn - 1);
        foreach ((BattleSide Side, EntityId Move) key in _sideStreaks
            .Where(entry => entry.Value.LastTurn < turn - 1).Select(entry => entry.Key).ToArray())
            _sideStreaks.Remove(key);
    }

    public BattleActionAttemptId BeginMove(int actionSequence, BattleHistoryOwner source, EntityId move)
    {
        ValidateOwner(source);
        if (_currentTurn < 0)
            throw new InvalidOperationException("BeginTurn must precede action attempts.");
        if (actionSequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(actionSequence));
        var id = new BattleActionAttemptId(_currentTurn, actionSequence);
        if (_pending.ContainsKey(id) || _attempts.Any(attempt => attempt.Id == id))
            throw new ArgumentException("Action attempt identities must be unique.", nameof(actionSequence));

        (BattleSide Side, int Party) key = Key(source);
        if (_creatureStreaks.TryGetValue(key, out Streak? streak) && streak.Move != move)
            _creatureStreaks.Remove(key);
        _pending.Add(id, new Pending(source, move));
        return id;
    }

    public void MarkStarted(BattleActionAttemptId id)
    {
        Pending pending = FindPending(id);
        if (pending.Started)
            throw new InvalidOperationException("An action attempt can start only once.");
        _pending[id] = pending with { Started = true };

        var key = (pending.Source.Side, pending.Move);
        if (!_sideStreaks.TryGetValue(key, out Streak? streak) || streak.LastTurn < _currentTurn - 1)
            _sideStreaks[key] = new Streak(pending.Move, 1, _currentTurn);
        else if (streak.LastTurn < _currentTurn)
            _sideStreaks[key] = streak with { Count = Increment(streak.Count), LastTurn = _currentTurn };
    }

    public void ReplacePendingMove(BattleActionAttemptId id, EntityId move)
    {
        Pending pending = FindPending(id);
        if (pending.Started)
            throw new InvalidOperationException("A started action attempt cannot replace its move.");
        if (move.Category != EntityCategory.Move)
            throw new ArgumentException("Action attempts require a move EntityId.", nameof(move));
        _pending[id] = pending with { Move = move };
    }

    public void Complete(BattleActionAttemptId id, BattleActionResult result,
        IEnumerable<BattleHistoryOwner>? targets = null)
    {
        if (result is BattleActionResult.Pending || !Enum.IsDefined(result))
            throw new ArgumentOutOfRangeException(nameof(result));
        Pending pending = FindPending(id);
        if (!pending.Started && result is BattleActionResult.Missed or BattleActionResult.Succeeded
            or BattleActionResult.Connected)
            throw new ArgumentException("Only a started move can miss, succeed, or connect.", nameof(result));
        BattleHistoryOwner[] capturedTargets = (targets ?? []).ToArray();
        foreach (BattleHistoryOwner target in capturedTargets)
            ValidateOwner(target);
        if (capturedTargets.Select(Key).Distinct().Count() != capturedTargets.Length)
            throw new ArgumentException("Attempt targets must be unique.", nameof(targets));

        _pending.Remove(id);
        _attempts.Add(new BattleActionAttempt(id, pending.Source, pending.Move, pending.Started, result,
            Array.AsReadOnly(capturedTargets)));
        (BattleSide Side, int Party) sourceKey = Key(pending.Source);
        _completed.Add(sourceKey);
        if (_faintedOwners.Contains(sourceKey))
        {
            ClearCreature(pending.Source);
            return;
        }
        _lastResults[sourceKey] = result;
        if (result is BattleActionResult.Succeeded or BattleActionResult.Connected)
            _lastSuccessfulMoves[sourceKey] = pending.Move;
        if (result == BattleActionResult.Connected)
        {
            Streak next = _creatureStreaks.TryGetValue(sourceKey, out Streak? previous)
                && previous.Move == pending.Move && previous.LastTurn >= _currentTurn - 1
                ? previous with { Count = Increment(previous.Count), LastTurn = _currentTurn }
                : new Streak(pending.Move, 1, _currentTurn);
            _creatureStreaks[sourceKey] = next;
        }
        else
        {
            _creatureStreaks.Remove(sourceKey);
        }
    }

    public void RecordSwitch(BattleHistoryOwner outgoing, BattleHistoryOwner incoming)
    {
        ValidateOwner(outgoing);
        ValidateOwner(incoming);
        ClearCreature(outgoing);
        ClearCreature(incoming);
        _lastSuccessfulMoves.Remove(Key(outgoing));
        _lastSuccessfulMoves.Remove(Key(incoming));
        if (_currentTurn >= 0)
            _switched.Add(Key(incoming));
    }

    public void RecordFaint(BattleHistoryOwner owner)
    {
        ValidateOwner(owner);
        _lastSuccessfulMoves.Remove(Key(owner));
        if (_currentTurn >= 0)
        {
            _faints.Add((_currentTurn, owner.Side));
            _faintedOwners.Add(Key(owner));
        }
        ClearCreature(owner);
    }

    public BattleActionFormulaInputs PowerInputs(BattleHistoryOwner source, BattleHistoryOwner target,
        EntityId move) => PowerInputs(source, target, move, null, null);

    public BattleActionFormulaInputs PreviewPowerInputs(BattleHistoryOwner source, BattleHistoryOwner target,
        EntityId move, bool sourceBeforeTarget, bool sourceAfterTarget) =>
        PowerInputs(source, target, move, sourceBeforeTarget, sourceAfterTarget);

    public IReadOnlyList<BattleActionAttempt> Snapshot() => _attempts
        .OrderBy(attempt => attempt.Id.Turn)
        .ThenBy(attempt => attempt.Id.ActionSequence)
        .ToArray();

    public bool PreviousActionFailed(BattleHistoryOwner source)
    {
        ValidateOwner(source);
        return _lastResults.TryGetValue(Key(source), out BattleActionResult result)
            && result is BattleActionResult.Prevented or BattleActionResult.Failed or BattleActionResult.Missed;
    }

    public EntityId? LastSuccessfulMove(BattleHistoryOwner source)
    {
        ValidateOwner(source);
        return _lastSuccessfulMoves.TryGetValue(Key(source), out EntityId move) ? move : null;
    }

    public DamageClass? PlannedMoveClass(BattleHistoryOwner owner)
    {
        ValidateOwner(owner);
        return _plans.TryGetValue(Key(owner), out BattleActionPlan? plan) ? plan.MoveClass : null;
    }

    public void RecordDamage(BattleDamageRecord record)
    {
        ValidateOwner(record.Source);
        ValidateOwner(record.Target);
        if (record.Attempt.Turn != _currentTurn)
            throw new ArgumentException("Damage records must belong to the current turn.", nameof(record));
        Pending? pending = _pending.GetValueOrDefault(record.Attempt);
        BattleActionAttempt? completed = _attempts.FirstOrDefault(attempt => attempt.Id == record.Attempt);
        if (pending is null && completed is null)
            throw new ArgumentException("Damage records require a known action attempt.", nameof(record));
        BattleHistoryOwner expectedSource = pending?.Source ?? completed!.Source;
        EntityId expectedMove = pending?.Move ?? completed!.Move;
        if (record.Source != expectedSource || record.Move != expectedMove)
            throw new ArgumentException("Damage source and move must match the action attempt.", nameof(record));
        if (record.Move.Category != EntityCategory.Move || record.DamageType.Category != EntityCategory.Type
            || !Enum.IsDefined(record.DamageClass)
            || record.DamageClass == DamageClass.Status && record.Cause != BattleDamageCause.HpFormula
            || !Enum.IsDefined(record.Cause) || !Enum.IsDefined(record.Failure))
            throw new ArgumentException("Damage records require typed move, type, class, cause, and failure values.",
                nameof(record));
        if (record.CalculatedDamage < 0 || record.AppliedDamage < 0 || record.ActualHpRemoved < 0
            || record.AppliedDamage > record.CalculatedDamage || record.ActualHpRemoved > record.AppliedDamage)
            throw new ArgumentException("Damage amounts must be nonnegative and calculated >= applied >= actual.",
                nameof(record));
        ValidateDamageOutcome(record);
        if (_damage.Any(existing => existing.Attempt == record.Attempt && Key(existing.Target) == Key(record.Target)
            && existing.HitNumber == record.HitNumber))
            throw new ArgumentException("A target hit can be recorded only once per action attempt.", nameof(record));
        _damage.Add(record);
    }

    public IReadOnlyList<BattleDamageRecord> DamageSnapshot() => _damage
        .OrderBy(record => record.Attempt.Turn)
        .ThenBy(record => record.Attempt.ActionSequence)
        .ThenBy(record => (int)record.Target.Slot.Side)
        .ThenBy(record => record.Target.Slot.Position)
        .ThenBy(record => record.HitNumber)
        .ToArray();

    public IReadOnlyList<BattleDamageRecord> DamageTo(BattleHistoryOwner target, int turn)
    {
        ValidateOwner(target);
        if (turn < 0)
            throw new ArgumentOutOfRangeException(nameof(turn));
        return DamageSnapshot().Where(record => Key(record.Target) == Key(target) && record.Attempt.Turn == turn)
            .ToArray();
    }

    public IReadOnlyList<BattleDamageRecord> DamageFrom(BattleHistoryOwner source, int turn)
    {
        ValidateOwner(source);
        if (turn < 0)
            throw new ArgumentOutOfRangeException(nameof(turn));
        return DamageSnapshot().Where(record => Key(record.Source) == Key(source) && record.Attempt.Turn == turn)
            .ToArray();
    }

    public int TotalActualDamageTo(BattleHistoryOwner target, int turn) =>
        checked(DamageTo(target, turn).Sum(record => record.ActualHpRemoved));

    public void EndBattle()
    {
        _attempts.Clear();
        _damage.Clear();
        _pending.Clear();
        _plans.Clear();
        _completed.Clear();
        _switched.Clear();
        _faintedOwners.Clear();
        _creatureStreaks.Clear();
        _sideStreaks.Clear();
        _lastResults.Clear();
        _lastSuccessfulMoves.Clear();
        _faints.Clear();
        _currentTurn = -1;
    }

    private BattleActionFormulaInputs PowerInputs(BattleHistoryOwner source, BattleHistoryOwner target,
        EntityId move, bool? sourceBeforeTarget, bool? sourceAfterTarget)
    {
        ValidateOwner(source);
        ValidateOwner(target);
        (BattleSide Side, int Party) sourceKey = Key(source);
        (BattleSide Side, int Party) targetKey = Key(target);
        int connected = _creatureStreaks.TryGetValue(sourceKey, out Streak? creature)
            && creature.Move == move ? creature.Count : 0;
        int attemptedTurns = _sideStreaks.TryGetValue((source.Side, move), out Streak? side)
            ? side.LastTurn == _currentTurn ? Math.Max(0, side.Count - 1)
            : side.LastTurn == _currentTurn - 1 ? side.Count : 0
            : 0;
        bool switched = _switched.Contains(targetKey);
        bool pendingTargetMove = _plans.TryGetValue(targetKey, out BattleActionPlan? plan)
            && plan.Kind == BattlePlannedActionKind.Move && !_completed.Contains(targetKey);
        bool completedTargetMove = _completed.Contains(targetKey) && !switched;
        return new BattleActionFormulaInputs(
            connected,
            attemptedTurns,
            sourceBeforeTarget ?? (switched || pendingTargetMove),
            sourceAfterTarget ?? completedTargetMove,
            PreviousActionFailed(source),
            _faints.Contains((_currentTurn - 1, source.Side)));
    }

    private Pending FindPending(BattleActionAttemptId id) =>
        _pending.TryGetValue(id, out Pending? pending)
            ? pending
            : throw new ArgumentException("Unknown pending action attempt.", nameof(id));

    private static void ValidateDamageOutcome(BattleDamageRecord record)
    {
        bool targetLevelFailure = record.Failure is BattleDamageFailure.Missed
            or BattleDamageFailure.Protected or BattleDamageFailure.Blocked
            or BattleDamageFailure.NoQualifyingDamage;
        if (targetLevelFailure)
        {
            if (record.HitNumber != 0 || record.Attempted || record.Connected || record.CalculatedDamage != 0
                || record.AppliedDamage != 0 || record.ActualHpRemoved != 0 || record.Critical
                || record.Substitute || record.FaintedTarget)
                throw new ArgumentException("Target-level failures require hit zero and no attempted damage.",
                    nameof(record));
            return;
        }
        if (record.HitNumber <= 0 || !record.Attempted)
            throw new ArgumentException("Resolved hits require a positive hit number and attempted=true.", nameof(record));
        if (record.Failure == BattleDamageFailure.None)
        {
            if (!record.Connected || record.ActualHpRemoved <= 0 || record.Substitute)
                throw new ArgumentException("Successful hits require positive creature HP removal.", nameof(record));
        }
        else if (record.Connected || record.ActualHpRemoved != 0 || record.FaintedTarget)
        {
            throw new ArgumentException("Failed hits cannot connect, remove creature HP, or faint the target.",
                nameof(record));
        }
        if (record.Substitute != (record.Failure == BattleDamageFailure.Substitute))
            throw new ArgumentException("Substitute flag and failure must agree.", nameof(record));
        if (record.Failure == BattleDamageFailure.Immune
            && (record.CalculatedDamage != 0 || record.AppliedDamage != 0 || record.Critical))
            throw new ArgumentException("Immune hits require zero damage and no critical result.", nameof(record));
    }

    private void ClearCreature(BattleHistoryOwner owner)
    {
        (BattleSide Side, int Party) key = Key(owner);
        _creatureStreaks.Remove(key);
        _lastResults.Remove(key);
    }

    private static (BattleSide Side, int Party) Key(BattleHistoryOwner owner) =>
        (owner.Side, owner.PartyIndex);

    private static int Increment(int value) => value == int.MaxValue ? value : value + 1;

    private static void ValidateOwner(BattleHistoryOwner owner)
    {
        if (!Enum.IsDefined(owner.Side) || owner.PartyIndex < 0 || owner.Slot.Side != owner.Side
            || owner.Slot.Position < 0)
            throw new ArgumentException("History owners require a valid side, party index, and same-side slot.",
                nameof(owner));
    }
}

public static class ActionHistoryFormulas
{
    public static bool HasPowerFormula(BattleMove move) => move.SecondaryEffects.Any(effect =>
        effect is ConsecutivePowerEffect or HistoryPowerEffect);

    public static int ConsecutivePower(int authoredPower, int priorCount, ConsecutivePowerMode mode,
        int step, int cap)
    {
        if (authoredPower <= 0 || priorCount < 0 || step <= 0 || cap < authoredPower)
            throw new ArgumentOutOfRangeException(nameof(authoredPower),
                "Consecutive formulas require positive power/step, a nonnegative count, and cap at least base power.");
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (mode == ConsecutivePowerMode.Linear)
            return (int)Math.Min(cap, authoredPower + (long)step * priorCount);
        if (step == 1)
            return authoredPower;

        int value = authoredPower;
        for (int i = 0; i < priorCount && value < cap; i++)
            value = value > cap / step ? cap : Math.Min(cap, checked(value * step));
        return value;
    }
}
