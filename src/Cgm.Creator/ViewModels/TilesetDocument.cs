using Cgm.Core.Model;

namespace Cgm.Creator.ViewModels;

/// <summary>
/// Tileset editor (MAP_EDITOR_SPEC 17C): edits each <see cref="Tile"/>'s sprite and gameplay flags.
/// A tile's list position is its local index, and maps store global indices across their tilesets,
/// so adds only append and only the trailing tile may be removed — an interior delete would silently
/// renumber every map that references this tileset. Every edit is one undoable whole-record snapshot.
/// </summary>
public sealed class TilesetDocument : EntityEditorDocument<Tileset>
{
    public TilesetDocument(ProjectSession session, Tileset model) : base(session, model) { }

    public string Name
    {
        get => Model.Name;
        set { if (value != Model.Name) Edit(Model with { Name = value }); }
    }

    public IReadOnlyList<Tile> Tiles => Model.Tiles;

    public IReadOnlyList<LedgeDir> LedgeDirs { get; } =
        [LedgeDir.None, LedgeDir.Up, LedgeDir.Down, LedgeDir.Left, LedgeDir.Right];

    /// <summary>Sprites available to assign to a tile (the picker source).</summary>
    public IReadOnlyList<EntityId> AvailableSprites =>
        Session.All<SpriteSheet>().SelectMany(s => s.Cells).Select(c => c.SpriteId)
            .OrderBy(id => id.Slug, StringComparer.Ordinal).ToList();

    /// <summary>Appends a blank tile — never inserts, so existing indices are never renumbered.</summary>
    public void AddTile() => Edit(Model with { Tiles = Model.Tiles.Append(new Tile()).ToList() });

    /// <summary>Removes the trailing tile only. Removing an interior tile would renumber the maps
    /// that reference this tileset, so it is refused (use <see cref="ClearTile"/> to blank it).</summary>
    public bool RemoveTile(int index)
    {
        if (Model.Tiles.Count == 0)
            return false;
        if (index != Model.Tiles.Count - 1)
            return false; // interior delete renumbers maps — refused
        Edit(Model with { Tiles = Model.Tiles.Take(Model.Tiles.Count - 1).ToList() });
        return true;
    }

    /// <summary>Resets a tile to blank in place — the index is preserved, so no map is renumbered.</summary>
    public void ClearTile(int index) => Replace(index, new Tile());

    public void SetSprite(int index, EntityId? sprite) => Update(index, t => t with { Sprite = sprite });
    public void SetSolid(int index, bool value) => Update(index, t => t with { Solid = value });
    public void SetGrass(int index, bool value) => Update(index, t => t with { Grass = value });
    public void SetWater(int index, bool value) => Update(index, t => t with { Water = value });
    public void SetCounter(int index, bool value) => Update(index, t => t with { Counter = value });
    public void SetLedge(int index, LedgeDir dir) => Update(index, t => t with { Ledge = dir });
    public void SetTerrainTag(int index, string tag) => Update(index, t => t with { TerrainTag = tag ?? "" });

    private void Update(int index, Func<Tile, Tile> edit)
    {
        if (index < 0 || index >= Model.Tiles.Count)
            return;
        Tile next = edit(Model.Tiles[index]);
        if (next != Model.Tiles[index])
            Replace(index, next);
    }

    private void Replace(int index, Tile tile)
    {
        if (index < 0 || index >= Model.Tiles.Count || tile == Model.Tiles[index])
            return;
        var tiles = Model.Tiles.ToList();
        tiles[index] = tile;
        Edit(Model with { Tiles = tiles });
    }
}
