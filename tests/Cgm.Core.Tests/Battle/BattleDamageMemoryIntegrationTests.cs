using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattleDamageMemoryIntegrationTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId Ghost = EntityId.Parse("type:ghost");

    [Fact]
    public void StandardHit_RecordsCompleteCriticalContactAndFaintEvidence()
    {
        BattleMove move = Damage("impact", power: 200, critStage: 4, contact: true);
        BattleController battle = Singles(Creature("source", 100, 100, move),
            Creature("target", 20, 1, Wait()), new FakeRng(ints: [15], doubles: [0.0]));

        battle.ResolveTurn(new UseMove(0), new Pass());

        BattleDamageRecord record = Assert.Single(battle.ActionHistory.DamageSnapshot());
        Assert.Equal((BattleDamageCause.Standard, 1, true, true, BattleDamageFailure.None),
            (record.Cause, record.HitNumber, record.Attempted, record.Connected, record.Failure));
        Assert.Equal((DamageClass.Physical, Normal, move.Move),
            (record.DamageClass, record.DamageType, record.Move));
        Assert.True(record.Critical);
        Assert.True(record.Contact);
        Assert.True(record.FaintedTarget);
        Assert.True(record.CalculatedDamage >= record.AppliedDamage);
        Assert.Equal(20, record.ActualHpRemoved);
        Assert.Equal(0, record.Target.PartyIndex);
    }

    [Fact]
    public void SurvivalAndOverkill_DistinguishCalculatedAppliedAndActualDamage()
    {
        BattleMove move = new(EntityId.Parse("move:fixed"), Normal, DamageClass.Physical,
            null, null, 10, 0, 0, fixedDamage: 999);
        BattleCreature target = Creature("target", 200, 1, Wait(),
            heldEffects: [new Effect { Op = "surviveFromFull" }]);
        BattleController battle = Singles(Creature("source", 100, 100, move), target, new FakeRng());
        battle.ResolveTurn(new UseMove(0), new Pass());

        BattleDamageRecord survived = Assert.Single(battle.ActionHistory.DamageSnapshot());
        Assert.Equal((999, 199, 199),
            (survived.CalculatedDamage, survived.AppliedDamage, survived.ActualHpRemoved));

        battle = Singles(Creature("source2", 100, 100, move), Creature("target2", 30, 1, Wait()),
            new FakeRng());
        battle.ResolveTurn(new UseMove(0), new Pass());
        BattleDamageRecord overkill = Assert.Single(battle.ActionHistory.DamageSnapshot());
        Assert.Equal((999, 999, 30),
            (overkill.CalculatedDamage, overkill.AppliedDamage, overkill.ActualHpRemoved));
    }

    [Fact]
    public void MissProtectAndImmunity_RecordDistinctNonConnectionsWithoutStatusNoise()
    {
        BattleMove miss = new(EntityId.Parse("move:miss"), Normal, DamageClass.Physical,
            40, 1, 10, 0, 0);
        BattleController battle = Singles(Creature("source", 100, 100, miss),
            Creature("target", 100, 1, Wait()), new FakeRng(ints: [99]));
        battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Equal((0, false, BattleDamageFailure.Missed), Fields(Assert.Single(battle.ActionHistory.DamageSnapshot())));

        BattleMove protect = new(EntityId.Parse("move:protect"), Normal, DamageClass.Status,
            null, null, 10, 4, 0, isProtect: true);
        battle = Singles(Creature("source2", 100, 100, Damage("hit")),
            Creature("target2", 100, 1, protect), new FakeRng(doubles: [0.0]));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Equal((0, false, BattleDamageFailure.Protected), Fields(Assert.Single(battle.ActionHistory.DamageSnapshot())));

        battle = Singles(Creature("source3", 100, 100, Damage("immune")),
            Creature("target3", 100, 1, Wait(), type: Ghost), new FakeRng());
        battle.ResolveTurn(new UseMove(0), new Pass());
        BattleDamageRecord immune = Assert.Single(battle.ActionHistory.DamageSnapshot());
        Assert.Equal((1, true, BattleDamageFailure.Immune), Fields(immune));
        Assert.Equal((0, 0, 0), (immune.CalculatedDamage, immune.AppliedDamage, immune.ActualHpRemoved));

        BattleMove fixedMiss = new(EntityId.Parse("move:fixed_miss"), Normal, DamageClass.Physical,
            null, 1, 10, 0, 0, fixedDamage: 20);
        battle = Singles(Creature("source4", 100, 100, fixedMiss),
            Creature("target4", 100, 1, Wait()), new FakeRng(ints: [99]));
        battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Equal(BattleDamageCause.Fixed, Assert.Single(battle.ActionHistory.DamageSnapshot()).Cause);
    }

    [Fact]
    public void MultiHit_RecordsOneOrderedEntryPerResolvedHitAndStopsAtFaint()
    {
        BattleMove move = new(EntityId.Parse("move:multi"), Normal, DamageClass.Physical,
            20, null, 10, 0, 0, multiHitMin: 2, multiHitMax: 2);
        BattleController battle = Singles(Creature("source", 200, 100, move),
            Creature("target", 20, 1, Wait()), new FakeRng(ints: [0, 15, 15], doubles: [0.99, 0.99]));
        battle.ResolveTurn(new UseMove(0), new Pass());

        BattleDamageRecord[] records = [.. battle.ActionHistory.DamageSnapshot()];
        Assert.Equal([1, 2], records.Select(record => record.HitNumber));
        Assert.True(records[^1].FaintedTarget);
        Assert.Equal(20, records.Sum(record => record.ActualHpRemoved));
    }

    [Fact]
    public void DoublesSpread_RecordsTopologyOrderAndStableCreatureOwners()
    {
        BattleMove spread = new(EntityId.Parse("move:spread"), Normal, DamageClass.Special,
            80, null, 10, 0, 0, target: MoveTarget.AllOpponents);
        var battle = new BattleController(
            [Creature("p0", 200, 100, spread), Creature("p1", 200, 90, Wait())],
            [Creature("e0", 200, 1, Wait()), Creature("e1", 200, 1, Wait())],
            BattleTopology.Doubles, [0, 1], [0, 1], Chart(),
            new FakeRng(ints: [15, 15], doubles: [0.99, 0.99]));
        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new(new(BattleSide.Player, 0), new UseMove(0)),
            new(new(BattleSide.Player, 1), new Pass()),
            new(new(BattleSide.Enemy, 0), new Pass()),
            new(new(BattleSide.Enemy, 1), new Pass()),
        ]));

        BattleDamageRecord[] records = [.. battle.ActionHistory.DamageSnapshot()];
        Assert.Equal([0, 1], records.Select(record => record.Target.Slot.Position));
        Assert.Equal([0, 1], records.Select(record => record.Target.PartyIndex));
        Assert.All(records, record => Assert.Equal(0, record.Source.PartyIndex));
        Assert.Equal(records, battle.ActionHistory.DamageFrom(records[0].Source, 0));
    }

    [Theory]
    [InlineData("fixed", BattleDamageCause.Fixed)]
    [InlineData("level", BattleDamageCause.Level)]
    [InlineData("ohko", BattleDamageCause.OneHitKnockout)]
    public void FormulaBypassingMoves_RecordTypedCause(string kind, BattleDamageCause expected)
    {
        BattleMove move = kind switch
        {
            "fixed" => new(EntityId.Parse("move:fixed"), Normal, DamageClass.Physical,
                null, null, 10, 0, 0, fixedDamage: 20),
            "level" => new(EntityId.Parse("move:level"), Normal, DamageClass.Physical,
                null, null, 10, 0, 0, fixedDamageLevel: true),
            _ => new(EntityId.Parse("move:ohko"), Normal, DamageClass.Physical,
                null, null, 10, 0, 0, ohko: true, bypassAccuracy: true),
        };
        BattleController battle = Singles(Creature("source", 200, 100, move),
            Creature("target", 200, 1, Wait()), new FakeRng());
        battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Equal(expected, Assert.Single(battle.ActionHistory.DamageSnapshot()).Cause);
    }

    [Fact]
    public void CounterAndStatusHpFormula_RecordTypedCauses()
    {
        BattleMove counter = new(EntityId.Parse("move:counter"), Normal, DamageClass.Physical,
            null, null, 10, 0, 0, counterCategory: DamageClass.Physical);
        BattleController battle = Singles(Creature("counterer", 300, 1, counter),
            Creature("striker", 300, 100, Damage("strike", 40)),
            new FakeRng(ints: [15], doubles: [0.99]));
        battle.ResolveTurn(new UseMove(0), new UseMove(0));
        Assert.Contains(battle.ActionHistory.DamageSnapshot(), record => record.Cause == BattleDamageCause.Counter);

        battle = Singles(Creature("counterer2", 300, 100, counter),
            Creature("idle", 300, 1, Wait()), new FakeRng());
        battle.ResolveTurn(new UseMove(0), new Pass());
        BattleDamageRecord fizzle = Assert.Single(battle.ActionHistory.DamageSnapshot());
        Assert.Equal((BattleDamageCause.Counter, 0, BattleDamageFailure.NoQualifyingDamage),
            (fizzle.Cause, fizzle.HitNumber, fizzle.Failure));

        BattleMove equalize = new(EntityId.Parse("move:equalize"), Normal, DamageClass.Status,
            null, null, 10, 0, 0, secondaryEffects: [new HpEqualizeEffect(HpEqualizeMode.MatchSource)]);
        BattleCreature source = Creature("source", 100, 100, equalize);
        source.TakeDamage(80);
        battle = Singles(source, Creature("target", 100, 1, Wait()), new FakeRng());
        battle.ResolveTurn(new UseMove(0), new Pass());
        BattleDamageRecord formula = Assert.Single(battle.ActionHistory.DamageSnapshot());
        Assert.Equal((BattleDamageCause.HpFormula, DamageClass.Status, 80),
            (formula.Cause, formula.DamageClass, formula.ActualHpRemoved));

        BattleMove missedEqualize = new(EntityId.Parse("move:equalize_miss"), Normal, DamageClass.Status,
            null, 1, 10, 0, 0, secondaryEffects: [new HpEqualizeEffect(HpEqualizeMode.MatchSource)]);
        battle = Singles(Creature("source2", 100, 100, missedEqualize),
            Creature("target2", 100, 1, Wait()), new FakeRng(ints: [99]));
        battle.ResolveTurn(new UseMove(0), new Pass());
        BattleDamageRecord formulaMiss = Assert.Single(battle.ActionHistory.DamageSnapshot());
        Assert.Equal((BattleDamageCause.HpFormula, BattleDamageFailure.Missed),
            (formulaMiss.Cause, formulaMiss.Failure));
    }

    [Fact]
    public void SeededReplay_ProducesIdenticalDamageRecords()
    {
        BattleDamageRecord[] Run()
        {
            BattleController battle = Singles(Creature("source", 200, 100, Damage("replay", 60)),
                Creature("target", 200, 1, Wait()), new FakeRng(ints: [7], doubles: [0.5]));
            battle.ResolveTurn(new UseMove(0), new Pass());
            return [.. battle.ActionHistory.DamageSnapshot()];
        }

        Assert.Equal(Run(), Run());
    }

    private static (int Hit, bool Attempted, BattleDamageFailure Failure) Fields(BattleDamageRecord record) =>
        (record.HitNumber, record.Attempted, record.Failure);

    private static BattleController Singles(BattleCreature player, BattleCreature enemy, IRng rng) =>
        new(player, enemy, Chart(), rng);

    private static BattleMove Damage(string slug, int power = 60, int critStage = 0, bool contact = false) =>
        new(EntityId.Parse($"move:{slug}"), Normal, DamageClass.Physical,
            power, null, 10, 0, critStage, makesContact: contact);

    private static BattleMove Wait() => new(EntityId.Parse("move:wait"), Normal, DamageClass.Status,
        null, null, 10, 0, 0);

    private static BattleCreature Creature(string slug, int hp, int speed, BattleMove move,
        IReadOnlyList<Effect>? heldEffects = null, EntityId? type = null) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [type ?? Normal],
            new Stats(hp, 100, 100, 100, 100, speed), [move], heldItemBattleEffects: heldEffects);

    private static TypeChart Chart() => new(
        [new TypeDef { Id = Normal, NoDamageTo = [Ghost] }, new TypeDef { Id = Ghost }]);
}
