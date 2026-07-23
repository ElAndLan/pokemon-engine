using Cgm.Core.Model;

namespace Cgm.Creator.ViewModels;

/// <summary>One tile with its index, for the tileset grid. A short summary of its flags aids
/// scanning for the wrong-flag bugs the editor exists to catch.</summary>
public sealed record TileRow(int Index, Tile Tile)
{
    public string Flags => string.Concat(
        Tile.Solid ? "S" : "", Tile.Grass ? "G" : "", Tile.Water ? "W" : "",
        Tile.Counter ? "C" : "", Tile.Ledge != Cgm.Core.Model.LedgeDir.None ? "L" : "");
}

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

    /// <summary>Tiles with their global index, for the grid view.</summary>
    public IReadOnlyList<TileRow> TileRows =>
        Model.Tiles.Select((t, i) => new TileRow(i, t)).ToList();

    public IReadOnlyList<LedgeDir> LedgeDirs { get; } =
        [LedgeDir.None, LedgeDir.Up, LedgeDir.Down, LedgeDir.Left, LedgeDir.Right];

    /// <summary>Sprites available to assign to a tile (the picker source).</summary>
    public IReadOnlyList<EntityId> AvailableSprites =>
        Session.All<SpriteSheet>().SelectMany(s => s.Cells).Select(c => c.SpriteId)
            .OrderBy(id => id.Slug, StringComparer.Ordinal).ToList();

    /// <summary>Appends a blank tile — never inserts, so existing indices are never renumbered.</summary>
    public bool AddTile()
    {
        if (CountChangeBlockReason(removing: false) is not null)
            return false;
        Edit(Model with { Tiles = Model.Tiles.Append(new Tile()).ToList() });
        return true;
    }

    /// <summary>Removes the trailing tile only. Removing an interior tile would renumber the maps
    /// that reference this tileset, so it is refused (use <see cref="ClearTile"/> to blank it).</summary>
    public bool RemoveTile(int index)
    {
        if (Model.Tiles.Count == 0)
            return false;
        if (index != Model.Tiles.Count - 1)
            return false; // interior delete renumbers maps — refused
        if (CountChangeBlockReason(removing: true) is not null)
            return false;
        Edit(Model with { Tiles = Model.Tiles.Take(Model.Tiles.Count - 1).ToList() });
        return true;
    }

    public string? CountChangeBlockReason(bool removing)
    {
        foreach (Map map in Session.All<Map>().Where(m => m.Tilesets.Contains(Model.Id)))
        {
            int setPosition = map.Tilesets.ToList().IndexOf(Model.Id);
            if (setPosition != map.Tilesets.Count - 1)
                return $"Map '{map.Id}' has another tileset after this one; changing the tile count would renumber its painted cells.";

            if (removing && Model.Tiles.Count > 0)
            {
                int offset = map.Tilesets.Take(setPosition)
                    .Select(id => Session.Find<Tileset>(id)?.Tiles.Count ?? 0).Sum();
                int removedIndex = offset + Model.Tiles.Count - 1;
                if (map.Layers.Ground.Contains(removedIndex)
                    || map.Layers.DecoBelow.Contains(removedIndex)
                    || map.Layers.DecoAbove.Contains(removedIndex))
                    return $"Map '{map.Id}' paints with trailing tile {Model.Tiles.Count - 1}; clear those cells before removing it.";
            }
        }
        return null;
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
