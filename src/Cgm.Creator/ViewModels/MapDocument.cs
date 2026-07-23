using Cgm.Core.Model;
using Cgm.Creator.Maps;

namespace Cgm.Creator.ViewModels;

public enum MapLayerId { Ground, DecoBelow, DecoAbove }
public enum MapTool { Paint, RectFill, Bucket, Eyedropper, Erase }
public enum MapEditMode { Tiles, Collision, Encounters, Entities }

/// <summary>One palette entry: a global tile index and the sprite that draws it.</summary>
public sealed record PaletteTile(int Index, EntityId Tileset, int LocalIndex, EntityId? Sprite);

/// <summary>
/// Map editor (MAP_EDITOR_SPEC 17C). Visual-layer painting goes through the pure
/// <see cref="MapLayerOps"/>; collision and encounter overlays edit the sparse override lists.
/// Every committed change is one undoable whole-record <see cref="Map"/> snapshot, and a pointer
/// stroke (press → drag → release) commits exactly one snapshot via <see cref="BeginStroke"/> /
/// <see cref="EndStroke"/>, so a drag across fifty cells is a single undo step.
/// </summary>
public sealed class MapDocument : EntityEditorDocument<Map>
{
    private int[]? _strokeLayer;   // working copy during a stroke; committed on EndStroke
    private int[]? _strokeOriginal;
    private bool _strokeChanged;
    private (int X, int Y)? _lastStrokeCell;

    public MapDocument(ProjectSession session, Map model) : base(session, model) =>
        SelectedTile = Palette.Count > 0 ? 0 : MapLayerOps.Empty;

    public int Width => Model.Width;
    public int Height => Model.Height;
    public int TileSize => Session.Settings.TileSize;

    /// <summary>The sprite that draws a global tile index, or null (empty or unknown index).</summary>
    public EntityId? SpriteFor(int tileIndex) => Palette.At(tileIndex)?.Sprite;

    // --- Active editing state (presentation only, never serialized) ---
    public MapLayerId ActiveLayer { get; set; } = MapLayerId.Ground;
    public MapTool Tool { get; set; } = MapTool.Paint;
    public MapEditMode EditMode { get; set; } = MapEditMode.Tiles;
    public int SelectedTile { get; set; }
    public CollisionValue SelectedCollision { get; set; } = CollisionValue.Solid;
    public EntityId? SelectedEncounterTable { get; set; }

    /// <summary>The map's tilesets flattened into the global index space layers store into. Rebuilt
    /// whenever the map's tileset list changes (including undo/redo) — the immutable record hands us
    /// a fresh list reference each edit, so a cheap reference check decides when to reflatten instead
    /// of allocating a palette per render access.</summary>
    private IReadOnlyList<EntityId>? _paletteKey;
    private int _paletteRevision = -1;
    private TilePalette _palette = new([]);
    public TilePalette Palette
    {
        get
        {
            if (!ReferenceEquals(_paletteKey, Model.Tilesets) || _paletteRevision != Session.Revision)
            {
                _paletteKey = Model.Tilesets;
                _paletteRevision = Session.Revision;
                _palette = new TilePalette(Tilesets());
            }
            return _palette;
        }
    }

    /// <summary>The tilesets currently on the map, and every tileset available to add.</summary>
    public IReadOnlyList<EntityId> MapTilesets => Model.Tilesets;
    public IEnumerable<Tileset> AvailableTilesets => Session.All<Tileset>();
    public bool HasTileset => Model.Tilesets.Count > 0;

    /// <summary>Adds a tileset to the map's palette (undoable). Appends, so existing painted tile
    /// indices keep their meaning. No-op if already present or unknown.</summary>
    public void AddTileset(EntityId tilesetId)
    {
        if (Model.Tilesets.Contains(tilesetId) || Session.Find<Tileset>(tilesetId) is null)
            return;
        Edit(Model with { Tilesets = Model.Tilesets.Append(tilesetId).ToList() });
        if (SelectedTile < 0 && Palette.Count > 0)
            SelectedTile = 0;
    }

