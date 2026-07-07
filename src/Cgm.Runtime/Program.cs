namespace Cgm.Runtime;

internal static class Program
{
    public static int Main(string[] args)
    {
        bool debug = args.Contains("--debug");
        using var host = new RuntimeHost(debug);
        return host.Run();
    }
}
