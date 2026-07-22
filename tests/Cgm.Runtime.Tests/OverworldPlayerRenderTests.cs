using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>The player renders as an animated, feet-anchored sprite when art is present, and falls
/// back to the flat marker when it is not — so an unarted map stays playable.</summary>
public sealed class OverworldPlayerRenderTests : IDisposable
{
    private const int Tile = 16;
    private const int W = 64;
    private const int H = 48;
    private const string Asset = "assets/chars.png";

    private static readonly EntityId MapId = EntityId.Parse("map:field");
    private static readonly EntityId TilesetId = EntityId.Parse("tileset:t");

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;
    private readonly TextureHandle _fontAtlas;

    public OverworldPlayerRenderTests()
    {
        _fontAtlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, _fontAtlas, new BitmapFont());
    }

    public void Dispose() => _renderer.Dispose();

    private static EntityId Sprite(string s) => EntityId.Parse("sprite:" + s);

    // A character sheet: player_down_n at 21x28, the taller-than-a-tile shape.
    private static SpriteSheet Sheet() => new()
    {
        Id = EntityId.Parse("sheet:chars"), Asset = Asset, ImageW = 64, ImageH = 32, Mode = SliceMode.Rects,
        Cells = [new SheetCell { Rect = new Rect(0, 0, 21, 28), SpriteId = Sprite("player_down_n") }],
    };

    private static Animation DownClip() => new()
    {
        Id = EntityId.Parse("anim:walk_down"), Loop = true,
        Frames = [new AnimFrame(Sprite("player_down_n"), 100)],
    };

    private static Tileset Tileset() => new()
    {
        Id = TilesetId, Tiles = [new Tile()],   // one blank, sprite-less tile
    };

    // Wider than the viewport so the camera clamps to 0 at the left-edge start tile, keeping screen
    // positions deterministic for the anchor assertion.
    private static Map Map() => new()
    {
        Id = MapId, Width = 8, Height = 4, Tilesets = [TilesetId],
        Layers = new MapLayers { Ground = [.. Enumerable.Repeat(0, 32)] },
    };

    private SpriteAtlas Atlas()
    {
        var atlas = new SpriteAtlas(_renderer,
            new PackAssetSource(new Dictionary<string, byte[]> { [Asset] = TestPng.Solid(64, 32, 10, 20, 30) }),
            [Sheet()]);
        atlas.PreloadAll();
        return atlas;
    }

    private OverworldScene Scene(SpriteAtlas? atlas, WalkAnimator? walk) =>
        new(_ui, Map(), [Tileset()], new GridPos(0, 0), Facing.Down, Tile, W, H,
            sprites: atlas, playerWalk: walk);

    private IReadOnlyList<Quad> RenderOnce(OverworldScene scene)
    {
        _batch.Begin();
        scene.Render();
        var (quads, calls, _) = _batch.End();
        _renderer.BeginFrame(new Viewport(1, 0, 0, W, H), W, H, new Rgba(0, 0, 0, 255));
        _renderer.Draw(quads, calls);
        _renderer.EndFrame();
        return _renderer.Drawn;
    }

    /// <summary>The player's sprite quad, identified by the character texture and the 21x28 cell.</summary>
    private Quad? PlayerSprite(IReadOnlyList<Quad> drawn) =>
        drawn.Where(q => q.Texture != _fontAtlas && q.Source == new RectI(0, 0, 21, 28))
             .Cast<Quad?>().FirstOrDefault();

    [Fact]
    public void WithArtThePlayerDrawsAsASprite()
    {
        using SpriteAtlas atlas = Atlas();
        OverworldScene scene = Scene(atlas, new WalkAnimator([DownClip(), null, null, null]));
        scene.Update(Idle);

        Quad? player = PlayerSprite(RenderOnce(scene));
        Assert.NotNull(player);
        Assert.Equal(1, player!.Value.Layer);   // under decoAbove, over ground
    }

    /// <summary>Feet-anchored: bottom-aligned to the tile and horizontally centred, top overhanging.</summary>
    [Fact]
    public void ThePlayerSpriteIsFeetAnchoredToItsTile()
    {
        using SpriteAtlas atlas = Atlas();
        OverworldScene scene = Scene(atlas, new WalkAnimator([DownClip(), null, null, null]));
        scene.Update(Idle);

        // Player stands at the left-edge tile (0,0), where the camera clamps to 0.
        RectI tile = new(0, 0, Tile, Tile);
        Quad player = PlayerSprite(RenderOnce(scene))!.Value;
        Assert.Equal(tile.X + (Tile - 21) / 2, player.Dest.X);   // centred
        Assert.Equal(tile.Y + Tile - 28, player.Dest.Y);         // bottom-aligned (overhangs upward)
        Assert.Equal((21, 28), (player.Dest.Width, player.Dest.Height));
    }

    [Fact]
    public void WithoutAnimatorThePlayerFallsBackToTheFlatMarker()
    {
        using SpriteAtlas atlas = Atlas();
        OverworldScene scene = Scene(atlas, walk: null);
        scene.Update(Idle);

        IReadOnlyList<Quad> drawn = RenderOnce(scene);
        Assert.Null(PlayerSprite(drawn));
        // The flat marker is a font-atlas, tile-sized quad on layer 1.
        Assert.Contains(drawn, q => q.Texture == _fontAtlas && q.Layer == 1 && q.Dest.Width == Tile);
    }

    [Fact]
    public void WithoutAtlasThePlayerFallsBackToTheFlatMarker()
    {
        OverworldScene scene = Scene(atlas: null, walk: new WalkAnimator([DownClip(), null, null, null]));
        scene.Update(Idle);
        Assert.Contains(RenderOnce(scene), q => q.Texture == _fontAtlas && q.Layer == 1 && q.Dest.Width == Tile);
    }

    private static readonly TickInput Idle =
        new(new HashSet<GameAction>(), new HashSet<GameAction>(), new HashSet<GameAction>());
}
