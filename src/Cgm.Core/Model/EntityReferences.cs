using System.Collections;
using System.Reflection;

namespace Cgm.Core.Model;

/// <summary>
/// Reflection walk that yields every <see cref="EntityId"/> referenced anywhere in an object graph
/// (through nested records, structs, and collections). Used by the broken-reference validation rule
/// and by the Creator's "what references this?" check before a delete. The path-carrying variant
/// names where each reference lives (e.g. <c>learnset[3].move</c>) for usage results and
/// validation field navigation.
/// </summary>
public static class EntityReferences
{
    /// <param name="skipRootId">Skip the root's own <c>Id</c> property (a declaration, not a reference).</param>
    public static IEnumerable<EntityId> Collect(object root, bool skipRootId = true) =>
        CollectWithPaths(root, skipRootId).Select(r => r.Id);

    /// <summary>Every referenced id with the camelCase path of the field holding it.</summary>
    public static IEnumerable<(EntityId Id, string Path)> CollectWithPaths(object root, bool skipRootId = true)
    {
        foreach (PropertyInfo prop in root.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (skipRootId && prop.Name == nameof(IEntity.Id))
                continue;
            if (prop.GetIndexParameters().Length > 0)
                continue;

            foreach ((EntityId id, string path) in FromValue(prop.GetValue(root), CamelCase(prop.Name)))
                yield return (id, path);
        }
    }

    private static IEnumerable<(EntityId Id, string Path)> FromValue(object? value, string path)
    {
        switch (value)
        {
            case null:
            case string:
                yield break;
            case EntityId id:
                yield return (id, path);
                break;
            case IDictionary dict:
                foreach (DictionaryEntry entry in dict)
                {
                    foreach ((EntityId id, string p) in FromValue(entry.Key, $"{path}[{entry.Key}]"))
                        yield return (id, p);
                    foreach ((EntityId id, string p) in FromValue(entry.Value, $"{path}[{entry.Key}]"))
                        yield return (id, p);
                }
                break;
            case IEnumerable seq:
            {
                int index = 0;
                foreach (object? item in seq)
                {
                    foreach ((EntityId id, string p) in FromValue(item, $"{path}[{index}]"))
                        yield return (id, p);
                    index++;
                }
                break;
            }
            default:
                Type t = value.GetType();
                if (t.IsPrimitive || t.IsEnum || t.Namespace != "Cgm.Core.Model")
                    yield break;
                foreach (PropertyInfo prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (prop.GetIndexParameters().Length > 0)
                        continue;
                    foreach ((EntityId id, string p) in FromValue(prop.GetValue(value), $"{path}.{CamelCase(prop.Name)}"))
                        yield return (id, p);
                }
                break;
        }
    }

    /// <summary>Paths use the serialized (camelCase) field names, matching what authors see in
    /// validation messages and DATA_SCHEMA.</summary>
    private static string CamelCase(string name) =>
        name.Length > 0 && char.IsUpper(name[0]) ? char.ToLowerInvariant(name[0]) + name[1..] : name;
}
