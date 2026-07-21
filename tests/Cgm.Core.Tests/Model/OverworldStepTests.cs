using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

/// <summary>ENGINE_RUNTIME_SPEC 16D completed-step order and interaction priority. The order is a
/// rule, so it is asserted here in Core rather than in the scene that presents it.</summary>
public sealed class OverworldStepTests
{
    private static readonly EntityId TrainerId = EntityId.Parse("trainer:rival");
    private static readonly EntityId TableId = EntityId.Parse("encounter:grass");
    private static readonly EntityId SpeciesId = EntityId.Parse("species:pebbling");

    /// <summary>Counts draws so a test can prove an early transition consumed no randomness.</summary>
    private sealed class CountingRng(params double[] doubles) : IRng
    {
        private int _doubleAt;
        public int Draws { get; private set; }

        public int Next(int maxExclusive) { Draws++; return 0; }
        public int Next(int minInclusive, int maxExclusive) { Draws++; return minInclusive; }
        public double NextDouble()
        {
            Draws++;
            return _doubleAt < doubles.Length ? doubles[_doubleAt++] : 1.0;
        }
    }

    private static Map Map(IEnumerable<MapEntity>? entities = null,
        IEnumerable<EncounterZoneCell>? zones = null) => new()
        {
            Id = EntityId.Parse("map:test"),
            Name = "Test",
            Width = 8,
            Height = 8,
            Entities = entities?.ToList() ?? [],
            EncounterZones = zones?.ToList() ?? [],
        };

    private static CollisionValue[] Open() =>
        Enumerable.Repeat(CollisionValue.Open, 64).ToArray();

    private static WarpEntity Warp(GridPos pos) => new()
    {
        Key = "warp", Pos = pos, Target = EntityId.Parse("map:other"), TargetPos = new GridPos(0, 0),
    };

    private static TriggerEntity Trigger(GridPos pos, string? condition = null) => new()
    {
        Key = "trigger", Pos = pos, Condition = condition,
        Actions = [new TriggerAction { Op = TriggerOp.Dialogue, Text = "hi" }],
    };

    private static NpcEntity Spotter(GridPos pos, Facing facing) => new()
    {
        Key = "npc", Pos = pos, Facing = facing, Trainer = TrainerId,
    };

    private static Dictionary<EntityId, Trainer> Trainers(int sightRange = 4) => new()
    {
        [TrainerId] = new Trainer { Id = TrainerId, Name = "Rival", SightRange = sightRange },
    };

    private static Dictionary<EntityId, EncounterTable> Tables(double rate = 1.0) => new()
    {
        [TableId] = new EncounterTable
        {
            Id = TableId, Name = "Grass", BaseRate = rate,
            Slots = [new EncounterSlot { Species = SpeciesId, Weight = 1, MinLevel = 3, MaxLevel = 5 }],
        },
    };

    private static StepOutcome Resolve(Map map, GridPos landed, FlagStore? flags = null,
        IRng? rng = null, IReadOnlyDictionary<EntityId, Trainer>? trainers = null,
        IReadOnlyDictionary<EntityId, EncounterTable>? tables = null) =>
        OverworldStep.Resolve(landed, map, Open(), flags ?? new FlagStore(),
            trainers ?? Trainers(), tables ?? Tables(), rng ?? new CountingRng());

    // --- Completed-step order -----------------------------------------------------

    [Fact]
    public void EmptyTile_ProducesNoOutcome() =>
        Assert.IsType<StepOutcome.None>(Resolve(Map(), new GridPos(1, 1)));

    [Fact]
    public void SteppingOnAWarp_Warps() =>
        Assert.IsType<StepOutcome.Warp>(Resolve(Map([Warp(new GridPos(2, 2))]), new GridPos(2, 2)));

    [Fact]
    public void SteppingOnATrigger_Fires() =>
        Assert.IsType<StepOutcome.Trigger>(Resolve(Map([Trigger(new GridPos(2, 2))]), new GridPos(2, 2)));

    [Fact]
    public void WalkingIntoSight_SpotsTheTrainer()
    {
        Map map = Map([Spotter(new GridPos(2, 0), Facing.Down)]);
        var outcome = Assert.IsType<StepOutcome.TrainerSpotted>(Resolve(map, new GridPos(2, 3)));
        Assert.Equal(TrainerId, outcome.Trainer.Id);
    }

