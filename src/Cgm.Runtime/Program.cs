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

        if (boot.Smoke)
        {
            Console.WriteLine($"[runtime] smoke passed: {content!.StartMap.Id}");
            return (int)RuntimeExit.Success;
        }

        using var host = new RuntimeHost(content!.Config.Debug, content);
        return host.Run();
    }
}
