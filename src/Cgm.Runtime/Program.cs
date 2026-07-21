using Cgm.Runtime.Engine;

namespace Cgm.Runtime;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (!BootArgs.TryParse(args, out BootArgs boot, out BootDiagnostic? error))
            return Fail(error!);

        if (!BootLoader.TryLoad(boot, AppContext.BaseDirectory, out RuntimeContent? content, out error))
            return Fail(error!);

        if (boot.Smoke)
        {
            Console.WriteLine($"[runtime] smoke passed: {content!.StartMap.Id}");
            return (int)RuntimeExit.Success;
        }

        // ponytail: the window still runs on the showcase host. Replacing it with BootScene over
        // RuntimeContent is 16B/16C work; 16A's contract is reaching a validated aggregate.
        ExportedGame? game = boot.Exported ? TryLoadExport() : null;
        using var host = new RuntimeHost(content!.Config.Debug, game);
        return host.Run();
    }

    private static int Fail(BootDiagnostic diagnostic)
    {
        Console.Error.WriteLine(diagnostic.Format());
        return (int)diagnostic.Exit;
    }

    private static ExportedGame? TryLoadExport()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        return File.Exists(configPath) ? ExportedGameBoot.Load(AppContext.BaseDirectory, configPath) : null;
    }
}
