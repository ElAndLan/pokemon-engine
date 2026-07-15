using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleDamageHistoryTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Move = EntityId.Parse("move:probe");
    private static readonly BattleHistoryOwner Source = Owner(BattleSide.Player, 0, 0);
    private static readonly BattleHistoryOwner Target = Owner(BattleSide.Enemy, 0, 0);

    [Fact]
    public void RecordsAndQueries_ReturnStableCopiesAndExactTotals()
    {
        (BattleActionHistory history, BattleActionAttemptId attempt) = StartedHistory();
        history.RecordDamage(Record(attempt, hit: 2, calculated: 20, applied: 20, actual: 10));
        history.RecordDamage(Record(attempt, hit: 1, calculated: 30, applied: 25, actual: 25));

        BattleDamageRecord[] snapshot = [.. history.DamageSnapshot()];
        Assert.Equal([1, 2], snapshot.Select(record => record.HitNumber));
        Assert.Equal(35, history.TotalActualDamageTo(Target, 0));
        Assert.Equal(2, history.DamageFrom(Source, 0).Count);
        Assert.Equal(2, history.DamageTo(Target with { Slot = new(BattleSide.Enemy, 1) }, 0).Count);
        Assert.Equal(2, history.DamageFrom(Source with { Slot = new(BattleSide.Player, 1) }, 0).Count);
        snapshot[0] = snapshot[0] with { ActualHpRemoved = 1 };
        Assert.Equal(25, history.DamageSnapshot()[0].ActualHpRemoved);

        history.Complete(attempt, BattleActionResult.Connected, [Target]);
        history.RecordDamage(Record(attempt, hit: 3, calculated: 1, applied: 1, actual: 1));
        Assert.Equal(3, history.DamageSnapshot().Count);
    }

    [Fact]
    public void Records_AgeAfterPreviousTurnAndClearAtBattleEnd()
    {
        var history = new BattleActionHistory();
        AddTurn(history, 0, 10);
        AddTurn(history, 1, 20);
        Assert.Equal([0, 1], history.DamageSnapshot().Select(record => record.Attempt.Turn));
        AddTurn(history, 2, 30);
        Assert.Equal([1, 2], history.DamageSnapshot().Select(record => record.Attempt.Turn));

        history.RecordSwitch(Source, Owner(BattleSide.Player, 1, 0));
        history.RecordFaint(Target);
        Assert.Equal(2, history.DamageSnapshot().Count);
        history.EndBattle();
        Assert.Empty(history.DamageSnapshot());
    }

    [Theory]
    [InlineData(BattleDamageFailure.Missed)]
    [InlineData(BattleDamageFailure.Protected)]
    [InlineData(BattleDamageFailure.NoQualifyingDamage)]
    public void TargetLevelFailures_AreTypedHitZero(BattleDamageFailure failure)
    {
        (BattleActionHistory history, BattleActionAttemptId attempt) = StartedHistory();
        history.RecordDamage(Record(attempt, hit: 0, attempted: false, connected: false, failure: failure,
            calculated: 0, applied: 0, actual: 0));
        Assert.Equal(failure, Assert.Single(history.DamageSnapshot()).Failure);
    }

    [Theory]
    [InlineData(BattleDamageFailure.Immune, false)]
    [InlineData(BattleDamageFailure.NoDamage, false)]
    [InlineData(BattleDamageFailure.Substitute, true)]
    public void ResolvedNonConnections_AreTypedPositiveHits(BattleDamageFailure failure, bool substitute)
    {
        (BattleActionHistory history, BattleActionAttemptId attempt) = StartedHistory();
        history.RecordDamage(Record(attempt, failure: failure, connected: false, substitute: substitute,
            calculated: failure == BattleDamageFailure.Substitute ? 30 : 0,
            applied: failure == BattleDamageFailure.Substitute ? 30 : 0, actual: 0));
        Assert.Equal((failure, substitute), (Assert.Single(history.DamageSnapshot()).Failure, substitute));
    }

    [Fact]
    public void Validation_RejectsForeignDuplicateMalformedAndInconsistentRecords()
    {
        (BattleActionHistory history, BattleActionAttemptId attempt) = StartedHistory();
        BattleDamageRecord valid = Record(attempt);
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid with { Attempt = new(1, 1) }));
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid with { Attempt = new(0, 9) }));
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid with { Source = Owner(BattleSide.Player, 1, 0) }));
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid with { Move = EntityId.Parse("move:other") }));
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid with { DamageType = EntityId.Parse("move:bad") }));
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid with { DamageClass = DamageClass.Status }));
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid with { Cause = (BattleDamageCause)99 }));
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid with { CalculatedDamage = -1 }));
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid with { AppliedDamage = 41 }));
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid with { ActualHpRemoved = 31 }));
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid with { Connected = false }));
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid with { Failure = BattleDamageFailure.Immune }));
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid with { FaintedTarget = true, Connected = false }));
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid with
        {
            HitNumber = 0, Attempted = false, Connected = false, Failure = BattleDamageFailure.Missed,
            CalculatedDamage = 1, AppliedDamage = 0, ActualHpRemoved = 0,
        }));
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid with { HitNumber = 0 }));
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid with
        {
            Connected = false, Failure = BattleDamageFailure.Substitute, ActualHpRemoved = 0,
        }));
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid with
        {
            Connected = false, Failure = BattleDamageFailure.Immune, CalculatedDamage = 1,
            AppliedDamage = 0, ActualHpRemoved = 0,
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => history.DamageTo(Target, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => history.DamageFrom(Source, -1));
        history.RecordDamage(valid);
        Assert.Throws<ArgumentException>(() => history.RecordDamage(valid));
    }

    [Fact]
    public void HpFormula_AllowsEffectiveStatusClassAndTotalsOverflowChecked()
    {
        (BattleActionHistory history, BattleActionAttemptId attempt) = StartedHistory();
        history.RecordDamage(Record(attempt, hit: 1, calculated: int.MaxValue, applied: int.MaxValue,
            actual: int.MaxValue, damageClass: DamageClass.Status, cause: BattleDamageCause.HpFormula));
        history.RecordDamage(Record(attempt, hit: 2, calculated: 1, applied: 1, actual: 1,
            damageClass: DamageClass.Status, cause: BattleDamageCause.HpFormula));
        Assert.Throws<OverflowException>(() => history.TotalActualDamageTo(Target, 0));
    }

    private static void AddTurn(BattleActionHistory history, int turn, int damage)
    {
        history.BeginTurn(turn, [new(Source, BattlePlannedActionKind.Move)]);
        BattleActionAttemptId attempt = history.BeginMove(1, Source, Move);
        history.MarkStarted(attempt);
        history.RecordDamage(Record(attempt, calculated: damage, applied: damage, actual: damage));
        history.Complete(attempt, BattleActionResult.Connected, [Target]);
    }

    private static (BattleActionHistory History, BattleActionAttemptId Attempt) StartedHistory()
    {
        var history = new BattleActionHistory();
        history.BeginTurn(0, [new(Source, BattlePlannedActionKind.Move)]);
        BattleActionAttemptId attempt = history.BeginMove(1, Source, Move);
        history.MarkStarted(attempt);
        return (history, attempt);
    }

    private static BattleDamageRecord Record(BattleActionAttemptId attempt, int hit = 1,
        bool attempted = true, bool connected = true, BattleDamageFailure failure = BattleDamageFailure.None,
        int calculated = 40, int applied = 30, int actual = 30, bool substitute = false,
        DamageClass damageClass = DamageClass.Physical, BattleDamageCause cause = BattleDamageCause.Standard) =>
        new(attempt, Source, Target, Move, damageClass, Normal, cause, hit, attempted, connected, failure,
            calculated, applied, actual, Critical: false, Contact: true, Substitute: substitute, FaintedTarget: false);

    private static BattleHistoryOwner Owner(BattleSide side, int party, int position) =>
        new(side, party, new BattleSlot(side, position));
}
