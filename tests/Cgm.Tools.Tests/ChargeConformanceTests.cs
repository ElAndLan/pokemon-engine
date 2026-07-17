using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation.Rules;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class ChargeConformanceTests
{
    private static MoveConformanceCatalog Catalog => CgmJson.Deserialize<MoveConformanceCatalog>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MoveConformance", "definitions.v1.json")));

    public static IEnumerable<object[]> CertifiedRecords() => Catalog.Entries
        .Where(entry => entry.TestIds.Any(id =>
            id.StartsWith("ChargeConformanceTests.", StringComparison.Ordinal)))
        .Select(entry => new object[] { entry });

    [Theory]
    [MemberData(nameof(CertifiedRecords))]
    public void Certified(MoveConformanceRecord record)
    {
        Move move = record.Mechanics.ToMove(record.ReferenceKey);
        Project project = new(new ProjectSettings { Name = "Charge Conformance" },
            new Dictionary<EntityId, IEntity> { [move.Id] = move });
        Assert.Empty(new MoveRule().Check(project));
        BattleMove compiled = MoveCompiler.ToBattleMove(move);
        TypeChart chart = new([new TypeDef { Id = move.Type }]);

        if (compiled.Charge is not null)
        {
            BattleCreature source = Creature("source", compiled, move.Type, 100);
            BattleCreature target = Creature("target", Inert(move.Type), move.Type, 1);
            var battle = new BattleController(source, target, chart, new Rng(1));

            IReadOnlyList<BattleEvent> charge = battle.ResolveTurn(new UseMove(0), new Pass());
            Assert.Contains(charge, e => e is Charging);
            Assert.Equal(compiled.MaxPp - 1, compiled.Pp);
            Assert.Single(battle.IntentQueueSnapshot);

            IReadOnlyList<BattleEvent> release = battle.ResolveTurn(new Pass(), new Pass());
            Assert.Contains(release, e => e is ChargeReleased);
            Assert.False(source.IsCharging);
            Assert.Equal(compiled.MaxPp - 1, compiled.Pp);
            Assert.Empty(battle.IntentQueueSnapshot);
        }

        if (compiled.SecondaryEffects.OfType<SemiInvulnerableHitEffect>().SingleOrDefault() is { } hit)
        {
            SemiInvulnerableState state = hit.States.Order().First();
            BattleMove chargeMove = new(EntityId.Parse("move:conformance_charge"), move.Type,
                DamageClass.Status, null, null, 10, 0, 0, target: MoveTarget.User,
                charge: new ChargeMoveEffect(state));
            BattleCreature target = Creature("target", chargeMove, move.Type, 100);
            BattleCreature source = Creature("source", compiled, move.Type, 1);
            var battle = new BattleController(target, source, chart, new Rng(1));
            int before = target.CurrentHp;

            IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

            Assert.DoesNotContain(events, e => e is SemiInvulnerableAvoided);
            Assert.True(move.DamageClass == DamageClass.Status || target.CurrentHp < before);
        }

        Assert.Contains($"ChargeConformanceTests.Certified({record.ReferenceKey})", record.TestIds);
        Assert.Contains(record.MechanicFamilies, family => family is "chargeTurn"
            or "chargeStartStat" or "semiInvulnerableHit");
    }

    private static BattleCreature Creature(string slug, BattleMove move, EntityId type, int speed) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [type],
            new Stats(1000, 100, 100, 100, 100, speed), [move]);

    private static BattleMove Inert(EntityId type) =>
        new(EntityId.Parse("move:inert"), type, DamageClass.Status, null, null, 20, 0, 0);
}
