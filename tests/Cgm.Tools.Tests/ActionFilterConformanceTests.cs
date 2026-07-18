using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation.Rules;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class ActionFilterConformanceTests
{
    public static IEnumerable<object[]> CertifiedRecords() => Catalog.Entries
        .Where(entry => entry.TestIds.Any(id => id.StartsWith("ActionFilterConformanceTests.", StringComparison.Ordinal)))
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
        ApplyActionFilterEffect effect = Assert.Single(move.SecondaryEffects.OfType<ApplyActionFilterEffect>());
        BattleConditionDefinition condition = ActionFilterConditions.For(effect.Filter, effect.MoveTag);
        Assert.Equal(effect.Filter, condition.ActionFilter!.Kind);
        Assert.Equal(effect.Duration, condition.DefaultDuration);
        Assert.Contains($"ActionFilterConformanceTests.Certified({record.ReferenceKey})", record.TestIds);
    }
}
