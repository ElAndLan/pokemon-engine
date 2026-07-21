using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>PHASE_16_DEMO_PLAN §3.3 lifecycle, driven headlessly through the real scene stack: walk
/// the overworld, take an encounter, play the battle, and return with progression applied. This
/// mirrors what RuntimeHost sequences, so the demo path is covered without a window.</summary>
public sealed class DemoLifecycleTests : IDisposable
{
    private const int Tile = 16;
    private const int W = 256;
    private const int H = 192;

    private static readonly EntityId MapId = EntityId.Parse("map:field");
    private static readonly EntityId TypeId = EntityId.Parse("type:plain");
    private static readonly EntityId MoveId = EntityId.Parse("move:tackle");
    private static readonly EntityId Starter = EntityId.Parse("species:pebbling");
    private static readonly EntityId WildId = EntityId.Parse("species:sprig");
    private static readonly EntityId TableId = EntityId.Parse("encounter:grass");

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;
    private readonly SceneStack _scenes = new();

    public DemoLifecycleTests()
    {
        TextureHandle atlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, atlas, new BitmapFont());
    }

    public void Dispose()
    {
        _scenes.Dispose();
        _renderer.Dispose();
    }

    private static Species Species(EntityId id) => new()
    {
        Id = id, Name = id.Slug, Types = [TypeId],
        BaseStats = new Stats(60, 50, 50, 50, 50, 50),
        GrowthRate = "medium-fast", BaseExp = 64,
        Learnset = [new LearnsetEntry { Level = 1, Move = MoveId }],
    };

    /// <summary>A map whose every tile is an encounter zone, so any completed step rolls.</summary>
    private static GameDb Db()
    {
        var map = new Map
        {
            Id = MapId, Name = "Field", Width = 8, Height = 8,
            Layers = new MapLayers { Ground = Enumerable.Repeat(0, 64).ToList() },
            EncounterZones = Enumerable.Range(0, 64)
                .Select(i => new EncounterZoneCell(i, TableId)).ToList(),
        };
        var settings = new ProjectSettings
        {
            Name = "Demo", TileSize = Tile, StartMap = MapId, StartPos = new GridPos(2, 2),
            StarterParty = [Starter], Boxes = new BoxConfig { Count = 1, Capacity = 5 },
        };
        return new GameDb(settings,
        [
            map, Species(Starter), Species(WildId),
            new TypeDef { Id = TypeId, Name = "Plain" },
            new Move
            {
                Id = MoveId, Name = "Tackle", Type = TypeId, Pp = 35, Power = 40,
                DamageClass = DamageClass.Physical, Accuracy = 100,
            },
            new EncounterTable
            {
                Id = TableId, Name = "Grass", BaseRate = 1.0,   // every step encounters
                Slots = [new EncounterSlot { Species = WildId, Weight = 1, MinLevel = 3, MaxLevel = 3 }],
            },
        ]);
    }

    private static TickInput Press(params GameAction[] actions) =>
        new(actions.ToHashSet(), actions.ToHashSet(), new HashSet<GameAction>());

    private static readonly TickInput Idle =
        new(new HashSet<GameAction>(), new HashSet<GameAction>(), new HashSet<GameAction>());

    private void Frame(TickInput input)
    {
        _scenes.Tick(input);
        _renderer.BeginFrame(new Viewport(2, 0, 0, W * 2, H * 2), W, H, new Rgba(0, 0, 0, 255));
        _batch.Begin();
        _scenes.Render();
        var (quads, calls, _) = _batch.End();
        _renderer.Draw(quads, calls);
        _renderer.EndFrame();
    }

    /// <summary>Walks until the overworld reports an encounter, mirroring the host's Advance.</summary>
    private StepOutcome? WalkUntilEncounter(OverworldScene overworld, int maxTicks = 400)
    {
        for (int i = 0; i < maxTicks; i++)
        {
            Frame(Press(GameAction.Right));
            if (overworld.TakePending() is { } pending)
                return pending;
        }
        return null;
    }

    [Fact]
    public void WalkingInGrass_ProducesAWildEncounter()
    {
        GameDb db = Db();
        var session = new WorldSession(db, _ui, Tile, W, H, new Rng(5));
        session.InitialiseNewGame();

        OverworldScene overworld = session.Enter(MapId, new GridPos(2, 2), Facing.Right)!;
        _scenes.Push(overworld);
        Frame(Idle);

        Assert.IsType<StepOutcome.WildEncounter>(WalkUntilEncounter(overworld));
    }

    /// <summary>The full §3.3 path: encounter, battle, outcome, and progression back in the session.</summary>
    [Fact]
    public void EncounterThroughBattleToProgression()
    {
        GameDb db = Db();
        var session = new WorldSession(db, _ui, Tile, W, H, new Rng(5));
        session.InitialiseNewGame();
        long expBefore = session.Party[0].Exp;

        OverworldScene overworld = session.Enter(MapId, new GridPos(2, 2), Facing.Right)!;
        _scenes.Push(overworld);
        Frame(Idle);

        var encounter = Assert.IsType<StepOutcome.WildEncounter>(WalkUntilEncounter(overworld));

        BattleStart start = BattleLauncher.Wild(db, session, encounter.Species, encounter.Level, session.Rng);
        Assert.True(start.Started);

        var presenter = new BattleScene(start.Battle!,
            b => new UseMove(RandomAi.ChooseMove(b.Active(BattleSide.Enemy), session.Rng)));
        var battle = new BattleHostScene(_ui, presenter, W, H);
        _scenes.Replace(battle, fade: false);
        Frame(Idle);

        // Play it out: Confirm both submits actions and advances presentation.
        for (int i = 0; i < 4000 && !battle.Finished; i++)
            Frame(Press(GameAction.Confirm));

        Assert.True(battle.Finished, "the battle should reach an outcome");
        Assert.NotEmpty(battle.Log);

        BattleLauncher.ApplyResult(session, start.Battle!);
        if (battle.Outcome?.Winner == BattleSide.Player)
        {
            BattleLauncher.AwardExperience(db, session, start.Battle!, trainer: false);
            Assert.True(session.Party[0].Exp > expBefore, "winning should award experience");
        }

        // Return to the overworld, exactly as the host does.
        OverworldScene returned = session.Enter(session.CurrentMap, session.Position, session.Facing)!;
        _scenes.Replace(returned, fade: false);
        Frame(Idle);

        Assert.Same(returned, _scenes.Active);
        Assert.Equal(MapId, session.CurrentMap);
    }

    /// <summary>Party HP written back from the battle must survive the return to the overworld.</summary>
    [Fact]
    public void BattleDamageCarriesBackToTheOverworldSession()
    {
        GameDb db = Db();
        var session = new WorldSession(db, _ui, Tile, W, H, new Rng(5));
        session.InitialiseNewGame();
        session.Enter(MapId, new GridPos(2, 2), Facing.Right);

        BattleStart start = BattleLauncher.Wild(db, session, WildId, 3, session.Rng);
        var presenter = new BattleScene(start.Battle!,
            b => new UseMove(RandomAi.ChooseMove(b.Active(BattleSide.Enemy), session.Rng)));
        var battle = new BattleHostScene(_ui, presenter, W, H);
        _scenes.Push(battle);

        for (int i = 0; i < 4000 && !battle.Finished; i++)
            Frame(Press(GameAction.Confirm));

        BattleLauncher.ApplyResult(session, start.Battle!);
        Assert.Equal(start.Battle!.Party(BattleSide.Player)[0].CurrentHp, session.Party[0].CurHp);
    }

    /// <summary>A whited-out party blacks out to the checkpoint instead of resuming where it fell.</summary>
    [Fact]
    public void AWhitedOutPartyBlacksOutToTheCheckpoint()
    {
        GameDb db = Db();
        var session = new WorldSession(db, _ui, Tile, W, H, new Rng(5));
        session.InitialiseNewGame();
        session.Enter(MapId, new GridPos(6, 6), Facing.Right);
        session.VisitCenter(MapId, new GridPos(1, 1));
        session.Enter(MapId, new GridPos(6, 6), Facing.Right);

        session.Party[0] = session.Party[0] with { CurHp = 0 };
        Assert.True(session.PartyIsWhitedOut);

        OverworldScene recovered = session.Blackout()!;
        _scenes.Push(recovered);
        Frame(Idle);

        Assert.Equal(new GridPos(1, 1), recovered.PlayerPos);
        Assert.False(session.PartyIsWhitedOut);
    }

    [Fact]
    public void TheBattleSceneRendersThroughTheRealBatch()
    {
        GameDb db = Db();
        var session = new WorldSession(db, _ui, Tile, W, H, new Rng(5));
        session.InitialiseNewGame();

        BattleStart start = BattleLauncher.Wild(db, session, WildId, 3, session.Rng);
        var presenter = new BattleScene(start.Battle!, b => new UseMove(0));
        _scenes.Push(new BattleHostScene(_ui, presenter, W, H));
        Frame(Idle);

        Assert.NotEmpty(_renderer.Drawn);
        Assert.All(_renderer.Drawn, q => Assert.False(q.Dest.IsEmpty));
    }
}
