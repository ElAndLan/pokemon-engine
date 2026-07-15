using System.Text.Json;
using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Core.Tests.Battle;

public sealed class BattlePartyResourceFormulaTests
{
    private static readonly EntityId Normal = EntityId.Parse("type:normal");
    private static readonly EntityId HeldItem = EntityId.Parse("item:weight_stone");

    [Fact]
    public void Inputs_CountSlotsIndependentlyAndSeparateLivingFaintedAndContributing()
    {
        BattleCreature source = Creature("source", Inert());
        BattleCreature status = Creature("status", Inert());
        status.SetStatus(PersistentStatus.Poison);
        BattleCreature fainted = Creature("fainted", Inert());
        fainted.TakeDamage(fainted.MaxHp);

        PartyResourceFormulaInputs inputs = PartyResourceFormulas.Inputs(
            [source, source, status, fainted], source, status, 3, 2);

        Assert.Equal((3, 1, 2), (inputs.LivingParty, inputs.FaintedParty, inputs.ContributingParty));
        Assert.Equal(0, PartyResourceFormulas.Count([], PartyMemberFilter.Living));
        Assert.Equal(1, PartyResourceFormulas.Count([fainted], PartyMemberFilter.Fainted));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PartyResourceFormulas.Count([source], (PartyMemberFilter)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PartyResourceFormulas.Inputs([source], source, status, 1, 2));
    }

    [Fact]
    public void FriendshipAndPositiveStages_CoverBoundariesMissingModeAndAllSevenStats()
    {
        Assert.Equal(1, PartyResourceFormulas.FriendshipPower(0, FriendshipPowerMode.Current));
        Assert.Equal(102, PartyResourceFormulas.FriendshipPower(255, FriendshipPowerMode.Current));
        Assert.Equal(1, PartyResourceFormulas.FriendshipPower(1, FriendshipPowerMode.Current));
        Assert.Equal(101, PartyResourceFormulas.FriendshipPower(254, FriendshipPowerMode.Current));
        Assert.Equal(102, PartyResourceFormulas.FriendshipPower(0, FriendshipPowerMode.Missing));
        Assert.Equal(1, PartyResourceFormulas.FriendshipPower(255, FriendshipPowerMode.Missing));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PartyResourceFormulas.FriendshipPower(256, FriendshipPowerMode.Current));

        BattleCreature creature = Creature("stages", Inert(), friendship: 255);
        creature.SetStage(StatKind.Atk, 6);
        creature.SetStage(StatKind.Def, -6);
        creature.SetStage(StatKind.Spa, 1);
        creature.SetStage(StatKind.Spd, 2);
        creature.SetStage(StatKind.Spe, 3);
        creature.SetStage(StatKind.Accuracy, 4);
        creature.SetStage(StatKind.Evasion, 5);
        Assert.Equal(21, PartyResourceFormulas.PositiveStageSum(creature));
        Assert.Equal(100, PartyResourceFormulas.Linear(21, 20, 5, 100));
        Assert.Equal(int.MaxValue, PartyResourceFormulas.Linear(int.MaxValue, int.MaxValue, int.MaxValue, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => PartyResourceFormulas.Linear(1, 0, 0, null));
    }

    [Fact]
    public void PpBands_CoverZeroOneAndMaximumAndRejectMalformedPublicInputs()
    {
        FormulaPowerBand[] bands = [new(0, 200), new(1, 80), new(2, 40), new(5, 20)];
        Assert.Equal(200, PartyResourceFormulas.PpPower(0, bands));
        Assert.Equal(80, PartyResourceFormulas.PpPower(1, bands));
        Assert.Equal(20, PartyResourceFormulas.PpPower(5, bands));
        Assert.Throws<ArgumentOutOfRangeException>(() => PartyResourceFormulas.PpPower(-1, bands));
        Assert.Throws<ArgumentException>(() => PartyResourceFormulas.PpPower(1, []));
    }

    [Fact]
    public void WeightedTable_UsesAuthoredHalfOpenIntervalsAndSkipsZeroWeights()
    {
        WeightedPowerEntry[] entries = [new(0, 999), new(2, 10), new(3, 50), new(1, 90)];
        Assert.Equal(10, Select(entries, 0));
        Assert.Equal(10, Select(entries, 1));
        Assert.Equal(50, Select(entries, 2));
        Assert.Equal(50, Select(entries, 4));
        Assert.Equal(90, Select(entries, 5));
        Assert.Equal(43, PartyResourceFormulas.ExpectedWeightedPower(entries));
    }

