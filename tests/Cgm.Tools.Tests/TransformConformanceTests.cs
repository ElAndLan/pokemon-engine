using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class TransformConformanceTests
{
    public static IEnumerable<object[]> CertifiedRows() => Rows()
        .Select(entry => new object[] { entry.ReferenceKey });

    [Theory]
    [MemberData(nameof(CertifiedRows))]
    public void Certified(string referenceKey)
    {
        MoveConformanceRecord entry = Catalog().Entries.Single(row => row.ReferenceKey == referenceKey);
        BattleMove move = MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(referenceKey));

        Assert.IsType<TransformEffect>(Assert.Single(move.SecondaryEffects));
        Assert.Equal(MoveTarget.Selected, move.Target);
        Assert.Equal(DamageClass.Status, move.DamageClass);
        Assert.Contains($"TransformConformanceTests.Certified({referenceKey})", entry.TestIds);
    }

    [Fact]
    public void CertifiedCohortIsTheSingleEligibleTransformDefinition()
    {
        MoveConformanceRecord row = Assert.Single(Rows());
        Assert.Equal("move-0144", row.ReferenceKey);
        Assert.Equal(["targetTopology", "transform"], row.MechanicFamilies);
    }

    private static IEnumerable<MoveConformanceRecord> Rows() => Catalog().Entries.Where(entry =>
        entry.TestIds.Any(id => id.StartsWith("TransformConformanceTests.Certified(",
            StringComparison.Ordinal)));

    private static MoveConformanceCatalog Catalog() => CgmJson.Deserialize<MoveConformanceCatalog>(
        File.ReadAllText(Path.Combine(RepoRoot(), "docs", "move-conformance", "definitions.v1.json")));

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CreatureGameMaker.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
