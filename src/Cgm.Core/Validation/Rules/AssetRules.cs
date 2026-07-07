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
