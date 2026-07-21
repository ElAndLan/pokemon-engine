using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16D/16E healing services and blackout. Every restoration rule is
/// Core's; these assert Runtime routes through it and persists the checkpoint.</summary>
public sealed class RecoveryFlowTests : IDisposable
{
    private const int Tile = 16;
    private static readonly EntityId Town = EntityId.Parse("map:town");
    private static readonly EntityId Route = EntityId.Parse("map:route");
    private static readonly EntityId TypeId = EntityId.Parse("type:plain");
    private static readonly EntityId MoveId = EntityId.Parse("move:tackle");
    private static readonly EntityId Starter = EntityId.Parse("species:pebbling");

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;

    public RecoveryFlowTests()
    {
        TextureHandle atlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, atlas, new BitmapFont());
    }

    public void Dispose() => _renderer.Dispose();

    private static Map Map(EntityId id) => new()
    {
        Id = id, Name = id.Slug, Width = 8, Height = 8,
        Layers = new MapLayers { Ground = Enumerable.Repeat(0, 64).ToList() },
    };

    private WorldSession Session()
    {
        var settings = new ProjectSettings
        {
            Name = "T", TileSize = Tile, StarterParty = [Starter],
            StartMap = Town, StartPos = new GridPos(1, 1),
            Boxes = new BoxConfig { Count = 1, Capacity = 5 },
        };
        var db = new GameDb(settings,
        [
            Map(Town), Map(Route),
            new Species
            {
                Id = Starter, Name = "Pebbling", Types = [TypeId],
                BaseStats = new Stats(50, 50, 50, 50, 50, 50), GrowthRate = "medium-fast",
                Learnset = [new LearnsetEntry { Level = 1, Move = MoveId }],
            },
            new TypeDef { Id = TypeId, Name = "Plain" },
            new Move { Id = MoveId, Name = "Tackle", Type = TypeId, Pp = 35 },
        ]);

        var session = new WorldSession(db, _ui, Tile, 256, 192, new Rng(1));
        session.InitialiseNewGame();
        return session;
    }

    private static void Hurt(WorldSession session, int hp = 1) =>
        session.Party[0] = session.Party[0] with
        {
            CurHp = hp,
            Status = PersistentStatus.Poison,
            Moves = [new MoveSlot(MoveId, 0)],
        };

    // --- Healing ------------------------------------------------------------------

    [Fact]
    public void Heal_RestoresHpStatusAndPp()
    {
        WorldSession session = Session();
        int maxHp = session.Party[0].CurHp;
        Hurt(session);

        session.Heal();

        Assert.Equal(maxHp, session.Party[0].CurHp);
        Assert.Null(session.Party[0].Status);
        Assert.Equal(35, session.Party[0].Moves[0].Pp);
    }

    [Fact]
    public void Heal_RevivesAFaintedMember()
    {
        WorldSession session = Session();
        Hurt(session, hp: 0);
        session.Heal();

        Assert.True(session.Party[0].CurHp > 0);
        Assert.False(session.PartyIsWhitedOut);
    }

    [Fact]
    public void Heal_OnAnEmptyParty_IsHarmless()
    {
        WorldSession session = Session();
        session.Party.Clear();
        session.Heal();
        Assert.Empty(session.Party);
    }

    // --- Checkpoints --------------------------------------------------------------

    [Fact]
    public void VisitCenter_HealsAndRecordsTheCheckpoint()
    {
        WorldSession session = Session();
        Hurt(session);

        session.VisitCenter(Town, new GridPos(4, 5));

        Assert.True(session.Party[0].CurHp > 1);
        Assert.Equal(new RespawnPoint(Town, new GridPos(4, 5)), session.Respawn);
    }

    [Fact]
    public void NoCheckpointIsSetUntilACenterIsVisited() => Assert.Null(Session().Respawn);

    [Fact]
    public void VisitingASecondCenter_MovesTheCheckpoint()
    {
        WorldSession session = Session();
        session.VisitCenter(Town, new GridPos(1, 1));
        session.VisitCenter(Route, new GridPos(7, 7));

        Assert.Equal(new RespawnPoint(Route, new GridPos(7, 7)), session.Respawn);
    }

    [Fact]
    public void TheCheckpointSurvivesASaveRoundTrip()
    {
        WorldSession first = Session();
        first.Enter(Town, new GridPos(2, 2), Facing.Down);
        first.VisitCenter(Route, new GridPos(3, 3));

        WorldSession second = Session();
        second.Restore(first.ToSave("h"));

        Assert.Equal(new RespawnPoint(Route, new GridPos(3, 3)), second.Respawn);
    }

    // --- Blackout -----------------------------------------------------------------

    [Fact]
    public void Blackout_ReturnsToTheCheckpointAndHeals()
    {
        WorldSession session = Session();
        session.Enter(Route, new GridPos(6, 6), Facing.Up);
        session.VisitCenter(Town, new GridPos(2, 3));
        session.Enter(Route, new GridPos(6, 6), Facing.Up);
        Hurt(session, hp: 0);

        OverworldScene scene = session.Blackout()!;

        Assert.Equal(Town, session.CurrentMap);
        Assert.Equal(new GridPos(2, 3), scene.PlayerPos);
        Assert.True(session.Party[0].CurHp > 0);
        Assert.False(session.PartyIsWhitedOut);
    }

    /// <summary>Without a visited centre, Core falls back to the project start.</summary>
    [Fact]
    public void Blackout_WithoutACheckpoint_ReturnsToTheProjectStart()
    {
        WorldSession session = Session();
        session.Enter(Route, new GridPos(6, 6), Facing.Up);
        Hurt(session, hp: 0);

        OverworldScene scene = session.Blackout()!;

        Assert.Equal(Town, session.CurrentMap);
        Assert.Equal(new GridPos(1, 1), scene.PlayerPos);
    }

    [Fact]
    public void Blackout_RestoresEveryPartyMember()
    {
        WorldSession session = Session();
        session.Enter(Town, new GridPos(1, 1), Facing.Down);
        session.Party.Add(session.Party[0] with { CurHp = 0 });
        Hurt(session, hp: 0);

        session.Blackout();
        Assert.All(session.Party, member => Assert.True(member.CurHp > 0));
    }

    [Fact]
    public void Blackout_PreservesFlagsAndBoxes()
    {
        WorldSession session = Session();
        session.Enter(Town, new GridPos(1, 1), Facing.Down);
        session.Flags.SetInt("badges", 2);
        session.Boxes[0].Add(session.Party[0]);
        Hurt(session, hp: 0);

        session.Blackout();

        Assert.Equal(2, session.Flags.GetInt("badges"));
        Assert.Single(session.Boxes[0]);
    }

    /// <summary>Blackout faces the player down on arrival, per Core's rule.</summary>
    [Fact]
    public void Blackout_FacesThePlayerDown()
    {
        WorldSession session = Session();
        session.Enter(Route, new GridPos(6, 6), Facing.Up);
        Hurt(session, hp: 0);

        Assert.Equal(Facing.Down, session.Blackout()!.PlayerFacing);
    }

    [Fact]
    public void Blackout_KeepsTheCheckpointForNextTime()
    {
        WorldSession session = Session();
        session.VisitCenter(Town, new GridPos(2, 3));
        session.Enter(Route, new GridPos(6, 6), Facing.Up);
        Hurt(session, hp: 0);

        session.Blackout();
        Assert.Equal(new RespawnPoint(Town, new GridPos(2, 3)), session.Respawn);
    }
}
