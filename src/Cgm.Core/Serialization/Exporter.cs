using System.Security.Cryptography;
using Cgm.Core.Model;
using Cgm.Core.Validation;

namespace Cgm.Core.Serialization;

/// <summary>Export-time knobs; anything null falls back to the project's own values.</summary>
public sealed record ExportOptions(
    string? GameName = null,
    string? WindowTitle = null,
    string RequiredRuntimeVersion = "1.0.0",
    string? SaveDirName = null,
    bool Debug = false,
    bool OverrideValidation = false,
    DateTime? BuildTimestampUtc = null,
    string? TemplateFolder = null);

/// <summary>What an export produced: validation report plus written artifact paths.</summary>
public sealed record ExportResult(ValidationReport Validation, string PackPath, string ConfigPath, string? ExePath = null);

/// <summary>
/// Export operation (EXPORT_PIPELINE_SPEC): validate as a hard gate, optionally copy a runtime
/// template folder, then write <c>game.cgmpack</c> + <c>config.json</c> into the output folder.
/// Icon/version patching and the smoke-launch wrapper remain Creator/CI concerns.
/// </summary>
public static class Exporter
{
    public const string PackFileName = "game.cgmpack";
    public const string ConfigFileName = "config.json";
    public const string RuntimeTemplateExeName = "Cgm.Runtime.exe";

    public static ExportResult ExportData(Project project, ExportOptions options, string outFolder)
    {
        ValidationReport report = Validator.Run(project);
        if (report.HasErrors && !options.OverrideValidation)
            throw new InvalidOperationException(
                $"Export blocked: {report.ErrorCount} validation error(s). Fix them or override explicitly.");

        string gameName = options.GameName ?? project.Settings.Name;
        Directory.CreateDirectory(outFolder);
        string? exePath = options.TemplateFolder is null
            ? null
            : CopyTemplate(options.TemplateFolder, outFolder, gameName);

        IReadOnlyDictionary<string, byte[]> assets = CollectAssets(project);

        string packPath = Path.Combine(outFolder, PackFileName);
        using (FileStream fs = File.Create(packPath))
            CgmPack.Write(GameDb.FromProject(project), fs,
                new PackOptions(gameName, options.RequiredRuntimeVersion, options.BuildTimestampUtc),
                assets);

        var config = new RuntimeConfig
        {
            GameName = gameName,
            WindowTitle = options.WindowTitle ?? gameName,
            SaveDirName = options.SaveDirName ?? RuntimeConfig.SafeSaveDir(gameName),
            PackPath = PackFileName,
            Debug = options.Debug,
        };
        string configPath = Path.Combine(outFolder, ConfigFileName);
        File.WriteAllText(configPath, CgmJson.Serialize(config));

        return new ExportResult(report, packPath, configPath, exePath);
    }

    /// <summary>Reads every asset a sheet references, from the project folder, for embedding in the
    /// pack (EXPORT_PIPELINE_SPEC asset sections). A missing file or a mismatched
    /// <see cref="SpriteSheet.ContentHash"/> aborts the export: shipping a game whose art silently
    /// differs from what was authored is worse than not shipping it.</summary>
    private static IReadOnlyDictionary<string, byte[]> CollectAssets(Project project)
    {
        var assets = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (SpriteSheet sheet in project.All<SpriteSheet>())
        {
            string path = AssetPath.Normalize(sheet.Asset);
            if (path.Length == 0)
                throw new InvalidOperationException(
                    $"Sheet '{sheet.Id}' has an empty or unsafe asset path '{sheet.Asset}'.");
            if (assets.ContainsKey(path))
                continue;   // two sheets may legitimately slice the same image

            string full = AssetPath.Resolve(project.Root, path)
                ?? throw new InvalidOperationException(
                    $"Sheet '{sheet.Id}' references asset '{path}', but the project has no folder to read it from.");
            if (!File.Exists(full))
                throw new FileNotFoundException($"Sheet '{sheet.Id}' references missing asset '{path}'.", full);

            byte[] bytes = File.ReadAllBytes(full);
            string actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.IsNullOrEmpty(sheet.ContentHash)
                && !string.Equals(sheet.ContentHash, actual, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Asset '{path}' has changed since '{sheet.Id}' was authored "
                    + $"(expected {sheet.ContentHash}, found {actual}). Re-import the sheet.");

            assets[path] = bytes;
        }
        return assets;
    }

    private static string CopyTemplate(string templateFolder, string outFolder, string gameName)
    {
        if (!Directory.Exists(templateFolder))
            throw new DirectoryNotFoundException($"Runtime template folder not found: {templateFolder}");

        string sourceExe = Path.Combine(templateFolder, RuntimeTemplateExeName);
        if (!File.Exists(sourceExe))
            throw new FileNotFoundException($"Runtime template is missing {RuntimeTemplateExeName}.", sourceExe);

        foreach (string source in Directory.EnumerateFiles(templateFolder, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(templateFolder, source);
            string dest = Path.Combine(outFolder, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(source, dest, overwrite: true);
        }

        string copiedExe = Path.Combine(outFolder, RuntimeTemplateExeName);
        string exePath = Path.Combine(outFolder, SafeExeStem(gameName) + ".exe");
        if (!string.Equals(copiedExe, exePath, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(copiedExe, exePath, overwrite: true);
            File.Delete(copiedExe);
        }
        return exePath;
    }

    private static string SafeExeStem(string gameName)
    {
        string stem = RuntimeConfig.SafeSaveDir(gameName);
        foreach (char c in Path.GetInvalidFileNameChars())
            stem = stem.Replace(c, '_');
        return stem.Length == 0 ? "Game" : stem;
    }
}