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
}
