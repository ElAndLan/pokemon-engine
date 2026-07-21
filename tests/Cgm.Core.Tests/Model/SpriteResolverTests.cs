using Cgm.Core.Model;

namespace Cgm.Core.Tests.Model;

/// <summary>Sprite ids are projections of sheet cells (DATA_SCHEMA.md §4.6-4.7), so cell geometry is
/// a Core rule. A wrong rectangle here draws the neighbouring tile without any error.</summary>
public sealed class SpriteResolverTests
{
    private static EntityId Sprite(string slug) => EntityId.Parse("sprite:" + slug);

    private static SpriteSheet Grid(int imageW = 64, int imageH = 32, int cell = 16,
        int offsetX = 0, int offsetY = 0, int spacingX = 0, int spacingY = 0,
        params SheetCell[] cells) => new()
        {
            Id = EntityId.Parse("sheet:test"), Name = "Test", Asset = "assets/test.png",
            ImageW = imageW, ImageH = imageH, Mode = SliceMode.Grid,
            CellW = cell, CellH = cell,
            OffsetX = offsetX, OffsetY = offsetY, SpacingX = spacingX, SpacingY = spacingY,
            Cells = cells,
        };

    private static SheetCell Cell(int index, string slug) =>
        new() { Index = index, SpriteId = Sprite(slug) };

    // --- Column arithmetic ---------------------------------------------------------

    [Theory]
    [InlineData(64, 16, 0, 0, 4)]     // exact fit
    [InlineData(70, 16, 0, 0, 4)]     // a partial trailing cell does not count
    [InlineData(64, 16, 16, 0, 3)]    // offset eats one column
    [InlineData(70, 16, 0, 2, 4)]     // 4 cells + 3 gaps = 70; the last needs no trailing gap
    [InlineData(69, 16, 0, 2, 3)]     // one pixel short of the fourth
    [InlineData(52, 16, 0, 2, 3)]     // 3 cells + 2 gaps exactly
    [InlineData(8, 16, 0, 0, 0)]      // cell wider than the image
    public void ColumnsAccountForOffsetAndSpacing(int imageW, int cell, int offsetX, int spacingX, int expected) =>
        Assert.Equal(expected, SpriteResolver.Columns(Grid(imageW, 32, cell, offsetX, 0, spacingX)));

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void ANonPositiveCellSizeYieldsNoColumns(int cell) =>
        Assert.Equal(0, SpriteResolver.Columns(Grid(cell: cell)));

    /// <summary>Without a recorded image size there is no column count, so nothing resolves rather
    /// than resolving against a guess.</summary>
    [Fact]
    public void AnUnsizedSheetYieldsNoColumns() =>
        Assert.Equal(0, SpriteResolver.Columns(Grid(imageW: 0)));

    // --- Cell rectangles ------------------------------------------------------------

    [Fact]
    public void GridCellsWrapAcrossRows()
    {
        SpriteSheet sheet = Grid(cells: [Cell(0, "a"), Cell(3, "b"), Cell(4, "c"), Cell(7, "d")]);
        var resolver = new SpriteResolver([sheet]);

        Assert.True(resolver.TryResolve(Sprite("a"), out _, out Rect a));
        Assert.True(resolver.TryResolve(Sprite("b"), out _, out Rect b));
        Assert.True(resolver.TryResolve(Sprite("c"), out _, out Rect c));
        Assert.True(resolver.TryResolve(Sprite("d"), out _, out Rect d));

        Assert.Equal(new Rect(0, 0, 16, 16), a);
        Assert.Equal(new Rect(48, 0, 16, 16), b);    // last column of row 0
        Assert.Equal(new Rect(0, 16, 16, 16), c);    // wraps to row 1
        Assert.Equal(new Rect(48, 16, 16, 16), d);
    }

