using System.Text.Json;
using Cgm.Core.Model;
using Cgm.Runtime.Engine;

namespace Cgm.Runtime.Tests;

/// <summary>The bag's battle-usable consumables: which items the ITEMS panel offers and the heal it
/// reads from their battle effects. Key items, TMs, and capture balls are excluded.</summary>
public sealed class BattleItemsTests : IDisposable
{
    private const int Tile = 16;
    private static readonly EntityId MapId = EntityId.Parse("map:field");
    private static readonly EntityId Starter = EntityId.Parse("species:pebbling");
    private static readonly EntityId TypeId = EntityId.Parse("type:plain");

    private static readonly EntityId Tonic = EntityId.Parse("item:tonic");     // heals in battle
    private static readonly EntityId Ball = EntityId.Parse("item:orb");        // capture, excluded
    private static readonly EntityId Key = EntityId.Parse("item:map_key");     // key item, excluded
    private static readonly EntityId Field = EntityId.Parse("item:repel");     // field-only, excluded

    private readonly RecordingRenderer _renderer = new();
    private readonly QuadBatch _batch = new();
    private readonly UiPainter _ui;

    public BattleItemsTests()
    {
        TextureHandle atlas = _renderer.CreateTexture(FontAtlas.Width, FontAtlas.Height, FontAtlas.Rgba());
        _ui = new UiPainter(_batch, atlas, new BitmapFont());
    }

    public void Dispose() => _renderer.Dispose();

    private static Effect Heal(int amount) => new()
    {
        Op = "heal",
        Params = new Dictionary<string, JsonElement> { ["amount"] = JsonSerializer.SerializeToElement(amount) },
    };

    private static GameDb Db() => new(
        new ProjectSettings
        {
            Name = "T", TileSize = Tile, StartMap = MapId, StartPos = new GridPos(1, 1),
            StarterParty = [Starter], Boxes = new BoxConfig { Count = 1, Capacity = 4 },
        },
        [
            new Map { Id = MapId, Name = "F", Width = 4, Height = 4, Layers = new MapLayers { Ground = [.. Enumerable.Repeat(0, 16)] } },
            new Species { Id = Starter, Name = "Pebbling", Types = [TypeId], GrowthRate = "medium-fast", BaseStats = new Stats(40, 40, 40, 40, 40, 40) },
            new TypeDef { Id = TypeId, Name = "Plain" },
            new Item { Id = Tonic, Name = "Tonic", Pocket = "medicine", Consumable = true, UsableInBattle = true, Effects = [Heal(30)] },
            new Item { Id = Ball, Name = "Orb", Pocket = "balls", Consumable = true, UsableInBattle = true },
            new Item { Id = Key, Name = "Map Key", Pocket = "key", KeyItem = true, UsableInBattle = true },
            new Item { Id = Field, Name = "Repel", Pocket = "items", Consumable = true, UsableInField = true, UsableInBattle = false },
        ]);

    private WorldSession Session()
    {
        var session = new WorldSession(Db(), _ui, Tile, 256, 192, new Rng(1));
        session.InitialiseNewGame();
        session.AddItem(Tonic, 2);
        session.AddItem(Ball, 5);
        session.AddItem(Key);
        session.AddItem(Field, 3);
        return session;
    }

    [Fact]
    public void OffersOnlyTheBattleUsableConsumable()
    {
        IReadOnlyList<BattleItemChoice> items = Session().BattleItems();
        Assert.Equal([Tonic], items.Select(i => i.Item));   // ball, key, and field-only are all excluded
    }

    [Fact]
    public void ReadsTheHealAmountFromTheItemsBattleEffects() =>
        Assert.Equal(30, Session().BattleItems().Single().HealAmount);

    [Fact]
    public void CarriesThePocketForGrouping() =>
        Assert.Equal("medicine", Session().BattleItems().Single().Pocket);

    [Fact]
    public void AnEmptyBagOffersNoBattleItems()
    {
        var session = new WorldSession(Db(), _ui, Tile, 256, 192, new Rng(1));
        session.InitialiseNewGame();
        Assert.Empty(session.BattleItems());
    }

    /// <summary>A battle-usable item without a heal effect is still offered, at zero heal — Core may
    /// apply other effects, and the demo just needs it to appear and consume the turn.</summary>
    [Fact]
    public void ANonHealingBattleItemReadsZeroHeal()
    {
        var db = new GameDb(Db().Settings,
        [
            .. Db().Entities.Where(e => e.Id != Tonic),
            new Item { Id = Tonic, Name = "Dire Hit", Pocket = "battle", Consumable = true, UsableInBattle = true },
        ]);
        var session = new WorldSession(db, _ui, Tile, 256, 192, new Rng(1));
        session.InitialiseNewGame();
        session.AddItem(Tonic);
        Assert.Equal(0, session.BattleItems().Single().HealAmount);
    }
}
