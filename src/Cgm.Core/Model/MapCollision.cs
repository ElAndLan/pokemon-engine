namespace Cgm.Core.Model;

/// <summary>
/// Derives a per-cell <see cref="CollisionValue"/> grid for a map from its tile flags
/// (MAP_EDITOR_SPEC). A cell blocks if any layer's tile there is solid or water; otherwise the
/// first ledge tile gives a one-way ledge; else it's open. <c>collisionOverrides</c> win.
/// Pure — reused by the map editor's overlay and the runtime's movement.
/// </summary>
public static class MapCollision
{
    /// <summary><paramref name="objects"/> supplies the definitions for the map's placed
    /// <see cref="ObjectEntity"/> entities (§4.11a); when given, each object's <see
    /// cref="MapObject.Collision"/> footprint marks its tiles solid. Omitted (the default), placed
    /// objects do not block — the map editor's overlay and pre-object callers keep their behaviour.</summary>
    public static CollisionValue[] Derive(Map map, IReadOnlyList<Tileset> tilesets,
        IReadOnlyDictionary<EntityId, MapObject>? objects = null)
    {
        int n = map.Width * map.Height;
        var result = new CollisionValue[n];
        var palette = new TilePalette(tilesets);   // the shared global tile index

        for (int cell = 0; cell < n; cell++)
            result[cell] = CellCollision(map, palette, cell, n);

        if (objects is not null)
            ApplyObjectFootprints(map, objects, result);

        foreach (CollisionOverride o in map.CollisionOverrides)
            if (o.Index >= 0 && o.Index < n)
                result[o.Index] = o.Value; // an explicit override is the author's final say

        return result;
    }

    /// <summary>Marks each placed object's collision-footprint tiles solid. The footprint's top-left
    /// is <c>pos - anchor</c>; a cell is solid where the object's row-major collision array is true.</summary>
    private static void ApplyObjectFootprints(Map map, IReadOnlyDictionary<EntityId, MapObject> objects,
        CollisionValue[] result)
    {
        foreach (ObjectEntity placed in map.Entities.OfType<ObjectEntity>())
        {
            if (!objects.TryGetValue(placed.Object, out MapObject? def))
                continue;

            int leftTile = placed.Pos.X - def.Anchor.X;
            int topTile = placed.Pos.Y - def.Anchor.Y;
            for (int cy = 0; cy < def.FootprintH; cy++)
                for (int cx = 0; cx < def.FootprintW; cx++)
                {
                    int footIndex = cy * def.FootprintW + cx;
                    if (footIndex >= def.Collision.Count || !def.Collision[footIndex])
                        continue;

                    int tx = leftTile + cx, ty = topTile + cy;
                    if (tx >= 0 && tx < map.Width && ty >= 0 && ty < map.Height)
                        result[ty * map.Width + tx] = CollisionValue.Solid;
                }
        }
    }

    private static CollisionValue CellCollision(Map map, TilePalette palette, int cell, int n)
    {
        CollisionValue? ledge = null;
        foreach (int index in TilesAt(map, cell, n))
        {
            if (palette.At(index) is not { } tile)
                continue;
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