    [Fact]
    public void SteppingInGrass_CanRollAnEncounter()
    {
        Map map = Map(zones: [new EncounterZoneCell(2 * 8 + 2, TableId)]);
        var outcome = Assert.IsType<StepOutcome.WildEncounter>(
            Resolve(map, new GridPos(2, 2), rng: new CountingRng(0.0)));

        Assert.Equal(SpeciesId, outcome.Species);
        Assert.InRange(outcome.Level, 3, 5);
    }

    // --- Priority: first transition wins ------------------------------------------

    [Fact]
    public void WarpBeatsTrigger()
    {
        Map map = Map([Trigger(new GridPos(2, 2)), Warp(new GridPos(2, 2))]);
        Assert.IsType<StepOutcome.Warp>(Resolve(map, new GridPos(2, 2)));
    }

    [Fact]
    public void TriggerBeatsTrainerSight()
    {
        Map map = Map([Spotter(new GridPos(2, 0), Facing.Down), Trigger(new GridPos(2, 3))]);
        Assert.IsType<StepOutcome.Trigger>(Resolve(map, new GridPos(2, 3)));
    }

    [Fact]
    public void TrainerSightBeatsEncounter()
    {
        Map map = Map([Spotter(new GridPos(2, 0), Facing.Down)],
            [new EncounterZoneCell(3 * 8 + 2, TableId)]);
        Assert.IsType<StepOutcome.TrainerSpotted>(Resolve(map, new GridPos(2, 3), rng: new CountingRng(0.0)));
    }

    /// <summary>The determinism rule: an earlier transition must not advance the RNG stream, or a
    /// seeded replay diverges the moment a step warps instead of rolling grass.</summary>
    [Fact]
    public void AnEarlierTransition_ConsumesNoRandomness()
    {
        Map map = Map([Warp(new GridPos(2, 2))], [new EncounterZoneCell(2 * 8 + 2, TableId)]);
        var rng = new CountingRng(0.0);

        Assert.IsType<StepOutcome.Warp>(Resolve(map, new GridPos(2, 2), rng: rng));
        Assert.Equal(0, rng.Draws);
    }

    [Fact]
    public void TrainerSightAlsoConsumesNoRandomness()
    {
        Map map = Map([Spotter(new GridPos(2, 0), Facing.Down)],
            [new EncounterZoneCell(3 * 8 + 2, TableId)]);
        var rng = new CountingRng(0.0);

        Resolve(map, new GridPos(2, 3), rng: rng);
        Assert.Equal(0, rng.Draws);
    }

    // --- Conditions and suppression -----------------------------------------------

    [Fact]
    public void ATriggerWithAnUnsetCondition_StaysDormant()
    {
        Map map = Map([Trigger(new GridPos(2, 2), condition: "flag:opened")]);
        Assert.IsType<StepOutcome.None>(Resolve(map, new GridPos(2, 2)));
    }

    [Fact]
    public void ATriggerWithASetCondition_Fires()
    {
        var flags = new FlagStore();
        flags.SetBool("flag:opened", true);
        Map map = Map([Trigger(new GridPos(2, 2), condition: "flag:opened")]);
        Assert.IsType<StepOutcome.Trigger>(Resolve(map, new GridPos(2, 2), flags));
    }

    [Fact]
    public void ADefeatedTrainer_NoLongerSpots()
    {
        var flags = new FlagStore();
        flags.SetBool(TrainerSight.DefeatedFlag(TrainerId), true);
        Map map = Map([Spotter(new GridPos(2, 0), Facing.Down)]);
        Assert.IsType<StepOutcome.None>(Resolve(map, new GridPos(2, 3), flags));
    }

    [Fact]
    public void AnInteractOnlyTrainer_NeverSpots()
    {
        Map map = Map([Spotter(new GridPos(2, 0), Facing.Down)]);
        Assert.IsType<StepOutcome.None>(Resolve(map, new GridPos(2, 3), trainers: Trainers(sightRange: 0)));
    }

