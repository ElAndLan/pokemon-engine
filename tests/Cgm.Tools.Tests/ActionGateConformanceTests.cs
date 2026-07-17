using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation.Rules;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class ActionGateConformanceTests
{
    private static MoveConformanceCatalog Catalog => CgmJson.Deserialize<MoveConformanceCatalog>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MoveConformance", "definitions.v1.json")));

    public static IEnumerable<object[]> CertifiedRecords() => Catalog.Entries
        .Where(entry => entry.TestIds.Any(id =>
            id.StartsWith("ActionGateConformanceTests.", StringComparison.Ordinal)))
        .Select(entry => new object[] { entry });

    [Theory]
    [MemberData(nameof(CertifiedRecords))]
    public void Certified(MoveConformanceRecord record)
    {
        Move move = record.Mechanics.ToMove(record.ReferenceKey);
        Project project = new(new ProjectSettings { Name = "Action Gate Conformance" },
            new Dictionary<EntityId, IEntity> { [move.Id] = move });
        Assert.Empty(new MoveRule().Check(project));
        BattleMove compiled = MoveCompiler.ToBattleMove(move);
        if (compiled.SecondaryEffects.OfType<QueueActionGateEffect>().SingleOrDefault() is { } recharge)
        {
            Assert.Equal(new QueueActionGateEffect(1, QueueActionGateOwner.Creature), recharge);
            BattleCreature source = Creature("source", compiled, move.Type, 100);
            BattleCreature target = Creature("target", Inert(move.Type), move.Type, 1);
            var battle = new BattleController(source, target,
                new TypeChart([new TypeDef { Id = move.Type }]), new Rng(1));
            battle.ResolveTurn(new UseMove(0), new Pass());
            Assert.Single(battle.ResolveTurn(new UseMove(0), new Pass()).OfType<ActionSkipped>());
        }
        else
        {
            MoveGateEffect[] gates = compiled.SecondaryEffects.OfType<MoveGateEffect>().ToArray();
            Assert.NotEmpty(gates);
            BattleCreature source = Creature("source", compiled, move.Type, 100);
            var inputs = new BattleMoveGateInputs(SourceBeforeTarget: true,
                TargetPlannedMoveClass: DamageClass.Physical, MatchingDamageReceived: true);
            foreach (MoveGateEffect gate in gates)
            {
                BattleMoveGateInputs eligible = gate is
                    { Kind: MoveGateKind.DamageReceived, DamageMode: MoveGateDamageMode.Forbid }
                    ? inputs with { MatchingDamageReceived = false }
                    : inputs;
                Assert.Null(BattleActionGates.Failure(compiled, source, gate, eligible));
            }
        }
        Assert.Contains($"ActionGateConformanceTests.Certified({record.ReferenceKey})", record.TestIds);
        Assert.Contains(record.MechanicFamilies, family => family is "recharge" or "moveGate");
    }

    private static BattleCreature Creature(string slug, BattleMove move, EntityId type, int speed) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [type],
            new Stats(1000, 100, 100, 100, 100, speed), [move]);

    private static BattleMove Inert(EntityId type) =>
        new(EntityId.Parse("move:inert"), type, DamageClass.Status, null, null, 20, 0, 0);
}
