using Cgm.Core.Model;
using Cgm.Core.Validation;
using Cgm.Core.Validation.Rules;

namespace Cgm.Core.Tests.Validation;

/// <summary>A cell that slices outside its image draws the wrong pixels silently, so it has to be a
/// validation error rather than a runtime surprise (DATA_SCHEMA.md §4.6).</summary>
public sealed class SheetSliceRuleTests
{
    private static Project Project(params SpriteSheet[] sheets) =>
        new(new ProjectSettings { Name = "T" },
            sheets.ToDictionary(s => s.Id, s => (IEntity)s));

    private static SpriteSheet Sheet(
        string asset = "assets/a.png", int imageW = 64, int imageH = 32,
        int cellW = 16, int cellH = 16, SliceMode mode = SliceMode.Grid,
        params SheetCell[] cells) => new()
        {
            Id = EntityId.Parse("sheet:a"), Name = "A", Asset = asset,
            ImageW = imageW, ImageH = imageH, Mode = mode, CellW = cellW, CellH = cellH,
            Cells = cells,
        };

    private static SheetCell Cell(int index) =>
        new() { Index = index, SpriteId = EntityId.Parse("sprite:a") };

    private static IReadOnlyList<ValidationIssue> Check(Project project) =>
        [.. new SheetSliceRule().Check(project)];

    [Fact]
    public void AWellSlicedSheetPasses() =>
        Assert.Empty(Check(Project(Sheet(cells: [Cell(0), Cell(7)]))));

    [Fact]
    public void ASheetWithNoCellsPasses() =>
        Assert.Empty(Check(Project(Sheet())));

    [Theory]
    [InlineData("")]
    [InlineData("../outside.png")]
    [InlineData("C:/absolute.png")]
    public void AnUnsafeAssetPathIsAnError(string asset)
    {
        ValidationIssue issue = Assert.Single(Check(Project(Sheet(asset))));
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Contains("escapes the project folder", issue.Message);
    }

    /// <summary>A v9 sheet migrated forward has no image size. Reporting it is the whole reason the
    /// migration leaves the field at zero instead of guessing.</summary>
    [Theory]
    [InlineData(0, 32)]
    [InlineData(64, 0)]
    [InlineData(0, 0)]
    public void AnUnsizedImageIsAnError(int imageW, int imageH)
    {
        ValidationIssue issue = Assert.Single(Check(Project(Sheet(imageW: imageW, imageH: imageH))));
        Assert.Contains("re-import", issue.Message);
    }

    [Fact]
    public void AGridThatFitsNoColumnsIsAnError()
    {
        ValidationIssue issue = Assert.Single(Check(Project(Sheet(imageW: 8, cellW: 16))));
        Assert.Contains("fits no columns", issue.Message);
    }

    /// <summary>One structural fault reports once; the rule does not then also blame every cell.</summary>
    [Fact]
    public void AStructuralFaultDoesNotCascadePerCell() =>
        Assert.Single(Check(Project(Sheet(imageW: 8, cellW: 16, cells: [Cell(0), Cell(1), Cell(2)]))));

    [Fact]
    public void ACellPastTheLastRowIsAnError()
    {
        // 64x32 at 16px = 4 columns x 2 rows = indices 0..7.
        ValidationIssue issue = Assert.Single(Check(Project(Sheet(cells: [Cell(8)]))));
        Assert.Contains("outside the 64x32 image", issue.Message);
    }

    [Fact]
    public void AGridCellWithoutAnIndexIsAnError()
    {
        var sheet = Sheet(cells: [new SheetCell { SpriteId = EntityId.Parse("sprite:a") }]);
        Assert.Contains("neither a usable index nor a rect", Assert.Single(Check(Project(sheet))).Message);
    }

    [Fact]
    public void ARectReachingPastTheImageIsAnError()
    {
        var sheet = Sheet(mode: SliceMode.Rects, cells:
            [new SheetCell { Rect = new Rect(60, 0, 16, 16), SpriteId = EntityId.Parse("sprite:a") }]);

        Assert.Contains("outside the 64x32 image", Assert.Single(Check(Project(sheet))).Message);
    }

    [Fact]
    public void ARectFlushWithTheImageEdgePasses()
    {
        var sheet = Sheet(mode: SliceMode.Rects, cells:
            [new SheetCell { Rect = new Rect(48, 16, 16, 16), SpriteId = EntityId.Parse("sprite:a") }]);

        Assert.Empty(Check(Project(sheet)));
    }

    [Fact]
    public void TheRuleIsRegisteredWithTheValidator() =>
        Assert.Contains(Validator.Run(Project(Sheet(imageW: 0))).Issues, i => i.RuleId == "sheet-slice");
}
