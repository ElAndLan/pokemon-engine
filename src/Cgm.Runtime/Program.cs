using Cgm.Runtime.Engine;

namespace Cgm.Runtime;

internal static class Program
{
    public static int Main(string[] args)
    {
        if (!BootArgs.TryParse(args, out BootArgs boot, out BootDiagnostic? error)
            || !BootLoader.TryLoad(boot, AppContext.BaseDirectory, out RuntimeContent? content, out error))
        {
            Console.Error.WriteLine(error!.Format());
            return (int)error.Exit;
        }

        // Smoke runs the same boot and render path in a hidden window, then exits after one frame.
        using var host = new RuntimeHost(content!.Config.Debug, content, boot.Smoke);
        int exit = host.Run();
        if (boot.Smoke && exit == (int)RuntimeExit.Success)
            Console.WriteLine($"[runtime] smoke passed: {content.StartMap.Id}");
        return exit;
    }
}
