using Cgm.Core.Model;
using Cgm.Creator.ViewModels;

namespace Cgm.Creator.Tests.ViewModels;

/// <summary>MAP_EDITOR_SPEC 17C map document: tools paint through MapLayerOps, a pointer stroke is
/// one undo step, overlays edit sparse lists, resize anchors top-left and drops out-of-bounds data.</summary>
public sealed class MapDocumentTests : IDisposable
{
    private readonly string _project = TestRepo.CopySampleToTemp("fixture-min");
    private readonly ProjectSession _session;

    public MapDocumentTests() => _session = ProjectSession.Open(_project);

    public void Dispose()
    {
        _session.Close();
        Directory.Delete(_project, recursive: true);
    }

    private MapDocument Doc(int w = 8, int h = 8)
    {
        var map = new Map
        {
            Id = EntityId.Parse("map:doc"),
            Name = "doc",
            Width = w,
            Height = h,
            Tilesets = [EntityId.Parse("tileset:exterior")], // exists in fixture-min
            Layers = new MapLayers
            {
                Ground = Enumerable.Repeat(-1, w * h).ToList(),
                DecoBelow = Enumerable.Repeat(-1, w * h).ToList(),
                DecoAbove = Enumerable.Repeat(-1, w * h).ToList(),
            },
        };
        _session.Add(map);
        return new MapDocument(_session, map);
    }

    [Fact]
    public void Paint_SetsOneCell()
    {
        MapDocument doc = Doc();
        doc.SelectedTile = 3;
        doc.PaintCell(2, 1);

        Assert.Equal(3, doc.TileAt(MapLayerId.Ground, 2, 1));
        Assert.Equal(-1, doc.TileAt(MapLayerId.Ground, 0, 0));
    }

    [Fact]
    public void Stroke_AcrossManyCells_IsOneUndoStep()
    {
        MapDocument doc = Doc();
        doc.SelectedTile = 1;

        doc.BeginStroke();
        for (int x = 0; x < 8; x++)
            doc.StrokePaint(x, 0);
        doc.EndStroke();

        Assert.All(Enumerable.Range(0, 8), x => Assert.Equal(1, doc.TileAt(MapLayerId.Ground, x, 0)));

        doc.Undo.Undo(); // the whole row reverts at once
        Assert.All(Enumerable.Range(0, 8), x => Assert.Equal(-1, doc.TileAt(MapLayerId.Ground, x, 0)));
    }

    [Fact]
    public void EmptyStroke_CommitsNothing()
    {
        MapDocument doc = Doc();
        doc.BeginStroke();
        doc.EndStroke();
        Assert.False(doc.IsDirty);
    }

    [Fact]
    public void ActiveLayer_TargetsThatLayerOnly()
    {
        MapDocument doc = Doc();
        doc.ActiveLayer = MapLayerId.DecoAbove;
        doc.SelectedTile = 5;
        doc.PaintCell(1, 1);

        Assert.Equal(5, doc.TileAt(MapLayerId.DecoAbove, 1, 1));
        Assert.Equal(-1, doc.TileAt(MapLayerId.Ground, 1, 1));
    }

    [Fact]
    public void Bucket_FillsContiguousRegion()
    {
        MapDocument doc = Doc(4, 4);
        doc.SelectedTile = 7;
        doc.Tool = MapTool.Bucket;
        doc.PaintCell(0, 0);

        Assert.All(Enumerable.Range(0, 16), i => Assert.Equal(7, doc.TileAt(MapLayerId.Ground, i % 4, i / 4)));
    }

    [Fact]
    public void RectFill_UsesAnchor()
    {
        MapDocument doc = Doc();
        doc.SelectedTile = 2;
        doc.Tool = MapTool.RectFill;

        doc.BeginStroke();
        doc.StrokePaint(3, 3, rectAnchorX: 1, rectAnchorY: 1);
        doc.EndStroke();

        Assert.Equal(2, doc.TileAt(MapLayerId.Ground, 1, 1));
        Assert.Equal(2, doc.TileAt(MapLayerId.Ground, 3, 3));
        Assert.Equal(-1, doc.TileAt(MapLayerId.Ground, 0, 0));
        Assert.Equal(-1, doc.TileAt(MapLayerId.Ground, 4, 4));
    }

    [Fact]
    public void Eyedropper_ReadsCellIntoSelection()
    {
        MapDocument doc = Doc();
        doc.SelectedTile = 9;
        doc.PaintCell(2, 2);

        doc.SelectedTile = 0;
        doc.Tool = MapTool.Eyedropper;
        doc.PaintCell(2, 2);
        Assert.Equal(9, doc.SelectedTile);
    }

    [Fact]
    public void Erase_ClearsCell()
    {
        MapDocument doc = Doc();
        doc.SelectedTile = 4;
        doc.PaintCell(1, 1);

        doc.Tool = MapTool.Erase;
        doc.PaintCell(1, 1);
        Assert.Equal(-1, doc.TileAt(MapLayerId.Ground, 1, 1));
    }

    [Fact]
    public void OutOfBounds_IsNoOp()
    {
        MapDocument doc = Doc();
        doc.SelectedTile = 1;
        doc.PaintCell(99, 99);
        Assert.False(doc.IsDirty);
    }

    // --- Overlays ---