    /// <summary>Every palette tile with its global index and sprite, for the palette strip.</summary>
    public IReadOnlyList<PaletteTile> PaletteTiles
    {
        get
        {
            var result = new List<PaletteTile>();
            int global = 0;
            foreach (EntityId id in Model.Tilesets)
                if (Session.Find<Tileset>(id) is { } tileset)
                    for (int local = 0; local < tileset.Tiles.Count; local++)
                        result.Add(new PaletteTile(global++, id, local, tileset.Tiles[local].Sprite));
            return result;
        }
    }

    public IReadOnlyList<int> Layer(MapLayerId id)
    {
        IReadOnlyList<int> layer = id switch
        {
        MapLayerId.Ground => Model.Layers.Ground,
        MapLayerId.DecoBelow => Model.Layers.DecoBelow,
        _ => Model.Layers.DecoAbove,
        };
        return layer.Count == Width * Height
            ? layer
            : Enumerable.Repeat(MapLayerOps.Empty, Width * Height).ToArray();
    }

    /// <summary>The live layer shown while a gesture is in progress; committed model otherwise.</summary>
    public IReadOnlyList<int> LayerForRender(MapLayerId id) =>
        _strokeLayer is not null && id == ActiveLayer ? _strokeLayer : Layer(id);

    public int TileAt(MapLayerId id, int x, int y) =>
        InBounds(x, y) ? Layer(id)[y * Width + x] : MapLayerOps.Empty;

    // --- Stroke lifecycle: one pointer gesture = one undo step ---

    public void BeginStroke()
    {
        _strokeOriginal = Layer(ActiveLayer).ToArray();
        _strokeLayer = (int[])_strokeOriginal.Clone();
        _strokeChanged = false;
        _lastStrokeCell = null;
    }

    /// <summary>Applies the active tool at a cell into the in-progress stroke buffer. Bucket and
    /// rect-fill are whole-gesture tools, so they apply once and the stroke is a formality.</summary>
    public void StrokePaint(int x, int y, int rectAnchorX = -1, int rectAnchorY = -1)
    {
        if (_strokeLayer is null || !InBounds(x, y))
            return;

        int[] before = _strokeLayer;
        if (Tool is MapTool.Paint or MapTool.Erase && _lastStrokeCell is { } last)
        {
            int tile = Tool == MapTool.Erase ? MapLayerOps.Empty : SelectedTile;
            foreach ((int px, int py) in Line(last.X, last.Y, x, y))
                _strokeLayer = MapLayerOps.Paint(_strokeLayer, Width, Height, px, py, tile);
        }
        else
        {
            _strokeLayer = Tool switch
            {
                MapTool.Paint => MapLayerOps.Paint(before, Width, Height, x, y, SelectedTile),
                MapTool.Erase => MapLayerOps.Paint(before, Width, Height, x, y, MapLayerOps.Empty),
                MapTool.Bucket => MapLayerOps.BucketFill(before, Width, Height, x, y, SelectedTile),
                MapTool.RectFill when rectAnchorX >= 0 && _strokeOriginal is not null =>
                    MapLayerOps.RectFill(_strokeOriginal, Width, Height, rectAnchorX, rectAnchorY, x, y, SelectedTile),
                MapTool.Eyedropper => Eyedrop(before, x, y),
                _ => before,
            };
        }
        _lastStrokeCell = (x, y);
        _strokeChanged = _strokeOriginal is not null
            && !_strokeLayer.AsSpan().SequenceEqual(_strokeOriginal);
    }

    public void EndStroke()
    {
        if (_strokeLayer is { } layer && _strokeChanged)
            Edit(Model with { Layers = WithLayer(ActiveLayer, layer) });
        _strokeLayer = null;
        _strokeOriginal = null;
        _strokeChanged = false;
        _lastStrokeCell = null;
    }

    public void CancelStroke()
    {
        _strokeLayer = null;
        _strokeOriginal = null;
        _strokeChanged = false;
        _lastStrokeCell = null;
    }

