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
    private readonly SpriteAtlas? _sprites;
    private readonly Animation?[] _walkClips;

    public WorldSession(GameDb db, UiPainter ui, int tileSize, int virtualWidth, int virtualHeight,
        IRng? rng = null, FlagStore? flags = null, SpriteAtlas? sprites = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(ui);
        if (tileSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(tileSize), tileSize, "Tile size must be positive.");

        _db = db;
        _ui = ui;
        _sprites = sprites;
        // Player walk clips resolved once, ordered by Facing (Down, Up, Left, Right); a fresh
        // WalkAnimator wraps them per scene.
        _walkClips = new Animation?[4];
        IReadOnlyList<EntityId> clipIds = db.Settings.PlayerSprites.WalkClips;
        for (int i = 0; i < clipIds.Count && i < _walkClips.Length; i++)
            _walkClips[i] = db.Find<Animation>(clipIds[i]);
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

    /// <summary>Where a blackout returns to. Null until a center is visited, in which case Core falls
    /// back to the project start.</summary>
    public RespawnPoint? Respawn { get; private set; }

    /// <summary>Full-heals the party through Core's rule: full HP, full PP, cleared status.</summary>
    public void Heal()
    {
        IReadOnlyList<CreatureInstance> healed = Recovery.HealParty(Party, _db);
        Party.Clear();
        Party.AddRange(healed);
    }

    /// <summary>Visits a healing service: heal, and set this location as the respawn checkpoint.</summary>
    public void VisitCenter(EntityId map, GridPos pos)
    {
        Heal();
        Respawn = new RespawnPoint(map, pos);
    }

    /// <summary>Blacks out through Core's rule — return to the checkpoint (or the project start when
    /// none was set) and full-heal — and re-enters the resulting map.
    ///
    /// The 16D spec also calls for a Core-calculated money deduction. Core has no such rule today, so
    /// none is applied: inventing a penalty formula in Runtime would be a rule living outside Core.
    /// Recorded as a Core gap in IMPLEMENTATION_PLAN rather than worked around here.</summary>
    public OverworldScene? Blackout() => Restore(Recovery.Blackout(ToSave(""), _db));

    public FlagStore Flags { get; }

    /// <summary>The active party, at most <see cref="PartyStorage.MaxParty"/>. Mutated only through
    /// Core operations, never by list arithmetic in a scene.</summary>
    public List<CreatureInstance> Party { get; } = [];

    /// <summary>PC boxes, sized by project settings. Overflow from a full party lands here.</summary>
    public List<List<CreatureInstance>> Boxes { get; } = [];

    /// <summary>Carried items and their counts. Grouped into authored pockets for display only;
    /// what an item *does* stays in its definition and in Core.</summary>
    public Dictionary<EntityId, int> Bag { get; } = [];

    /// <summary>Adds items, merging with any already carried.</summary>
    public void AddItem(EntityId item, int count = 1)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be positive.");
        Bag[item] = Bag.GetValueOrDefault(item) + count;
    }

    /// <summary>Consumes items, returning false when there are not enough. The bag never goes
    /// negative, so a failed consume leaves the count untouched.</summary>
    public bool ConsumeItem(EntityId item, int count = 1)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "Item count must be positive.");
        if (Bag.GetValueOrDefault(item) < count)
            return false;

        int remaining = Bag[item] - count;
        if (remaining == 0)
            Bag.Remove(item);
        else
            Bag[item] = remaining;
        return true;
    }

    public int ItemCount(EntityId item) => Bag.GetValueOrDefault(item);

    /// <summary>Carried capture devices, identified by their authored pocket. Pocket membership is
    /// display grouping the project already defines; the capture maths stays in Core.</summary>
    public IReadOnlyList<EntityId> CaptureItems(string pocket = "balls") =>
        Bag.Keys
            .Where(id => _db.Find<Item>(id) is { } item
                && string.Equals(item.Pocket, pocket, StringComparison.OrdinalIgnoreCase))
            .OrderBy(id => id.ToString(), StringComparer.Ordinal)
            .ToList();

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

        // A fresh animator per scene, so walk-cycle timing starts clean on each map entry.
        return new OverworldScene(_ui, map, tilesets, position, facing, _tileSize, _width, _height,
            Flags, Rng, _trainers, _tables, _sprites, new WalkAnimator(_walkClips));
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
        Respawn = Respawn,
        Bag = BagByPocket(),
    };

    /// <summary>The save groups the bag by pocket (DATA_SCHEMA §5). An item whose definition is
    /// missing falls into the default pocket rather than being dropped from the save.</summary>
    private Dictionary<string, IReadOnlyList<BagEntry>> BagByPocket()
    {
        var pockets = new Dictionary<string, List<BagEntry>>(StringComparer.Ordinal);
        foreach ((EntityId id, int count) in Bag.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            string pocket = _db.Find<Item>(id)?.Pocket ?? "items";
            if (!pockets.TryGetValue(pocket, out List<BagEntry>? entries))
                pockets[pocket] = entries = [];
            entries.Add(new BagEntry(id, count));
        }
        return pockets.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<BagEntry>)pair.Value);
    }

    /// <summary>Restores session state from a save and returns the scene for its map, or null when
    /// the saved map no longer exists in this content.</summary>
    public OverworldScene? Restore(SaveFile save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (save.Map is not { } map)
            return null;

        Flags.Load(save.Flags);
        Respawn = save.Respawn;

        // Replace rather than merge: a released creature must not reappear from the old session.
        Party.Clear();
        Party.AddRange(save.Party.Take(PartyStorage.MaxParty));

        foreach (List<CreatureInstance> box in Boxes)
            box.Clear();
        for (int i = 0; i < save.Boxes.Count && i < Boxes.Count; i++)
            Boxes[i].AddRange(save.Boxes[i]);

        // Pockets are display grouping, so the bag flattens back to counts. A duplicate entry across
        // pockets sums rather than overwriting, which a hand-edited save could otherwise lose.
        Bag.Clear();
        foreach (BagEntry entry in save.Bag.Values.SelectMany(entries => entries))
            if (entry.Count > 0)
                Bag[entry.Item] = Bag.GetValueOrDefault(entry.Item) + entry.Count;

        return Enter(map, save.Pos, save.Facing);
    }
}
