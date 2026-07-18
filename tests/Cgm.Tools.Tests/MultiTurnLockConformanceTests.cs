using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation.Rules;
using Cgm.Tools.MoveAudit;

namespace Cgm.Tools.Tests;

public sealed class MultiTurnLockConformanceTests
{
    public static IEnumerable<object[]> CertifiedRecords() => Catalog.Entries
        .Where(entry => entry.TestIds.Any(id => id.StartsWith("MultiTurnLockConformanceTests.", StringComparison.Ordinal)))
        .Select(entry => new object[] { entry });

    private static MoveConformanceCatalog Catalog => CgmJson.Deserialize<MoveConformanceCatalog>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MoveConformance", "definitions.v1.json")));

    [Theory]
    [MemberData(nameof(CertifiedRecords))]
    public void Certified(MoveConformanceRecord record)
    {
        Move definition = record.Mechanics.ToMove(record.ReferenceKey);
        Project project = new(new ProjectSettings { Name = "Conformance" },
            new Dictionary<EntityId, IEntity> { [definition.Id] = definition });
        Assert.Empty(new MoveRule().Check(project));
        BattleMove move = MoveCompiler.ToBattleMove(definition);
        if (move.SecondaryEffects.OfType<MultiTurnPowerBoostEffect>().SingleOrDefault() is { } boost)
        {
            BattleCreature boostSource = Creature("source", move, record.Mechanics.Type, 100);
            BattleCreature boostTarget = Creature("target", Wait(record.Mechanics.Type), record.Mechanics.Type, 1);
            var boostBattle = new BattleController(boostSource, boostTarget,
                new TypeChart([new TypeDef { Id = record.Mechanics.Type }]), new MinimumRng());
            boostBattle.ResolveTurn(new UseMove(0), new Pass());
            Assert.Equal(boost.Key, boostSource.MultiTurnPowerBoostKey);
            Assert.Equal(boost.Multiplier, boostSource.MultiTurnPowerBoost);
            Assert.Contains($"MultiTurnLockConformanceTests.Certified({record.ReferenceKey})", record.TestIds);
            return;
        }
        MultiTurnLockProfile profile = Assert.IsType<MultiTurnLockProfile>(move.MultiTurnLockProfile);
        Assert.True(profile.MinTurns >= 2);
        BattleCreature source = Creature("source", move, record.Mechanics.Type, 100);
        BattleCreature ally = Creature("ally", Wait(record.Mechanics.Type), record.Mechanics.Type, 50);
        BattleCreature target = Creature("target", Wait(record.Mechanics.Type), record.Mechanics.Type, 1);
        BattleCreature targetAlly = Creature("target_ally", Wait(record.Mechanics.Type), record.Mechanics.Type, 2);
        var battle = new BattleController([source, ally], [target, targetAlly], BattleTopology.Doubles,
            [0, 1], [0, 1], new TypeChart([new TypeDef { Id = record.Mechanics.Type }]), new MinimumRng());

        IReadOnlyList<BattleEvent> first = battle.ResolveTurn(Actions(new UseMove(0), move.Target));
        IReadOnlyList<BattleEvent> last = first;
        for (int turn = 1; turn < profile.MinTurns; turn++)
            last = battle.ResolveTurn(Actions(new Pass(), move.Target));

        Assert.Contains(first, e => e is MultiTurnLockStarted { Turns: var turns } && turns == profile.MinTurns);
        Assert.Contains(last, e => e is MultiTurnLockEnded { Reason: MultiTurnLockEndReason.Completed });
        Assert.Equal(profile.EndEffect == MultiTurnLockEndEffect.Confusion,
            last.Any(e => e is Confused { Side: BattleSide.Player }));
        if (profile.MaxPowerStep > 0)
            Assert.Equal(move.Power * 16, battle.QueryTrace.Last(entry => entry.Result.Query == BattleQueryId.BasePower
                && entry.SourceSlot.Side == BattleSide.Player).Result.FinalValue.ToInt32());
        Assert.Equal(move.MaxPp - 1, move.Pp);
        Assert.Contains($"MultiTurnLockConformanceTests.Certified({record.ReferenceKey})", record.TestIds);
    }

    private static BattleCreature Creature(string id, BattleMove move, EntityId type, int speed) => new(
        EntityId.Parse($"species:{id}"), id, 50, [type], new Stats(9999, 100, 100, 100, 100, speed), [move]);

    private static BattleMove Wait(EntityId type) => new(EntityId.Parse("move:wait"), type,
        DamageClass.Status, null, null, 10, 0, 0);

    private static BattleTurnActions Actions(BattleAction player, MoveTarget target) => new(BattleTopology.Doubles,
    [
        new BattleActionSubmission(new(BattleSide.Player, 0), player,
            target == MoveTarget.Selected ? new ActiveSlotSelection(new(BattleSide.Enemy, 0)) : null),
        new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
        new BattleActionSubmission(new(BattleSide.Enemy, 0), new Pass()),
        new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
    ]);

    private sealed class MinimumRng : IRng
    {
        public int Next(int maxExclusive) => 0;
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
        public double NextDouble() => 0;
    }
}
