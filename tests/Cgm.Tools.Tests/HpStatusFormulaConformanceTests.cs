using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation.Rules;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class HpStatusFormulaConformanceTests
{
    private static MoveConformanceCatalog Catalog => CgmJson.Deserialize<MoveConformanceCatalog>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MoveConformance", "definitions.v1.json")));

    public static IEnumerable<object[]> CertifiedRecords() => Catalog.Entries
        .Where(entry => entry.TestIds.Any(id => id.StartsWith("HpStatusFormulaConformanceTests.", StringComparison.Ordinal)))
        .Select(entry => new object[] { entry });

    [Theory]
    [MemberData(nameof(CertifiedRecords))]
    public void Certified(MoveConformanceRecord record)
    {
        Move move = record.Mechanics.ToMove(record.ReferenceKey);
        Project project = new(new ProjectSettings { Name = "Formula Conformance" },
            new Dictionary<EntityId, IEntity> { [move.Id] = move });
        Assert.Empty(new MoveRule().Check(project));
        BattleMove compiled = MoveCompiler.ToBattleMove(move);
        EntityId type = move.Type;
        BattleCreature source = Creature("source", compiled, type, 200, 1000);
        BattleCreature target = Creature("target", Inert(type), type, 200, 100);
        source.TakeDamage(100);
        target.TakeDamage(100);
        if (compiled.SecondaryEffects.OfType<HpEqualizeEffect>().Any())
            source.TakeDamage(50);
        PrepareStatuses(compiled, source, target);
        var battle = new BattleController(source, target, new TypeChart([new TypeDef { Id = type }]), new MaxDamageRng());

        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(new UseMove(0), new UseMove(0));

        AssertFormula(compiled, source, target, battle, events);
        Assert.Contains($"HpStatusFormulaConformanceTests.Certified({record.ReferenceKey})", record.TestIds);
        Assert.Contains(record.MechanicFamilies, family => IsFormula(family));
    }

    private static void PrepareStatuses(BattleMove move, BattleCreature source, BattleCreature target)
    {
        if (move.SecondaryEffects.OfType<StatusPowerEffect>().SingleOrDefault() is not { } formula)
            return;
        BattleCreature subject = formula.Subject == StatusPowerSubject.User ? source : target;
        subject.SetStatus(formula.Status ?? PersistentStatus.Burn);
    }

    private static void AssertFormula(BattleMove move, BattleCreature source, BattleCreature target,
        BattleController battle, IReadOnlyList<BattleEvent> events)
    {
        BattleQueryResult? power = battle.QueryTrace
            .Where(entry => entry.Result.Query == BattleQueryId.BasePower)
            .Select(entry => entry.Result)
            .FirstOrDefault();
        if (move.HpBandPower is not null)
            Assert.Equal(40, power!.FinalValue.ToInt32());
        else if (move.HpRatioPower is { Scale: { } })
            Assert.Equal(61, power!.FinalValue.ToInt32());
        else if (move.HpRatioPower is not null)
            Assert.Equal(move.Power!.Value / 2, power!.FinalValue.ToInt32());
        else if (move.TargetHpThresholdPower is not null || move.SecondaryEffects.OfType<StatusPowerEffect>().Any())
            Assert.Equal(move.Power!.Value * 2, power!.FinalValue.ToInt32());

        if (move.SecondaryEffects.OfType<HpFractionEffect>().SingleOrDefault() is { } fraction)
        {
            int expected = 100 - Math.Max(1, 100 * fraction.Fraction.Num / fraction.Fraction.Den);
            Assert.Equal(expected, target.CurrentHp);
            Assert.Contains(events, item => item is HpFractionDamaged { Slot.Side: BattleSide.Enemy });
        }
        if (move.SecondaryEffects.OfType<HpEqualizeEffect>().SingleOrDefault() is { } equalize)
        {
            Assert.Equal(equalize.Mode == HpEqualizeMode.Average ? 75 : source.CurrentHp, target.CurrentHp);
            Assert.Contains(events, item => item is HpFormulaChanged { Slot.Side: BattleSide.Enemy });
        }
        if (move.SecondaryEffects.OfType<CannotKoEffect>().Any()
            && !move.SecondaryEffects.OfType<HpFractionEffect>().Any())
        {
            Assert.Equal(1, target.CurrentHp);
            Assert.DoesNotContain(events, item => item is Fainted { Slot.Side: BattleSide.Enemy });
        }
    }

    private static bool IsFormula(string family) => family is "targetHpThresholdPower" or "hpRatioPower"
        or "hpBandPower" or "statusPower" or "statusCountPower" or "hpFraction" or "hpEqualize"
        or "cannotKo" or "statusChance";

    private static BattleCreature Creature(string slug, BattleMove move, EntityId type, int hp, int attack) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [type], new Stats(hp, attack, 100, attack, 100, 100), [move]);

    private static BattleMove Inert(EntityId type) =>
        new(EntityId.Parse("move:inert"), type, DamageClass.Status, null, null, 20, 0, 0);

    private sealed class MaxDamageRng : IRng
    {
        public int Next(int maxExclusive) => maxExclusive == 16 ? 15 : 0;
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
        public double NextDouble() => 0.99;
    }
}
