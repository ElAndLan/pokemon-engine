using Cgm.Runtime.Engine;

namespace Cgm.Runtime;

internal static class Program
{
    public static int Main(string[] args)
    {
        string? config = ValueOf(args, "--config");
        if (args.Contains("--smoke"))
            return Smoke(config);

        ExportedGame? game = TryLoadExport(config);
        bool debug = args.Contains("--debug") || game?.Config.Debug == true;
        using var host = new RuntimeHost(debug, game);
        return host.Run();
    }

    private static int Smoke(string? config)
    {
        try
        {
            ExportedGameBoot.Smoke(AppContext.BaseDirectory, config);
            Console.WriteLine("[runtime] smoke passed");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        {
            Console.Error.WriteLine($"[runtime] smoke failed: {ex.Message}");
            return 1;
        }
    }

    private static ExportedGame? TryLoadExport(string? config)
    {
        string configPath = config ?? Path.Combine(AppContext.BaseDirectory, "config.json");
        if (!File.Exists(configPath))
            return null;
        return ExportedGameBoot.Load(AppContext.BaseDirectory, configPath);
    }

    private static string? ValueOf(string[] args, string flag)
    {
        int i = Array.IndexOf(args, flag);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}