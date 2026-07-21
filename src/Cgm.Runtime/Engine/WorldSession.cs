using Cgm.Core.Model;

namespace Cgm.Runtime.Engine;

/// <summary>Owns the play session's world state across map changes: flags, the world RNG stream, and
/// where the player currently is. Scenes borrow it, so walking through a warp preserves flags and
/// keeps one deterministic RNG stream rather than reseeding per map.
///
/// State lives here only until 16E moves it into the Core save; the shape deliberately mirrors the
/// save's map/pos/facing/flags/rngStates fields so that move is a relocation, not a redesign.</summary>
public sealed class WorldSession
{
    private readonly GameDb _db;
    private readonly UiPainter _ui;
    private readonly int _tileSize;
    private readonly int _width;
    private readonly int _height;
    private readonly IReadOnlyDictionary<EntityId, Trainer> _trainers;
    private readonly IReadOnlyDictionary<EntityId, EncounterTable> _tables;

    public WorldSession(GameDb db, UiPainter ui, int tileSize, int virtualWidth, int virtualHeight,
        IRng? rng = null, FlagStore? flags = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(ui);
        if (tileSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(tileSize), tileSize, "Tile size must be positive.");

        _db = db;
        _ui = ui;
        _tileSize = tileSize;
        _width = virtualWidth;
        _height = virtualHeight;
        _trainers = db.All<Trainer>().ToDictionary(t => t.Id);
        _tables = db.All<EncounterTable>().ToDictionary(t => t.Id);
        Rng = rng ?? new Rng(1);
        Flags = flags ?? new FlagStore();
    }

    public FlagStore Flags { get; }

    /// <summary>One world RNG stream for the whole session, so encounters replay identically.</summary>
    public IRng Rng { get; }

    public EntityId CurrentMap { get; private set; }

    public GridPos Position { get; private set; }

    public Facing Facing { get; private set; }

    /// <summary>Builds the scene for a map. Returns null when the map is missing, which validation
    /// already rejects — Runtime reports the defect rather than substituting content.</summary>
    public OverworldScene? Enter(EntityId mapId, GridPos position, Facing facing)
    {
        if (_db.Find<Map>(mapId) is not { } map)
            return null;

        CurrentMap = mapId;
        Position = position;
        Facing = facing;

        IReadOnlyList<Tileset> tilesets = map.Tilesets
            .Select(_db.Find<Tileset>)
            .OfType<Tileset>()
            .ToList();

        return new OverworldScene(_ui, map, tilesets, position, facing, _tileSize, _width, _height,
            Flags, Rng, _trainers, _tables);
    }

    /// <summary>Follows a warp to its target map and landing tile, preserving facing.</summary>
    public OverworldScene? Follow(WarpEntity warp)
    {
        ArgumentNullException.ThrowIfNull(warp);
        return Enter(warp.Target, warp.TargetPos, Facing);
    }

    /// <summary>Records where the player is now, so a later warp or save reads the live position
    /// rather than where the map was entered.</summary>
    public void Track(OverworldScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Position = scene.PlayerPos;
        Facing = scene.PlayerFacing;
    }
}
