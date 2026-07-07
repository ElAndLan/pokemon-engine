using System.Collections;
using System.Reflection;
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
            foreach (EntityId reference in CollectRefs(entity, isRoot: true))
                if (!resolvable.Contains(reference))
                    yield return new ValidationIssue(Id, ValidationSeverity.Error, entity.Id,
                        $"References '{reference}', which does not exist.",
                        "Create the target entity or fix the reference.");

        foreach (EntityId reference in CollectRefs(project.Settings, isRoot: true))
            if (!resolvable.Contains(reference))
                yield return new ValidationIssue(Id, ValidationSeverity.Error, project.Settings.Id,
                    $"Project settings reference '{reference}', which does not exist.");
    }

    private static IEnumerable<EntityId> CollectRefs(object obj, bool isRoot)
    {
        foreach (PropertyInfo prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (isRoot && prop.Name == nameof(IEntity.Id))
                continue; // an entity's own id is a declaration, not a reference
            if (prop.GetIndexParameters().Length > 0)
                continue;

            foreach (EntityId id in FromValue(prop.GetValue(obj)))
                yield return id;
        }
    }

    private static IEnumerable<EntityId> FromValue(object? value)
    {
        switch (value)
        {
            case null:
            case string:
                yield break;
            case EntityId id:
                yield return id;
                break;
            case IEnumerable seq:
                foreach (object? item in seq)
                    foreach (EntityId id in FromValue(item))
                        yield return id;
                break;
            default:
                Type t = value.GetType();
                if (t.IsPrimitive || t.IsEnum || t.Namespace != "Cgm.Core.Model")
                    yield break;
                // A nested model record/struct (e.g. Evolution, NpcEntity, LearnsetEntry): recurse.
                foreach (EntityId id in CollectRefs(value, isRoot: false))
                    yield return id;
                break;
        }
    }
}
