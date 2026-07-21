using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation;

namespace Cgm.Runtime.Engine;

/// <summary>The owned aggregate handed to scene construction (ENGINE_RUNTIME_SPEC 16A step 8).
/// Scenes receive no source-mode flag and cannot branch on raw versus packed content.
/// <paramref name="ContentHash"/> identifies the content a save was written against: packs carry one
/// in their manifest, raw project mode has none, and an empty hash skips the save's content check
/// rather than blocking development.</summary>
public sealed record RuntimeContent(GameDb Db, Map StartMap, RuntimeConfig Config, string ContentHash = "");

/// <summary>Steps 2-7 of the 16A startup state machine: select one data source, canonicalize its
/// root, verify versions and hash, materialize one <see cref="GameDb"/>, and resolve the start
/// state. Failure stops immediately; later steps never run to accumulate diagnostics.</summary>
public static class BootLoader
{
    /// <summary>The runtime version a pack must require. Bumped only with a breaking host change.</summary>
    public const string RuntimeVersion = "1.0.0";

    public static bool TryLoad(BootArgs args, string exeDir, out RuntimeContent? content,
        out BootDiagnostic? error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(exeDir);
        content = null;
        try
        {
            return args.Exported
                ? TryLoadExported(exeDir, args.Debug, out content, out error)
                : TryLoadProject(args.ProjectPath!, args.Debug, out content, out error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Host paths stay out of the summary; the category and safe message are all release sees.
            return Fail(out error, RuntimeExit.Content, "content", $"Content could not be read: {ex.Message}");
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException
            or ArgumentException or FormatException)
        {
            return Fail(out error, RuntimeExit.Content, "content", $"Content is invalid: {ex.Message}");
        }
    }

    private static bool TryLoadProject(string projectPath, bool debug, out RuntimeContent? content,
        out BootDiagnostic? error)
    {
        content = null;
        if (!Directory.Exists(projectPath))
            return Fail(out error, RuntimeExit.Arguments, "arguments",
                File.Exists(projectPath) ? "Project path is a file, not a folder." : "Project folder does not exist.");

        Project project = ProjectLoader.Load(projectPath);
        // Raw mode uses the same validator and severity gate as pack compilation.
        ValidationReport report = Validator.Run(project);
        if (report.ErrorCount > 0)
            return Fail(out error, RuntimeExit.Content, "content",
                $"Project failed validation with {report.ErrorCount} error(s).",
                report.Issues.First(i => i.Severity == ValidationSeverity.Error).EntityId?.ToString());

        var config = new RuntimeConfig
        {
            GameName = project.Settings.Name,
            WindowTitle = project.Settings.Name,
            SaveDirName = RuntimeConfig.SafeSaveDir(project.Settings.Name),
            Debug = debug,
        };
        return TryComplete(GameDb.FromProject(project), config, "", out content, out error);
    }

    private static bool TryLoadExported(string exeDir, bool debug, out RuntimeContent? content,
        out BootDiagnostic? error)
    {
        content = null;
        string configPath = Path.Combine(exeDir, Exporter.ConfigFileName);
        if (!File.Exists(configPath))
            return Fail(out error, RuntimeExit.Arguments, "arguments",
                $"No {Exporter.ConfigFileName} beside the executable and no --project given.");

        RuntimeConfig config = CgmJson.Deserialize<RuntimeConfig>(File.ReadAllText(configPath));
        if (config.VirtualWidth <= 0 || config.VirtualHeight <= 0)
            return Fail(out error, RuntimeExit.Arguments, "arguments",
                "Runtime config virtual resolution must be positive.");
        if (config.SchemaVersion > SchemaVersions.Current)
            return Fail(out error, RuntimeExit.Content, "content",
                $"Config schema v{config.SchemaVersion} is newer than this runtime's v{SchemaVersions.Current}.");

        // Exported paths resolve against the config directory, never the working directory, and may
        // not leave the game folder — a config is content, so it cannot point the runtime anywhere.
        string packPath = Path.GetFullPath(Path.Combine(exeDir, config.PackPath));
        if (string.IsNullOrWhiteSpace(config.PackPath) || !Contains(exeDir, packPath))
            return Fail(out error, RuntimeExit.Arguments, "arguments", "Config pack path escapes the game folder.");
        if (!File.Exists(packPath))
            return Fail(out error, RuntimeExit.Arguments, "arguments", "Configured pack file does not exist.");

        PackManifest manifest;
        using (FileStream header = File.OpenRead(packPath))
            manifest = CgmPack.ReadManifest(header);
        if (manifest.PackFormatVersion != CgmPack.FormatVersion)
            return Fail(out error, RuntimeExit.Content, "content",
                $"Pack format v{manifest.PackFormatVersion} is not the supported v{CgmPack.FormatVersion}.");
        if (!string.Equals(manifest.RequiredRuntimeVersion, RuntimeVersion, StringComparison.Ordinal))
            return Fail(out error, RuntimeExit.Content, "content",
                $"Pack requires runtime {manifest.RequiredRuntimeVersion}; this runtime is {RuntimeVersion}.");

        // CgmPack.Read verifies the content hash before deserializing sections.
        GameDb db;
        using (FileStream body = File.OpenRead(packPath))
            db = CgmPack.Read(body);
        return TryComplete(db, config with { Debug = config.Debug || debug }, manifest.ContentHash,
            out content, out error);
    }

    /// <summary>Step 7: resolve and validate the start state. Never chooses a fallback entity.</summary>
    private static bool TryComplete(GameDb db, RuntimeConfig config, string contentHash,
        out RuntimeContent? content,
        out BootDiagnostic? error)
    {
        content = null;
        if (db.Settings.StartMap is not { } startMapId)
            return Fail(out error, RuntimeExit.Content, "content", "Project declares no start map.");
        if (db.Find<Map>(startMapId) is not { } startMap)
            return Fail(out error, RuntimeExit.Content, "content", "Start map is missing from the database.",
                startMapId.ToString());

        GridPos start = db.Settings.StartPos;
        if (start.X < 0 || start.Y < 0 || start.X >= startMap.Width || start.Y >= startMap.Height)
            return Fail(out error, RuntimeExit.Content, "content",
                $"Start position ({start.X},{start.Y}) is outside the {startMap.Width}x{startMap.Height} start map.",
                startMapId.ToString());

        content = new RuntimeContent(db, startMap, config, contentHash);
        error = null;
        return true;
    }

    /// <summary>True when <paramref name="candidate"/> is inside <paramref name="root"/>. The trailing
    /// separator matters: without it "C:\game" would also contain "C:\gameEvil".</summary>
    private static bool Contains(string root, string candidate)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(candidate).StartsWith(full, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Fail(out BootDiagnostic? error, RuntimeExit exit, string category, string summary,
        string? identifier = null)
    {
        error = new BootDiagnostic(exit, category, summary, identifier);
        return false;
    }
}
