using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation.Rules;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class MoveReferenceConformanceTests
{
    public static IEnumerable<object[]> CertifiedRecords() => Catalog.Entries
        .Where(entry => entry.TestIds.Any(id => id.StartsWith(
            "MoveReferenceConformanceTests.", StringComparison.Ordinal)))
        .Select(entry => new object[] { entry });

    private static MoveConformanceCatalog Catalog => CgmJson.Deserialize<MoveConformanceCatalog>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MoveConformance", "definitions.v1.json")));

    [Theory]
    [MemberData(nameof(CertifiedRecords))]
    public void Certified(MoveConformanceRecord record)
    {
        Move definition = record.Mechanics.ToMove(record.ReferenceKey);
        Project project = new(new ProjectSettings { Name = "Conformance" },
            new Dictionary<EntityId, IEntity> { [definition.Id] = definition });
        Assert.Empty(new MoveRule().Check(project));
        BattleMove move = MoveCompiler.ToBattleMove(definition);
        string[] packageOps = record.Mechanics.Effects
            .Where(effect => effect.Op is "callMove" or "turnOrderIntent" or "pairedAction")
            .Select(effect => effect.Op).ToArray();
        Assert.Single(packageOps);
        Assert.True(packageOps[0] switch
        {
            "callMove" => move.SecondaryEffects.OfType<CallMoveEffect>().Count() == 1,
            "turnOrderIntent" => move.SecondaryEffects.OfType<TurnOrderIntentEffect>().Count() == 1,
            "pairedAction" => move.SecondaryEffects.OfType<PairedActionEffect>().Count() == 1
                && record.RequiredTopology == "doubles",
            _ => false,
        });
        Assert.Contains($"MoveReferenceConformanceTests.Certified({record.ReferenceKey})", record.TestIds);
    }
}
