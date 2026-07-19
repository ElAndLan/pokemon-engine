using Cgm.Core.Battle;
using Cgm.Core.Serialization;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class TypeMutationConformanceTests
{
    public static IEnumerable<object[]> CertifiedRows() => Catalog().Entries
        .Where(entry => entry.TestIds.Any(id => id.StartsWith("TypeMutationConformanceTests.Certified(",
            StringComparison.Ordinal)))
        .Select(entry => new object[] { entry.ReferenceKey });

    [Theory]
    [MemberData(nameof(CertifiedRows))]
    public void Certified(string referenceKey)
    {
        MoveConformanceRecord entry = Catalog().Entries.Single(row => row.ReferenceKey == referenceKey);
        TypeMutationEffect mutation = Assert.Single(MoveCompiler
            .ToBattleMove(entry.Mechanics.ToMove(referenceKey)).SecondaryEffects.OfType<TypeMutationEffect>());
        Assert.Contains(mutation.Operation, new[]
        {
            BattleTypeOperation.Replace,
            BattleTypeOperation.Add,
            BattleTypeOperation.Remove,
            BattleTypeOperation.Copy,
        });
        Assert.Contains(entry.TestIds, id => id == $"TypeMutationConformanceTests.Certified({referenceKey})");
    }

    [Fact]
    public void CertifiedCohortCoversTypeMutationOperations()
    {
        TypeMutationEffect[] effects = Catalog().Entries
            .Where(entry => entry.TestIds.Any(id => id.StartsWith("TypeMutationConformanceTests.Certified(",
                StringComparison.Ordinal)))
            .Select(entry => MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(entry.ReferenceKey)))
            .SelectMany(move => move.SecondaryEffects.OfType<TypeMutationEffect>()).ToArray();

        Assert.Equal(5, effects.Length);
        Assert.Contains(effects, effect => effect.Operation == BattleTypeOperation.Replace
            && effect.Subject == BattleTypeSubject.Target);
        Assert.Contains(effects, effect => effect.Operation == BattleTypeOperation.Add
            && effect.Subject == BattleTypeSubject.Target);
        Assert.Contains(effects, effect => effect.Operation == BattleTypeOperation.Copy
            && effect.Source == BattleTypeSubject.Target);
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
