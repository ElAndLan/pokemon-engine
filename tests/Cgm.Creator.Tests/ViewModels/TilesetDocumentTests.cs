using Cgm.Core.Model;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Tests.ViewModels;

/// <summary>MAP_EDITOR_SPEC 17C tileset editor: per-tile flag edits are undoable, adds append,
/// and only the trailing tile may be removed (interior delete would renumber maps).</summary>
public sealed class TilesetDocumentTests : IDisposable
{
    private readonly string _project = TestRepo.CopySampleToTemp("fixture-min");
    private readonly ProjectSession _session;

    public TilesetDocumentTests() => _session = ProjectSession.Open(_project);

    public void Dispose()
    {
        _session.Close();
        Directory.Delete(_project, recursive: true);
    }

    private TilesetDocument Doc(int tiles = 3)
    {
        var ts = new Tileset
        {
            Id = EntityId.Parse("tileset:doc"),
            Name = "doc",
            Tiles = Enumerable.Range(0, tiles).Select(_ => new Tile()).ToList(),
        };
        _session.Add(ts);
        return new TilesetDocument(_session, ts);
    }

    [Fact]
    public void SetFlags_AreUndoable()
    {
        TilesetDocument doc = Doc();

        doc.SetSolid(1, true);
        doc.SetGrass(1, true);
        doc.SetLedge(1, LedgeDir.Down);
        doc.SetTerrainTag(1, "cliff");

        Tile tile = doc.Tiles[1];
        Assert.True(tile.Solid);
        Assert.True(tile.Grass);
        Assert.Equal(LedgeDir.Down, tile.Ledge);
        Assert.Equal("cliff", tile.TerrainTag);

        doc.Undo.Undo(); // terrainTag
        Assert.Equal("", doc.Tiles[1].TerrainTag);
        Assert.Equal(LedgeDir.Down, doc.Tiles[1].Ledge); // earlier edits intact
    }

    [Fact]
    public void NoOpSet_DoesNotDirty()
    {
        TilesetDocument doc = Doc();
        doc.SetSolid(0, false); // already false
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void AddTile_Appends()
    {
        TilesetDocument doc = Doc(2);
        doc.AddTile();
        Assert.Equal(3, doc.Tiles.Count);

        doc.Undo.Undo();
        Assert.Equal(2, doc.Tiles.Count);
    }

    [Fact]
    public void RemoveTile_OnlyTrailing_InteriorRefused()
    {
        TilesetDocument doc = Doc(3);

        Assert.False(doc.RemoveTile(0)); // interior: would renumber maps
        Assert.Equal(3, doc.Tiles.Count);

        Assert.True(doc.RemoveTile(2)); // trailing: safe
        Assert.Equal(2, doc.Tiles.Count);
    }

    [Fact]
    public void ClearTile_BlanksInPlace_PreservesIndex()
    {
        TilesetDocument doc = Doc(3);
        doc.SetSolid(1, true);
        doc.SetSprite(1, EntityId.Parse("sprite:x"));

        doc.ClearTile(1);
        Assert.Equal(new Tile(), doc.Tiles[1]);
        Assert.Equal(3, doc.Tiles.Count); // index preserved, no renumber
    }

    [Fact]
    public void SetSprite_UsesReferencePickerSource()
    {
        _session.Add(new SpriteSheet
        {
            Id = EntityId.Parse("sheet:tiles"),
            Name = "tiles",
            Cells = [new SheetCell { SpriteId = EntityId.Parse("sprite:tiles_0"), Rect = new Rect(0, 0, 16, 16) }],
        });
        TilesetDocument doc = Doc();
        Assert.Contains(EntityId.Parse("sprite:tiles_0"), doc.AvailableSprites);

        doc.SetSprite(0, EntityId.Parse("sprite:tiles_0"));
        Assert.Equal(EntityId.Parse("sprite:tiles_0"), doc.Tiles[0].Sprite);
    }
}
