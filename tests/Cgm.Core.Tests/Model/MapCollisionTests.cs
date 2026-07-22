using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

public sealed class MapCollisionTests
{
    // Tileset with a known global index layout:
    // 0 = open path, 1 = solid wall, 2 = ledge-down, 3 = water.
    private static Tileset Ts() => new()
    {
        Id = EntityId.Parse("tileset:t"),
        Tiles =
        [
            new Tile { TerrainTag = "path" },
            new Tile { Solid = true },
            new Tile { Ledge = LedgeDir.Down },
            new Tile { Water = true },
        ],
    };

    private static Map MapWith(int[] ground, int w = 2, int h = 2, params CollisionOverride[] overrides) => new()
    {
        Id = EntityId.Parse("map:m"),
        Width = w,
        Height = h,
        Tilesets = [EntityId.Parse("tileset:t")],
        Layers = new MapLayers { Ground = ground },
        CollisionOverrides = overrides,
    };

    [Fact]
    public void Derive_MapsTileFlagsToCollision()
    {
        Map map = MapWith([0, 1, 2, 3]); // open, solid, ledge-down, water
        CollisionValue[] c = MapCollision.Derive(map, [Ts()]);
        Assert.Equal(CollisionValue.Open, c[0]);
        Assert.Equal(CollisionValue.Solid, c[1]);
        Assert.Equal(CollisionValue.LedgeDown, c[2]);
        Assert.Equal(CollisionValue.Solid, c[3]); // water blocks
    }

    [Fact]
    public void Derive_EmptyCell_IsOpen()
    {
        Map map = MapWith([-1, -1, -1, -1]);
        Assert.All(MapCollision.Derive(map, [Ts()]), v => Assert.Equal(CollisionValue.Open, v));
    }

    [Fact]
    public void Derive_OutOfRangeIndex_TreatedEmpty()
    {
        Map map = MapWith([99, -1, -1, -1]);
        Assert.Equal(CollisionValue.Open, MapCollision.Derive(map, [Ts()])[0]);
    }

    [Fact]
    public void Derive_OverrideWins()
    {
        Map map = MapWith([1, 0, 0, 0], overrides:
        [
            new CollisionOverride(0, CollisionValue.Open),   // solid cell forced open
            new CollisionOverride(1, CollisionValue.Solid),  // open cell forced solid
        ]);
        CollisionValue[] c = MapCollision.Derive(map, [Ts()]);
        Assert.Equal(CollisionValue.Open, c[0]);
        Assert.Equal(CollisionValue.Solid, c[1]);
    }

    [Fact]
    public void Derive_AnySolidLayerBlocks()
    {
        // Ground open, deco-above solid on the same cell → Solid.
        var map = new Map
        {
            Id = EntityId.Parse("map:m"),
            Width = 1,
            Height = 1,
            Tilesets = [EntityId.Parse("tileset:t")],
            Layers = new MapLayers { Ground = [0], DecoAbove = [1] },
        };
        Assert.Equal(CollisionValue.Solid, MapCollision.Derive(map, [Ts()])[0]);
    }

    [Fact]
    public void Derive_GlobalIndexSpansMultipleTilesets()
    {
        // Second tileset starts at global index 4; index 5 = its solid tile.
        var second = new Tileset
        {
            Id = EntityId.Parse("tileset:u"),
            Tiles = [new Tile { TerrainTag = "sand" }, new Tile { Solid = true }],
        };
        var map = new Map
        {
            Id = EntityId.Parse("map:m"),
            Width = 2,
            Height = 1,
            Tilesets = [EntityId.Parse("tileset:t"), EntityId.Parse("tileset:u")],
            Layers = new MapLayers { Ground = [4, 5] },
        };
        CollisionValue[] c = MapCollision.Derive(map, [Ts(), second]);
        Assert.Equal(CollisionValue.Open, c[0]);  // global 4 = second tileset tile 0
        Assert.Equal(CollisionValue.Solid, c[1]); // global 5 = second tileset tile 1
    }