    [Fact]
    public void GrassOutsideTheZone_RollsNothing()
    {
        Map map = Map(zones: [new EncounterZoneCell(2 * 8 + 2, TableId)]);
        var rng = new CountingRng(0.0);
        Assert.IsType<StepOutcome.None>(Resolve(map, new GridPos(5, 5), rng: rng));
        Assert.Equal(0, rng.Draws);
    }

    [Fact]
    public void AFailedEncounterRoll_ProducesNothing()
    {
        Map map = Map(zones: [new EncounterZoneCell(2 * 8 + 2, TableId)]);
        // NextDouble returns 0.9 against a 0.1 rate: no encounter.
        Assert.IsType<StepOutcome.None>(
            Resolve(map, new GridPos(2, 2), rng: new CountingRng(0.9), tables: Tables(rate: 0.1)));
    }

    [Fact]
    public void AMissingEncounterTable_IsIgnored()
    {
        Map map = Map(zones: [new EncounterZoneCell(2 * 8 + 2, EntityId.Parse("encounter:absent"))]);
        Assert.IsType<StepOutcome.None>(Resolve(map, new GridPos(2, 2), rng: new CountingRng(0.0)));
    }

    [Fact]
    public void AnEmptySlotTable_ProducesNothing()
    {
        var tables = new Dictionary<EntityId, EncounterTable>
        {
            [TableId] = new EncounterTable { Id = TableId, Name = "Empty", BaseRate = 1.0, Slots = [] },
        };
        Map map = Map(zones: [new EncounterZoneCell(2 * 8 + 2, TableId)]);
        Assert.IsType<StepOutcome.None>(
            Resolve(map, new GridPos(2, 2), rng: new CountingRng(0.0), tables: tables));
    }

    // --- Interaction priority -----------------------------------------------------

    [Fact]
    public void Interact_PrefersTheEntityBeingFaced()
    {
        Map map = Map([
            new SignEntity { Key = "ahead", Pos = new GridPos(2, 1), Text = "ahead" },
            new SignEntity { Key = "under", Pos = new GridPos(2, 2), Text = "under" },
        ]);

        var outcome = Assert.IsType<StepOutcome.Interact>(
            OverworldStep.Interact(new GridPos(2, 2), Facing.Up, map));
        Assert.Equal("ahead", outcome.Entity.Key);
    }

    /// <summary>A trigger is not "talk-to", so it only interacts when nothing else is ahead.</summary>
    [Fact]
    public void Interact_FallsBackToAFacingTrigger()
    {
        Map map = Map([Trigger(new GridPos(2, 1))]);
        var outcome = Assert.IsType<StepOutcome.Interact>(
            OverworldStep.Interact(new GridPos(2, 2), Facing.Up, map));
        Assert.IsType<TriggerEntity>(outcome.Entity);
    }

    [Fact]
    public void Interact_FallsBackToTheCurrentTile()
    {
        Map map = Map([new PickupEntity { Key = "under", Pos = new GridPos(2, 2), Item = EntityId.Parse("item:potion") }]);
        var outcome = Assert.IsType<StepOutcome.Interact>(
            OverworldStep.Interact(new GridPos(2, 2), Facing.Up, map));
        Assert.Equal("under", outcome.Entity.Key);
    }

    [Fact]
    public void Interact_WithNothingAnywhere_ProducesNone() =>
        Assert.IsType<StepOutcome.None>(OverworldStep.Interact(new GridPos(2, 2), Facing.Up, Map()));

    [Fact]
    public void Interact_FacingOffTheMap_IsSafe() =>
        Assert.IsType<StepOutcome.None>(OverworldStep.Interact(new GridPos(0, 0), Facing.Up, Map()));

    // --- Guards -------------------------------------------------------------------

    [Fact]
    public void NullArguments_AreRejected()
    {
        Map map = Map();
        Assert.Throws<ArgumentNullException>(() => OverworldStep.Resolve(default, null!, Open(),
            new FlagStore(), Trainers(), Tables(), new CountingRng()));
        Assert.Throws<ArgumentNullException>(() => OverworldStep.Resolve(default, map, Open(),
            new FlagStore(), Trainers(), Tables(), null!));
        Assert.Throws<ArgumentNullException>(() => OverworldStep.Interact(default, Facing.Up, null!));
    }
}
