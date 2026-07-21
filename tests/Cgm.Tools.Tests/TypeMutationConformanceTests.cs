using Cgm.Core.Battle;
using Cgm.Core.Serialization;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class TypeMutationConformanceTests
{
    public static IEnumerable<object[]> CertifiedRows() => Rows()
        .Select(entry => new object[] { entry.ReferenceKey });

    [Theory]
    [MemberData(nameof(CertifiedRows))]
    public void Certified(string referenceKey)
    {
        MoveConformanceRecord entry = Catalog().Entries.Single(row => row.ReferenceKey == referenceKey);
        BattleMove move = MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(referenceKey));

        Assert.Contains(move.SecondaryEffects, IsTypeEffect);
        Assert.Contains($"TypeMutationConformanceTests.Certified({referenceKey})", entry.TestIds);
    }

    [Fact]
    public void CertifiedCohortCoversEligibleTypeFamilies()
    {
        BattleMove[] moves = Rows()
            .Select(entry => MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(entry.ReferenceKey))).ToArray();
        TypeMutationEffect[] mutations = moves.SelectMany(move => move.SecondaryEffects
            .OfType<TypeMutationEffect>()).ToArray();

        Assert.Equal(11, moves.Length);
        Assert.Contains(mutations, effect => effect.Operation == BattleTypeOperation.Replace
            && effect.Source == BattleTypeSource.Environment);
        Assert.Contains(mutations, effect => effect.Operation == BattleTypeOperation.Replace
            && effect.Source == BattleTypeSource.FirstMove);
        Assert.Contains(mutations, effect => effect.Operation == BattleTypeOperation.Replace
            && effect.Source == BattleTypeSource.ResistantToLastDamage);
        Assert.Contains(mutations, effect => effect.Operation == BattleTypeOperation.Copy
            && effect.Source == BattleTypeSource.Target);
        Assert.Contains(mutations, effect => effect.Operation == BattleTypeOperation.Add);
        Assert.Contains(moves, move => move.SecondaryEffects.OfType<MoveTypeQueryEffect>().Any());
        Assert.Contains(moves, move => move.SecondaryEffects.OfType<MoveTypeOverrideEffect>()
            .Any(effect => effect.Subject == BattleTypeSubject.AllActive && effect.MatchType is not null));
        Assert.Contains(moves, move => move.SecondaryEffects.OfType<MoveGateEffect>()
            .Any(effect => effect.Kind == MoveGateKind.SourceBeforeTarget)
            && move.SecondaryEffects.OfType<MoveTypeOverrideEffect>().Any());
    }

    private static bool IsTypeEffect(MoveEffect effect) => effect is TypeRequireEffect or TypeMutationEffect
        or MoveTypeQueryEffect or MoveTypeOverrideEffect;

    private static IEnumerable<MoveConformanceRecord> Rows() => Catalog().Entries.Where(entry =>
        entry.TestIds.Any(id => id.StartsWith("TypeMutationConformanceTests.Certified(",
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
