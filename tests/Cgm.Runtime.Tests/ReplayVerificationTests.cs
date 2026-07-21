using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16G acceptance: a scripted replay is deterministic across runs, the
/// same content produces identical results from a raw folder and from a pack, and a save round trip
/// preserves the world. The trace is the evidence — equal traces mean equal simulation.</summary>
public sealed class ReplayVerificationTests : IDisposable
{
    private const int Tile = 16;
    private static readonly EntityId MapId = EntityId.Parse("map:field");
    private static readonly EntityId TypeId = EntityId.Parse("type:plain");
    private static readonly EntityId MoveId = EntityId.Parse("move:tackle");
    private static readonly EntityId Starter = EntityId.Parse("species:pebbling");
    private static readonly EntityId WildId = EntityId.Parse("species:sprig");
    private static readonly EntityId TrainerId = EntityId.Parse("trainer:rival");
    private static readonly EntityId TableId = EntityId.Parse("encounter:grass");
    private static readonly EntityId Orb = EntityId.Parse("item:capture_orb");

    private readonly string _dir = Directory.CreateTempSubdirectory("cgm-replay").FullName;
    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;

    public ReplayVerificationTests()
    {
        TextureHandle atlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, atlas, new BitmapFont());
    }

    public void Dispose()
    {
        _renderer.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private static Species Species(EntityId id) => new()
    {
        Id = id, Name = id.Slug, Types = [TypeId],
        BaseStats = new Stats(60, 50, 50, 50, 50, 50),
        GrowthRate = "medium-fast", BaseExp = 64, CatchRate = 255,
        Learnset = [new LearnsetEntry { Level = 1, Move = MoveId }],
    };

    /// <summary>The neutral fixture: a map with grass, a sign to talk to, a trigger, a warp target,
    /// and a trainer watching a column — the demo's interaction classes in one place.</summary>
    private static Project Fixture()
    {
        var field = new Map
        {
            Id = MapId, Name = "Field", Width = 10, Height = 10,
            Layers = new MapLayers { Ground = Enumerable.Repeat(0, 100).ToList() },
            Entities =
            [
                new PlayerStartEntity { Key = "start", Pos = new GridPos(1, 1) },

                // Directly below the start, so walking down steps onto it before reaching grass.
                new TriggerEntity
                {
                    Key = "gate", Pos = new GridPos(1, 2),
                    Actions = [new TriggerAction { Op = TriggerOp.SetFlag, Flag = "passed_gate", Value = 1 }],
                },

                // Off the route, so the trace records a sighting only if the script goes looking.
                new NpcEntity
                {
                    Key = "rival", Pos = new GridPos(8, 0), Facing = Facing.Down, Trainer = TrainerId,
                },
            ],

            // Grass fills rows 4 and below, so walking down reliably enters it.
            EncounterZones = Enumerable.Range(40, 60).Select(i => new EncounterZoneCell(i, TableId)).ToList(),
        };

        var settings = new ProjectSettings
        {
            Name = "Replay Fixture", TileSize = Tile,
            StartMap = MapId, StartPos = new GridPos(1, 1), StartFacing = Facing.Down,
            StarterParty = [Starter], Boxes = new BoxConfig { Count = 2, Capacity = 4 },
        };

        var entities = new Dictionary<EntityId, IEntity>
        {
            [MapId] = field,
            [Starter] = Species(Starter),
            [WildId] = Species(WildId),
            [TypeId] = new TypeDef { Id = TypeId, Name = "Plain" },
            [MoveId] = new Move
            {
                Id = MoveId, Name = "Tackle", Type = TypeId, Pp = 35, Power = 40,
                DamageClass = DamageClass.Physical, Accuracy = 100,
            },
            [Orb] = new Item { Id = Orb, Name = "Capture Orb", Pocket = "balls", Consumable = true },
            [TableId] = new EncounterTable
            {
                Id = TableId, Name = "Grass", BaseRate = 0.5,
                Slots = [new EncounterSlot { Species = WildId, Weight = 1, MinLevel = 3, MaxLevel = 5 }],
            },
            [TrainerId] = new Trainer
            {
                Id = TrainerId, Name = "Rival", SightRange = 4, AiProfile = AiProfile.Basic,
                Party = [new PartyMember { Species = WildId, Level = 4 }],
            },
        };

        return new Project(settings, entities);
    }

    /// <summary>The fixture route: step the trigger, walk down into grass, then cross it. Long enough
    /// that encounters and the trigger both land, so the trace has real content to compare.</summary>
    private static ReplayScript Route(int seed = 1) => ReplayScript.Of(seed,
        (40, ReplayStep.Hold(GameAction.Down)),    // over the gate trigger
        (10, ReplayStep.Idle),
        (120, ReplayStep.Hold(GameAction.Down)),   // down into the grass rows
        (80, ReplayStep.Hold(GameAction.Right)),   // across the grass
        (10, ReplayStep.Idle));

    private GameDb RawDb()
    {
        Project project = Fixture();
        string folder = Path.Combine(_dir, "raw");
        Directory.CreateDirectory(folder);
        ProjectFile.Save(folder, project.Settings);
        foreach (IEntity entity in project.Entities)
        {
            string dir = Directory.CreateDirectory(Path.Combine(folder, "data",
                entity.Id.Category.ToString().ToLowerInvariant())).FullName;
            File.WriteAllText(Path.Combine(dir, $"{entity.Id.Slug}.json"), CgmJson.SerializeEntity(entity));
        }

        Assert.True(BootLoader.TryLoad(new BootArgs(folder, false, false, null), _dir,
            out RuntimeContent? content, out BootDiagnostic? error), error?.Summary);
        return content!.Db;
    }

    private GameDb PackedDb()
    {
        string folder = Path.Combine(_dir, "packed");
        Directory.CreateDirectory(folder);
        using (FileStream pack = File.Create(Path.Combine(folder, "game.cgmpack")))
            CgmPack.Write(GameDb.FromProject(Fixture()), pack);
        File.WriteAllText(Path.Combine(folder, Exporter.ConfigFileName), CgmJson.Serialize(new RuntimeConfig
        {
            GameName = "Replay Fixture", VirtualWidth = 256, VirtualHeight = 192, PackPath = "game.cgmpack",
        }));

        Assert.True(BootLoader.TryLoad(new BootArgs(null, false, false, null), folder,
            out RuntimeContent? content, out BootDiagnostic? error), error?.Summary);
        return content!.Db;
    }

    private ReplayTrace Run(GameDb db, ReplayScript script) => ReplayHarness.Run(db, _ui, script, Tile);

    // --- Determinism --------------------------------------------------------------

    /// <summary>The headline 16G row: the same script twice must produce byte-identical traces.</summary>
    [Fact]
    public void AScriptReplaysIdenticallyTwice()
    {
        GameDb db = RawDb();
        Assert.Equal(Run(db, Route()).Digest, Run(db, Route()).Digest);
    }

    [Fact]
    public void ReplayIsIdenticalAcrossFreshlyLoadedDatabases() =>
        Assert.Equal(Run(RawDb(), Route()).Digest, Run(RawDb(), Route()).Digest);

    /// <summary>A different seed must change the run, or the seed is not reaching the simulation and
    /// the determinism above would be vacuous.</summary>
    [Fact]
    public void ADifferentSeedProducesADifferentReplay()
    {
        GameDb db = RawDb();
        Assert.NotEqual(Run(db, Route(seed: 1)).Digest, Run(db, Route(seed: 9999)).Digest);
    }

    [Fact]
    public void ADifferentScriptProducesADifferentReplay()
    {
        GameDb db = RawDb();
        ReplayScript other = ReplayScript.Of(1, (30, ReplayStep.Hold(GameAction.Up)));
        Assert.NotEqual(Run(db, Route()).Digest, Run(db, other).Digest);
    }

    // --- Raw versus packed parity -------------------------------------------------

    /// <summary>The 16A parity promise, proven through play rather than through loading: identical
    /// content from either source must simulate identically.</summary>
    [Fact]
    public void RawAndPackedContentReplayIdentically()
    {
        ReplayTrace raw = Run(RawDb(), Route());
        ReplayTrace packed = Run(PackedDb(), Route());

        Assert.Equal(raw.Lines, packed.Lines);
        Assert.Equal(raw.SaveJson, packed.SaveJson);
        Assert.Equal(raw.Digest, packed.Digest);
    }

    [Fact]
    public void RawAndPackedAgreeAcrossSeveralSeeds()
    {
        GameDb raw = RawDb();
        GameDb packed = PackedDb();

        foreach (int seed in new[] { 1, 7, 42, 1234 })
            Assert.Equal(Run(raw, Route(seed)).Digest, Run(packed, Route(seed)).Digest);
    }

    // --- The trace is meaningful --------------------------------------------------

    /// <summary>A trace that recorded nothing would make every comparison above pass vacuously.</summary>
    [Fact]
    public void TheTraceRecordsTheRouteAndFinalState()
    {
        ReplayTrace trace = Run(RawDb(), Route());

        Assert.Contains(trace.Lines, line => line.StartsWith("seed ", StringComparison.Ordinal));
        Assert.Contains(trace.Lines, line => line.StartsWith("start map=", StringComparison.Ordinal));
        Assert.Contains(trace.Lines, line => line.StartsWith("end map=", StringComparison.Ordinal));
        Assert.Contains(trace.Lines, line => line.Contains("party[0]"));
        Assert.True(trace.Lines.Count > 5, $"trace only had {trace.Lines.Count} lines");
    }

    /// <summary>The route must actually do something: the player ends somewhere other than the start.</summary>
    [Fact]
    public void TheRouteMovesThePlayer()
    {
        ReplayTrace trace = Run(RawDb(), Route());
        string start = trace.Lines.First(l => l.StartsWith("start map=", StringComparison.Ordinal));
        string end = trace.Lines.First(l => l.StartsWith("end map=", StringComparison.Ordinal));

        Assert.NotEqual(start["start ".Length..], end["end ".Length..]);
    }

    [Fact]
    public void TheRouteReachesTheFixturesInteractions()
    {
        ReplayTrace trace = Run(RawDb(), Route());
        Assert.Contains(trace.Lines, line =>
            line.StartsWith("trigger", StringComparison.Ordinal)
            || line.StartsWith("wild", StringComparison.Ordinal)
            || line.StartsWith("trainer", StringComparison.Ordinal)
            || line.StartsWith("interact", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSaveJsonIsRealAndParsable()
    {
        ReplayTrace trace = Run(RawDb(), Route());
        SaveFile save = CgmJson.Deserialize<SaveFile>(trace.SaveJson);

        Assert.Equal(MapId, save.Map);
        Assert.NotEmpty(save.Party);
    }

    // --- Save and relaunch --------------------------------------------------------

    /// <summary>The save a replay produces must restore to the same world it described.</summary>
    [Fact]
    public void ReplayingThenRestoringPreservesTheWorld()
    {
        GameDb db = RawDb();
        ReplayTrace trace = Run(db, Route());
        SaveFile save = CgmJson.Deserialize<SaveFile>(trace.SaveJson);

        var restored = new WorldSession(db, _ui, Tile, 256, 192, new Rng(1));
        Assert.NotNull(restored.Restore(save));

        Assert.Equal(save.Map, restored.CurrentMap);
        Assert.Equal(save.Pos, restored.Position);
        Assert.Equal(save.Facing, restored.Facing);
        Assert.Equal(save.Party.Count, restored.Party.Count);
        Assert.Equal(CgmJson.Serialize(save), CgmJson.Serialize(restored.ToSave(save.GameContentHash)));
    }

    [Fact]
    public void ADurableSaveRoundTripMatchesTheReplayState()
    {
        GameDb db = RawDb();
        ReplayTrace trace = Run(db, Route());
        SaveFile save = CgmJson.Deserialize<SaveFile>(trace.SaveJson);

        var repo = new SaveRepository(Path.Combine(_dir, "slot"));
        repo.Write(save);

        SaveLoadResult loaded = repo.Load(save.GameContentHash);
        Assert.True(loaded.Succeeded, loaded.Message);
        Assert.Equal(CgmJson.Serialize(save), CgmJson.Serialize(loaded.Save));
    }

    // --- Guards -------------------------------------------------------------------

    [Fact]
    public void AnEmptyScriptStillProducesAValidTrace()
    {
        ReplayTrace trace = Run(RawDb(), new ReplayScript([]));
        Assert.NotEmpty(trace.Lines);
        Assert.NotEmpty(trace.Digest);
    }

    [Fact]
    public void NullArguments_AreRejected()
    {
        GameDb db = RawDb();
        Assert.Throws<ArgumentNullException>(() => ReplayHarness.Run(null!, _ui, Route()));
        Assert.Throws<ArgumentNullException>(() => ReplayHarness.Run(db, null!, Route()));
        Assert.Throws<ArgumentNullException>(() => ReplayHarness.Run(db, _ui, null!));
    }

    /// <summary>Content with no start map traces the failure rather than throwing.</summary>
    [Fact]
    public void ContentWithoutAStartMapTracesTheFailure()
    {
        Project fixture = Fixture();
        var db = new GameDb(fixture.Settings with { StartMap = null }, fixture.Entities.ToList());

        ReplayTrace trace = ReplayHarness.Run(db, _ui, Route(), Tile);
        Assert.Contains("no start map", trace.Lines);
    }
}
