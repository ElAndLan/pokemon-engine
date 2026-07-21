namespace Cgm.Core.Model;

/// <summary>
/// The canonical form of a project-relative asset path (DATA_SCHEMA.md §4.6). Authored data is
/// untrusted input that ends up as a filesystem read, so one rule decides what a legal path is and
/// every consumer — pack writer, exporter, validator, runtime loader — asks this and nothing else.
/// </summary>
public static class AssetPath
{
    /// <summary>Returns the canonical form, or an empty string when the path is not a safe
    /// project-relative reference. Rejects absolute and rooted paths, and any traversal segment: a
    /// pack built from authored data must not be able to read or embed a file outside the project.</summary>
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        string canonical = path.Trim().Replace('\\', '/');
        if (canonical.StartsWith('/') || Path.IsPathRooted(canonical))
            return "";

        var segments = new List<string>();
        foreach (string segment in canonical.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
                continue;
            // Traversal is refused outright rather than resolved: "assets/../../x" has no legitimate
            // authoring meaning, and collapsing it would hide the intent from the error message.
            if (segment == "..")
                return "";
            segments.Add(segment);
        }

        return segments.Count == 0 ? "" : string.Join('/', segments);
    }

    /// <summary>Resolves a normalized path against a root for reading. Returns null when the path is
    /// unsafe, so a caller cannot accidentally open something outside <paramref name="root"/>.</summary>
    public static string? Resolve(string root, string? path)
    {
        ArgumentNullException.ThrowIfNull(root);
        string normalized = Normalize(path);
        return normalized.Length == 0 || root.Length == 0
            ? null
            : Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar));
    }
}
