using Cgm.Runtime.Engine;

namespace Cgm.Runtime;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (!BootArgs.TryParse(args, out BootArgs boot, out BootDiagnostic? error))
            return Fail(error!);

        // ponytail: 16A step 1 (argument parsing) is complete; steps 2-8 still route through the
        // showcase boot below. --project raw-folder loading arrives with the loader in this package.
        if (!boot.Exported)
            return Fail(new BootDiagnostic(RuntimeExit.Arguments, "arguments",
                "--project raw-folder loading is not implemented yet."));

        if (boot.Smoke)
            return Smoke();

        ExportedGame? game = TryLoadExport();
        using var host = new RuntimeHost(boot.Debug || game?.Config.Debug == true, game);
        return host.Run();
    }

    private static int Fail(BootDiagnostic diagnostic)
    {
        Console.Error.WriteLine(diagnostic.Format());
        return (int)diagnostic.Exit;
    }

    private static int Smoke()
    {
        try
        {
            ExportedGameBoot.Smoke(AppContext.BaseDirectory);
            Console.WriteLine("[runtime] smoke passed");
            return (int)RuntimeExit.Success;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        {
            // ponytail: every smoke failure reports 10 because the showcase loader throws untyped
            // content errors. Splitting content (3) and asset (4) from a true smoke-invariant
            // failure (10) lands with the categorized loader in 16A steps 2-8.
            return Fail(new BootDiagnostic(RuntimeExit.SmokeAssertion, "smoke", ex.Message));
        }
    }

    private static ExportedGame? TryLoadExport()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        return File.Exists(configPath) ? ExportedGameBoot.Load(AppContext.BaseDirectory, configPath) : null;
    }
}
