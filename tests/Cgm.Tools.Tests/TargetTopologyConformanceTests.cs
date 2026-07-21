using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation.Rules;
using Cgm.Tools.MoveAudit;
using System.Text.Json;

namespace Cgm.Tools.Tests;

public sealed class TargetTopologyConformanceTests
{
    public static IEnumerable<object[]> CertifiedRecords() => Catalog.Entries
        .Where(entry => entry.TestIds.Any(id => id.StartsWith("TargetTopologyConformanceTests.", StringComparison.Ordinal)))
        .Select(entry => new object[] { entry });

    private static MoveConformanceCatalog Catalog => CgmJson.Deserialize<MoveConformanceCatalog>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MoveConformance", "definitions.v1.json")));

    [Theory]
    [MemberData(nameof(CertifiedRecords))]
    public void Certified(MoveConformanceRecord record)
    {
        Move move = record.Mechanics.ToMove(record.ReferenceKey);
        Project project = new(new ProjectSettings { Name = "Conformance" },
            new Dictionary<EntityId, IEntity> { [move.Id] = move });
        Assert.Empty(new MoveRule().Check(project));
        BattleMove compiled = MoveCompiler.ToBattleMove(move);
        MoveGateEffect? requiredDamageGate = compiled.SecondaryEffects.OfType<MoveGateEffect>()
            .SingleOrDefault(gate => gate is
                { Kind: MoveGateKind.DamageReceived, DamageMode: MoveGateDamageMode.Require });
        BattleMove enemyMove = requiredDamageGate is null
            ? Wait(move.Type)
            : new BattleMove(EntityId.Parse("move:gate_setup"), move.Type,
                requiredDamageGate.DamageClass ?? DamageClass.Physical, 40, null, 20, 0, 0);

        BattleCreature player0 = Creature("p0", compiled, move.Type, 200);
        BattleCreature player1 = Creature("p1", Wait(move.Type), move.Type, 150);
        AbilityHook contactHook = new()
        {
            Hook = AbilityHookPoint.OnContactReceived,
            Effects = [new Effect { Op = "contactChanceEffect", Chance = 100, Params = Params(("damage", 1)) }],
        };
        BattleCreature enemy0 = Creature("e0", enemyMove, move.Type, 100, [contactHook]);
        BattleCreature enemy1 = Creature("e1", Wait(move.Type), move.Type, 50);
        player0.TakeDamage(400);
        var battle = new BattleController(
            [player0, player1], [enemy0, enemy1], BattleTopology.Doubles,
            [0, 1], [0, 1], new TypeChart([new TypeDef { Id = move.Type }]), new ConformanceRng());

        BattleSlot source = new(BattleSide.Player, 0);
        BattleActionSelection? selection = move.Target switch
        {
            MoveTarget.Ally or MoveTarget.UserOrAlly => new ActiveSlotSelection(new(BattleSide.Player, 1)),
            MoveTarget.SelectedPokemonMeFirst => new ActiveSlotSelection(new(BattleSide.Enemy, 0)),
            _ => null,
        };
        BattleTurnActions Actions(BattleActionSelection? selected) => new(BattleTopology.Doubles,
        [
            new BattleActionSubmission(source, new UseMove(0), selected),
            new BattleActionSubmission(new(BattleSide.Player, 1), new Pass()),
            new BattleActionSubmission(new(BattleSide.Enemy, 0),
                requiredDamageGate is null ? new Pass() : new UseMove(0),
                requiredDamageGate is null ? null : new ActiveSlotSelection(source)),
            new BattleActionSubmission(new(BattleSide.Enemy, 1), new Pass()),
        ]);

        int ppBeforeInvalid = compiled.Pp;
        BattleActionSelection? invalidSelection = move.Target == MoveTarget.Ally
            ? null
            : new ActiveSlotSelection(new(BattleSide.Enemy, 0));
        Assert.Throws<ArgumentException>(() => battle.ResolveTurn(Actions(invalidSelection)));
        Assert.Equal(ppBeforeInvalid, compiled.Pp);
        Assert.Empty(battle.Log);

        BattleTurnActions actions = Actions(selection);
        IReadOnlyList<BattleEvent> events = battle.ResolveTurn(actions);
        if (compiled.ChargeTurn)
        {
            Assert.Contains(events, e => e is Charging { Side: BattleSide.Player });
            Assert.DoesNotContain(events, e => e is MoveUsed);
            events = battle.ResolveTurn(actions);
        }
        BattleSlot[] expectedTargets = ExpectedTargets(move.Target);
        Assert.Contains(events, e => e is MoveUsed used && used.Slot == source);
        IReadOnlyList<BattleEvent> sourceEvents = events
            .SkipWhile(e => e is not MoveUsed { Slot: { Side: BattleSide.Player, Position: 0 } })
            .ToArray();
        Assert.Equal(expectedTargets, battle.Trace
            .Where(e => e.Kind == EffectTraceKind.Accuracy && e.SourceSlot == source)
            .Select(e => e.TargetSlot!.Value)
            .ToArray());

        if (move.DamageClass != DamageClass.Status)
            Assert.Equal(expectedTargets, sourceEvents.OfType<DamageDealt>().Select(e => e.Slot).ToArray());

        foreach (MoveEffect effect in compiled.SecondaryEffects)
            AssertEffect(effect, player0, [player0, player1, enemy0, enemy1], expectedTargets, sourceEvents);
        if (compiled.MultiTurnLock)
            Assert.True(player0.IsLocked);
        if (compiled.SelfDestruct)
            Assert.True(player0.IsFainted);
        Assert.Equal(compiled.MakesContact,
            sourceEvents.Any(e => e is ContactDamaged
                { Slot: { Side: BattleSide.Player, Position: 0 }, Amount: 1 }));

        Assert.Contains($"TargetTopologyConformanceTests.Certified({record.ReferenceKey})", record.TestIds);
        Assert.Equal("doubles", record.RequiredTopology);
    }

