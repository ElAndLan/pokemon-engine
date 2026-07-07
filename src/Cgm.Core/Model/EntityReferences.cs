using System.Collections;
using System.Reflection;

namespace Cgm.Core.Model;

/// <summary>
/// Reflection walk that yields every <see cref="EntityId"/> referenced anywhere in an object graph
/// (through nested records, structs, and collections). Used by the broken-reference validation rule
/// and by the Creator's "what references this?" check before a delete.
/// </summary>
public static class EntityReferences
{
    /// <param name="skipRootId">Skip the root's own <c>Id</c> property (a declaration, not a reference).</param>
    public static IEnumerable<EntityId> Collect(object root, bool skipRootId = true)
    {
        foreach (PropertyInfo prop in root.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (skipRootId && prop.Name == nameof(IEntity.Id))
                continue;
            if (prop.GetIndexParameters().Length > 0)
                continue;

            foreach (EntityId id in FromValue(prop.GetValue(root)))
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
                foreach (EntityId id in Collect(value, skipRootId: false)) // nested model record/struct
                    yield return id;
                break;
        }
    }
}
