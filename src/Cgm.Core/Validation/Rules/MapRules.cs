using Cgm.Core.Model;

namespace Cgm.Core.Validation.Rules;

/// <summary>Every placed entity needs a non-empty key that is unique within its map. Without this a
/// duplicate silently repoints saved flags at the wrong entity — the exact failure the key exists to
/// prevent — and an empty key makes an entity unaddressable by Runtime, saves, and diagnostics.</summary>
public sealed class MapEntityKeyRule : IValidationRule
{
    public string Id => "map-entity-key";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (Map map in project.All<Map>())
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < map.Entities.Count; i++)
            {
                string key = map.Entities[i].Key;
                if (string.IsNullOrWhiteSpace(key))
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, map.Id,
                        $"Entity {i} has no key.", "Give every placed entity a stable key.");
                else if (!seen.Add(key))
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, map.Id,
                        $"Entity key '{key}' is used more than once.", "Entity keys must be unique per map.");
            }
        }
    }
}

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
