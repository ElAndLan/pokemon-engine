using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation.Rules;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class DelayedActionConformanceTests
{
    private static MoveConformanceCatalog Catalog => CgmJson.Deserialize<MoveConformanceCatalog>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MoveConformance", "definitions.v1.json")));

    public static IEnumerable<object[]> CertifiedRecords() => Catalog.Entries
        .Where(entry => entry.TestIds.Any(id =>
            id.StartsWith("DelayedActionConformanceTests.", StringComparison.Ordinal)))
        .Select(entry => new object[] { entry });

    [Theory]
    [MemberData(nameof(CertifiedRecords))]
    public void Certified(MoveConformanceRecord record)
    {
        Move move = record.Mechanics.ToMove(record.ReferenceKey);
        Project project = new(new ProjectSettings { Name = "Delayed Action Conformance" },
            new Dictionary<EntityId, IEntity> { [move.Id] = move });
        Assert.Empty(new MoveRule().Check(project));
        BattleMove compiled = MoveCompiler.ToBattleMove(move);
        EntityId neutral = move.Type;
        TypeChart chart = new([new TypeDef { Id = neutral }]);

        if (compiled.SecondaryEffects.OfType<DelayedDamageEffect>().Any())
            AssertDelayedDamage(compiled, neutral, chart);
        else if (compiled.SecondaryEffects.OfType<DelayedHealEffect>().Any())
            AssertDelayedHeal(compiled, neutral, chart);
        else if (compiled.SecondaryEffects.OfType<DelayedStatusEffect>().Any())
            AssertDelayedStatus(compiled, neutral, chart);
        else
            AssertReplacementRestore(compiled, neutral, chart);

        Assert.Contains($"DelayedActionConformanceTests.Certified({record.ReferenceKey})", record.TestIds);
        Assert.Contains(record.MechanicFamilies, family => family is "delayedDamage"
            or "delayedHeal" or "delayedStatus" or "replacementRestore");
    }

    private static void AssertDelayedDamage(BattleMove move, EntityId type, TypeChart chart)
    {
        BattleCreature source = Creature("source", type, move);
        BattleCreature target = Creature("target", type, Inert(type));
        var battle = new BattleController(source, target, chart, new Rng(1));
        int before = target.CurrentHp;

        Assert.Contains(battle.ResolveTurn(new UseMove(0), new Pass()),
            battleEvent => battleEvent is DelayedActionQueued);
        battle.ResolveTurn(new Pass(), new Pass());
        Assert.Contains(battle.ResolveTurn(new Pass(), new Pass()),
            battleEvent => battleEvent is DelayedActionResolved);
        Assert.True(target.CurrentHp < before);
    }

    private static void AssertDelayedHeal(BattleMove move, EntityId type, TypeChart chart)
    {
        BattleCreature source = Creature("source", type, move);
        source.TakeDamage(source.MaxHp / 2);
        var battle = new BattleController(source, Creature("target", type, Inert(type)), chart, new Rng(1));
        int before = source.CurrentHp;

        battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Contains(battle.ResolveTurn(new Pass(), new Pass()),
            battleEvent => battleEvent is DelayedActionResolved);
        Assert.True(source.CurrentHp > before);
    }

    private static void AssertDelayedStatus(BattleMove move, EntityId type, TypeChart chart)
    {
        BattleCreature target = Creature("target", type, Inert(type));
        var battle = new BattleController(Creature("source", type, move), target, chart, new Rng(1));

        battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Contains(battle.ResolveTurn(new Pass(), new Pass()),
            battleEvent => battleEvent is DelayedActionResolved);
        Assert.Equal(PersistentStatus.Sleep, target.Status);
    }

    private static void AssertReplacementRestore(BattleMove move, EntityId type, TypeChart chart)
    {
        BattleCreature source = Creature("source", type, move);
        BattleCreature reserve = Creature("reserve", type, Inert(type));
        reserve.TakeDamage(reserve.MaxHp / 2);
        reserve.SetStatus(PersistentStatus.Poison);
        var battle = new BattleController([source, reserve], [Creature("target", type, Inert(type))],
            chart, new Rng(1));

        battle.ResolveTurn(new UseMove(0), new Pass());
        IReadOnlyList<BattleEvent> replacement = battle.ResolveReplacements(
            [new BattleReplacementSelection(new BattleSlot(BattleSide.Player, 0), 1)]);

        Assert.Contains(replacement, battleEvent => battleEvent is DelayedActionResolved);
        Assert.Equal(reserve.MaxHp, reserve.CurrentHp);
        Assert.Null(reserve.Status);
    }

    private static BattleCreature Creature(string slug, EntityId type, BattleMove move) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [type],
            new Stats(500, 100, 100, 100, 100, 100), [move]);

    private static BattleMove Inert(EntityId type) =>
        new(EntityId.Parse("move:inert"), type, DamageClass.Status, null, null, 20, 0, 0);
}
