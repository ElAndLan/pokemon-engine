using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

/// <summary>The global tile index shared by collision derivation and rendering. If these two ever
/// disagreed, the tile a player walks through would not be the tile drawn.</summary>
public sealed class TilePaletteTests
{
    private static Tileset Set(string slug, params Tile[] tiles) =>
        new() { Id = EntityId.Parse("tileset:" + slug), Name = slug, Tiles = tiles };

    private static Tile Tile(string terrain) => new() { TerrainTag = terrain };

    [Fact]
    public void TilesetsFlattenInListOrder()
    {
        var palette = new TilePalette(
        [
            Set("a", Tile("a0"), Tile("a1")),
            Set("b", Tile("b0")),
        ]);

        Assert.Equal(3, palette.Count);
        Assert.Equal("a0", palette.At(0)!.TerrainTag);
        Assert.Equal("a1", palette.At(1)!.TerrainTag);
        Assert.Equal("b0", palette.At(2)!.TerrainTag);
    }

    /// <summary>Reordering a map's tilesets renumbers every tile after the first set. Asserted so the
    /// index space is understood to be positional, not identity-based.</summary>
    [Fact]
    public void ReorderingTilesetsChangesTheIndexSpace()
    {
        Tileset a = Set("a", Tile("a0"), Tile("a1"));
        Tileset b = Set("b", Tile("b0"));

        Assert.Equal("b0", new TilePalette([b, a]).At(0)!.TerrainTag);
        Assert.Equal("a0", new TilePalette([a, b]).At(0)!.TerrainTag);
    }

    [Theory]
    [InlineData(-1)]      // the schema's "empty cell"
    [InlineData(3)]       // one past the end
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void AnIndexOutsideThePaletteIsAbsentRatherThanAnError(int index) =>
        Assert.Null(new TilePalette([Set("a", Tile("a0"), Tile("a1")), Set("b", Tile("b0"))]).At(index));

    [Fact]
    public void AnEmptyPaletteHasNoTiles()
    {
        var palette = new TilePalette([]);
        Assert.Equal(0, palette.Count);
        Assert.Null(palette.At(0));
    }

    [Fact]
    public void ATilesetWithNoTilesContributesNothing() =>
        Assert.Equal(1, new TilePalette([Set("empty"), Set("a", Tile("a0"))]).Count);

    [Fact]
    public void NullTilesetsAreRejected() =>
        Assert.Throws<ArgumentNullException>(() => new TilePalette(null!));

    /// <summary>Collision derivation must read the same index space this exposes.</summary>
    [Fact]
    public void CollisionAgreesWithThePaletteOnTheIndexSpace()
    {
        Tileset[] tilesets = [Set("a", Tile("open")), Set("b", new Tile { Solid = true })];
        var map = new Map
        {
            Id = EntityId.Parse("map:m"), Width = 2, Height = 1,
            Layers = new MapLayers { Ground = [0, 1] },
        };

        CollisionValue[] collision = MapCollision.Derive(map, tilesets);
        var palette = new TilePalette(tilesets);

        Assert.False(palette.At(0)!.Solid);
        Assert.True(palette.At(1)!.Solid);
        Assert.Equal(CollisionValue.Open, collision[0]);
        Assert.Equal(CollisionValue.Solid, collision[1]);
    }
}