    private static void AssertEffect(MoveEffect effect, BattleCreature source, BattleCreature[] creatures,
        BattleSlot[] targets, IReadOnlyList<BattleEvent> events)
    {
        BattleCreature At(BattleSlot slot) => creatures[(slot.Side == BattleSide.Player ? 0 : 2) + slot.Position];
        switch (effect)
        {
            case AilmentEffect ailment:
                Assert.All(targets, slot => Assert.Equal(ailment.Status, At(slot).Status));
                Assert.Equal(targets, events.OfType<StatusApplied>().Select(e => e.Slot).ToArray());
                break;
            case ConfusionEffect:
                Assert.All(targets, slot => Assert.True(At(slot).IsConfused));
                Assert.Equal(targets, events.OfType<Confused>().Select(e => e.Slot).ToArray());
                break;
            case StatChangeEffect stat when stat.OnSelf:
                Assert.Equal(stat.Delta, source.Stage(stat.Stat));
                Assert.Contains(events, e => e is StatStageChanged { Slot: { Side: BattleSide.Player, Position: 0 } });
                break;
            case StatChangeEffect stat:
                Assert.All(targets, slot => Assert.Equal(stat.Delta, At(slot).Stage(stat.Stat)));
                Assert.Equal(targets, events.OfType<StatStageChanged>().Select(e => e.Slot).ToArray());
                break;
            case DrainEffect:
                Assert.True(source.CurrentHp > 600);
                Assert.Contains(events, e => e is Healed { Slot: { Side: BattleSide.Player, Position: 0 } });
                break;
            case RecoilEffect:
                Assert.True(source.CurrentHp < 600);
                Assert.Contains(events, e => e is Recoiled { Slot: { Side: BattleSide.Player, Position: 0 } });
                break;
            case FlinchEffect:
                Assert.All(targets, slot => Assert.True(At(slot).Flinched));
                break;
            case MoveGateEffect or QueueActionGateEffect:
                break;
        }

    }

    private static BattleSlot[] ExpectedTargets(MoveTarget target) => target switch
    {
        MoveTarget.AllOpponents => [new(BattleSide.Enemy, 0), new(BattleSide.Enemy, 1)],
        MoveTarget.AllOtherPokemon => [new(BattleSide.Player, 1), new(BattleSide.Enemy, 0), new(BattleSide.Enemy, 1)],
        MoveTarget.RandomOpponent or MoveTarget.SelectedPokemonMeFirst => [new(BattleSide.Enemy, 0)],
        MoveTarget.Ally or MoveTarget.UserOrAlly => [new(BattleSide.Player, 1)],
        MoveTarget.UserAndAllies => [new(BattleSide.Player, 0), new(BattleSide.Player, 1)],
        _ => throw new InvalidDataException($"Unexpected certified target {target}."),
    };

    private static BattleCreature Creature(string slug, BattleMove move, EntityId type, int speed,
        IReadOnlyList<AbilityHook>? abilityHooks = null) =>
        new(EntityId.Parse($"species:{slug}"), slug, 50, [type], new Stats(1000, 100, 100, 100, 100, speed), [move],
            abilityHooks: abilityHooks);

    private static BattleMove Wait(EntityId type) =>
        new(EntityId.Parse("move:wait"), type, DamageClass.Status, null, null, 20, 0, 0);

    private static IReadOnlyDictionary<string, JsonElement> Params(params (string Key, object Value)[] values) =>
        values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value));

    private sealed class ConformanceRng : IRng
    {
        public int Next(int maxExclusive) => maxExclusive == 16 ? 15 : 0;
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
        public double NextDouble() => 0.99;
    }
}