    [Fact]
    public void WeightedTable_SinglePositiveEntryDoesNotDrawAndInvalidTablesFailClosed()
    {
        var rng = new CountingRng();
        int power = PartyResourceFormulas.SelectWeightedPower(
            [new(0, 1), new(7, 55), new(0, 2)], rng, out int? draw, out int total);
        Assert.Equal((55, null, 7, 0), (power, draw, total, rng.IntDraws));
        Assert.Throws<ArgumentException>(() =>
            PartyResourceFormulas.SelectWeightedPower([new(0, 1)], rng, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PartyResourceFormulas.SelectWeightedPower([new(-1, 1)], rng, out _, out _));
        Assert.Throws<OverflowException>(() =>
            PartyResourceFormulas.ExpectedWeightedPower([new(int.MaxValue, 1), new(1, 1)]));
    }

    [Fact]
    public void Compiler_ProducesEveryTypedOp()
    {
        Assert.IsType<PartyCountPowerEffect>(Compile("partyCountPower",
            ("filter", "contributing"), ("base", 10), ("perMember", 20), ("cap", 100)).SecondaryEffects.Single());
        Assert.IsType<FriendshipPowerEffect>(Compile("friendshipPower", ("mode", "missing")).SecondaryEffects.Single());
        Assert.IsType<PpPowerEffect>(Compile("ppPower", ("timing", "afterSpend"),
            ("bands", "0:200,1:80,2:40")).SecondaryEffects.Single());
        Assert.IsType<PositiveStagePowerEffect>(Compile("positiveStagePower", ("subject", "target"),
            ("base", 20), ("perStage", 20), ("cap", 200)).SecondaryEffects.Single());
        Assert.IsType<ItemDataPowerEffect>(Compile("itemDataPower", ("field", "flingPower")).SecondaryEffects.Single());
        Assert.IsType<RandomTablePowerEffect>(Compile("randomTablePower",
            ("entries", "1:10,3:50,0:999")).SecondaryEffects.Single());
    }

    [Fact]
    public void Compiler_RejectsChanceStatusUnknownMalformedAndDuplicateReplacementFormulas()
    {
        Effect chance = Op("friendshipPower", ("mode", "current")) with { Chance = 50 };
        Assert.Throws<ArgumentException>(() => Compile(chance));
        Assert.Throws<ArgumentException>(() => Compile(Op("partyCountPower",
            ("filter", "living"), ("base", 0), ("perMember", 0))));
        Assert.Throws<ArgumentException>(() => Compile(Op("ppPower", ("timing", "beforeSpend"),
            ("bands", "0:40,99:20"))));
        Assert.Throws<ArgumentException>(() => Compile(Op("randomTablePower", ("entries", "0:10,0:20"))));
        Assert.Throws<ArgumentException>(() => Compile(Op("itemDataPower", ("field", "unknown"))));
        Assert.Throws<ArgumentException>(() => Compile(
            Op("friendshipPower", ("mode", "current")),
            Op("randomTablePower", ("entries", "1:10"))));

        Move status = Move([Op("friendshipPower", ("mode", "current"))]) with { DamageClass = DamageClass.Status };
        Assert.Throws<ArgumentException>(() => MoveCompiler.ToBattleMove(status));
    }

    [Fact]
    public void Resolver_UsesPpSnapshotsAroundTheActualSpend()
    {
        BattleMove before = Compile("ppPower", ("timing", "beforeSpend"), ("bands", "0:200,1:80,2:40"));
        BattleMove after = Compile("ppPower", ("timing", "afterSpend"), ("bands", "0:200,1:80,2:40"));

        Assert.Equal(40, ResolvePower(before));
        Assert.Equal(80, ResolvePower(after));
        Assert.Equal(1, before.Pp);
        Assert.Equal(1, after.Pp);
    }

    [Fact]
    public void PpSnapshot_ChargeReleaseWithNoSpendUsesEqualBeforeAndAfterValues()
    {
        BattleMove charge = Compile(
            Op("ppPower", ("timing", "afterSpend"), ("bands", "0:200,1:80,2:40")),
            Op("chargeTurn"));
        var battle = new BattleController(Creature("charge", charge), Creature("charge_target", Inert()),
            Chart(), new CountingRng());

        battle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Equal(1, charge.Pp);
        Assert.DoesNotContain(battle.QueryTrace, entry => entry.Result.Query == BattleQueryId.BasePower);
        battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Equal(80, BasePower(battle));
        Assert.Equal(1, charge.Pp);
    }

    [Fact]
    public void ItemDataPower_UsesEffectiveHeldItemAndFailsBeforeAccuracyOrFormulaRng()
    {
        BattleMove missingMove = CompileWithAccuracy(50, "itemDataPower", ("field", "flingPower"));
        var missingRng = new CountingRng();
        var missing = new BattleController(Creature("missing", missingMove, heldItem: HeldItem),
            Creature("target", Inert()), Chart(), missingRng);

        missing.ResolveTurn(new UseMove(0), new Pass());

        Assert.Contains(missing.Log, e => e is MoveFailed { Reason: MoveFailureReason.FormulaInputUnavailable });
        Assert.Equal((0, 0, 1), (missingRng.IntDraws, missingRng.DoubleDraws, missingMove.Pp));
        Assert.DoesNotContain(missing.QueryTrace, entry => entry.Result.Query == BattleQueryId.Accuracy);

        var item = new Item { Id = HeldItem, Name = "Weight Stone", FlingPower = 90 };
        BattleMove presentMove = Compile("itemDataPower", ("field", "flingPower"));
        var present = new BattleController(Creature("present", presentMove, heldItem: HeldItem),
            Creature("presenttarget", Inert()), Chart(), new CountingRng(), itemData: [item]);
        present.ResolveTurn(new UseMove(0), new Pass());
        Assert.Equal(90, BasePower(present));

        BattleMove suppressedMove = CompileWithAccuracy(50, "itemDataPower", ("field", "flingPower"));
        var suppressedRng = new CountingRng();
        var suppressed = new BattleController(Creature("suppressed", suppressedMove, heldItem: HeldItem),
            Creature("suppressedtarget", Inert()), Chart(), suppressedRng, itemData: [item]);
        suppressed.Overlays.Apply(new BattleOverlayApplication(
            new(BattleSide.Player, 0, new BattleSlot(BattleSide.Player, 0)), new(), BattleOverlayLayer.Suppression,
            new SuppressionOverlay(BattleEffectiveValueKind.HeldItem), 0, 0));
        suppressed.ResolveTurn(new UseMove(0), new Pass());
        Assert.Contains(suppressed.Log, e => e is MoveFailed { Reason: MoveFailureReason.FormulaInputUnavailable });
        Assert.Equal(0, suppressedRng.IntDraws);
    }

    [Fact]
    public void ItemDataPower_RejectsUnknownAndNonpositiveCatalogValues()
    {
        foreach (IReadOnlyList<Item> catalog in new IReadOnlyList<Item>[]
        {
            [new Item { Id = EntityId.Parse("item:other"), Name = "Other", FlingPower = 100 }],
            [new Item { Id = HeldItem, Name = "Weight Stone", FlingPower = 0 }],
            [new Item { Id = HeldItem, Name = "Weight Stone", FlingPower = null }],
        })
        {
            BattleMove move = Compile("itemDataPower", ("field", "flingPower"));
            var battle = new BattleController(Creature("invalid_item", move, heldItem: HeldItem),
                Creature("invalid_item_target", Inert()), Chart(), new CountingRng(), itemData: catalog);
            battle.ResolveTurn(new UseMove(0), new Pass());
            Assert.Contains(battle.Log, e => e is MoveFailed { Reason: MoveFailureReason.FormulaInputUnavailable });
        }
    }

    [Fact]
    public void RandomTable_DrawsOncePerActionAndReusesPowerAcrossDoublesTargets()
    {
        BattleMove random = Compile("randomTablePower", MoveTarget.AllOpponents, ("entries", "1:20,3:100"));
        var rng = new CountingRng([1]);
        var battle = new BattleController(
            [Creature("p0", random), Creature("p1", Inert())],
            [Creature("e0", Inert()), Creature("e1", Inert())], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), rng);

        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new(new(BattleSide.Player, 0), new UseMove(0)),
            new(new(BattleSide.Player, 1), new Pass()),
            new(new(BattleSide.Enemy, 0), new Pass()),
            new(new(BattleSide.Enemy, 1), new Pass()),
        ]));

        Assert.Equal([100, 100], battle.QueryTrace.Where(q => q.Result.Query == BattleQueryId.BasePower)
            .Select(q => q.Result.FinalValue.ToInt32()));
        EffectTraceEntry trace = Assert.Single(battle.Trace, entry => entry.Kind == EffectTraceKind.PowerTable);
        Assert.Equal((true, 1d, 4d, 100), (trace.Performed, trace.DrawResult, trace.DrawBound, trace.Value));
        Assert.Equal(1, rng.Bounds.Count(bound => bound == 4));
    }

    [Fact]
    public void RandomTable_AllMissSkipsSelectionAndSinglePositiveEntryTracesNoDraw()
    {
        BattleMove misses = CompileWithAccuracy(1, "randomTablePower", ("entries", "1:20,3:100"));
        var missRng = new CountingRng([99]);
        var missBattle = new BattleController(Creature("miss", misses), Creature("targetmiss", Inert()), Chart(), missRng);
        missBattle.ResolveTurn(new UseMove(0), new Pass());
        Assert.DoesNotContain(missBattle.Trace, entry => entry.Kind == EffectTraceKind.PowerTable);
        Assert.Equal([100], missRng.Bounds);

        BattleMove fixedTable = Compile("randomTablePower", ("entries", "0:1,7:55,0:2"));
        var noDrawRng = new CountingRng();
        var fixedBattle = new BattleController(Creature("fixed", fixedTable), Creature("fixedtarget", Inert()),
            Chart(), noDrawRng);
        fixedBattle.ResolveTurn(new UseMove(0), new Pass());
        EffectTraceEntry trace = Assert.Single(fixedBattle.Trace, entry => entry.Kind == EffectTraceKind.PowerTable);
        Assert.Equal((false, null, 7d, 55), (trace.Performed, trace.DrawResult, trace.DrawBound, trace.Value));
        Assert.DoesNotContain(7, noDrawRng.Bounds);
    }

    [Fact]
    public void RandomTable_ReusesOneSelectionAcrossEveryMultiHit()
    {
        BattleMove move = Compile(
            Op("randomTablePower", ("entries", "1:20,3:100")),
            Op("multiHit", ("min", 2), ("max", 2)));
        var rng = new CountingRng([1]);
        var battle = new BattleController(Creature("multi", move), Creature("multi_target", Inert()), Chart(), rng);

        battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Equal([100, 100], battle.QueryTrace.Where(q => q.Result.Query == BattleQueryId.BasePower)
            .Select(q => q.Result.FinalValue.ToInt32()));
        Assert.Single(battle.Trace, entry => entry.Kind == EffectTraceKind.PowerTable);
        Assert.Equal(1, rng.Bounds.Count(bound => bound == 4));
    }

    [Fact]
    public void RandomTable_ReplayIsDeterministicForTheSameSeedAndActions()
    {
        static BattleController Run()
        {
            BattleMove move = Compile("randomTablePower", ("entries", "1:20,3:100,1:150"));
            var battle = new BattleController(Creature("replay", move), Creature("replay_target", Inert()),
                Chart(), new Rng(2026));
            battle.ResolveTurn(new UseMove(0), new Pass());
            return battle;
        }

        BattleController first = Run();
        BattleController second = Run();
        Assert.Equal(first.Log, second.Log);
        Assert.Equal(first.Trace, second.Trace);
        Assert.Equal(first.QueryTrace.Select(entry => (entry.Result.Query, entry.Result.FinalValue,
                entry.SourceSlot, entry.TargetSlot)),
            second.QueryTrace.Select(entry => (entry.Result.Query, entry.Result.FinalValue,
                entry.SourceSlot, entry.TargetSlot)));
    }

    [Fact]
    public void PositiveStageAndFriendshipQueriesUseCapturedActorState()
    {
        BattleMove stages = Compile("positiveStagePower", ("subject", "user"),
            ("base", 20), ("perStage", 10), ("cap", 200));
        BattleCreature source = Creature("boosted", stages, friendship: 255);
        source.SetStage(StatKind.Atk, 6);
        source.SetStage(StatKind.Accuracy, 2);
        var stageBattle = new BattleController(source, Creature("stage_target", Inert()), Chart(), new CountingRng());
        stageBattle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Equal(100, BasePower(stageBattle));

        BattleMove friendship = Compile("friendshipPower", ("mode", "current"));
        var friendshipBattle = new BattleController(Creature("friend", friendship, friendship: 255),
            Creature("friend_target", Inert()), Chart(), new CountingRng());
        friendshipBattle.ResolveTurn(new UseMove(0), new Pass());
        Assert.Equal(102, BasePower(friendshipBattle));
    }

    [Theory]
    [InlineData("living", 40)]
    [InlineData("fainted", 20)]
    [InlineData("contributing", 30)]
    public void PartyCountResolver_UsesTheActingSideAndEveryLockedFilter(string filter, int expected)
    {
        BattleMove move = Compile("partyCountPower", ("filter", filter),
            ("base", 10), ("perMember", 10), ("cap", 100));
        BattleCreature source = Creature($"party_{filter}", move);
        BattleCreature healthy = Creature($"healthy_{filter}", Inert());
        BattleCreature status = Creature($"status_{filter}", Inert());
        status.SetStatus(PersistentStatus.Burn);
        BattleCreature fainted = Creature($"fainted_{filter}", Inert());
        fainted.TakeDamage(fainted.MaxHp);
        var battle = new BattleController([source, healthy, status, fainted],
            [Creature($"target_{filter}", Inert())], Chart(), new CountingRng());

        battle.ResolveTurn(new UseMove(0), new Pass());

        Assert.Equal(expected, BasePower(battle));
    }

    [Fact]
    public void DoublesItemFailure_RecordsEveryMaterializedTargetWithoutDrawingAccuracy()
    {
        BattleMove move = Compile("itemDataPower", MoveTarget.AllOpponents, ("field", "flingPower"));
        var rng = new CountingRng();
        var battle = new BattleController(
            [Creature("item_user", move, heldItem: HeldItem), Creature("item_ally", Inert())],
            [Creature("item_target_a", Inert()), Creature("item_target_b", Inert())], BattleTopology.Doubles,
            [0, 1], [0, 1], Chart(), rng);

        battle.ResolveTurn(new BattleTurnActions(BattleTopology.Doubles,
        [
            new(new(BattleSide.Player, 0), new UseMove(0)),
            new(new(BattleSide.Player, 1), new Pass()),
            new(new(BattleSide.Enemy, 0), new Pass()),
            new(new(BattleSide.Enemy, 1), new Pass()),
        ]));

        Assert.Single(battle.Log, e => e is MoveFailed { Reason: MoveFailureReason.FormulaInputUnavailable });
        Assert.Equal(2, battle.ActionHistory.DamageSnapshot().Count(record =>
            record.Failure == BattleDamageFailure.NoQualifyingDamage));
        Assert.Equal(0, rng.IntDraws);
    }

    [Fact]
    public void SmartAi_UsesWeightedMeanAndOwnPartyWithoutConsumingFormulaRng()
    {
        BattleMove random = Compile("randomTablePower", ("entries", "1:20,3:100"));
        BattleMove fixedMove = new(EntityId.Parse("move:fixed"), Normal, DamageClass.Special, 70, null, 2, 0, 0);
        BattleCreature active = Creature("ai", random, additional: fixedMove);
        BattleCreature reserve = Creature("reserve", Inert());
        var rng = new CountingRng();

        SmartAiDecision decision = SmartAi.ChooseAction(new SmartAiContext(
            [active, reserve], 0, [Creature("defender", Inert())], 0, Chart(), rng,
            Weights: new SmartAiWeights { NoiseFraction = 0 }));

        Assert.Equal(0, Assert.IsType<UseMove>(decision.Action).MoveIndex);
        Assert.Equal(0, rng.IntDraws);
        Assert.True(decision.Scores[0].Components.Single(c => c.Name == "damage").Value
            > decision.Scores[1].Components.Single(c => c.Name == "damage").Value);
    }

    [Fact]
    public void SmartAi_ItemFormulaUsesEffectiveCatalogDataAndRejectsUnavailableInput()
    {
        BattleMove itemMove = Compile("itemDataPower", ("field", "flingPower"));
        BattleMove fixedMove = new(EntityId.Parse("move:fixed_item"), Normal, DamageClass.Special, 80, null, 2, 0, 0);
        BattleCreature active = Creature("item_ai", itemMove, heldItem: HeldItem, additional: fixedMove);
        BattleCreature defender = Creature("item_defender", Inert());
        var item = new Item { Id = HeldItem, Name = "Weight Stone", FlingPower = 100 };

        SmartAiDecision available = SmartAi.ChooseAction(new SmartAiContext(
            [active], 0, [defender], 0, Chart(), new CountingRng(),
            Weights: new SmartAiWeights { NoiseFraction = 0 }, ItemData: new Dictionary<EntityId, Item> { [HeldItem] = item }));
        SmartAiDecision unavailable = SmartAi.ChooseAction(new SmartAiContext(
            [active], 0, [defender], 0, Chart(), new CountingRng(),
            Weights: new SmartAiWeights { NoiseFraction = 0 }));

        Assert.Equal(0, Assert.IsType<UseMove>(available.Action).MoveIndex);
        Assert.Equal(1, Assert.IsType<UseMove>(unavailable.Action).MoveIndex);
    }

    [Fact]
    public void Creature_RejectsFriendshipOutsideTheSerializedRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Creature("invalid_friendship", Inert(), friendship: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Creature("invalid_friendship_high", Inert(), friendship: 256));
    }

    private static int Select(IReadOnlyList<WeightedPowerEntry> entries, int draw) =>
        PartyResourceFormulas.SelectWeightedPower(entries, new CountingRng([draw]), out _, out _);

    private static int ResolvePower(BattleMove move)
    {
        var battle = new BattleController(Creature("source", move), Creature("target", Inert()),
            Chart(), new CountingRng());
        battle.ResolveTurn(new UseMove(0), new Pass());
        return BasePower(battle);
    }

    private static int BasePower(BattleController battle) => Assert.Single(battle.QueryTrace,
        entry => entry.Result.Query == BattleQueryId.BasePower).Result.FinalValue.ToInt32();

    private static BattleMove Compile(string op, params (string Key, object Value)[] values) => Compile(Op(op, values));

    private static BattleMove Compile(string op, MoveTarget target, params (string Key, object Value)[] values) =>
        MoveCompiler.ToBattleMove(Move([Op(op, values)]) with { Target = target });

    private static BattleMove Compile(params Effect[] effects) => MoveCompiler.ToBattleMove(Move(effects));

    private static BattleMove CompileWithAccuracy(int accuracy, string op,
        params (string Key, object Value)[] values) =>
        MoveCompiler.ToBattleMove(Move([Op(op, values)]) with { Accuracy = accuracy });

    private static Move Move(IReadOnlyList<Effect> effects) => new()
    {
        Id = EntityId.Parse("move:formula"), Name = "Formula", Type = Normal,
        DamageClass = DamageClass.Special, Power = null, Accuracy = null, Pp = 2,
        Target = MoveTarget.Selected, Effects = effects,
    };

    private static Effect Op(string op, params (string Key, object Value)[] values) => new()
    {
        Op = op,
        Params = values.ToDictionary(value => value.Key, value => JsonSerializer.SerializeToElement(value.Value)),
    };

    private static BattleCreature Creature(string id, BattleMove move, int friendship = 70,
        EntityId? heldItem = null, BattleMove? additional = null) =>
        new(EntityId.Parse($"species:{id}"), id, 50, [Normal], new Stats(2000, 100, 100, 100, 100, 100),
            additional is null ? [move] : [move, additional], heldItem: heldItem, friendship: friendship);

    private static BattleMove Inert() =>
        new(EntityId.Parse("move:inert"), Normal, DamageClass.Status, null, null, 20, 0, 0);

    private static TypeChart Chart() => new([new TypeDef { Id = Normal }]);

    private sealed class CountingRng(IEnumerable<int>? ints = null) : IRng
    {
        private readonly Queue<int> _ints = new(ints ?? []);
        public List<int> Bounds { get; } = [];
        public int IntDraws => Bounds.Count;
        public int DoubleDraws { get; private set; }

        public int Next(int maxExclusive)
        {
            Bounds.Add(maxExclusive);
            return _ints.Count > 0 ? _ints.Dequeue() : maxExclusive == 16 ? 15 : 0;
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            Bounds.Add(maxExclusive);
            return _ints.Count > 0 ? _ints.Dequeue() : minInclusive;
        }

        public double NextDouble()
        {
            DoubleDraws++;
            return 0.99;
        }
    }
}
