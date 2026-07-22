using Cgm.Core.Model;

namespace Cgm.Core.Validation.Rules;

/// <summary>
/// Every <see cref="EntityId"/> referenced anywhere in the project must resolve. One reflection
/// walk covers all entities (and the project settings), present and future, instead of a hand
/// check per field. Resolvable targets = loaded entities + sprite ids projected from sheet cells
/// (sprites have no standalone files — DATA_SCHEMA §4.6). An entity's own <c>Id</c> is not a ref.
/// </summary>
public sealed class BrokenReferenceRule : IValidationRule
{
    public string Id => "broken-reference";

    public IEnumerable<ValidationIssue> Check(Project project)
    {
        var resolvable = new HashSet<EntityId>();
        foreach (IEntity e in project.Entities)
            resolvable.Add(e.Id);
        foreach (SpriteSheet sheet in project.All<SpriteSheet>())
            foreach (SheetCell cell in sheet.Cells)
                resolvable.Add(cell.SpriteId);

        foreach (IEntity entity in project.Entities)
            foreach ((EntityId reference, string path) in EntityReferences.CollectWithPaths(entity))
                if (!resolvable.Contains(reference))
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, entity.Id,
                        $"References '{reference}', which does not exist.",
                        "Create the target entity or fix the reference.", Field: path);

        foreach ((EntityId reference, string path) in EntityReferences.CollectWithPaths(project.Settings))
            if (!resolvable.Contains(reference))
                yield return new ValidationIssue(Id, ValidationSeverity.Error, project.Settings.Id,
                    $"Project settings reference '{reference}', which does not exist.", Field: path);
    }
}
