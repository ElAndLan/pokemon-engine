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
    DateTime? BuildTimestampUtc = null);

/// <summary>What an export produced: the validation report and the two written artifact paths.</summary>
public sealed record ExportResult(ValidationReport Validation, string PackPath, string ConfigPath);

/// <summary>
/// The data half of export (EXPORT_PIPELINE_SPEC): validate as a hard gate, then write
/// <c>game.cgmpack</c> + <c>config.json</c> into the output folder. The exe template copy/patch and
/// smoke test are separate build/CI concerns. Reused by the Creator export screen and the CLI.
/// </summary>
public static class Exporter
{
    public const string PackFileName = "game.cgmpack";
    public const string ConfigFileName = "config.json";

    public static ExportResult ExportData(Project project, ExportOptions options, string outFolder)
    {
        ValidationReport report = Validator.Run(project);
        if (report.HasErrors && !options.OverrideValidation)
            throw new InvalidOperationException(
                $"Export blocked: {report.ErrorCount} validation error(s). Fix them or override explicitly.");

        Directory.CreateDirectory(outFolder);
        string gameName = options.GameName ?? project.Settings.Name;

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

        return new ExportResult(report, packPath, configPath);
    }
}
