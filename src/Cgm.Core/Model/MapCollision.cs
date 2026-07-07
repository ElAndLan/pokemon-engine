namespace Cgm.Core.Model;

/// <summary>
/// Derives a per-cell <see cref="CollisionValue"/> grid for a map from its tile flags
/// (MAP_EDITOR_SPEC). A cell blocks if any layer's tile there is solid or water; otherwise the
/// first ledge tile gives a one-way ledge; else it's open. <c>collisionOverrides</c> win.
/// Pure — reused by the map editor's overlay and the runtime's movement.
/// </summary>
public static class MapCollision
{
    public static CollisionValue[] Derive(Map map, IReadOnlyList<Tileset> tilesets)
    {
        int n = map.Width * map.Height;
        var result = new CollisionValue[n];
        List<Tile> flat = tilesets.SelectMany(t => t.Tiles).ToList(); // global tile index

        for (int cell = 0; cell < n; cell++)
            result[cell] = CellCollision(map, flat, cell, n);

        foreach (CollisionOverride o in map.CollisionOverrides)
            if (o.Index >= 0 && o.Index < n)
                result[o.Index] = o.Value; // override wins

        return result;
    }

    private static CollisionValue CellCollision(Map map, List<Tile> flat, int cell, int n)
    {
        CollisionValue? ledge = null;
        foreach (int index in TilesAt(map, cell, n))
        {
            if (index < 0 || index >= flat.Count)
                continue;
            Tile tile = flat[index];
            if (tile.Solid || tile.Water) // water blocks until surf (Phase 16)
                return CollisionValue.Solid;
            if (tile.Ledge != LedgeDir.None)
                ledge ??= Ledge(tile.Ledge);
        }
        return ledge ?? CollisionValue.Open;
    }

    private static IEnumerable<int> TilesAt(Map map, int cell, int n)
    {
        MapLayers l = map.Layers;
        if (l.Ground.Count == n) yield return l.Ground[cell];
        if (l.DecoBelow.Count == n) yield return l.DecoBelow[cell];
        if (l.DecoAbove.Count == n) yield return l.DecoAbove[cell];
    }

    private static CollisionValue Ledge(LedgeDir dir) => dir switch
    {
        LedgeDir.Up => CollisionValue.LedgeUp,
        LedgeDir.Down => CollisionValue.LedgeDown,
        LedgeDir.Left => CollisionValue.LedgeLeft,
        LedgeDir.Right => CollisionValue.LedgeRight,
        _ => CollisionValue.Open,
    };
}
