using Cgm.Core.Model;
using Cgm.Creator.Maps;

namespace Cgm.Creator.ViewModels;

public enum MapLayerId { Ground, DecoBelow, DecoAbove }
public enum MapTool { Paint, RectFill, Bucket, Eyedropper, Erase }

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
    private bool _strokeChanged;

    public MapDocument(ProjectSession session, Map model) : base(session, model)
    {
        Palette = new TilePalette(Tilesets());
        SelectedTile = Palette.Count > 0 ? 0 : MapLayerOps.Empty;
    }

    public int Width => Model.Width;
    public int Height => Model.Height;

    // --- Active editing state (presentation only, never serialized) ---
    public MapLayerId ActiveLayer { get; set; } = MapLayerId.Ground;
    public MapTool Tool { get; set; } = MapTool.Paint;
    public int SelectedTile { get; set; }
    public CollisionValue SelectedCollision { get; set; } = CollisionValue.Solid;
    public EntityId? SelectedEncounterTable { get; set; }

    /// <summary>The map's tilesets flattened into the global index space layers store into.</summary>
    public TilePalette Palette { get; private set; }

    public IReadOnlyList<int> Layer(MapLayerId id) => id switch
    {
        MapLayerId.Ground => Model.Layers.Ground,
        MapLayerId.DecoBelow => Model.Layers.DecoBelow,
        _ => Model.Layers.DecoAbove,
    };

    public int TileAt(MapLayerId id, int x, int y) =>
        InBounds(x, y) ? Layer(id)[y * Width + x] : MapLayerOps.Empty;

    // --- Stroke lifecycle: one pointer gesture = one undo step ---

    public void BeginStroke()
    {
        _strokeLayer = Layer(ActiveLayer).ToArray();
        _strokeChanged = false;
    }

    /// <summary>Applies the active tool at a cell into the in-progress stroke buffer. Bucket and
    /// rect-fill are whole-gesture tools, so they apply once and the stroke is a formality.</summary>
    public void StrokePaint(int x, int y, int rectAnchorX = -1, int rectAnchorY = -1)
    {
        if (_strokeLayer is null || !InBounds(x, y))
            return;

        int[] before = _strokeLayer;
        _strokeLayer = Tool switch
        {
            MapTool.Paint => MapLayerOps.Paint(before, Width, Height, x, y, SelectedTile),
            MapTool.Erase => MapLayerOps.Paint(before, Width, Height, x, y, MapLayerOps.Empty),
            MapTool.Bucket => MapLayerOps.BucketFill(before, Width, Height, x, y, SelectedTile),
            MapTool.RectFill when rectAnchorX >= 0 =>
                MapLayerOps.RectFill(before, Width, Height, rectAnchorX, rectAnchorY, x, y, SelectedTile),
            MapTool.Eyedropper => Eyedrop(before, x, y),
            _ => before,
        };
        if (!_strokeLayer.AsSpan().SequenceEqual(before))
            _strokeChanged = true;
    }

    public void EndStroke()
    {
        if (_strokeLayer is { } layer && _strokeChanged)
            Edit(Model with { Layers = WithLayer(ActiveLayer, layer) });
        _strokeLayer = null;
        _strokeChanged = false;
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
