using Cgm.Core.Model;

namespace Cgm.Core.Validation.Rules;

/// <summary>Animation clips need at least one frame, each with a positive duration
/// (ASSET_PIPELINE_SPEC v4). Frame sprite existence is covered by broken-reference.</summary>
public sealed class AnimationRule : IValidationRule
{
    public string Id => "animation";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (Animation anim in project.All<Animation>())
        {
            if (anim.Frames.Count == 0)
                yield return new ValidationIssue(Id, ValidationSeverity.Error, anim.Id,
                    "Animation has no frames.");

            foreach (AnimFrame frame in anim.Frames)
                if (frame.Ms <= 0)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, anim.Id,
                        $"Frame '{frame.Sprite}' has a non-positive duration ({frame.Ms} ms).");
        }
    }
}

/// <summary>A sheet must name a safe asset and slice into cells that actually exist inside that
/// image (DATA_SCHEMA.md §4.6). Caught here rather than at load: an out-of-bounds cell would sample
/// neighbouring art or nothing at all, which is a silently wrong picture rather than a crash.</summary>
public sealed class SheetSliceRule : IValidationRule
{
    public string Id => "sheet-slice";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (SpriteSheet sheet in project.All<SpriteSheet>())
        {
            if (AssetPath.Normalize(sheet.Asset).Length == 0)
            {
                yield return new ValidationIssue(Id, ValidationSeverity.Error, sheet.Id,
                    $"Asset path '{sheet.Asset}' is empty, absolute, or escapes the project folder.");
                continue;
            }

            if (sheet.ImageW <= 0 || sheet.ImageH <= 0)
            {
                yield return new ValidationIssue(Id, ValidationSeverity.Error, sheet.Id,
                    $"Image size {sheet.ImageW}x{sheet.ImageH} is unset; re-import the sheet to record it.");
                continue;
            }

            if (sheet.Mode == SliceMode.Grid && SpriteResolver.Columns(sheet) <= 0)
            {
                yield return new ValidationIssue(Id, ValidationSeverity.Error, sheet.Id,
                    $"Grid cell size {sheet.CellW}x{sheet.CellH} with offset {sheet.OffsetX} fits no "
                    + $"columns in a {sheet.ImageW}px-wide image.");
                continue;
            }

            foreach (SheetCell cell in sheet.Cells)
            {
                if (!SpriteResolver.TryRect(sheet, cell, out Rect rect))
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, sheet.Id,
                        $"Cell for sprite '{cell.SpriteId}' has neither a usable index nor a rect.");
                else if (!SpriteResolver.InBounds(sheet, rect))
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, sheet.Id,
                        $"Sprite '{cell.SpriteId}' slices {rect.X},{rect.Y} {rect.W}x{rect.H}, "
                        + $"outside the {sheet.ImageW}x{sheet.ImageH} image.");
            }
        }
    }
}

/// <summary>A sprite id may be defined by only one sheet cell — duplicate definitions make
/// references ambiguous (and slip past broken-reference, which dedupes the resolvable set).</summary>
public sealed class SpriteUniquenessRule : IValidationRule
{
    public string Id => "sprite-uniqueness";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        var owners = new Dictionary<EntityId, EntityId>();
        foreach (SpriteSheet sheet in project.All<SpriteSheet>())
            foreach (SheetCell cell in sheet.Cells)
                if (!owners.TryAdd(cell.SpriteId, sheet.Id))
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, sheet.Id,
                        $"Sprite '{cell.SpriteId}' is already defined by '{owners[cell.SpriteId]}'.");
    }
}
