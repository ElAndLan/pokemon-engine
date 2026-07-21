using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>16H tile rendering: a map draws its authored tiles through the sprite atlas, and stays
/// legible when art is absent. The tile a player collides with and the tile drawn come from one
/// index space, so those cannot drift apart.</summary>
public sealed class OverworldTileRenderTests : IDisposable
{
    private const int Tile = 16;
    private const int W = 64;
    private const int H = 48;
    private const string Asset = "assets/tiles.png";

    private static readonly EntityId MapId = EntityId.Parse("map:field");
    private static readonly EntityId SheetId = EntityId.Parse("sheet:tiles");
    private static readonly EntityId TilesetId = EntityId.Parse("tileset:outdoor");

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;
    private readonly TextureHandle _fontAtlas;

    public OverworldTileRenderTests()
    {
        _fontAtlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, _fontAtlas, new BitmapFont());
    }

    public void Dispose() => _renderer.Dispose();

    private static EntityId Sprite(string slug) => EntityId.Parse("sprite:" + slug);

    /// <summary>A 32x16 sheet: cell 0 "grass", cell 1 "tree".</summary>
    private static SpriteSheet Sheet() => new()
    {
        Id = SheetId, Name = "Tiles", Asset = Asset,
        ImageW = 32, ImageH = 16, Mode = SliceMode.Grid, CellW = Tile, CellH = Tile,
        Cells =
        [
            new SheetCell { Index = 0, SpriteId = Sprite("grass") },
            new SheetCell { Index = 1, SpriteId = Sprite("tree") },
        ],
    };

    /// <summary>Global tile 0 = walkable grass with a sprite, 1 = solid tree, 2 = sprite-less.</summary>
    private static Tileset Tileset() => new()
    {
        Id = TilesetId, Name = "Outdoor",
        Tiles =
        [
            new Tile { Sprite = Sprite("grass"), Grass = true },
            new Tile { Sprite = Sprite("tree"), Solid = true },
            new Tile(),
        ],
    };

    /// <summary>A 2x1 map: grass then tree, with an optional decoAbove tree canopy on cell 0.</summary>
    private static Map Map(IReadOnlyList<int>? decoAbove = null) => new()
    {
        Id = MapId, Name = "Field", Width = 2, Height = 1, Tilesets = [TilesetId],
        Layers = new MapLayers { Ground = [0, 1], DecoAbove = decoAbove ?? [] },
    };

    private static IAssetSource Assets(byte[]? png = null) =>
        new PackAssetSource(new Dictionary<string, byte[]>
        {
            [Asset] = png ?? TestPng.Solid(32, 16, 60, 140, 70),
        });

    private SpriteAtlas Atlas(IAssetSource? assets = null) =>
        new(_renderer, assets ?? Assets(), [Sheet()]);

    /// <summary>Renders one frame and returns the quads that reached the renderer.</summary>
    private IReadOnlyList<Quad> RenderOnce(SpriteAtlas? sprites, Map? map = null)
    {
        var scene = new OverworldScene(_ui, map ?? Map(), [Tileset()], new GridPos(0, 0),
            Facing.Down, Tile, W, H, sprites: sprites);

        _batch.Begin();
        scene.Render();
        var (quads, calls, _) = _batch.End();

        _renderer.BeginFrame(new Viewport(1, 0, 0, W, H), W, H, new Rgba(0, 0, 0, 255));
        _renderer.Draw(quads, calls);
        _renderer.EndFrame();
        return _renderer.Drawn;
    }

    /// <summary>Quads drawn from the tile sheet rather than the font atlas.</summary>
    private IEnumerable<Quad> TileQuads(IReadOnlyList<Quad> drawn) =>
        drawn.Where(q => q.Texture != _fontAtlas);

    /// <summary>Collision-colour panels standing in for absent art: font-atlas quads, one tile in
    /// size, on the ground layer. This deliberately excludes the full-screen background and the
    /// player marker, which are also painted from the font atlas.</summary>
    private IEnumerable<Quad> FallbackTiles(IReadOnlyList<Quad> drawn) =>
        drawn.Where(q => q.Texture == _fontAtlas && q.Layer == 0 && q.Dest.Width == Tile);

    // --- With art ---------------------------------------------------------------------

    [Fact]
    public void AuthoredTilesDrawFromTheSheet()
    {
        using SpriteAtlas atlas = Atlas();
        var tiles = TileQuads(RenderOnce(atlas)).ToList();

        // Cell 0 samples sheet cell 0, cell 1 samples sheet cell 1, each at its map position.
        Assert.Contains(tiles, q => q.Source == new RectI(0, 0, 16, 16) && q.Dest.X == 0);
        Assert.Contains(tiles, q => q.Source == new RectI(16, 0, 16, 16) && q.Dest.X == 16);
    }

    [Fact]
    public void EveryTileQuadIsOneTileBig()
    {
        using SpriteAtlas atlas = Atlas();
        Assert.All(TileQuads(RenderOnce(atlas)),
            q => Assert.Equal((Tile, Tile), (q.Dest.Width, q.Dest.Height)));
    }

    /// <summary>decoAbove must sort over the player so a canopy occludes; ground must sort under.</summary>
    [Fact]
    public void DecoAboveSortsOverThePlayerAndGroundUnder()
    {
        using SpriteAtlas atlas = Atlas();
        var tiles = TileQuads(RenderOnce(atlas, Map(decoAbove: [1, -1]))).ToList();

        Assert.Contains(tiles, q => q.Layer == 0);   // ground
        Assert.Contains(tiles, q => q.Layer == 2);   // canopy, above the player's layer 1
        Assert.DoesNotContain(tiles, q => q.Layer == 1);
    }

    /// <summary>An empty cell (-1) in a layer draws nothing at all.</summary>
    [Fact]
    public void AnEmptyLayerCellDrawsNothing()
    {
        using SpriteAtlas atlas = Atlas();
        Assert.DoesNotContain(TileQuads(RenderOnce(atlas, Map(decoAbove: [-1, -1]))),
            q => q.Layer == 2);
    }

    /// <summary>The collision fallback must not paint over art that did load.</summary>
    [Fact]
    public void ADrawnTileGetsNoColourFallbackBehindIt()
    {
        using SpriteAtlas atlas = Atlas();
        IReadOnlyList<Quad> drawn = RenderOnce(atlas);

        Assert.Empty(FallbackTiles(drawn));
    }

    // --- Without art -------------------------------------------------------------------

    [Fact]
    public void WithoutAnAtlasTilesFallBackToCollisionColours()
    {
        IReadOnlyList<Quad> drawn = RenderOnce(sprites: null);

        Assert.Empty(TileQuads(drawn));
        Assert.Equal(2, FallbackTiles(drawn).Count());   // one per visible cell
    }

    /// <summary>A sheet that fails to load must degrade exactly like having no atlas — the demo stays
    /// playable and legible rather than rendering an invisible map.</summary>
    [Fact]
    public void AFailedSheetLoadFallsBackToCollisionColours()
    {
        using var atlas = new SpriteAtlas(_renderer,
            new PackAssetSource(new Dictionary<string, byte[]>()), [Sheet()]);
        IReadOnlyList<Quad> drawn = RenderOnce(atlas);

        Assert.Empty(TileQuads(drawn));
        Assert.Equal(2, FallbackTiles(drawn).Count());
    }

    /// <summary>A tile carrying no sprite is authored, not broken; it falls back per cell.</summary>
    [Fact]
    public void ATileWithoutASpriteFallsBackForThatCellOnly()
    {
        using SpriteAtlas atlas = Atlas();
        var map = Map() with { Layers = new MapLayers { Ground = [0, 2] } };
        IReadOnlyList<Quad> drawn = RenderOnce(atlas, map);

        Assert.Single(TileQuads(drawn));        // only the grass tile has art
        Assert.Single(FallbackTiles(drawn));    // the sprite-less tile falls back
    }

    // --- Agreement with collision --------------------------------------------------------

    /// <summary>Drawing and collision read one index space, so the solid tile drawn at a cell is the
    /// tile that blocks there.</summary>
    [Fact]
    public void TheDrawnTileIsTheTileThatBlocks()
    {
        CollisionValue[] collision = MapCollision.Derive(Map(), [Tileset()]);
        Assert.Equal(CollisionValue.Open, collision[0]);
        Assert.Equal(CollisionValue.Solid, collision[1]);

        using SpriteAtlas atlas = Atlas();
        var tiles = TileQuads(RenderOnce(atlas)).ToList();

        // The solid cell draws the "tree" sprite (sheet cell 1) at x=16, where collision blocks.
        Assert.Contains(tiles, q => q.Dest.X == 16 && q.Source.X == 16);
    }
}
