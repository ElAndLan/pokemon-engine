namespace Cgm.Runtime.Tests;

/// <summary>Resolves repo-relative paths by walking up to the solution file.</summary>
internal static class TestRepo
{
    public static string Root { get; } = FindRoot();

    public static string Sample(string relative) => Path.Combine(Root, "samples", relative);

    private static string FindRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CreatureGameMaker.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repo root (CreatureGameMaker.slnx).");
    }
}
