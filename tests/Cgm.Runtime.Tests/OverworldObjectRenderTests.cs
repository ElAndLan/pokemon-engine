using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>Placed multi-tile objects render at their footprint, bottom-anchored, on the layer their
/// definition asks for (below the player, or above like a canopy).</summary>
public sealed class OverworldObjectRenderTests : IDisposable
{
    private const int Tile = 16;
    private const int W = 128;
    private const int H = 96;
    private const string Asset = "assets/obj.png";

    private static readonly EntityId MapId = EntityId.Parse("map:field");
    private static readonly EntityId TilesetId = EntityId.Parse("tileset:t");
    private static readonly EntityId ClinicId = EntityId.Parse("object:clinic");

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;
    private readonly TextureHandle _fontAtlas;

    public OverworldObjectRenderTests()
    {
        _fontAtlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, _fontAtlas, new BitmapFont());
    }

    public void Dispose() => _renderer.Dispose();

    private static EntityId Sprite(string s) => EntityId.Parse("sprite:" + s);

    // A 3x2-footprint object whose sprite (48x40) overhangs the footprint (48x32) by 8px.
    private static readonly Rect ObjRect = new(0, 0, 48, 40);

    private static SpriteSheet Sheet() => new()
    {
        Id = EntityId.Parse("sheet:obj"), Asset = Asset, ImageW = 64, ImageH = 64, Mode = SliceMode.Rects,
        Cells = [new SheetCell { Rect = ObjRect, SpriteId = Sprite("clinic") }],
    };

    private static MapObject Clinic(ObjectLayer layer) => new()
    {
        Id = ClinicId, FootprintW = 3, FootprintH = 2, Anchor = new GridPos(1, 1),
        Layer = layer, Sprite = Sprite("clinic"),
    };

    private static Tileset Tileset() => new() { Id = TilesetId, Tiles = [new Tile()] };

    // Placement anchor tile (1,1 within footprint) sits at map (4,4).
    private static Map Map() => new()
    {
        Id = MapId, Width = 8, Height = 6, Tilesets = [TilesetId],
        Layers = new MapLayers { Ground = [.. Enumerable.Repeat(0, 48)] },
        Entities = [new ObjectEntity { Key = "clinic", Pos = new GridPos(4, 4), Object = ClinicId }],
    };

    private SpriteAtlas Atlas()
    {
        var atlas = new SpriteAtlas(_renderer,
            new PackAssetSource(new Dictionary<string, byte[]> { [Asset] = TestPng.Solid(64, 64, 40, 60, 40) }),
            [Sheet()]);
        atlas.PreloadAll();
        return atlas;
    }

    private IReadOnlyList<Quad> Render(ObjectLayer layer, SpriteAtlas? atlas)
    {
        var scene = new OverworldScene(_ui, Map(), [Tileset()], new GridPos(0, 0), Facing.Down,
            Tile, W, H, sprites: atlas,
            objects: new Dictionary<EntityId, MapObject> { [ClinicId] = Clinic(layer) });

        _batch.Begin();
        scene.Render();
        var (quads, calls, _) = _batch.End();
        _renderer.BeginFrame(new Viewport(1, 0, 0, W, H), W, H, new Rgba(0, 0, 0, 255));
        _renderer.Draw(quads, calls);
        _renderer.EndFrame();
        return _renderer.Drawn;
    }

    private Quad ObjectQuad(IReadOnlyList<Quad> drawn) =>
        Assert.Single(drawn, q => q.Texture != _fontAtlas && q.Source == new RectI(0, 0, 48, 40));

    // Map is exactly one viewport (128x96), so the camera is pinned at 0 and TileRect(x,y) = (16x, 16y).
    // Footprint top-left tile = pos(4,4) - anchor(1,1) = (3,3) -> origin (48,48). Footprint 3x2 = 48x32;
    // sprite 48x40 centres (offset 0) and bottom-aligns: destY = 48 + 32 - 40 = 40.
    [Fact]
    public void AnObjectDrawsAtItsFootprintBottomAnchoredAndCentred()
    {
        using SpriteAtlas atlas = Atlas();
        Quad obj = ObjectQuad(Render(ObjectLayer.Below, atlas));

        Assert.Equal(new RectI(48, 40, 48, 40), obj.Dest);
    }

    [Fact]
    public void AnAboveObjectSortsOverThePlayer()
    {
        using SpriteAtlas atlas = Atlas();
        Assert.Equal(2, ObjectQuad(Render(ObjectLayer.Above, atlas)).Layer);
    }

    [Fact]
    public void ABelowObjectSortsUnderThePlayer()
    {
        using SpriteAtlas atlas = Atlas();
        Assert.Equal(0, ObjectQuad(Render(ObjectLayer.Below, atlas)).Layer);
    }

    [Fact]
    public void WithoutAnAtlasNoObjectIsDrawn()
    {
        Assert.DoesNotContain(Render(ObjectLayer.Below, atlas: null),
            q => q.Texture != _fontAtlas && q.Source == new RectI(0, 0, 48, 40));
    }
}
