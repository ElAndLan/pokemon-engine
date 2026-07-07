using Cgm.Core.Model;

namespace Cgm.Core.Validation.Rules;

/// <summary>A project must have exactly one player-start entity across all maps.</summary>
public sealed class PlayerStartRule : IValidationRule
{
    public string Id => "player-start";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        int count = project.All<Map>()
            .SelectMany(m => m.Entities)
            .OfType<PlayerStartEntity>()
            .Count();

        if (count != 1)
            yield return new ValidationIssue(Id, ValidationSeverity.Error, null,
                $"Project has {count} player-start entities; must be exactly 1.");
    }
}

/// <summary>A warp must land on a walkable (non-solid) tile in its target map. Existence/bounds of
/// the target are covered by broken-reference and warp-target; this checks the landing tile.</summary>
public sealed class WarpLandingRule : IValidationRule
{
    public string Id => "warp-landing";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (Map map in project.All<Map>())
            foreach (WarpEntity warp in map.Entities.OfType<WarpEntity>())
            {
                if (project.Find<Map>(warp.Target) is not { } target)
                    continue;

                List<Tileset> tilesets = target.Tilesets
                    .Select(project.Find<Tileset>).OfType<Tileset>().ToList();
                CollisionValue[] collision = MapCollision.Derive(target, tilesets);

                int idx = warp.TargetPos.Y * target.Width + warp.TargetPos.X;
                if (idx < 0 || idx >= collision.Length)
                    continue; // out-of-bounds is warp-target's job

                if (collision[idx] == CollisionValue.Solid)
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, map.Id,
                        $"Warp lands on a solid tile ({warp.TargetPos.X},{warp.TargetPos.Y}) in '{warp.Target}'.");
            }
    }
}
