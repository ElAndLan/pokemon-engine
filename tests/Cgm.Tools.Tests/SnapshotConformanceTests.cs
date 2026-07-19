using Cgm.Core.Battle;
using Cgm.Core.Serialization;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class SnapshotConformanceTests
{
    public static IEnumerable<object[]> CertifiedRows() => Catalog().Entries
        .Where(entry => entry.TestIds.Any(id => id.StartsWith("SnapshotConformanceTests.Certified(",
            StringComparison.Ordinal)))
        .Select(entry => new object[] { entry.ReferenceKey });

    [Theory]
    [MemberData(nameof(CertifiedRows))]
    public void Certified(string referenceKey)
    {
        MoveConformanceRecord entry = Catalog().Entries.Single(row => row.ReferenceKey == referenceKey);
        BattleMove move = MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(referenceKey));
        Assert.Contains(move.SecondaryEffects, effect =>
            effect is DecoyEffect or TransformEffect or MoveReplaceEffect);
        Assert.Contains(entry.TestIds, id => id == $"SnapshotConformanceTests.Certified({referenceKey})");
    }

    [Fact]
    public void CertifiedCohortCoversDecoyTransformAndMoveReplacement()
    {
        MoveEffect[] effects = Catalog().Entries
            .Where(entry => entry.TestIds.Any(id => id.StartsWith("SnapshotConformanceTests.Certified(",
                StringComparison.Ordinal)))
            .Select(entry => MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(entry.ReferenceKey)))
            .SelectMany(move => move.SecondaryEffects).ToArray();

        Assert.Contains(effects, effect => effect is DecoyEffect);
        Assert.Contains(effects, effect => effect is TransformEffect);
        Assert.Contains(effects, effect => effect is MoveReplaceEffect);
    }

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
