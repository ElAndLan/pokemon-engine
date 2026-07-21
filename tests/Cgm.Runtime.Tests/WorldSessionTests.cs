using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>ENGINE_RUNTIME_SPEC 16D warp and map switching: session state survives a map change, and
/// missing content is reported rather than substituted.</summary>
public sealed class WorldSessionTests : IDisposable
{
    private const int Tile = 16;
    private const int W = 256;
    private const int H = 192;

    private static readonly EntityId MapA = EntityId.Parse("map:town");
    private static readonly EntityId MapB = EntityId.Parse("map:route");

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;

    public WorldSessionTests()
    {
        TextureHandle atlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, atlas, new BitmapFont());
    }

    public void Dispose() => _renderer.Dispose();

    private static Map Map(EntityId id, IEnumerable<MapEntity>? entities = null) => new()
    {
        Id = id,
        Name = id.Slug,
        Width = 8,
        Height = 8,
        Layers = new MapLayers { Ground = Enumerable.Repeat(0, 64).ToList() },
        Entities = entities?.ToList() ?? [],
    };

    private static WarpEntity Warp(GridPos pos, EntityId target, GridPos landing) => new()
    {
        Key = "warp", Pos = pos, Target = target, TargetPos = landing,
    };

    private WorldSession Session(params Map[] maps)
    {
        var db = new GameDb(new ProjectSettings { Name = "T", TileSize = Tile },
            maps.Cast<IEntity>().ToList());
        return new WorldSession(db, _ui, Tile, W, H);
    }

    private static TickInput Hold(params GameAction[] held) =>
        new(held.ToHashSet(), held.ToHashSet(), new HashSet<GameAction>());

    private static void Walk(OverworldScene scene, GameAction direction, int ticks = 24)
    {
        for (int i = 0; i < ticks; i++)
            scene.Update(Hold(direction));
    }

    // --- Entering -----------------------------------------------------------------

    [Fact]
    public void Enter_BuildsTheSceneAndRecordsPosition()
    {
        WorldSession session = Session(Map(MapA));
        OverworldScene scene = session.Enter(MapA, new GridPos(2, 3), Facing.Left)!;

        Assert.NotNull(scene);
        Assert.Equal(MapA, session.CurrentMap);
        Assert.Equal(new GridPos(2, 3), session.Position);
        Assert.Equal(Facing.Left, session.Facing);
        Assert.Equal(new GridPos(2, 3), scene.PlayerPos);
    }

    /// <summary>Validation rejects a missing map, so Runtime reports rather than substituting one.</summary>
    [Fact]
    public void Enter_AMissingMap_ReturnsNullWithoutChangingState()
    {
        WorldSession session = Session(Map(MapA));
        session.Enter(MapA, new GridPos(1, 1), Facing.Down);

        Assert.Null(session.Enter(EntityId.Parse("map:absent"), default, Facing.Down));
        Assert.Equal(MapA, session.CurrentMap);   // unchanged
    }

    // --- Warping ------------------------------------------------------------------

    [Fact]
    public void Follow_MovesToTheTargetMapAndLandingTile()
    {
        WorldSession session = Session(Map(MapA), Map(MapB));
        session.Enter(MapA, new GridPos(1, 1), Facing.Up);

        OverworldScene next = session.Follow(Warp(new GridPos(2, 2), MapB, new GridPos(5, 6)))!;

        Assert.Equal(MapB, session.CurrentMap);
        Assert.Equal(new GridPos(5, 6), next.PlayerPos);
        Assert.Equal(new GridPos(5, 6), session.Position);
    }

    [Fact]
    public void Follow_PreservesFacingAcrossTheWarp()
    {
        WorldSession session = Session(Map(MapA), Map(MapB));
        session.Enter(MapA, new GridPos(1, 1), Facing.Left);

        OverworldScene next = session.Follow(Warp(new GridPos(2, 2), MapB, new GridPos(0, 0)))!;
        Assert.Equal(Facing.Left, next.PlayerFacing);
    }

    [Fact]
    public void Follow_AMissingTargetMap_ReturnsNull()
    {
        WorldSession session = Session(Map(MapA));
        session.Enter(MapA, new GridPos(1, 1), Facing.Down);

        Assert.Null(session.Follow(Warp(new GridPos(2, 2), EntityId.Parse("map:absent"), default)));
        Assert.Equal(MapA, session.CurrentMap);
    }

    [Fact]
    public void Follow_RejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => Session(Map(MapA)).Follow(null!));

    // --- State that must survive a map change -------------------------------------

    /// <summary>Flags are session state: a flag set on one map is still set after warping.</summary>
    [Fact]
    public void Flags_SurviveAMapChange()
    {
        WorldSession session = Session(Map(MapA), Map(MapB));
        OverworldScene first = session.Enter(MapA, new GridPos(1, 1), Facing.Down)!;
        first.Flags.SetBool("met_rival", true);

        OverworldScene second = session.Follow(Warp(default, MapB, new GridPos(0, 0)))!;
        Assert.True(second.Flags.GetBool("met_rival"));
        Assert.Same(session.Flags, second.Flags);
    }

    /// <summary>One RNG stream for the session: warping must not reseed encounters, or a replay
    /// would diverge every time the player changed maps.</summary>
    [Fact]
    public void TheRngStreamIsSharedAcrossMaps()
    {
        var db = new GameDb(new ProjectSettings { Name = "T", TileSize = Tile },
            [Map(MapA), Map(MapB)]);
        var session = new WorldSession(db, _ui, Tile, W, H, new Rng(7));
        session.Enter(MapA, new GridPos(1, 1), Facing.Down);

        int first = session.Rng.Next(1000);
        session.Follow(Warp(default, MapB, new GridPos(0, 0)));
        int afterWarp = session.Rng.Next(1000);

        // Exact: the session stream must match one uninterrupted Rng(7), not merely differ from
        // its own first draw. A reseed would restart the sequence instead of continuing it.
        var reference = new Rng(7);
        Assert.Equal(reference.Next(1000), first);
        Assert.Equal(reference.Next(1000), afterWarp);
    }

    [Fact]
    public void Track_RecordsWhereThePlayerActuallyWalkedTo()
    {
        WorldSession session = Session(Map(MapA), Map(MapB));
        OverworldScene scene = session.Enter(MapA, new GridPos(1, 1), Facing.Right)!;

        Walk(scene, GameAction.Right, ticks: 16);
        session.Track(scene);

        Assert.Equal(new GridPos(2, 1), session.Position);
        Assert.Equal(scene.PlayerPos, session.Position);
    }

    [Fact]
    public void Track_RejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => Session(Map(MapA)).Track(null!));

    // --- End to end ---------------------------------------------------------------

    /// <summary>Walking onto a warp surfaces it, and following it lands on the other map.</summary>
    [Fact]
    public void WalkingOntoAWarp_SurfacesItAndFollowingItSwitchesMaps()
    {
        Map town = Map(MapA, [Warp(new GridPos(3, 2), MapB, new GridPos(4, 4))]);
        WorldSession session = Session(town, Map(MapB));
        OverworldScene scene = session.Enter(MapA, new GridPos(3, 3), Facing.Up)!;

        Walk(scene, GameAction.Up);
        var warp = Assert.IsType<StepOutcome.Warp>(scene.TakePending());

        OverworldScene next = session.Follow(warp.Entity)!;
        Assert.Equal(MapB, session.CurrentMap);
        Assert.Equal(new GridPos(4, 4), next.PlayerPos);
    }

    [Fact]
    public void WarpingBack_ReturnsToTheOriginalMap()
    {
        Map town = Map(MapA, [Warp(new GridPos(1, 1), MapB, new GridPos(2, 2))]);
        Map route = Map(MapB, [Warp(new GridPos(2, 2), MapA, new GridPos(1, 1))]);
        WorldSession session = Session(town, route);

        session.Enter(MapA, new GridPos(1, 1), Facing.Down);
        session.Follow(Warp(default, MapB, new GridPos(2, 2)));
        Assert.Equal(MapB, session.CurrentMap);

        session.Follow(Warp(default, MapA, new GridPos(1, 1)));
        Assert.Equal(MapA, session.CurrentMap);
        Assert.Equal(new GridPos(1, 1), session.Position);
    }

    // --- Construction -------------------------------------------------------------

    [Fact]
    public void ConstructorGuards_RejectBadArguments()
    {
        var db = new GameDb(new ProjectSettings { Name = "T" }, [Map(MapA)]);
        Assert.Throws<ArgumentNullException>(() => new WorldSession(null!, _ui, Tile, W, H));
        Assert.Throws<ArgumentNullException>(() => new WorldSession(db, null!, Tile, W, H));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldSession(db, _ui, 0, W, H));
    }

    [Fact]
    public void AnInjectedFlagStoreAndRngAreUsed()
    {
        var flags = new FlagStore();
        flags.SetBool("preset", true);
        var db = new GameDb(new ProjectSettings { Name = "T", TileSize = Tile }, [Map(MapA)]);
        var session = new WorldSession(db, _ui, Tile, W, H, new Rng(7), flags);

        Assert.Same(flags, session.Flags);
        Assert.True(session.Enter(MapA, default, Facing.Down)!.Flags.GetBool("preset"));
    }
}
