using Cgm.Core.Battle;
using Cgm.Core.Serialization;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class AbilityMutationConformanceTests
{
    public static IEnumerable<object[]> CertifiedRows() => Catalog().Entries
        .Where(entry => entry.TestIds.Any(id => id.StartsWith("AbilityMutationConformanceTests.Certified(",
            StringComparison.Ordinal)))
        .Select(entry => new object[] { entry.ReferenceKey });

    [Theory]
    [MemberData(nameof(CertifiedRows))]
    public void Certified(string referenceKey)
    {
        MoveConformanceRecord entry = Catalog().Entries.Single(row => row.ReferenceKey == referenceKey);
        AbilityMutationEffect mutation = Assert.Single(MoveCompiler
            .ToBattleMove(entry.Mechanics.ToMove(referenceKey)).SecondaryEffects.OfType<AbilityMutationEffect>());
        Assert.Contains(mutation.Operation, new[]
        {
            BattleAbilityOperation.Copy,
            BattleAbilityOperation.Swap,
            BattleAbilityOperation.Replace,
            BattleAbilityOperation.Suppress,
        });
        Assert.Contains(entry.TestIds, id => id == $"AbilityMutationConformanceTests.Certified({referenceKey})");
    }

    [Fact]
    public void CertifiedCohortCoversAbilityMutationFamilies()
    {
        AbilityMutationEffect[] effects = Catalog().Entries
            .Where(entry => entry.TestIds.Any(id => id.StartsWith("AbilityMutationConformanceTests.Certified(",
                StringComparison.Ordinal)))
            .Select(entry => MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(entry.ReferenceKey)))
            .SelectMany(move => move.SecondaryEffects.OfType<AbilityMutationEffect>()).ToArray();

        Assert.Equal(7, effects.Length);
        Assert.Contains(effects, effect => effect.Operation == BattleAbilityOperation.Copy
            && effect.Subject == BattleAbilitySubject.UserAndAllies);
        Assert.Contains(effects, effect => effect.Operation == BattleAbilityOperation.Copy
            && effect.Source == BattleAbilitySubject.User && effect.Subject == BattleAbilitySubject.Target);
        Assert.Contains(effects, effect => effect.Operation == BattleAbilityOperation.Swap);
        Assert.Contains(effects, effect => effect.Operation == BattleAbilityOperation.Replace);
        Assert.Contains(effects, effect => effect.Operation == BattleAbilityOperation.Suppress);
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