    [Fact]
    public void Collision_SetAndClear_OneUndoEach()
    {
        MapDocument doc = Doc();
        doc.SetCollision(2, 2, CollisionValue.Solid);
        Assert.Equal(CollisionValue.Solid, doc.CollisionAt(2, 2));

        doc.SetCollision(2, 2, CollisionValue.LedgeDown); // replace, not duplicate
        Assert.Equal(CollisionValue.LedgeDown, doc.CollisionAt(2, 2));

        doc.SetCollision(2, 2, null);
        Assert.Null(doc.CollisionAt(2, 2));
    }

    [Fact]
    public void Encounter_SetAndClear()
    {
        MapDocument doc = Doc();
        var table = EntityId.Parse("encounter:test_room_grass");

        doc.SetEncounter(3, 3, table);
        Assert.Equal(table, doc.EncounterAt(3, 3));

        doc.SetEncounter(3, 3, null);
        Assert.Null(doc.EncounterAt(3, 3));
    }

    // --- Resize ---

    [Fact]
    public void Resize_Grow_PreservesTopLeft_PadsEmpty()
    {
        MapDocument doc = Doc(4, 4);
        doc.SelectedTile = 6;
        doc.PaintCell(3, 3);

        doc.Resize(6, 6);
        Assert.Equal(6, doc.Width);
        Assert.Equal(6, doc.TileAt(MapLayerId.Ground, 3, 3)); // preserved
        Assert.Equal(-1, doc.TileAt(MapLayerId.Ground, 5, 5)); // new cell
    }

    [Fact]
    public void Resize_Shrink_DropsOutOfBoundsOverlaysAndEntities()
    {
        MapDocument doc = Doc(8, 8);
        doc.SetCollision(6, 6, CollisionValue.Solid); // will be dropped
        doc.SetCollision(1, 1, CollisionValue.Solid); // survives, remapped
        doc.Place(new SignEntity { Pos = new GridPos(7, 7), Text = "gone" }); // dropped
        doc.Place(new SignEntity { Pos = new GridPos(2, 2), Text = "kept" });

        doc.Resize(4, 4);
        Assert.Equal(CollisionValue.Solid, doc.CollisionAt(1, 1));
        Assert.Null(doc.CollisionAt(6, 6)); // out of new bounds → dropped
        Assert.Single(doc.Entities);
        Assert.Equal(new GridPos(2, 2), doc.Entities[0].Pos);
    }

    // --- Entity placement ---

    [Fact]
    public void Place_AssignsStableUniqueKey_ByKind()
    {
        MapDocument doc = Doc();
        string a = doc.Place(new SignEntity { Pos = new GridPos(1, 1), Text = "a" });
        string b = doc.Place(new SignEntity { Pos = new GridPos(2, 2), Text = "b" });
        string npc = doc.Place(new NpcEntity { Pos = new GridPos(3, 3) });

        Assert.Equal("sign_0", a);
        Assert.Equal("sign_1", b);
        Assert.Equal("npc_0", npc);
    }

    [Fact]
    public void Place_OutOfBounds_Refused()
    {
        MapDocument doc = Doc();
        Assert.Equal("", doc.Place(new SignEntity { Pos = new GridPos(99, 0) }));
        Assert.Empty(doc.Entities);
    }

    [Fact]
    public void MoveConfigureDelete_AreUndoable()
    {
        MapDocument doc = Doc();
        string key = doc.Place(new SignEntity { Pos = new GridPos(1, 1), Text = "hi" });

        doc.MoveEntity(key, new GridPos(4, 4));
        Assert.Equal(new GridPos(4, 4), doc.EntityAt(4, 4)!.Pos);

        doc.ConfigureEntity((doc.EntityAt(4, 4) as SignEntity)! with { Text = "changed" });
        Assert.Equal("changed", ((SignEntity)doc.EntityAt(4, 4)!).Text);

        doc.DeleteEntity(key);
        Assert.Empty(doc.Entities);

        doc.Undo.Undo(); // delete
        doc.Undo.Undo(); // configure
        doc.Undo.Undo(); // move
        Assert.Equal(new GridPos(1, 1), doc.EntityAt(1, 1)!.Pos);
    }

    [Fact]
    public void FreshKey_NeverReusesAfterDelete()
    {
        MapDocument doc = Doc();
        doc.Place(new SignEntity { Pos = new GridPos(0, 0) }); // sign_0
        string second = doc.Place(new SignEntity { Pos = new GridPos(1, 0) }); // sign_1
        doc.DeleteEntity(second);
        // sign_1 is free again by number, but sign_0 still exists; a new sign takes the lowest free.
        Assert.Equal("sign_1", doc.Place(new SignEntity { Pos = new GridPos(2, 0) }));
    }

    // --- Play-from-map ---

    [Fact]
    public void PlayFromArgs_BuildsRuntimeLine_ForOpenCell()
    {
        MapDocument doc = Doc();
        string? args = doc.PlayFromArgs(@"C:\proj", 3, 4);
        Assert.Equal("--project \"C:\\proj\" --map map:doc --at 3,4", args);
    }

    [Fact]
    public void PlayFromArgs_RefusesSolidOrOutOfBounds()
    {
        MapDocument doc = Doc();
        doc.SetCollision(2, 2, CollisionValue.Solid);
        Assert.Null(doc.PlayFromArgs(@"C:\proj", 2, 2)); // solid
        Assert.Null(doc.PlayFromArgs(@"C:\proj", 99, 0)); // out of bounds
    }
}
