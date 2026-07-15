using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation.Rules;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class ActionHistoryFormulaConformanceTests
{
    private static MoveConformanceCatalog Catalog => CgmJson.Deserialize<MoveConformanceCatalog>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MoveConformance", "definitions.v1.json")));

    public static IEnumerable<object[]> CertifiedRecords() => Catalog.Entries
        .Where(entry => entry.TestIds.Any(id =>
            id.StartsWith("ActionHistoryFormulaConformanceTests.", StringComparison.Ordinal)))
        .Select(entry => new object[] { entry });

    [Theory]
    [MemberData(nameof(CertifiedRecords))]
    public void Certified(MoveConformanceRecord record)
    {
        Move move = record.Mechanics.ToMove(record.ReferenceKey);
        Project project = new(new ProjectSettings { Name = "Action History Formula Conformance" },
            new Dictionary<EntityId, IEntity> { [move.Id] = move });
        Assert.Empty(new MoveRule().Check(project));
        BattleMove compiled = MoveCompiler.ToBattleMove(move);
        HistoryPowerEffect formula = Assert.Single(compiled.SecondaryEffects.OfType<HistoryPowerEffect>());
        BattleActionFormulaInputs inputs = Inputs(formula.Condition, compiled.Move);
        BattleCreature source = Creature("source", compiled, move.Type);
        BattleCreature target = Creature("target", Inert(move.Type), move.Type);

        HpStatusPowerQuery query = HpStatusFormulas.PowerQuery(compiled, source, target, actionInputs: inputs);
        int resolved = BattleQuery.ResolveInteger(BattleQueryId.BasePower, query.AuthoredBase, query.Modifiers);

        Assert.Equal(checked(compiled.Power!.Value * formula.Multiplier.Num / formula.Multiplier.Den), resolved);
        Assert.Contains($"ActionHistoryFormulaConformanceTests.Certified({record.ReferenceKey})", record.TestIds);
        Assert.Contains("historyPower", record.MechanicFamilies);
    }

    private static BattleActionFormulaInputs Inputs(HistoryPowerCondition condition, EntityId move)
    {
        var history = new BattleActionHistory();
        var source = new BattleHistoryOwner(BattleSide.Player, 0, new BattleSlot(BattleSide.Player, 0));
        var ally = new BattleHistoryOwner(BattleSide.Player, 1, new BattleSlot(BattleSide.Player, 1));
        var target = new BattleHistoryOwner(BattleSide.Enemy, 0, new BattleSlot(BattleSide.Enemy, 0));
        BattleActionPlan[] plans = [new(source, BattlePlannedActionKind.Move), new(target, BattlePlannedActionKind.Move)];
        history.BeginTurn(0, plans);
        if (condition == HistoryPowerCondition.SourceAfterTarget)
            Complete(history, 1, target, EntityId.Parse("move:inert"), BattleActionResult.Succeeded, source);
        else if (condition == HistoryPowerCondition.PreviousActionFailed)
        {
            Complete(history, 1, source, move, BattleActionResult.Failed, target);
            history.BeginTurn(1, plans);
        }
        else if (condition == HistoryPowerCondition.AllyFaintedPreviousTurn)
        {
            history.RecordFaint(ally);
            history.BeginTurn(1, plans);
        }
        else
            throw new InvalidOperationException($"Unexpected certified history condition {condition}.");
        return history.PowerInputs(source, target, move);
    }

    private static void Complete(BattleActionHistory history, int sequence, BattleHistoryOwner source, EntityId move,
        BattleActionResult result, BattleHistoryOwner target)
    {
        BattleActionAttemptId id = history.BeginMove(sequence, source, move);
        if (result == BattleActionResult.Succeeded)
            history.MarkStarted(id);
        history.Complete(id, result, [target]);
    }

    private static BattleCreature Creature(string slug, BattleMove move, EntityId type) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [type],
            new Stats(1000, 100, 100, 100, 100, 100), [move]);

    private static BattleMove Inert(EntityId type) =>
        new(EntityId.Parse("move:inert"), type, DamageClass.Status, null, null, 20, 0, 0);
}
