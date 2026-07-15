using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation.Rules;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class PartyResourceFormulaConformanceTests
{
    private static MoveConformanceCatalog Catalog => CgmJson.Deserialize<MoveConformanceCatalog>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MoveConformance", "definitions.v1.json")));

    public static IEnumerable<object[]> CertifiedRecords() => Catalog.Entries
        .Where(entry => entry.TestIds.Any(id =>
            id.StartsWith("PartyResourceFormulaConformanceTests.", StringComparison.Ordinal)))
        .Select(entry => new object[] { entry });

    [Theory]
    [MemberData(nameof(CertifiedRecords))]
    public void Certified(MoveConformanceRecord record)
    {
        Move move = record.Mechanics.ToMove(record.ReferenceKey);
        Project project = new(new ProjectSettings { Name = "Party Resource Formula Conformance" },
            new Dictionary<EntityId, IEntity> { [move.Id] = move });
        Assert.Empty(new MoveRule().Check(project));
        BattleMove compiled = MoveCompiler.ToBattleMove(move);
        BattleCreature source = Creature("source", compiled, move.Type, friendship: 255);
        BattleCreature target = Creature("target", Inert(move.Type), move.Type);
        BattleCreature fainted = Creature("fainted", Inert(move.Type), move.Type);
        fainted.TakeDamage(fainted.MaxHp);
        foreach (StatKind stat in new[] { StatKind.Atk, StatKind.Def, StatKind.Spa, StatKind.Spd,
                     StatKind.Spe, StatKind.Accuracy, StatKind.Evasion })
        {
            source.SetStage(stat, 6);
            target.SetStage(stat, 6);
        }
        int? randomPower = compiled.SecondaryEffects.OfType<RandomTablePowerEffect>().SingleOrDefault() is { } random
            ? PartyResourceFormulas.ExpectedWeightedPower(random.Entries)
            : null;
        PartyResourceFormulaInputs inputs = PartyResourceFormulas.Inputs([source, fainted], source, target,
            compiled.Pp, compiled.Pp - 1, itemPower: 90, randomPower: randomPower);

        HpStatusPowerQuery query = HpStatusFormulas.PowerQuery(compiled, source, target, resourceInputs: inputs);
        int resolved = BattleQuery.ResolveInteger(BattleQueryId.BasePower, query.AuthoredBase, query.Modifiers);

        MoveEffect formula = Assert.Single(compiled.SecondaryEffects, effect => effect is
            PartyCountPowerEffect or FriendshipPowerEffect or PpPowerEffect or PositiveStagePowerEffect
            or ItemDataPowerEffect or RandomTablePowerEffect);
        int expected = formula switch
        {
            PartyCountPowerEffect party => PartyResourceFormulas.Linear(
                party.Filter == PartyMemberFilter.Fainted ? inputs.FaintedParty
                    : party.Filter == PartyMemberFilter.Living ? inputs.LivingParty : inputs.ContributingParty,
                party.Base, party.PerMember, party.Cap),
            FriendshipPowerEffect friendship => PartyResourceFormulas.FriendshipPower(
                inputs.Friendship, friendship.Mode),
            PpPowerEffect pp => PartyResourceFormulas.PpPower(
                pp.Timing == PpPowerTiming.BeforeSpend ? inputs.PpBeforeSpend : inputs.PpAfterSpend, pp.Bands),
            PositiveStagePowerEffect stages => PartyResourceFormulas.Linear(
                stages.Subject == StatusPowerSubject.User ? inputs.SourcePositiveStages : inputs.TargetPositiveStages,
                stages.Base, stages.PerStage, stages.Cap),
            ItemDataPowerEffect => inputs.ItemPower!.Value,
            RandomTablePowerEffect => inputs.RandomPower!.Value,
            _ => throw new InvalidOperationException(),
        };
        Assert.Equal(expected, resolved);
        Assert.Contains($"PartyResourceFormulaConformanceTests.Certified({record.ReferenceKey})", record.TestIds);
        Assert.Contains(record.MechanicFamilies, family => family is "partyCountPower" or "friendshipPower"
            or "ppPower" or "positiveStagePower" or "itemDataPower" or "randomTablePower");
    }

    private static BattleCreature Creature(string slug, BattleMove move, EntityId type, int friendship = 70) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [type],
            new Stats(1000, 100, 100, 100, 100, 100), [move], friendship: friendship);

    private static BattleMove Inert(EntityId type) =>
        new(EntityId.Parse("move:inert"), type, DamageClass.Status, null, null, 20, 0, 0);
}
