using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class DecoyConformanceTests
{
    public static IEnumerable<object[]> CertifiedRows() => Rows()
        .Select(entry => new object[] { entry.ReferenceKey });

    [Theory]
    [MemberData(nameof(CertifiedRows))]
    public void Certified(string referenceKey)
    {
        MoveConformanceRecord entry = Catalog().Entries.Single(row => row.ReferenceKey == referenceKey);
        BattleMove move = MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(referenceKey));

        Assert.Equal(new DecoyEffect(new Fraction(1, 4)),
            Assert.Single(move.SecondaryEffects.OfType<DecoyEffect>()));
        Assert.Equal(MoveTarget.User, move.Target);
        Assert.Contains($"DecoyConformanceTests.Certified({referenceKey})", entry.TestIds);
    }

    [Fact]
    public void CertifiedCohortIsTheSingleEligibleDecoyDefinition()
    {
        MoveConformanceRecord row = Assert.Single(Rows());
        Assert.Equal("move-0164", row.ReferenceKey);
        Assert.Equal(["decoy", "targetTopology"], row.MechanicFamilies);
    }

    private static IEnumerable<MoveConformanceRecord> Rows() => Catalog().Entries.Where(entry =>
        entry.TestIds.Any(id => id.StartsWith("DecoyConformanceTests.Certified(",
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
