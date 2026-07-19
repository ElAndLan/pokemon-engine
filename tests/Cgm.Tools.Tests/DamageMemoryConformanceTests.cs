using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class DamageMemoryConformanceTests
{
    public static IEnumerable<object[]> CertifiedRows() => Catalog().Entries
        .Where(entry => entry.TestIds.Any(id => id.StartsWith("DamageMemoryConformanceTests.Certified(",
            StringComparison.Ordinal)))
        .Select(entry => new object[] { entry.ReferenceKey });

    [Theory]
    [MemberData(nameof(CertifiedRows))]
    public void Certified(string referenceKey)
    {
        MoveConformanceRecord entry = Catalog().Entries.Single(row => row.ReferenceKey == referenceKey);
        BattleMove move = MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(referenceKey));
        Assert.True(move.CounterCategory is not null || move.SecondaryEffects.OfType<RevengeDamageEffect>().Any());
        Assert.Contains(entry.TestIds, id => id == $"DamageMemoryConformanceTests.Certified({referenceKey})");
    }

    [Fact]
    public void CertifiedCohortCoversCounterMirrorCoatAndRevenge()
    {
        BattleMove[] moves = Catalog().Entries
            .Where(entry => entry.TestIds.Any(id => id.StartsWith("DamageMemoryConformanceTests.Certified(",
                StringComparison.Ordinal)))
            .Select(entry => MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(entry.ReferenceKey))).ToArray();

        Assert.Contains(moves, move => move.CounterCategory == DamageClass.Physical);
        Assert.Contains(moves, move => move.CounterCategory == DamageClass.Special);
        Assert.Contains(moves, move => move.SecondaryEffects.OfType<RevengeDamageEffect>().Any());
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
