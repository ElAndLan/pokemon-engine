using System.Text.Json;
using Cgm.Core.Model;
using Cgm.Core.Serialization;
using Cgm.Core.Validation;

namespace Cgm.Tools;

internal static class Program
{
    // Exit codes: 0 = valid, 1 = validation errors, 2 = usage/load failure.
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            PrintUsage();
            return 0;
        }

        return args[0] switch
        {
            "validate" => Validate(args[1..]),
            "export" => Export(args[1..]),
            _ => UnknownCommand(args[0]),
        };
    }

    // Exit codes: 0 = exported, 1 = validation errors blocked it, 2 = usage/load failure.
    private static int Export(string[] args)
    {
        string[] positional = [.. args.Where(a => !a.StartsWith("--", StringComparison.Ordinal))];
        if (positional.Length < 2)
        {
            Console.Error.WriteLine("export: usage is 'cgm export <project> <out> [--name X] [--debug] [--force]'.");
            return 2;
        }

        Project project;
        try
        {
            project = ProjectLoader.Load(positional[0]);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        {
            Console.Error.WriteLine($"Could not load project: {ex.Message}");
            return 2;
        }

        var options = new ExportOptions(
            GameName: ValueOf(args, "--name"),
            Debug: args.Contains("--debug"),
            OverrideValidation: args.Contains("--force"));

        try
        {
            ExportResult result = Exporter.ExportData(project, options, positional[1]);
            Console.WriteLine($"Exported {result.PackPath} and {result.ConfigPath}.");
            if (result.Validation.WarningCount > 0)
                Console.WriteLine($"({result.Validation.WarningCount} warning(s) — see 'cgm validate'.)");
            return 0;
        }
        catch (InvalidOperationException ex) // validation hard gate
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static string? ValueOf(string[] args, string flag)
    {
        int i = Array.IndexOf(args, flag);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static int Validate(string[] args)
    {
        bool asJson = args.Contains("--json");
        string? path = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
        if (path is null)
        {
            Console.Error.WriteLine("validate: missing <project> path.");
            return 2;
        }

        Project project;
        try
        {
            project = ProjectLoader.Load(path);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException)
        {
            Console.Error.WriteLine($"Could not load project: {ex.Message}");
            return 2;
        }

        ValidationReport report = Validator.Run(project);

        if (asJson)
        {
            Console.WriteLine(CgmJson.Serialize(report.Issues));
        }
        else
        {
            foreach (ValidationIssue issue in report.Issues)
                Console.WriteLine(issue);
            Console.WriteLine($"{report.ErrorCount} error(s), {report.WarningCount} warning(s).");
        }

        return report.HasErrors ? 1 : 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"cgm: unknown command '{command}'.");
        PrintUsage();
        return 2;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("cgm — Creature Game Maker command-line tools");
        Console.WriteLine();
        Console.WriteLine("Usage: cgm <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  validate <project> [--json]   Validate a project folder");
        Console.WriteLine("  export <project> <out>        Export a standalone game     (Phase 12)");
        Console.WriteLine("  --help                        Show this help");
    }
}
