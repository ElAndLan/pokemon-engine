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

        string packPath = Path.Combine(outFolder, PackFileName);
        using (FileStream fs = File.Create(packPath))
            CgmPack.Write(GameDb.FromProject(project), fs,
                new PackOptions(gameName, options.RequiredRuntimeVersion, options.BuildTimestampUtc));

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