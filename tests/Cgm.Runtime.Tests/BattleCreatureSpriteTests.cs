using Cgm.Core.Battle;
using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>The battle scene draws the combatants' creature sprites — the enemy's front, the
/// player's back — through the atlas, falling back to the coloured platform markers without art.</summary>
public sealed class BattleCreatureSpriteTests : IDisposable
{
    private const int Tile = 16, W = 256, H = 192;
    private const string Asset = "assets/creatures.png";

    private static readonly EntityId MapId = EntityId.Parse("map:field");
    private static readonly EntityId TypeId = EntityId.Parse("type:plain");
    private static readonly EntityId MoveId = EntityId.Parse("move:tackle");
    private static readonly EntityId Starter = EntityId.Parse("species:pebbling");
    private static readonly EntityId WildId = EntityId.Parse("species:sprig");

    private static readonly Rect FrontRect = new(0, 0, 32, 32);   // sprig_front
    private static readonly Rect BackRect = new(32, 0, 32, 32);   // pebbling_back

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;
    private readonly TextureHandle _fontAtlas;

    public BattleCreatureSpriteTests()
    {
        _fontAtlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, _fontAtlas, new BitmapFont());
    }

    public void Dispose() => _renderer.Dispose();

    private static EntityId Sprite(string s) => EntityId.Parse("sprite:" + s);

    private static Species Species(EntityId id, EntityId? front, EntityId? back) => new()
    {
        Id = id, Name = id.Slug, Types = [TypeId],
        BaseStats = new Stats(60, 50, 50, 50, 50, 50),
        GrowthRate = "medium-fast", BaseExp = 64, CatchRate = 255,
        Learnset = [new LearnsetEntry { Level = 1, Move = MoveId }],
        Sprites = new SpeciesSprites { Front = front, Back = back },
    };

    private static SpriteSheet Sheet() => new()
    {
        Id = EntityId.Parse("sheet:creatures"), Asset = Asset, ImageW = 64, ImageH = 32, Mode = SliceMode.Rects,
        Cells =
        [
            new SheetCell { Rect = FrontRect, SpriteId = Sprite("sprig_front"), Class = SpriteClass.CreatureFront },
            new SheetCell { Rect = BackRect, SpriteId = Sprite("pebbling_back"), Class = SpriteClass.CreatureBack },
        ],
    };

    private static GameDb Db() => new(
        new ProjectSettings
        {
            Name = "T", TileSize = Tile, StartMap = MapId, StartPos = new GridPos(1, 1),
            StarterParty = [Starter], Boxes = new BoxConfig { Count = 1, Capacity = 4 },
        },
        [
            new Map { Id = MapId, Name = "F", Width = 4, Height = 4, Layers = new MapLayers { Ground = [.. Enumerable.Repeat(0, 16)] } },
            Species(Starter, front: Sprite("pebbling_front"), back: Sprite("pebbling_back")),
            Species(WildId, front: Sprite("sprig_front"), back: Sprite("sprig_back")),
            Sheet(),
            new TypeDef { Id = TypeId, Name = "Plain" },
            new Move { Id = MoveId, Name = "Tackle", Type = TypeId, Pp = 35, Power = 40, DamageClass = DamageClass.Physical, Accuracy = 100 },
        ]);

    private WorldSession Session(GameDb db)
    {
        var session = new WorldSession(db, _ui, Tile, W, H, new Rng(1));
        session.InitialiseNewGame();
        return session;
    }

    private SpriteAtlas Atlas()
    {
        var atlas = new SpriteAtlas(_renderer,
            new PackAssetSource(new Dictionary<string, byte[]> { [Asset] = TestPng.Solid(64, 32, 30, 60, 40) }),
            [Sheet()]);
        atlas.PreloadAll();
        return atlas;
    }

    private BattleHostScene Scene(GameDb db, SpriteAtlas? atlas)
    {
        BattleStart start = BattleLauncher.Wild(db, Session(db), WildId, 3, new Rng(2));
        var presenter = new BattleScene(start.Battle!, b => new UseMove(0), null, id => id.Slug);
        return new BattleHostScene(_ui, presenter, W, H,
            sprites: atlas, speciesSprites: id => db.Find<Species>(id)?.Sprites);
    }

    private IReadOnlyList<Quad> RenderOnce(BattleHostScene scene)
    {
        scene.Enter();
        _batch.Begin();
        scene.Render();
        var (quads, calls, _) = _batch.End();
        _renderer.BeginFrame(new Viewport(1, 0, 0, W, H), W, H, new Rgba(0, 0, 0, 255));
        _renderer.Draw(quads, calls);
        _renderer.EndFrame();
        return _renderer.Drawn;
    }

    private bool Drew(IReadOnlyList<Quad> drawn, Rect cell) =>
        drawn.Any(q => q.Texture != _fontAtlas && q.Source == new RectI(cell.X, cell.Y, cell.W, cell.H));

    [Fact]
    public void TheEnemyDrawsItsFrontAndThePlayerItsBack()
    {
        GameDb db = Db();
        using SpriteAtlas atlas = Atlas();
        IReadOnlyList<Quad> drawn = RenderOnce(Scene(db, atlas));

        Assert.True(Drew(drawn, FrontRect), "enemy front sprite should be drawn");
        Assert.True(Drew(drawn, BackRect), "player back sprite should be drawn");
    }

    [Fact]
    public void CombatantSpritesSitAboveTheirPlatforms()
    {
        GameDb db = Db();
        using SpriteAtlas atlas = Atlas();
        IReadOnlyList<Quad> drawn = RenderOnce(Scene(db, atlas));

        // Both creature sprites draw on layer 2, over the layer-1 platform markers.
        Assert.All(drawn.Where(q => q.Texture != _fontAtlas), q => Assert.Equal(2, q.Layer));
    }

    [Fact]
    public void WithoutAnAtlasTheCombatantsFallBackToPlatformMarkers()
    {
        GameDb db = Db();
        IReadOnlyList<Quad> drawn = RenderOnce(Scene(db, atlas: null));

        Assert.False(Drew(drawn, FrontRect));
        Assert.False(Drew(drawn, BackRect));
        // The coloured platform markers (font-atlas, layer 1) still stand in.
        Assert.Contains(drawn, q => q.Texture == _fontAtlas && q.Layer == 1);
    }
}
