using Cgm.Core.Battle;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleQueryTests
{
    [Fact]
    public void Value_ReducesFractionsAndNormalizesZero()
    {
        Assert.Equal(new BattleQueryValue(2, 3), new BattleQueryValue(4, 6));
        Assert.Equal(new BattleQueryValue(0, 1), new BattleQueryValue(0, 99));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BattleQueryValue(1, 0));
    }

    [Fact]
    public void IntegerPipeline_AppliesStagesOperationsAndFloorsInLockedOrder()
    {
        BattleQueryModifier[] modifiers =
        [
            new(BattleQueryStage.Hooks, BattleQueryOperation.Multiply, new BattleQueryValue(1, 2),
                Priority: 0, OwnerScope: BattleQueryOwnerScope.Target, InsertionOrder: 3),
            new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Multiply, new BattleQueryValue(2, 3),
                InsertionOrder: 2),
            new(BattleQueryStage.SourceTargetState, BattleQueryOperation.Add, new BattleQueryValue(5),
                InsertionOrder: 1),
            new(BattleQueryStage.RulesetOverride, BattleQueryOperation.Max, new BattleQueryValue(10),
                InsertionOrder: 4),
        ];

        BattleQueryResult result = BattleQuery.Evaluate(BattleQueryId.BasePower, new BattleQueryValue(20), modifiers);

        // (20 + 5) * 2/3 => 16, then * 1/2 => 8, then max 10.
        Assert.Equal(10, result.FinalValue.ToInt32());
        Assert.Equal(
            [BattleQueryStage.MoveIdentity, BattleQueryStage.AuthoredBase, BattleQueryStage.SourceTargetState, BattleQueryStage.SourceTargetState,
                BattleQueryStage.Hooks, BattleQueryStage.RulesetOverride, BattleQueryStage.FinalClamp],
            result.Steps.Select(step => step.Stage));
    }

    [Fact]
    public void HookOrdering_IsPriorityThenScopeThenInsertionAndOnlyFirstReplaceWins()
    {
        BattleQueryModifier[] modifiers =
        [
            new(BattleQueryStage.Hooks, BattleQueryOperation.Replace, new BattleQueryValue(30), 1,
                BattleQueryOwnerScope.Source, 2),
            new(BattleQueryStage.Hooks, BattleQueryOperation.Replace, new BattleQueryValue(40), 2,
                BattleQueryOwnerScope.Field, 3),
            new(BattleQueryStage.Hooks, BattleQueryOperation.Replace, new BattleQueryValue(50), 2,
                BattleQueryOwnerScope.Source, 1),
        ];

        BattleQueryResult result = BattleQuery.Evaluate(BattleQueryId.BasePower, new BattleQueryValue(10), modifiers);
        BattleQueryStep[] replaces = result.Steps.Where(step => step.Operation == BattleQueryOperation.Replace).ToArray();

        Assert.Equal(50, result.FinalValue.ToInt32());
        Assert.Equal([1, 3, 2], replaces.Select(step => step.InsertionOrder!.Value));
        Assert.Equal([true, false, false], replaces.Select(step => step.Applied));
    }

    [Fact]
    public void FractionPipeline_RemainsExactAndReduced()
    {
        BattleQueryResult result = BattleQuery.Evaluate(BattleQueryId.Effectiveness, new BattleQueryValue(1, 2),
        [
            new(BattleQueryStage.Hooks, BattleQueryOperation.Add, new BattleQueryValue(1, 4), InsertionOrder: 0),
            new(BattleQueryStage.Hooks, BattleQueryOperation.Multiply, new BattleQueryValue(2, 3), InsertionOrder: 1),
        ]);

        Assert.Equal(new BattleQueryValue(1, 2), result.FinalValue);
        Assert.Equal(new BattleQueryValue(1), BattleQuery.Evaluate(BattleQueryId.CriticalChance,
            new BattleQueryValue(3, 2)).FinalValue);
    }

    [Theory]
    [InlineData(BattleQueryId.BasePower, -1, 1)]
    [InlineData(BattleQueryId.BasePower, 0, 1)]
    [InlineData(BattleQueryId.Accuracy, -1, 0)]
    [InlineData(BattleQueryId.Accuracy, 101, 100)]
    [InlineData(BattleQueryId.Healing, -1, 0)]
    [InlineData(BattleQueryId.Healing, 0, 0)]
    [InlineData(BattleQueryId.Priority, -99, -7)]
    [InlineData(BattleQueryId.Priority, 99, 7)]
    public void Registry_ClampsIntegerBoundaries(BattleQueryId query, int value, int expected)
    {
        Assert.Equal(expected, BattleQuery.ResolveInteger(query, value));
    }

    [Fact]
    public void InvalidInputs_AreRejectedBeforeEvaluation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleQuery.Spec((BattleQueryId)999));
        Assert.Throws<ArgumentException>(() => BattleQuery.Evaluate(BattleQueryId.BasePower, new BattleQueryValue(1, 2)));
        Assert.Throws<ArgumentException>(() => BattleQuery.Evaluate(BattleQueryId.Effectiveness, default));
        Assert.Throws<ArgumentException>(() => BattleQuery.Evaluate(BattleQueryId.BasePower, new BattleQueryValue(1),
        [
            new(BattleQueryStage.Hooks, BattleQueryOperation.Add, new BattleQueryValue(1), InsertionOrder: 0),
            new(BattleQueryStage.Hooks, BattleQueryOperation.Max, new BattleQueryValue(2), InsertionOrder: 0),
        ]));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleQuery.Evaluate(BattleQueryId.BasePower, new BattleQueryValue(1),
            [new(BattleQueryStage.AuthoredBase, BattleQueryOperation.Add, new BattleQueryValue(1))]));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleQuery.Evaluate(BattleQueryId.BasePower, new BattleQueryValue(1),
            [new(BattleQueryStage.Hooks, (BattleQueryOperation)999, new BattleQueryValue(1))]));
        Assert.Throws<ArgumentOutOfRangeException>(() => BattleQuery.Evaluate(BattleQueryId.BasePower, new BattleQueryValue(1),
            [new(BattleQueryStage.Hooks, BattleQueryOperation.Add, new BattleQueryValue(1),
                OwnerScope: (BattleQueryOwnerScope)999)]));
        Assert.Throws<ArgumentException>(() => BattleQuery.Evaluate(BattleQueryId.BasePower, new BattleQueryValue(1),
            [new(BattleQueryStage.Hooks, BattleQueryOperation.Multiply, default)]));
        Assert.Throws<ArgumentException>(() => BattleQuery.Evaluate(BattleQueryId.BasePower, new BattleQueryValue(1),
            [new(BattleQueryStage.Hooks, BattleQueryOperation.Add, new BattleQueryValue(1, 2))]));
        Assert.Throws<OverflowException>(() => BattleQuery.Evaluate(BattleQueryId.BasePower, new BattleQueryValue(int.MaxValue),
            [new(BattleQueryStage.Hooks, BattleQueryOperation.Multiply, new BattleQueryValue(long.MaxValue))]));
    }

    [Fact]
    public void EmptyModifiers_ReturnClampedBaseWithStableTrace()
    {
        BattleQueryResult result = BattleQuery.Evaluate(BattleQueryId.FinalDamage, new BattleQueryValue(42));

        Assert.Equal(42, result.FinalValue.ToInt32());
        Assert.Equal([BattleQueryStage.MoveIdentity, BattleQueryStage.AuthoredBase, BattleQueryStage.FinalClamp],
            result.Steps.Select(step => step.Stage));
    }

    [Fact]
    public void AccuracyStage_FloorsBeforeTheIntegerRollComparison()
    {
        var rng = new FakeRng(ints: [71]);

        Assert.False(BattleRolls.Hits(95, accuracyStage: -1, evasionStage: 0, rng));
    }

    [Fact]
    public void BlankRulesetContext_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => BattleQuery.Evaluate(BattleQueryId.Speed, new BattleQueryValue(10),
            context: new BattleQueryContext(Ruleset: " ")));
    }
}
