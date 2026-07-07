using Cgm.Core.Model;
using Cgm.Creator.Assets;

namespace Cgm.Creator.Tests.Assets;

public sealed class SheetBuilderTests
{
    private static readonly EntityId SheetId = EntityId.Parse("sheet:overworld");

    private static ImageData Solid(int w, int h)
    {
        var op = new bool[w * h];
        Array.Fill(op, true);
        return new ImageData(w, h, op);
    }

    [Fact]
    public void Build_AllOpaque_MakesOneCellPerGridSlot()
    {
        SpriteSheet sheet = SheetBuilder.Build(SheetId, "assets/o.png", Solid(32, 32), new GridSpec(16, 16));
        Assert.Equal(4, sheet.Cells.Count);
        Assert.Equal("assets/o.png", sheet.Asset);
        Assert.Equal(16, sheet.CellW);
        Assert.Equal(EntityId.Parse("sprite:overworld_0"), sheet.Cells[0].SpriteId);
        Assert.Equal(new Rect(0, 0, 16, 16), sheet.Cells[0].Rect);
    }

    [Fact]
    public void Build_ExcludesFullyTransparentCells_ButKeepsGridIndex()
    {
        // 32×32, 16px grid. Make the top-left cell fully transparent.
        ImageData img = Solid(32, 32);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                img.Opaque[y * 32 + x] = false;

        SpriteSheet sheet = SheetBuilder.Build(SheetId, "a.png", img, new GridSpec(16, 16));
        Assert.Equal(3, sheet.Cells.Count);
        Assert.DoesNotContain(sheet.Cells, c => c.Index == 0);          // cell 0 dropped
        Assert.Contains(sheet.Cells, c => c.SpriteId == EntityId.Parse("sprite:overworld_1")); // ids stay grid-stable
    }

    [Fact]
    public void Build_ExcludeDisabled_KeepsEmptyCells()
    {
        var img = new ImageData(32, 32, new bool[32 * 32]); // all transparent
        SpriteSheet keep = SheetBuilder.Build(SheetId, "a.png", img, new GridSpec(16, 16), excludeTransparent: false);
        SpriteSheet drop = SheetBuilder.Build(SheetId, "a.png", img, new GridSpec(16, 16));
        Assert.Equal(4, keep.Cells.Count);
        Assert.Empty(drop.Cells);
    }

    [Fact]
    public void Build_RoundTripsThroughSerialization()
    {
        SpriteSheet sheet = SheetBuilder.Build(SheetId, "a.png", Solid(32, 16), new GridSpec(16, 16));
        var back = Cgm.Core.Serialization.CgmJson.Deserialize<SpriteSheet>(
            Cgm.Core.Serialization.CgmJson.SerializeEntity(sheet));
        Assert.Equal(sheet.Cells.Count, back.Cells.Count);
        Assert.Equal(sheet.Cells[0].SpriteId, back.Cells[0].SpriteId);
    }
}
