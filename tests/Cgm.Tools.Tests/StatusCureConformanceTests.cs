using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class StatusCureConformanceTests
{
    public static IEnumerable<object[]> CertifiedRows() => Catalog().Entries
        .Where(entry => entry.TestIds.Any(id => id.StartsWith("StatusCureConformanceTests.Certified(",
            StringComparison.Ordinal)))
        .Select(entry => new object[] { entry.ReferenceKey });

    [Theory]
    [MemberData(nameof(CertifiedRows))]
    public void Certified(string referenceKey)
    {
        MoveConformanceRecord entry = Catalog().Entries.Single(row => row.ReferenceKey == referenceKey);
        BattleMove move = MoveCompiler.ToBattleMove(entry.Mechanics.ToMove(referenceKey));
        StatusCureEffect cure = Assert.Single(move.SecondaryEffects.OfType<StatusCureEffect>());

        Assert.All(cure.Statuses, status => Assert.True(Enum.IsDefined(status)));
        Assert.Equal(move.DamageClass != DamageClass.Status, cure.RequireDamage);
        Assert.Equal(move.Target == MoveTarget.User ? HpFractionRecipient.Self : HpFractionRecipient.Target,
            cure.Recipient);
        if (move.SecondaryEffects.OfType<StatusPowerEffect>().SingleOrDefault() is { } formula)
            Assert.Equal([formula.Status!.Value], cure.Statuses);
        else if (move.Target == MoveTarget.User)
            Assert.Equal(
                [PersistentStatus.Burn, PersistentStatus.Paralysis, PersistentStatus.Poison, PersistentStatus.Toxic],
                cure.Statuses);
        else
            Assert.Equal([PersistentStatus.Burn], cure.Statuses);
        Assert.Contains("statusCure", entry.MechanicFamilies);
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
