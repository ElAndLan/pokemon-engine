using Cgm.Core.Model;

namespace Cgm.Runtime.Engine;

/// <summary>Process exit categories (ENGINE_RUNTIME_SPEC 16A error and diagnostic contract).</summary>
public enum RuntimeExit
{
    Success = 0,
    Arguments = 2,
    Content = 3,
    Asset = 4,
    Save = 5,
    Initialization = 6,
    SmokeAssertion = 10,
}

/// <summary>The single structured diagnostic emitted before a categorized exit. <paramref name="Identifier"/>
/// is content-relative; host paths never appear here so release output cannot leak them.</summary>
public sealed record BootDiagnostic(RuntimeExit Exit, string Category, string Summary, string? Identifier = null)
{
    public string Format() => Identifier is null
        ? $"[runtime] {Category} (exit {(int)Exit}): {Summary}"
        : $"[runtime] {Category} (exit {(int)Exit}): {Summary} [{Identifier}]";
}

/// <summary>Debug spawn override. Facing stays null when unspecified; 16D resolves it to the project
/// start facing, which is not known at parse time.</summary>
public sealed record SpawnOverride(EntityId Map, int X, int Y, Facing? Facing);

/// <summary>Parsed command line (ENGINE_RUNTIME_SPEC 16A command-line contract). Parsing creates no
/// window and reads no content: step 1 of the startup state machine.</summary>
public sealed record BootArgs(string? ProjectPath, bool Debug, bool Smoke, SpawnOverride? Spawn)
{
    private const string SpawnMap = "--spawn-map";
    private const string SpawnX = "--spawn-x";
    private const string SpawnY = "--spawn-y";
    private const string SpawnFacing = "--spawn-facing";

    private static readonly string[] ValueFlags = ["--project", SpawnMap, SpawnX, SpawnY, SpawnFacing];
    private static readonly string[] BoolFlags = ["--debug", "--smoke"];

    /// <summary>Exported mode is the absence of --project: the host reads the adjacent config.</summary>
    public bool Exported => ProjectPath is null;

    public static bool TryParse(IReadOnlyList<string> args, out BootArgs parsed, out BootDiagnostic? error)
    {
        ArgumentNullException.ThrowIfNull(args);
        parsed = new BootArgs(null, false, false, null);

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Count; i++)
        {
            string arg = args[i];
            if (BoolFlags.Contains(arg))
            {
                if (!flags.Add(arg))
                    return Fail(out error, $"Duplicate argument '{arg}'.");
                continue;
            }
            if (!ValueFlags.Contains(arg))
                return Fail(out error, $"Unknown argument '{arg}'.");
            if (i + 1 >= args.Count || ValueFlags.Contains(args[i + 1]) || BoolFlags.Contains(args[i + 1]))
                return Fail(out error, $"Argument '{arg}' requires a value.");
            if (!values.TryAdd(arg, args[++i]))
                return Fail(out error, $"Duplicate argument '{arg}'.");
        }

        string? project = values.GetValueOrDefault("--project");
        if (project is not null && string.IsNullOrWhiteSpace(project))
            return Fail(out error, "Argument '--project' requires a non-empty path.");
        bool debug = flags.Contains("--debug");

        if (!TryParseSpawn(values, project, debug, out SpawnOverride? spawn, out error))
            return false;

        // Relative project roots resolve against the working directory; exported roots resolve
        // against the executable, which only the loader knows, so it is left alone here.
        parsed = new BootArgs(project is null ? null : Path.GetFullPath(project), debug,
            flags.Contains("--smoke"), spawn);
        return true;
    }

    private static bool TryParseSpawn(Dictionary<string, string> values, string? project, bool debug,
        out SpawnOverride? spawn, out BootDiagnostic? error)
    {
        spawn = null;
        error = null;
        string[] present = [SpawnMap, SpawnX, SpawnY];
        int count = present.Count(values.ContainsKey);
        if (count == 0)
        {
            if (values.ContainsKey(SpawnFacing))
                return Fail(out error, $"'{SpawnFacing}' requires the full spawn argument set.");
            return true;
        }
        if (count < present.Length)
            return Fail(out error, $"Spawn requires all of {SpawnMap}, {SpawnX}, and {SpawnY}.");
        if (project is null || !debug)
            return Fail(out error, "Spawn arguments require both --project and --debug.");

        if (!EntityId.TryParse(values[SpawnMap], out EntityId map) || map.Category != EntityCategory.Map)
            return Fail(out error, $"'{SpawnMap}' requires a map entity ID.");
        if (!int.TryParse(values[SpawnX], out int x) || !int.TryParse(values[SpawnY], out int y) || x < 0 || y < 0)
            return Fail(out error, $"'{SpawnX}' and '{SpawnY}' require nonnegative integers.");

        Facing? facing = null;
        if (values.TryGetValue(SpawnFacing, out string? facingText))
        {
            // Named directions only; Enum.TryParse would otherwise accept "3" as Right.
            facing = facingText.ToLowerInvariant() switch
            {
                "down" => Facing.Down,
                "up" => Facing.Up,
                "left" => Facing.Left,
                "right" => Facing.Right,
                _ => null,
            };
            if (facing is null)
                return Fail(out error, $"'{SpawnFacing}' must be down, up, left, or right.");
        }

        spawn = new SpawnOverride(map, x, y, facing);
        return true;
    }

    private static bool Fail(out BootDiagnostic? error, string summary)
    {
        error = new BootDiagnostic(RuntimeExit.Arguments, "arguments", summary);
        return false;
    }
}
