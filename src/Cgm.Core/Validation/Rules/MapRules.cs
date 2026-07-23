using Cgm.Core.Model;

namespace Cgm.Core.Validation.Rules;

/// <summary>Map layers must match map dimensions and every non-empty cell must resolve through the
/// map's current tileset order. This turns index-shift damage into an authoring error instead of a
/// silently blank or incorrect runtime tile.</summary>
public sealed class MapLayerShapeRule : IValidationRule
{
    public string Id => "map-layer-shape";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (Map map in project.All<Map>())
        {
            int expected = map.Width > 0 && map.Height > 0 ? map.Width * map.Height : 0;
            int paletteCount = map.Tilesets
                .Select(project.Find<Tileset>).OfType<Tileset>().Sum(t => t.Tiles.Count);
            foreach ((string name, IReadOnlyList<int> layer) in Layers(map))
            {
                if (layer.Count != 0 && layer.Count != expected)
                {
                    yield return Error(map.Id, name,
                        $"Layer '{name}' has {layer.Count} cells; expected {expected} for {map.Width}×{map.Height}.");
                    continue;
                }

                int bad = layer.Select((tile, index) => (tile, index))
                    .FirstOrDefault(x => x.tile < -1
                        || (map.Tilesets.Count > 0 && x.tile >= paletteCount)).index;
                if (layer.Any(tile => tile < -1
                    || (map.Tilesets.Count > 0 && tile >= paletteCount)))
                    yield return Error(map.Id, name,
                        $"Layer '{name}' contains a tile index outside -1..{paletteCount - 1} (first at cell {bad}).");
            }
        }
    }

    private static IEnumerable<(string Name, IReadOnlyList<int> Layer)> Layers(Map map)
    {
        yield return ("ground", map.Layers.Ground);
        yield return ("decoBelow", map.Layers.DecoBelow);
        yield return ("decoAbove", map.Layers.DecoAbove);
    }

    private ValidationIssue Error(EntityId map, string field, string message) =>
        new(Id, ValidationSeverity.Error, map, message,
            "Resize or repaint the map with tiles from its assigned tilesets.", $"layers.{field}");
}

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

/// <summary>Every authored world action must carry the fields its op needs and reference the right
/// entity category. Checked at author time so Runtime can dispatch on the op alone: an action that
/// reaches the engine is already complete, and Runtime never has to interpret or repair one.</summary>
public sealed class TriggerActionRule : IValidationRule
{
    public string Id => "trigger-action";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        foreach (Map map in project.All<Map>())
            foreach (TriggerEntity trigger in map.Entities.OfType<TriggerEntity>())
                foreach (ValidationIssue issue in Actions(project, map.Id, $"trigger '{trigger.Key}'", trigger.Actions))
                    yield return issue;

        foreach (MapObject obj in project.All<MapObject>())
            foreach (ValidationIssue issue in Actions(project, obj.Id, "interaction", obj.Interaction))
                yield return issue;
    }

    private IEnumerable<ValidationIssue> Actions(Project project, EntityId owner, string where,
        IReadOnlyList<TriggerAction> actions)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            TriggerAction action = actions[i];
            string at = $"{where} action {i}";

            if (!Enum.IsDefined(action.Op))
            {
                yield return Error(owner, $"{at} has an unknown op.");
                continue;
            }

            switch (action.Op)
            {
                case TriggerOp.Dialogue when string.IsNullOrWhiteSpace(action.Text):
                    yield return Error(owner, $"{at} is dialogue with no text.");
                    break;

                case TriggerOp.SetFlag or TriggerOp.ClearFlag when string.IsNullOrWhiteSpace(action.Flag):
                    yield return Error(owner, $"{at} is a flag op with no flag name.");
                    break;

                case TriggerOp.GiveItem:
                    if (action.Entity is not { } item || item.Category != EntityCategory.Item)
                        yield return Error(owner, $"{at} must reference an item entity.");
                    else if (project.Find<Item>(item) is null)
                        yield return Error(owner, $"{at} references missing item '{item}'.");
                    if (action.Value <= 0)
                        yield return Error(owner, $"{at} must give a positive quantity.");
                    break;

                case TriggerOp.StartBattle:
                    if (action.Entity is not { } trainer || trainer.Category != EntityCategory.Trainer)
                        yield return Error(owner, $"{at} must reference a trainer entity.");
                    else if (project.Find<Trainer>(trainer) is null)
                        yield return Error(owner, $"{at} references missing trainer '{trainer}'.");
                    break;
            }
        }
    }

    private ValidationIssue Error(EntityId owner, string message) =>
        new(Id, ValidationSeverity.Error, owner, message);
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