    /// <summary>A single-cell edit outside a stroke (e.g. a click). Convenience over Begin/End.</summary>
    public void PaintCell(int x, int y)
    {
        BeginStroke();
        StrokePaint(x, y);
        EndStroke();
    }

    private int[] Eyedrop(int[] layer, int x, int y)
    {
        SelectedTile = layer[y * Width + x];
        return layer; // reading a cell changes no pixels
    }

    private static IEnumerable<(int X, int Y)> Line(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            yield return (x0, y0);
            if (x0 == x1 && y0 == y1)
                yield break;
            int twice = 2 * error;
            if (twice >= dy) { error += dy; x0 += sx; }
            if (twice <= dx) { error += dx; y0 += sy; }
        }
    }

    // --- Overlays: sparse per-cell lists keyed by index ---

    /// <summary>Sets or clears (null) a cell's collision override. One undo step.</summary>
    public void SetCollision(int x, int y, CollisionValue? value)
    {
        if (!InBounds(x, y))
            return;
        int index = y * Width + x;
        var list = Model.CollisionOverrides.Where(o => o.Index != index).ToList();
        if (value is { } v)
            list.Add(new CollisionOverride(index, v));
        if (!list.SequenceEqual(Model.CollisionOverrides))
            Edit(Model with { CollisionOverrides = list.OrderBy(o => o.Index).ToList() });
    }

    public CollisionValue? CollisionAt(int x, int y)
    {
        if (!InBounds(x, y))
            return null;
        int index = y * Width + x;
        foreach (CollisionOverride o in Model.CollisionOverrides)
            if (o.Index == index)
                return o.Value;
        return null;
    }

    /// <summary>Sets or clears (null) a cell's encounter zone. One undo step.</summary>
    public void SetEncounter(int x, int y, EntityId? table)
    {
        if (!InBounds(x, y))
            return;
        int index = y * Width + x;
        var list = Model.EncounterZones.Where(z => z.Index != index).ToList();
        if (table is { } t)
            list.Add(new EncounterZoneCell(index, t));
        if (!list.SequenceEqual(Model.EncounterZones))
            Edit(Model with { EncounterZones = list.OrderBy(z => z.Index).ToList() });
    }

    public EntityId? EncounterAt(int x, int y)
    {
        if (!InBounds(x, y))
            return null;
        int index = y * Width + x;
        foreach (EncounterZoneCell z in Model.EncounterZones)
            if (z.Index == index)
                return z.Table;
        return null;
    }

    // --- Resize: top-left anchor, dropping out-of-bounds overrides/zones/entities ---

    public void Resize(int newW, int newH)
    {
        if (newW <= 0 || newH <= 0 || (newW == Width && newH == Height))
            return;

        MapLayers layers = new()
        {
            Ground = MapLayerOps.Resize([.. Model.Layers.Ground], Width, Height, newW, newH),
            DecoBelow = MapLayerOps.Resize([.. Model.Layers.DecoBelow], Width, Height, newW, newH),
            DecoAbove = MapLayerOps.Resize([.. Model.Layers.DecoAbove], Width, Height, newW, newH),
        };

        // Sparse overrides and zones are keyed by old-width index; remap surviving cells, drop the rest.
        var overrides = Remap(Model.CollisionOverrides, o => o.Index, (o, i) => o with { Index = i }, newW, newH);
        var zones = Remap(Model.EncounterZones, z => z.Index, (z, i) => z with { Index = i }, newW, newH);
        var entities = Model.Entities.Where(e => e.Pos.X < newW && e.Pos.Y < newH).ToList();

        Edit(Model with
        {
            Width = newW,
            Height = newH,
            Layers = layers,
            CollisionOverrides = overrides,
            EncounterZones = zones,
            Entities = entities,
        });
    }

    private List<T> Remap<T>(IReadOnlyList<T> items, Func<T, int> index, Func<T, int, T> withIndex, int newW, int newH)
    {
        var result = new List<T>();
        foreach (T item in items)
        {
            int old = index(item);
            int x = old % Width, y = old / Width;
            if (x < newW && y < newH)
                result.Add(withIndex(item, y * newW + x));
        }
        return result.OrderBy(index).ToList();
    }

    // --- Entity placement (MAP_EDITOR_SPEC 17C) ---

    public IReadOnlyList<MapEntity> Entities => Model.Entities;

    /// <summary>The entity at a cell, if any (topmost by list order), for select/move/delete.</summary>
    public MapEntity? EntityAt(int x, int y) =>
        Model.Entities.LastOrDefault(e => e.Pos.X == x && e.Pos.Y == y);

    /// <summary>Places an entity, assigning a stable, never-reused key <c>{kind}_{n}</c>. Returns
    /// the key. Out-of-bounds placement is refused (empty key).</summary>
    public string Place(MapEntity entity)
    {
        if (!InBounds(entity.Pos.X, entity.Pos.Y))
            return "";
        string key = FreshKey(KindOf(entity));
        Edit(Model with { Entities = Model.Entities.Append(entity with { Key = key }).ToList() });
        return key;
    }

    public void MoveEntity(string key, GridPos pos)
    {
        if (!InBounds(pos.X, pos.Y))
            return;
        var list = Model.Entities.Select(e => e.Key == key ? e with { Pos = pos } : e).ToList();
        if (!list.SequenceEqual(Model.Entities))
            Edit(Model with { Entities = list });
    }

    /// <summary>Replaces an entity's whole record (its per-instance config), keyed by <see cref="MapEntity.Key"/>.</summary>
    public void ConfigureEntity(MapEntity edited)
    {
        var list = Model.Entities.Select(e => e.Key == edited.Key ? edited : e).ToList();
        if (!list.SequenceEqual(Model.Entities))
            Edit(Model with { Entities = list });
    }

    public void DeleteEntity(string key)
    {
        var list = Model.Entities.Where(e => e.Key != key).ToList();
        if (list.Count != Model.Entities.Count)
            Edit(Model with { Entities = list });
    }

    /// <summary>A key unique within the map and never colliding with an existing one, matching the
    /// v8 migration's derivation so hand-authored and editor-authored keys share one scheme.</summary>
    private string FreshKey(string kind)
    {
        var taken = Model.Entities.Select(e => e.Key).ToHashSet(StringComparer.Ordinal);
        for (int n = 0; ; n++)
        {
            string candidate = $"{kind}_{n}";
            if (taken.Add(candidate))
                return candidate;
        }
    }

    private static string KindOf(MapEntity e) => e switch
    {
        PlayerStartEntity => "player_start",
        NpcEntity => "npc",
        WarpEntity => "warp",
        PickupEntity => "pickup",
        SignEntity => "sign",
        TriggerEntity => "trigger",
        ObjectEntity => "object",
        _ => "entity",
    };

    // --- Play-from-map (MAP_EDITOR_SPEC 17C; the process launch is 17F) ---

    /// <summary>Assembles the Runtime argument line to spawn on this map at a cell, or null when the
    /// target is out of bounds or solid (collision override or a solid tile under it). Only the
    /// argument string is 17C; 17F runs the process.</summary>
    public string? PlayFromArgs(string projectFolder, int x, int y)
    {
        if (!InBounds(x, y) || IsSolid(x, y))
            return null;
        return $"--project \"{projectFolder}\" --map {Model.Id} --at {x},{y}";
    }

    private bool IsSolid(int x, int y)
    {
        if (CollisionAt(x, y) is { } forced)
            return forced == CollisionValue.Solid;
        // Otherwise the ground tile's own solid flag, resolved through the palette.
        int index = TileAt(MapLayerId.Ground, x, y);
        return Palette.At(index)?.Solid == true;
    }

    private MapLayers WithLayer(MapLayerId id, int[] layer) => id switch
    {
        MapLayerId.Ground => Model.Layers with { Ground = layer },
        MapLayerId.DecoBelow => Model.Layers with { DecoBelow = layer },
        _ => Model.Layers with { DecoAbove = layer },
    };

    private IReadOnlyList<Tileset> Tilesets() =>
        Model.Tilesets.Select(id => Session.Find<Tileset>(id)).OfType<Tileset>().ToList();

    private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
}