    // --- Placed-object footprints -----------------------------------------------------

    private static (Map, Dictionary<EntityId, MapObject>) MapWithObject(
        int fw, int fh, GridPos anchor, GridPos pos, IReadOnlyList<bool> collision)
    {
        EntityId objId = EntityId.Parse("object:hut");
        var map = new Map
        {
            Id = EntityId.Parse("map:m"), Width = 4, Height = 4,
            Tilesets = [EntityId.Parse("tileset:t")],
            Layers = new MapLayers { Ground = [.. Enumerable.Repeat(0, 16)] },   // all open
            Entities = [new ObjectEntity { Key = "hut", Pos = pos, Object = objId }],
        };
        var defs = new Dictionary<EntityId, MapObject>
        {
            [objId] = new MapObject
            {
                Id = objId, FootprintW = fw, FootprintH = fh, Anchor = anchor, Collision = collision,
            },
        };
        return (map, defs);
    }

    [Fact]
    public void Derive_PlacedObject_MarksItsSolidFootprintTiles()
    {
        // A 2x2 hut, anchor at its top-left, placed at (1,1); collision solid everywhere.
        var (map, defs) = MapWithObject(2, 2, new GridPos(0, 0), new GridPos(1, 1), [true, true, true, true]);
        CollisionValue[] c = MapCollision.Derive(map, [Ts()], defs);

        foreach (int cell in new[] { 1 + 1 * 4, 2 + 1 * 4, 1 + 2 * 4, 2 + 2 * 4 })
            Assert.Equal(CollisionValue.Solid, c[cell]);
        Assert.Equal(CollisionValue.Open, c[0]);   // outside the footprint
    }

    [Fact]
    public void Derive_PlacedObject_RespectsTheAnchorOffset()
    {
        // Anchor at (1,1) means the placement pos is the object's own (1,1) cell, so the footprint's
        // top-left lands at pos - anchor = (0,0).
        var (map, defs) = MapWithObject(2, 2, new GridPos(1, 1), new GridPos(1, 1), [true, true, true, true]);
        CollisionValue[] c = MapCollision.Derive(map, [Ts()], defs);
        Assert.Equal(CollisionValue.Solid, c[0]);          // tile (0,0)
        Assert.Equal(CollisionValue.Solid, c[1 + 1 * 4]);  // tile (1,1)
    }

    [Fact]
    public void Derive_PlacedObject_OnlyMarksCellsFlaggedSolid()
    {
        // A door gap: bottom-left cell is open.
        var (map, defs) = MapWithObject(2, 2, new GridPos(0, 0), new GridPos(0, 0), [true, true, false, true]);
        CollisionValue[] c = MapCollision.Derive(map, [Ts()], defs);
        Assert.Equal(CollisionValue.Open, c[0 + 1 * 4]);   // the open cell
        Assert.Equal(CollisionValue.Solid, c[0]);
    }

    /// <summary>Objects only block when their definitions are supplied; the default overload (map
    /// editor overlay, pre-object callers) is unaffected.</summary>
    [Fact]
    public void Derive_WithoutObjectDefs_ObjectsDoNotBlock()
    {
        var (map, _) = MapWithObject(2, 2, new GridPos(0, 0), new GridPos(1, 1), [true, true, true, true]);
        Assert.All(MapCollision.Derive(map, [Ts()]), v => Assert.Equal(CollisionValue.Open, v));
    }

    /// <summary>A footprint tile off the map edge is ignored rather than throwing.</summary>
    [Fact]
    public void Derive_PlacedObject_ClipsAtTheMapEdge()
    {
        var (map, defs) = MapWithObject(2, 2, new GridPos(0, 0), new GridPos(3, 3), [true, true, true, true]);
        CollisionValue[] c = MapCollision.Derive(map, [Ts()], defs);
        Assert.Equal(CollisionValue.Solid, c[3 + 3 * 4]);  // the one in-bounds cell
    }
}