    [Fact]
    public void OffsetAndSpacingShiftEveryCell()
    {
        SpriteSheet sheet = Grid(imageW: 68, imageH: 40, cell: 16, offsetX: 2, offsetY: 4,
            spacingX: 2, spacingY: 2, cells: [Cell(0, "a"), Cell(1, "b"), Cell(3, "c")]);
        var resolver = new SpriteResolver([sheet]);

        Assert.True(resolver.TryResolve(Sprite("a"), out _, out Rect a));
        Assert.True(resolver.TryResolve(Sprite("b"), out _, out Rect b));
        Assert.True(resolver.TryResolve(Sprite("c"), out _, out Rect c));

        Assert.Equal(new Rect(2, 4, 16, 16), a);
        Assert.Equal(new Rect(20, 4, 16, 16), b);    // 2 + 16 + 2
        Assert.Equal(new Rect(2, 22, 16, 16), c);    // 3 columns fit, so index 3 wraps
    }

    [Fact]
    public void RectsModeUsesTheAuthoredRectangle()
    {
        var sheet = new SpriteSheet
        {
            Id = EntityId.Parse("sheet:r"), Asset = "assets/r.png", ImageW = 64, ImageH = 64,
            Mode = SliceMode.Rects,
            Cells = [new SheetCell { Rect = new Rect(5, 7, 11, 13), SpriteId = Sprite("a") }],
        };

        Assert.True(new SpriteResolver([sheet]).TryResolve(Sprite("a"), out _, out Rect rect));
        Assert.Equal(new Rect(5, 7, 11, 13), rect);
    }

    [Fact]
    public void ARectsCellWithoutARectDoesNotResolve()
    {
        var sheet = new SpriteSheet
        {
            Id = EntityId.Parse("sheet:r"), Asset = "assets/r.png", ImageW = 64, ImageH = 64,
            Mode = SliceMode.Rects,
            Cells = [new SheetCell { Index = 0, SpriteId = Sprite("a") }],   // index is meaningless here
        };

        Assert.False(new SpriteResolver([sheet]).Contains(Sprite("a")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    public void AGridCellWithoutAUsableIndexDoesNotResolve(int? index)
    {
        SpriteSheet sheet = Grid(cells: [new SheetCell { Index = index, SpriteId = Sprite("a") }]);
        Assert.False(new SpriteResolver([sheet]).Contains(Sprite("a")));
    }

    // --- Lookup ---------------------------------------------------------------------

    [Fact]
    public void ResolvingReportsTheOwningSheet()
    {
        SpriteSheet sheet = Grid(cells: [Cell(0, "a")]);
        Assert.True(new SpriteResolver([sheet]).TryResolve(Sprite("a"), out SpriteSheet owner, out _));
        Assert.Equal(sheet.Id, owner.Id);
    }

    [Fact]
    public void AnUnknownSpriteDoesNotResolve()
    {
        var resolver = new SpriteResolver([Grid(cells: [Cell(0, "a")])]);
        Assert.False(resolver.TryResolve(Sprite("missing"), out _, out Rect rect));
        Assert.Equal(default, rect);
    }

    [Fact]
    public void AnEmptyResolverFindsNothing() =>
        Assert.False(new SpriteResolver([]).Contains(Sprite("a")));

    [Fact]
    public void NullSheetsAreRejected() =>
        Assert.Throws<ArgumentNullException>(() => new SpriteResolver(null!));

    // --- Bounds ---------------------------------------------------------------------

    [Theory]
    [InlineData(0, 0, 16, 16, true)]
    [InlineData(48, 16, 16, 16, true)]    // bottom-right corner exactly
    [InlineData(49, 16, 16, 16, false)]   // one past the right edge
    [InlineData(48, 17, 16, 16, false)]
    [InlineData(-1, 0, 16, 16, false)]
    [InlineData(0, -1, 16, 16, false)]
    [InlineData(0, 0, 0, 16, false)]      // empty
    [InlineData(0, 0, 16, 0, false)]
    public void InBoundsUsesHalfOpenRectangles(int x, int y, int w, int h, bool inside) =>
        Assert.Equal(inside, SpriteResolver.InBounds(Grid(64, 32), new Rect(x, y, w, h)));
}
