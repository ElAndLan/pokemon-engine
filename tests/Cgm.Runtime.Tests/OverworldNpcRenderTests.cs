using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>A placed NPC draws its character sprite (feet-anchored, like the player) when art is
/// present, and falls back to the marker box when it is not.</summary>
public sealed class OverworldNpcRenderTests : IDisposable
{
    private const int Tile = 16, W = 128, H = 96;
    private const string Asset = "assets/chars.png";
    private static readonly EntityId MapId = EntityId.Parse("map:field");
    private static readonly EntityId TilesetId = EntityId.Parse("tileset:t");

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;
    private readonly TextureHandle _fontAtlas;

    public OverworldNpcRenderTests()
    {
        _fontAtlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, _fontAtlas, new BitmapFont());
    }

    public void Dispose() => _renderer.Dispose();

    private static readonly Rect NpcRect = new(0, 0, 21, 28);

    private static SpriteSheet Sheet() => new()
    {
        Id = EntityId.Parse("sheet:chars"), Asset = Asset, ImageW = 64, ImageH = 32, Mode = SliceMode.Rects,
        Cells = [new SheetCell { Rect = NpcRect, SpriteId = EntityId.Parse("sprite:npc"), Class = SpriteClass.Character }],
    };

    private static Tileset Tileset() => new() { Id = TilesetId, Tiles = [new Tile()] };

    private static Map Map(EntityId? npcSprite) => new()
    {
        Id = MapId, Width = 8, Height = 6, Tilesets = [TilesetId],
        Layers = new MapLayers { Ground = [.. Enumerable.Repeat(0, 48)] },
        Entities =
        [
            new PlayerStartEntity { Key = "p", Pos = new GridPos(0, 5) },
            new NpcEntity { Key = "n", Pos = new GridPos(4, 3), Sprite = npcSprite },
        ],
    };

    private SpriteAtlas Atlas()
    {
        var atlas = new SpriteAtlas(_renderer,
            new PackAssetSource(new Dictionary<string, byte[]> { [Asset] = TestPng.Solid(64, 32, 50, 50, 60) }),
            [Sheet()]);
        atlas.PreloadAll();
        return atlas;
    }

    private IReadOnlyList<Quad> Render(EntityId? npcSprite, SpriteAtlas? atlas)
    {
        var scene = new OverworldScene(_ui, Map(npcSprite), [Tileset()], new GridPos(0, 5), Facing.Down,
            Tile, W, H, sprites: atlas);
        _batch.Begin();
        scene.Render();
        var (quads, calls, _) = _batch.End();
        _renderer.BeginFrame(new Viewport(1, 0, 0, W, H), W, H, new Rgba(0, 0, 0, 255));
        _renderer.Draw(quads, calls);
        _renderer.EndFrame();
        return _renderer.Drawn;
    }

    private bool DrewNpcSprite(IReadOnlyList<Quad> drawn) =>
        drawn.Any(q => q.Texture != _fontAtlas && q.Source == new RectI(0, 0, 21, 28));

    [Fact]
    public void AnNpcWithASpriteDrawsIt()
    {
        using SpriteAtlas atlas = Atlas();
        Assert.True(DrewNpcSprite(Render(EntityId.Parse("sprite:npc"), atlas)));
    }

    [Fact]
    public void TheNpcSpriteIsFeetAnchored()
    {
        using SpriteAtlas atlas = Atlas();
        Quad npc = Render(EntityId.Parse("sprite:npc"), atlas)
            .Single(q => q.Texture != _fontAtlas && q.Source == new RectI(0, 0, 21, 28));

        // NPC at tile (4,3); camera pinned (map == viewport). Feet-anchored, horizontally centred.
        RectI tile = new(4 * Tile, 3 * Tile, Tile, Tile);
        Assert.Equal(tile.X + (Tile - 21) / 2, npc.Dest.X);
        Assert.Equal(tile.Y + Tile - 28, npc.Dest.Y);
        Assert.Equal(1, npc.Layer);
    }

    [Fact]
    public void AnNpcWithoutASpriteFallsBackToTheMarker()
    {
        using SpriteAtlas atlas = Atlas();
        IReadOnlyList<Quad> drawn = Render(npcSprite: null, atlas);
        Assert.False(DrewNpcSprite(drawn));
        // A font-atlas marker on layer 1 stands in (smaller than a full tile — it is inset).
        Assert.Contains(drawn, q => q.Texture == _fontAtlas && q.Layer == 1 && q.Dest.Width < Tile);
    }

    [Fact]
    public void WithoutAnAtlasTheNpcFallsBackToTheMarker()
    {
        IReadOnlyList<Quad> drawn = Render(EntityId.Parse("sprite:npc"), atlas: null);
        Assert.False(DrewNpcSprite(drawn));
        Assert.Contains(drawn, q => q.Texture == _fontAtlas && q.Layer == 1 && q.Dest.Width < Tile);
    }
}
