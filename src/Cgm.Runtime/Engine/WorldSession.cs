using Cgm.Core.Battle;
using Cgm.Core.Model;

namespace Cgm.Runtime.Engine;

/// <summary>Owns the play session's world state across map changes: flags, the world RNG stream, and
/// where the player currently is. Scenes borrow it, so walking through a warp preserves flags and
/// keeps one deterministic RNG stream rather than reseeding per map.
///
/// It also owns the party and PC boxes, mutated only through Core operations so no scene performs
/// list arithmetic on them. State projects into the Core <see cref="SaveFile"/> through
/// <see cref="ToSave"/> and returns through <see cref="Restore"/>.</summary>
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
        for (int i = 0; i < db.Settings.Boxes.Count; i++)
            Boxes.Add([]);
    }

    private int BoxCapacity => _db.Settings.Boxes.Capacity;

    /// <summary>Builds the starting party from the project's starter list. Each member is generated
    /// through Core with the session RNG, so New Game is reproducible under a fixed seed. Level and
    /// moves come from the species learnset; nothing is invented here.</summary>
    public void InitialiseNewGame(int starterLevel = 5)
    {
        Party.Clear();
        foreach (List<CreatureInstance> box in Boxes)
            box.Clear();

        foreach (EntityId speciesId in _db.Settings.StarterParty.Take(PartyStorage.MaxParty))
        {
            if (_db.Find<Species>(speciesId) is not { } species)
                continue;   // validation rejects this; skip rather than invent a substitute

            IReadOnlyList<MoveSlot> moves = species.Learnset
                .Where(entry => entry.Level <= starterLevel)
                .OrderBy(entry => entry.Level)
                .TakeLast(4)
                .Select(entry => new MoveSlot(entry.Move, _db.Find<Move>(entry.Move)?.Pp ?? 1))
                .ToList();

            Party.Add(InstanceGen.Create(speciesId, species.BaseStats, species.GrowthRate,
                starterLevel, moves, Rng, species.Abilities));
        }
    }

    /// <summary>Routes a caught or gifted creature through Core: party first, then the first box
    /// with room. Null means everything is full.</summary>
    public DepositResult? Deposit(CreatureInstance creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        return PartyStorage.Deposit(creature, Party, Boxes, BoxCapacity);
    }

    /// <summary>True when every party member has fainted, which is the blackout condition.</summary>
    public bool PartyIsWhitedOut => Party.Count > 0 && Party.All(member => member.CurHp <= 0);

    public FlagStore Flags { get; }

    /// <summary>The active party, at most <see cref="PartyStorage.MaxParty"/>. Mutated only through
    /// Core operations, never by list arithmetic in a scene.</summary>
    public List<CreatureInstance> Party { get; } = [];

    /// <summary>PC boxes, sized by project settings. Overflow from a full party lands here.</summary>
    public List<List<CreatureInstance>> Boxes { get; } = [];

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

    /// <summary>Projects the live session into a save record. Only session-owned state is written
    /// here; party, bag, and money join it as 16E adds them.</summary>
    public SaveFile ToSave(string contentHash) => new()
    {
        GameContentHash = contentHash ?? "",
        Map = CurrentMap,
        Pos = Position,
        Facing = Facing,
        Flags = Flags.Snapshot(),
        Party = Party.ToList(),
        Boxes = Boxes.Select(box => (IReadOnlyList<CreatureInstance>)box.ToList()).ToList(),
    };

    /// <summary>Restores session state from a save and returns the scene for its map, or null when
    /// the saved map no longer exists in this content.</summary>
    public OverworldScene? Restore(SaveFile save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (save.Map is not { } map)
            return null;

        Flags.Load(save.Flags);

        // Replace rather than merge: a released creature must not reappear from the old session.
        Party.Clear();
        Party.AddRange(save.Party.Take(PartyStorage.MaxParty));

        foreach (List<CreatureInstance> box in Boxes)
            box.Clear();
        for (int i = 0; i < save.Boxes.Count && i < Boxes.Count; i++)
            Boxes[i].AddRange(save.Boxes[i]);

        return Enter(map, save.Pos, save.Facing);
    }
}
