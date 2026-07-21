namespace Cgm.Core.Model;

/// <summary>
/// A map's tilesets flattened into one global tile index space, in the order the map lists them
/// (DATA_SCHEMA.md §4.11). Map layers store indices into this space, so collision derivation and
/// rendering must agree on it exactly — one wrong offset draws a different tile than the one the
/// player walks through. That makes it a rule, and it lives here rather than in either consumer.
/// </summary>
public sealed class TilePalette
{
    private readonly List<Tile> _tiles;

    public TilePalette(IReadOnlyList<Tileset> tilesets)
    {
        ArgumentNullException.ThrowIfNull(tilesets);
        _tiles = [.. tilesets.SelectMany(t => t.Tiles)];
    }

    public int Count => _tiles.Count;

    /// <summary>The tile at a global index, or null when the index is empty (-1) or out of range.
    /// An unknown index is not an error: a map may legitimately outlive a tileset edit, and an
    /// absent tile draws nothing rather than throwing mid-frame.</summary>
    public Tile? At(int index) => index >= 0 && index < _tiles.Count ? _tiles[index] : null;
}
