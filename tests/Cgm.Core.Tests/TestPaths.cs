namespace Cgm.Core.Tests;

/// <summary>Resolves repo-relative paths (samples/, tests/fixtures/) by walking up to the .slnx.</summary>
internal static class TestPaths
{
    public static string RepoRoot { get; } = FindRepoRoot();

    public static string Sample(string relative) => Path.Combine(RepoRoot, "samples", relative);

    public static string Fixture(string relative) => Path.Combine(RepoRoot, "tests", "fixtures", relative);

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CreatureGameMaker.slnx")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate repo root (CreatureGameMaker.slnx).");
    }
}
